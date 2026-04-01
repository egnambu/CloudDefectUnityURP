using UnityEngine;
using SavingSystem.Core;

namespace SavingSystem.Entities
{
    /// <summary>
    /// Example Item persistent entity implementation.
    /// Demonstrates how to create a world item that saves/loads properly.
    /// </summary>
    public class PersistentItem : PersistentEntityBase
    {
        #region Inspector Fields

        [Header("Item Data")]
        [SerializeField] private string _itemTypeId = "Generic";
        [SerializeField] private int _stackCount = 1;
        [SerializeField] private int _durability = 100;
        [SerializeField] private int _maxDurability = 100;

        [Header("State")]
        [SerializeField] private bool _isPickedUp;
        [SerializeField] private bool _isDropped;
        [SerializeField] private bool _isKinematic;

        [Header("Ownership")]
        [SerializeField] private ulong _ownerEntityIdValue;
        [SerializeField] private bool _hasOwner;

        [Header("Container")]
        [SerializeField] private ulong _containerEntityIdValue;
        [SerializeField] private int _containerSlotIndex = -1;
        [SerializeField] private bool _isInContainer;

        #endregion

        #region Components

        private Rigidbody _rigidbody;
        private Collider _collider;

        #endregion

        #region Properties

        public string ItemTypeId
        {
            get => _itemTypeId;
            set { _itemTypeId = value; MarkDirty(); }
        }

        public int StackCount
        {
            get => _stackCount;
            set { _stackCount = Mathf.Max(0, value); MarkDirty(); }
        }

        public int Durability
        {
            get => _durability;
            set
            {
                _durability = Mathf.Clamp(value, 0, _maxDurability);
                MarkDirty();
                if (_durability <= 0)
                    OnItemBroken();
            }
        }

        public bool IsPickedUp
        {
            get => _isPickedUp;
            private set { _isPickedUp = value; MarkDirty(); }
        }

        public bool IsInContainer
        {
            get => _isInContainer;
            private set { _isInContainer = value; MarkDirty(); }
        }

        public EntityID OwnerEntityId
        {
            get => new EntityID(_ownerEntityIdValue);
            private set { _ownerEntityIdValue = value.Value; _hasOwner = value.IsValid; MarkDirty(); }
        }

        public bool HasOwner => _hasOwner;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _rigidbody = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
        }

        private void Update()
        {
            // Update position tracking for dropped items
            if (!_isPickedUp && !_isInContainer)
            {
                UpdatePositionTracking();
            }
        }

        #endregion

        #region State Capture/Restore

        public override EntityStateData CaptureState()
        {
            ItemStateData state = new ItemStateData();

            // Base state
            PopulateBaseState(state);

            // Item data
            state.ItemTypeID = _itemTypeId;
            state.StackCount = _stackCount;
            state.Durability = _durability;
            state.MaxDurability = _maxDurability;

            // State
            state.IsPickedUp = _isPickedUp;
            state.IsDropped = _isDropped;
            state.IsKinematic = _rigidbody != null ? _rigidbody.isKinematic : true;

            // Physics velocity (for dropped items in motion)
            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                state.Velocity = _rigidbody.linearVelocity;
            }

            // Ownership
            state.OwnerEntityID = _ownerEntityIdValue;
            state.HasOwner = _hasOwner;

            // Container
            state.ContainerEntityID = _containerEntityIdValue;
            state.ContainerSlotIndex = _containerSlotIndex;
            state.IsInContainer = _isInContainer;

            return state;
        }

        public override void RestoreState(EntityStateData state)
        {
            if (state is not ItemStateData itemState)
            {
                Debug.LogError($"[PersistentItem] Invalid state data type: {state.GetType()}");
                return;
            }

            // Base state
            RestoreBaseState(state);

            // Item data
            _itemTypeId = itemState.ItemTypeID;
            _stackCount = itemState.StackCount;
            _durability = itemState.Durability;
            _maxDurability = itemState.MaxDurability;

            // State
            _isPickedUp = itemState.IsPickedUp;
            _isDropped = itemState.IsDropped;
            _isKinematic = itemState.IsKinematic;

            // Apply physics state
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = _isKinematic;
                if (!_isKinematic)
                {
                    _rigidbody.linearVelocity = itemState.Velocity;
                }
            }

            // Ownership
            _ownerEntityIdValue = itemState.OwnerEntityID;
            _hasOwner = itemState.HasOwner;

            // Container
            _containerEntityIdValue = itemState.ContainerEntityID;
            _containerSlotIndex = itemState.ContainerSlotIndex;
            _isInContainer = itemState.IsInContainer;

            // Update visibility based on state
            UpdateVisibility();
        }

        #endregion

        #region Item Operations

        /// <summary>
        /// Picks up the item (transfers to inventory).
        /// </summary>
        public void Pickup(EntityID newOwner)
        {
            IsPickedUp = true;
            OwnerEntityId = newOwner;
            _isDropped = false;
            _isInContainer = false;

            // Disable physics and visibility
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }
            if (_collider != null)
            {
                _collider.enabled = false;
            }

            gameObject.SetActive(false);
            MarkDirty();
        }

        /// <summary>
        /// Drops the item at a position.
        /// </summary>
        public void Drop(Vector3 position, Vector3 velocity = default)
        {
            IsPickedUp = false;
            _isDropped = true;
            _isInContainer = false;

            transform.position = position;

            // Enable physics
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
                _rigidbody.linearVelocity = velocity;
            }
            if (_collider != null)
            {
                _collider.enabled = true;
            }

            gameObject.SetActive(true);
            ForceChunkUpdate();
            MarkDirty();
        }

        /// <summary>
        /// Places the item in a container.
        /// </summary>
        public void PlaceInContainer(EntityID containerId, int slotIndex)
        {
            _containerEntityIdValue = containerId.Value;
            _containerSlotIndex = slotIndex;
            _isInContainer = true;
            _isPickedUp = false;
            _isDropped = false;

            gameObject.SetActive(false);
            MarkDirty();
        }

        /// <summary>
        /// Removes the item from its container.
        /// </summary>
        public void RemoveFromContainer()
        {
            _containerEntityIdValue = 0;
            _containerSlotIndex = -1;
            _isInContainer = false;
            MarkDirty();
        }

        /// <summary>
        /// Transfers ownership to a new entity.
        /// </summary>
        public void TransferOwnership(EntityID newOwner)
        {
            OwnerEntityId = newOwner;
        }

        /// <summary>
        /// Clears ownership.
        /// </summary>
        public void ClearOwnership()
        {
            _ownerEntityIdValue = 0;
            _hasOwner = false;
            MarkDirty();
        }

        /// <summary>
        /// Modifies the stack count.
        /// </summary>
        public int ModifyStack(int delta)
        {
            int oldCount = _stackCount;
            _stackCount = Mathf.Max(0, _stackCount + delta);

            if (_stackCount == 0)
            {
                OnStackDepleted();
            }

            return _stackCount - oldCount;
        }

        /// <summary>
        /// Damages the item's durability.
        /// </summary>
        public void DamageDurability(int amount)
        {
            Durability -= amount;
        }

        /// <summary>
        /// Repairs the item's durability.
        /// </summary>
        public void Repair(int amount)
        {
            Durability += amount;
        }

        #endregion

        #region Events

        protected virtual void OnItemBroken()
        {
            Debug.Log($"[PersistentItem] {EntityID} broke");
            // Override to implement item breaking behavior
        }

        protected virtual void OnStackDepleted()
        {
            Debug.Log($"[PersistentItem] {EntityID} stack depleted");
            // Override to implement stack depletion behavior (usually destruction)
        }

        #endregion

        #region Visibility

        private void UpdateVisibility()
        {
            // Items that are picked up or in containers should be invisible
            bool shouldBeVisible = !_isPickedUp && !_isInContainer;
            gameObject.SetActive(shouldBeVisible);

            if (_collider != null)
            {
                _collider.enabled = shouldBeVisible;
            }
        }

        #endregion
    }
}
