using UnityEngine;
using SavingSystem.Core;

namespace SavingSystem.Entities
{
    /// <summary>
    /// Example Interactive persistent entity implementation.
    /// Demonstrates how to create an interactive object (door, switch) that saves/loads properly.
    /// </summary>
    public class PersistentInteractive : PersistentEntityBase
    {
        #region Inspector Fields

        [Header("Interactive State")]
        [SerializeField] private bool _isOpen;
        [SerializeField] private bool _isLocked;
        [SerializeField] private string _lockKeyId = "";

        [Header("Animation")]
        [SerializeField] private float _animationProgress;
        [SerializeField] private bool _isAnimating;
        [SerializeField] private float _animationSpeed = 1f;

        [Header("Usage")]
        [SerializeField] private int _useCount;
        [SerializeField] private int _maxUses = -1; // -1 = unlimited

        [Header("Linked Objects")]
        [SerializeField] private PersistentInteractive[] _linkedObjects;

        #endregion

        #region Private Fields

        private long _lastUsedTimestamp;
        private Animator _animator;
        private static readonly int OpenHash = Animator.StringToHash("IsOpen");

        #endregion

        #region Properties

        public bool IsOpen
        {
            get => _isOpen;
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    MarkDirty();
                    OnStateChanged();
                }
            }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { _isLocked = value; MarkDirty(); }
        }

        public string LockKeyId
        {
            get => _lockKeyId;
            set { _lockKeyId = value; MarkDirty(); }
        }

        public int UseCount => _useCount;

        public bool CanUse => _maxUses < 0 || _useCount < _maxUses;

        #endregion

        #region Unity Lifecycle

        protected override void Awake()
        {
            base.Awake();
            _animator = GetComponent<Animator>();
        }

        protected override void Start()
        {
            base.Start();
            ApplyState();
        }

        private void Update()
        {
            if (_isAnimating)
            {
                UpdateAnimation();
            }
        }

        #endregion

        #region State Capture/Restore

        public override EntityStateData CaptureState()
        {
            InteractiveStateData state = new InteractiveStateData();

            // Base state
            PopulateBaseState(state);

            // Interactive state
            state.IsOpen = _isOpen;
            state.IsLocked = _isLocked;
            state.LockKeyID = _lockKeyId;

            // Animation
            state.AnimationProgress = _animationProgress;
            state.IsAnimating = _isAnimating;

            // Usage
            state.UseCount = _useCount;
            state.LastUsedTimestamp = _lastUsedTimestamp;

            // Linked objects
            if (_linkedObjects != null && _linkedObjects.Length > 0)
            {
                state.LinkedEntityIDs = new ulong[_linkedObjects.Length];
                for (int i = 0; i < _linkedObjects.Length; i++)
                {
                    if (_linkedObjects[i] != null)
                    {
                        state.LinkedEntityIDs[i] = _linkedObjects[i].EntityID.Value;
                    }
                }
            }

            return state;
        }

        public override void RestoreState(EntityStateData state)
        {
            if (state is not InteractiveStateData interactiveState)
            {
                Debug.LogError($"[PersistentInteractive] Invalid state data type: {state.GetType()}");
                return;
            }

            // Base state
            RestoreBaseState(state);

            // Interactive state
            _isOpen = interactiveState.IsOpen;
            _isLocked = interactiveState.IsLocked;
            _lockKeyId = interactiveState.LockKeyID;

            // Animation
            _animationProgress = interactiveState.AnimationProgress;
            _isAnimating = interactiveState.IsAnimating;

            // Usage
            _useCount = interactiveState.UseCount;
            _lastUsedTimestamp = interactiveState.LastUsedTimestamp;

            // Apply visual state immediately
            ApplyState();
        }

        #endregion

        #region Interaction

        /// <summary>
        /// Attempts to use/interact with this object.
        /// </summary>
        public bool TryInteract(EntityID interactor = default)
        {
            if (!CanUse)
            {
                OnMaxUsesReached();
                return false;
            }

            if (_isLocked)
            {
                OnLockedInteraction();
                return false;
            }

            _useCount++;
            _lastUsedTimestamp = System.DateTime.UtcNow.Ticks;
            MarkDirty();

            Toggle();
            return true;
        }

        /// <summary>
        /// Attempts to unlock with a key ID.
        /// </summary>
        public bool TryUnlock(string keyId)
        {
            if (!_isLocked)
                return true;

            if (string.IsNullOrEmpty(_lockKeyId) || _lockKeyId == keyId)
            {
                _isLocked = false;
                MarkDirty();
                OnUnlocked();
                return true;
            }

            OnUnlockFailed();
            return false;
        }

        /// <summary>
        /// Locks the object.
        /// </summary>
        public void Lock(string keyId = null)
        {
            _isLocked = true;
            if (keyId != null)
            {
                _lockKeyId = keyId;
            }
            MarkDirty();
        }

        /// <summary>
        /// Unlocks the object without requiring a key.
        /// </summary>
        public void ForceUnlock()
        {
            _isLocked = false;
            MarkDirty();
            OnUnlocked();
        }

        /// <summary>
        /// Toggles the open/closed state.
        /// </summary>
        public void Toggle()
        {
            IsOpen = !_isOpen;
        }

        /// <summary>
        /// Opens the object.
        /// </summary>
        public void Open()
        {
            if (!_isOpen)
            {
                IsOpen = true;
            }
        }

        /// <summary>
        /// Closes the object.
        /// </summary>
        public void Close()
        {
            if (_isOpen)
            {
                IsOpen = false;
            }
        }

        #endregion

        #region Animation

        protected virtual void OnStateChanged()
        {
            StartAnimation();
            TriggerLinkedObjects();
        }

        protected virtual void StartAnimation()
        {
            _isAnimating = true;
            _animationProgress = 0f;

            if (_animator != null)
            {
                _animator.SetBool(OpenHash, _isOpen);
            }
        }

        protected virtual void UpdateAnimation()
        {
            _animationProgress += Time.deltaTime * _animationSpeed;

            if (_animationProgress >= 1f)
            {
                _animationProgress = 1f;
                _isAnimating = false;
                OnAnimationComplete();
            }
        }

        protected virtual void OnAnimationComplete()
        {
            // Override for custom behavior when animation completes
        }

        protected virtual void ApplyState()
        {
            // Apply visual state immediately (skip animation)
            if (_animator != null)
            {
                _animator.SetBool(OpenHash, _isOpen);
                // Force animation to end state
                _animator.Update(0f);
            }
        }

        #endregion

        #region Linked Objects

        /// <summary>
        /// Triggers all linked objects (for switches that control doors, etc.)
        /// </summary>
        protected virtual void TriggerLinkedObjects()
        {
            if (_linkedObjects == null)
                return;

            foreach (PersistentInteractive linked in _linkedObjects)
            {
                if (linked != null)
                {
                    linked.OnLinkedTrigger(this);
                }
            }
        }

        /// <summary>
        /// Called when a linked object triggers this one.
        /// </summary>
        protected virtual void OnLinkedTrigger(PersistentInteractive source)
        {
            // Default behavior: match state of source
            IsOpen = source.IsOpen;
        }

        #endregion

        #region Events

        protected virtual void OnLockedInteraction()
        {
            Debug.Log($"[PersistentInteractive] {EntityID} is locked");
            // Override for locked interaction feedback (sound, UI message)
        }

        protected virtual void OnUnlocked()
        {
            Debug.Log($"[PersistentInteractive] {EntityID} unlocked");
            // Override for unlock feedback
        }

        protected virtual void OnUnlockFailed()
        {
            Debug.Log($"[PersistentInteractive] {EntityID} unlock failed - wrong key");
            // Override for failed unlock feedback
        }

        protected virtual void OnMaxUsesReached()
        {
            Debug.Log($"[PersistentInteractive] {EntityID} max uses reached");
            // Override for max uses feedback
        }

        #endregion
    }
}
