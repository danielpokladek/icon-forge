#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct IconForgeSettings
{
    public string PrefabFolder;
    public string OutputFolder;
    public int Resolution;
    public float Padding;
    public Vector3 Rotation;
    public bool IsOrthographic;
    public bool AutoAssignSprites;
}

public struct GeneratedSpriteData
{
    public string GUID;
    public string PrefabPath;
    public string SpritePath;
}

public static class IconForge
{
    private const string _debugPrefix = "IconForge:";

    public static List<GeneratedSpriteData>? Generate(IconForgeSettings settings)
    {
        // Remove any current selection, as it can cause issues.
        // Additionally, lock the selection until tool is complete.
        Selection.activeObject = null;
        ActiveEditorTracker.sharedTracker.isLocked = true;

        if (!Directory.Exists(settings.OutputFolder))
        {
            Debug.LogError($"{_debugPrefix} Something went wrong, and output folder is missing!");
            return null;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { settings.PrefabFolder });

        Scene originalScene = SceneManager.GetActiveScene();
        Scene tempScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive
        );
        SceneManager.SetActiveScene(tempScene);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.white;

        GameObject lightGO = new("TempLight");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50, 30, 0);
        light.shadows = LightShadows.None;
        light.intensity = 1.2f;

        GameObject cameraGO = new("TempCamera");
        Camera camera = cameraGO.AddComponent<Camera>();

        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.orthographic = settings.IsOrthographic;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = Color.clear;

        camera.transform.rotation = Quaternion.identity;

        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;

        var results = GenerateSpritesFromGuids(guids, settings, camera);

        if (cameraGO != null)
            Object.DestroyImmediate(cameraGO);

        if (lightGO != null)
            Object.DestroyImmediate(lightGO);

        SceneManager.SetActiveScene(originalScene);
        EditorSceneManager.CloseScene(tempScene, true);

        ConfigureTextureImportSettings(settings);

        ActiveEditorTracker.sharedTracker.isLocked = false;

        Debug.Log($"{_debugPrefix} Sprite generation complete!");

        return results;
    }

    private static List<GeneratedSpriteData> GenerateSpritesFromGuids(
        string[] guids,
        IconForgeSettings settings,
        Camera camera
    )
    {
        List<GeneratedSpriteData> results = new();
        int totalFiles = guids.Length;

        AssetDatabase.StartAssetEditing();

        try
        {
            for (int i = 0; i < totalFiles; i++)
            {
                var guid = guids[i];

                EditorUtility.DisplayProgressBar(
                    _debugPrefix,
                    $"Generating Texture {i + 1}/{totalFiles}",
                    (float)i / totalFiles
                );

                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab == null)
                {
                    Debug.LogWarning($"{_debugPrefix} Skipped invalid prefab at path: {path}");
                    continue;
                }

                GameObject go = Object.Instantiate(prefab);

                if (go == null)
                {
                    Debug.LogWarning($"{_debugPrefix} Skipped invalid prefab at path: {path}");
                    continue;
                }

                go.transform.rotation = Quaternion.Euler(settings.Rotation);

                string relativeSpritePath = GetRelativeSpritePath(
                    path,
                    settings.PrefabFolder,
                    settings.OutputFolder
                );

                string? directory = Path.GetDirectoryName(relativeSpritePath);

                if (!string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                CreateSprite(go, camera, relativeSpritePath, settings.Resolution, settings.Padding);

                Object.DestroyImmediate(go);

                results.Add(
                    new GeneratedSpriteData
                    {
                        GUID = guid,
                        PrefabPath = path,
                        SpritePath = relativeSpritePath,
                    }
                );
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        return results;
    }

    private static void CreateSprite(
        GameObject prefabInstance,
        Camera camera,
        string fullSavePath,
        int resolution,
        float padding
    )
    {
        prefabInstance.transform.position = Vector3.zero;

        Bounds bounds = CalculateBounds(prefabInstance);

        float cameraSize = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        camera.orthographicSize = cameraSize * padding;

        camera.transform.position = bounds.center + Vector3.back * 10f;

        RenderTexture rt = new(resolution, resolution, 24);
        camera.targetTexture = rt;

        Texture2D screenshot = new(resolution, resolution, TextureFormat.ARGB32, false);
        camera.Render();

        RenderTexture.active = rt;

        screenshot.ReadPixels(new(0, 0, resolution, resolution), 0, 0);
        screenshot.Apply();

        byte[] bytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(fullSavePath, bytes);

        camera.targetTexture = null;
        RenderTexture.active = null;

        rt.Release();

        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(screenshot);
    }

    private static void ConfigureTextureImportSettings(IconForgeSettings settings)
    {
        AssetDatabase.StartAssetEditing();
        List<string> assetsToReserialize = new();

        try
        {
            var files = Directory.GetFiles(
                settings.OutputFolder,
                "*.png",
                SearchOption.AllDirectories
            );

            foreach (var file in files)
            {
                TextureImporter? importer = (TextureImporter)AssetImporter.GetAtPath(file);

                if (importer == null)
                {
                    Debug.Log($"{_debugPrefix} Could not find importer for: {file}");
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = settings.Resolution;
                importer.alphaIsTransparency = true;

                assetsToReserialize.Add(file);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();

            AssetDatabase.ForceReserializeAssets(assetsToReserialize);

            AssetDatabase.Refresh();

            EditorUtility.UnloadUnusedAssetsImmediate();
        }

        EditorUtility.ClearProgressBar();
    }

    private static Bounds CalculateBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning(
                $"[UI SCREENSHOT]: No renderers found for prefab: {go.name}! Defaulting to 1x1x1 bounds."
            );

            return new(go.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        return bounds;
    }

    private static string GetRelativeSpritePath(
        string path,
        string prefabFolder,
        string outputFolder
    )
    {
        string relativePath = path.Substring(prefabFolder.Length).TrimStart('/', '\\');
        relativePath = Path.ChangeExtension(relativePath, ".png");

        return Path.Combine(outputFolder, relativePath);
    }
}
