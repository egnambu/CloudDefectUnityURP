using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Multi-agent navigation system for NPCs that follow shared paths with local steering and avoidance.
/// Optimized for dozens of aerial agents (drones, vehicles) with no per-frame allocations.
/// 
/// Key Features:
/// - Shared path following (waypoint-based or spline sampling)
/// - Local steering: Separation, Cohesion (to path), Alignment
/// - Agent-to-agent collision prediction and avoidance
/// - Lightweight obstacle avoidance using NonAlloc raycasts
/// - Deterministic, no NavMesh dependency
/// 
/// Performance: No LINQ, no allocations, uses static buffers, NonAlloc physics queries,
/// and component caching to eliminate per-frame GC pressure.
/// </summary>
public class AerialSystemNavAgents : MonoBehaviour
{
    #region Configuration

    [Header("Path Following")]
    [Tooltip("The waypoints defining the shared path. Agents will follow these in sequence.")]
    public Transform[] pathWaypoints = new Transform[0];
    
    [Tooltip("Distance threshold to consider a waypoint reached")]
    public float waypointReachDistance = 1f;
    
    [Tooltip("If true, loop back to first waypoint when reaching the end")]
    public bool loopPath = true;
    
    [Header("Movement Settings")]
    [Tooltip("Base movement speed along the path")]
    public float baseSpeed = 5f;
    
    [Tooltip("Maximum speed the agent can reach")]
    public float maxSpeed = 8f;
    
    [Tooltip("How quickly the agent can accelerate/decelerate")]
    public float acceleration = 2f;
    
    [Tooltip("How quickly the agent rotates to face movement direction")]
    public float rotationSpeed = 5f;
    
    [Header("Local Steering - Separation")]
    [Tooltip("Radius to check for nearby agents to avoid")]
    public float separationRadius = 3f;
    
    [Tooltip("Weight of separation force (higher = stronger avoidance)")]
    public float separationWeight = 2f;
    
    [Tooltip("Layers to consider as other agents")]
    public LayerMask agentLayer = ~0;
    
    [Header("Local Steering - Cohesion")]
    [Tooltip("Radius to check for path corridor alignment")]
    public float cohesionRadius = 5f;
    
    [Tooltip("Weight of cohesion force (pulls agent back to path center)")]
    public float cohesionWeight = 1f;
    
    [Header("Local Steering - Alignment")]
    [Tooltip("Radius to check for velocity matching with nearby agents")]
    public float alignmentRadius = 4f;
    
    [Tooltip("Weight of alignment force (velocity smoothing)")]
    public float alignmentWeight = 0.8f;
    
    [Header("Agent-to-Agent Avoidance")]
    [Tooltip("How far ahead to predict collisions (in seconds)")]
    public float collisionLookaheadTime = 2f;
    
    [Tooltip("Distance threshold for collision prediction")]
    public float collisionAvoidanceDistance = 2f;
    
    [Tooltip("Maximum lateral offset to avoid collision")]
    public float maxLateralOffset = 3f;
    
    [Tooltip("Maximum vertical offset to avoid collision")]
    public float maxVerticalOffset = 2f;
    
    [Tooltip("How quickly agent returns to path after avoiding")]
    public float offsetRecoverySpeed = 1f;
    
    [Header("Obstacle Avoidance")]
    [Tooltip("Enable obstacle micro-avoidance using raycasts")]
    public bool enableObstacleAvoidance = true;
    
    [Tooltip("Distance to check for obstacles ahead")]
    public float obstacleCheckDistance = 5f;
    
    [Tooltip("Radius of the spherecast for obstacle detection")]
    public float obstacleCastRadius = 0.5f;
    
    [Tooltip("Layers considered as obstacles")]
    public LayerMask obstacleLayer = ~0;
    
    [Tooltip("Number of raycast directions to check (front, left, right, up, down)")]
    public int obstacleRayCount = 5;
    
    [Tooltip("Weight of obstacle avoidance force")]
    public float obstacleAvoidanceWeight = 3f;
    
    [Header("Debug Visualization")]
    public bool showDebugGizmos = true;
    public bool showPathGizmos = true;
    public bool showSteeringForces = false;
    public Color pathColor = Color.cyan;
    public Color separationColor = Color.red;
    public Color cohesionColor = Color.green;
    public Color alignmentColor = Color.blue;
    public Color avoidanceColor = Color.yellow;

    #endregion

    #region State

    // Path following state
    private int currentWaypointIndex = 0;
    private Vector3 currentTargetPosition;
    
    // Movement state
    private Vector3 currentVelocity = Vector3.zero;
    private float currentSpeed = 0f;
    
    // Avoidance state
    private Vector3 currentLateralOffset = Vector3.zero;
    private Vector3 currentVerticalOffset = Vector3.zero;
    private bool isAvoiding = false;
    
    // Cached values
    private Vector3 desiredDirection = Vector3.forward;
    private Vector3 pathDirection = Vector3.forward;
    
    #endregion

    #region Static Buffers (No Allocations)

    // Reusable buffers for Physics.OverlapSphereNonAlloc
    private static Collider[] nearbyAgentsBuffer = new Collider[32];
    private static Collider[] nearbyObstaclesBuffer = new Collider[16];
    private static RaycastHit[] raycastHitsBuffer = new RaycastHit[8];
    
    // Obstacle check directions (static, reused, normalized)
    private static readonly Vector3[] obstacleCheckDirections = new Vector3[]
    {
        Vector3.forward,                                        // Front
        new Vector3(0.707f, 0f, 0.707f),                       // Front-right (normalized)
        new Vector3(-0.707f, 0f, 0.707f),                      // Front-left (normalized)
        new Vector3(0f, 0.707f, 0.707f),                       // Front-up (normalized)
        new Vector3(0f, -0.707f, 0.707f)                       // Front-down (normalized)
    };

    #endregion

    #region Component Cache (GC Optimization)
    
    /// <summary>
    /// Cache for AerialSystemNavAgents components to avoid per-frame GetComponent calls.
    /// Key: Collider instance ID, Value: Cached agent component (can be null)
    /// </summary>
    private static Dictionary<int, AerialSystemNavAgents> agentComponentCache = new Dictionary<int, AerialSystemNavAgents>(64);
    
    /// <summary>
    /// Gets the AerialSystemNavAgents component from a collider using the cache.
    /// Avoids repeated GetComponent calls which cause GC allocations.
    /// </summary>
    private static AerialSystemNavAgents GetCachedAgentComponent(Collider collider)
    {
        int instanceId = collider.GetInstanceID();
        
        if (agentComponentCache.TryGetValue(instanceId, out AerialSystemNavAgents cachedAgent))
        {
            return cachedAgent;
        }
        
        // Cache miss - perform GetComponent and cache result
        AerialSystemNavAgents agent = collider.GetComponent<AerialSystemNavAgents>();
        agentComponentCache[instanceId] = agent; // Cache even if null
        return agent;
    }
    
    /// <summary>
    /// Clears the component cache. Call this if agents are destroyed/created at runtime.
    /// </summary>
    public static void ClearAgentComponentCache()
    {
        agentComponentCache.Clear();
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        ValidateSetup();
        InitializeAgent();
        
        // Pre-cache this agent's collider for faster lookups
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            agentComponentCache[myCollider.GetInstanceID()] = this;
        }
    }
    
    private void OnDestroy()
    {
        // Remove from cache when destroyed to prevent stale references
        Collider myCollider = GetComponent<Collider>();
        if (myCollider != null)
        {
            agentComponentCache.Remove(myCollider.GetInstanceID());
        }
    }

    private void Update()
    {
        if (pathWaypoints == null || pathWaypoints.Length == 0)
            return;

        // Calculate steering forces
        Vector3 pathFollowingForce = CalculatePathFollowingForce();
        Vector3 separationForce = CalculateSeparationForce();
        Vector3 cohesionForce = CalculateCohesionForce();
        Vector3 alignmentForce = CalculateAlignmentForce();
        Vector3 avoidanceForce = CalculateAgentAvoidanceForce();
        Vector3 obstacleForce = enableObstacleAvoidance ? CalculateObstacleAvoidanceForce() : Vector3.zero;

        // Combine all steering forces
        Vector3 steeringForce = pathFollowingForce +
                                separationForce * separationWeight +
                                cohesionForce * cohesionWeight +
                                alignmentForce * alignmentWeight +
                                avoidanceForce +
                                obstacleForce * obstacleAvoidanceWeight;

        // Apply steering to velocity
        ApplySteering(steeringForce);

        // Update position and rotation
        UpdateMovement();
        UpdateRotation();

        // Debug visualization
        if (showSteeringForces)
        {
            DebugDrawForces(separationForce, cohesionForce, alignmentForce, avoidanceForce, obstacleForce);
        }
    }

    #endregion

    #region Initialization & Validation

    private void ValidateSetup()
    {
        if (pathWaypoints == null || pathWaypoints.Length == 0)
        {
            Debug.LogWarning($"[AerialSystemNavAgents] {gameObject.name} has no path waypoints assigned!", this);
        }

        // Validate no null waypoints
        for (int i = 0; i < pathWaypoints.Length; i++)
        {
            if (pathWaypoints[i] == null)
            {
                Debug.LogError($"[AerialSystemNavAgents] Waypoint {i} is null on {gameObject.name}!", this);
            }
        }
    }

    private void InitializeAgent()
    {
        if (pathWaypoints.Length > 0 && pathWaypoints[0] != null)
        {
            currentWaypointIndex = 0;
            currentTargetPosition = pathWaypoints[0].position;
            currentSpeed = baseSpeed;
            
            // Calculate initial path direction
            if (pathWaypoints.Length > 1 && pathWaypoints[1] != null)
            {
                pathDirection = (pathWaypoints[1].position - pathWaypoints[0].position).normalized;
            }
        }
    }

    #endregion

    #region Path Following

    /// <summary>
    /// Calculate the steering force to follow the path.
    /// Returns a normalized direction toward the current waypoint.
    /// </summary>
    private Vector3 CalculatePathFollowingForce()
    {
        if (pathWaypoints.Length == 0 || pathWaypoints[currentWaypointIndex] == null)
            return Vector3.zero;

        currentTargetPosition = pathWaypoints[currentWaypointIndex].position;

        // Apply current avoidance offsets to the target position
        Vector3 offsetTarget = currentTargetPosition + currentLateralOffset + currentVerticalOffset;

        // Direction to target waypoint
        Vector3 directionToWaypoint = offsetTarget - transform.position;
        float distanceToWaypoint = directionToWaypoint.magnitude;

        // Check if waypoint reached
        if (distanceToWaypoint < waypointReachDistance)
        {
            AdvanceToNextWaypoint();
            
            // Recalculate after advancing
            if (pathWaypoints[currentWaypointIndex] != null)
            {
                currentTargetPosition = pathWaypoints[currentWaypointIndex].position;
                offsetTarget = currentTargetPosition + currentLateralOffset + currentVerticalOffset;
                directionToWaypoint = offsetTarget - transform.position;
            }
        }

        // Update desired direction
        if (directionToWaypoint.sqrMagnitude > 0.001f)
        {
            desiredDirection = directionToWaypoint.normalized;
        }

        return desiredDirection;
    }

    /// <summary>
    /// Advance to the next waypoint in the path.
    /// </summary>
    private void AdvanceToNextWaypoint()
    {
        int previousIndex = currentWaypointIndex;
        currentWaypointIndex++;

        // Handle end of path
        if (currentWaypointIndex >= pathWaypoints.Length)
        {
            if (loopPath)
            {
                currentWaypointIndex = 0;
            }
            else
            {
                currentWaypointIndex = pathWaypoints.Length - 1; // Stay at last waypoint
            }
        }

        // Update path direction for cohesion
        if (currentWaypointIndex < pathWaypoints.Length && pathWaypoints[currentWaypointIndex] != null)
        {
            int nextIndex = (currentWaypointIndex + 1) % pathWaypoints.Length;
            if (nextIndex < pathWaypoints.Length && pathWaypoints[nextIndex] != null)
            {
                pathDirection = (pathWaypoints[nextIndex].position - pathWaypoints[currentWaypointIndex].position).normalized;
            }
        }
    }

    #endregion

    #region Local Steering Forces

    /// <summary>
    /// Separation: Push away from nearby agents to prevent clustering.
    /// Uses radius-based checks, no flock-wide iteration.
    /// </summary>
    private Vector3 CalculateSeparationForce()
    {
        int nearbyCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            separationRadius,
            nearbyAgentsBuffer,
            agentLayer,
            QueryTriggerInteraction.Ignore
        );

        if (nearbyCount == 0)
            return Vector3.zero;

        Vector3 separationForce = Vector3.zero;
        int validNeighbors = 0;

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider otherCollider = nearbyAgentsBuffer[i];
            
            // Skip self
            if (otherCollider.transform == transform)
                continue;

            Vector3 awayFromOther = transform.position - otherCollider.transform.position;
            float distance = awayFromOther.magnitude;

            // Skip if too far (shouldn't happen due to OverlapSphere, but safety check)
            if (distance < 0.01f || distance > separationRadius)
                continue;

            // Weight by inverse distance (closer = stronger push)
            float weight = 1f - (distance / separationRadius);
            separationForce += awayFromOther.normalized * weight;
            validNeighbors++;
        }

        // Average the force
        if (validNeighbors > 0)
        {
            separationForce /= validNeighbors;
        }

        return separationForce;
    }

    /// <summary>
    /// Cohesion: Pull agent back toward the path corridor center.
    /// NOT toward other agents, but toward the ideal path line.
    /// </summary>
    private Vector3 CalculateCohesionForce()
    {
        // Find closest point on path segment
        Vector3 closestPointOnPath = FindClosestPointOnPath(transform.position);
        
        // Direction back to path
        Vector3 towardPath = closestPointOnPath - transform.position;
        float distanceFromPath = towardPath.magnitude;

        // Only apply cohesion if agent has drifted from path
        if (distanceFromPath < 0.1f)
            return Vector3.zero;

        // Stronger pull when further from path
        float cohesionStrength = Mathf.Clamp01(distanceFromPath / cohesionRadius);
        
        return towardPath.normalized * cohesionStrength;
    }

    /// <summary>
    /// Alignment: Match velocity with nearby agents for smooth flow.
    /// </summary>
    private Vector3 CalculateAlignmentForce()
    {
        int nearbyCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            alignmentRadius,
            nearbyAgentsBuffer,
            agentLayer,
            QueryTriggerInteraction.Ignore
        );

        if (nearbyCount == 0)
            return Vector3.zero;

        Vector3 averageVelocity = Vector3.zero;
        int validNeighbors = 0;

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider otherCollider = nearbyAgentsBuffer[i];
            
            // Skip self
            if (otherCollider.transform == transform)
                continue;

            // Use cached component lookup to avoid GC
            AerialSystemNavAgents otherAgent = GetCachedAgentComponent(otherCollider);
            if (otherAgent != null)
            {
                averageVelocity += otherAgent.currentVelocity;
                validNeighbors++;
            }
        }

        if (validNeighbors == 0)
            return Vector3.zero;

        // Average velocity
        averageVelocity /= validNeighbors;

        // Alignment force is the difference between our velocity and average
        Vector3 alignmentForce = averageVelocity - currentVelocity;

        return alignmentForce.normalized;
    }

    /// <summary>
    /// Find the closest point on the path to a given position.
    /// Used for cohesion to pull agents back to the path corridor.
    /// </summary>
    private Vector3 FindClosestPointOnPath(Vector3 position)
    {
        if (pathWaypoints.Length == 0)
            return position;

        if (pathWaypoints.Length == 1)
            return pathWaypoints[0].position;

        Vector3 closestPoint = pathWaypoints[0].position;
        float closestDistance = float.MaxValue;

        // Check each path segment
        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            if (pathWaypoints[i] == null || pathWaypoints[i + 1] == null)
                continue;

            Vector3 segmentStart = pathWaypoints[i].position;
            Vector3 segmentEnd = pathWaypoints[i + 1].position;

            Vector3 pointOnSegment = ClosestPointOnLineSegment(position, segmentStart, segmentEnd);
            float distance = Vector3.Distance(position, pointOnSegment);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = pointOnSegment;
            }
        }

        // If looping, check segment from last to first waypoint
        if (loopPath && pathWaypoints.Length > 2)
        {
            Vector3 segmentStart = pathWaypoints[pathWaypoints.Length - 1].position;
            Vector3 segmentEnd = pathWaypoints[0].position;

            Vector3 pointOnSegment = ClosestPointOnLineSegment(position, segmentStart, segmentEnd);
            float distance = Vector3.Distance(position, pointOnSegment);

            if (distance < closestDistance)
            {
                closestPoint = pointOnSegment;
            }
        }

        return closestPoint;
    }

    /// <summary>
    /// Find the closest point on a line segment to a given point.
    /// </summary>
    private Vector3 ClosestPointOnLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLength = line.magnitude;

        if (lineLength < 0.001f)
            return lineStart;

        Vector3 lineDirection = line / lineLength;
        Vector3 toPoint = point - lineStart;

        float projection = Vector3.Dot(toPoint, lineDirection);
        projection = Mathf.Clamp(projection, 0f, lineLength);

        return lineStart + lineDirection * projection;
    }

    #endregion

    #region Agent-to-Agent Avoidance

    /// <summary>
    /// Predict future collisions with other agents and apply avoidance offsets.
    /// Uses lateral and vertical offsets, then smoothly returns to path.
    /// </summary>
    private Vector3 CalculateAgentAvoidanceForce()
    {
        // Predict future position
        Vector3 futurePosition = transform.position + currentVelocity * collisionLookaheadTime;

        // Check for agents in predicted path
        int nearbyCount = Physics.OverlapSphereNonAlloc(
            futurePosition,
            collisionAvoidanceDistance,
            nearbyAgentsBuffer,
            agentLayer,
            QueryTriggerInteraction.Ignore
        );

        if (nearbyCount == 0)
        {
            // No collision predicted, recover offsets
            RecoverFromAvoidance();
            return Vector3.zero;
        }

        // Find closest threatening agent
        AerialSystemNavAgents closestThreat = null;
        float closestDistance = float.MaxValue;
        Vector3 closestThreatPosition = Vector3.zero;

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider otherCollider = nearbyAgentsBuffer[i];
            
            // Skip self
            if (otherCollider.transform == transform)
                continue;

            // Use cached component lookup to avoid GC
            AerialSystemNavAgents otherAgent = GetCachedAgentComponent(otherCollider);
            if (otherAgent == null)
                continue;

            // Predict other agent's future position
            Vector3 otherFuturePosition = otherAgent.transform.position + otherAgent.currentVelocity * collisionLookaheadTime;
            
            float distance = Vector3.Distance(futurePosition, otherFuturePosition);

            if (distance < closestDistance && distance < collisionAvoidanceDistance)
            {
                closestDistance = distance;
                closestThreat = otherAgent;
                closestThreatPosition = otherAgent.transform.position;
            }
        }

        // No valid threat
        if (closestThreat == null)
        {
            RecoverFromAvoidance();
            return Vector3.zero;
        }

        // Calculate avoidance offset
        isAvoiding = true;
        
        // Determine avoidance direction (lateral or vertical)
        Vector3 toThreat = closestThreatPosition - transform.position;
        Vector3 avoidanceDirection = Vector3.zero;

        // Use perpendicular direction for lateral avoidance
        Vector3 right = Vector3.Cross(pathDirection, Vector3.up).normalized;
        
        // Decide offset direction based on relative position
        float lateralDot = Vector3.Dot(toThreat, right);
        if (Mathf.Abs(lateralDot) > 0.1f)
        {
            // Apply lateral offset
            float offsetDirection = lateralDot > 0 ? -1f : 1f;
            currentLateralOffset = right * (offsetDirection * maxLateralOffset);
            avoidanceDirection = right * offsetDirection;
        }
        else
        {
            // Apply vertical offset
            float verticalDirection = transform.position.y > closestThreatPosition.y ? 1f : -1f;
            currentVerticalOffset = Vector3.up * (verticalDirection * maxVerticalOffset);
            avoidanceDirection = Vector3.up * verticalDirection;
        }

        return avoidanceDirection;
    }

    /// <summary>
    /// Smoothly return to the original path after avoidance.
    /// </summary>
    private void RecoverFromAvoidance()
    {
        if (!isAvoiding)
            return;

        float recoveryDelta = offsetRecoverySpeed * Time.deltaTime;

        // Lerp offsets back to zero
        currentLateralOffset = Vector3.Lerp(currentLateralOffset, Vector3.zero, recoveryDelta);
        currentVerticalOffset = Vector3.Lerp(currentVerticalOffset, Vector3.zero, recoveryDelta);

        // Check if recovery complete
        if (currentLateralOffset.sqrMagnitude < 0.01f && currentVerticalOffset.sqrMagnitude < 0.01f)
        {
            currentLateralOffset = Vector3.zero;
            currentVerticalOffset = Vector3.zero;
            isAvoiding = false;
        }
    }

    #endregion

    #region Obstacle Avoidance

    /// <summary>
    /// Lightweight obstacle avoidance using NonAlloc spherecasts.
    /// Checks multiple directions and biases steering away from obstacles.
    /// </summary>
    private Vector3 CalculateObstacleAvoidanceForce()
    {
        Vector3 avoidanceForce = Vector3.zero;
        int obstaclesDetected = 0;

        // Check multiple directions relative to current facing
        for (int i = 0; i < obstacleCheckDirections.Length && i < obstacleRayCount; i++)
        {
            Vector3 worldDirection = transform.TransformDirection(obstacleCheckDirections[i]);

            int hitCount = Physics.SphereCastNonAlloc(
                transform.position,
                obstacleCastRadius,
                worldDirection,
                raycastHitsBuffer,
                obstacleCheckDistance,
                obstacleLayer,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount > 0)
            {
                // Found obstacle in this direction
                RaycastHit hit = raycastHitsBuffer[0]; // Use closest hit

                // Steer away from obstacle
                Vector3 awayFromObstacle = transform.position - hit.point;
                float weight = 1f - (hit.distance / obstacleCheckDistance);
                avoidanceForce += awayFromObstacle.normalized * weight;
                obstaclesDetected++;
            }
        }

        // Average the avoidance force
        if (obstaclesDetected > 0)
        {
            avoidanceForce /= obstaclesDetected;
        }

        return avoidanceForce;
    }

    #endregion

    #region Movement & Steering Application

    /// <summary>
    /// Apply the combined steering force to the agent's velocity.
    /// Respects acceleration limits and max speed.
    /// </summary>
    private void ApplySteering(Vector3 steeringForce)
    {
        // Desired velocity based on steering
        Vector3 desiredVelocity = steeringForce.normalized * baseSpeed;

        // Calculate steering adjustment
        Vector3 steeringAdjustment = desiredVelocity - currentVelocity;

        // Limit steering by acceleration
        float maxSteeringChange = acceleration * Time.deltaTime;
        if (steeringAdjustment.magnitude > maxSteeringChange)
        {
            steeringAdjustment = steeringAdjustment.normalized * maxSteeringChange;
        }

        // Apply steering
        currentVelocity += steeringAdjustment;

        // Clamp to max speed
        if (currentVelocity.magnitude > maxSpeed)
        {
            currentVelocity = currentVelocity.normalized * maxSpeed;
        }

        currentSpeed = currentVelocity.magnitude;
    }

    /// <summary>
    /// Update the agent's position based on current velocity.
    /// </summary>
    private void UpdateMovement()
    {
        if (currentVelocity.sqrMagnitude < 0.001f)
            return;

        transform.position += currentVelocity * Time.deltaTime;
    }

    /// <summary>
    /// Smoothly rotate the agent to face the movement direction.
    /// </summary>
    private void UpdateRotation()
    {
        if (currentVelocity.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(currentVelocity);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Set a new path for this agent to follow.
    /// </summary>
    public void SetPath(Transform[] newPath, int startWaypointIndex = 0)
    {
        pathWaypoints = newPath;
        currentWaypointIndex = Mathf.Clamp(startWaypointIndex, 0, newPath.Length - 1);
        
        if (newPath.Length > 0)
        {
            currentTargetPosition = newPath[currentWaypointIndex].position;
        }
    }

    /// <summary>
    /// Get the current velocity of this agent (for alignment calculations).
    /// </summary>
    public Vector3 GetVelocity()
    {
        return currentVelocity;
    }

    /// <summary>
    /// Get the current waypoint index.
    /// </summary>
    public int GetCurrentWaypointIndex()
    {
        return currentWaypointIndex;
    }

    /// <summary>
    /// Reset the agent to a specific position and waypoint.
    /// </summary>
    public void ResetAgent(Vector3 position, int waypointIndex = 0)
    {
        transform.position = position;
        currentWaypointIndex = Mathf.Clamp(waypointIndex, 0, pathWaypoints.Length - 1);
        currentVelocity = Vector3.zero;
        currentSpeed = 0f;
        currentLateralOffset = Vector3.zero;
        currentVerticalOffset = Vector3.zero;
        isAvoiding = false;
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos)
            return;

        if (showPathGizmos)
        {
            DrawPathGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos)
            return;

        // Draw radii
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);

        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, cohesionRadius);

        Gizmos.color = new Color(0f, 0f, 1f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, alignmentRadius);

        // Draw current target
        if (pathWaypoints.Length > 0 && pathWaypoints[currentWaypointIndex] != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, currentTargetPosition);
            Gizmos.DrawWireSphere(currentTargetPosition, 0.5f);
        }

        // Draw velocity
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, currentVelocity);
    }

    private void DrawPathGizmos()
    {
        if (pathWaypoints == null || pathWaypoints.Length == 0)
            return;

        Gizmos.color = pathColor;

        // Draw path segments
        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            if (pathWaypoints[i] != null && pathWaypoints[i + 1] != null)
            {
                Gizmos.DrawLine(pathWaypoints[i].position, pathWaypoints[i + 1].position);
            }
        }

        // Draw loop connection
        if (loopPath && pathWaypoints.Length > 1 && pathWaypoints[0] != null && pathWaypoints[pathWaypoints.Length - 1] != null)
        {
            Gizmos.DrawLine(pathWaypoints[pathWaypoints.Length - 1].position, pathWaypoints[0].position);
        }

        // Draw waypoint spheres
        foreach (Transform waypoint in pathWaypoints)
        {
            if (waypoint != null)
            {
                Gizmos.DrawWireSphere(waypoint.position, 0.3f);
            }
        }
    }

    private void DebugDrawForces(Vector3 separation, Vector3 cohesion, Vector3 alignment, Vector3 avoidance, Vector3 obstacle)
    {
        // Draw separation force
        if (separation.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, separation * 2f, separationColor, 0f, false);
        }

        // Draw cohesion force
        if (cohesion.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, cohesion * 2f, cohesionColor, 0f, false);
        }

        // Draw alignment force
        if (alignment.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, alignment * 2f, alignmentColor, 0f, false);
        }

        // Draw avoidance force
        if (avoidance.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, avoidance * 3f, avoidanceColor, 0f, false);
        }

        // Draw obstacle force
        if (obstacle.sqrMagnitude > 0.01f)
        {
            Debug.DrawRay(transform.position, obstacle * 2f, Color.magenta, 0f, false);
        }
    }

    #endregion
}
