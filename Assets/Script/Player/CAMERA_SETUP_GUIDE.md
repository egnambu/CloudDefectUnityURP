# TPS Camera Setup Guide (Cinemachine 3.1.5)

## Overview
This system uses two Cinemachine cameras that switch based on whether the player is aiming:
- **Free-Look Camera**: For normal movement (character rotates toward movement direction)
- **Aiming Camera**: For aiming mode (character strafes and faces camera direction)

---

## Setup Instructions

### 1. Create the Cameras in Your Scene

#### A. Free-Look Camera (Normal Movement)
1. In Hierarchy, right-click → **Cinemachine** → **Targeted Cameras** → **3rd Person Follow Camera**
2. Rename it to `CM_FreeLook`
3. In Inspector:
   - **Priority**: 10 (active by default)
   - **Tracking Target**: Drag your Player GameObject here
   
4. Configure **CinemachineOrbitalFollow** component:
   - **Binding Mode**: **Lock To Target With World Up** (IMPORTANT!)
     - This prevents camera flipping when character rotates
     - Keeps camera upright relative to world, not character
   - **Orbit Style**: **Three Ring** (for smooth vertical movement)
     - **Why Three Ring?** Allows camera to smoothly move between different heights
     - Top ring for looking down, center for normal view, bottom for looking up
   - **Orbits**: Expand this section
     - **Top Rig Height**: 4, **Radius**: 5
     - **Center Rig Height**: 2, **Radius**: 6
     - **Bottom Rig Height**: 0.5, **Radius**: 5
   - **Horizontal Axis**:
     - **Value**: 0
     - **Range**: -180 to 180
     - **Wrap**: Checked
     - **Recentering**:
       - **Enabled**: Check this if you want auto-center behind player
       - **Target**: **Look At Target** (camera will recenter to face the player)
       - **Wait Time**: 2 seconds (how long to wait before recentering)
       - **Recentering Time**: 1 second (how fast to recenter)
   - **Vertical Axis**:
     - **Value**: 0.5
     - **Range**: 0 to 1 (this controls which orbit ring to use)
     - **Recentering**:
       - **Enabled**: Usually unchecked (don't recenter vertical position)
       - **Target**: **Look At Target**
       - **Wait Time**: 1 second
       - **Recentering Time**: 0.5 seconds
   - **Radial Axis**:
     - **Value**: 1 (distance multiplier)
     - **Range**: 0.5 to 1.5 (allows zoom in/out)
     - **Recentering**:
       - **Enabled**: Usually unchecked (keep zoom level)
       - **Target**: **Look At Target**
       - **Wait Time**: 1 second
       - **Recentering Time**: 0.5 seconds

5. Configure **CinemachineRotationComposer** component:
   - **Tracked Point Offset**: Y: 1.5 (eye level)
   - **Lookahead Time**: 0.2
   - **Lookahead Smoothing**: 5
   - **Horizontal/Vertical Damping**: 0.5-1

#### B. Aiming Camera (Combat Mode)
1. In Hierarchy, right-click → **Cinemachine** → **Targeted Cameras** → **3rd Person Camera**
2. Rename it to `CM_Aiming`
3. In Inspector:
   - **Priority**: 0 (inactive by default)
   - **Tracking Target**: Drag your Player GameObject here
   
4. Configure **CinemachineOrbitalFollow** component:
   - **Binding Mode**: **Lock To Target With World Up** (IMPORTANT!)
     - Prevents camera from rotating with character's rotation
     - Camera stays upright relative to world
   - **Orbit Style**: **Three Ring** (or **Single Ring** for simpler setup)
     - **Why same component?** OrbitalFollow is multipurpose - we configure it differently for aiming
     - Single Ring is simpler and more stable for close-range aiming
   - **Orbits**: 
     - **Center Rig Height**: 1.5, **Radius**: 2.5 (tighter for aiming)
   - **Horizontal Axis**:
     - **Value**: 0
     - **Range**: -180 to 180
     - **Wrap**: Checked
     - **Recentering**:
       - **Enabled**: Unchecked (don't auto-center while aiming)
       - **Target**: **Look At Target**
       - **Wait Time**: 1 second
       - **Recentering Time**: 0.5 seconds
   - **Vertical Axis**:
     - **Value**: 0.5
     - **Range**: 0 to 1
     - **Recentering**:
       - **Enabled**: Unchecked (keep vertical position)
       - **Target**: **Look At Target**
       - **Wait Time**: 1 second
       - **Recentering Time**: 0.5 seconds
   - **Radial Axis**:
     - **Value**: 1
     - **Recentering**:
       - **Enabled**: Unchecked (keep zoom level)
       - **Target**: **Look At Target**
       - **Wait Time**: 1 second
       - **Recentering Time**: 0.5 seconds
   - **Target Offset**: 
     - X: 0.8 (offset to right for over-shoulder)
     - Y: 1.6 (head level)

5. Configure **CinemachineRotationComposer** component:
   - **Tracked Point Offset**: Y: 1.5
   - **Screen Position**: X: 0.6, Y: 0.55 (off-center for over-shoulder)
   - **Horizontal/Vertical Damping**: 0.2-0.5 (more responsive)

---

### 2. Setup the Camera Manager

1. Create an **empty GameObject** in your scene
2. Rename it to `CameraManager`
3. Add the **TPSCameraManager** component
4. In Inspector:
   - **Free Look Camera**: Drag `CM_FreeLook` here
   - **Aiming Camera**: Drag `CM_Aiming` here
   - **Active Priority**: 10
   - **Inactive Priority**: 0
   - **Free Look Sensitivity**: 2.0
   - **Aiming Sensitivity**: 1.0

---

### 3. Configure Your Main Camera

Your Main Camera should have:
- **CinemachineBrain** component (auto-added by Cinemachine)
- **Default Blend**:
  - **Style**: EaseInOut
  - **Time**: 0.3-0.5 seconds

---

### 4. Lock Cursor (Optional but Recommended)

Add this to your player script's `Start()` method:
```csharp
Cursor.lockState = CursorLockMode.Locked;
Cursor.visible = false;
```

---

### 5. Test It

1. **Play the scene**
2. Move mouse - camera should orbit around player
3. Use **WASD** to move - character should turn toward movement direction
4. Hold **Right Mouse Button** (AimDownSights) - camera should zoom in over shoulder
5. While aiming, **WASD** should now strafe instead of turning

---

## Troubleshooting

### Camera flips when turning around
- **CRITICAL**: Set **Binding Mode** to **Lock To Target With World Up** in CinemachineOrbitalFollow
- This is THE solution to camera flipping issues
- Make sure **Horizontal Axis → Wrap** is checked
- Ensure both cameras follow the same target point (not different bones)

### What do the Binding Modes do?
- **Lock To Target On Assign**: Camera rotation locks to target's rotation at assignment time
- **Lock To Target With World Up**: Camera follows target but stays upright (RECOMMENDED for TPS)
- **Lock To Target No Roll**: Follows target rotation but prevents camera roll
- **Lock To Target Forward**: Camera always faces target's forward direction
- **World Space**: Camera is completely independent of target rotation
- **Lazy Follow**: Camera smoothly follows target with damping

### What do the Recentering Target options do?
- **Look At Target**: Camera will recenter to face the target (player)
- **Tracking Target**: Camera will recenter to align with target's movement direction
- **Custom Target**: You can specify a different GameObject to recenter toward
- **World Space**: Camera will recenter to a fixed world direction

### Recentering Behavior:
- **Enabled**: Turns on/off auto-recentering
- **Wait Time**: How long player must be idle before camera starts recentering
- **Recentering Time**: How fast the camera moves back to center position
- **Damping**: Smooths the recentering movement (higher = smoother but slower)

### What do the Orbit Styles do?
- **Three Ring**: Uses Top/Center/Bottom rings, interpolates between them (smooth height changes)
- **Single Ring**: Only uses Center ring (simpler, good for aiming camera)
- **Flat**: No vertical orbit control, only horizontal

### Why use CinemachineOrbitalFollow for both cameras?
**CinemachineOrbitalFollow is extremely versatile and multipurpose!**

**For Free-Look Mode:**
- **Orbit Style**: Three Ring - allows full vertical freedom
- **Large Radius**: 5-6 units for cinematic feel
- **Recentering ON**: Auto-centers behind player
- **Full Axis Control**: Mouse controls full orbit

**For Aiming Mode:**
- **Orbit Style**: Single Ring - stable, fixed height
- **Small Radius**: 2-3 units for close combat
- **Recentering OFF**: Camera stays where you position it
- **Constrained Control**: Mouse has less influence, camera stays put

**Same component, different personality through settings!** 🎭

### Camera too slow/fast
- Adjust **Mouse Sensitivity** in TPSCameraManager (in Inspector)
- Check your Input Manager's `mouseSensitivity` value

### Camera jerky/stuttering
- Make sure your Main Camera's **CinemachineBrain** is set to:
  - **Update Method**: Smart Update (or Late Update)
  - **Blend Update Method**: Late Update

### Character not rotating correctly
- Verify your Player has the **FP_Movement** script
- Check that camera manager is found (check console for warnings)

### Cameras not switching
- Make sure **TPSCameraManager** is in the scene and references are assigned
- Check Priority values in Inspector (10 vs 0)
- Verify InputBindingManager detects right mouse button

### Camera going through walls
- Add **CinemachineDeoccluder** extension to each camera:
  - Select camera → Inspector → Add Extension → **Deoccluder**
  - **Collision Filter**: Select layers (Default, Environment, etc.)
  - **Damping**: 0.5-1
  - **Damping When Occluded**: 0

---

## Quick Setup (Alternative Method)

If the above seems complex, try this simpler approach:

### Simple Free-Look Camera:
1. Right-click in Hierarchy → **Cinemachine** → **Create Third Person Camera**
2. Set **Tracking Target** to your player
3. In **CinemachineOrbitalFollow**:
   - **Binding Mode**: **Lock To Target With World Up**
   - **Orbit Style**: **Three Ring**
   - **Shoulder Offset**: (0, 1.5, 0)
   - **Camera Distance**: 5-6

### Simple Aiming Camera:
1. Duplicate the free-look camera
2. Rename to `CM_Aiming`
3. Set **Priority** to 0
4. In **CinemachineOrbitalFollow**:
   - **Binding Mode**: **Lock To Target With World Up** (keep same)
   - **Orbit Style**: **Single Ring** (simpler for aiming)
   - **Camera Distance**: 2-3
   - **Shoulder Offset** X: 0.8 (right shoulder view)

Then hook them up to the TPSCameraManager as described in step 2 above.

---

## Advanced Customization

### Different FOV for Aiming
- On Aiming Camera, adjust **Lens → Field of View** to 45-50 for zoom effect

### Auto-Recenter Behind Player
- On Free-Look Camera → **CinemachineOrbitalFollow** → **Horizontal Axis**:
  - Check **Recentering Enabled**
  - **Target**: **Look At Target** (faces the player)
  - **Wait Time**: 2 seconds
  - **Recentering Time**: 1 second

### Advanced Recentering
- **Vertical Axis Recentering**: Can auto-adjust camera height when player stops moving
- **Radial Axis Recentering**: Can auto-zoom camera in/out based on player speed
- **Custom Target**: Set recentering to face a different object (like a waypoint or enemy)

### Collision Avoidance
Already mentioned above - use **CinemachineDeoccluder** extension.

### Speed-Based Camera
- On Free-Look Camera → **CinemachineOrbitalFollow** → **Radial Axis**:
  - Set **Range** to wider values (0.3 to 1.5)
  - Create script to adjust **RadialAxis.Value** based on player speed

---

## Tips
- **Damping** in RotationComposer controls camera smoothness (lower = snappier)
- **Lookahead** makes camera anticipate where you're moving
- Use **Dead Zone** in RotationComposer to prevent camera moving on small player movements
- The **Vertical Axis** (0-1) in OrbitalFollow interpolates between top/center/bottom rigs
- **Recentering** is great for free-look cameras but should be OFF for aiming cameras
- **Wait Time** prevents annoying recentering when player briefly stops




using UnityEngine;

public class TPSController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float turnSpeed = 720f; // degrees per second
    public float jumpForce = 6f;
    public float gravity = -20f;

    public CharacterController controller;
    public Transform cam;
    public Animator animator;
    public Pilot1 input;
    public Vector2 moveInput;

    private Vector3 velocity;
    private bool grounded;
    private bool isAiming;
    private bool jumpPressed;
    private float smoothMoveX;
    private float smoothMoveY;
    private float smoothSpeed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = Camera.main.transform;

        input = new Pilot1();
    }

    void OnEnable()
    {
        input.Enable();
    }

    void OnDisable()
    {
        input.Disable();
    }

    void OnDestroy()
    {
        input.Dispose();
    }

    void Update()
    {
        moveInput = input.PlayerA.Move.ReadValue<Vector2>();
        isAiming = input.PlayerA.Aim.IsPressed();
        jumpPressed = input.PlayerA.Jump.triggered;

        Move();
        UpdateAnimator();
    }

    void Move()
    {
        Vector3 inputDir = new Vector3(moveInput.x, 0, moveInput.y);

        if (inputDir.sqrMagnitude < 0.01f)
        {
            // No movement, do nothing
            return;
        }

        // Convert input to camera-relative
        Vector3 camForward = cam.forward; 
        Vector3 camRight = cam.right;

        camForward.y = 0;
        camRight.y = 0;

        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDir = camForward * inputDir.z + camRight * inputDir.x;

        // Rotate based on aiming
        if (isAiming)
        {
            // When aiming, strafe and face camera forward
            Quaternion targetRotation = Quaternion.LookRotation(camForward);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }
        else
        {
            // Free walk, rotate to face movement direction
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        // Ground check and jump
        grounded = controller.isGrounded;
        if (grounded)
        {
            if (velocity.y < 0)
                velocity.y = -2f; // small downward bias for sticking to ground

            if (jumpPressed)
            {
                velocity.y = jumpForce;
                if (animator != null) animator.SetBool("IsJumping", true);
                StartCoroutine(ResetJumpBool());
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Move the character
        controller.Move((moveDir * moveSpeed + Vector3.up * velocity.y) * Time.deltaTime);
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Interpolate towards target values for smooth visual updates
        const float lerpRate = 12f;

        float targetSpeed = moveInput.magnitude;
        smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, Time.deltaTime * lerpRate);

        smoothMoveX = Mathf.Lerp(smoothMoveX, moveInput.x, Time.deltaTime * lerpRate);
        smoothMoveY = Mathf.Lerp(smoothMoveY, moveInput.y, Time.deltaTime * lerpRate);

        animator.SetFloat("MoveX", smoothMoveX);
        animator.SetFloat("MoveY", smoothMoveY);
        animator.SetFloat("Speed", smoothSpeed);
    }

    private System.Collections.IEnumerator ResetJumpBool()
    {
        yield return new WaitForSeconds(0.1f);
        if (animator != null) animator.SetBool("IsJumping", false);
    }
}
