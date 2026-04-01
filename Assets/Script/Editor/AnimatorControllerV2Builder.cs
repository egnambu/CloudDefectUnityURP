using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

/// <summary>
/// Editor script that automatically creates the V2 Animator Controller setup.
/// Uses the old animator as a reference to extract animation clips.
/// </summary>
public class AnimatorControllerV2Builder : EditorWindow
{
    [Header("Source Reference")]
    [SerializeField] private AnimatorController sourceController;
    
    [Header("Avatar Mask")]
    [SerializeField] private AvatarMask torsoMask;
    
    [Header("Output")]
    [SerializeField] private string outputPath = "Assets/Characters/Pilot/Pilot_AnimController_V2.controller";
    
    // Cached animation clips from source
    private Dictionary<string, AnimationClip> clipCache = new Dictionary<string, AnimationClip>();
    
    [MenuItem("Tools/Animator Controller V2 Builder")]
    public static void ShowWindow()
    {
        var window = GetWindow<AnimatorControllerV2Builder>("Animator V2 Builder");
        window.minSize = new Vector2(400, 300);
    }
    
    private void OnGUI()
    {
        GUILayout.Label("Animator Controller V2 Builder", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox(
            "This tool creates a new Animator Controller with the V2 setup:\n" +
            "• Single LocomotionState integer parameter\n" +
            "• LandingType integer parameter\n" +
            "• No Any State or Exit transitions\n" +
            "• writeDefaults = false on all states",
            MessageType.Info
        );
        
        GUILayout.Space(10);
        
        sourceController = (AnimatorController)EditorGUILayout.ObjectField(
            "Source Animator (Old)",
            sourceController,
            typeof(AnimatorController),
            false
        );
        
        torsoMask = (AvatarMask)EditorGUILayout.ObjectField(
            "Torso Avatar Mask",
            torsoMask,
            typeof(AvatarMask),
            false
        );
        
        GUILayout.Space(5);
        
        outputPath = EditorGUILayout.TextField("Output Path", outputPath);
        
        GUILayout.Space(15);
        
        EditorGUI.BeginDisabledGroup(sourceController == null);
        
        if (GUILayout.Button("Build V2 Animator Controller", GUILayout.Height(40)))
        {
            BuildAnimatorController();
        }
        
        EditorGUI.EndDisabledGroup();
        
        if (sourceController == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the old Pilot_AnimController to extract animation clips from it.",
                MessageType.Warning
            );
        }
        
        GUILayout.Space(10);
        
        if (GUILayout.Button("Build WITHOUT Source (Manual Clip Assignment)", GUILayout.Height(30)))
        {
            BuildAnimatorControllerWithoutSource();
        }
    }
    
    private void BuildAnimatorController()
    {
        // Cache clips from source
        CacheClipsFromSource();
        
        // Create controller
        var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
        
        // Add parameters
        AddParameters(controller);
        
        // Setup base layer (layer 0 already exists)
        SetupBaseLayer(controller);
        
        // Add and setup aim layer
        SetupAimLayer(controller);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog(
            "Success",
            $"Animator Controller V2 created at:\n{outputPath}",
            "OK"
        );
        
        // Select the new controller
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }
    
    private void BuildAnimatorControllerWithoutSource()
    {
        clipCache.Clear();
        
        var controller = AnimatorController.CreateAnimatorControllerAtPath(outputPath);
        
        AddParameters(controller);
        SetupBaseLayer(controller);
        SetupAimLayer(controller);
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog(
            "Success",
            $"Animator Controller V2 created at:\n{outputPath}\n\n" +
            "NOTE: Animation clips are not assigned. You'll need to assign them manually.",
            "OK"
        );
        
        Selection.activeObject = controller;
        EditorGUIUtility.PingObject(controller);
    }
    
    #region Clip Caching
    
    private void CacheClipsFromSource()
    {
        clipCache.Clear();
        
        if (sourceController == null) return;
        
        // Recursively find all clips in the source controller
        foreach (var layer in sourceController.layers)
        {
            CacheClipsFromStateMachine(layer.stateMachine);
        }
        
        Debug.Log($"[AnimatorV2Builder] Cached {clipCache.Count} animation clips from source");
    }
    
    private void CacheClipsFromStateMachine(AnimatorStateMachine sm)
    {
        if (sm == null) return;
        
        foreach (var state in sm.states)
        {
            CacheClipFromMotion(state.state.motion);
        }
        
        foreach (var subSM in sm.stateMachines)
        {
            CacheClipsFromStateMachine(subSM.stateMachine);
        }
    }
    
    private void CacheClipFromMotion(Motion motion)
    {
        if (motion == null) return;
        
        if (motion is AnimationClip clip)
        {
            if (!clipCache.ContainsKey(clip.name))
            {
                clipCache[clip.name] = clip;
            }
        }
        else if (motion is BlendTree bt)
        {
            foreach (var child in bt.children)
            {
                CacheClipFromMotion(child.motion);
            }
        }
    }
    
    private AnimationClip GetClip(string name)
    {
        if (clipCache.TryGetValue(name, out var clip))
        {
            return clip;
        }
        
        Debug.LogWarning($"[AnimatorV2Builder] Clip not found: {name}");
        return null;
    }
    
    #endregion
    
    #region Parameters
    
    private void AddParameters(AnimatorController controller)
    {
        // Primary state control
        controller.AddParameter("LocomotionState", AnimatorControllerParameterType.Int);
        controller.AddParameter("LandingType", AnimatorControllerParameterType.Int);
        controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
        
        // Blend parameters
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("AimX", AnimatorControllerParameterType.Float);
        controller.AddParameter("AimY", AnimatorControllerParameterType.Float);
        controller.AddParameter("HoverX", AnimatorControllerParameterType.Float);
        controller.AddParameter("HoverY", AnimatorControllerParameterType.Float);
        controller.AddParameter("FlightX", AnimatorControllerParameterType.Float);
        controller.AddParameter("FlightY", AnimatorControllerParameterType.Float);
        
        Debug.Log("[AnimatorV2Builder] Parameters added");
    }
    
    #endregion
    
    #region Base Layer
    
    private void SetupBaseLayer(AnimatorController controller)
    {
        var baseLayer = controller.layers[0];
        var rootSM = baseLayer.stateMachine;
        
        // Clear default state if any
        rootSM.states = new ChildAnimatorState[0];
        rootSM.stateMachines = new ChildAnimatorStateMachine[0];
        rootSM.anyStateTransitions = new AnimatorStateTransition[0];
        
        // Create all states
        var grounded = CreateState(rootSM, "Grounded", CreateGroundedBlendTree(controller), new Vector3(0, 0, 0));
        var jump = CreateState(rootSM, "Jump", GetClip("TwinSword_Jump_Start"), new Vector3(0, 100, 0));
        var fall = CreateState(rootSM, "Fall", GetClip("TwinSword_Jump_Loop"), new Vector3(0, 200, 0));
        var lightLand = CreateState(rootSM, "Light Land", GetClip("TwinSword_Jump_End"), new Vector3(-150, 300, 0));
        var heavyLand = CreateState(rootSM, "Heavy Land", GetClip("A_SuperheroLanding_C"), new Vector3(150, 300, 0));
        var hoverStart = CreateState(rootSM, "Hover Start", GetClip("A_Flight_Hover_Start_B"), new Vector3(-300, 100, 0), 2f);
        var hoverBlend = CreateState(rootSM, "Hover Blend", CreateHoverBlendTree(controller), new Vector3(-300, 200, 0));
        var flightStart = CreateState(rootSM, "Flight Start", GetClip("A_Flight_FastMove_Start_A"), new Vector3(300, 100, 0), 2f);
        var flightBlend = CreateState(rootSM, "Flight Blend", CreateFlightBlendTree(controller), new Vector3(300, 200, 0));
        
        // Set default state
        rootSM.defaultState = grounded;
        
        // === TRANSITIONS ===
        
        // From Grounded
        AddTransition(grounded, jump, false, 0f, 0.15f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 1));
        AddTransition(grounded, fall, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 2));
        AddTransition(grounded, hoverStart, false, 0f, 0.25f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 3));
        AddTransition(grounded, flightStart, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 4));
        
        // From Jump
        AddTransition(jump, fall, true, 0.85f, 0.15f, TransitionInterruptionSource.None);
        AddTransition(jump, hoverStart, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 3));
        AddTransition(jump, flightStart, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 4));
        
        // From Fall
        AddTransition(fall, lightLand, false, 0f, 0.1f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0),
            new Condition("LandingType", AnimatorConditionMode.Equals, 1));
        AddTransition(fall, heavyLand, false, 0f, 0.15f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0),
            new Condition("LandingType", AnimatorConditionMode.Equals, 2));
        AddTransition(fall, hoverStart, false, 0f, 0.25f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 3));
        AddTransition(fall, flightStart, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 4));
        
        // From Light Land
        AddTransition(lightLand, grounded, true, 0.7f, 0.2f, TransitionInterruptionSource.None,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0));
        
        // From Heavy Land
        AddTransition(heavyLand, grounded, true, 0.8f, 0.25f, TransitionInterruptionSource.None,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0));
        
        // From Hover Start
        AddTransition(hoverStart, hoverBlend, true, 0.9f, 0.2f, TransitionInterruptionSource.None);
        AddTransition(hoverStart, flightStart, false, 0f, 0.15f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 4));
        
        // From Hover Blend
        AddTransition(hoverBlend, fall, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 2));
        AddTransition(hoverBlend, lightLand, false, 0f, 0.15f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0),
            new Condition("LandingType", AnimatorConditionMode.Equals, 1));
        AddTransition(hoverBlend, flightStart, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 4));
        
        // From Flight Start
        AddTransition(flightStart, flightBlend, true, 0.9f, 0.25f, TransitionInterruptionSource.None);
        AddTransition(flightStart, hoverBlend, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 3));
        
        // From Flight Blend
        AddTransition(flightBlend, fall, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 2));
        AddTransition(flightBlend, hoverBlend, false, 0f, 0.25f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 3));
        AddTransition(flightBlend, lightLand, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0),
            new Condition("LandingType", AnimatorConditionMode.Equals, 1));
        AddTransition(flightBlend, heavyLand, false, 0f, 0.2f, TransitionInterruptionSource.Source,
            new Condition("LocomotionState", AnimatorConditionMode.Equals, 0),
            new Condition("LandingType", AnimatorConditionMode.Equals, 2));
        
        Debug.Log("[AnimatorV2Builder] Base layer setup complete");
    }
    
    #endregion
    
    #region Aim Layer
    
    private void SetupAimLayer(AnimatorController controller)
    {
        // Add new layer
        controller.AddLayer("Aim");
        
        var layers = controller.layers;
        var aimLayer = layers[1];
        aimLayer.defaultWeight = 1f;
        aimLayer.blendingMode = AnimatorLayerBlendingMode.Override;
        aimLayer.avatarMask = torsoMask;
        
        var aimSM = aimLayer.stateMachine;
        
        // Clear
        aimSM.states = new ChildAnimatorState[0];
        aimSM.anyStateTransitions = new AnimatorStateTransition[0];
        
        // Create states
        var empty = CreateState(aimSM, "Empty", null, new Vector3(0, 0, 0));
        var aimBlend = CreateState(aimSM, "Aim Blend", CreateAimBlendTree(controller), new Vector3(0, 100, 0));
        
        aimSM.defaultState = empty;
        
        // Transitions
        AddTransition(empty, aimBlend, false, 0f, 0.2f, TransitionInterruptionSource.None,
            new Condition("IsAiming", AnimatorConditionMode.If, 0));
        AddTransition(aimBlend, empty, false, 0f, 0.25f, TransitionInterruptionSource.None,
            new Condition("IsAiming", AnimatorConditionMode.IfNot, 0));
        
        // Apply layer changes
        controller.layers = layers;
        
        Debug.Log("[AnimatorV2Builder] Aim layer setup complete");
    }
    
    #endregion
    
    #region Blend Trees
    
    private BlendTree CreateGroundedBlendTree(AnimatorController controller)
    {
        var bt = new BlendTree
        {
            name = "Grounded Blend",
            blendType = BlendTreeType.FreeformCartesian2D,
            blendParameter = "MoveX",
            blendParameterY = "MoveY",
            useAutomaticThresholds = false
        };
        
        bt.AddChild(GetClip("TwinSword_Common_Idle"), new Vector2(0, 0));
        bt.AddChild(GetClip("TwinSword_Common_Run_Loop"), new Vector2(0, 1));
        bt.AddChild(GetClip("TwinSword_Common_Sprint"), new Vector2(0, 1.5f));
        
        AssetDatabase.AddObjectToAsset(bt, controller);
        
        return bt;
    }
    
    private BlendTree CreateHoverBlendTree(AnimatorController controller)
    {
        var bt = new BlendTree
        {
            name = "Hover Blend",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "HoverX",
            blendParameterY = "HoverY",
            useAutomaticThresholds = false
        };
        
        // Try to find hover clips, fall back to generic names
        var hoverLoop = GetClip("A_Flight_Hover_Loop") ?? GetClip("Hover_Loop");
        var hoverFwd = GetClip("A_Flight_Hover_Forward") ?? GetClip("Hover_Forward") ?? hoverLoop;
        var hoverBwd = GetClip("A_Flight_Hover_Backward") ?? GetClip("Hover_Backward") ?? hoverLoop;
        var hoverLeft = GetClip("A_Flight_Hover_Left") ?? GetClip("Hover_Left") ?? hoverLoop;
        var hoverRight = GetClip("A_Flight_Hover_Right") ?? GetClip("Hover_Right") ?? hoverLoop;
        
        bt.AddChild(hoverLoop, new Vector2(0, 0));
        bt.AddChild(hoverFwd, new Vector2(0, 1));
        bt.AddChild(hoverBwd, new Vector2(0, -1));
        bt.AddChild(hoverLeft, new Vector2(-1, 0));
        bt.AddChild(hoverRight, new Vector2(1, 0));
        
        AssetDatabase.AddObjectToAsset(bt, controller);
        
        return bt;
    }
    
    private BlendTree CreateFlightBlendTree(AnimatorController controller)
    {
        var bt = new BlendTree
        {
            name = "Flight Blend",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "FlightX",
            blendParameterY = "FlightY",
            useAutomaticThresholds = false
        };
        
        // Try to find flight clips
        var flightLoop = GetClip("A_Flight_FastMove_Loop") ?? GetClip("Flight_Loop");
        var flightFwd = GetClip("A_Flight_FastMove_Forward") ?? GetClip("Flight_Forward") ?? flightLoop;
        var flightBwd = GetClip("A_Flight_FastMove_Backward") ?? GetClip("Flight_Backward") ?? flightLoop;
        var flightLeft = GetClip("A_Flight_FastMove_Left") ?? GetClip("Flight_Left") ?? flightLoop;
        var flightRight = GetClip("A_Flight_FastMove_Right") ?? GetClip("Flight_Right") ?? flightLoop;
        
        bt.AddChild(flightLoop, new Vector2(0, 0));
        bt.AddChild(flightFwd, new Vector2(0, 1));
        bt.AddChild(flightBwd, new Vector2(0, -1));
        bt.AddChild(flightLeft, new Vector2(-1, 0));
        bt.AddChild(flightRight, new Vector2(1, 0));
        
        AssetDatabase.AddObjectToAsset(bt, controller);
        
        return bt;
    }
    
    private BlendTree CreateAimBlendTree(AnimatorController controller)
    {
        var bt = new BlendTree
        {
            name = "Aim Blend",
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = "AimX",
            blendParameterY = "AimY",
            useAutomaticThresholds = false
        };
        
        bt.AddChild(GetClip("AS_Rifle_Aim"), new Vector2(0, 0));
        bt.AddChild(GetClip("AS_Rifle_WalkFwd_Aim"), new Vector2(0, 1));
        bt.AddChild(GetClip("AS_Rifle_WalkBwd_Aim"), new Vector2(0, -1));
        bt.AddChild(GetClip("AS_Rifle_WalkLeft_Aim"), new Vector2(-0.5f, 0.5f));
        bt.AddChild(GetClip("AS_Rifle_WalkRight_Aim"), new Vector2(0.5f, 0.5f));
        bt.AddChild(GetClip("AS_Rifle_WalkLeft_Aim"), new Vector2(-0.5f, -0.5f));
        bt.AddChild(GetClip("AS_Rifle_WalkRight_Aim"), new Vector2(0.5f, -0.5f));
        
        AssetDatabase.AddObjectToAsset(bt, controller);
        
        return bt;
    }
    
    #endregion
    
    #region State & Transition Helpers
    
    private AnimatorState CreateState(AnimatorStateMachine sm, string name, Motion motion, Vector3 position, float speed = 1f)
    {
        var state = sm.AddState(name, position);
        state.motion = motion;
        state.speed = speed;
        state.writeDefaultValues = false; // CRITICAL: Set to false
        return state;
    }
    
    private struct Condition
    {
        public string parameter;
        public AnimatorConditionMode mode;
        public float threshold;
        
        public Condition(string param, AnimatorConditionMode mode, float threshold)
        {
            this.parameter = param;
            this.mode = mode;
            this.threshold = threshold;
        }
    }
    
    private void AddTransition(
        AnimatorState source,
        AnimatorState destination,
        bool hasExitTime,
        float exitTime,
        float duration,
        TransitionInterruptionSource interruptionSource,
        params Condition[] conditions)
    {
        var transition = source.AddTransition(destination);
        
        transition.hasExitTime = hasExitTime;
        transition.exitTime = exitTime;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.offset = 0f;
        transition.interruptionSource = interruptionSource;
        transition.orderedInterruption = true;
        transition.canTransitionToSelf = false; // CRITICAL: Prevent self-loops
        
        foreach (var condition in conditions)
        {
            transition.AddCondition(condition.mode, condition.threshold, condition.parameter);
        }
    }
    
    #endregion
}
