#nullable enable

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public struct FolderSettings
{
    public string PrefabFolder;
    public string OutputFolder;
}

public struct CameraSettings
{
    public Vector3 Rotation;
    public Color Background;
    public float NearClip;
    public float FarClip;
    public bool AllowHDR;
    public bool AllowMSAA;
    public bool Orthographic;
}

public struct RenderingSettings
{
    public Vector3 PrefabRotation;
    public int Resolution;
    public float Padding;
}

public struct LightingSettings
{
    public Vector3 Rotation;
    public LightShadows Shadows;
    public float Intensity;
}

public struct IconForgeSettings
{
    public FolderSettings FolderSettings;
    public RenderingSettings RenderingSettings;
    public LightingSettings LightingSettings;
    public CameraSettings CameraSettings;
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

        if (!Directory.Exists(settings.FolderSettings.PrefabFolder))
        {
            Debug.LogError($"{_debugPrefix} Something went wrong, and prefab folder is missing!");
            return null;
        }

        if (!Directory.Exists(settings.FolderSettings.OutputFolder))
        {
            Debug.LogError($"{_debugPrefix} Something went wrong, and output folder is missing!");
            return null;
        }

        string[] guids = AssetDatabase.FindAssets(
            "t:Prefab",
            new[] { settings.FolderSettings.PrefabFolder }
        );

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
        light.transform.rotation = Quaternion.Euler(settings.LightingSettings.Rotation);
        light.shadows = settings.LightingSettings.Shadows;
        light.intensity = settings.LightingSettings.Intensity;

        GameObject cameraGO = new("TempCamera");
        Camera camera = cameraGO.AddComponent<Camera>();
        camera.allowHDR = settings.CameraSettings.AllowHDR;
        camera.allowMSAA = settings.CameraSettings.AllowMSAA;
        camera.orthographic = settings.CameraSettings.Orthographic;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = settings.CameraSettings.Background;
        camera.transform.rotation = Quaternion.identity;
        camera.nearClipPlane = settings.CameraSettings.NearClip;
        camera.farClipPlane = settings.CameraSettings.FarClip;

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

                go.transform.rotation = Quaternion.Euler(settings.RenderingSettings.PrefabRotation);

                string relativeSpritePath = GetRelativeSpritePath(
                    path,
                    settings.FolderSettings.PrefabFolder,
                    settings.FolderSettings.OutputFolder
                );

                string? directory = Path.GetDirectoryName(relativeSpritePath);

                if (!string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                CreateSprite(
                    go,
                    camera,
                    relativeSpritePath,
                    settings.RenderingSettings.Resolution,
                    1 + settings.RenderingSettings.Padding
                );

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
                settings.FolderSettings.OutputFolder,
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
                importer.spritePixelsPerUnit = settings.RenderingSettings.Resolution;
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
