using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEngine;

/// <summary>
/// Description: Synchronizes large custom textures over the network using progressive chunking.
/// Context: Attached to the Player prefab.
/// Justification: Prevents network lag and timeout disconnects when sending 1MB+ PNG files by breaking them into 16KB chunks.
/// </summary>
public class NetworkTextureTransfer : NetworkBehaviour
{
    [Header("Debug")]
    [Tooltip("Role: Prints this component's informational Debug.Log messages. Warnings and errors always print.")]
    [SerializeField] private bool _enableDebugLogs = false;

    // Settings
    // 16 KB per chunk to avoid saturating the network
    private const int CHUNK_SIZE = 16000;

    // Client-side storage used to reassemble the chunks

    private Dictionary<int, byte[]> _receivedChunks = new Dictionary<int, byte[]>();
    private int _totalExpectedChunks = 0;
    private int _chunksReceivedCount = 0;

    // Server-side cache for players joining mid-game (late joiners)
    private byte[] _serverFullTextureCache;

    /// <summary>
    /// Loads the saved eye texture from disk, applies it locally and starts streaming it to the server.
    /// </summary>
    public override void OnStartLocalPlayer()
    {
        // The local player reads its texture from disk
        string savedPath = PlayerPrefs.GetString("SelectedEyeTexture", "");


        if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
        {
            byte[] fileData = File.ReadAllBytes(savedPath);

            // Apply instantly for the local player (no latency)

            ApplyTextureFromBytes(fileData);

            // Start uploading to the server
            StartCoroutine(SendTextureRoutine(fileData));
        }
    }

    /// <summary>
    /// On remote copies, requests the cached texture from the server so late joiners receive it.
    /// </summary>
    public override void OnStartClient()
    {
        base.OnStartClient();

        // A freshly arrived remote copy asks the server for the texture
        if (!isLocalPlayer)
        {
            CmdRequestTexture();
        }
    }

    #region Upload (Owner -> Server)

    private IEnumerator SendTextureRoutine(byte[] fileData)
    {
        int totalChunks = Mathf.CeilToInt((float)fileData.Length / CHUNK_SIZE);
        if (_enableDebugLogs) Debug.Log($"[TextureTransfer] Sending texture ({fileData.Length} bytes) in {totalChunks} chunks...");

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int size = Mathf.Min(CHUNK_SIZE, fileData.Length - offset);


            byte[] chunk = new byte[size];
            System.Array.Copy(fileData, offset, chunk, 0, size);

            CmdSendChunk(i, totalChunks, chunk);

            // 0.05s pause between chunks so the game does not lag
            yield return new WaitForSeconds(0.05f);

        }
    }

    [Command]
    private void CmdSendChunk(int index, int total, byte[] chunkData)
    {
        // The server assembles its own copy for late joiners
        AssembleServerCache(index, total, chunkData);

        // The server immediately relays the chunk to everyone
        RpcReceiveChunk(index, total, chunkData);
    }

    #endregion

    #region Reception (Server -> All Clients)

    [ClientRpc]
    private void RpcReceiveChunk(int index, int total, byte[] chunkData)
    {
        // The local player already applied its image instantly, so ignore
        if (isLocalPlayer) return;

        ReceiveChunkLocally(index, total, chunkData);
    }

    private void ReceiveChunkLocally(int index, int total, byte[] chunkData)
    {
        if (_totalExpectedChunks == 0)
        {
            _totalExpectedChunks = total;
            _receivedChunks.Clear();
            _chunksReceivedCount = 0;
        }

        if (!_receivedChunks.ContainsKey(index))
        {
            _receivedChunks.Add(index, chunkData);
            _chunksReceivedCount++;

            // All chunks have arrived
            if (_chunksReceivedCount == _totalExpectedChunks)
            {
                AssembleAndApplyTextureOnClient();
            }
        }
    }

    private void AssembleAndApplyTextureOnClient()
    {
        int totalSize = 0;
        foreach (var chunk in _receivedChunks.Values) totalSize += chunk.Length;

        byte[] fullData = new byte[totalSize];
        int currentOffset = 0;


        for (int i = 0; i < _totalExpectedChunks; i++)
        {
            if (_receivedChunks.TryGetValue(i, out byte[] chunk))
            {
                System.Array.Copy(chunk, 0, fullData, currentOffset, chunk.Length);
                currentOffset += chunk.Length;
            }
        }

        if (_enableDebugLogs) Debug.Log($"[TextureTransfer] Texture received and assembled!");
        ApplyTextureFromBytes(fullData);

        // Release the network buffers
        _receivedChunks.Clear();
        _totalExpectedChunks = 0;
    }

    private void ApplyTextureFromBytes(byte[] imageData)
    {
        Texture2D eyeTex = new Texture2D(2, 2);
        if (eyeTex.LoadImage(imageData))
        {
            eyeTex.filterMode = FilterMode.Point; // Look Pixel-Art


            // SSOT: delegate to PlayerCustomization, which already handles clean material instancing
            PlayerCustomization customization = GetComponent<PlayerCustomization>();
            if (customization != null)
            {
                customization.ApplyLocalEyeTexture(eyeTex);
            }
            else
            {
                Debug.LogWarning("[TextureTransfer] PlayerCustomization not found on this object, cannot apply the texture.");
            }
        }
    }

    #endregion

    #region Late Joiners

    [Command(requiresAuthority = false)]
    private void CmdRequestTexture(NetworkConnectionToClient sender = null)
    {
        if (_serverFullTextureCache != null && _serverFullTextureCache.Length > 0)
        {
            // The server sends the texture only to the client that just joined
            StartCoroutine(ServerSendTextureToClientRoutine(sender, _serverFullTextureCache));
        }
    }

    private IEnumerator ServerSendTextureToClientRoutine(NetworkConnectionToClient target, byte[] fileData)
    {
        int totalChunks = Mathf.CeilToInt((float)fileData.Length / CHUNK_SIZE);
        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int size = Mathf.Min(CHUNK_SIZE, fileData.Length - offset);


            byte[] chunk = new byte[size];
            System.Array.Copy(fileData, offset, chunk, 0, size);

            TargetReceiveChunk(target, i, totalChunks, chunk);
            yield return new WaitForSeconds(0.05f);

        }
    }

    [TargetRpc]
    private void TargetReceiveChunk(NetworkConnection target, int index, int total, byte[] chunkData)
    {
        ReceiveChunkLocally(index, total, chunkData);
    }

    private Dictionary<int, byte[]> _serverTempChunks = new Dictionary<int, byte[]>();
    private void AssembleServerCache(int index, int total, byte[] chunkData)
    {
        if (!_serverTempChunks.ContainsKey(index))
        {
            _serverTempChunks.Add(index, chunkData);


            if (_serverTempChunks.Count == total)
            {
                int totalSize = 0;
                foreach (var c in _serverTempChunks.Values) totalSize += c.Length;
                _serverFullTextureCache = new byte[totalSize];


                int offset = 0;
                for (int i = 0; i < total; i++)
                {
                    System.Array.Copy(_serverTempChunks[i], 0, _serverFullTextureCache, offset, _serverTempChunks[i].Length);
                    offset += _serverTempChunks[i].Length;
                }
                _serverTempChunks.Clear();
            }
        }
    }
    #endregion
}

