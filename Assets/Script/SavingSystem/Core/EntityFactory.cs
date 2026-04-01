using System;
using System.Collections.Generic;
using UnityEngine;

namespace SavingSystem.Core
{
    /// <summary>
    /// Factory for creating entity instances from save data.
    /// Handles prefab lookup and entity instantiation.
    /// </summary>
    public class EntityFactory
    {
        #region Private Fields

        // Prefab registry: PrefabID -> Prefab GameObject
        private readonly Dictionary<string, GameObject> _prefabRegistry;

        // Type factories for custom instantiation logic
        private readonly Dictionary<EntityType, Func<EntityStateData, IPersistentEntity>> _typeFactories;

        // Default prefab lookup path (Resources folder)
        private readonly string _resourcesPath;

        #endregion

        #region Constructor

        public EntityFactory(string resourcesPath = "Prefabs/Entities")
        {
            _prefabRegistry = new Dictionary<string, GameObject>();
            _typeFactories = new Dictionary<EntityType, Func<EntityStateData, IPersistentEntity>>();
            _resourcesPath = resourcesPath;

            // Register default factories
            RegisterDefaultFactories();
        }

        #endregion

        #region Prefab Registration

        /// <summary>
        /// Registers a prefab for a specific prefab ID.
        /// </summary>
        public void RegisterPrefab(string prefabId, GameObject prefab)
        {
            if (string.IsNullOrEmpty(prefabId) || prefab == null)
            {
                Debug.LogWarning("[EntityFactory] Invalid prefab registration");
                return;
            }

            _prefabRegistry[prefabId] = prefab;
        }

        /// <summary>
        /// Registers multiple prefabs from an array.
        /// </summary>
        public void RegisterPrefabs(PrefabMapping[] mappings)
        {
            foreach (PrefabMapping mapping in mappings)
            {
                RegisterPrefab(mapping.PrefabId, mapping.Prefab);
            }
        }

        /// <summary>
        /// Unregisters a prefab.
        /// </summary>
        public void UnregisterPrefab(string prefabId)
        {
            _prefabRegistry.Remove(prefabId);
        }

        /// <summary>
        /// Gets a registered prefab by ID.
        /// Falls back to Resources.Load if not found in registry.
        /// </summary>
        public GameObject GetPrefab(string prefabId)
        {
            // Check registry first
            if (_prefabRegistry.TryGetValue(prefabId, out GameObject prefab))
            {
                return prefab;
            }

            // Try loading from Resources
            string resourcePath = $"{_resourcesPath}/{prefabId}";
            prefab = Resources.Load<GameObject>(resourcePath);

            if (prefab != null)
            {
                // Cache for future use
                _prefabRegistry[prefabId] = prefab;
                return prefab;
            }

            Debug.LogWarning($"[EntityFactory] Prefab not found: {prefabId} (tried Resources path: {resourcePath})");
            return null;
        }

        #endregion

        #region Type Factory Registration

        /// <summary>
        /// Registers a custom factory function for an entity type.
        /// </summary>
        public void RegisterTypeFactory(EntityType type, Func<EntityStateData, IPersistentEntity> factory)
        {
            _typeFactories[type] = factory;
        }

        /// <summary>
        /// Registers default factory methods for standard entity types.
        /// </summary>
        private void RegisterDefaultFactories()
        {
            _typeFactories[EntityType.NPC] = CreateNPCEntity;
            _typeFactories[EntityType.Item] = CreateItemEntity;
            _typeFactories[EntityType.Destructible] = CreateDestructibleEntity;
            _typeFactories[EntityType.Interactive] = CreateInteractiveEntity;
            _typeFactories[EntityType.Container] = CreateContainerEntity;
            _typeFactories[EntityType.Vehicle] = CreateVehicleEntity;
        }

        #endregion

        #region Entity Creation

        /// <summary>
        /// Creates an entity from save data.
        /// </summary>
        public IPersistentEntity CreateEntity(EntityStateData state)
        {
            if (state == null)
            {
                Debug.LogError("[EntityFactory] Cannot create entity from null state");
                return null;
            }

            // Check for custom factory
            if (_typeFactories.TryGetValue(state.Type, out var factory))
            {
                return factory(state);
            }

            // Default creation using prefab
            return CreateDefaultEntity(state);
        }

        /// <summary>
        /// Default entity creation - instantiates prefab and restores state.
        /// </summary>
        private IPersistentEntity CreateDefaultEntity(EntityStateData state)
        {
            GameObject prefab = GetPrefab(state.PrefabID);
            if (prefab == null)
            {
                Debug.LogError($"[EntityFactory] Failed to create entity: prefab '{state.PrefabID}' not found");
                return null;
            }

            // Instantiate at saved position and rotation
            Vector3 position = state.Position;
            Quaternion rotation = Quaternion.Euler(state.Rotation);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, position, rotation);

            // Get persistent entity component
            IPersistentEntity entity = instance.GetComponent<IPersistentEntity>();
            if (entity == null)
            {
                Debug.LogError($"[EntityFactory] Prefab '{state.PrefabID}' does not have IPersistentEntity component");
                UnityEngine.Object.Destroy(instance);
                return null;
            }

            // Set entity ID from save data
            if (entity is PersistentEntityBase persistentBase)
            {
                persistentBase.SetEntityId(new EntityID(state.EntityIDValue));
            }

            // Restore state
            entity.RestoreState(state);

            return entity;
        }

        #endregion

        #region Type-Specific Factories

        private IPersistentEntity CreateNPCEntity(EntityStateData state)
        {
            NPCStateData npcState = state as NPCStateData;
            if (npcState == null)
            {
                Debug.LogError("[EntityFactory] Invalid NPC state data");
                return null;
            }

            return CreateDefaultEntity(state);
        }

        private IPersistentEntity CreateItemEntity(EntityStateData state)
        {
            ItemStateData itemState = state as ItemStateData;
            if (itemState == null)
            {
                Debug.LogError("[EntityFactory] Invalid Item state data");
                return null;
            }

            // Skip items that are in containers (they're managed by the container)
            if (itemState.IsInContainer)
            {
                return null;
            }

            // Skip picked up items
            if (itemState.IsPickedUp)
            {
                return null;
            }

            return CreateDefaultEntity(state);
        }

        private IPersistentEntity CreateDestructibleEntity(EntityStateData state)
        {
            DestructibleStateData destructibleState = state as DestructibleStateData;
            if (destructibleState == null)
            {
                Debug.LogError("[EntityFactory] Invalid Destructible state data");
                return null;
            }

            // Skip fully destroyed objects (they shouldn't be re-instantiated)
            if (destructibleState.IsFullyDestroyed)
            {
                return null;
            }

            return CreateDefaultEntity(state);
        }

        private IPersistentEntity CreateInteractiveEntity(EntityStateData state)
        {
            InteractiveStateData interactiveState = state as InteractiveStateData;
            if (interactiveState == null)
            {
                Debug.LogError("[EntityFactory] Invalid Interactive state data");
                return null;
            }

            return CreateDefaultEntity(state);
        }

        private IPersistentEntity CreateContainerEntity(EntityStateData state)
        {
            ContainerStateData containerState = state as ContainerStateData;
            if (containerState == null)
            {
                Debug.LogError("[EntityFactory] Invalid Container state data");
                return null;
            }

            return CreateDefaultEntity(state);
        }

        private IPersistentEntity CreateVehicleEntity(EntityStateData state)
        {
            VehicleStateData vehicleState = state as VehicleStateData;
            if (vehicleState == null)
            {
                Debug.LogError("[EntityFactory] Invalid Vehicle state data");
                return null;
            }

            return CreateDefaultEntity(state);
        }

        #endregion
    }

    /// <summary>
    /// Mapping between prefab ID and prefab asset.
    /// Used for editor-configured prefab registration.
    /// </summary>
    [Serializable]
    public class PrefabMapping
    {
        public string PrefabId;
        public GameObject Prefab;
    }

    /// <summary>
    /// ScriptableObject for configuring entity prefabs in the editor.
    /// </summary>
    [CreateAssetMenu(fileName = "EntityPrefabRegistry", menuName = "Saving System/Entity Prefab Registry")]
    public class EntityPrefabRegistry : ScriptableObject
    {
        [Header("Entity Prefabs")]
        [Tooltip("Prefab mappings for all persistent entity types.")]
        public PrefabMapping[] Prefabs;

        [Header("Default Prefabs")]
        [Tooltip("Default NPC prefab when specific prefab not found.")]
        public GameObject DefaultNPCPrefab;

        [Tooltip("Default Item prefab when specific prefab not found.")]
        public GameObject DefaultItemPrefab;

        [Tooltip("Default Destructible prefab when specific prefab not found.")]
        public GameObject DefaultDestructiblePrefab;

        /// <summary>
        /// Registers all prefabs with the given factory.
        /// </summary>
        public void RegisterWithFactory(EntityFactory factory)
        {
            if (Prefabs != null)
            {
                factory.RegisterPrefabs(Prefabs);
            }

            // Register defaults
            if (DefaultNPCPrefab != null)
                factory.RegisterPrefab("Default_NPC", DefaultNPCPrefab);

            if (DefaultItemPrefab != null)
                factory.RegisterPrefab("Default_Item", DefaultItemPrefab);

            if (DefaultDestructiblePrefab != null)
                factory.RegisterPrefab("Default_Destructible", DefaultDestructiblePrefab);
        }
    }
}
