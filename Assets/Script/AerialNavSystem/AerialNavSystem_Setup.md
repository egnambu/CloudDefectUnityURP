# 3D Aerial Navigation System - Setup Guide

A performant A* pathfinding system for 3D navigation in Unity that leverages Unity's built-in physics spatial queries instead of maintaining separate spatial data structures.

## Features

- **Physics-based obstacle detection** - Uses `Physics.CheckSphere` and `Physics.SphereCast` for collision detection
- **No pre-baked navigation mesh** - Works dynamically with any collider configuration
- **Full 3D movement** - Supports cardinal (6 directions) and diagonal (26 directions) movement
- **Path smoothing** - Uses line-of-sight checks to remove unnecessary waypoints
- **Async pathfinding** - Can spread computation across frames to prevent hitches
- **Debug visualization** - Built-in Gizmo drawing for path debugging

---

## Quick Setup

### 1. Add the Component

1. Create an empty GameObject in your scene (or use an existing manager object)
2. Add the `AerialNavSystem` component to it
3. Configure the settings in the Inspector

### 2. Configure Settings

| Setting | Description | Recommended Value |
|---------|-------------|-------------------|
| **Node Size** | Distance between navigation nodes | 1.0 for large spaces, 0.5 for tight areas |
| **Agent Radius** | Collision radius for obstacle checks | Half the width of your flying agent |
| **Obstacle Layer** | LayerMask for obstacles | Create an "Obstacles" layer |
| **Max Iterations** | Safety limit for pathfinding | 10000 (increase for very large areas) |
| **Allow Diagonal** | Enable 3D diagonal movement | True for smoother paths |
| **Smooth Path** | Remove redundant waypoints | True (recommended) |

### 3. Set Up Obstacles

1. Create a new Layer: **Edit > Project Settings > Tags and Layers**
   - Add a layer called "Obstacles" (or similar)
2. Assign all obstacle objects to this layer
3. Ensure obstacles have Colliders attached (any collider type works)
4. Set the `obstacleLayer` field in `AerialNavSystem` to your obstacle layer

---

## Usage Examples

### Basic Pathfinding

```csharp
public class FlyingAgent : MonoBehaviour
{
    public AerialNavSystem navSystem;
    public float moveSpeed = 5f;
    
    private List<Vector3> currentPath;
    private int pathIndex = 0;
    
    public void NavigateTo(Vector3 destination)
    {
        currentPath = navSystem.FindPath(transform.position, destination);
        pathIndex = 0;
        
        if (currentPath == null)
        {
            Debug.Log("No path found!");
        }
    }
    
    void Update()
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
            return;
        
        Vector3 target = currentPath[pathIndex];
        transform.position = Vector3.MoveTowards(
            transform.position, 
            target, 
            moveSpeed * Time.deltaTime
        );
        
        if (Vector3.Distance(transform.position, target) < 0.1f)
        {
            pathIndex++;
        }
    }
}
```

### Async Pathfinding (For Large Areas)

Use this approach to prevent frame hitches when pathfinding over large distances:

```csharp
public class AsyncFlyingAgent : MonoBehaviour
{
    public AerialNavSystem navSystem;
    
    private Coroutine pathfindingCoroutine;
    private List<Vector3> currentPath;
    
    public void NavigateToAsync(Vector3 destination)
    {
        if (pathfindingCoroutine != null)
            StopCoroutine(pathfindingCoroutine);
        
        pathfindingCoroutine = StartCoroutine(FindPathCoroutine(destination));
    }
    
    private IEnumerator FindPathCoroutine(Vector3 destination)
    {
        var pathEnumerator = navSystem.FindPathAsync(
            transform.position, 
            destination, 
            nodesPerFrame: 100  // Process 100 nodes per frame
        );
        
        while (pathEnumerator.MoveNext())
        {
            if (pathEnumerator.Current != null)
            {
                // Path found!
                currentPath = pathEnumerator.Current;
                Debug.Log($"Path found with {currentPath.Count} waypoints");
                yield break;
            }
            yield return null; // Wait for next frame
        }
        
        Debug.Log("No path found");
    }
}
```

### Checking Positions

```csharp
// Check if a position is safe to fly to
if (navSystem.IsPositionNavigable(targetPosition))
{
    // Position is clear of obstacles
}

// Check line of sight between two points
if (navSystem.HasLineOfSight(transform.position, targetPosition))
{
    // Direct path is clear, no need for pathfinding
    transform.position = Vector3.MoveTowards(
        transform.position, 
        targetPosition, 
        speed * Time.deltaTime
    );
}
else
{
    // Need to pathfind around obstacles
    var path = navSystem.FindPath(transform.position, targetPosition);
}
```

---

## Performance Tips

### 1. Use Appropriate Node Size

- **Larger node size** = faster pathfinding, less precise paths
- **Smaller node size** = slower pathfinding, more precise paths
- Start with `nodeSize = 1` and adjust based on your needs

### 2. Layer Mask Optimization

Only include necessary layers in `obstacleLayer`:

```csharp
// In Inspector: Set to only your obstacle layer
// Or via code:
navSystem.obstacleLayer = LayerMask.GetMask("Obstacles", "Walls");
```

### 3. Use Line-of-Sight First

Before pathfinding, check if a direct path exists:

```csharp
public List<Vector3> GetOptimalPath(Vector3 start, Vector3 end)
{
    // Quick check - can we go directly?
    if (navSystem.HasLineOfSight(start, end))
    {
        return new List<Vector3> { start, end };
    }
    
    // Need full pathfinding
    return navSystem.FindPath(start, end);
}
```

### 4. Cache Paths

Don't recalculate every frame:

```csharp
private float repathInterval = 0.5f;
private float lastRepathTime;

void Update()
{
    if (Time.time - lastRepathTime > repathInterval)
    {
        currentPath = navSystem.FindPath(transform.position, target.position);
        lastRepathTime = Time.time;
    }
}
```

### 5. Use Async for Long Paths

For paths that might take many iterations, use `FindPathAsync` to prevent frame drops.

---

## How It Works

### Unity Physics Integration

The system uses Unity's physics queries instead of maintaining its own spatial data:

| Task | Unity API Used |
|------|----------------|
| Obstacle detection | `Physics.CheckSphere()` |
| Line-of-sight | `Physics.SphereCast()` |
| Path smoothing | `Physics.SphereCast()` |

This approach:
- ✅ Automatically works with dynamic obstacles
- ✅ No memory overhead for spatial grids
- ✅ Uses Unity's optimized BVH/broadphase
- ✅ Consistent with your game's physics setup

### A* Algorithm

1. **Node Generation**: Nodes are generated lazily based on `nodeSize` grid
2. **Neighbor Exploration**: 6 cardinal + 20 diagonal directions (26-connectivity)
3. **Heuristic**: Octile distance (accurate for 3D grid movement)
4. **Path Smoothing**: Greedy line-of-sight optimization

---

## Troubleshooting

### "No path found" when there should be one

1. **Check obstacle layer** - Ensure `obstacleLayer` doesn't include everything
2. **Agent radius too large** - Reduce `agentRadius` if passages are tight
3. **Node size too large** - Try smaller `nodeSize` for narrow gaps
4. **Max iterations too low** - Increase `maxIterations` for distant targets

### Path goes through obstacles

1. **Missing colliders** - Ensure all obstacles have colliders
2. **Wrong layer** - Check obstacles are on the correct layer
3. **Agent radius too small** - Increase `agentRadius`

### Performance issues

1. **Node size too small** - Increase `nodeSize`
2. **Use async** - Switch to `FindPathAsync` for long paths
3. **Reduce diagonal** - Set `allowDiagonal = false` (explores fewer neighbors)
4. **Cache paths** - Don't recalculate every frame

### Debug Visualization

The system includes comprehensive debug visualization that works in both Editor and Runtime:

#### Enable Debug Options

| Option | Description |
|--------|-------------|
| **Show Debug Path** | Displays the last calculated path with waypoints |
| **Show Debug Bounds** | Shows the navigation boundary volume |
| **Show Debug Nodes** | Visualizes all navigation grid nodes (navigable vs blocked) |
| **Max Debug Nodes Per Axis** | Limits node visualization to prevent editor freeze |

#### Debug Colors

| Color | Meaning |
|-------|---------|
| **Green (path)** | Path line connections |
| **Yellow spheres** | Path waypoints |
| **Blue sphere** | Path start point |
| **Red sphere** | Path end point |
| **Cyan box** | Navigation bounds |
| **Green wireframe cubes** | Navigable nodes |
| **Red solid cubes** | Blocked nodes |

#### Test Path in Editor

Right-click the component → **"Test Path (from origin to bounds center)"** to generate a test path for visualization without running the game.

---

## API Reference

### Public Methods

```csharp
// Synchronous pathfinding
List<Vector3> FindPath(Vector3 start, Vector3 end)

// Async pathfinding (spread across frames)
IEnumerator<List<Vector3>> FindPathAsync(Vector3 start, Vector3 end, int nodesPerFrame = 100)

// Check if position is clear
bool IsPositionNavigable(Vector3 position)

// Check direct line of sight
bool HasLineOfSight(Vector3 from, Vector3 to)
```

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `nodeSize` | float | Grid cell size |
| `agentRadius` | float | Collision check radius |
| `obstacleLayer` | LayerMask | Layers to consider as obstacles |
| `maxIterations` | int | Max nodes to explore |
| `allowDiagonal` | bool | Enable 3D diagonal movement |
| `smoothPath` | bool | Enable path optimization |
| `showDebugPath` | bool | Enable Gizmo visualization |

---

## Advanced: Custom Integration

### With Behavior Trees / State Machines

```csharp
public class PatrolState : IState
{
    private AerialNavSystem nav;
    private List<Vector3> patrolPoints;
    private int currentPoint = 0;
    
    public void OnEnter()
    {
        NavigateToNextPoint();
    }
    
    private void NavigateToNextPoint()
    {
        var path = nav.FindPath(transform.position, patrolPoints[currentPoint]);
        if (path != null)
        {
            StartFollowingPath(path);
        }
        currentPoint = (currentPoint + 1) % patrolPoints.Count;
    }
}
```

### With Object Pooling

```csharp
// The system doesn't allocate during pathfinding except for the result list
// You can pool the result lists if needed:

public class PathPool
{
    private Queue<List<Vector3>> pool = new Queue<List<Vector3>>();
    
    public List<Vector3> Get()
    {
        return pool.Count > 0 ? pool.Dequeue() : new List<Vector3>();
    }
    
    public void Return(List<Vector3> path)
    {
        path.Clear();
        pool.Enqueue(path);
    }
}
```
