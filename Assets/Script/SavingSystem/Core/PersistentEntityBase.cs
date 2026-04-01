using System;
using UnityEngine;

namespace SavingSystem.Core
{
    /// <summary>
    /// Base class for persistent entities in the world.
    /// Handles chunk tracking, dirty state, and common functionality.
    /// Derive from this for specific entity types (NPCs, Items, etc.)
    /// </summary>
    public abstract class PersistentEntityBase : MonoBehaviour, IPersistentEntity
    {
        #region Serialized Fields

        [Header("Entity Identity")]
        [SerializeField]
        [Tooltip("Unique identifier for this entity. Generated automatically if not set.")]
        private string _entityGuid = "";

        [SerializeField]
        [Tooltip("Prefab identifier for instantiation on load.")]
        private string _prefabID = "";

        #endregion

        #region Private Fields

        private EntityID _entityId;
        private ChunkID _currentChunk;
        private Vector3 _lastTrackedPosition;
        private bool _isDirty;
        private bool _isInitialized;
        private Vector3 _chunkSize;

        #endregion

        #region Events

        public event Action<IPersistentEntity, ChunkID, ChunkID> OnChunkChanged;
        public event Action<IPersistentEntity> OnBecameDirty;

        #endregion

        #region IPersistentEntity Implementation

        public EntityID EntityID
        {
            get
            {
                EnsureEntityId();
                return _entityId;
            }
        }

        public string PrefabID
        {
            get => _prefabID;
            protected set => _prefabID = value;
        }

        public Vector3 WorldPosition => transform.position;

        public ChunkID CurrentChunk => _currentChunk;

        public bool IsDirty => _isDirty;

        public void MarkDirty()
        {
            if (!_isDirty)
            {
                _isDirty = true;
                OnBecameDirty?.Invoke(this);
            }
        }

        public void ClearDirty()
        {
            _isDirty = false;
        }

        public abstract EntityStateData CaptureState();
        public abstract void RestoreState(EntityStateData state);

        #endregion

        #region Unity Lifecycle

        protected virtual void Awake()
        {
            EnsureEntityId();
        }

        protected virtual void Start()
        {
            // Deferred initialization to allow WorldSavingSystem to be ready
            if (!_isInitialized)
            {
                Initialize();
            }
        }

        protected virtual void OnDestroy()
        {
            // Notify the saving system this entity is being destroyed
            WorldSavingSystem instance = WorldSavingSystem.Instance;
            if (instance != null)
            {
                instance.OnEntityDestroyed(this);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Ensures the entity has a valid ID.
        /// </summary>
        private void EnsureEntityId()
        {
            if (_entityId.IsValid)
                return;

            if (!string.IsNullOrEmpty(_entityGuid))
            {
                // Try to parse existing GUID
                if (Guid.TryParse(_entityGuid, out Guid guid))
                {
                    _entityId = EntityID.FromGuid(guid);
                }
                else
                {
                    // Treat as hash seed
                    _entityId = new EntityID((ulong)_entityGuid.GetHashCode());
                }
            }
            else
            {
                // Generate new GUID
                Guid newGuid = Guid.NewGuid();
                _entityGuid = newGuid.ToString();
                _entityId = EntityID.FromGuid(newGuid);
            }
        }

        /// <summary>
        /// Initializes the entity with the world saving system.
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;

            WorldSavingSystem instance = WorldSavingSystem.Instance;
            if (instance == null)
            {
                Debug.LogWarning($"[PersistentEntity] WorldSavingSystem not found, entity {EntityID} cannot initialize");
                return;
            }

            _chunkSize = instance.ChunkSize;
            _lastTrackedPosition = transform.position;
            _currentChunk = ChunkID.FromWorldPosition(_lastTrackedPosition, _chunkSize);

            // Register with the saving system
            instance.RegisterEntity(this);

            _isInitialized = true;
        }

        /// <summary>
        /// Sets the chunk size (called by WorldSavingSystem on registration).
        /// </summary>
        public void SetChunkSize(Vector3 chunkSize)
        {
            _chunkSize = chunkSize;

            // Recalculate current chunk
            ChunkID newChunk = ChunkID.FromWorldPosition(transform.position, _chunkSize);
            if (_isInitialized && newChunk != _currentChunk)
            {
                ChunkID oldChunk = _currentChunk;
                _currentChunk = newChunk;
                OnChunkChanged?.Invoke(this, oldChunk, newChunk);
            }
            else
            {
                _currentChunk = newChunk;
            }
        }

        #endregion

        #region Position Tracking

        /// <summary>
        /// Call this when the entity moves to update chunk tracking.
        /// Can be called every frame or on significant movement.
        /// </summary>
        public void UpdatePositionTracking()
        {
            if (!_isInitialized || _chunkSize == Vector3.zero)
                return;

            Vector3 currentPosition = transform.position;

            // Check if we've moved enough to potentially change chunks
            // Use a small threshold to avoid unnecessary chunk calculations
            Vector3 delta = currentPosition - _lastTrackedPosition;
            float sqrDistance = delta.sqrMagnitude;

            // Only recompute chunk if moved significantly (1/4 of smallest chunk dimension)
            float threshold = Mathf.Min(_chunkSize.x, _chunkSize.y, _chunkSize.z) * 0.25f;

            if (sqrDistance > threshold * threshold)
            {
                _lastTrackedPosition = currentPosition;
                ChunkID newChunk = ChunkID.FromWorldPosition(currentPosition, _chunkSize);

                if (newChunk != _currentChunk)
                {
                    ChunkID oldChunk = _currentChunk;
                    _currentChunk = newChunk;
                    OnChunkChanged?.Invoke(this, oldChunk, newChunk);
                    MarkDirty(); // Position change = dirty
                }
            }
        }

        /// <summary>
        /// Forces an immediate chunk update regardless of movement threshold.
        /// </summary>
        public void ForceChunkUpdate()
        {
            if (_chunkSize == Vector3.zero)
                return;

            _lastTrackedPosition = transform.position;
            ChunkID newChunk = ChunkID.FromWorldPosition(_lastTrackedPosition, _chunkSize);

            if (newChunk != _currentChunk)
            {
                ChunkID oldChunk = _currentChunk;
                _currentChunk = newChunk;
                OnChunkChanged?.Invoke(this, oldChunk, newChunk);
            }
        }

        #endregion

        #region State Helpers

        /// <summary>
        /// Populates common state fields in the provided data object.
        /// </summary>
        protected void PopulateBaseState(EntityStateData data)
        {
            data.EntityID = EntityID;
            data.PrefabID = _prefabID;
            data.Position = transform.position;
            data.Rotation = transform.eulerAngles;
            data.SaveTimestamp = DateTime.UtcNow.Ticks;
            data.IsDestroyed = false;
        }

        /// <summary>
        /// Restores common state fields from the provided data object.
        /// </summary>
        protected void RestoreBaseState(EntityStateData data)
        {
            transform.position = data.Position;
            transform.eulerAngles = data.Rotation;
            ForceChunkUpdate();
        }

        /// <summary>
        /// Sets the entity ID (used when loading from save).
        /// </summary>
        public void SetEntityId(EntityID id)
        {
            _entityId = id;
            _entityGuid = id.Value.ToString("X16");
        }

        #endregion

        #region Editor

#if UNITY_EDITOR
        /// <summary>
        /// Generates a new GUID for this entity in the editor.
        /// </summary>
        [ContextMenu("Generate New Entity ID")]
        private void EditorGenerateNewId()
        {
            Guid newGuid = Guid.NewGuid();
            _entityGuid = newGuid.ToString();
            _entityId = EntityID.FromGuid(newGuid);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        /// <summary>
        /// Validates the entity setup in the editor.
        /// </summary>
        protected virtual void OnValidate()
        {
            if (string.IsNullOrEmpty(_prefabID))
            {
                _prefabID = gameObject.name;
            }
        }
#endif

        #endregion
    }
}
