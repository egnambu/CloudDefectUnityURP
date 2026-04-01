using System;
using System.Collections.Generic;
using UnityEngine;

namespace SavingSystem
{
    using Core;
    using Serialization;

    /// <summary>
    /// Main controller for the chunk-based delta world saving system.
    /// Attach this to the player or camera (the center of the active world).
    /// </summary>
    public class WorldSavingSystem : MonoBehaviour
    {
        #region Singleton

        private static WorldSavingSystem _instance;

        /// <summary>
        /// Singleton instance. Use with caution - prefer dependency injection where possible.
        /// </summary>
        public static WorldSavingSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<WorldSavingSystem>();
                }
                return _instance;
            }
        }

        #endregion

        #region Inspector Configuration

        [Header("Chunk Configuration")]
        [SerializeField]
        [Tooltip("Size of each chunk in world units (X, Y, Z).")]
        private Vector3 _chunkSize = new Vector3(50f, 50f, 50f);

        [SerializeField]
        [Tooltip("Number of chunks to keep active around the center (half-extents in each direction).")]
        private Vector3Int _activeChunkHalfExtents = new Vector3Int(2, 1, 2);

        [SerializeField]
        [Tooltip("Number of chunks to load ahead of movement (buffer zone).")]
        private Vector3Int _loadBufferHalfExtents = new Vector3Int(3, 1, 3);

        [Header("Save Configuration")]
        [SerializeField]
        [Tooltip("Base folder name for saves (relative to Application.persistentDataPath).")]
        private string _saveFolder = "WorldSaves";

        [SerializeField]
        [Tooltip("Current save slot name.")]
        private string _currentSaveName = "Save1";

        [SerializeField]
        [Tooltip("Auto-save interval in seconds (0 = disabled).")]
        private float _autoSaveInterval = 300f;

        [SerializeField]
        [Tooltip("Use delta saves for auto-save (more efficient).")]
        private bool _useDeltaAutoSave = true;

        [Header("Performance")]
        [SerializeField]
        [Tooltip("Maximum entities to process per frame during load.")]
        private int _maxEntitiesPerFrame = 10;

        [SerializeField]
        [Tooltip("Minimum time between chunk update checks (seconds).")]
        private float _chunkUpdateInterval = 0.5f;

        [Header("Debug")]
        [SerializeField]
        private bool _showDebugGizmos = true;

        [SerializeField]
        private bool _logChunkChanges = false;

        #endregion

        #region Private Fields

        private WorldIndexManager _worldIndex;
        private SaveLoadPipeline _saveLoadPipeline;
        private SaveSerializer _serializer;

        private ChunkID _currentCenterChunk;
        private HashSet<ChunkID> _loadedChunks;
        private HashSet<ChunkID> _chunksToLoad;
        private HashSet<ChunkID> _chunksToUnload;

        private float _lastChunkUpdateTime;
        private float _lastAutoSaveTime;
        private float _playTime;

        // Entity factory for instantiation
        private EntityFactory _entityFactory;

        // Track which entities came from save data vs runtime spawned
        private HashSet<EntityID> _loadedEntityIds;

        // Destroyed entities from base world (loaded from save)
        private HashSet<ulong> _destroyedBaseEntityIds;

        #endregion

        #region Properties

        /// <summary>
        /// Current chunk size configuration.
        /// </summary>
        public Vector3 ChunkSize => _chunkSize;

        /// <summary>
        /// Active chunk half-extents.
        /// </summary>
        public Vector3Int ActiveChunkHalfExtents => _activeChunkHalfExtents;

        /// <summary>
        /// Current center chunk (where this object is located).
        /// </summary>
        public ChunkID CurrentCenterChunk => _currentCenterChunk;

        /// <summary>
        /// World index manager for spatial queries.
        /// </summary>
        public WorldIndexManager WorldIndex => _worldIndex;

        /// <summary>
        /// Current save name/slot.
        /// </summary>
        public string CurrentSaveName
        {
            get => _currentSaveName;
            set => _currentSaveName = value;
        }

        /// <summary>
        /// Total play time in seconds.
        /// </summary>
        public float PlayTime => _playTime;

        /// <summary>
        /// Whether there are unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges => _worldIndex?.HasDirtyData ?? false;

        #endregion

        #region Events

        /// <summary>
        /// Fired when a chunk is loaded.
        /// </summary>
        public event Action<ChunkID> OnChunkLoaded;

        /// <summary>
        /// Fired when a chunk is unloaded.
        /// </summary>
        public event Action<ChunkID> OnChunkUnloaded;

        /// <summary>
        /// Fired before saving begins.
        /// </summary>
        public event Action OnSaveStarted;

        /// <summary>
        /// Fired after saving completes.
        /// </summary>
        public event Action OnSaveCompleted;

        /// <summary>
        /// Fired before loading begins.
        /// </summary>
        public event Action OnLoadStarted;

        /// <summary>
        /// Fired after loading completes.
        /// </summary>
        public event Action OnLoadCompleted;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Singleton setup
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[WorldSavingSystem] Duplicate instance found, destroying...");
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Initialize systems
            InitializeSystems();
        }

        private void Start()
        {
            // Calculate initial center chunk
            _currentCenterChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);

            // Initialize loaded chunks around center
            InitializeLoadedChunks();
        }

        private void Update()
        {
            // Track play time
            _playTime += Time.deltaTime;

            // Check for chunk updates
            if (Time.time - _lastChunkUpdateTime >= _chunkUpdateInterval)
            {
                _lastChunkUpdateTime = Time.time;
                UpdateActiveChunks();
            }

            // Auto-save
            if (_autoSaveInterval > 0 && Time.time - _lastAutoSaveTime >= _autoSaveInterval)
            {
                _lastAutoSaveTime = Time.time;
                AutoSave();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion

        #region Initialization

        private void InitializeSystems()
        {
            // Create world index
            _worldIndex = new WorldIndexManager(_chunkSize);

            // Create serializer
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, _saveFolder);
            _serializer = new SaveSerializer(savePath);

            // Create save/load pipeline
            _saveLoadPipeline = new SaveLoadPipeline(_serializer, _worldIndex);

            // Initialize collections
            _loadedChunks = new HashSet<ChunkID>();
            _chunksToLoad = new HashSet<ChunkID>();
            _chunksToUnload = new HashSet<ChunkID>();
            _loadedEntityIds = new HashSet<EntityID>();
            _destroyedBaseEntityIds = new HashSet<ulong>();

            // Create entity factory
            _entityFactory = new EntityFactory();

            // Subscribe to world index events
            _worldIndex.OnEntityChangedChunk += HandleEntityChangedChunk;

            Debug.Log($"[WorldSavingSystem] Initialized with chunk size {_chunkSize}");
        }

        private void InitializeLoadedChunks()
        {
            // Mark all chunks in range as needing load
            ChunkMathUtility.GetChunksInRange(_currentCenterChunk, _loadBufferHalfExtents, chunk =>
            {
                _loadedChunks.Add(chunk);
            });

            if (_logChunkChanges)
            {
                Debug.Log($"[WorldSavingSystem] Initialized {_loadedChunks.Count} chunks around {_currentCenterChunk}");
            }
        }

        #endregion

        #region Chunk Management

        /// <summary>
        /// Updates active chunks based on current position.
        /// Uses pure math, no physics.
        /// </summary>
        private void UpdateActiveChunks()
        {
            ChunkID newCenterChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);

            // Only process if center chunk changed
            if (newCenterChunk == _currentCenterChunk)
                return;

            ChunkID oldCenterChunk = _currentCenterChunk;
            _currentCenterChunk = newCenterChunk;

            if (_logChunkChanges)
            {
                Debug.Log($"[WorldSavingSystem] Center chunk changed: {oldCenterChunk} -> {newCenterChunk}");
            }

            // Determine chunks that should be loaded
            _chunksToLoad.Clear();
            ChunkMathUtility.GetChunksInRange(newCenterChunk, _loadBufferHalfExtents, chunk =>
            {
                if (!_loadedChunks.Contains(chunk))
                {
                    _chunksToLoad.Add(chunk);
                }
            });

            // Determine chunks that should be unloaded
            _chunksToUnload.Clear();
            foreach (ChunkID loadedChunk in _loadedChunks)
            {
                if (!loadedChunk.IsWithinRange(newCenterChunk, _loadBufferHalfExtents))
                {
                    _chunksToUnload.Add(loadedChunk);
                }
            }

            // Process unloads first (free memory)
            foreach (ChunkID chunk in _chunksToUnload)
            {
                UnloadChunk(chunk);
            }

            // Process loads
            foreach (ChunkID chunk in _chunksToLoad)
            {
                LoadChunk(chunk);
            }
        }

        /// <summary>
        /// Loads a chunk and its entities from save data.
        /// </summary>
        private void LoadChunk(ChunkID chunkId)
        {
            if (_loadedChunks.Contains(chunkId))
                return;

            _loadedChunks.Add(chunkId);

            // Try to load saved data for this chunk
            if (!string.IsNullOrEmpty(_currentSaveName))
            {
                ChunkSaveData chunkData = _saveLoadPipeline.LoadChunk(_currentSaveName, chunkId);
                if (chunkData != null)
                {
                    InstantiateChunkEntities(chunkData);
                }
            }

            OnChunkLoaded?.Invoke(chunkId);

            if (_logChunkChanges)
            {
                Debug.Log($"[WorldSavingSystem] Loaded chunk {chunkId}");
            }
        }

        /// <summary>
        /// Unloads a chunk and cleans up its entities.
        /// </summary>
        private void UnloadChunk(ChunkID chunkId)
        {
            if (!_loadedChunks.Contains(chunkId))
                return;

            // Save dirty entities before unloading
            if (_worldIndex.HasEntitiesInChunk(chunkId))
            {
                // Mark chunk for save if any entities are dirty
                bool hasDirtyEntities = false;
                foreach (EntityID entityId in _worldIndex.GetDirtyEntitiesInChunk(chunkId))
                {
                    hasDirtyEntities = true;
                    break;
                }

                if (hasDirtyEntities && !string.IsNullOrEmpty(_currentSaveName))
                {
                    // Save this chunk's data before unloading
                    ChunkSaveData chunkData = CaptureChunkState(chunkId);
                    _serializer.SaveChunk(chunkData, _currentSaveName);
                }

                // Destroy loaded entities in this chunk
                List<IPersistentEntity> entitiesToDestroy = new List<IPersistentEntity>();
                foreach (IPersistentEntity entity in _worldIndex.GetEntityObjectsInChunk(chunkId))
                {
                    // Only destroy entities that were loaded from save
                    if (_loadedEntityIds.Contains(entity.EntityID))
                    {
                        entitiesToDestroy.Add(entity);
                    }
                }

                foreach (IPersistentEntity entity in entitiesToDestroy)
                {
                    _loadedEntityIds.Remove(entity.EntityID);
                    _worldIndex.UnregisterEntity(entity.EntityID);

                    if (entity is MonoBehaviour mb)
                    {
                        Destroy(mb.gameObject);
                    }
                }
            }

            _loadedChunks.Remove(chunkId);
            OnChunkUnloaded?.Invoke(chunkId);

            if (_logChunkChanges)
            {
                Debug.Log($"[WorldSavingSystem] Unloaded chunk {chunkId}");
            }
        }

        /// <summary>
        /// Captures the current state of all entities in a chunk.
        /// </summary>
        private ChunkSaveData CaptureChunkState(ChunkID chunkId)
        {
            ChunkSaveData chunkData = new ChunkSaveData(chunkId);

            foreach (IPersistentEntity entity in _worldIndex.GetEntityObjectsInChunk(chunkId))
            {
                EntityStateData state = entity.CaptureState();
                chunkData.AddEntityState(state);
            }

            return chunkData;
        }

        /// <summary>
        /// Instantiates entities from loaded chunk data.
        /// </summary>
        private void InstantiateChunkEntities(ChunkSaveData chunkData)
        {
            foreach (EntityStateData state in chunkData.GetAllEntityStates())
            {
                // Skip destroyed entities
                if (state.IsDestroyed)
                    continue;

                // Skip if already in world
                EntityID entityId = new EntityID(state.EntityIDValue);
                if (_worldIndex.HasEntity(entityId))
                    continue;

                // Check if this entity was destroyed from base world
                if (_destroyedBaseEntityIds.Contains(state.EntityIDValue))
                    continue;

                // Instantiate via factory
                IPersistentEntity entity = _entityFactory.CreateEntity(state);
                if (entity != null)
                {
                    _loadedEntityIds.Add(entityId);
                }
            }

            // Track destroyed entities from this chunk
            foreach (ulong destroyedId in chunkData.DestroyedEntityIDs)
            {
                _destroyedBaseEntityIds.Add(destroyedId);
            }
        }

        #endregion

        #region Entity Management

        /// <summary>
        /// Registers an entity with the saving system.
        /// Called automatically by PersistentEntityBase.
        /// </summary>
        public void RegisterEntity(IPersistentEntity entity)
        {
            if (entity == null)
                return;

            // Set chunk size on entity
            if (entity is PersistentEntityBase persistentBase)
            {
                persistentBase.SetChunkSize(_chunkSize);
            }

            _worldIndex.RegisterEntity(entity);
        }

        /// <summary>
        /// Called when an entity is destroyed.
        /// </summary>
        public void OnEntityDestroyed(IPersistentEntity entity)
        {
            if (entity == null)
                return;

            // If this was a base world entity, track its destruction
            if (!_loadedEntityIds.Contains(entity.EntityID))
            {
                _worldIndex.MarkEntityDestroyed(entity.EntityID);
            }
            else
            {
                _worldIndex.UnregisterEntity(entity.EntityID);
                _loadedEntityIds.Remove(entity.EntityID);
            }
        }

        /// <summary>
        /// Checks if an entity from the base world should be suppressed (was destroyed in save).
        /// </summary>
        public bool IsEntityDestroyed(EntityID entityId)
        {
            return _destroyedBaseEntityIds.Contains(entityId.Value);
        }

        /// <summary>
        /// Checks if a base world entity should be suppressed by ID value.
        /// </summary>
        public bool IsEntityDestroyed(ulong entityIdValue)
        {
            return _destroyedBaseEntityIds.Contains(entityIdValue);
        }

        private void HandleEntityChangedChunk(EntityID entityId, ChunkID oldChunk, ChunkID newChunk)
        {
            if (_logChunkChanges)
            {
                Debug.Log($"[WorldSavingSystem] Entity {entityId} moved from {oldChunk} to {newChunk}");
            }
        }

        #endregion

        #region Save/Load Public API

        /// <summary>
        /// Performs a full save of all entities.
        /// </summary>
        public void SaveGame()
        {
            SaveGame(_currentSaveName);
        }

        /// <summary>
        /// Performs a full save to a specific slot.
        /// </summary>
        public void SaveGame(string saveName)
        {
            OnSaveStarted?.Invoke();

            _saveLoadPipeline.SaveWorld(saveName, transform.position, _playTime);
            _currentSaveName = saveName;

            OnSaveCompleted?.Invoke();

            Debug.Log($"[WorldSavingSystem] Game saved to '{saveName}'");
        }

        /// <summary>
        /// Performs a delta save (only dirty data).
        /// </summary>
        public void SaveDelta()
        {
            SaveDelta(_currentSaveName);
        }

        /// <summary>
        /// Performs a delta save to a specific slot.
        /// </summary>
        public void SaveDelta(string saveName)
        {
            if (!_worldIndex.HasDirtyData)
            {
                Debug.Log("[WorldSavingSystem] No changes to save");
                return;
            }

            OnSaveStarted?.Invoke();

            _saveLoadPipeline.SaveDelta(saveName, transform.position, _playTime);
            _currentSaveName = saveName;

            OnSaveCompleted?.Invoke();

            Debug.Log($"[WorldSavingSystem] Delta saved to '{saveName}'");
        }

        /// <summary>
        /// Loads a game from a save slot.
        /// </summary>
        public void LoadGame(string saveName)
        {
            OnLoadStarted?.Invoke();

            // Clear current state
            ClearLoadedEntities();

            _currentSaveName = saveName;

            // Load world metadata
            WorldSaveData worldData = _saveLoadPipeline.LoadWorldMetadata(saveName);
            if (worldData == null)
            {
                Debug.LogWarning($"[WorldSavingSystem] Save '{saveName}' not found");
                OnLoadCompleted?.Invoke();
                return;
            }

            // Restore play time
            _playTime = worldData.PlayTime;

            // Load destroyed entity list
            _destroyedBaseEntityIds = _saveLoadPipeline.GetDestroyedEntityIds(saveName);

            // Update chunk size if different (migration support)
            if (worldData.ChunkSize != _chunkSize)
            {
                Debug.LogWarning($"[WorldSavingSystem] Save uses different chunk size: {worldData.ChunkSize}. " +
                                 $"Current: {_chunkSize}. Using save's chunk size.");
                _chunkSize = worldData.ChunkSize;
                _worldIndex.ChunkSize = _chunkSize;
            }

            // Load chunks around current position
            _currentCenterChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);
            _loadedChunks.Clear();

            Dictionary<ChunkID, ChunkSaveData> loadedChunkData = _saveLoadPipeline.LoadChunksInRange(
                saveName, _currentCenterChunk, _loadBufferHalfExtents);

            foreach (var kvp in loadedChunkData)
            {
                _loadedChunks.Add(kvp.Key);
                InstantiateChunkEntities(kvp.Value);
            }

            // Also add chunks with no save data to loaded set
            ChunkMathUtility.GetChunksInRange(_currentCenterChunk, _loadBufferHalfExtents, chunk =>
            {
                _loadedChunks.Add(chunk);
            });

            OnLoadCompleted?.Invoke();

            Debug.Log($"[WorldSavingSystem] Game loaded from '{saveName}': {loadedChunkData.Count} chunks, " +
                      $"{_loadedEntityIds.Count} entities");
        }

        /// <summary>
        /// Starts a new game (clears save state).
        /// </summary>
        public void NewGame(string saveName)
        {
            ClearLoadedEntities();

            _currentSaveName = saveName;
            _playTime = 0f;
            _destroyedBaseEntityIds.Clear();

            // Initialize fresh chunks
            _currentCenterChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);
            _loadedChunks.Clear();
            InitializeLoadedChunks();

            Debug.Log($"[WorldSavingSystem] New game started: '{saveName}'");
        }

        /// <summary>
        /// Checks if a save exists.
        /// </summary>
        public bool SaveExists(string saveName)
        {
            return _saveLoadPipeline.SaveExists(saveName);
        }

        /// <summary>
        /// Deletes a save.
        /// </summary>
        public void DeleteSave(string saveName)
        {
            _saveLoadPipeline.DeleteSave(saveName);
            Debug.Log($"[WorldSavingSystem] Save '{saveName}' deleted");
        }

        /// <summary>
        /// Gets all available save names.
        /// </summary>
        public string[] GetAvailableSaves()
        {
            return _saveLoadPipeline.GetAvailableSaves();
        }

        private void AutoSave()
        {
            if (string.IsNullOrEmpty(_currentSaveName))
                return;

            if (_useDeltaAutoSave)
            {
                SaveDelta();
            }
            else
            {
                SaveGame();
            }
        }

        private void ClearLoadedEntities()
        {
            // Destroy all loaded entities
            foreach (EntityID entityId in _loadedEntityIds)
            {
                IPersistentEntity entity = _worldIndex.GetEntity(entityId);
                if (entity != null)
                {
                    _worldIndex.UnregisterEntity(entityId);

                    if (entity is MonoBehaviour mb)
                    {
                        Destroy(mb.gameObject);
                    }
                }
            }

            _loadedEntityIds.Clear();
            _loadedChunks.Clear();
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Updates chunk size at runtime.
        /// Warning: This will require re-indexing all entities.
        /// </summary>
        public void SetChunkSize(Vector3 newChunkSize)
        {
            if (newChunkSize == _chunkSize)
                return;

            _chunkSize = newChunkSize;
            _worldIndex.ChunkSize = newChunkSize;

            // Force re-calculation of all entity chunks
            // Note: This is expensive, avoid during gameplay
            Debug.LogWarning("[WorldSavingSystem] Chunk size changed - entities will be re-indexed");
        }

        /// <summary>
        /// Updates active chunk extents.
        /// </summary>
        public void SetActiveChunkExtents(Vector3Int halfExtents)
        {
            _activeChunkHalfExtents = halfExtents;
        }

        /// <summary>
        /// Updates load buffer extents.
        /// </summary>
        public void SetLoadBufferExtents(Vector3Int halfExtents)
        {
            _loadBufferHalfExtents = halfExtents;
        }

        #endregion

        #region Debug

        private void OnDrawGizmos()
        {
            if (!_showDebugGizmos)
                return;

            // Draw current chunk
            Gizmos.color = Color.yellow;
            ChunkID centerChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);
            DrawChunkGizmo(centerChunk);

            // Draw active chunk bounds
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            ChunkMathUtility.GetChunksInRange(centerChunk, _activeChunkHalfExtents, chunk =>
            {
                if (chunk != centerChunk)
                {
                    DrawChunkGizmo(chunk);
                }
            });

            // Draw load buffer bounds
            Gizmos.color = new Color(0f, 0f, 1f, 0.1f);
            ChunkMathUtility.GetChunksInRange(centerChunk, _loadBufferHalfExtents, chunk =>
            {
                if (!chunk.IsWithinRange(centerChunk, _activeChunkHalfExtents))
                {
                    DrawChunkGizmo(chunk);
                }
            });
        }

        private void DrawChunkGizmo(ChunkID chunk)
        {
            Vector3 center = chunk.GetWorldCenter(_chunkSize);
            Gizmos.DrawWireCube(center, _chunkSize);
        }

        /// <summary>
        /// Gets debug statistics.
        /// </summary>
        public string GetDebugStats()
        {
            if (_worldIndex == null)
                return "System not initialized";

            WorldIndexStats stats = _worldIndex.GetStats();
            return $"Entities: {stats.TotalEntities} | " +
                   $"Active Chunks: {stats.ActiveChunks} | " +
                   $"Loaded Chunks: {_loadedChunks?.Count ?? 0} | " +
                   $"Dirty: {stats.DirtyEntities} entities, {stats.DirtyChunks} chunks | " +
                   $"Center: {_currentCenterChunk}";
        }

        #endregion
    }
}
