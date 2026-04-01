using System;
using UnityEngine;

namespace SavingSystem.Core
{
    /// <summary>
    /// Base interface for all persistent entities in the world.
    /// Entities implement this to participate in the saving system.
    /// </summary>
    public interface IPersistentEntity
    {
        /// <summary>
        /// Unique identifier for this entity. Must be stable across saves/loads.
        /// </summary>
        EntityID EntityID { get; }

        /// <summary>
        /// The prefab identifier used to reconstruct this entity on load.
        /// </summary>
        string PrefabID { get; }

        /// <summary>
        /// Current world position of the entity.
        /// </summary>
        Vector3 WorldPosition { get; }

        /// <summary>
        /// Current chunk this entity belongs to (cached, updated on movement).
        /// </summary>
        ChunkID CurrentChunk { get; }

        /// <summary>
        /// Whether this entity has unsaved changes.
        /// </summary>
        bool IsDirty { get; }

        /// <summary>
        /// Marks the entity as dirty (needing save).
        /// </summary>
        void MarkDirty();

        /// <summary>
        /// Clears the dirty flag after saving.
        /// </summary>
        void ClearDirty();

        /// <summary>
        /// Captures the current state as serializable data.
        /// </summary>
        EntityStateData CaptureState();

        /// <summary>
        /// Restores state from saved data.
        /// </summary>
        void RestoreState(EntityStateData state);

        /// <summary>
        /// Called when the entity's chunk changes.
        /// </summary>
        event Action<IPersistentEntity, ChunkID, ChunkID> OnChunkChanged;

        /// <summary>
        /// Called when the entity becomes dirty.
        /// </summary>
        event Action<IPersistentEntity> OnBecameDirty;
    }

    /// <summary>
    /// Entity types for categorization and factory instantiation.
    /// </summary>
    public enum EntityType
    {
        None = 0,
        NPC = 1,
        Item = 2,
        Vehicle = 3,
        Destructible = 4,
        Interactive = 5,    // Doors, switches, etc.
        Container = 6,      // Chests, crates, etc.
        Custom = 100
    }

    /// <summary>
    /// Base class for all serializable entity state data.
    /// Contains common fields shared by all entity types.
    /// </summary>
    [Serializable]
    public class EntityStateData
    {
        /// <summary>
        /// The entity's unique identifier.
        /// </summary>
        public ulong EntityIDValue;

        /// <summary>
        /// Type of entity for deserialization/factory lookup.
        /// </summary>
        public EntityType Type;

        /// <summary>
        /// Prefab identifier for instantiation.
        /// </summary>
        public string PrefabID;

        /// <summary>
        /// World position (serialized as components for compatibility).
        /// </summary>
        public float PositionX;
        public float PositionY;
        public float PositionZ;

        /// <summary>
        /// World rotation (serialized as Euler angles).
        /// </summary>
        public float RotationX;
        public float RotationY;
        public float RotationZ;

        /// <summary>
        /// Timestamp when this state was saved.
        /// </summary>
        public long SaveTimestamp;

        /// <summary>
        /// Whether this entity has been destroyed/removed from the world.
        /// </summary>
        public bool IsDestroyed;

        public EntityStateData()
        {
            SaveTimestamp = DateTime.UtcNow.Ticks;
        }

        public Vector3 Position
        {
            get => new Vector3(PositionX, PositionY, PositionZ);
            set
            {
                PositionX = value.x;
                PositionY = value.y;
                PositionZ = value.z;
            }
        }

        public Vector3 Rotation
        {
            get => new Vector3(RotationX, RotationY, RotationZ);
            set
            {
                RotationX = value.x;
                RotationY = value.y;
                RotationZ = value.z;
            }
        }

        public EntityID EntityID
        {
            get => new EntityID(EntityIDValue);
            set => EntityIDValue = value.Value;
        }
    }

    /// <summary>
    /// State data for NPC entities.
    /// Contains appearance, behavior, and AI state information.
    /// </summary>
    [Serializable]
    public class NPCStateData : EntityStateData
    {
        // Appearance indices for character customization
        public int HeadIndex;
        public int BodyIndex;
        public int ClothingIndex;
        public int HairIndex;
        public int AccessoryIndex;

        // Appearance colors (serialized as components)
        public float SkinColorR, SkinColorG, SkinColorB;
        public float HairColorR, HairColorG, HairColorB;
        public float ClothingColorR, ClothingColorG, ClothingColorB;

        // AI/Behavior state
        public string CurrentTaskID;
        public string AIStateID;
        public float TaskProgress;

        // Stats
        public float Health;
        public float MaxHealth;
        public bool IsHostile;
        public bool IsAlerted;

        // Schedule/Routine
        public int CurrentScheduleIndex;
        public float ScheduleTimer;

        // Faction/Relationships
        public string FactionID;
        public int ReputationWithPlayer;

        public NPCStateData()
        {
            Type = EntityType.NPC;
            Health = 100f;
            MaxHealth = 100f;
        }

        public Color SkinColor
        {
            get => new Color(SkinColorR, SkinColorG, SkinColorB);
            set { SkinColorR = value.r; SkinColorG = value.g; SkinColorB = value.b; }
        }

        public Color HairColor
        {
            get => new Color(HairColorR, HairColorG, HairColorB);
            set { HairColorR = value.r; HairColorG = value.g; HairColorB = value.b; }
        }

        public Color ClothingColor
        {
            get => new Color(ClothingColorR, ClothingColorG, ClothingColorB);
            set { ClothingColorR = value.r; ClothingColorG = value.g; ClothingColorB = value.b; }
        }
    }

    /// <summary>
    /// State data for item/pickup entities.
    /// </summary>
    [Serializable]
    public class ItemStateData : EntityStateData
    {
        public string ItemTypeID;
        public int StackCount;
        public int Durability;
        public int MaxDurability;

        // Ownership tracking
        public ulong OwnerEntityID;
        public bool HasOwner;

        // Container state (if item is in a container)
        public ulong ContainerEntityID;
        public int ContainerSlotIndex;
        public bool IsInContainer;

        // World state
        public bool IsPickedUp;
        public bool IsDropped;

        // Physics state (for dropped items)
        public float VelocityX, VelocityY, VelocityZ;
        public bool IsKinematic;

        public ItemStateData()
        {
            Type = EntityType.Item;
            StackCount = 1;
        }

        public Vector3 Velocity
        {
            get => new Vector3(VelocityX, VelocityY, VelocityZ);
            set { VelocityX = value.x; VelocityY = value.y; VelocityZ = value.z; }
        }
    }

    /// <summary>
    /// State data for destructible objects.
    /// </summary>
    [Serializable]
    public class DestructibleStateData : EntityStateData
    {
        public float Health;
        public float MaxHealth;
        public int DamageStage;     // Visual damage level
        public bool IsFullyDestroyed;

        // Debris tracking
        public bool HasSpawnedDebris;
        public string DebrisConfigID;

        public DestructibleStateData()
        {
            Type = EntityType.Destructible;
        }
    }

    /// <summary>
    /// State data for interactive objects (doors, switches, etc.)
    /// </summary>
    [Serializable]
    public class InteractiveStateData : EntityStateData
    {
        public bool IsOpen;
        public bool IsLocked;
        public string LockKeyID;

        // Animation state
        public float AnimationProgress;
        public bool IsAnimating;

        // Usage tracking
        public int UseCount;
        public long LastUsedTimestamp;

        // Linked objects (for switches that control other objects)
        public ulong[] LinkedEntityIDs;

        public InteractiveStateData()
        {
            Type = EntityType.Interactive;
        }
    }

    /// <summary>
    /// State data for container objects (chests, crates, etc.)
    /// </summary>
    [Serializable]
    public class ContainerStateData : EntityStateData
    {
        public bool IsOpen;
        public bool IsLocked;
        public string LockKeyID;

        // Inventory (stored as array of item state data)
        public ItemStateData[] Contents;
        public int Capacity;

        // Loot generation
        public bool HasBeenLooted;
        public string LootTableID;
        public long FirstOpenedTimestamp;

        public ContainerStateData()
        {
            Type = EntityType.Container;
        }
    }

    /// <summary>
    /// State data for vehicle entities.
    /// </summary>
    [Serializable]
    public class VehicleStateData : EntityStateData
    {
        public string VehicleTypeID;
        
        // Condition
        public float Health;
        public float MaxHealth;
        public float Fuel;
        public float MaxFuel;

        // Physics state
        public float VelocityX, VelocityY, VelocityZ;
        public float AngularVelocityX, AngularVelocityY, AngularVelocityZ;

        // Component damage
        public float EngineHealth;
        public float[] WheelHealth;
        public bool[] DoorStates;   // true = open

        // Ownership
        public ulong OwnerEntityID;
        public bool HasOwner;
        public bool IsLocked;

        // Occupants
        public ulong[] OccupantEntityIDs;

        public VehicleStateData()
        {
            Type = EntityType.Vehicle;
        }

        public Vector3 Velocity
        {
            get => new Vector3(VelocityX, VelocityY, VelocityZ);
            set { VelocityX = value.x; VelocityY = value.y; VelocityZ = value.z; }
        }

        public Vector3 AngularVelocity
        {
            get => new Vector3(AngularVelocityX, AngularVelocityY, AngularVelocityZ);
            set { AngularVelocityX = value.x; AngularVelocityY = value.y; AngularVelocityZ = value.z; }
        }
    }
}
