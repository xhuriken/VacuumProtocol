using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Description: Handles the local saving and loading of custom eye textures drawn by the player.
/// Context: Used by the Lobby UI when exporting a drawing from the Texture Editor.
/// Justification: Separates disk I/O logic from UI controllers and network scripts.
/// </summary>
public static class CustomEyeTextureManager
{
    private const string FOLDER_NAME = "CustomEyes";

    public static string GetFolderPath()
    {
        string path = Path.Combine(Application.persistentDataPath, FOLDER_NAME);
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return path;
    }

    /// <summary>
    /// Description: Saves a painted Texture2D as a PNG to the local AppData folder.
    /// Context: Called when the user clicks 'Save' in the TextureEditorPanelUI.
    /// </summary>
    public static string SaveCustomEyeTexture(Texture2D texture)
    {
        if (texture == null) return null;

        try
        {
            byte[] bytes = texture.EncodeToPNG();
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"EyeTex_{timestamp}.png";
            string fullPath = Path.Combine(GetFolderPath(), fileName);

            File.WriteAllBytes(fullPath, bytes);
            Debug.Log($"[CustomEyeTextureManager] Saved eye texture to: {fullPath}");
            return fullPath;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CustomEyeTextureManager] Error saving eye texture: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// Description: Loads all previously saved custom eye PNGs from the local disk.
    /// Context: Called when initializing the 'Apparence' tab in the lobby.
    /// </summary>
    public static List<Texture2D> LoadAllCustomEyeTextures()
    {
        List<Texture2D> textures = new List<Texture2D>();
        string folderPath = GetFolderPath();

        try
        {
            string[] files = Directory.GetFiles(folderPath, "*.png");
            foreach (string file in files)
            {
                byte[] fileData = File.ReadAllBytes(file);
                Texture2D tex = new Texture2D(2, 2);
                if (tex.LoadImage(fileData)) // Auto-resizes the texture dimensions
                {
                    tex.name = Path.GetFileNameWithoutExtension(file);
                    textures.Add(tex);
                }
                else
                {
                    Debug.LogWarning($"[CustomEyeTextureManager] Failed to load image data from: {file}");
                    UnityEngine.Object.Destroy(tex);
                }
            }
            Debug.Log($"[CustomEyeTextureManager] Loaded {textures.Count} custom eye textures from disk.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[CustomEyeTextureManager] Error loading custom eye textures: {e.Message}");
        }

        return textures;
    }
}

