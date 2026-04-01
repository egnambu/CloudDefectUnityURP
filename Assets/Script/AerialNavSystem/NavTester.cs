using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class NavTester : MonoBehaviour
{
    [Header("References")]
    public AerialNavSystem navSystem;
    public Transform target;
    
    [Header("Legacy Test Controls")]
    [Tooltip("Find path from this object to target (Legacy A*)")]
    public bool testFindPath = false;
    
    [Tooltip("Test async pathfinding (spread across frames)")]
    public bool testFindPathAsync = false;
    
    [Tooltip("Check if current position is navigable")]
    public bool testIsPositionNavigable = false;
    
    [Tooltip("Check if target position is navigable")]
    public bool testIsTargetNavigable = false;
    
    [Tooltip("Check line of sight to target")]
    public bool testLineOfSight = false;
    
    [Tooltip("Check if current position is within bounds")]
    public bool testIsWithinBounds = false;
    
    [Tooltip("Continuously update path every frame (performance test)")]
    public bool continuousPathfinding = false;
    
    [Header("Optimized Pathfinding Tests")]
    [Tooltip("Test the optimized FindPathOptimized supermethod")]
    public bool testFindPathOptimized = false;
    
    [Tooltip("Test weighted A* pathfinding")]
    public bool testFindPathWeighted = false;
    
    [Tooltip("Test optimized async pathfinding")]
    public bool testFindPathOptimizedAsync = false;
    
    [Tooltip("Compare legacy vs optimized pathfinding performance")]
    public bool testComparePerformance = false;
    
    [Tooltip("Number of iterations for performance comparison")]
    [Range(1, 100)]
    public int performanceTestIterations = 10;
    
    [Header("Optimized Settings")]
    [Tooltip("Weight for Weighted A* (1.0 = standard, >1.0 = greedy/faster)")]
    [Range(1.0f, 2.0f)]
    public float aStarWeight = 1.2f;
    
    [Tooltip("Use reduced neighbors (14 instead of 26) for optimized pathfinding")]
    public bool useReducedNeighbors = true;
    
    [Header("Async Settings")]
    [Tooltip("Nodes to process per frame during async pathfinding")]
    public int nodesPerFrame = 100;
    
    [Header("Results (Read Only)")]
    [SerializeField] private bool lastPathFound = false;
    [SerializeField] private int lastPathWaypointCount = 0;
    [SerializeField] private float lastPathTimeMs = 0f;
    [SerializeField] private bool currentPosNavigable = false;
    [SerializeField] private bool targetPosNavigable = false;
    [SerializeField] private bool hasLineOfSight = false;
    [SerializeField] private bool isWithinBounds = false;
    [SerializeField] private string lastTestResult = "No test run yet";
    
    [Header("Performance Comparison Results")]
    [SerializeField] private float legacyAvgTimeMs = 0f;
    [SerializeField] private float optimizedAvgTimeMs = 0f;
    [SerializeField] private float speedupFactor = 0f;
    
    [Header("Debug Visualization")]
    public bool drawPathGizmos = true;
    public bool drawLineOfSightGizmo = true;
    [Tooltip("Show waypoint position labels in scene view")]
    public bool showWaypointLabels = true;
    [Tooltip("Show waypoint coordinates in console")]
    public bool logWaypointPositions = false;
    public Color gizmoPathColor = Color.magenta;
    public Color gizmoLineOfSightColor = Color.cyan;
    public Color gizmoBlockedColor = Color.red;
    
    // Internal state
    private List<Vector3> currentPath;
    private Coroutine asyncPathCoroutine;
    private bool isAsyncPathfinding = false;

    void Start()
    {
        ValidateReferences();
    }

    void Update()
    {
        // Handle toggle tests
        HandleTestToggles();
        
        // Continuous pathfinding for stress testing
        if (continuousPathfinding && navSystem != null && target != null)
        {
            TestFindPath();
        }
    }
    
    private void ValidateReferences()
    {
        if (navSystem == null)
        {
            Debug.LogError("[NavTester] AerialNavSystem reference is not set!");
        }
        if (target == null)
        {
            Debug.LogWarning("[NavTester] Target transform is not set. Some tests will not work.");
        }
    }
    
    private void HandleTestToggles()
    {
        // Test Find Path (Legacy)
        if (testFindPath)
        {
            testFindPath = false;
            TestFindPath();
        }
        
        // Test Async Find Path (Legacy)
        if (testFindPathAsync)
        {
            testFindPathAsync = false;
            TestFindPathAsync();
        }
        
        // Test Is Position Navigable (current position)
        if (testIsPositionNavigable)
        {
            testIsPositionNavigable = false;
            TestIsPositionNavigable(transform.position, "Current Position");
        }
        
        // Test Is Target Navigable
        if (testIsTargetNavigable)
        {
            testIsTargetNavigable = false;
            if (target != null)
            {
                TestIsPositionNavigable(target.position, "Target Position");
            }
            else
            {
                LogResult("Target not set!", true);
            }
        }
        
        // Test Line of Sight
        if (testLineOfSight)
        {
            testLineOfSight = false;
            TestLineOfSight();
        }
        
        // Test Is Within Bounds
        if (testIsWithinBounds)
        {
            testIsWithinBounds = false;
            TestIsWithinBounds();
        }
        
        // ===== OPTIMIZED PATHFINDING TESTS =====
        
        // Test Optimized Pathfinding (Supermethod)
        if (testFindPathOptimized)
        {
            testFindPathOptimized = false;
            TestFindPathOptimized();
        }
        
        // Test Weighted A*
        if (testFindPathWeighted)
        {
            testFindPathWeighted = false;
            TestFindPathWeighted();
        }
        
        // Test Optimized Async
        if (testFindPathOptimizedAsync)
        {
            testFindPathOptimizedAsync = false;
            TestFindPathOptimizedAsync();
        }
        
        // Performance Comparison
        if (testComparePerformance)
        {
            testComparePerformance = false;
            TestComparePerformance();
        }
    }
    
    #region Test Methods
    
    public void TestFindPath()
    {
        if (!ValidateForPathfinding()) return;
        
        float startTime = Time.realtimeSinceStartup;
        currentPath = navSystem.FindPath(transform.position, target.position);
        float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
        
        lastPathTimeMs = elapsed;
        
        if (currentPath != null)
        {
            lastPathFound = true;
            lastPathWaypointCount = currentPath.Count;
            
            string result = $"[LEGACY] Path FOUND! Waypoints: {currentPath.Count}, Time: {elapsed:F3}ms";
            
            if (logWaypointPositions)
            {
                result += "\n  Waypoints: " + GetWaypointString();
            }
            
            LogResult(result);
        }
        else
        {
            lastPathFound = false;
            lastPathWaypointCount = 0;
            LogResult($"[LEGACY] Path NOT FOUND. Time: {elapsed:F3}ms", true);
        }
    }
    
    public void TestFindPathAsync()
    {
        if (!ValidateForPathfinding()) return;
        
        if (isAsyncPathfinding)
        {
            LogResult("Async pathfinding already in progress!", true);
            return;
        }
        
        if (asyncPathCoroutine != null)
        {
            StopCoroutine(asyncPathCoroutine);
        }
        
        asyncPathCoroutine = StartCoroutine(AsyncPathfindingCoroutine());
    }
    
    private IEnumerator AsyncPathfindingCoroutine()
    {
        isAsyncPathfinding = true;
        float startTime = Time.realtimeSinceStartup;
        int frameCount = 0;
        
        LogResult("[LEGACY ASYNC] Pathfinding started...");
        
        var enumerator = navSystem.FindPathAsync(transform.position, target.position, nodesPerFrame);
        
        while (enumerator.MoveNext())
        {
            frameCount++;
            
            if (enumerator.Current != null)
            {
                // Path found!
                currentPath = enumerator.Current;
                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                
                lastPathFound = true;
                lastPathWaypointCount = currentPath.Count;
                lastPathTimeMs = elapsed;
                
                string result = $"[LEGACY ASYNC] Path FOUND! Waypoints: {currentPath.Count}, Time: {elapsed:F3}ms, Frames: {frameCount}";
                
                if (logWaypointPositions)
                {
                    result += "\n  Waypoints: " + GetWaypointString();
                }
                
                LogResult(result);
                
                isAsyncPathfinding = false;
                yield break;
            }
            
            yield return null;
        }
        
        // No path found
        float elapsedFail = (Time.realtimeSinceStartup - startTime) * 1000f;
        lastPathFound = false;
        lastPathWaypointCount = 0;
        currentPath = null;
        lastPathTimeMs = elapsedFail;
        LogResult($"[LEGACY ASYNC] Path NOT FOUND. Frames: {frameCount}, Time: {elapsedFail:F3}ms", true);
        
        isAsyncPathfinding = false;
    }
    
    public void TestIsPositionNavigable(Vector3 position, string positionName)
    {
        if (navSystem == null)
        {
            LogResult("NavSystem not set!", true);
            return;
        }
        
        bool navigable = navSystem.IsPositionNavigable(position);
        
        if (positionName == "Current Position")
            currentPosNavigable = navigable;
        else if (positionName == "Target Position")
            targetPosNavigable = navigable;
        
        LogResult($"{positionName} ({position}) is {(navigable ? "NAVIGABLE" : "BLOCKED")}", !navigable);
    }
    
    public void TestLineOfSight()
    {
        if (!ValidateForPathfinding()) return;
        
        hasLineOfSight = navSystem.HasLineOfSight(transform.position, target.position);
        float distance = Vector3.Distance(transform.position, target.position);
        
        LogResult($"Line of Sight to target: {(hasLineOfSight ? "CLEAR" : "BLOCKED")} (Distance: {distance:F2})", !hasLineOfSight);
    }
    
    public void TestIsWithinBounds()
    {
        if (navSystem == null)
        {
            LogResult("NavSystem not set!", true);
            return;
        }
        
        bool selfInBounds = navSystem.IsWithinBounds(transform.position);
        bool targetInBounds = target != null && navSystem.IsWithinBounds(target.position);
        
        isWithinBounds = selfInBounds;
        
        string result = $"Within Bounds - Self: {(selfInBounds ? "YES" : "NO")}";
        if (target != null)
        {
            result += $", Target: {(targetInBounds ? "YES" : "NO")}";
        }
        
        var bounds = navSystem.GetBounds();
        if (bounds.HasValue)
        {
            result += $"\nBounds: Center={bounds.Value.center}, Size={bounds.Value.size}";
        }
        else
        {
            result += "\nNo bounds set (global navigation)";
        }
        
        LogResult(result, !selfInBounds);
    }
    
    #endregion
    
    #region Optimized Pathfinding Test Methods
    
    /// <summary>
    /// Tests the optimized FindPathOptimized supermethod.
    /// Uses caching, weighted A*, and optionally reduced neighbors.
    /// </summary>
    public void TestFindPathOptimized()
    {
        if (!ValidateForPathfinding()) return;
        
        float startTime = Time.realtimeSinceStartup;
        currentPath = navSystem.FindPathOptimized(transform.position, target.position, aStarWeight, useReducedNeighbors);
        float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
        
        lastPathTimeMs = elapsed;
        
        if (currentPath != null)
        {
            lastPathFound = true;
            lastPathWaypointCount = currentPath.Count;
            
            string result = $"[OPTIMIZED] Path FOUND! Waypoints: {currentPath.Count}, Time: {elapsed:F3}ms";
            result += $"\n  Settings: Weight={aStarWeight:F2}, ReducedNeighbors={useReducedNeighbors}";
            
            if (logWaypointPositions)
            {
                result += "\n  Waypoints: " + GetWaypointString();
            }
            
            LogResult(result);
        }
        else
        {
            lastPathFound = false;
            lastPathWaypointCount = 0;
            LogResult($"[OPTIMIZED] Path NOT FOUND. Time: {elapsed:F3}ms", true);
        }
    }
    
    /// <summary>
    /// Tests the weighted A* pathfinding (without caching or reduced neighbors).
    /// </summary>
    public void TestFindPathWeighted()
    {
        if (!ValidateForPathfinding()) return;
        
        float startTime = Time.realtimeSinceStartup;
        currentPath = navSystem.FindPathWeighted(transform.position, target.position, aStarWeight);
        float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
        
        lastPathTimeMs = elapsed;
        
        if (currentPath != null)
        {
            lastPathFound = true;
            lastPathWaypointCount = currentPath.Count;
            
            string result = $"[WEIGHTED A*] Path FOUND! Waypoints: {currentPath.Count}, Time: {elapsed:F3}ms";
            result += $"\n  Weight={aStarWeight:F2}";
            
            if (logWaypointPositions)
            {
                result += "\n  Waypoints: " + GetWaypointString();
            }
            
            LogResult(result);
        }
        else
        {
            lastPathFound = false;
            lastPathWaypointCount = 0;
            LogResult($"[WEIGHTED A*] Path NOT FOUND. Time: {elapsed:F3}ms", true);
        }
    }
    
    /// <summary>
    /// Tests the optimized async pathfinding.
    /// </summary>
    public void TestFindPathOptimizedAsync()
    {
        if (!ValidateForPathfinding()) return;
        
        if (isAsyncPathfinding)
        {
            LogResult("Async pathfinding already in progress!", true);
            return;
        }
        
        if (asyncPathCoroutine != null)
        {
            StopCoroutine(asyncPathCoroutine);
        }
        
        asyncPathCoroutine = StartCoroutine(OptimizedAsyncPathfindingCoroutine());
    }
    
    private IEnumerator OptimizedAsyncPathfindingCoroutine()
    {
        isAsyncPathfinding = true;
        float startTime = Time.realtimeSinceStartup;
        int frameCount = 0;
        
        LogResult("[OPTIMIZED ASYNC] Pathfinding started...");
        
        var enumerator = navSystem.FindPathOptimizedAsync(
            transform.position, target.position, 
            aStarWeight, useReducedNeighbors, nodesPerFrame);
        
        while (enumerator.MoveNext())
        {
            frameCount++;
            
            if (enumerator.Current != null)
            {
                // Path found!
                currentPath = enumerator.Current;
                float elapsed = (Time.realtimeSinceStartup - startTime) * 1000f;
                
                lastPathFound = true;
                lastPathWaypointCount = currentPath.Count;
                lastPathTimeMs = elapsed;
                
                string result = $"[OPTIMIZED ASYNC] Path FOUND! Waypoints: {currentPath.Count}, Time: {elapsed:F3}ms, Frames: {frameCount}";
                result += $"\n  Settings: Weight={aStarWeight:F2}, ReducedNeighbors={useReducedNeighbors}";
                
                if (logWaypointPositions)
                {
                    result += "\n  Waypoints: " + GetWaypointString();
                }
                
                LogResult(result);
                
                isAsyncPathfinding = false;
                yield break;
            }
            
            yield return null;
        }
        
        // No path found
        float elapsedFail = (Time.realtimeSinceStartup - startTime) * 1000f;
        lastPathFound = false;
        lastPathWaypointCount = 0;
        currentPath = null;
        lastPathTimeMs = elapsedFail;
        LogResult($"[OPTIMIZED ASYNC] Path NOT FOUND. Frames: {frameCount}, Time: {elapsedFail:F3}ms", true);
        
        isAsyncPathfinding = false;
    }
    
    /// <summary>
    /// Compares legacy FindPath vs optimized FindPathOptimized performance.
    /// Runs multiple iterations to get accurate timing.
    /// </summary>
    public void TestComparePerformance()
    {
        if (!ValidateForPathfinding()) return;
        
        Debug.Log("[NavTester] ========== PERFORMANCE COMPARISON ==========");
        
        // Warm up
        navSystem.FindPath(transform.position, target.position);
        navSystem.FindPathOptimized(transform.position, target.position, aStarWeight, useReducedNeighbors);
        
        // Test legacy
        float legacyTotal = 0f;
        int legacySuccessCount = 0;
        int legacyWaypoints = 0;
        
        for (int i = 0; i < performanceTestIterations; i++)
        {
            float startTime = Time.realtimeSinceStartup;
            var path = navSystem.FindPath(transform.position, target.position);
            legacyTotal += (Time.realtimeSinceStartup - startTime) * 1000f;
            
            if (path != null)
            {
                legacySuccessCount++;
                legacyWaypoints = path.Count;
            }
        }
        
        // Test optimized
        float optimizedTotal = 0f;
        int optimizedSuccessCount = 0;
        int optimizedWaypoints = 0;
        
        for (int i = 0; i < performanceTestIterations; i++)
        {
            float startTime = Time.realtimeSinceStartup;
            var path = navSystem.FindPathOptimized(transform.position, target.position, aStarWeight, useReducedNeighbors);
            optimizedTotal += (Time.realtimeSinceStartup - startTime) * 1000f;
            
            if (path != null)
            {
                optimizedSuccessCount++;
                optimizedWaypoints = path.Count;
            }
        }
        
        // Calculate results
        legacyAvgTimeMs = legacyTotal / performanceTestIterations;
        optimizedAvgTimeMs = optimizedTotal / performanceTestIterations;
        speedupFactor = legacyAvgTimeMs > 0 ? legacyAvgTimeMs / optimizedAvgTimeMs : 0f;
        
        // Log results
        string result = $"[PERFORMANCE COMPARISON] ({performanceTestIterations} iterations)";
        result += $"\n  LEGACY FindPath:";
        result += $"\n    Avg Time: {legacyAvgTimeMs:F3}ms";
        result += $"\n    Success: {legacySuccessCount}/{performanceTestIterations}";
        result += $"\n    Waypoints: {legacyWaypoints}";
        result += $"\n  OPTIMIZED FindPathOptimized (Weight={aStarWeight:F2}, ReducedNeighbors={useReducedNeighbors}):";
        result += $"\n    Avg Time: {optimizedAvgTimeMs:F3}ms";
        result += $"\n    Success: {optimizedSuccessCount}/{performanceTestIterations}";
        result += $"\n    Waypoints: {optimizedWaypoints}";
        result += $"\n  SPEEDUP: {speedupFactor:F2}x faster";
        
        if (optimizedWaypoints > legacyWaypoints && legacyWaypoints > 0)
        {
            float pathLengthIncrease = ((float)optimizedWaypoints / legacyWaypoints - 1f) * 100f;
            result += $"\n  PATH LENGTH: +{pathLengthIncrease:F1}% (trade-off for speed)";
        }
        
        LogResult(result);
        Debug.Log("[NavTester] ========== COMPARISON COMPLETE ==========");
    }
    
    /// <summary>
    /// Gets a formatted string of all waypoint positions.
    /// </summary>
    private string GetWaypointString()
    {
        if (currentPath == null || currentPath.Count == 0)
            return "No waypoints";
        
        return string.Join(" -> ", currentPath.Select(v => $"({v.x:F1}, {v.y:F1}, {v.z:F1})"));
    }
    
    #region Utility
    
    private bool ValidateForPathfinding()
    {
        if (navSystem == null)
        {
            LogResult("NavSystem not set!", true);
            return false;
        }
        if (target == null)
        {
            LogResult("Target not set!", true);
            return false;
        }
        return true;
    }
    
    private void LogResult(string message, bool isWarning = false)
    {
        lastTestResult = message;
        
        if (isWarning)
            Debug.LogWarning($"[NavTester] {message}");
        else
            Debug.Log($"[NavTester] {message}");
    }
    
    #endregion
    
    #region Context Menu Tests
    
    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        Debug.Log("[NavTester] ========== RUNNING ALL TESTS ==========");
        
        TestIsWithinBounds();
        TestIsPositionNavigable(transform.position, "Current Position");
        
        if (target != null)
        {
            TestIsPositionNavigable(target.position, "Target Position");
            TestLineOfSight();
            TestFindPath();
        }
        
        Debug.Log("[NavTester] ========== ALL TESTS COMPLETE ==========");
    }
    
    [ContextMenu("Run All Optimized Tests")]
    public void RunAllOptimizedTests()
    {
        Debug.Log("[NavTester] ========== RUNNING OPTIMIZED TESTS ==========");
        
        if (target != null)
        {
            TestFindPathOptimized();
            TestFindPathWeighted();
            TestComparePerformance();
        }
        else
        {
            LogResult("Target not set!", true);
        }
        
        Debug.Log("[NavTester] ========== OPTIMIZED TESTS COMPLETE ==========");
    }
    
    [ContextMenu("Clear Path")]
    public void ClearPath()
    {
        currentPath = null;
        lastPathFound = false;
        lastPathWaypointCount = 0;
        lastPathTimeMs = 0f;
        LogResult("Path cleared");
    }
    
    [ContextMenu("Clear Navigation Cache")]
    public void ClearNavigationCache()
    {
        if (navSystem != null)
        {
            navSystem.ClearNavigabilityCache();
            LogResult("Navigation cache cleared");
        }
        else
        {
            LogResult("NavSystem not set!", true);
        }
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmos()
    {
        // Draw path
        if (drawPathGizmos && currentPath != null && currentPath.Count >= 2)
        {
            Gizmos.color = gizmoPathColor;
            
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
                Gizmos.DrawWireSphere(currentPath[i], 0.2f);
            }
            
            // Draw last point
            Gizmos.DrawWireSphere(currentPath[currentPath.Count - 1], 0.2f);
            
            // Highlight start and end
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(currentPath[0], 0.3f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(currentPath[currentPath.Count - 1], 0.3f);
            
            // Draw waypoint labels if enabled
            if (showWaypointLabels)
            {
                #if UNITY_EDITOR
                for (int i = 0; i < currentPath.Count; i++)
                {
                    Vector3 labelPos = currentPath[i] + Vector3.up * 0.5f;
                    string label = $"[{i}]\n({currentPath[i].x:F1}, {currentPath[i].y:F1}, {currentPath[i].z:F1})";
                    
                    // Color code start, end, and middle waypoints
                    GUIStyle style = new GUIStyle();
                    if (i == 0)
                        style.normal.textColor = Color.green;
                    else if (i == currentPath.Count - 1)
                        style.normal.textColor = Color.red;
                    else
                        style.normal.textColor = Color.white;
                    
                    style.fontSize = 10;
                    style.fontStyle = FontStyle.Bold;
                    
                    UnityEditor.Handles.Label(labelPos, label, style);
                }
                #endif
            }
        }
        
        // Draw line of sight indicator
        if (drawLineOfSightGizmo && target != null)
        {
            if (navSystem != null && navSystem.HasLineOfSight(transform.position, target.position))
            {
                Gizmos.color = gizmoLineOfSightColor;
            }
            else
            {
                Gizmos.color = gizmoBlockedColor;
            }
            
            // Draw dashed line effect
            Vector3 start = transform.position;
            Vector3 end = target.position;
            float dashLength = 0.5f;
            float distance = Vector3.Distance(start, end);
            Vector3 direction = (end - start).normalized;
            
            for (float d = 0; d < distance; d += dashLength * 2)
            {
                Vector3 dashStart = start + direction * d;
                Vector3 dashEnd = start + direction * Mathf.Min(d + dashLength, distance);
                Gizmos.DrawLine(dashStart, dashEnd);
            }
        }
        
        // Draw current position indicator
        if (navSystem != null)
        {
            bool navigable = navSystem.IsPositionNavigable(transform.position);
            Gizmos.color = navigable ? Color.green : Color.red;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw agent radius preview
        if (navSystem != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, navSystem.agentRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, navSystem.agentRadius);
        }
    }
    
    #endregion
}
#endregion