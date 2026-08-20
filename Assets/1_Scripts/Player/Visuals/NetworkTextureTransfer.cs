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
    // Settings
    // 16 Ko par morceau pour éviter de saturer le réseau
    private const int CHUNK_SIZE = 16000;

    // Stockage côté client pour assembler les morceaux

    private Dictionary<int, byte[]> _receivedChunks = new Dictionary<int, byte[]>();
    private int _totalExpectedChunks = 0;
    private int _chunksReceivedCount = 0;

    // Stockage côté serveur pour les joueurs qui rejoignent en cours de partie (Late Joiners)
    private byte[] _serverFullTextureCache;

    public override void OnStartLocalPlayer()
    {
        // Le joueur local lit sa texture depuis le disque
        string savedPath = PlayerPrefs.GetString("SelectedEyeTexture", "");


        if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
        {
            byte[] fileData = File.ReadAllBytes(savedPath);

            // On applique instantanément pour le joueur local (pas de latence)

            ApplyTextureFromBytes(fileData);

            // On démarre l'envoi vers le serveur
            StartCoroutine(SendTextureRoutine(fileData));
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Si on est un client distant qui vient d'arriver, on demande la texture au serveur
        if (!isLocalPlayer)
        {
            CmdRequestTexture();
        }
    }

    #region Envoi (Propriétaire -> Serveur)

    private IEnumerator SendTextureRoutine(byte[] fileData)
    {
        int totalChunks = Mathf.CeilToInt((float)fileData.Length / CHUNK_SIZE);
        Debug.Log($"[TextureTransfer] Envoi de la texture ({fileData.Length} octets) en {totalChunks} morceaux...");

        for (int i = 0; i < totalChunks; i++)
        {
            int offset = i * CHUNK_SIZE;
            int size = Mathf.Min(CHUNK_SIZE, fileData.Length - offset);


            byte[] chunk = new byte[size];
            System.Array.Copy(fileData, offset, chunk, 0, size);

            CmdSendChunk(i, totalChunks, chunk);

            // Pause de 0.05s entre chaque morceau pour ne pas faire lagger le jeu
            yield return new WaitForSeconds(0.05f);

        }
    }

    [Command]
    private void CmdSendChunk(int index, int total, byte[] chunkData)
    {
        // Le serveur assemble sa propre copie pour les retardataires
        AssembleServerCache(index, total, chunkData);

        // Le serveur redistribue immédiatement le morceau à tout le monde
        RpcReceiveChunk(index, total, chunkData);
    }

    #endregion

    #region Réception (Serveur -> Tous les Clients)

    [ClientRpc]
    private void RpcReceiveChunk(int index, int total, byte[] chunkData)
    {
        // Le joueur local a déjà appliqué son image instantanément, on ignore
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

            // Si tous les morceaux sont arrivés
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

        Debug.Log($"[TextureTransfer] Texture reçue et assemblée !");
        ApplyTextureFromBytes(fullData);

        // Nettoyage de la mémoire réseau
        _receivedChunks.Clear();
        _totalExpectedChunks = 0;
    }

    private void ApplyTextureFromBytes(byte[] imageData)
    {
        Texture2D eyeTex = new Texture2D(2, 2);
        if (eyeTex.LoadImage(imageData))
        {
            eyeTex.filterMode = FilterMode.Point; // Look Pixel-Art


            // SSOT: Centralisation vers PlayerCustomization qui gère déjà l'instanciation propre des matériaux
            PlayerCustomization customization = GetComponent<PlayerCustomization>();
            if (customization != null)
            {
                customization.ApplyLocalEyeTexture(eyeTex);
            }
            else
            {
                Debug.LogWarning("[TextureTransfer] PlayerCustomization introuvable sur ce composant, impossible d'appliquer la texture.");
            }
        }
    }

    #endregion

    #region Late Joiners (Les retardataires)

    [Command(requiresAuthority = false)]
    private void CmdRequestTexture(NetworkConnectionToClient sender = null)
    {
        if (_serverFullTextureCache != null && _serverFullTextureCache.Length > 0)
        {
            // Le serveur renvoie la texture uniquement à la personne qui vient d'arriver
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

