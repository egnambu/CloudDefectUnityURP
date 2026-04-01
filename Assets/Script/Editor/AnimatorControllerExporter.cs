using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Exports an AnimatorController to a structured JSON format suitable for LLM analysis.
/// All Unity-internal IDs are replaced with semantic names.
/// </summary>
public class AnimatorControllerExporter : EditorWindow
{
    #region Data Transfer Objects
    
    // Root export structure
    [Serializable]
    public class AnimatorExport
    {
        public string controllerName;
        public string avatarMask;
        public string updateMode;
        public bool applyRootMotion;
        public List<ParameterExport> parameters = new List<ParameterExport>();
        public List<LayerExport> layers = new List<LayerExport>();
    }

    [Serializable]
    public class ParameterExport
    {
        public string name;
        public string type;
        public float defaultFloat;
        public int defaultInt;
        public bool defaultBool;
    }

    [Serializable]
    public class LayerExport
    {
        public string name;
        public float weight;
        public string blendingMode;
        public string syncedLayerName;
        public string avatarMask;
        public bool iKPass;
        public StateMachineExport stateMachine;
    }

    [Serializable]
    public class StateMachineExport
    {
        public string name;
        public string defaultStateName;
        public string parentStateMachineName;
        public List<StateExport> states = new List<StateExport>();
        public List<StateMachineExport> subStateMachines = new List<StateMachineExport>();
        public List<TransitionExport> anyStateTransitions = new List<TransitionExport>();
        public List<TransitionExport> entryTransitions = new List<TransitionExport>();
    }

    [Serializable]
    public class StateExport
    {
        public string name;
        public string tag;
        public string motionType;
        public string motionName;
        public BlendTreeExport blendTree;
        public float speed;
        public string speedParameterName;
        public bool speedParameterActive;
        public bool mirror;
        public string mirrorParameterName;
        public bool mirrorParameterActive;
        public float cycleOffset;
        public string cycleOffsetParameterName;
        public bool cycleOffsetParameterActive;
        public bool footIK;
        public bool writeDefaults;
        public List<BehaviourExport> behaviours = new List<BehaviourExport>();
        public List<TransitionExport> transitions = new List<TransitionExport>();
    }

    [Serializable]
    public class BlendTreeExport
    {
        public string name;
        public string blendType;
        public string blendParameter;
        public string blendParameterY;
        public bool useAutomaticThresholds;
        public float minThreshold;
        public float maxThreshold;
        public List<BlendTreeChildExport> children = new List<BlendTreeChildExport>();
    }

    [Serializable]
    public class BlendTreeChildExport
    {
        public string motionType;
        public string motionName;
        public BlendTreeExport nestedBlendTree;
        public float threshold;
        public Vector2 position;
        public float timeScale;
        public bool mirror;
        public float cycleOffset;
        public string directBlendParameter;
    }

    [Serializable]
    public class TransitionExport
    {
        public string sourceState;
        public string destinationState;
        public string destinationStateMachine;
        public bool isExitTransition;
        public bool hasExitTime;
        public float exitTime;
        public bool hasFixedDuration;
        public float duration;
        public float offset;
        public string interruptionSource;
        public bool orderedInterruption;
        public bool canTransitionToSelf;
        public bool solo;
        public bool mute;
        public List<ConditionExport> conditions = new List<ConditionExport>();
    }

    [Serializable]
    public class ConditionExport
    {
        public string parameter;
        public string mode;
        public float threshold;
    }

    [Serializable]
    public class BehaviourExport
    {
        public string typeName;
        public List<FieldExport> fields = new List<FieldExport>();
    }

    [Serializable]
    public class FieldExport
    {
        public string name;
        public string type;
        public string value;
    }

    #endregion

    #region Editor Window

    private AnimatorController selectedController;
    private GameObject selectedGameObject;
    private string lastExportPath = "";

    [MenuItem("Tools/Animator Controller Exporter")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorControllerExporter>("Animator Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Animator Controller Exporter", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Option 1: Direct AnimatorController reference
        selectedController = (AnimatorController)EditorGUILayout.ObjectField(
            "Animator Controller",
            selectedController,
            typeof(AnimatorController),
            false
        );

        GUILayout.Space(5);
        GUILayout.Label("— OR —", EditorStyles.centeredGreyMiniLabel);
        GUILayout.Space(5);

        // Option 2: GameObject with Animator component
        selectedGameObject = (GameObject)EditorGUILayout.ObjectField(
            "GameObject with Animator",
            selectedGameObject,
            typeof(GameObject),
            true
        );

        GUILayout.Space(15);

        // Determine which controller to export
        AnimatorController controllerToExport = GetControllerToExport();

        EditorGUI.BeginDisabledGroup(controllerToExport == null);
        if (GUILayout.Button("Export to JSON", GUILayout.Height(30)))
        {
            ExportController(controllerToExport);
        }
        EditorGUI.EndDisabledGroup();

        if (controllerToExport == null)
        {
            EditorGUILayout.HelpBox(
                "Select an Animator Controller asset or a GameObject with an Animator component.",
                MessageType.Info
            );
        }

        if (!string.IsNullOrEmpty(lastExportPath))
        {
            GUILayout.Space(10);
            EditorGUILayout.HelpBox($"Last export: {lastExportPath}", MessageType.None);
        }
    }

    private AnimatorController GetControllerToExport()
    {
        if (selectedController != null)
            return selectedController;

        if (selectedGameObject != null)
        {
            var animator = selectedGameObject.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                // Handle both direct AnimatorController and AnimatorOverrideController
                if (animator.runtimeAnimatorController is AnimatorController ac)
                    return ac;
                
                if (animator.runtimeAnimatorController is AnimatorOverrideController aoc)
                    return aoc.runtimeAnimatorController as AnimatorController;
            }
        }

        return null;
    }

    #endregion

    #region Menu Items

    [MenuItem("Assets/Export Animator Controller to JSON", true)]
    private static bool ValidateExportSelected()
    {
        return Selection.activeObject is AnimatorController;
    }

    [MenuItem("Assets/Export Animator Controller to JSON")]
    private static void ExportSelected()
    {
        var controller = Selection.activeObject as AnimatorController;
        if (controller != null)
        {
            ExportController(controller);
        }
    }

    #endregion

    #region Main Export Logic

    private static void ExportController(AnimatorController controller)
    {
        if (controller == null)
        {
            Debug.LogError("[AnimatorExporter] No controller provided.");
            return;
        }

        string defaultName = $"{controller.name}_Export.json";
        string path = EditorUtility.SaveFilePanel(
            "Export Animator Controller",
            Application.dataPath,
            defaultName,
            "json"
        );

        if (string.IsNullOrEmpty(path))
            return;

        try
        {
            var export = BuildExport(controller);
            string json = ToFormattedJson(export);
            File.WriteAllText(path, json, Encoding.UTF8);
            
            Debug.Log($"[AnimatorExporter] Exported '{controller.name}' to: {path}");
            
            // Update last path in window if open
            var window = GetWindow<AnimatorControllerExporter>();
            if (window != null)
                window.lastExportPath = path;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AnimatorExporter] Export failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// Builds the complete export data structure from an AnimatorController.
    /// </summary>
    public static AnimatorExport BuildExport(AnimatorController controller)
    {
        var export = new AnimatorExport
        {
            controllerName = controller.name,
            // Note: AnimatorController doesn't have avatar mask at controller level;
            // it's per-layer. Setting to empty.
            avatarMask = "",
            // Update mode and root motion are runtime Animator properties, not on the controller
            updateMode = "Normal",
            applyRootMotion = false
        };

        ExportParameters(controller, export);
        ExportLayers(controller, export);

        return export;
    }

    #endregion

    #region Parameter Export

    private static void ExportParameters(AnimatorController controller, AnimatorExport export)
    {
        // Sort parameters by name for deterministic output
        var sortedParams = controller.parameters.OrderBy(p => p.name).ToArray();

        foreach (var param in sortedParams)
        {
            var paramExport = new ParameterExport
            {
                name = param.name,
                type = param.type.ToString(),
                defaultFloat = param.defaultFloat,
                defaultInt = param.defaultInt,
                defaultBool = param.defaultBool
            };
            export.parameters.Add(paramExport);
        }
    }

    #endregion

    #region Layer Export

    private static void ExportLayers(AnimatorController controller, AnimatorExport export)
    {
        // Layers are ordered; preserve their order
        for (int i = 0; i < controller.layers.Length; i++)
        {
            var layer = controller.layers[i];
            var layerExport = new LayerExport
            {
                name = layer.name,
                weight = layer.defaultWeight,
                blendingMode = layer.blendingMode.ToString(),
                syncedLayerName = layer.syncedLayerIndex >= 0 && layer.syncedLayerIndex < controller.layers.Length
                    ? controller.layers[layer.syncedLayerIndex].name
                    : "",
                avatarMask = layer.avatarMask != null ? layer.avatarMask.name : "",
                iKPass = layer.iKPass,
                stateMachine = ExportStateMachine(layer.stateMachine, null)
            };
            export.layers.Add(layerExport);
        }
    }

    #endregion

    #region State Machine Export

    private static StateMachineExport ExportStateMachine(
        AnimatorStateMachine stateMachine,
        string parentName)
    {
        if (stateMachine == null)
            return null;

        var smExport = new StateMachineExport
        {
            name = stateMachine.name,
            defaultStateName = stateMachine.defaultState != null
                ? stateMachine.defaultState.name
                : "",
            parentStateMachineName = parentName ?? ""
        };

        // Export states (sorted by name for determinism)
        var sortedStates = stateMachine.states
            .OrderBy(s => s.state.name)
            .ToArray();

        foreach (var childState in sortedStates)
        {
            smExport.states.Add(ExportState(childState.state));
        }

        // Export sub-state machines recursively (sorted by name)
        var sortedSubMachines = stateMachine.stateMachines
            .OrderBy(s => s.stateMachine.name)
            .ToArray();

        foreach (var childSM in sortedSubMachines)
        {
            smExport.subStateMachines.Add(
                ExportStateMachine(childSM.stateMachine, stateMachine.name)
            );
        }

        // Export Any State transitions
        var anyStateTransitions = stateMachine.anyStateTransitions
            .OrderBy(t => t.destinationState?.name ?? t.destinationStateMachine?.name ?? "")
            .ToArray();

        foreach (var transition in anyStateTransitions)
        {
            smExport.anyStateTransitions.Add(ExportTransition(transition, "Any State"));
        }

        // Export Entry transitions (transitions from entry node)
        var entryTransitions = stateMachine.entryTransitions
            .OrderBy(t => t.destinationState?.name ?? t.destinationStateMachine?.name ?? "")
            .ToArray();

        foreach (var transition in entryTransitions)
        {
            smExport.entryTransitions.Add(ExportEntryTransition(transition));
        }

        return smExport;
    }

    #endregion

    #region State Export

    private static StateExport ExportState(AnimatorState state)
    {
        if (state == null)
            return null;

        var stateExport = new StateExport
        {
            name = state.name,
            tag = state.tag,
            speed = state.speed,
            speedParameterName = state.speedParameter,
            speedParameterActive = state.speedParameterActive,
            mirror = state.mirror,
            mirrorParameterName = state.mirrorParameter,
            mirrorParameterActive = state.mirrorParameterActive,
            cycleOffset = state.cycleOffset,
            cycleOffsetParameterName = state.cycleOffsetParameter,
            cycleOffsetParameterActive = state.cycleOffsetParameterActive,
            footIK = state.iKOnFeet,
            writeDefaults = state.writeDefaultValues
        };

        // Determine motion type and export accordingly
        if (state.motion == null)
        {
            stateExport.motionType = "None";
            stateExport.motionName = "";
        }
        else if (state.motion is BlendTree blendTree)
        {
            stateExport.motionType = "BlendTree";
            stateExport.motionName = blendTree.name;
            stateExport.blendTree = ExportBlendTree(blendTree);
        }
        else if (state.motion is AnimationClip clip)
        {
            stateExport.motionType = "AnimationClip";
            stateExport.motionName = clip.name;
        }
        else
        {
            stateExport.motionType = state.motion.GetType().Name;
            stateExport.motionName = state.motion.name;
        }

        // Export behaviours
        ExportBehaviours(state, stateExport);

        // Export transitions (sorted by destination for determinism)
        var sortedTransitions = state.transitions
            .OrderBy(t => t.destinationState?.name ?? t.destinationStateMachine?.name ?? "Exit")
            .ToArray();

        foreach (var transition in sortedTransitions)
        {
            stateExport.transitions.Add(ExportTransition(transition, state.name));
        }

        return stateExport;
    }

    #endregion

    #region Blend Tree Export

    private static BlendTreeExport ExportBlendTree(BlendTree blendTree)
    {
        if (blendTree == null)
            return null;

        var btExport = new BlendTreeExport
        {
            name = blendTree.name,
            blendType = blendTree.blendType.ToString(),
            blendParameter = blendTree.blendParameter,
            blendParameterY = blendTree.blendParameterY,
            useAutomaticThresholds = blendTree.useAutomaticThresholds,
            minThreshold = blendTree.minThreshold,
            maxThreshold = blendTree.maxThreshold
        };

        // Export children - order matters for blend trees, don't sort
        foreach (var child in blendTree.children)
        {
            var childExport = new BlendTreeChildExport
            {
                threshold = child.threshold,
                position = child.position,
                timeScale = child.timeScale,
                mirror = child.mirror,
                cycleOffset = child.cycleOffset,
                directBlendParameter = child.directBlendParameter
            };

            if (child.motion == null)
            {
                childExport.motionType = "None";
                childExport.motionName = "";
            }
            else if (child.motion is BlendTree nestedBlendTree)
            {
                childExport.motionType = "BlendTree";
                childExport.motionName = nestedBlendTree.name;
                // Recursively export nested blend trees
                childExport.nestedBlendTree = ExportBlendTree(nestedBlendTree);
            }
            else if (child.motion is AnimationClip clip)
            {
                childExport.motionType = "AnimationClip";
                childExport.motionName = clip.name;
            }
            else
            {
                childExport.motionType = child.motion.GetType().Name;
                childExport.motionName = child.motion.name;
            }

            btExport.children.Add(childExport);
        }

        return btExport;
    }

    #endregion

    #region Transition Export

    private static TransitionExport ExportTransition(
        AnimatorStateTransition transition,
        string sourceName)
    {
        if (transition == null)
            return null;

        var transExport = new TransitionExport
        {
            sourceState = sourceName,
            destinationState = transition.destinationState != null
                ? transition.destinationState.name
                : "",
            destinationStateMachine = transition.destinationStateMachine != null
                ? transition.destinationStateMachine.name
                : "",
            isExitTransition = transition.isExit,
            hasExitTime = transition.hasExitTime,
            exitTime = transition.exitTime,
            hasFixedDuration = transition.hasFixedDuration,
            duration = transition.duration,
            offset = transition.offset,
            interruptionSource = transition.interruptionSource.ToString(),
            orderedInterruption = transition.orderedInterruption,
            canTransitionToSelf = transition.canTransitionToSelf,
            solo = transition.solo,
            mute = transition.mute
        };

        // Export conditions (preserve order as it may be significant)
        foreach (var condition in transition.conditions)
        {
            transExport.conditions.Add(new ConditionExport
            {
                parameter = condition.parameter,
                mode = condition.mode.ToString(),
                threshold = condition.threshold
            });
        }

        return transExport;
    }

    private static TransitionExport ExportEntryTransition(AnimatorTransition transition)
    {
        if (transition == null)
            return null;

        var transExport = new TransitionExport
        {
            sourceState = "Entry",
            destinationState = transition.destinationState != null
                ? transition.destinationState.name
                : "",
            destinationStateMachine = transition.destinationStateMachine != null
                ? transition.destinationStateMachine.name
                : "",
            isExitTransition = transition.isExit,
            // Entry transitions don't have these properties
            hasExitTime = false,
            exitTime = 0f,
            hasFixedDuration = true,
            duration = 0f,
            offset = 0f,
            interruptionSource = "None",
            orderedInterruption = true,
            canTransitionToSelf = false,
            solo = transition.solo,
            mute = transition.mute
        };

        foreach (var condition in transition.conditions)
        {
            transExport.conditions.Add(new ConditionExport
            {
                parameter = condition.parameter,
                mode = condition.mode.ToString(),
                threshold = condition.threshold
            });
        }

        return transExport;
    }

    #endregion

    #region Behaviour Export

    private static void ExportBehaviours(AnimatorState state, StateExport stateExport)
    {
        var behaviours = state.behaviours;
        if (behaviours == null || behaviours.Length == 0)
            return;

        // Sort by type name for determinism
        var sortedBehaviours = behaviours
            .Where(b => b != null)
            .OrderBy(b => b.GetType().Name)
            .ToArray();

        foreach (var behaviour in sortedBehaviours)
        {
            var behaviourExport = new BehaviourExport
            {
                typeName = behaviour.GetType().FullName
            };

            // Use SerializedObject to extract field values without reflection
            // This is the Unity-preferred way to read serialized data
            var serializedObject = new SerializedObject(behaviour);
            var iterator = serializedObject.GetIterator();

            // Enter the first child to skip the root
            if (iterator.NextVisible(true))
            {
                do
                {
                    // Skip Unity internal properties
                    if (iterator.name == "m_Script" || 
                        iterator.name == "m_ObjectHideFlags" ||
                        iterator.name == "m_Name")
                        continue;

                    var fieldExport = new FieldExport
                    {
                        name = iterator.name,
                        type = iterator.propertyType.ToString(),
                        value = GetSerializedPropertyValue(iterator)
                    };
                    behaviourExport.fields.Add(fieldExport);

                } while (iterator.NextVisible(false));
            }

            // Sort fields by name for determinism
            behaviourExport.fields = behaviourExport.fields
                .OrderBy(f => f.name)
                .ToList();

            stateExport.behaviours.Add(behaviourExport);
        }
    }

    /// <summary>
    /// Extracts a human-readable string value from a SerializedProperty.
    /// </summary>
    private static string GetSerializedPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                return prop.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return prop.boolValue.ToString();
            case SerializedPropertyType.Float:
                return prop.floatValue.ToString("G9");
            case SerializedPropertyType.String:
                return prop.stringValue ?? "";
            case SerializedPropertyType.Color:
                return $"RGBA({prop.colorValue.r:F3}, {prop.colorValue.g:F3}, {prop.colorValue.b:F3}, {prop.colorValue.a:F3})";
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue != null 
                    ? prop.objectReferenceValue.name 
                    : "null";
            case SerializedPropertyType.LayerMask:
                return prop.intValue.ToString();
            case SerializedPropertyType.Enum:
                // Return the enum name if available
                if (prop.enumNames != null && prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length)
                    return prop.enumNames[prop.enumValueIndex];
                return prop.enumValueIndex.ToString();
            case SerializedPropertyType.Vector2:
                return $"({prop.vector2Value.x:G9}, {prop.vector2Value.y:G9})";
            case SerializedPropertyType.Vector3:
                return $"({prop.vector3Value.x:G9}, {prop.vector3Value.y:G9}, {prop.vector3Value.z:G9})";
            case SerializedPropertyType.Vector4:
                return $"({prop.vector4Value.x:G9}, {prop.vector4Value.y:G9}, {prop.vector4Value.z:G9}, {prop.vector4Value.w:G9})";
            case SerializedPropertyType.Rect:
                var r = prop.rectValue;
                return $"Rect(x:{r.x:G9}, y:{r.y:G9}, w:{r.width:G9}, h:{r.height:G9})";
            case SerializedPropertyType.ArraySize:
                return prop.intValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                return prop.animationCurveValue != null 
                    ? $"AnimationCurve({prop.animationCurveValue.length} keys)" 
                    : "null";
            case SerializedPropertyType.Bounds:
                var b = prop.boundsValue;
                return $"Bounds(center:{b.center}, size:{b.size})";
            case SerializedPropertyType.Quaternion:
                var q = prop.quaternionValue;
                return $"Quaternion({q.x:G9}, {q.y:G9}, {q.z:G9}, {q.w:G9})";
            case SerializedPropertyType.Vector2Int:
                return $"({prop.vector2IntValue.x}, {prop.vector2IntValue.y})";
            case SerializedPropertyType.Vector3Int:
                return $"({prop.vector3IntValue.x}, {prop.vector3IntValue.y}, {prop.vector3IntValue.z})";
            case SerializedPropertyType.RectInt:
                var ri = prop.rectIntValue;
                return $"RectInt(x:{ri.x}, y:{ri.y}, w:{ri.width}, h:{ri.height})";
            case SerializedPropertyType.BoundsInt:
                var bi = prop.boundsIntValue;
                return $"BoundsInt(pos:{bi.position}, size:{bi.size})";
            default:
                // For complex types, indicate the type
                return $"<{prop.propertyType}>";
        }
    }

    #endregion

    #region JSON Serialization

    /// <summary>
    /// Converts the export object to formatted JSON.
    /// Unity's JsonUtility doesn't format nicely, so we use a custom approach.
    /// </summary>
    private static string ToFormattedJson(AnimatorExport export)
    {
        var sb = new StringBuilder();
        WriteJson(sb, export, 0);
        return sb.ToString();
    }

    private static void WriteJson(StringBuilder sb, object obj, int indent)
    {
        if (obj == null)
        {
            sb.Append("null");
            return;
        }

        var type = obj.GetType();

        if (type == typeof(string))
        {
            sb.Append('"');
            sb.Append(EscapeJsonString((string)obj));
            sb.Append('"');
        }
        else if (type == typeof(bool))
        {
            sb.Append((bool)obj ? "true" : "false");
        }
        else if (type == typeof(int) || type == typeof(float) || type == typeof(double))
        {
            if (obj is float f)
                sb.Append(f.ToString("G9", System.Globalization.CultureInfo.InvariantCulture));
            else if (obj is double d)
                sb.Append(d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture));
            else
                sb.Append(obj.ToString());
        }
        else if (type == typeof(Vector2))
        {
            var v = (Vector2)obj;
            sb.Append($"{{ \"x\": {v.x:G9}, \"y\": {v.y:G9} }}");
        }
        else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
        {
            var list = (System.Collections.IList)obj;
            if (list.Count == 0)
            {
                sb.Append("[]");
            }
            else
            {
                sb.AppendLine("[");
                for (int i = 0; i < list.Count; i++)
                {
                    sb.Append(new string(' ', (indent + 1) * 2));
                    WriteJson(sb, list[i], indent + 1);
                    if (i < list.Count - 1)
                        sb.Append(',');
                    sb.AppendLine();
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append(']');
            }
        }
        else if (type.IsClass)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            // Filter out null optional fields for cleaner output
            var nonNullFields = fields
                .Where(f => {
                    var value = f.GetValue(obj);
                    if (value == null) return false;
                    // Also skip empty strings for cleaner output
                    if (value is string s && string.IsNullOrEmpty(s)) return false;
                    // Skip empty lists
                    if (value is System.Collections.IList list && list.Count == 0) return false;
                    return true;
                })
                .ToArray();

            if (nonNullFields.Length == 0)
            {
                sb.Append("{}");
            }
            else
            {
                sb.AppendLine("{");
                for (int i = 0; i < nonNullFields.Length; i++)
                {
                    var field = nonNullFields[i];
                    sb.Append(new string(' ', (indent + 1) * 2));
                    sb.Append('"');
                    sb.Append(field.Name);
                    sb.Append("\": ");
                    WriteJson(sb, field.GetValue(obj), indent + 1);
                    if (i < nonNullFields.Length - 1)
                        sb.Append(',');
                    sb.AppendLine();
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append('}');
            }
        }
        else
        {
            // Fallback for other types
            sb.Append('"');
            sb.Append(EscapeJsonString(obj.ToString()));
            sb.Append('"');
        }
    }

    private static string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";

        var sb = new StringBuilder(str.Length);
        foreach (char c in str)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < ' ')
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    #endregion

    #region Public API

    /// <summary>
    /// Programmatic entry point: exports an AnimatorController to JSON and returns the string.
    /// </summary>
    public static string ExportToJsonString(AnimatorController controller)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));

        var export = BuildExport(controller);
        return ToFormattedJson(export);
    }

    /// <summary>
    /// Programmatic entry point: exports an AnimatorController to a file.
    /// </summary>
    public static void ExportToFile(AnimatorController controller, string filePath)
    {
        if (controller == null)
            throw new ArgumentNullException(nameof(controller));
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentNullException(nameof(filePath));

        var json = ExportToJsonString(controller);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    #endregion
}
