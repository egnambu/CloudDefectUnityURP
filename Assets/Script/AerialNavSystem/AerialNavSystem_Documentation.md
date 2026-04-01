# AerialNavSystem - Technical Documentation

## Overview

`AerialNavSystem` is a runtime 3D A* pathfinding system for Unity that uses physics queries instead of pre-baked navigation meshes. It's designed for flying agents, drones, or any entity that needs to navigate in full 3D space.
---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     AerialNavSystem                         │
├─────────────────────────────────────────────────────────────┤
│  Public API                                                 │
│  ├── FindPath(start, end) → List<Vector3>                  │
│  ├── FindPathAsync(start, end) → IEnumerator               │
│  ├── IsPositionNavigable(position) → bool                  │
│  ├── IsWithinBounds(position) → bool                       │
│  └── HasLineOfSight(from, to) → bool                       │
├─────────────────────────────────────────────────────────────┤
│  Core A* Algorithm                                          │
│  ├── FindPathInternal() - Main pathfinding loop            │
│  ├── GetNeighbors() - 6 cardinal + 20 diagonal directions  │
│  ├── Heuristic() - Octile distance for 3D                  │
│  └── GetMovementCost() - Cardinal/diagonal costs           │
├─────────────────────────────────────────────────────────────┤
│  Spatial Queries (Unity Physics)                            │
│  ├── Physics.CheckSphere() - Obstacle detection            │
│  └── Physics.SphereCast() - Line-of-sight checks           │
├─────────────────────────────────────────────────────────────┤
│  Support Systems                                            │
│  ├── PriorityQueue<T> - Min-heap for open set              │
│  ├── SmoothPath() - Removes redundant waypoints            │
│  └── Bounds checking - Optional BoxCollider constraint     │
└─────────────────────────────────────────────────────────────┘
```

---

## How It Works

### 1. Grid Discretization

The system converts world positions to integer grid coordinates:

```
World Position (5.7, 3.2, 8.9) with nodeSize = 1.0
         ↓
Grid Node (6, 3, 9)
```

**Key Methods:**
- `WorldToNode(Vector3)` → `Vector3Int`
- `NodeToWorld(Vector3Int)` → `Vector3`

### 2. Neighbor Exploration

Each node can have up to **26 neighbors** in 3D space:

| Type | Count | Directions | Cost Multiplier |
|------|-------|------------|-----------------|
| Cardinal | 6 | ±X, ±Y, ±Z | 1.0 |
| 2D Diagonal | 12 | XY, XZ, YZ planes | √2 ≈ 1.414 |
| 3D Diagonal | 8 | All three axes | √3 ≈ 1.732 |

### 3. Obstacle Detection

Uses Unity's `Physics.CheckSphere()` at each potential node position:

```csharp
bool IsNodeNavigable(Vector3Int node)
{
    Vector3 worldPos = NodeToWorld(node);
    
    // Check bounds first (cheap)
    if (!IsWithinBounds(worldPos))
        return false;
    
    // Then check physics (more expensive)
    return !Physics.CheckSphere(worldPos, agentRadius, obstacleLayer);
}
```

### 4. Diagonal Safety

Diagonal moves are validated to prevent corner-cutting:

```
Moving from A to B diagonally requires C and D to be clear:

    C . B
    . X .      X = obstacle
    A . D      A→B blocked because C has obstacle
```

### 5. Heuristic Function

Uses **Octile Distance** for 3D, which accounts for diagonal movement costs:

```csharp
// Simplified concept:
cost = (cardinalMoves * 1.0) + (2D_diagonals * 1.414) + (3D_diagonals * 1.732)
```

### 6. Path Smoothing

After finding a path, unnecessary waypoints are removed using line-of-sight checks:

```
Original:  A → B → C → D → E → F
                    ↓
Smoothed:  A ────────→ D → F    (if A can see D directly)
```

---

## Bounds System

### How Bounds Work

When a `BoxCollider` is assigned to `navBounds`:

1. The collider's world-space `Bounds` are cached on `Awake()`
2. Every position check first verifies it's inside the bounds
3. Pathfinding is restricted to the bounded volume

### Setting Up Bounds

1. Create an empty GameObject
2. Add a `BoxCollider` component
3. **Enable "Is Trigger"** (so it doesn't physically block anything)
4. Scale/position it to cover your navigation area
5. Assign it to the `navBounds` field

### Runtime Bounds Updates

If you move or resize the bounds collider at runtime:

```csharp
navSystem.navBounds.transform.position = newPosition;
navSystem.UpdateBoundsCache(); // Required after changes!
```

---

## Data Structures

### PriorityQueue<T>

A min-heap implementation optimized for A*:

| Operation | Time Complexity |
|-----------|-----------------|
| Enqueue | O(log n) |
| Dequeue | O(log n) |
| Contains | O(1) |

**Key feature:** Uses `Dictionary<T, int>` for O(1) containment checks, critical for A* performance.

### Memory Usage

| Structure | Purpose | Allocation |
|-----------|---------|------------|
| `openSet` | Nodes to explore | Grows during search |
| `closedSet` | Explored nodes | Grows during search |
| `cameFrom` | Path reconstruction | Grows during search |
| `gScore/fScore` | Cost tracking | Grows during search |
| `lastPath` | Debug visualization | Persists between calls |

---

## Performance Characteristics

### Time Complexity

- **Best case:** O(1) - Start equals end
- **Average case:** O(n log n) - Where n = nodes explored
- **Worst case:** O(maxIterations) - No path exists

### Optimization Strategies

1. **Bounds checking before physics** - Cheap rejection
2. **Cached bounds** - Avoids repeated `collider.bounds` calls
3. **Octile heuristic** - Better estimates than Euclidean
4. **Path smoothing** - Reduces waypoint count for followers

### Performance Tips

```csharp
// 1. Use appropriate node size
navSystem.nodeSize = 1.0f;  // Larger = faster but less precise

// 2. Limit obstacle layers
navSystem.obstacleLayer = LayerMask.GetMask("Obstacles");

// 3. Use async for long paths
StartCoroutine(FindPathCoroutine(destination));

// 4. Cache paths, don't recalculate every frame
if (Time.time - lastPathTime > 0.5f) {
    currentPath = navSystem.FindPath(start, end);
    lastPathTime = Time.time;
}
```

---

## Inspector Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `nodeSize` | float | 1.0 | Grid cell size in world units |
| `agentRadius` | float | 0.5 | Sphere radius for collision checks |
| `obstacleLayer` | LayerMask | Everything | Layers treated as obstacles |
| `maxIterations` | int | 10000 | Safety limit for pathfinding |
| `allowDiagonal` | bool | true | Enable 26-connectivity vs 6 |
| `smoothPath` | bool | true | Post-process path optimization |
| `navBounds` | BoxCollider | null | Optional navigation boundary |
| `enforceBounds` | bool | true | Whether bounds are enforced |
| `showDebugPath` | bool | false | Draw path gizmos |
| `showDebugBounds` | bool | false | Draw bounds gizmos |

---

## API Reference

### FindPath

```csharp
public List<Vector3> FindPath(Vector3 start, Vector3 end)
```

**Returns:** List of waypoints from start to end, or `null` if no path exists.

**Usage:**
```csharp
var path = navSystem.FindPath(transform.position, target.position);
if (path != null) {
    // Follow the path
}
```

### FindPathAsync

```csharp
public IEnumerator<List<Vector3>> FindPathAsync(
    Vector3 start, 
    Vector3 end, 
    int nodesPerFrame = 100)
```

**Returns:** An enumerator that yields `null` during processing, then yields the path (or `null` if no path).

**Usage:**
```csharp
IEnumerator FindPathCoroutine(Vector3 dest) {
    var enumerator = navSystem.FindPathAsync(transform.position, dest, 50);
    while (enumerator.MoveNext()) {
        if (enumerator.Current != null) {
            currentPath = enumerator.Current;
            yield break;
        }
        yield return null;
    }
}
```

### IsPositionNavigable

```csharp
public bool IsPositionNavigable(Vector3 position)
```

**Returns:** `true` if position is within bounds and not blocked by obstacles.

### IsWithinBounds

```csharp
public bool IsWithinBounds(Vector3 position)
```

**Returns:** `true` if position is inside the navBounds (or if no bounds set).

### HasLineOfSight

```csharp
public bool HasLineOfSight(Vector3 from, Vector3 to)
```

**Returns:** `true` if a sphere of `agentRadius` can travel from `from` to `to` without hitting obstacles.

### UpdateBoundsCache

```csharp
public void UpdateBoundsCache()
```

Call this after modifying `navBounds` transform at runtime.

### GetBounds

```csharp
public Bounds? GetBounds()
```

**Returns:** The current navigation bounds, or `null` if unbounded.

---

## Common Issues & Solutions

### "No path found" when path should exist

1. **Check obstacle layer** - Ensure it doesn't include everything
2. **Agent radius too large** - Try reducing it
3. **Node size too large** - Reduce for tight spaces
4. **Start/end blocked** - Verify with `IsPositionNavigable()`
5. **Outside bounds** - Check with `IsWithinBounds()`

### Path goes through obstacles

1. **Missing colliders** on obstacles
2. **Wrong layer** assignment
3. **Agent radius too small**

### Performance issues

1. **Reduce maxIterations** for early failure
2. **Increase nodeSize** for faster searches
3. **Use FindPathAsync** to spread across frames
4. **Set enforceBounds = true** with smaller bounds

---

## Version History

| Version | Changes |
|---------|---------|
| 1.0 | Initial implementation |
| 1.1 | Added bounds support via BoxCollider |
| 1.1 | Added IsWithinBounds(), GetBounds(), UpdateBoundsCache() |
| 1.1 | Added showDebugBounds visualization |