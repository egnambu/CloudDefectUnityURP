# Pilot Controller - Beginner's Guide

A third-person character controller with ground movement, flight, and aiming capabilities.

---

## 🎮 What Does This Script Do?

This script controls your player character. It handles:
- **Walking/Running** on the ground
- **Jumping** and falling
- **Flying** through the air
- **Aiming** (works in any state!)
- **Camera switching** based on what you're doing

---

## 🧠 Core Concepts

### States vs Flags

Think of it like this:

| **State** (What you ARE) | **Flag** (What you're ALSO doing) |
|--------------------------|-----------------------------------|
| You can only be ONE state at a time | You can have MULTIPLE flags at once |
| `Grounded`, `Airborne`, `Flying`, `Landing` | `Aiming`, `Sprinting`, `Hovering` |

**Example:** You can be `Flying` (state) while also `Aiming` (flag) at the same time!

---

## 📊 Player States Explained

```
┌─────────────┐
│  Grounded   │ ◄── Standing on the ground
└──────┬──────┘
       │ (jump or walk off edge)
       ▼
┌─────────────┐
│  Airborne   │ ◄── In the air, falling
└──────┬──────┘
       │ (press Launch button)
       ▼
┌─────────────┐
│   Flying    │ ◄── Actively flying with propulsion
└──────┬──────┘
       │ (release Launch or approach ground)
       ▼
┌─────────────┐
│  Landing    │ ◄── Transitioning back to ground
└─────────────┘
```

---

## 🚩 Player Flags Explained

Flags are **modifiers** that can be active alongside any state:

| Flag | When Active | What It Does |
|------|-------------|--------------|
| `Aiming` | Hold Aim button | Face camera direction, use aim camera |
| `Sprinting` | Hold Launch while grounded | Move faster on ground |
| `Hovering` | Hold Hover while airborne | Stop falling, float in place |

---

## 🔧 How To Set Up

### Step 1: Add the Script
1. Select your player GameObject in the Hierarchy
2. Drag `PilotController.cs` onto it (or use Add Component)

### Step 2: Required Components
The script needs these on your player:

| Component | How to Add |
|-----------|------------|
| **CharacterController** | Add Component → Physics → Character Controller |
| **Animator** | Should be on your character model (child object) |

### Step 3: Assign References in Inspector

| Field | What to Drag Here |
|-------|-------------------|
| `Mesh Root` | The child object with your character's mesh/model |
| `Follow Cam` | Your main Cinemachine camera |
| `Aim Cam` | Your over-shoulder aim camera |
| `Flight Cam` | Your flight camera |
| `Ground Layer Mask` | Select which layers count as "ground" |

### Step 4: Set Up Input
This script uses Unity's **New Input System**. You need:
1. An Input Actions asset called `Pilot1`
2. An action map called `PlayerA` with these actions:
   - `Move` (Vector2 - WASD/Left Stick)
   - `Aim` (Button - Right Mouse/Left Trigger)
   - `Jump` (Button - Space/A Button)
   - `Launch` (Button - Shift/B Button)
   - `Hover` (Button - your choice)

---

## 📝 Code Structure

The script runs in this order every frame:

```
Update() - Runs every frame
├── ReadInput()      → Get button presses
├── UpdateState()    → Check if state should change
├── UpdateFlags()    → Set active flags
├── UpdateCamera()   → Switch cameras
└── UpdateAnimator() → Update animation parameters

FixedUpdate() - Runs at fixed intervals (physics)
└── Based on current state:
    ├── Grounded/Airborne → Move() + ApplyGravity()
    ├── Flying            → HandleFlight()
    └── Landing           → HandleLanding()
```

---

## 🎬 Animator Parameters

Set these up in your Animator Controller:

| Parameter | Type | Used For |
|-----------|------|----------|
| `IsAiming` | Bool | Aim pose/layer |
| `IsFlying` | Bool | Flight animation |
| `IsHovering` | Bool | Hover animation |
| `IsJumping` | Bool | Jump animation |
| `IsLanding` | Bool | Landing animation |
| `Landing` | Trigger | Trigger landing anim |
| `MoveX` | Float | Strafe blend (-1 to 1) |
| `MoveY` | Float | Forward blend (0 to 1.5) |
| `AimX` | Float | Aim strafe blend |
| `AimY` | Float | Aim forward blend |

**Aim Layer:** The script controls layer weight on layer index 2 for aiming overlay.

---

## 🎥 Camera Setup

You need 3 Cinemachine cameras:

1. **Follow Cam** - Normal third-person follow
2. **Aim Cam** - Over-the-shoulder for aiming
3. **Flight Cam** - Wider view for flying

The script sets `Priority = 10` on the active camera and `0` on others.

---

## 🔌 Using From Other Scripts

Want to check the player's state from another script?

```csharp
// Get reference to the controller
Pilot_Controller player = GetComponent<Pilot_Controller>();

// Check current state
if (player.State == PlayerState.Flying)
{
    Debug.Log("Player is flying!");
}

// Check flags (these work in any state)
if (player.IsAiming)
{
    Debug.Log("Player is aiming!");
}

if (player.IsGrounded)
{
    Debug.Log("Player is on the ground!");
}
```

---

## ⚙️ Tuning Values

| Setting | Default | What It Does |
|---------|---------|--------------|
| `moveSpeed` | 5 | Walking speed |
| `sprintSpeed` | 10 | Running speed |
| `turnSpeed` | 720 | How fast you rotate (degrees/sec) |
| `flightSpeed` | 15 | Max flying speed |
| `flightAcceleration` | 8 | How fast you speed up in flight |
| `pitchSpeed` | 80 | Up/down rotation in flight |
| `yawSpeed` | 80 | Left/right rotation in flight |
| `gravity` | -20 | Fall speed |
| `jumpBufferTime` | 0.1 | Input forgiveness for jumping |

---

## ❓ Common Issues

### Character won't move
- Check that `CharacterController` is added
- Make sure Input System is set up correctly
- Verify `Pilot1` input asset exists and is generated

### Character falls through ground
- Set `Ground Layer Mask` to include your ground layer
- Make sure ground objects have colliders

### Cameras don't switch
- Assign all 3 Cinemachine cameras in Inspector
- Make sure Cinemachine Brain is on your Main Camera

### Animations not playing
- Check Animator parameters match the names above
- Verify Animator is assigned (auto-finds in children)

---

## 📚 Learn More

- [Unity CharacterController](https://docs.unity3d.com/Manual/class-CharacterController.html)
- [Unity Input System](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html)
- [Cinemachine](https://docs.unity3d.com/Packages/com.unity.cinemachine@3.0/manual/index.html)