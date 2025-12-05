using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

/// <summary>
/// Unity Editor tool that reads an Animator Controller and generates
/// a human-readable text description of all states, transitions, parameters,
/// and state machines - perfect for LLM debugging and documentation.
/// </summary>
public class AnimatorDocumentor : EditorWindow
{
    private RuntimeAnimatorController animatorController;
    private Vector2 scrollPosition;
    private string generatedDocumentation = "";
    private bool includeTransitionDetails = true;
    private bool includeParameterInfo = true;
    private bool includeBlendTreeInfo = true;
    private bool includeStateMachineHierarchy = true;
    private bool includeMotionInfo = true;
    private bool compactMode = false;
    private string exportPath = "";

    [MenuItem("Tools/Animator Documentor")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimatorDocumentor>("Animator Documentor");
        window.minSize = new Vector2(500, 600);
    }

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Animator Controller Documentor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Attach an Animator Controller to generate a text-based documentation " +
            "of all states, transitions, and parameters. Useful for LLM debugging.", 
            MessageType.Info);

        EditorGUILayout.Space(10);

        // Controller Selection
        EditorGUILayout.LabelField("Animator Controller", EditorStyles.boldLabel);
        animatorController = (RuntimeAnimatorController)EditorGUILayout.ObjectField(
            "Controller", 
            animatorController, 
            typeof(RuntimeAnimatorController), 
            false);

        EditorGUILayout.Space(10);

        // Options
        EditorGUILayout.LabelField("Documentation Options", EditorStyles.boldLabel);
        includeParameterInfo = EditorGUILayout.Toggle("Include Parameters", includeParameterInfo);
        includeStateMachineHierarchy = EditorGUILayout.Toggle("Include State Machine Hierarchy", includeStateMachineHierarchy);
        includeTransitionDetails = EditorGUILayout.Toggle("Include Transition Details", includeTransitionDetails);
        includeBlendTreeInfo = EditorGUILayout.Toggle("Include Blend Tree Info", includeBlendTreeInfo);
        includeMotionInfo = EditorGUILayout.Toggle("Include Motion/Clip Info", includeMotionInfo);
        compactMode = EditorGUILayout.Toggle("Compact Mode (Less Verbose)", compactMode);

        EditorGUILayout.Space(10);

        // Generate Button
        EditorGUI.BeginDisabledGroup(animatorController == null);
        if (GUILayout.Button("Generate Documentation", GUILayout.Height(35)))
        {
            GenerateDocumentation();
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(5);

        // Export Options
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy to Clipboard", GUILayout.Height(25)))
        {
            if (!string.IsNullOrEmpty(generatedDocumentation))
            {
                EditorGUIUtility.systemCopyBuffer = generatedDocumentation;
                Debug.Log("Documentation copied to clipboard!");
            }
        }
        if (GUILayout.Button("Export to File", GUILayout.Height(25)))
        {
            ExportToFile();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);

        // Documentation Preview
        EditorGUILayout.LabelField("Generated Documentation", EditorStyles.boldLabel);
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        
        // Use a text area style that wraps
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
        textAreaStyle.wordWrap = true;
        textAreaStyle.richText = false;
        
        generatedDocumentation = EditorGUILayout.TextArea(
            generatedDocumentation, 
            textAreaStyle, 
            GUILayout.ExpandHeight(true));
        
        EditorGUILayout.EndScrollView();
    }

    private void GenerateDocumentation()
    {
        if (animatorController == null)
        {
            generatedDocumentation = "No Animator Controller assigned.";
            return;
        }

        AnimatorController controller = animatorController as AnimatorController;
        if (controller == null)
        {
            // Try to get the actual AnimatorController from RuntimeAnimatorController
            string path = AssetDatabase.GetAssetPath(animatorController);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        if (controller == null)
        {
            generatedDocumentation = "Could not load AnimatorController. Make sure it's a valid .controller asset.";
            return;
        }

        StringBuilder sb = new StringBuilder();
        string separator = compactMode ? "-" : "=";
        string line = new string(separator[0], 60);

        // Header
        sb.AppendLine(line);
        sb.AppendLine($"ANIMATOR CONTROLLER DOCUMENTATION");
        sb.AppendLine($"Controller Name: {controller.name}");
        sb.AppendLine($"Asset Path: {AssetDatabase.GetAssetPath(controller)}");
        sb.AppendLine($"Generated: {System.DateTime.Now}");
        sb.AppendLine(line);
        sb.AppendLine();

        // Parameters Section
        if (includeParameterInfo)
        {
            DocumentParameters(sb, controller);
        }

        // Layers and State Machines
        DocumentLayers(sb, controller);

        generatedDocumentation = sb.ToString();
        Debug.Log($"Documentation generated: {generatedDocumentation.Length} characters");
    }

    private void DocumentParameters(StringBuilder sb, AnimatorController controller)
    {
        sb.AppendLine("## PARAMETERS");
        sb.AppendLine(new string('-', 40));

        if (controller.parameters.Length == 0)
        {
            sb.AppendLine("(No parameters defined)");
        }
        else
        {
            sb.AppendLine($"Total Parameters: {controller.parameters.Length}");
            sb.AppendLine();

            foreach (var param in controller.parameters)
            {
                string defaultValue = GetParameterDefaultValue(param);
                sb.AppendLine($"  - {param.name}");
                sb.AppendLine($"    Type: {param.type}");
                sb.AppendLine($"    Default: {defaultValue}");
                if (!compactMode) sb.AppendLine();
            }
        }
        sb.AppendLine();
    }

    private string GetParameterDefaultValue(AnimatorControllerParameter param)
    {
        switch (param.type)
        {
            case AnimatorControllerParameterType.Float:
                return param.defaultFloat.ToString("F2");
            case AnimatorControllerParameterType.Int:
                return param.defaultInt.ToString();
            case AnimatorControllerParameterType.Bool:
                return param.defaultBool.ToString();
            case AnimatorControllerParameterType.Trigger:
                return "(Trigger)";
            default:
                return "Unknown";
        }
    }

    private void DocumentLayers(StringBuilder sb, AnimatorController controller)
    {
        sb.AppendLine("## LAYERS");
        sb.AppendLine(new string('-', 40));
        sb.AppendLine($"Total Layers: {controller.layers.Length}");
        sb.AppendLine();

        for (int i = 0; i < controller.layers.Length; i++)
        {
            var layer = controller.layers[i];
            sb.AppendLine($"### LAYER {i}: \"{layer.name}\"");
            sb.AppendLine($"    Blending Mode: {layer.blendingMode}");
            sb.AppendLine($"    Weight: {layer.defaultWeight}");
            
            if (layer.avatarMask != null)
            {
                sb.AppendLine($"    Avatar Mask: {layer.avatarMask.name}");
            }
            
            sb.AppendLine($"    IK Pass: {layer.iKPass}");
            sb.AppendLine($"    Sync Layer: {(layer.syncedLayerIndex >= 0 ? controller.layers[layer.syncedLayerIndex].name : "None")}");
            sb.AppendLine();

            // Document the state machine for this layer
            if (layer.stateMachine != null && includeStateMachineHierarchy)
            {
                DocumentStateMachine(sb, layer.stateMachine, 1);
            }

            sb.AppendLine();
        }
    }

    private void DocumentStateMachine(StringBuilder sb, AnimatorStateMachine stateMachine, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        sb.AppendLine($"{indent}STATE MACHINE: \"{stateMachine.name}\"");
        
        // Default State
        if (stateMachine.defaultState != null)
        {
            sb.AppendLine($"{indent}Default State: \"{stateMachine.defaultState.name}\"");
        }

        // Any State Transitions
        if (stateMachine.anyStateTransitions.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}ANY STATE TRANSITIONS ({stateMachine.anyStateTransitions.Length}):");
            foreach (var transition in stateMachine.anyStateTransitions)
            {
                DocumentTransition(sb, transition, "Any State", depth + 1);
            }
        }

        // Entry Transitions
        if (stateMachine.entryTransitions.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}ENTRY TRANSITIONS ({stateMachine.entryTransitions.Length}):");
            foreach (var transition in stateMachine.entryTransitions)
            {
                string destName = transition.destinationState?.name ?? 
                                  transition.destinationStateMachine?.name ?? "Unknown";
                sb.AppendLine($"{subIndent}-> \"{destName}\"");
            }
        }

        // States
        sb.AppendLine();
        sb.AppendLine($"{indent}STATES ({stateMachine.states.Length}):");
        
        foreach (var childState in stateMachine.states)
        {
            DocumentState(sb, childState.state, depth + 1);
        }

        // Sub-State Machines (Recursive)
        if (stateMachine.stateMachines.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}SUB-STATE MACHINES ({stateMachine.stateMachines.Length}):");
            
            foreach (var childSM in stateMachine.stateMachines)
            {
                DocumentStateMachine(sb, childSM.stateMachine, depth + 1);
            }
        }

        // State Machine Behaviours
        var behaviours = stateMachine.behaviours;
        if (behaviours != null && behaviours.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}STATE MACHINE BEHAVIOURS:");
            foreach (var behaviour in behaviours)
            {
                if (behaviour != null)
                {
                    sb.AppendLine($"{subIndent}- {behaviour.GetType().Name}");
                }
            }
        }
    }

    private void DocumentState(StringBuilder sb, AnimatorState state, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        sb.AppendLine();
        sb.AppendLine($"{indent}STATE: \"{state.name}\"");
        sb.AppendLine($"{subIndent}Tag: \"{state.tag}\"");
        sb.AppendLine($"{subIndent}Speed: {state.speed} (Multiplier Param: {(string.IsNullOrEmpty(state.speedParameterActive ? state.speedParameter : "") ? "None" : state.speedParameter)})");
        sb.AppendLine($"{subIndent}Cycle Offset: {state.cycleOffset}");
        sb.AppendLine($"{subIndent}Mirror: {state.mirror} (Param: {(state.mirrorParameterActive ? state.mirrorParameter : "None")})");
        sb.AppendLine($"{subIndent}Write Default Values: {state.writeDefaultValues}");

        // Motion Info
        if (includeMotionInfo && state.motion != null)
        {
            DocumentMotion(sb, state.motion, depth + 1);
        }
        else if (state.motion == null)
        {
            sb.AppendLine($"{subIndent}Motion: (None)");
        }

        // State Behaviours
        var behaviours = state.behaviours;
        if (behaviours != null && behaviours.Length > 0)
        {
            sb.AppendLine($"{subIndent}State Behaviours:");
            foreach (var behaviour in behaviours)
            {
                if (behaviour != null)
                {
                    sb.AppendLine($"{subIndent}  - {behaviour.GetType().Name}");
                    // Try to get serialized properties of the behaviour
                    DocumentStateBehaviourProperties(sb, behaviour, depth + 2);
                }
            }
        }

        // Transitions
        if (state.transitions.Length > 0 && includeTransitionDetails)
        {
            sb.AppendLine($"{subIndent}TRANSITIONS ({state.transitions.Length}):");
            foreach (var transition in state.transitions)
            {
                DocumentTransition(sb, transition, state.name, depth + 2);
            }
        }
    }

    private void DocumentMotion(StringBuilder sb, Motion motion, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        if (motion is AnimationClip clip)
        {
            sb.AppendLine($"{indent}Motion Type: Animation Clip");
            sb.AppendLine($"{indent}Clip Name: \"{clip.name}\"");
            sb.AppendLine($"{indent}Length: {clip.length:F2}s");
            sb.AppendLine($"{indent}Frame Rate: {clip.frameRate} FPS");
            sb.AppendLine($"{indent}Looping: {clip.isLooping}");
            sb.AppendLine($"{indent}Legacy: {clip.legacy}");
        }
        else if (motion is BlendTree blendTree && includeBlendTreeInfo)
        {
            DocumentBlendTree(sb, blendTree, depth);
        }
        else if (motion != null)
        {
            sb.AppendLine($"{indent}Motion: {motion.name} ({motion.GetType().Name})");
        }
    }

    private void DocumentBlendTree(StringBuilder sb, BlendTree blendTree, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        sb.AppendLine($"{indent}Motion Type: Blend Tree");
        sb.AppendLine($"{indent}Blend Tree Name: \"{blendTree.name}\"");
        sb.AppendLine($"{indent}Blend Type: {blendTree.blendType}");
        sb.AppendLine($"{indent}Blend Parameter: \"{blendTree.blendParameter}\"");
        
        if (blendTree.blendType == BlendTreeType.FreeformCartesian2D || 
            blendTree.blendType == BlendTreeType.FreeformDirectional2D ||
            blendTree.blendType == BlendTreeType.SimpleDirectional2D)
        {
            sb.AppendLine($"{indent}Blend Parameter Y: \"{blendTree.blendParameterY}\"");
        }

        sb.AppendLine($"{indent}Min Threshold: {blendTree.minThreshold}");
        sb.AppendLine($"{indent}Max Threshold: {blendTree.maxThreshold}");
        sb.AppendLine($"{indent}Children ({blendTree.children.Length}):");

        foreach (var child in blendTree.children)
        {
            string childName = child.motion?.name ?? "(Empty)";
            string childType = child.motion is BlendTree ? "BlendTree" : "Clip";
            
            if (blendTree.blendType == BlendTreeType.Simple1D)
            {
                sb.AppendLine($"{subIndent}- \"{childName}\" ({childType}) @ Threshold: {child.threshold:F2}");
            }
            else
            {
                sb.AppendLine($"{subIndent}- \"{childName}\" ({childType}) @ Position: ({child.position.x:F2}, {child.position.y:F2})");
            }

            // Recursively document nested blend trees
            if (child.motion is BlendTree nestedTree && !compactMode)
            {
                DocumentBlendTree(sb, nestedTree, depth + 2);
            }
        }
    }

    private void DocumentTransition(StringBuilder sb, AnimatorStateTransition transition, string sourceName, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        string destName = transition.destinationState?.name ?? 
                          transition.destinationStateMachine?.name ?? 
                          (transition.isExit ? "Exit" : "Unknown");

        sb.AppendLine($"{indent}TRANSITION: \"{sourceName}\" -> \"{destName}\"");
        
        if (!compactMode)
        {
            sb.AppendLine($"{subIndent}Has Exit Time: {transition.hasExitTime}");
            if (transition.hasExitTime)
            {
                sb.AppendLine($"{subIndent}Exit Time: {transition.exitTime:F3}");
            }
            sb.AppendLine($"{subIndent}Has Fixed Duration: {transition.hasFixedDuration}");
            sb.AppendLine($"{subIndent}Duration: {transition.duration:F3}");
            sb.AppendLine($"{subIndent}Offset: {transition.offset:F3}");
            sb.AppendLine($"{subIndent}Interruption Source: {transition.interruptionSource}");
            sb.AppendLine($"{subIndent}Ordered Interruption: {transition.orderedInterruption}");
            sb.AppendLine($"{subIndent}Can Transition To Self: {transition.canTransitionToSelf}");
        }

        // Conditions
        if (transition.conditions.Length > 0)
        {
            sb.AppendLine($"{subIndent}CONDITIONS ({transition.conditions.Length}):");
            foreach (var condition in transition.conditions)
            {
                string conditionStr = FormatCondition(condition);
                sb.AppendLine($"{subIndent}  - {conditionStr}");
            }
        }
        else
        {
            sb.AppendLine($"{subIndent}CONDITIONS: (None - Exit Time Only)");
        }

        if (!compactMode) sb.AppendLine();
    }

    private void DocumentTransition(StringBuilder sb, AnimatorTransition transition, string sourceName, int depth)
    {
        string indent = new string(' ', depth * 4);
        string subIndent = new string(' ', (depth + 1) * 4);

        string destName = transition.destinationState?.name ?? 
                          transition.destinationStateMachine?.name ?? 
                          (transition.isExit ? "Exit" : "Unknown");

        sb.AppendLine($"{indent}TRANSITION: \"{sourceName}\" -> \"{destName}\"");

        // Conditions
        if (transition.conditions.Length > 0)
        {
            sb.AppendLine($"{subIndent}CONDITIONS ({transition.conditions.Length}):");
            foreach (var condition in transition.conditions)
            {
                string conditionStr = FormatCondition(condition);
                sb.AppendLine($"{subIndent}  - {conditionStr}");
            }
        }
        else
        {
            sb.AppendLine($"{subIndent}CONDITIONS: (None)");
        }
    }

    private string FormatCondition(AnimatorCondition condition)
    {
        string modeStr = "";
        switch (condition.mode)
        {
            case AnimatorConditionMode.If:
                return $"{condition.parameter} == true";
            case AnimatorConditionMode.IfNot:
                return $"{condition.parameter} == false";
            case AnimatorConditionMode.Greater:
                return $"{condition.parameter} > {condition.threshold:F2}";
            case AnimatorConditionMode.Less:
                return $"{condition.parameter} < {condition.threshold:F2}";
            case AnimatorConditionMode.Equals:
                return $"{condition.parameter} == {(int)condition.threshold}";
            case AnimatorConditionMode.NotEqual:
                return $"{condition.parameter} != {(int)condition.threshold}";
            default:
                return $"{condition.parameter} {condition.mode} {condition.threshold}";
        }
    }

    private void DocumentStateBehaviourProperties(StringBuilder sb, StateMachineBehaviour behaviour, int depth)
    {
        if (compactMode) return;

        string indent = new string(' ', depth * 4);
        
        SerializedObject serializedBehaviour = new SerializedObject(behaviour);
        SerializedProperty prop = serializedBehaviour.GetIterator();
        
        bool hasProps = false;
        while (prop.NextVisible(true))
        {
            // Skip script reference
            if (prop.name == "m_Script") continue;
            if (prop.depth > 1) continue; // Only top-level properties
            
            if (!hasProps)
            {
                sb.AppendLine($"{indent}Properties:");
                hasProps = true;
            }
            
            string value = GetSerializedPropertyValue(prop);
            sb.AppendLine($"{indent}  {prop.displayName}: {value}");
        }
    }

    private string GetSerializedPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                return prop.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return prop.boolValue.ToString();
            case SerializedPropertyType.Float:
                return prop.floatValue.ToString("F2");
            case SerializedPropertyType.String:
                return $"\"{prop.stringValue}\"";
            case SerializedPropertyType.Enum:
                return prop.enumDisplayNames.Length > prop.enumValueIndex && prop.enumValueIndex >= 0 
                    ? prop.enumDisplayNames[prop.enumValueIndex] 
                    : prop.enumValueIndex.ToString();
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue != null 
                    ? prop.objectReferenceValue.name 
                    : "(None)";
            case SerializedPropertyType.Vector2:
                return prop.vector2Value.ToString();
            case SerializedPropertyType.Vector3:
                return prop.vector3Value.ToString();
            case SerializedPropertyType.Vector4:
                return prop.vector4Value.ToString();
            case SerializedPropertyType.Color:
                return prop.colorValue.ToString();
            case SerializedPropertyType.AnimationCurve:
                return "(AnimationCurve)";
            case SerializedPropertyType.LayerMask:
                return prop.intValue.ToString();
            default:
                return $"({prop.propertyType})";
        }
    }

    private void ExportToFile()
    {
        if (string.IsNullOrEmpty(generatedDocumentation))
        {
            Debug.LogWarning("No documentation to export. Generate documentation first.");
            return;
        }

        string defaultName = animatorController != null 
            ? $"{animatorController.name}_Documentation.txt" 
            : "AnimatorDocumentation.txt";

        string path = EditorUtility.SaveFilePanel(
            "Export Animator Documentation",
            Application.dataPath,
            defaultName,
            "txt");

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, generatedDocumentation);
            Debug.Log($"Documentation exported to: {path}");
            EditorUtility.RevealInFinder(path);
        }
    }
}

/// <summary>
/// Component version that can be attached to a GameObject with an Animator
/// to generate documentation at runtime or in editor.
/// </summary>
[ExecuteInEditMode]
public class AnimatorDocumentorComponent : MonoBehaviour
{
    [Header("Settings")]
    public Animator targetAnimator;
    public bool autoDocumentOnStart = false;
    
    [Header("Options")]
    public bool includeTransitionDetails = true;
    public bool includeParameterInfo = true;
    public bool includeBlendTreeInfo = true;
    public bool compactMode = false;

    [Header("Output")]
    [TextArea(20, 50)]
    public string documentation = "";

    private void Start()
    {
        if (autoDocumentOnStart && Application.isPlaying)
        {
            GenerateDocumentation();
        }
    }

    private void OnValidate()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }
    }

    [ContextMenu("Generate Documentation")]
    public void GenerateDocumentation()
    {
        if (targetAnimator == null || targetAnimator.runtimeAnimatorController == null)
        {
            documentation = "No Animator or Controller assigned.";
            return;
        }

#if UNITY_EDITOR
        AnimatorController controller = targetAnimator.runtimeAnimatorController as AnimatorController;
        if (controller == null)
        {
            string path = AssetDatabase.GetAssetPath(targetAnimator.runtimeAnimatorController);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }

        if (controller == null)
        {
            documentation = "Could not load AnimatorController.";
            return;
        }

        // Use a simplified runtime documentation
        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"=== ANIMATOR DOCUMENTATION ===");
        sb.AppendLine($"Controller: {controller.name}");
        sb.AppendLine($"Layers: {controller.layers.Length}");
        sb.AppendLine($"Parameters: {controller.parameters.Length}");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("## PARAMETERS");
        foreach (var param in controller.parameters)
        {
            sb.AppendLine($"  - {param.name} ({param.type})");
        }
        sb.AppendLine();

        // Layers and States
        foreach (var layer in controller.layers)
        {
            sb.AppendLine($"## LAYER: {layer.name}");
            DocumentStateMachineSimple(sb, layer.stateMachine, 1);
        }

        documentation = sb.ToString();
        Debug.Log("Documentation generated. Check the component inspector.");
#else
        documentation = "Documentation generation requires Unity Editor.";
#endif
    }

#if UNITY_EDITOR
    private void DocumentStateMachineSimple(StringBuilder sb, AnimatorStateMachine sm, int depth)
    {
        string indent = new string(' ', depth * 2);
        
        sb.AppendLine($"{indent}State Machine: {sm.name}");
        if (sm.defaultState != null)
            sb.AppendLine($"{indent}  Default: {sm.defaultState.name}");
        
        sb.AppendLine($"{indent}  States:");
        foreach (var state in sm.states)
        {
            sb.AppendLine($"{indent}    - {state.state.name}");
            
            if (includeTransitionDetails)
            {
                foreach (var trans in state.state.transitions)
                {
                    string dest = trans.destinationState?.name ?? 
                                  trans.destinationStateMachine?.name ?? 
                                  (trans.isExit ? "Exit" : "?");
                    
                    List<string> conditions = new List<string>();
                    foreach (var cond in trans.conditions)
                    {
                        conditions.Add(FormatCondition(cond));
                    }
                    
                    string condStr = conditions.Count > 0 
                        ? $"[{string.Join(" && ", conditions)}]" 
                        : "[Exit Time]";
                    
                    sb.AppendLine($"{indent}      -> {dest} {condStr}");
                }
            }
        }

        // Sub state machines
        foreach (var subSm in sm.stateMachines)
        {
            DocumentStateMachineSimple(sb, subSm.stateMachine, depth + 2);
        }
    }

    private string FormatCondition(AnimatorCondition cond)
    {
        switch (cond.mode)
        {
            case AnimatorConditionMode.If:
                return $"{cond.parameter}=true";
            case AnimatorConditionMode.IfNot:
                return $"{cond.parameter}=false";
            case AnimatorConditionMode.Greater:
                return $"{cond.parameter}>{cond.threshold:F1}";
            case AnimatorConditionMode.Less:
                return $"{cond.parameter}<{cond.threshold:F1}";
            case AnimatorConditionMode.Equals:
                return $"{cond.parameter}=={(int)cond.threshold}";
            case AnimatorConditionMode.NotEqual:
                return $"{cond.parameter}!={(int)cond.threshold}";
            default:
                return $"{cond.parameter}?";
        }
    }
#endif

    [ContextMenu("Copy to Clipboard")]
    public void CopyToClipboard()
    {
        GUIUtility.systemCopyBuffer = documentation;
        Debug.Log("Documentation copied to clipboard!");
    }
}
