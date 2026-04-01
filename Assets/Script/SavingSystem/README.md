# World Saving System Documentation

A data-driven 3D chunk-based delta world saving system for Unity. Designed for large open-world games (GTA-style) with efficient streaming and minimal save file sizes.

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Setup Guide](#setup-guide)
- [Creating Persistent Entities](#creating-persistent-entities)
- [Save/Load API](#saveload-api)
- [Configuration](#configuration)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

### Key Features

- **Pure Math Chunk Detection** - No Unity physics, colliders, or overlap checks
- **Delta Saving** - Only saves changes from base world (destroyed objects, moved items, NPC states)
- **Chunk Streaming** - Automatically loads/unloads chunks as player moves
- **Event-Driven** - No per-frame scans or global iterations
- **Scalable** - O(1) entity lookups, suitable for thousands of entities

### Architecture

```
WorldSavingSystem (MonoBehaviour - attach to player/camera)
    ├── WorldIndexManager (spatial index: ChunkID → EntityIDs)
    ├── SaveLoadPipeline (serialization layer)
    │   └── SaveSerializer (file I/O)
    └── EntityFactory (instantiation from save data)

PersistentEntityBase (MonoBehaviour - attach to persistent objects)
    ├── PersistentNPC
    ├── PersistentItem
    ├── PersistentDestructible
    └── PersistentInteractive
```

---

## Quick Start

### 1. Add WorldSavingSystem to Player

```csharp
// Attach WorldSavingSystem component to your player or main camera
// This object represents the "center" of the active world
```

1. Select your **Player** or **Main Camera** GameObject
2. Add Component → `WorldSavingSystem`
3. Configure chunk size and extents in Inspector

### 2. Make Objects Persistent

```csharp
// Add a persistent component to any object that needs saving
public class MyNPC : PersistentNPC
{
    // Your NPC logic here
}
```

### 3. Save/Load

```csharp
// Save the game
WorldSavingSystem.Instance.SaveGame("MySave");

// Load the game
WorldSavingSystem.Instance.LoadGame("MySave");

// Delta save (only dirty data - more efficient)
WorldSavingSystem.Instance.SaveDelta("MySave");
```

---

## Core Concepts

### Chunks

The world is divided into a 3D grid of chunks using pure math:

```csharp
ChunkID = floor(worldPosition / chunkSize)
```

- Chunks are **logical containers**, not GameObjects
- Each chunk has a unique `ChunkID(x, y, z)`
- Entities belong to exactly one chunk at a time

### Entity IDs

Every persistent entity has a stable `EntityID`:

```csharp
// Auto-generated in editor (stored as GUID string)
// Or assigned programmatically:
EntityID id = EntityID.GenerateUnique();
```

- IDs persist across saves/loads
- Used for entity lookups and relationships

### Delta Saving

The system only saves **changes** from the base world:

| What Gets Saved | Example |
|-----------------|---------|
| Destroyed base objects | Player destroyed a barrel |
| Modified state | Door opened, NPC took damage |
| Spawned entities | Dropped items, spawned NPCs |
| Moved entities | Item picked up and dropped elsewhere |

Objects that **never change** are NOT saved, keeping save files small.

### Dirty Tracking

Entities mark themselves as "dirty" when state changes:

```csharp
public float Health
{
    get => _health;
    set
    {
        _health = value;
        MarkDirty();  // Flags entity for saving
    }
}
```

Only dirty entities are written during delta saves.

---

## Setup Guide

### Step 1: Configure WorldSavingSystem

1. Create an empty GameObject or use your Player
2. Add the `WorldSavingSystem` component
3. Configure in Inspector:

| Property | Description | Recommended |
|----------|-------------|-------------|
| **Chunk Size** | Size of each chunk in world units | `(50, 50, 50)` for outdoor, `(20, 20, 20)` for indoor |
| **Active Chunk Half Extents** | Chunks to keep active around center | `(2, 1, 2)` = 5x3x5 = 75 chunks |
| **Load Buffer Half Extents** | Chunks to preload ahead | `(3, 1, 3)` = 7x3x7 = 147 chunks |
| **Save Folder** | Folder name in persistentDataPath | `"WorldSaves"` |
| **Auto Save Interval** | Seconds between auto-saves (0=disabled) | `300` (5 minutes) |
| **Use Delta Auto Save** | Use efficient delta saves for auto-save | `true` |

### Step 2: Create Entity Prefab Registry (Optional)

For entities that spawn from saves:

1. Create → Saving System → Entity Prefab Registry
2. Add prefab mappings (PrefabID → Prefab)
3. Reference in your game initialization

```csharp
// In your game manager
[SerializeField] private EntityPrefabRegistry _prefabRegistry;

void Start()
{
    // Get the factory from WorldSavingSystem and register prefabs
    // (Factory registration happens automatically if using Resources folder)
}
```

### Step 3: Organize Prefabs (Resources Method)

Place entity prefabs in: `Resources/Prefabs/Entities/`

```
Resources/
  Prefabs/
    Entities/
      NPC_Guard.prefab      → PrefabID: "NPC_Guard"
      Item_HealthPack.prefab → PrefabID: "Item_HealthPack"
      Door_Metal.prefab      → PrefabID: "Door_Metal"
```

The `PrefabID` on each entity should match the filename.

---

## Creating Persistent Entities

### Option 1: Use Built-in Entity Types

#### PersistentNPC

```csharp
using SavingSystem.Entities;

public class GuardNPC : PersistentNPC
{
    protected override void ApplyAppearance()
    {
        // Apply HeadIndex, BodyIndex, etc. to your character customizer
        _meshRenderer.material.color = SkinColor;
    }

    protected override void RestoreAIState()
    {
        // Resume AI from AIStateId and CurrentTaskId
        _aiController.SetState(AIStateId);
    }
}
```

#### PersistentItem

```csharp
using SavingSystem.Entities;

public class HealthPack : PersistentItem
{
    public void Use()
    {
        // Consume item
        ModifyStack(-1);  // Automatically marks dirty
    }
}
```

#### PersistentDestructible

```csharp
using SavingSystem.Entities;

public class ExplosiveBarrel : PersistentDestructible
{
    protected override void OnDestroyed()
    {
        base.OnDestroyed();
        // Spawn explosion effect
        Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
    }
}
```

#### PersistentInteractive

```csharp
using SavingSystem.Entities;

public class SecurityDoor : PersistentInteractive
{
    protected override void OnStateChanged()
    {
        base.OnStateChanged();
        // Play door sound
        _audioSource.PlayOneShot(IsOpen ? _openSound : _closeSound);
    }
}
```

### Option 2: Create Custom Entity Type

```csharp
using SavingSystem.Core;
using UnityEngine;

public class PersistentVehicle : PersistentEntityBase
{
    [SerializeField] private float _fuel = 100f;
    [SerializeField] private float _health = 100f;

    // Property with dirty tracking
    public float Fuel
    {
        get => _fuel;
        set { _fuel = value; MarkDirty(); }
    }

    // REQUIRED: Capture state for saving
    public override EntityStateData CaptureState()
    {
        VehicleStateData state = new VehicleStateData();
        PopulateBaseState(state);  // Fill common fields
        
        state.Fuel = _fuel;
        state.Health = _health;
        // ... other fields
        
        return state;
    }

    // REQUIRED: Restore state on load
    public override void RestoreState(EntityStateData state)
    {
        if (state is not VehicleStateData vehicleState)
            return;

        RestoreBaseState(state);  // Restore position, rotation
        
        _fuel = vehicleState.Fuel;
        _health = vehicleState.Health;
        // ... other fields
    }

    void Update()
    {
        // IMPORTANT: Call this when entity moves
        UpdatePositionTracking();
    }
}
```

### Entity Setup Checklist

- [ ] Inherits from `PersistentEntityBase` or a subclass
- [ ] Has unique Entity GUID (auto-generated, or use context menu)
- [ ] Has PrefabID set (matches prefab name for loading)
- [ ] Implements `CaptureState()` and `RestoreState()`
- [ ] Calls `MarkDirty()` when state changes
- [ ] Calls `UpdatePositionTracking()` when moving

---

## Save/Load API

### Basic Operations

```csharp
// Get the singleton instance
WorldSavingSystem saveSystem = WorldSavingSystem.Instance;

// Full save (all entities)
saveSystem.SaveGame("SaveSlot1");

// Delta save (only changes - faster, smaller files)
saveSystem.SaveDelta("SaveSlot1");

// Load a save
saveSystem.LoadGame("SaveSlot1");

// Start new game
saveSystem.NewGame("SaveSlot1");

// Check if save exists
bool exists = saveSystem.SaveExists("SaveSlot1");

// Delete a save
saveSystem.DeleteSave("SaveSlot1");

// Get available saves
string[] saves = saveSystem.GetAvailableSaves();
```

### Events

```csharp
void OnEnable()
{
    WorldSavingSystem.Instance.OnSaveStarted += HandleSaveStarted;
    WorldSavingSystem.Instance.OnSaveCompleted += HandleSaveCompleted;
    WorldSavingSystem.Instance.OnLoadStarted += HandleLoadStarted;
    WorldSavingSystem.Instance.OnLoadCompleted += HandleLoadCompleted;
    WorldSavingSystem.Instance.OnChunkLoaded += HandleChunkLoaded;
    WorldSavingSystem.Instance.OnChunkUnloaded += HandleChunkUnloaded;
}

void HandleSaveStarted()
{
    // Show saving indicator
}

void HandleLoadCompleted()
{
    // Refresh UI, resume gameplay
}

void HandleChunkLoaded(ChunkID chunk)
{
    // Chunk entered active range
}
```

### Checking State

```csharp
// Check for unsaved changes
if (saveSystem.HasUnsavedChanges)
{
    // Prompt "Save before quitting?"
}

// Get statistics
string stats = saveSystem.GetDebugStats();
Debug.Log(stats);
// Output: "Entities: 150 | Active Chunks: 12 | Dirty: 3 entities, 2 chunks"

// Get current chunk
ChunkID currentChunk = saveSystem.CurrentCenterChunk;

// Get play time
float playTime = saveSystem.PlayTime;
```

---

## Configuration

### Runtime Configuration

```csharp
// Change chunk size (expensive - re-indexes all entities)
saveSystem.SetChunkSize(new Vector3(100, 50, 100));

// Adjust active range
saveSystem.SetActiveChunkExtents(new Vector3Int(3, 2, 3));
saveSystem.SetLoadBufferExtents(new Vector3Int(4, 2, 4));

// Change save slot
saveSystem.CurrentSaveName = "AutoSave";
```

### Chunk Size Guidelines

| World Type | Recommended Chunk Size | Notes |
|------------|------------------------|-------|
| Open World (GTA) | `(100, 50, 100)` | Large chunks, fewer total |
| City Streets | `(50, 30, 50)` | Medium density |
| Indoor/Dungeon | `(20, 10, 20)` | Small rooms |
| Vertical (Tower) | `(30, 20, 30)` | Taller Y for floors |

### Performance Tuning

| Setting | Effect | Trade-off |
|---------|--------|-----------|
| Larger chunks | Fewer chunk transitions | More entities per chunk to process |
| Smaller chunks | Finer-grained streaming | More chunk files, more transitions |
| Larger load buffer | Smoother streaming | More memory usage |
| Higher auto-save interval | Less I/O | More potential data loss |

---

## Best Practices

### 1. Mark Dirty Appropriately

```csharp
// GOOD: Mark dirty on significant changes
public void TakeDamage(float damage)
{
    _health -= damage;
    MarkDirty();
}

// BAD: Marking dirty every frame
void Update()
{
    _timer += Time.deltaTime;
    MarkDirty();  // Don't do this!
}

// GOOD: Batch updates, mark dirty once
public void CompleteQuest()
{
    _questsCompleted++;
    _experience += 100;
    _reputation += 10;
    MarkDirty();  // Once at the end
}
```

### 2. Use Delta Saves for Auto-Save

```csharp
// Auto-save should use delta to minimize I/O
IEnumerator AutoSaveRoutine()
{
    while (true)
    {
        yield return new WaitForSeconds(300f);
        WorldSavingSystem.Instance.SaveDelta("AutoSave");
    }
}
```

### 3. Generate Entity IDs in Editor

- Use the context menu "Generate New Entity ID" on prefabs
- Don't generate IDs at runtime for base world objects
- Runtime-spawned entities can use `EntityID.GenerateUnique()`

### 4. Handle Base World Objects

For objects placed in scenes (not spawned):

```csharp
public class BaseWorldObject : PersistentDestructible
{
    void Start()
    {
        // Check if this object was destroyed in save data
        if (WorldSavingSystem.Instance.IsEntityDestroyed(EntityID))
        {
            Destroy(gameObject);
            return;
        }
        
        base.Start();
    }
}
```

### 5. Organize Save Data

```
PersistentDataPath/
  WorldSaves/
    Save1/
      world.json          ← World metadata
      chunks/
        chunk_0_0_0.json  ← Per-chunk entity data
        chunk_1_0_0.json
        chunk_-1_0_2.json
    AutoSave/
      world.json
      chunks/
        ...
```

---

## Troubleshooting

### Entity Not Saving

1. Check entity has `PersistentEntityBase` component
2. Verify `EntityID` is valid (not all zeros)
3. Confirm `MarkDirty()` is being called
4. Check entity is registered (appears in debug stats)

### Entity Not Loading

1. Verify `PrefabID` matches prefab name in Resources
2. Check prefab is in `Resources/Prefabs/Entities/`
3. Confirm `RestoreState()` is implemented correctly
4. Look for errors in Console on load

### Chunks Not Streaming

1. Confirm `WorldSavingSystem` is on the moving object (player)
2. Check `ChunkSize` isn't too large
3. Verify `LoadBufferHalfExtents` is reasonable
4. Enable `LogChunkChanges` for debugging

### Save File Too Large

1. Use delta saves instead of full saves
2. Check for entities marking dirty too often
3. Reduce data in `EntityStateData` subclasses
4. Consider larger chunks (fewer files)

### Debug Tools

1. **Debug UI**: Add `WorldSavingSystemDebugUI` component, press F3
2. **Gizmos**: Enable `ShowDebugGizmos` on `WorldSavingSystem`
3. **Logging**: Enable `LogChunkChanges` for chunk transitions

```csharp
// Get detailed stats
Debug.Log(WorldSavingSystem.Instance.GetDebugStats());

// Check specific entity
var entity = WorldSavingSystem.Instance.WorldIndex.GetEntity(entityId);
Debug.Log($"Entity {entityId} in chunk {entity?.CurrentChunk}");
```

---

## File Structure

```
Assets/Script/SavingSystem/
├── Core/
│   ├── ChunkMath.cs           ← ChunkID, EntityID, math utilities
│   ├── EntityDataModels.cs    ← IPersistentEntity, state data classes
│   ├── EntityFactory.cs       ← Entity instantiation from saves
│   ├── PersistentEntityBase.cs← Base MonoBehaviour for entities
│   └── WorldIndexManager.cs   ← Spatial index (ChunkID → Entities)
├── Serialization/
│   └── SaveLoadPipeline.cs    ← Save/load logic, JSON serialization
├── Entities/
│   ├── PersistentNPC.cs       ← Example NPC implementation
│   ├── PersistentItem.cs      ← Example item implementation
│   ├── PersistentDestructible.cs
│   └── PersistentInteractive.cs
├── Debug/
│   └── WorldSavingSystemDebugUI.cs ← Runtime debug UI
└── WorldSavingSystem.cs       ← Main controller MonoBehaviour
```

---

## API Reference

### WorldSavingSystem

| Method | Description |
|--------|-------------|
| `SaveGame(string)` | Full save to slot |
| `SaveDelta(string)` | Delta save (dirty only) |
| `LoadGame(string)` | Load from slot |
| `NewGame(string)` | Start fresh |
| `SaveExists(string)` | Check if slot exists |
| `DeleteSave(string)` | Delete save slot |
| `GetAvailableSaves()` | List all saves |

### PersistentEntityBase

| Method | Description |
|--------|-------------|
| `MarkDirty()` | Flag for saving |
| `ClearDirty()` | Clear dirty flag |
| `UpdatePositionTracking()` | Update chunk assignment |
| `ForceChunkUpdate()` | Immediate chunk recalculation |
| `CaptureState()` | (Abstract) Serialize to data |
| `RestoreState(data)` | (Abstract) Deserialize from data |

### ChunkID

| Method | Description |
|--------|-------------|
| `FromWorldPosition(pos, size)` | Calculate chunk from position |
| `GetWorldCenter(size)` | Get chunk center point |
| `IsWithinRange(center, extents)` | Check if in range |
| `ManhattanDistance(other)` | Distance in chunks |
