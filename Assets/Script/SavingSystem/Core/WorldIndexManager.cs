using System;
using System.Collections.Generic;
using UnityEngine;

namespace SavingSystem.Core
{
    /// <summary>
    /// Manages the spatial index of entities organized by chunks.
    /// Provides O(1) lookup for entities by chunk and tracks dirty entities for saving.
    /// No Unity physics or colliders - purely mathematical spatial partitioning.
    /// </summary>
    public class WorldIndexManager
    {
        #region Private Fields

        // Primary spatial index: ChunkID -> Set of EntityIDs in that chunk
        private readonly Dictionary<ChunkID, HashSet<EntityID>> _chunkToEntities;

        // Reverse lookup: EntityID -> ChunkID for quick chunk queries
        private readonly Dictionary<EntityID, ChunkID> _entityToChunk;

        // Entity registry: EntityID -> IPersistentEntity reference
        private readonly Dictionary<EntityID, IPersistentEntity> _entityRegistry;

        // Dirty tracking: Set of entities with unsaved changes
        private readonly HashSet<EntityID> _dirtyEntities;

        // Dirty chunks: Chunks containing dirty entities (for optimized save)
        private readonly HashSet<ChunkID> _dirtyChunks;

        // Destroyed entities: Track destroyed entities separately for delta saves
        private readonly HashSet<EntityID> _destroyedEntities;

        // Configuration
        private Vector3 _chunkSize;

        // Statistics
        private int _totalEntityCount;
        private int _activeChunkCount;

        #endregion

        #region Events

        /// <summary>
        /// Fired when an entity is registered.
        /// </summary>
        public event Action<EntityID, ChunkID> OnEntityRegistered;

        /// <summary>
        /// Fired when an entity is unregistered.
        /// </summary>
        public event Action<EntityID, ChunkID> OnEntityUnregistered;

        /// <summary>
        /// Fired when an entity moves to a new chunk.
        /// </summary>
        public event Action<EntityID, ChunkID, ChunkID> OnEntityChangedChunk;

        /// <summary>
        /// Fired when an entity becomes dirty.
        /// </summary>
        public event Action<EntityID> OnEntityDirty;

        /// <summary>
        /// Fired when a chunk becomes dirty (has dirty entities).
        /// </summary>
        public event Action<ChunkID> OnChunkDirty;

        #endregion

        #region Properties

        /// <summary>
        /// Current chunk size configuration.
        /// </summary>
        public Vector3 ChunkSize
        {
            get => _chunkSize;
            set => _chunkSize = value;
        }

        /// <summary>
        /// Total number of registered entities.
        /// </summary>
        public int TotalEntityCount => _totalEntityCount;

        /// <summary>
        /// Number of chunks with at least one entity.
        /// </summary>
        public int ActiveChunkCount => _activeChunkCount;

        /// <summary>
        /// Number of entities with unsaved changes.
        /// </summary>
        public int DirtyEntityCount => _dirtyEntities.Count;

        /// <summary>
        /// Number of chunks with dirty entities.
        /// </summary>
        public int DirtyChunkCount => _dirtyChunks.Count;

        /// <summary>
        /// Whether there are any unsaved changes.
        /// </summary>
        public bool HasDirtyData => _dirtyEntities.Count > 0 || _destroyedEntities.Count > 0;

        #endregion

        #region Constructor

        public WorldIndexManager(Vector3 chunkSize)
        {
            _chunkSize = chunkSize;
            _chunkToEntities = new Dictionary<ChunkID, HashSet<EntityID>>();
            _entityToChunk = new Dictionary<EntityID, ChunkID>();
            _entityRegistry = new Dictionary<EntityID, IPersistentEntity>();
            _dirtyEntities = new HashSet<EntityID>();
            _dirtyChunks = new HashSet<ChunkID>();
            _destroyedEntities = new HashSet<EntityID>();
            _totalEntityCount = 0;
            _activeChunkCount = 0;
        }

        #endregion

        #region Entity Registration

        /// <summary>
        /// Registers an entity with the world index.
        /// Automatically computes and assigns the chunk based on position.
        /// </summary>
        public void RegisterEntity(IPersistentEntity entity)
        {
            if (entity == null)
            {
                Debug.LogError("[WorldIndexManager] Cannot register null entity");
                return;
            }

            EntityID entityId = entity.EntityID;

            if (!entityId.IsValid)
            {
                Debug.LogError("[WorldIndexManager] Cannot register entity with invalid ID");
                return;
            }

            if (_entityRegistry.ContainsKey(entityId))
            {
                Debug.LogWarning($"[WorldIndexManager] Entity {entityId} already registered, updating...");
                UnregisterEntity(entityId);
            }

            // Compute chunk from position
            ChunkID chunk = ChunkID.FromWorldPosition(entity.WorldPosition, _chunkSize);

            // Add to registry
            _entityRegistry[entityId] = entity;
            _entityToChunk[entityId] = chunk;

            // Add to spatial index
            if (!_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> chunkEntities))
            {
                chunkEntities = new HashSet<EntityID>();
                _chunkToEntities[chunk] = chunkEntities;
                _activeChunkCount++;
            }
            chunkEntities.Add(entityId);

            _totalEntityCount++;

            // Subscribe to entity events
            entity.OnChunkChanged += HandleEntityChunkChanged;
            entity.OnBecameDirty += HandleEntityBecameDirty;

            // If entity is already dirty, track it
            if (entity.IsDirty)
            {
                MarkEntityDirty(entityId);
            }

            OnEntityRegistered?.Invoke(entityId, chunk);
        }

        /// <summary>
        /// Unregisters an entity from the world index.
        /// </summary>
        public void UnregisterEntity(EntityID entityId)
        {
            if (!_entityRegistry.TryGetValue(entityId, out IPersistentEntity entity))
            {
                return;
            }

            // Unsubscribe from events
            entity.OnChunkChanged -= HandleEntityChunkChanged;
            entity.OnBecameDirty -= HandleEntityBecameDirty;

            // Get current chunk
            if (_entityToChunk.TryGetValue(entityId, out ChunkID chunk))
            {
                // Remove from spatial index
                if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> chunkEntities))
                {
                    chunkEntities.Remove(entityId);

                    // Clean up empty chunk
                    if (chunkEntities.Count == 0)
                    {
                        _chunkToEntities.Remove(chunk);
                        _activeChunkCount--;
                    }
                }

                _entityToChunk.Remove(entityId);
            }

            // Remove from dirty tracking
            _dirtyEntities.Remove(entityId);
            UpdateDirtyChunks();

            // Remove from registry
            _entityRegistry.Remove(entityId);
            _totalEntityCount--;

            OnEntityUnregistered?.Invoke(entityId, chunk);
        }

        /// <summary>
        /// Marks an entity as destroyed (for delta saving).
        /// The entity is unregistered but tracked as destroyed.
        /// </summary>
        public void MarkEntityDestroyed(EntityID entityId)
        {
            if (_entityRegistry.ContainsKey(entityId))
            {
                _destroyedEntities.Add(entityId);
                UnregisterEntity(entityId);
            }
        }

        #endregion

        #region Chunk Movement

        /// <summary>
        /// Updates an entity's chunk assignment based on new position.
        /// Called by entities when they move.
        /// </summary>
        public void UpdateEntityPosition(EntityID entityId, Vector3 newPosition)
        {
            if (!_entityToChunk.TryGetValue(entityId, out ChunkID currentChunk))
            {
                Debug.LogWarning($"[WorldIndexManager] Entity {entityId} not found in index");
                return;
            }

            // Compute new chunk
            ChunkID newChunk = ChunkID.FromWorldPosition(newPosition, _chunkSize);

            // Only update if chunk changed
            if (currentChunk != newChunk)
            {
                MoveEntityToChunk(entityId, currentChunk, newChunk);
            }
        }

        /// <summary>
        /// Moves an entity from one chunk to another.
        /// </summary>
        private void MoveEntityToChunk(EntityID entityId, ChunkID fromChunk, ChunkID toChunk)
        {
            // Remove from old chunk
            if (_chunkToEntities.TryGetValue(fromChunk, out HashSet<EntityID> oldChunkEntities))
            {
                oldChunkEntities.Remove(entityId);

                if (oldChunkEntities.Count == 0)
                {
                    _chunkToEntities.Remove(fromChunk);
                    _activeChunkCount--;
                }
            }

            // Add to new chunk
            if (!_chunkToEntities.TryGetValue(toChunk, out HashSet<EntityID> newChunkEntities))
            {
                newChunkEntities = new HashSet<EntityID>();
                _chunkToEntities[toChunk] = newChunkEntities;
                _activeChunkCount++;
            }
            newChunkEntities.Add(entityId);

            // Update lookup
            _entityToChunk[entityId] = toChunk;

            // Update dirty chunk tracking if entity is dirty
            if (_dirtyEntities.Contains(entityId))
            {
                UpdateDirtyChunks();
            }

            OnEntityChangedChunk?.Invoke(entityId, fromChunk, toChunk);
        }

        #endregion

        #region Dirty Tracking

        /// <summary>
        /// Marks an entity as dirty (has unsaved changes).
        /// </summary>
        public void MarkEntityDirty(EntityID entityId)
        {
            if (!_entityRegistry.ContainsKey(entityId))
            {
                return;
            }

            if (_dirtyEntities.Add(entityId))
            {
                // Also mark the chunk as dirty
                if (_entityToChunk.TryGetValue(entityId, out ChunkID chunk))
                {
                    if (_dirtyChunks.Add(chunk))
                    {
                        OnChunkDirty?.Invoke(chunk);
                    }
                }

                OnEntityDirty?.Invoke(entityId);
            }
        }

        /// <summary>
        /// Clears the dirty flag for an entity after saving.
        /// </summary>
        public void ClearEntityDirty(EntityID entityId)
        {
            _dirtyEntities.Remove(entityId);

            if (_entityRegistry.TryGetValue(entityId, out IPersistentEntity entity))
            {
                entity.ClearDirty();
            }
        }

        /// <summary>
        /// Clears all dirty flags for entities in a chunk.
        /// </summary>
        public void ClearChunkDirty(ChunkID chunk)
        {
            if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> chunkEntities))
            {
                foreach (EntityID entityId in chunkEntities)
                {
                    ClearEntityDirty(entityId);
                }
            }

            _dirtyChunks.Remove(chunk);
        }

        /// <summary>
        /// Clears all dirty tracking (after full save).
        /// </summary>
        public void ClearAllDirty()
        {
            foreach (EntityID entityId in _dirtyEntities)
            {
                if (_entityRegistry.TryGetValue(entityId, out IPersistentEntity entity))
                {
                    entity.ClearDirty();
                }
            }

            _dirtyEntities.Clear();
            _dirtyChunks.Clear();
            _destroyedEntities.Clear();
        }

        /// <summary>
        /// Updates the dirty chunks set based on current dirty entities.
        /// </summary>
        private void UpdateDirtyChunks()
        {
            _dirtyChunks.Clear();

            foreach (EntityID entityId in _dirtyEntities)
            {
                if (_entityToChunk.TryGetValue(entityId, out ChunkID chunk))
                {
                    _dirtyChunks.Add(chunk);
                }
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Gets all entity IDs in a specific chunk.
        /// </summary>
        public IEnumerable<EntityID> GetEntitiesInChunk(ChunkID chunk)
        {
            if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> entities))
            {
                return entities;
            }
            return Array.Empty<EntityID>();
        }

        /// <summary>
        /// Gets all entities in a specific chunk.
        /// </summary>
        public IEnumerable<IPersistentEntity> GetEntityObjectsInChunk(ChunkID chunk)
        {
            if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> entityIds))
            {
                foreach (EntityID id in entityIds)
                {
                    if (_entityRegistry.TryGetValue(id, out IPersistentEntity entity))
                    {
                        yield return entity;
                    }
                }
            }
        }

        /// <summary>
        /// Gets an entity by ID.
        /// </summary>
        public IPersistentEntity GetEntity(EntityID entityId)
        {
            _entityRegistry.TryGetValue(entityId, out IPersistentEntity entity);
            return entity;
        }

        /// <summary>
        /// Gets the chunk an entity is currently in.
        /// </summary>
        public ChunkID GetEntityChunk(EntityID entityId)
        {
            _entityToChunk.TryGetValue(entityId, out ChunkID chunk);
            return chunk;
        }

        /// <summary>
        /// Checks if an entity exists in the index.
        /// </summary>
        public bool HasEntity(EntityID entityId)
        {
            return _entityRegistry.ContainsKey(entityId);
        }

        /// <summary>
        /// Checks if a chunk has any entities.
        /// </summary>
        public bool HasEntitiesInChunk(ChunkID chunk)
        {
            return _chunkToEntities.ContainsKey(chunk) && _chunkToEntities[chunk].Count > 0;
        }

        /// <summary>
        /// Gets all dirty entities.
        /// </summary>
        public IEnumerable<EntityID> GetDirtyEntities()
        {
            return _dirtyEntities;
        }

        /// <summary>
        /// Gets all dirty chunks.
        /// </summary>
        public IEnumerable<ChunkID> GetDirtyChunks()
        {
            return _dirtyChunks;
        }

        /// <summary>
        /// Gets all destroyed entity IDs (for delta save).
        /// </summary>
        public IEnumerable<EntityID> GetDestroyedEntities()
        {
            return _destroyedEntities;
        }

        /// <summary>
        /// Gets all dirty entities in a specific chunk.
        /// </summary>
        public IEnumerable<EntityID> GetDirtyEntitiesInChunk(ChunkID chunk)
        {
            if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> chunkEntities))
            {
                foreach (EntityID entityId in chunkEntities)
                {
                    if (_dirtyEntities.Contains(entityId))
                    {
                        yield return entityId;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all entities within a range of chunks around a center.
        /// </summary>
        public IEnumerable<IPersistentEntity> GetEntitiesInRange(ChunkID center, Vector3Int halfExtents)
        {
            for (int x = center.X - halfExtents.x; x <= center.X + halfExtents.x; x++)
            {
                for (int y = center.Y - halfExtents.y; y <= center.Y + halfExtents.y; y++)
                {
                    for (int z = center.Z - halfExtents.z; z <= center.Z + halfExtents.z; z++)
                    {
                        ChunkID chunk = new ChunkID(x, y, z);
                        foreach (IPersistentEntity entity in GetEntityObjectsInChunk(chunk))
                        {
                            yield return entity;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all active chunk IDs (chunks with entities).
        /// </summary>
        public IEnumerable<ChunkID> GetActiveChunks()
        {
            return _chunkToEntities.Keys;
        }

        /// <summary>
        /// Gets entity count for a specific chunk.
        /// </summary>
        public int GetEntityCountInChunk(ChunkID chunk)
        {
            if (_chunkToEntities.TryGetValue(chunk, out HashSet<EntityID> entities))
            {
                return entities.Count;
            }
            return 0;
        }

        #endregion

        #region Event Handlers

        private void HandleEntityChunkChanged(IPersistentEntity entity, ChunkID oldChunk, ChunkID newChunk)
        {
            // The entity handles its own chunk calculation, we just need to update the index
            MoveEntityToChunk(entity.EntityID, oldChunk, newChunk);
        }

        private void HandleEntityBecameDirty(IPersistentEntity entity)
        {
            MarkEntityDirty(entity.EntityID);
        }

        #endregion

        #region Debug

        /// <summary>
        /// Gets debug statistics about the world index.
        /// </summary>
        public WorldIndexStats GetStats()
        {
            return new WorldIndexStats
            {
                TotalEntities = _totalEntityCount,
                ActiveChunks = _activeChunkCount,
                DirtyEntities = _dirtyEntities.Count,
                DirtyChunks = _dirtyChunks.Count,
                DestroyedEntities = _destroyedEntities.Count,
                ChunkSize = _chunkSize
            };
        }

        #endregion
    }

    /// <summary>
    /// Statistics about the world index for debugging.
    /// </summary>
    public struct WorldIndexStats
    {
        public int TotalEntities;
        public int ActiveChunks;
        public int DirtyEntities;
        public int DirtyChunks;
        public int DestroyedEntities;
        public Vector3 ChunkSize;

        public override string ToString()
        {
            return $"WorldIndex: {TotalEntities} entities in {ActiveChunks} chunks " +
                   $"({DirtyEntities} dirty, {DestroyedEntities} destroyed)";
        }
    }
}
