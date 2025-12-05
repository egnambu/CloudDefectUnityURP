using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class AnimationConfigurator : EditorWindow
{
    // Directory
    private string targetFolder = "Assets/";
    private List<ModelImporter> foundModels = new List<ModelImporter>();
    private Vector2 scrollPos;

    // Toggle which settings to apply
    private bool applyLoopTime, applyLoopPose;
    private bool applyLockRotation, applyLockHeightY, applyLockPositionXZ;
    private bool applyRotationBasis, applyHeightBasis, applyPositionBasis;

    // Values to apply
    private bool loopTime = true;
    private bool loopPose = true;
    private bool lockRootRotation = true;
    private bool lockRootHeightY = true;
    private bool lockRootPositionXZ = true;

    private enum RootBasis { Original, BodyOrientation }
    private RootBasis rotationBasis = RootBasis.Original;
    private RootBasis heightBasis = RootBasis.Original;
    private RootBasis positionBasis = RootBasis.Original;

    [MenuItem("Tools/Animation Configurator")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimationConfigurator>("Animation Configurator");
        window.minSize = new Vector2(400, 500);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Animation Configurator", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Batch update animation settings for all models in a folder.", MessageType.Info);

        EditorGUILayout.Space(10);

        // === FOLDER SELECTION ===
        EditorGUILayout.LabelField("Target Folder", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        targetFolder = EditorGUILayout.TextField(targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string path = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
            if (!string.IsNullOrEmpty(path))
            {
                if (path.StartsWith(Application.dataPath))
                    targetFolder = "Assets" + path.Substring(Application.dataPath.Length);
                else
                    Debug.LogWarning("Please select a folder inside the Assets directory.");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Scan Folder", GUILayout.Height(30)))
        {
            ScanFolder();
        }

        EditorGUILayout.LabelField($"Found: {foundModels.Count} model(s) with animations", EditorStyles.miniLabel);

        EditorGUILayout.Space(15);

        // === SETTINGS TO APPLY ===
        EditorGUILayout.LabelField("Settings to Apply", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Check the box to APPLY that setting. Unchecked = leave unchanged.", MessageType.None);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Loop Settings
        DrawHeader("Loop Settings");
        DrawToggleSetting(ref applyLoopTime, ref loopTime, "Loop Time");
        DrawToggleSetting(ref applyLoopPose, ref loopPose, "Loop Pose");

        EditorGUILayout.Space(10);

        // Root Transform Rotation
        DrawHeader("Root Transform Rotation");
        DrawToggleSetting(ref applyLockRotation, ref lockRootRotation, "Bake Into Pose");
        DrawEnumSetting(ref applyRotationBasis, ref rotationBasis, "Based Upon");

        EditorGUILayout.Space(10);

        // Root Transform Position (Y)
        DrawHeader("Root Transform Position (Y)");
        DrawToggleSetting(ref applyLockHeightY, ref lockRootHeightY, "Bake Into Pose");
        DrawEnumSetting(ref applyHeightBasis, ref heightBasis, "Based Upon");

        EditorGUILayout.Space(10);

        // Root Transform Position (XZ)
        DrawHeader("Root Transform Position (XZ)");
        DrawToggleSetting(ref applyLockPositionXZ, ref lockRootPositionXZ, "Bake Into Pose");
        DrawEnumSetting(ref applyPositionBasis, ref positionBasis, "Based Upon");

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(15);

        // === QUICK PRESETS ===
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("In-Place Animation"))
        {
            SetPreset(true, true, true, true, true, RootBasis.Original, RootBasis.Original, RootBasis.Original);
        }
        if (GUILayout.Button("Root Motion"))
        {
            SetPreset(true, false, false, false, false, RootBasis.Original, RootBasis.Original, RootBasis.Original);
        }
        if (GUILayout.Button("Clear All"))
        {
            applyLoopTime = applyLoopPose = false;
            applyLockRotation = applyLockHeightY = applyLockPositionXZ = false;
            applyRotationBasis = applyHeightBasis = applyPositionBasis = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(15);

        // === APPLY BUTTON ===
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Apply Settings to All Models", GUILayout.Height(40)))
        {
            ApplySettings();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);
    }

    void DrawHeader(string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
    }

    void DrawToggleSetting(ref bool apply, ref bool value, string label)
    {
        EditorGUILayout.BeginHorizontal();
        apply = EditorGUILayout.Toggle(apply, GUILayout.Width(20));
        EditorGUI.BeginDisabledGroup(!apply);
        value = EditorGUILayout.Toggle(label, value);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    void DrawEnumSetting(ref bool apply, ref RootBasis value, string label)
    {
        EditorGUILayout.BeginHorizontal();
        apply = EditorGUILayout.Toggle(apply, GUILayout.Width(20));
        EditorGUI.BeginDisabledGroup(!apply);
        value = (RootBasis)EditorGUILayout.EnumPopup(label, value);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    void SetPreset(bool loop, bool loopP, bool lockRot, bool lockY, bool lockXZ, 
                   RootBasis rotBasis, RootBasis yBasis, RootBasis xzBasis)
    {
        applyLoopTime = applyLoopPose = true;
        applyLockRotation = applyLockHeightY = applyLockPositionXZ = true;
        applyRotationBasis = applyHeightBasis = applyPositionBasis = true;

        loopTime = loop;
        loopPose = loopP;
        lockRootRotation = lockRot;
        lockRootHeightY = lockY;
        lockRootPositionXZ = lockXZ;
        rotationBasis = rotBasis;
        heightBasis = yBasis;
        positionBasis = xzBasis;
    }

    void ScanFolder()
    {
        foundModels.Clear();

        if (!AssetDatabase.IsValidFolder(targetFolder))
        {
            Debug.LogError($"AnimationConfigurator: '{targetFolder}' is not a valid folder.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { targetFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer != null)
            {
                // Check if it has animations
                var clips = importer.clipAnimations;
                if (clips == null || clips.Length == 0)
                    clips = importer.defaultClipAnimations;

                if (clips != null && clips.Length > 0)
                {
                    foundModels.Add(importer);
                }
            }
        }

        Debug.Log($"AnimationConfigurator: Found {foundModels.Count} models with animations in '{targetFolder}'");
    }

    void ApplySettings()
    {
        if (foundModels.Count == 0)
        {
            Debug.LogWarning("AnimationConfigurator: No models found. Click 'Scan Folder' first.");
            return;
        }

        int count = 0;

        foreach (var importer in foundModels)
        {
            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;

            if (clips == null || clips.Length == 0)
                continue;

            bool modified = false;

            for (int i = 0; i < clips.Length; i++)
            {
                // Loop Settings
                if (applyLoopTime) { clips[i].loopTime = loopTime; modified = true; }
                if (applyLoopPose) { clips[i].loopPose = loopPose; modified = true; }

                // Root Rotation
                if (applyLockRotation) { clips[i].lockRootRotation = lockRootRotation; modified = true; }
                if (applyRotationBasis)
                {
                    clips[i].keepOriginalOrientation = (rotationBasis == RootBasis.Original);
                    modified = true;
                }

                // Root Position Y
                if (applyLockHeightY) { clips[i].lockRootHeightY = lockRootHeightY; modified = true; }
                if (applyHeightBasis)
                {
                    clips[i].keepOriginalPositionY = (heightBasis == RootBasis.Original);
                    modified = true;
                }

                // Root Position XZ
                if (applyLockPositionXZ) { clips[i].lockRootPositionXZ = lockRootPositionXZ; modified = true; }
                if (applyPositionBasis)
                {
                    clips[i].keepOriginalPositionXZ = (positionBasis == RootBasis.Original);
                    modified = true;
                }
            }

            if (modified)
            {
                importer.clipAnimations = clips;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                count++;
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"AnimationConfigurator: Updated {count} model(s)!");
    }
}
