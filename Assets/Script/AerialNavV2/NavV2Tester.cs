using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Test harness for AerialSystemNavAgents multi-agent navigation system.
/// Spawns and manages test scenarios for path following, steering, and avoidance.
/// </summary>
public class NavV2Tester : MonoBehaviour
{
    #region Configuration

    [Header("Agent Spawning")]
    [Tooltip("Prefab with AerialSystemNavAgents component attached")]
    public GameObject agentPrefab;
    
    [Tooltip("Number of agents to spawn")]
    [Range(1, 100)]
    public int agentCount = 10;
    
    [Tooltip("Spacing between agents at spawn")]
    public float spawnSpacing = 2f;
    
    [Tooltip("Random offset range for spawn positions")]
    public float spawnRandomness = 1f;
    
    [Header("Path Configuration")]
    [Tooltip("Path waypoints for agents to follow")]
    public Transform[] pathWaypoints;
    
    [Tooltip("Use circular path generation if no waypoints provided")]
    public bool generateCircularPath = true;
    
    [Tooltip("Radius of generated circular path")]
    public float circularPathRadius = 20f;
    
    [Tooltip("Number of waypoints in generated circular path")]
    [Range(4, 32)]
    public int circularPathWaypoints = 8;
    
    [Header("Test Controls")]
    [Tooltip("Spawn agents on Start")]
    public bool spawnOnStart = true;
    
    [Tooltip("Spawn all agents at runtime")]
    public bool spawnAgents = false;
    
    [Tooltip("Clear all spawned agents")]
    public bool clearAgents = false;
    
    [Tooltip("Randomize agent starting positions")]
    public bool randomizeStartPositions = false;
    
    [Tooltip("Toggle debug gizmos for all agents")]
    public bool toggleDebugGizmos = false;
    
    [Header("Performance Testing")]
    [Tooltip("Show FPS counter")]
    public bool showFPS = true;
    
    [Tooltip("Log performance stats every N seconds")]
    public float performanceLogInterval = 5f;
    
    [Header("Scenario Presets")]
    [Tooltip("Test scenario to configure")]
    public TestScenario scenario = TestScenario.Default;
    
    public enum TestScenario
    {
        Default,                    // Standard settings
        HighDensity,               // Many agents, tight spacing
        LowDensity,                // Few agents, wide spacing
        FastTraffic,               // High speeds
        SlowTraffic,               // Low speeds
        ObstacleAvoidance,         // With obstacles
        NoSteering,                // Path following only
        MaxSeparation,             // Strong separation forces
        TightFormation             // Weak separation, strong alignment
    }
    
    [Header("Debug Info (Read Only)")]
    [SerializeField] private int activeAgentCount = 0;
    [SerializeField] private float currentFPS = 0f;
    [SerializeField] private float averageFPS = 0f;
    [SerializeField] private string lastAction = "None";

    #endregion

    #region State

    private List<GameObject> spawnedAgents = new List<GameObject>(128); // Pre-allocated capacity
    private GameObject pathWaypointsRoot;
    private float performanceLogTimer = 0f;
    private float fpsUpdateTimer = 0f;
    private int frameCount = 0;
    private float fpsAccumulator = 0f;
    
    // Reusable list for GetActiveAgents to avoid allocation
    private List<GameObject> activeAgentsResult = new List<GameObject>(128);

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (spawnOnStart)
        {
            SetupTest();
        }
    }

    private void Update()
    {
        HandleTestToggles();
        UpdatePerformanceStats();
    }

    private void OnGUI()
    {
        if (showFPS)
        {
            DrawFPSCounter();
        }
    }

    #endregion

    #region Test Setup

    /// <summary>
    /// Setup the complete test environment.
    /// </summary>
    public void SetupTest()
    {
        ValidateConfiguration();
        GeneratePathIfNeeded();
        ApplyScenarioPreset();
        SpawnTestAgents();
        
        lastAction = $"Test setup complete: {agentCount} agents spawned";
        Debug.Log($"[NavV2Tester] {lastAction}");
    }

    private void ValidateConfiguration()
    {
        if (agentPrefab == null)
        {
            Debug.LogError("[NavV2Tester] Agent prefab is not assigned! Cannot spawn agents.", this);
            return;
        }

        if (agentPrefab.GetComponent<AerialSystemNavAgents>() == null)
        {
            Debug.LogError("[NavV2Tester] Agent prefab does not have AerialSystemNavAgents component!", this);
            return;
        }
    }

    private void GeneratePathIfNeeded()
    {
        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            if (generateCircularPath)
            {
                GenerateCircularPath();
                lastAction = $"Generated circular path with {circularPathWaypoints} waypoints";
                Debug.Log($"[NavV2Tester] {lastAction}");
            }
            else
            {
                Debug.LogWarning("[NavV2Tester] No path waypoints configured and auto-generation disabled!");
            }
        }
    }

    /// <summary>
    /// Generate a circular path around the tester object.
    /// </summary>
    private void GenerateCircularPath()
    {
        // Create root object for waypoints
        if (pathWaypointsRoot == null)
        {
            pathWaypointsRoot = new GameObject("Generated_Path_Waypoints");
            pathWaypointsRoot.transform.SetParent(transform);
            pathWaypointsRoot.transform.localPosition = Vector3.zero;
        }

        // Clear existing waypoints
        foreach (Transform child in pathWaypointsRoot.transform)
        {
            Destroy(child.gameObject);
        }

        pathWaypoints = new Transform[circularPathWaypoints];

        float angleStep = 360f / circularPathWaypoints;

        for (int i = 0; i < circularPathWaypoints; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                Mathf.Cos(angle) * circularPathRadius,
                0f,
                Mathf.Sin(angle) * circularPathRadius
            );

            GameObject waypoint = new GameObject($"Waypoint_{i}");
            waypoint.transform.SetParent(pathWaypointsRoot.transform);
            waypoint.transform.position = transform.position + position;
            
            pathWaypoints[i] = waypoint.transform;
        }
    }

    #endregion

    #region Agent Spawning

    /// <summary>
    /// Spawn test agents along the path.
    /// </summary>
    private void SpawnTestAgents()
    {
        if (agentPrefab == null || pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogWarning("[NavV2Tester] Cannot spawn agents: missing prefab or path!");
            return;
        }

        ClearAllAgents();

        for (int i = 0; i < agentCount; i++)
        {
            SpawnAgent(i);
        }

        activeAgentCount = spawnedAgents.Count;
        lastAction = $"Spawned {activeAgentCount} agents";
    }

    private void SpawnAgent(int index)
    {
        // Determine spawn position along path
        int waypointIndex = index % pathWaypoints.Length;
        Vector3 basePosition = pathWaypoints[waypointIndex].position;

        // Add spacing offset along path direction
        Vector3 spawnOffset = Vector3.zero;
        if (pathWaypoints.Length > 1)
        {
            int nextWaypointIndex = (waypointIndex + 1) % pathWaypoints.Length;
            Vector3 pathDirection = (pathWaypoints[nextWaypointIndex].position - basePosition).normalized;
            spawnOffset = pathDirection * (index / pathWaypoints.Length) * spawnSpacing;
        }

        // Add random offset
        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnRandomness, spawnRandomness),
            Random.Range(-spawnRandomness, spawnRandomness),
            Random.Range(-spawnRandomness, spawnRandomness)
        );

        Vector3 spawnPosition = basePosition + spawnOffset + randomOffset;

        // Instantiate agent
        GameObject agent = Instantiate(agentPrefab, spawnPosition, Quaternion.identity);
        agent.name = $"Agent_{index}";
        agent.transform.SetParent(transform);

        // Configure agent
        AerialSystemNavAgents agentScript = agent.GetComponent<AerialSystemNavAgents>();
        if (agentScript != null)
        {
            agentScript.SetPath(pathWaypoints, waypointIndex);
        }

        spawnedAgents.Add(agent);
    }

    /// <summary>
    /// Clear all spawned agents.
    /// </summary>
    private void ClearAllAgents()
    {
        foreach (GameObject agent in spawnedAgents)
        {
            if (agent != null)
            {
                Destroy(agent);
            }
        }

        spawnedAgents.Clear();
        activeAgentCount = 0;
        
        // Clear component cache since agents are destroyed
        AerialSystemNavAgents.ClearAgentComponentCache();
    }

    #endregion

    #region Scenario Presets

    /// <summary>
    /// Apply scenario-specific settings to all agents.
    /// </summary>
    private void ApplyScenarioPreset()
    {
        switch (scenario)
        {
            case TestScenario.HighDensity:
                agentCount = 50;
                spawnSpacing = 1f;
                spawnRandomness = 0.5f;
                break;

            case TestScenario.LowDensity:
                agentCount = 5;
                spawnSpacing = 5f;
                spawnRandomness = 2f;
                break;

            case TestScenario.FastTraffic:
                ApplyToAllAgents(agent => {
                    agent.baseSpeed = 10f;
                    agent.maxSpeed = 15f;
                });
                break;

            case TestScenario.SlowTraffic:
                ApplyToAllAgents(agent => {
                    agent.baseSpeed = 2f;
                    agent.maxSpeed = 4f;
                });
                break;

            case TestScenario.NoSteering:
                ApplyToAllAgents(agent => {
                    agent.separationWeight = 0f;
                    agent.cohesionWeight = 0f;
                    agent.alignmentWeight = 0f;
                });
                break;

            case TestScenario.MaxSeparation:
                ApplyToAllAgents(agent => {
                    agent.separationWeight = 5f;
                    agent.cohesionWeight = 0.5f;
                    agent.alignmentWeight = 0.5f;
                });
                break;

            case TestScenario.TightFormation:
                ApplyToAllAgents(agent => {
                    agent.separationWeight = 0.5f;
                    agent.cohesionWeight = 2f;
                    agent.alignmentWeight = 3f;
                });
                break;

            case TestScenario.Default:
            default:
                // Use prefab defaults
                break;
        }
    }

    /// <summary>
    /// Apply a configuration action to all spawned agents.
    /// </summary>
    private void ApplyToAllAgents(System.Action<AerialSystemNavAgents> action)
    {
        foreach (GameObject agentObj in spawnedAgents)
        {
            if (agentObj != null)
            {
                AerialSystemNavAgents agent = agentObj.GetComponent<AerialSystemNavAgents>();
                if (agent != null)
                {
                    action(agent);
                }
            }
        }
    }

    #endregion

    #region Test Controls

    private void HandleTestToggles()
    {
        if (spawnAgents)
        {
            spawnAgents = false;
            SpawnTestAgents();
        }

        if (clearAgents)
        {
            clearAgents = false;
            ClearAllAgents();
            lastAction = "Cleared all agents";
        }

        if (randomizeStartPositions)
        {
            randomizeStartPositions = false;
            RandomizeAgentPositions();
        }

        if (toggleDebugGizmos)
        {
            toggleDebugGizmos = false;
            ToggleDebugGizmosForAll();
        }
    }

    private void RandomizeAgentPositions()
    {
        foreach (GameObject agentObj in spawnedAgents)
        {
            if (agentObj != null && pathWaypoints.Length > 0)
            {
                int randomWaypoint = Random.Range(0, pathWaypoints.Length);
                Vector3 randomPosition = pathWaypoints[randomWaypoint].position;
                randomPosition += new Vector3(
                    Random.Range(-spawnRandomness * 2, spawnRandomness * 2),
                    Random.Range(-spawnRandomness, spawnRandomness),
                    Random.Range(-spawnRandomness * 2, spawnRandomness * 2)
                );

                AerialSystemNavAgents agent = agentObj.GetComponent<AerialSystemNavAgents>();
                if (agent != null)
                {
                    agent.ResetAgent(randomPosition, randomWaypoint);
                }
            }
        }

        lastAction = "Randomized agent positions";
    }

    private void ToggleDebugGizmosForAll()
    {
        bool newState = false;
        
        // Check first agent's state to toggle
        if (spawnedAgents.Count > 0)
        {
            AerialSystemNavAgents firstAgent = spawnedAgents[0].GetComponent<AerialSystemNavAgents>();
            if (firstAgent != null)
            {
                newState = !firstAgent.showDebugGizmos;
            }
        }

        ApplyToAllAgents(agent => {
            agent.showDebugGizmos = newState;
        });

        lastAction = $"Debug gizmos {(newState ? "enabled" : "disabled")}";
    }

    #endregion

    #region Performance Tracking

    private void UpdatePerformanceStats()
    {
        // Update FPS
        frameCount++;
        fpsUpdateTimer += Time.deltaTime;
        fpsAccumulator += Time.deltaTime;

        if (fpsUpdateTimer >= 0.5f) // Update FPS display twice per second
        {
            currentFPS = frameCount / fpsUpdateTimer;
            frameCount = 0;
            fpsUpdateTimer = 0f;
        }

        // Calculate average FPS
        if (fpsAccumulator > 0f)
        {
            averageFPS = Time.frameCount / fpsAccumulator;
        }

        // Log performance periodically
        performanceLogTimer += Time.deltaTime;
        if (performanceLogTimer >= performanceLogInterval)
        {
            LogPerformanceStats();
            performanceLogTimer = 0f;
        }
    }

    private void LogPerformanceStats()
    {
        Debug.Log($"[NavV2Tester] Performance Stats - Agents: {activeAgentCount}, FPS: {currentFPS:F1}, Avg FPS: {averageFPS:F1}");
    }

    private void DrawFPSCounter()
    {
        int width = 200;
        int height = 80;
        
        GUI.Box(new Rect(10, 10, width, height), "");
        
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 14;
        style.normal.textColor = Color.white;
        
        GUI.Label(new Rect(20, 15, width - 20, 20), $"FPS: {currentFPS:F1}", style);
        GUI.Label(new Rect(20, 35, width - 20, 20), $"Avg: {averageFPS:F1}", style);
        GUI.Label(new Rect(20, 55, width - 20, 20), $"Agents: {activeAgentCount}", style);
    }

    #endregion

    #region Context Menu

    [ContextMenu("Setup Test")]
    private void ContextSetupTest()
    {
        SetupTest();
    }

    [ContextMenu("Spawn Agents")]
    private void ContextSpawnAgents()
    {
        SpawnTestAgents();
    }

    [ContextMenu("Clear Agents")]
    private void ContextClearAgents()
    {
        ClearAllAgents();
    }

    [ContextMenu("Randomize Positions")]
    private void ContextRandomizePositions()
    {
        RandomizeAgentPositions();
    }

    [ContextMenu("Generate Circular Path")]
    private void ContextGenerateCircularPath()
    {
        GenerateCircularPath();
    }

    [ContextMenu("Apply Scenario: High Density")]
    private void ContextHighDensity()
    {
        scenario = TestScenario.HighDensity;
        ApplyScenarioPreset();
        lastAction = "Applied High Density scenario";
    }

    [ContextMenu("Apply Scenario: Fast Traffic")]
    private void ContextFastTraffic()
    {
        scenario = TestScenario.FastTraffic;
        ApplyScenarioPreset();
        lastAction = "Applied Fast Traffic scenario";
    }

    [ContextMenu("Apply Scenario: Tight Formation")]
    private void ContextTightFormation()
    {
        scenario = TestScenario.TightFormation;
        ApplyScenarioPreset();
        lastAction = "Applied Tight Formation scenario";
    }

    [ContextMenu("Toggle All Debug Gizmos")]
    private void ContextToggleGizmos()
    {
        ToggleDebugGizmosForAll();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        // Draw path if configured
        if (pathWaypoints != null && pathWaypoints.Length > 1)
        {
            Gizmos.color = Color.cyan;

            for (int i = 0; i < pathWaypoints.Length - 1; i++)
            {
                if (pathWaypoints[i] != null && pathWaypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(pathWaypoints[i].position, pathWaypoints[i + 1].position);
                }
            }

            // Draw loop if needed
            if (pathWaypoints.Length > 2 && pathWaypoints[0] != null && pathWaypoints[pathWaypoints.Length - 1] != null)
            {
                Gizmos.DrawLine(pathWaypoints[pathWaypoints.Length - 1].position, pathWaypoints[0].position);
            }
        }

        // Draw spawn area
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        if (generateCircularPath)
        {
            DrawCircle(transform.position, circularPathRadius, 32);
        }
    }

    private void DrawCircle(Vector3 center, float radius, int segments)
    {
        float angleStep = 360f / segments;
        Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );

            Gizmos.DrawLine(previousPoint, newPoint);
            previousPoint = newPoint;
        }
    }

    #endregion

    #region Public Utilities

    /// <summary>
    /// Get all currently active agents.
    /// Note: Returns a shared list - do not hold reference across frames.
    /// </summary>
    public List<GameObject> GetActiveAgents()
    {
        activeAgentsResult.Clear();
        activeAgentsResult.AddRange(spawnedAgents);
        return activeAgentsResult;
    }

    /// <summary>
    /// Add an obstacle at runtime for testing obstacle avoidance.
    /// </summary>
    public GameObject AddObstacle(Vector3 position, Vector3 size)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = position;
        obstacle.transform.localScale = size;
        obstacle.name = "Test_Obstacle";
        obstacle.transform.SetParent(transform);

        return obstacle;
    }

    /// <summary>
    /// Change scenario at runtime and reapply settings.
    /// </summary>
    public void ChangeScenario(TestScenario newScenario)
    {
        scenario = newScenario;
        ApplyScenarioPreset();
        lastAction = $"Changed to scenario: {newScenario}";
        Debug.Log($"[NavV2Tester] {lastAction}");
    }

    #endregion
}
