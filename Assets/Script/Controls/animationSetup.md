# Pilot Animator Controller V2 Setup Guide

This guide explains how to set up the new Animator Controller to work with `PilotTypeControllerV2.cs`.

---

## Overview of Changes

### Old System Problems
- **5 boolean parameters** (`IsAiming`, `IsFalling`, `IsFlying`, `IsHovering`, `IsGrounded`) created 32 possible combinations
- Race conditions when multiple bools changed in the same frame
- Exit transitions defaulted to ground state even when airborne
- `writeDefaults: true` caused animation flickering

### New System Solution
- **Single integer `LocomotionState`** parameter (0-4) - only one state possible
- **Integer `LandingType`** parameter set BEFORE state change
- **Boolean `IsAiming`** for aim layer control only
- Explicit transitions between all states - no Exit transitions
- `writeDefaults: false` on all states

---

## Step 1: Create Parameters

Delete all existing parameters and create these new ones:

| Parameter Name | Type | Default Value | Purpose |
|----------------|------|---------------|---------|
| `LocomotionState` | Int | 0 | Primary state control |
| `LandingType` | Int | 0 | Landing animation selection |
| `IsAiming` | Bool | false | Aim layer blend control |
| `MoveX` | Float | 0 | Ground locomotion X |
| `MoveY` | Float | 0 | Ground locomotion Y |
| `AimX` | Float | 0 | Aim blend X |
| `AimY` | Float | 0 | Aim blend Y |
| `HoverX` | Float | 0 | Hover blend X |
| `HoverY` | Float | 0 | Hover blend Y |
| `FlightX` | Float | 0 | Flight blend X |
| `FlightY` | Float | 0 | Flight blend Y |

### LocomotionState Values
```
0 = Grounded
1 = Jumping
2 = Falling
3 = Hovering
4 = Flying
```

### LandingType Values
```
0 = None (no landing)
1 = Light landing
2 = Heavy landing
```

---

## Step 2: Layer Setup

### Layer 0: Base (Movement Layer)
- **Weight:** 1
- **Blending Mode:** Override
- **Avatar Mask:** None (full body)
- **IK Pass:** Off

### Layer 1: Aim
- **Weight:** 1 (controlled by script via `SetLayerWeight`)
- **Blending Mode:** Override
- **Avatar Mask:** SK_Torso (upper body only)
- **IK Pass:** Off

---

## Step 3: Base Layer State Machine

Create a FLAT state machine (no sub-state machines) with these states:

### States to Create

| State Name | Motion | Speed | Write Defaults |
|------------|--------|-------|----------------|
| Grounded | Blend Tree (see below) | 1 | **false** |
| Jump | TwinSword_Jump_Start | 1 | **false** |
| Fall | TwinSword_Jump_Loop | 1 | **false** |
| Light Land | TwinSword_Jump_End | 1 | **false** |
| Heavy Land | A_SuperheroLanding_C | 1 | **false** |
| Hover Start | A_Flight_Hover_Start_B | 2 | **false** |
| Hover Blend | Blend Tree (see below) | 1 | **false** |
| Flight Start | A_Flight_FastMove_Start_A | 2 | **false** |
| Flight Blend | Blend Tree (see below) | 1 | **false** |

**Set "Grounded" as the default state** (orange color).

---

## Step 4: Blend Trees

### Grounded Blend Tree
- **Type:** Freeform Cartesian 2D
- **Parameters:** MoveX, MoveY

| Motion | Position X | Position Y |
|--------|------------|------------|
| TwinSword_Common_Idle | 0 | 0 |
| TwinSword_Common_Run_Loop | 0 | 1 |
| TwinSword_Common_Sprint | 0 | 1.5 |

### Hover Blend Tree
- **Type:** Freeform Directional 2D
- **Parameters:** HoverX, HoverY

| Motion | Position X | Position Y |
|--------|------------|------------|
| A_Flight_Hover_Loop | 0 | 0 |
| A_Flight_Hover_Forward | 0 | 1 |
| A_Flight_Hover_Backward | 0 | -1 |
| A_Flight_Hover_Left | -1 | 0 |
| A_Flight_Hover_Right | 1 | 0 |

### Flight Blend Tree
- **Type:** Freeform Directional 2D
- **Parameters:** FlightX, FlightY

| Motion | Position X | Position Y |
|--------|------------|------------|
| A_Flight_FastMove_Loop | 0 | 0 |
| A_Flight_FastMove_Forward | 0 | 1 |
| A_Flight_FastMove_Backward | 0 | -1 |
| A_Flight_FastMove_Left | -1 | 0 |
| A_Flight_FastMove_Right | 1 | 0 |

---

## Step 5: Transitions

### CRITICAL: No Any State Transitions
Do NOT use Any State transitions. All transitions must be explicit.

### CRITICAL: No Exit Transitions
Do NOT use Exit transitions. Always specify the exact destination state.

---

### From: Grounded

| To | Has Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|----------|---------------------|------------|
| Jump | No | 0.15 | Current State | LocomotionState == 1 |
| Fall | No | 0.2 | Current State | LocomotionState == 2 |
| Hover Start | No | 0.25 | Current State | LocomotionState == 3 |
| Flight Start | No | 0.2 | Current State | LocomotionState == 4 |

---

### From: Jump

| To | Has Exit Time | Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|-----------|----------|---------------------|------------|
| Fall | Yes | 0.85 | 0.15 | None | (none - auto transition) |
| Hover Start | No | - | 0.2 | Current State | LocomotionState == 3 |
| Flight Start | No | - | 0.2 | Current State | LocomotionState == 4 |

---

### From: Fall

| To | Has Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|----------|---------------------|------------|
| Light Land | No | 0.1 | Current State | LocomotionState == 0 AND LandingType == 1 |
| Heavy Land | No | 0.15 | Current State | LocomotionState == 0 AND LandingType == 2 |
| Hover Start | No | 0.25 | Current State | LocomotionState == 3 |
| Flight Start | No | 0.2 | Current State | LocomotionState == 4 |

---

### From: Light Land

| To | Has Exit Time | Exit Time | Duration | Conditions |
|----|---------------|-----------|----------|------------|
| Grounded | Yes | 0.7 | 0.2 | LocomotionState == 0 |

---

### From: Heavy Land

| To | Has Exit Time | Exit Time | Duration | Conditions |
|----|---------------|-----------|----------|------------|
| Grounded | Yes | 0.8 | 0.25 | LocomotionState == 0 |

---

### From: Hover Start

| To | Has Exit Time | Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|-----------|----------|---------------------|------------|
| Hover Blend | Yes | 0.9 | 0.2 | None | (none - auto) |
| Flight Start | No | - | 0.15 | Current State | LocomotionState == 4 |

---

### From: Hover Blend

| To | Has Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|----------|---------------------|------------|
| Fall | No | 0.2 | Current State | LocomotionState == 2 |
| Light Land | No | 0.15 | Current State | LocomotionState == 0 AND LandingType == 1 |
| Flight Start | No | 0.2 | Current State | LocomotionState == 4 |

---

### From: Flight Start

| To | Has Exit Time | Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|-----------|----------|---------------------|------------|
| Flight Blend | Yes | 0.9 | 0.25 | None | (none - auto) |
| Hover Blend | No | - | 0.2 | Current State | LocomotionState == 3 |

---

### From: Flight Blend

| To | Has Exit Time | Duration | Interruption Source | Conditions |
|----|---------------|----------|---------------------|------------|
| Fall | No | 0.2 | Current State | LocomotionState == 2 |
| Hover Blend | No | 0.25 | Current State | LocomotionState == 3 |
| Light Land | No | 0.2 | Current State | LocomotionState == 0 AND LandingType == 1 |
| Heavy Land | No | 0.2 | Current State | LocomotionState == 0 AND LandingType == 2 |

---

## Step 6: Aim Layer Setup

### States
| State Name | Motion | Write Defaults |
|------------|--------|----------------|
| Empty | None | **false** |
| Aim Blend | Blend Tree (see below) | **false** |

**Set "Empty" as the default state.**

### Aim Blend Tree
- **Type:** Freeform Directional 2D
- **Parameters:** AimX, AimY

| Motion | Position X | Position Y |
|--------|------------|------------|
| AS_Rifle_Aim | 0 | 0 |
| AS_Rifle_WalkFwd_Aim | 0 | 1 |
| AS_Rifle_WalkBwd_Aim | 0 | -1 |
| AS_Rifle_WalkLeft_Aim | -0.5 | 0.5 |
| AS_Rifle_WalkRight_Aim | 0.5 | 0.5 |
| AS_Rifle_WalkLeft_Aim | -0.5 | -0.5 |
| AS_Rifle_WalkRight_Aim | 0.5 | -0.5 |

### Aim Layer Transitions

| From | To | Duration | Conditions |
|------|-----|----------|------------|
| Empty | Aim Blend | 0.2 | IsAiming == true |
| Aim Blend | Empty | 0.25 | IsAiming == false |

---

## Step 7: Transition Settings Reference

### Standard Settings for All Transitions
- **Ordered Interruption:** true
- **Can Transition To Self:** false (IMPORTANT: prevents self-loops)
- **Solo:** false
- **Mute:** false

### Interruption Source Guidelines
| Transition Type | Interruption Source |
|-----------------|---------------------|
| Ground → Aerial | Current State |
| Aerial → Aerial | Current State |
| Aerial → Landing | Current State |
| Landing → Ground | None |
| Auto (exit time) | None |

---

## Step 8: Condition Mode Reference

| Condition | Mode | Threshold |
|-----------|------|-----------|
| LocomotionState == X | Equals | X |
| LandingType == X | Equals | X |
| IsAiming == true | If | 0 |
| IsAiming == false | IfNot | 0 |

---

## Step 9: Verification Checklist

Before testing, verify:

- [ ] All states have `Write Defaults` set to **false**
- [ ] No Any State transitions exist
- [ ] No Exit transitions exist (all transitions have explicit destinations)
- [ ] `LocomotionState` parameter exists as Int with default 0
- [ ] `LandingType` parameter exists as Int with default 0
- [ ] All transition conditions use correct modes (Equals for Int)
- [ ] `canTransitionToSelf` is **false** on all transitions
- [ ] Grounded state is set as the default (orange) state

---

## Step 10: Script Integration

Replace `PilotTypeController` with `PilotTypeControllerV2` on your character GameObject.

### Key Behavioral Differences

| Old Code | New Code |
|----------|----------|
| `animator.SetBool("IsFalling", true)` | `SetLocomotionState(LocomotionStateType.Falling)` |
| `animator.SetBool("IsFlying", true)` | `SetLocomotionState(LocomotionStateType.Flying)` |
| `animator.SetTrigger("HeavyLanding")` | `SetLandingType(LandingType.Heavy)` then `SetLocomotionState(LocomotionStateType.Grounded)` |
| Multiple bool resets in Enter() | Single `SetLocomotionState()` call |

---

## Troubleshooting

### Issue: Character stuck in idle while airborne
**Cause:** Exit transition fired before explicit transition could catch state change
**Fix:** Ensure all aerial states have explicit transitions to Fall with `LocomotionState == 2`

### Issue: Animation pops/flickers during rapid state changes
**Cause:** `Write Defaults` is true
**Fix:** Set `Write Defaults` to false on ALL states

### Issue: Landing animation doesn't play
**Cause:** LandingType set after LocomotionState change
**Fix:** Always call `SetLandingType()` BEFORE `SetLocomotionState(Grounded)`

### Issue: Can't interrupt transition to enter hover/flight
**Cause:** Interruption Source is None
**Fix:** Set Interruption Source to "Current State" on interruptible transitions

### Issue: State keeps re-entering itself
**Cause:** `canTransitionToSelf` is true
**Fix:** Uncheck "Can Transition To Self" on all transitions

---

## State Flow Diagram

```
                    ┌─────────────┐
                    │  Grounded   │◄────────────────┐
                    │ (Default)   │                 │
                    └──────┬──────┘                 │
                           │                        │
         ┌─────────────────┼─────────────────┐      │
         │                 │                 │      │
         ▼                 ▼                 ▼      │
    ┌─────────┐      ┌───────────┐    ┌───────────┐│
    │  Jump   │      │Hover Start│    │Flight Start││
    └────┬────┘      └─────┬─────┘    └─────┬─────┘│
         │                 │                 │      │
         ▼                 ▼                 ▼      │
    ┌─────────┐      ┌───────────┐    ┌───────────┐│
    │  Fall   │◄────►│Hover Blend│◄──►│Flight Blend││
    └────┬────┘      └─────┬─────┘    └─────┬─────┘│
         │                 │                 │      │
         │    ┌────────────┴────────────┐    │      │
         │    │                         │    │      │
         ▼    ▼                         ▼    ▼      │
    ┌──────────────┐              ┌──────────────┐  │
    │ Light Land   │              │ Heavy Land   │  │
    └───────┬──────┘              └───────┬──────┘  │
            │                             │         │
            └─────────────────────────────┴─────────┘
```

---

## File References

- **Controller Script:** `Assets/Script/Controls/PilotTypeControllerV2.cs`
- **This Guide:** `Assets/Script/Controls/animationSetup.md`
- **Original Export:** `Assets/Script/Editor/Pilot_AnimController_Export.json`
