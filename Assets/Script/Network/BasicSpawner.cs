using Fusion;
using System.Collections.Generic;
using UnityEngine;

// Attach this to a GameObject in your scene.
// Assign PlayerPrefab and EnemyPrefab in the Inspector.
// Only runs on the HOST — clients receive spawned objects via state sync.
public class BasicSpawner : MonoBehaviour
{
    [Header("Prefabs (must have NetworkObject component)")]
    public NetworkObject PlayerPrefab; // Drag your Player prefab here
    public NetworkObject EnemyPrefab;  // Drag your Enemy prefab here

    [Header("Spawn Settings")]
    public int EnemyCount = 3;         // How many enemies to spawn at startup
    public float SpawnRadius = 8f;     // Random radius around origin for enemies

    // Track which NetworkObject belongs to which player so we can despawn on disconnect
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new();

    // Called by NetworkRunnerManager when a player connects
    public void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        // Pick a random spawn point in a small area
        Vector3 spawnPos = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

        // Runner.Spawn is how you create networked objects.
        // inputAuthority: player means this player owns and sends input for this object.
        NetworkObject playerObj = runner.Spawn(
            PlayerPrefab,
            spawnPos,
            Quaternion.identity,
            inputAuthority: player
        );

        _spawnedPlayers[player] = playerObj;

        // Spawn enemies once (when the first player joins)
        if (_spawnedPlayers.Count == 1)
            SpawnEnemies(runner);
    }

    // Called when a player disconnects — clean up their object
    public void DespawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
        }
    }

    private void SpawnEnemies(NetworkRunner runner)
    {
        for (int i = 0; i < EnemyCount; i++)
        {
            // Spread enemies in a circle around the origin
            float angle = i * (360f / EnemyCount) * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(
                Mathf.Cos(angle) * SpawnRadius,
                0,
                Mathf.Sin(angle) * SpawnRadius
            );

            // No inputAuthority for enemies — the host controls them
            runner.Spawn(EnemyPrefab, pos, Quaternion.identity);
        }
    }
}