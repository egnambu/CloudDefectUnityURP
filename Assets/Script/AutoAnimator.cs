using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class AutoAnimator : MonoBehaviour
{
    [Header("Avatar Source")]
    [Tooltip("The humanoid avatar to apply to all models")]
    public Avatar humanoidAvatar;

    [Header("Model Prefabs")]
    [Tooltip("Drag model files (FBX/etc) that contain animation clips")]
    public List<GameObject> modelPrefabs = new List<GameObject>();

    [Header("Options")]
    [Tooltip("Enable looping for all clips")]
    public bool loopClips = true;

#if UNITY_EDITOR
    [ContextMenu("Setup All Models")]
    public void SetupAllModels()
    {
        if (humanoidAvatar == null)
        {
            Debug.LogError("AutoAnimator: Please assign a humanoid avatar!");
            return;
        }

        if (modelPrefabs == null || modelPrefabs.Count == 0)
        {
            Debug.LogError("AutoAnimator: Please add model prefabs to the list!");
            return;
        }

        int successCount = 0;
        int failCount = 0;

        foreach (var prefab in modelPrefabs)
        {
            if (prefab == null)
            {
                failCount++;
                continue;
            }

            string assetPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning($"AutoAnimator: Could not find asset path for '{prefab.name}'");
                failCount++;
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"AutoAnimator: '{prefab.name}' is not a model file, skipping...");
                failCount++;
                continue;
            }

            // Get the prefab name for renaming clips
            string prefabName = Path.GetFileNameWithoutExtension(assetPath);

            // === MODEL TAB ===
            importer.bakeAxisConversion = true;

            // === RIG TAB ===
            importer.animationType = ModelImporterAnimationType.Human;
            importer.sourceAvatar = humanoidAvatar;

            // === ANIMATION TAB ===
            ModelImporterClipAnimation[] clipAnimations = importer.clipAnimations;
            
            if (clipAnimations == null || clipAnimations.Length == 0)
            {
                clipAnimations = importer.defaultClipAnimations;
            }

            for (int i = 0; i < clipAnimations.Length; i++)
            {
                // Rename clip to prefab name
                clipAnimations[i].name = prefabName;

                // Loop settings
                clipAnimations[i].loopTime = loopClips;
                clipAnimations[i].loopPose = loopClips;

                // Root Transform Rotation - Bake Into Pose, Based Upon: Original
                clipAnimations[i].lockRootRotation = true;
                clipAnimations[i].keepOriginalOrientation = true;
                
                // Root Transform Position (Y) - Bake Into Pose, Based Upon: Original
                clipAnimations[i].lockRootHeightY = true;
                clipAnimations[i].keepOriginalPositionY = true;

                // Root Transform Position (XZ) - Bake Into Pose, Based Upon: Original
                clipAnimations[i].lockRootPositionXZ = true;
                clipAnimations[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clipAnimations;

            // Save and reimport
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();

            Debug.Log($"AutoAnimator: Configured '{prefabName}' - Clip renamed, Bake Axis ON, Humanoid set");
            successCount++;
        }

        AssetDatabase.Refresh();
        Debug.Log($"AutoAnimator: Done! Success: {successCount}, Failed: {failCount}");
    }

    [CustomEditor(typeof(AutoAnimator))]
    public class AutoAnimatorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            AutoAnimator script = (AutoAnimator)target;

            if (GUILayout.Button("Setup All Models", GUILayout.Height(40)))
            {
                script.SetupAllModels();
            }

            EditorGUILayout.HelpBox(
                "Instructions:\n" +
                "1. Assign a Humanoid Avatar\n" +
                "2. Drag model files (FBX) to the list\n" +
                "3. Click 'Setup All Models'\n\n" +
                "This will configure each model with:\n" +
                "• Model: Bake Axis Conversion ON\n" +
                "• Rig: Humanoid + Source Avatar\n" +
                "• Animation: Clip renamed to prefab name\n" +
                "• Animation: Loop Time/Pose (optional)\n" +
                "• Animation: All root transforms baked",
                MessageType.Info);
        }
    }
#endif
}
