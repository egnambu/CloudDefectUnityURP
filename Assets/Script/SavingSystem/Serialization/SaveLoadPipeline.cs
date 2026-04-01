using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SavingSystem.Serialization
{
    using Core;

    /// <summary>
    /// Represents save data for a single chunk.
    /// Contains all entity states within that chunk.
    /// </summary>
    [Serializable]
    public class ChunkSaveData
    {
        public int ChunkX;
        public int ChunkY;
        public int ChunkZ;
        public long SaveTimestamp;
        public int EntityCount;

        // Serialized entity data by type
        public List<NPCStateData> NPCs = new List<NPCStateData>();
        public List<ItemStateData> Items = new List<ItemStateData>();
        public List<DestructibleStateData> Destructibles = new List<DestructibleStateData>();
        public List<InteractiveStateData> Interactives = new List<InteractiveStateData>();
        public List<ContainerStateData> Containers = new List<ContainerStateData>();
        public List<VehicleStateData> Vehicles = new List<VehicleStateData>();

        // Track destroyed entity IDs for delta saves
        public List<ulong> DestroyedEntityIDs = new List<ulong>();

        public ChunkID ChunkID
        {
            get => new ChunkID(ChunkX, ChunkY, ChunkZ);
            set { ChunkX = value.X; ChunkY = value.Y; ChunkZ = value.Z; }
        }

        public ChunkSaveData()
        {
            SaveTimestamp = DateTime.UtcNow.Ticks;
        }

        public ChunkSaveData(ChunkID chunk) : this()
        {
            ChunkID = chunk;
        }

        /// <summary>
        /// Adds entity state data to the appropriate list based on type.
        /// </summary>
        public void AddEntityState(EntityStateData state)
        {
            switch (state.Type)
            {
                case EntityType.NPC:
                    NPCs.Add((NPCStateData)state);
                    break;
                case EntityType.Item:
                    Items.Add((ItemStateData)state);
                    break;
                case EntityType.Destructible:
                    Destructibles.Add((DestructibleStateData)state);
                    break;
                case EntityType.Interactive:
                    Interactives.Add((InteractiveStateData)state);
                    break;
                case EntityType.Container:
                    Containers.Add((ContainerStateData)state);
                    break;
                case EntityType.Vehicle:
                    Vehicles.Add((VehicleStateData)state);
                    break;
                default:
                    Debug.LogWarning($"[ChunkSaveData] Unknown entity type: {state.Type}");
                    break;
            }
            EntityCount++;
        }

        /// <summary>
        /// Gets all entity states in this chunk.
        /// </summary>
        public IEnumerable<EntityStateData> GetAllEntityStates()
        {
            foreach (var npc in NPCs) yield return npc;
            foreach (var item in Items) yield return item;
            foreach (var destructible in Destructibles) yield return destructible;
            foreach (var interactive in Interactives) yield return interactive;
            foreach (var container in Containers) yield return container;
            foreach (var vehicle in Vehicles) yield return vehicle;
        }

        /// <summary>
        /// Clears all entity data.
        /// </summary>
        public void Clear()
        {
            NPCs.Clear();
            Items.Clear();
            Destructibles.Clear();
            Interactives.Clear();
            Containers.Clear();
            Vehicles.Clear();
            DestroyedEntityIDs.Clear();
            EntityCount = 0;
        }
    }

    /// <summary>
    /// Represents the complete world save data.
    /// Contains metadata and references to chunk save files.
    /// </summary>
    [Serializable]
    public class WorldSaveData
    {
        public string SaveName;
        public string SaveVersion;
        public long SaveTimestamp;
        public float PlayTime;

        // World configuration at time of save
        public float ChunkSizeX;
        public float ChunkSizeY;
        public float ChunkSizeZ;

        // Player/center position at save time
        public float CenterPositionX;
        public float CenterPositionY;
        public float CenterPositionZ;

        // List of chunks with saved data
        public List<ChunkReference> SavedChunks = new List<ChunkReference>();

        // Global destroyed entities (entities destroyed but not yet cleaned up from base world)
        public List<ulong> GlobalDestroyedEntities = new List<ulong>();

        public Vector3 ChunkSize
        {
            get => new Vector3(ChunkSizeX, ChunkSizeY, ChunkSizeZ);
            set { ChunkSizeX = value.x; ChunkSizeY = value.y; ChunkSizeZ = value.z; }
        }

        public Vector3 CenterPosition
        {
            get => new Vector3(CenterPositionX, CenterPositionY, CenterPositionZ);
            set { CenterPositionX = value.x; CenterPositionY = value.y; CenterPositionZ = value.z; }
        }

        public WorldSaveData()
        {
            SaveVersion = "1.0";
            SaveTimestamp = DateTime.UtcNow.Ticks;
        }
    }

    /// <summary>
    /// Reference to a chunk's save file.
    /// </summary>
    [Serializable]
    public class ChunkReference
    {
        public int ChunkX;
        public int ChunkY;
        public int ChunkZ;
        public string FileName;
        public int EntityCount;
        public long LastModified;

        public ChunkID ChunkID
        {
            get => new ChunkID(ChunkX, ChunkY, ChunkZ);
            set { ChunkX = value.X; ChunkY = value.Y; ChunkZ = value.Z; }
        }
    }

    /// <summary>
    /// Handles serialization and deserialization of save data.
    /// Supports chunk-based streaming for large worlds.
    /// </summary>
    public class SaveSerializer
    {
        private readonly string _basePath;
        private readonly string _chunkSubfolder;

        public SaveSerializer(string basePath, string chunkSubfolder = "chunks")
        {
            _basePath = basePath;
            _chunkSubfolder = chunkSubfolder;
        }

        #region World Save/Load

        /// <summary>
        /// Saves the world metadata file.
        /// </summary>
        public void SaveWorldMetadata(WorldSaveData worldData, string saveName)
        {
            string savePath = GetWorldSavePath(saveName);
            EnsureDirectoryExists(savePath);

            string metadataPath = Path.Combine(savePath, "world.json");
            string json = JsonUtility.ToJson(worldData, true);
            File.WriteAllText(metadataPath, json, Encoding.UTF8);

            Debug.Log($"[SaveSerializer] Saved world metadata to {metadataPath}");
        }

        /// <summary>
        /// Loads the world metadata file.
        /// </summary>
        public WorldSaveData LoadWorldMetadata(string saveName)
        {
            string savePath = GetWorldSavePath(saveName);
            string metadataPath = Path.Combine(savePath, "world.json");

            if (!File.Exists(metadataPath))
            {
                Debug.LogWarning($"[SaveSerializer] World metadata not found: {metadataPath}");
                return null;
            }

            string json = File.ReadAllText(metadataPath, Encoding.UTF8);
            return JsonUtility.FromJson<WorldSaveData>(json);
        }

        /// <summary>
        /// Checks if a save exists.
        /// </summary>
        public bool SaveExists(string saveName)
        {
            string metadataPath = Path.Combine(GetWorldSavePath(saveName), "world.json");
            return File.Exists(metadataPath);
        }

        /// <summary>
        /// Deletes a save and all its chunk files.
        /// </summary>
        public void DeleteSave(string saveName)
        {
            string savePath = GetWorldSavePath(saveName);
            if (Directory.Exists(savePath))
            {
                Directory.Delete(savePath, true);
                Debug.Log($"[SaveSerializer] Deleted save: {saveName}");
            }
        }

        /// <summary>
        /// Gets all available save names.
        /// </summary>
        public string[] GetAvailableSaves()
        {
            if (!Directory.Exists(_basePath))
                return Array.Empty<string>();

            string[] directories = Directory.GetDirectories(_basePath);
            List<string> saves = new List<string>();

            foreach (string dir in directories)
            {
                string worldFile = Path.Combine(dir, "world.json");
                if (File.Exists(worldFile))
                {
                    saves.Add(Path.GetFileName(dir));
                }
            }

            return saves.ToArray();
        }

        #endregion

        #region Chunk Save/Load

        /// <summary>
        /// Saves a single chunk's data.
        /// </summary>
        public void SaveChunk(ChunkSaveData chunkData, string saveName)
        {
            string chunkPath = GetChunkFilePath(saveName, chunkData.ChunkID);
            EnsureDirectoryExists(chunkPath);

            chunkData.SaveTimestamp = DateTime.UtcNow.Ticks;
            string json = JsonUtility.ToJson(chunkData, false);
            File.WriteAllText(chunkPath, json, Encoding.UTF8);
        }

        /// <summary>
        /// Saves multiple chunks efficiently.
        /// </summary>
        public void SaveChunks(IEnumerable<ChunkSaveData> chunks, string saveName)
        {
            foreach (ChunkSaveData chunk in chunks)
            {
                SaveChunk(chunk, saveName);
            }
        }

        /// <summary>
        /// Loads a single chunk's data.
        /// </summary>
        public ChunkSaveData LoadChunk(string saveName, ChunkID chunkId)
        {
            string chunkPath = GetChunkFilePath(saveName, chunkId);

            if (!File.Exists(chunkPath))
            {
                return null; // No saved data for this chunk
            }

            string json = File.ReadAllText(chunkPath, Encoding.UTF8);
            return JsonUtility.FromJson<ChunkSaveData>(json);
        }

        /// <summary>
        /// Loads multiple chunks.
        /// </summary>
        public Dictionary<ChunkID, ChunkSaveData> LoadChunks(string saveName, IEnumerable<ChunkID> chunkIds)
        {
            Dictionary<ChunkID, ChunkSaveData> result = new Dictionary<ChunkID, ChunkSaveData>();

            foreach (ChunkID chunkId in chunkIds)
            {
                ChunkSaveData data = LoadChunk(saveName, chunkId);
                if (data != null)
                {
                    result[chunkId] = data;
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a chunk has saved data.
        /// </summary>
        public bool ChunkHasSaveData(string saveName, ChunkID chunkId)
        {
            return File.Exists(GetChunkFilePath(saveName, chunkId));
        }

        /// <summary>
        /// Deletes a chunk's save data.
        /// </summary>
        public void DeleteChunk(string saveName, ChunkID chunkId)
        {
            string chunkPath = GetChunkFilePath(saveName, chunkId);
            if (File.Exists(chunkPath))
            {
                File.Delete(chunkPath);
            }
        }

        #endregion

        #region Path Helpers

        private string GetWorldSavePath(string saveName)
        {
            return Path.Combine(_basePath, saveName);
        }

        private string GetChunkFolderPath(string saveName)
        {
            return Path.Combine(GetWorldSavePath(saveName), _chunkSubfolder);
        }

        private string GetChunkFilePath(string saveName, ChunkID chunkId)
        {
            string folder = GetChunkFolderPath(saveName);
            string fileName = $"chunk_{chunkId.X}_{chunkId.Y}_{chunkId.Z}.json";
            return Path.Combine(folder, fileName);
        }

        private void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        #endregion
    }

    /// <summary>
    /// Manages the save/load pipeline with delta saving support.
    /// </summary>
    public class SaveLoadPipeline
    {
        private readonly SaveSerializer _serializer;
        private readonly WorldIndexManager _worldIndex;
        private string _currentSaveName;

        public SaveLoadPipeline(SaveSerializer serializer, WorldIndexManager worldIndex)
        {
            _serializer = serializer;
            _worldIndex = worldIndex;
        }

        /// <summary>
        /// Current save name (slot).
        /// </summary>
        public string CurrentSaveName
        {
            get => _currentSaveName;
            set => _currentSaveName = value;
        }

        #region Saving

        /// <summary>
        /// Performs a full world save (all entities).
        /// </summary>
        public void SaveWorld(string saveName, Vector3 centerPosition, float playTime)
        {
            _currentSaveName = saveName;

            // Create world metadata
            WorldSaveData worldData = new WorldSaveData
            {
                SaveName = saveName,
                ChunkSize = _worldIndex.ChunkSize,
                CenterPosition = centerPosition,
                PlayTime = playTime
            };

            // Save all chunks with entities
            Dictionary<ChunkID, ChunkSaveData> chunkDataMap = new Dictionary<ChunkID, ChunkSaveData>();

            foreach (ChunkID chunkId in _worldIndex.GetActiveChunks())
            {
                ChunkSaveData chunkData = CaptureChunkState(chunkId);
                if (chunkData.EntityCount > 0)
                {
                    _serializer.SaveChunk(chunkData, saveName);

                    worldData.SavedChunks.Add(new ChunkReference
                    {
                        ChunkID = chunkId,
                        FileName = $"chunk_{chunkId.X}_{chunkId.Y}_{chunkId.Z}.json",
                        EntityCount = chunkData.EntityCount,
                        LastModified = chunkData.SaveTimestamp
                    });
                }
            }

            // Add destroyed entities to world data
            foreach (EntityID destroyedId in _worldIndex.GetDestroyedEntities())
            {
                worldData.GlobalDestroyedEntities.Add(destroyedId.Value);
            }

            // Save world metadata
            _serializer.SaveWorldMetadata(worldData, saveName);

            // Clear dirty flags
            _worldIndex.ClearAllDirty();

            Debug.Log($"[SaveLoadPipeline] Full save completed: {worldData.SavedChunks.Count} chunks, " +
                      $"{_worldIndex.TotalEntityCount} entities");
        }

        /// <summary>
        /// Performs a delta save (only dirty entities).
        /// More efficient for frequent saves.
        /// </summary>
        public void SaveDelta(string saveName, Vector3 centerPosition, float playTime)
        {
            if (!_worldIndex.HasDirtyData)
            {
                Debug.Log("[SaveLoadPipeline] No dirty data to save");
                return;
            }

            _currentSaveName = saveName;

            // Load existing world metadata or create new
            WorldSaveData worldData = _serializer.LoadWorldMetadata(saveName) ?? new WorldSaveData
            {
                SaveName = saveName,
                ChunkSize = _worldIndex.ChunkSize
            };

            worldData.CenterPosition = centerPosition;
            worldData.PlayTime = playTime;
            worldData.SaveTimestamp = DateTime.UtcNow.Ticks;

            // Save only dirty chunks
            foreach (ChunkID chunkId in _worldIndex.GetDirtyChunks())
            {
                ChunkSaveData chunkData = CaptureChunkState(chunkId);

                // Only save if there's actual data
                if (chunkData.EntityCount > 0 || chunkData.DestroyedEntityIDs.Count > 0)
                {
                    _serializer.SaveChunk(chunkData, saveName);

                    // Update or add chunk reference
                    ChunkReference existingRef = worldData.SavedChunks.Find(r =>
                        r.ChunkX == chunkId.X && r.ChunkY == chunkId.Y && r.ChunkZ == chunkId.Z);

                    if (existingRef != null)
                    {
                        existingRef.EntityCount = chunkData.EntityCount;
                        existingRef.LastModified = chunkData.SaveTimestamp;
                    }
                    else
                    {
                        worldData.SavedChunks.Add(new ChunkReference
                        {
                            ChunkID = chunkId,
                            FileName = $"chunk_{chunkId.X}_{chunkId.Y}_{chunkId.Z}.json",
                            EntityCount = chunkData.EntityCount,
                            LastModified = chunkData.SaveTimestamp
                        });
                    }
                }
            }

            // Update destroyed entities
            foreach (EntityID destroyedId in _worldIndex.GetDestroyedEntities())
            {
                if (!worldData.GlobalDestroyedEntities.Contains(destroyedId.Value))
                {
                    worldData.GlobalDestroyedEntities.Add(destroyedId.Value);
                }
            }

            // Save world metadata
            _serializer.SaveWorldMetadata(worldData, saveName);

            // Clear dirty flags
            _worldIndex.ClearAllDirty();

            Debug.Log($"[SaveLoadPipeline] Delta save completed: {_worldIndex.DirtyChunkCount} chunks updated");
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

        #endregion

        #region Loading

        /// <summary>
        /// Loads world metadata without loading entities.
        /// </summary>
        public WorldSaveData LoadWorldMetadata(string saveName)
        {
            return _serializer.LoadWorldMetadata(saveName);
        }

        /// <summary>
        /// Loads entities for chunks within a range of the center position.
        /// Returns the loaded chunk data for entity instantiation.
        /// </summary>
        public Dictionary<ChunkID, ChunkSaveData> LoadChunksInRange(
            string saveName,
            ChunkID centerChunk,
            Vector3Int halfExtents)
        {
            _currentSaveName = saveName;

            List<ChunkID> chunksToLoad = new List<ChunkID>();

            ChunkMathUtility.GetChunksInRange(centerChunk, halfExtents, chunk =>
            {
                if (_serializer.ChunkHasSaveData(saveName, chunk))
                {
                    chunksToLoad.Add(chunk);
                }
            });

            return _serializer.LoadChunks(saveName, chunksToLoad);
        }

        /// <summary>
        /// Loads a single chunk's data.
        /// </summary>
        public ChunkSaveData LoadChunk(string saveName, ChunkID chunkId)
        {
            return _serializer.LoadChunk(saveName, chunkId);
        }

        /// <summary>
        /// Gets the list of destroyed entity IDs from the save.
        /// </summary>
        public HashSet<ulong> GetDestroyedEntityIds(string saveName)
        {
            WorldSaveData worldData = _serializer.LoadWorldMetadata(saveName);
            if (worldData == null)
                return new HashSet<ulong>();

            return new HashSet<ulong>(worldData.GlobalDestroyedEntities);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Checks if a save exists.
        /// </summary>
        public bool SaveExists(string saveName)
        {
            return _serializer.SaveExists(saveName);
        }

        /// <summary>
        /// Deletes a save.
        /// </summary>
        public void DeleteSave(string saveName)
        {
            _serializer.DeleteSave(saveName);
        }

        /// <summary>
        /// Gets all available save names.
        /// </summary>
        public string[] GetAvailableSaves()
        {
            return _serializer.GetAvailableSaves();
        }

        #endregion
    }
}
