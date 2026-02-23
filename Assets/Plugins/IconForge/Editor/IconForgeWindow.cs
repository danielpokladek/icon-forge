#nullable enable

using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class IconForgeWindow : EditorWindow
{
    private const string PREFAB_FOLDER_KEY = "IconForgePrefabFolder";
    private const string OUTPUT_FOLDER_KEY = "IconForgeOutputFolder";
    private const string RESOLUTION_KEY = "IconForgeResolution";
    private const string PADDING_KEY = "IconForgePadding";
    private const string ROTATION_X_KEY = "IconForgeRotationX";
    private const string ROTATION_Y_KEY = "IconForgeRotationY";
    private const string ROTATION_Z_KEY = "IconForgeRotationZ";
    private const string ORTHOGRAPHIC_KEY = "IconForgeOrthographic";
    private const string RUN_POST_PROCESSOR_KEY = "IconForgeRunPostProcessor";
    private const string POST_PROCESSOR_KEY = "IconForgePostProcessor";
    private const string LIGHT_ROTATION_X_KEY = "IconForgeLightRotationX";
    private const string LIGHT_ROTATION_Y_KEY = "IconForgeLightRotationY";
    private const string LIGHT_ROTATION_Z_KEY = "IconForgeLightRotationZ";
    private const string LIGHT_INTENSITY_KEY = "IconForgeLightIntensity";
    private const string LIGHT_SHADOWS_KEY = "IconForgeLightShadows";
    private const string ALLOW_HDR_KEY = "IconForgeAllowHDR";
    private const string ALLOW_MSAA_KEY = "IconForgeAllowMSAA";
    private const string BACKGROUND_COLOR_KEY = "IconForgeBackgroundColor";
    private const string CAMERA_ROTATION_X_KEY = "IconForgeCameraRotationX";
    private const string CAMERA_ROTATION_Y_KEY = "IconForgeCameraRotationY";
    private const string CAMERA_ROTATION_Z_KEY = "IconForgeCameraRotationZ";
    private const string NEAR_CLIP_KEY = "IconForgeNearClip";
    private const string FAR_CLIP_KEY = "IconForgeFarClip";

    private static bool RUN_POST_PROCESSOR = false;
    private static IconForgePostProcessor? POST_PROCESSOR;

    private static DefaultAsset? PREFAB_FOLDER;
    private static DefaultAsset? OUTPUT_FOLDER;

    private static Vector3 LIGHT_ROTATION = new(50, 30, 0);
    private static float LIGHT_INTENSITY = 1.2f;
    private static LightShadows LIGHT_SHADOWS = LightShadows.None;

    private static bool ALLOW_HDR = false;
    private static bool ALLOW_MSAA = false;
    private static Color BACKGROUND_COLOR = Color.clear;
    private static Vector3 CAMERA_ROTATION = Vector3.zero;
    private static float NEAR_CLIP = 0.01f;
    private static float FAR_CLIP = 100f;
    private static bool ORTHOGRAPHIC = true;

    private static Vector3 PREFAB_ROTATION = new(0, 90, 0);
    private static int SPRITE_RESOLUTION = 256;
    private static float SPRITE_PADDING = 0.15f;

    private static string GetStyleSheetPath(ScriptableObject caller)
    {
        var thisScript = MonoScript.FromScriptableObject(caller);
        var scriptPath = AssetDatabase.GetAssetPath(thisScript);
        var directory = Path.GetDirectoryName(scriptPath);
        var ussPath = Path.Combine(directory, "IconForge.uss");
        return ussPath.Replace("\\", "/");
    }

    private void OnEnable()
    {
        LoadSettings();
    }

    [MenuItem("Tools/IconForge")]
    public static void ShowWindow()
    {
        IconForgeWindow wnd = GetWindow<IconForgeWindow>();
        wnd.titleContent = new GUIContent("Icon Forge");
    }

    public void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        var styleSheetPath = GetStyleSheetPath(this);
        StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);

        if (styleSheet != null)
        {
            root.styleSheets.Add(styleSheet);
        }
        else
        {
            Debug.LogWarning($"Unable to find style sheet! {styleSheetPath}");
        }

        var scrollView = new ScrollView() { mode = ScrollViewMode.Vertical };
        root.Add(scrollView);

        Foldout settingsFoldout = new() { text = "Settings", value = true };
        settingsFoldout.AddToClassList("component-style-foldout");
        scrollView.Add(settingsFoldout);

        SetupPostProcessorSettings(settingsFoldout);

        Foldout foldersFoldout = new() { text = "Folders", value = true };
        foldersFoldout.AddToClassList("component-style-foldout");
        settingsFoldout.Add(foldersFoldout);

        SetupFolderSettings(foldersFoldout);

        Foldout cameraFoldout = new() { text = "Camera", value = true };
        cameraFoldout.AddToClassList("component-style-foldout");
        settingsFoldout.Add(cameraFoldout);

        SetupCameraSettings(cameraFoldout);

        Foldout lightFoldout = new() { text = "Lighting", value = true };
        lightFoldout.AddToClassList("component-style-foldout");
        settingsFoldout.Add(lightFoldout);

        SetupLightingSettings(lightFoldout);

        Foldout renderingFoldout = new() { text = "Rendering", value = true };
        renderingFoldout.AddToClassList("component-style-foldout");
        settingsFoldout.Add(renderingFoldout);

        SetupRenderingSettings(renderingFoldout);

        var generateButton = new Button() { text = "Generate Prefab Icons" };
        root.Add(generateButton);
        generateButton.RegisterCallback<ClickEvent>(
            (e) =>
            {
                if (PREFAB_FOLDER == null || OUTPUT_FOLDER == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both folders!", "OK");
                    return;
                }

                FolderSettings folderSettings = new()
                {
                    PrefabFolder = AssetDatabase.GetAssetPath(PREFAB_FOLDER),
                    OutputFolder = AssetDatabase.GetAssetPath(OUTPUT_FOLDER),
                };

                CameraSettings cameraSettings = new()
                {
                    AllowHDR = ALLOW_HDR,
                    AllowMSAA = ALLOW_MSAA,
                    Background = BACKGROUND_COLOR,
                    FarClip = FAR_CLIP,
                    NearClip = NEAR_CLIP,
                    Orthographic = ORTHOGRAPHIC,
                    Rotation = CAMERA_ROTATION,
                };

                LightingSettings lightingSettings = new()
                {
                    Intensity = LIGHT_INTENSITY,
                    Rotation = LIGHT_ROTATION,
                    Shadows = LIGHT_SHADOWS,
                };

                RenderingSettings renderingSettings = new()
                {
                    Resolution = SPRITE_RESOLUTION,
                    Padding = SPRITE_PADDING,
                    PrefabRotation = PREFAB_ROTATION,
                };

                var settings = new IconForgeSettings
                {
                    FolderSettings = folderSettings,
                    CameraSettings = cameraSettings,
                    LightingSettings = lightingSettings,
                    RenderingSettings = renderingSettings,
                };

                var results = IconForge.Generate(settings);

                if (RUN_POST_PROCESSOR && POST_PROCESSOR != null)
                {
                    POST_PROCESSOR.OnGenerationComplete(results);
                }
            }
        );
    }

    private void SaveSettings()
    {
        // Post processor settings
        EditorPrefs.SetBool(RUN_POST_PROCESSOR_KEY, RUN_POST_PROCESSOR);
        EditorPrefs.SetString(
            POST_PROCESSOR_KEY,
            POST_PROCESSOR != null ? AssetDatabase.GetAssetPath(POST_PROCESSOR) : ""
        );

        // Folder settings
        EditorPrefs.SetString(
            PREFAB_FOLDER_KEY,
            PREFAB_FOLDER != null ? AssetDatabase.GetAssetPath(PREFAB_FOLDER) : ""
        );
        EditorPrefs.SetString(
            OUTPUT_FOLDER_KEY,
            OUTPUT_FOLDER != null ? AssetDatabase.GetAssetPath(OUTPUT_FOLDER) : ""
        );

        // Rendering settings
        EditorPrefs.SetInt(RESOLUTION_KEY, SPRITE_RESOLUTION);
        EditorPrefs.SetFloat(PADDING_KEY, SPRITE_PADDING);
        EditorPrefs.SetFloat(ROTATION_X_KEY, PREFAB_ROTATION.x);
        EditorPrefs.SetFloat(ROTATION_Y_KEY, PREFAB_ROTATION.y);
        EditorPrefs.SetFloat(ROTATION_Z_KEY, PREFAB_ROTATION.z);

        // Light settings
        EditorPrefs.SetFloat(LIGHT_ROTATION_X_KEY, LIGHT_ROTATION.x);
        EditorPrefs.SetFloat(LIGHT_ROTATION_Y_KEY, LIGHT_ROTATION.y);
        EditorPrefs.SetFloat(LIGHT_ROTATION_Z_KEY, LIGHT_ROTATION.z);
        EditorPrefs.SetFloat(LIGHT_INTENSITY_KEY, LIGHT_INTENSITY);
        EditorPrefs.SetInt(LIGHT_SHADOWS_KEY, (int)LIGHT_SHADOWS);

        // Camera settings
        EditorPrefs.SetBool(ALLOW_HDR_KEY, ALLOW_HDR);
        EditorPrefs.SetBool(ALLOW_MSAA_KEY, ALLOW_MSAA);
        EditorPrefs.SetString(BACKGROUND_COLOR_KEY, BACKGROUND_COLOR.ToString());
        EditorPrefs.SetFloat(CAMERA_ROTATION_X_KEY, CAMERA_ROTATION.x);
        EditorPrefs.SetFloat(CAMERA_ROTATION_Y_KEY, CAMERA_ROTATION.y);
        EditorPrefs.SetFloat(CAMERA_ROTATION_Z_KEY, CAMERA_ROTATION.z);
        EditorPrefs.SetFloat(NEAR_CLIP_KEY, NEAR_CLIP);
        EditorPrefs.SetFloat(FAR_CLIP_KEY, FAR_CLIP);
        EditorPrefs.SetBool(ORTHOGRAPHIC_KEY, ORTHOGRAPHIC);
    }

    private void LoadSettings()
    {
        // Post processor settings
        string postProcessorPath = EditorPrefs.GetString(POST_PROCESSOR_KEY, "");
        POST_PROCESSOR = string.IsNullOrEmpty(postProcessorPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<IconForgePostProcessor>(postProcessorPath);
        RUN_POST_PROCESSOR = EditorPrefs.GetBool(RUN_POST_PROCESSOR_KEY, true);

        // Folder settings
        string prefabPath = EditorPrefs.GetString(PREFAB_FOLDER_KEY, "");
        PREFAB_FOLDER = string.IsNullOrEmpty(prefabPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<DefaultAsset>(prefabPath);

        string outputPath = EditorPrefs.GetString(OUTPUT_FOLDER_KEY, "");
        OUTPUT_FOLDER = string.IsNullOrEmpty(outputPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<DefaultAsset>(outputPath);

        // Rendering settings
        SPRITE_RESOLUTION = EditorPrefs.GetInt(RESOLUTION_KEY, 512);
        SPRITE_PADDING = EditorPrefs.GetFloat(PADDING_KEY, 0.15f);
        PREFAB_ROTATION = new(
            EditorPrefs.GetFloat(ROTATION_X_KEY, 0f),
            EditorPrefs.GetFloat(ROTATION_Y_KEY, 90f),
            EditorPrefs.GetFloat(ROTATION_Z_KEY, 0f)
        );

        // Lighting settings
        LIGHT_ROTATION = new(
            EditorPrefs.GetFloat(LIGHT_ROTATION_X_KEY, 50f),
            EditorPrefs.GetFloat(LIGHT_ROTATION_Y_KEY, 30f),
            EditorPrefs.GetFloat(LIGHT_ROTATION_Z_KEY, 0)
        );
        LIGHT_INTENSITY = EditorPrefs.GetFloat(LIGHT_INTENSITY_KEY, 1.2f);
        LIGHT_SHADOWS = (LightShadows)EditorPrefs.GetInt(LIGHT_SHADOWS_KEY, 0);

        ALLOW_HDR = EditorPrefs.GetBool(ALLOW_HDR_KEY, false);
        ALLOW_MSAA = EditorPrefs.GetBool(ALLOW_MSAA_KEY, false);

        var background = EditorPrefs.GetString(BACKGROUND_COLOR_KEY);
        var hasParsedColor = ColorUtility.TryParseHtmlString(background, out Color color);

        BACKGROUND_COLOR = hasParsedColor ? color : Color.clear;
        CAMERA_ROTATION = new(
            EditorPrefs.GetFloat(CAMERA_ROTATION_X_KEY, 0),
            EditorPrefs.GetFloat(CAMERA_ROTATION_Y_KEY, 0),
            EditorPrefs.GetFloat(CAMERA_ROTATION_Z_KEY, 0)
        );
        NEAR_CLIP = EditorPrefs.GetFloat(NEAR_CLIP_KEY, 0.01f);
        FAR_CLIP = EditorPrefs.GetFloat(FAR_CLIP_KEY, 100f);
        ORTHOGRAPHIC = EditorPrefs.GetBool(ORTHOGRAPHIC_KEY, true);
    }

    private void SetupPostProcessorSettings(Foldout parent)
    {
        var postProcessorContent = new VisualElement();
        parent.Add(postProcessorContent);

        var runPostProcessor = new Toggle()
        {
            label = "Run Post Processor",
            value = RUN_POST_PROCESSOR,
        };
        postProcessorContent.Add(runPostProcessor);

        var postProcessor = new ObjectField()
        {
            label = "Post Processor",
            objectType = typeof(IconForgePostProcessor),
            allowSceneObjects = false,
            value = POST_PROCESSOR,
        };
        postProcessor.RegisterValueChangedCallback(
            (e) =>
            {
                POST_PROCESSOR = (IconForgePostProcessor)e.newValue;
                SaveSettings();
            }
        );

        if (RUN_POST_PROCESSOR)
        {
            parent.Add(postProcessor);
        }

        runPostProcessor.RegisterValueChangedCallback(
            (e) =>
            {
                if (e.newValue)
                {
                    postProcessorContent.Add(postProcessor);
                }
                else
                {
                    if (postProcessor.parent != null)
                    {
                        postProcessorContent.Remove(postProcessor);
                    }
                }

                RUN_POST_PROCESSOR = e.newValue;
                SaveSettings();
            }
        );
    }

    private void SetupFolderSettings(Foldout parent)
    {
        var prefabFolder = new ObjectField()
        {
            label = "Prefab Folder",
            objectType = typeof(DefaultAsset),
            allowSceneObjects = false,
            value = PREFAB_FOLDER,
        };
        parent.Add(prefabFolder);
        prefabFolder.RegisterValueChangedCallback(
            (e) =>
            {
                PREFAB_FOLDER = (DefaultAsset)e.newValue;
                SaveSettings();
            }
        );

        var outputFolder = new ObjectField()
        {
            label = "Output Field",
            objectType = typeof(DefaultAsset),
            allowSceneObjects = false,
            value = OUTPUT_FOLDER,
        };
        parent.Add(outputFolder);
        outputFolder.RegisterValueChangedCallback(
            (e) =>
            {
                OUTPUT_FOLDER = (DefaultAsset)e.newValue;
                SaveSettings();
            }
        );
    }

    private void SetupCameraSettings(Foldout parent)
    {
        var rotation = new Vector3Field() { label = "Rotation", value = CAMERA_ROTATION };
        rotation.RegisterValueChangedCallback(
            (e) =>
            {
                CAMERA_ROTATION = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(rotation);

        var background = new ColorField() { label = "Background", value = BACKGROUND_COLOR };
        background.RegisterValueChangedCallback(
            (e) =>
            {
                BACKGROUND_COLOR = e.newValue;
            }
        );
        parent.Add(background);

        var nearClip = new FloatField() { label = "Near Clip", value = NEAR_CLIP };
        nearClip.RegisterValueChangedCallback(
            (e) =>
            {
                NEAR_CLIP = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(nearClip);

        var farClip = new FloatField() { label = "Far Clip", value = FAR_CLIP };
        farClip.RegisterValueChangedCallback(
            (e) =>
            {
                FAR_CLIP = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(farClip);

        var allowHDR = new Toggle() { label = "Allow HDR", value = ALLOW_HDR };
        allowHDR.RegisterValueChangedCallback(
            (e) =>
            {
                ALLOW_HDR = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(allowHDR);

        var allowMSAA = new Toggle() { label = "Allow MSAA", value = ALLOW_MSAA };
        allowMSAA.RegisterValueChangedCallback(
            (e) =>
            {
                ALLOW_MSAA = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(allowMSAA);

        var orthographic = new Toggle() { label = "Orthographic", value = ORTHOGRAPHIC };
        orthographic.RegisterValueChangedCallback(
            (e) =>
            {
                ORTHOGRAPHIC = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(orthographic);
    }

    private void SetupLightingSettings(Foldout parent)
    {
        var rotation = new Vector3Field() { label = "Rotation", value = LIGHT_ROTATION };
        rotation.RegisterValueChangedCallback(
            (e) =>
            {
                LIGHT_ROTATION = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(rotation);

        var intensity = new FloatField() { label = "Intensity", value = LIGHT_INTENSITY };
        intensity.RegisterValueChangedCallback(
            (e) =>
            {
                LIGHT_INTENSITY = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(intensity);

        var shadows = new DropdownField() { label = "Shadows" };
        shadows.choices.Add("None");
        shadows.choices.Add("Hard");
        shadows.choices.Add("Soft");
        shadows.index = (int)LIGHT_SHADOWS;
        shadows.RegisterValueChangedCallback(
            (_) =>
            {
                LIGHT_SHADOWS = (LightShadows)shadows.index;
                SaveSettings();
            }
        );
        parent.Add(shadows);
    }

    private void SetupRenderingSettings(Foldout parent)
    {
        var prefabRotation = new Vector3Field()
        {
            label = "Prefab Rotation",
            value = PREFAB_ROTATION,
        };
        prefabRotation.RegisterValueChangedCallback(
            (e) =>
            {
                PREFAB_ROTATION = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(prefabRotation);

        var spriteResolution = new IntegerField()
        {
            label = "Sprite Resolution",
            value = SPRITE_RESOLUTION,
        };
        spriteResolution.RegisterValueChangedCallback(
            (e) =>
            {
                SPRITE_RESOLUTION = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(spriteResolution);

        var spritePadding = new FloatField()
        {
            label = "Sprite Padding",
            value = SPRITE_PADDING,
            tooltip =
                "This is a multiplier value - for example, 0.2f will result in 1/5th larger image.",
        };
        spritePadding.RegisterValueChangedCallback(
            (e) =>
            {
                SPRITE_PADDING = e.newValue;
                SaveSettings();
            }
        );
        parent.Add(spritePadding);
    }
}
