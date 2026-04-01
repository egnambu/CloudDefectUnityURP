using UnityEngine;
using SavingSystem.Core;

namespace SavingSystem.Entities
{
    /// <summary>
    /// Example NPC persistent entity implementation.
    /// Demonstrates how to create a data-driven NPC that saves/loads properly.
    /// </summary>
    public class PersistentNPC : PersistentEntityBase
    {
        #region Inspector Fields

        [Header("NPC Appearance")]
        [SerializeField] private int _headIndex;
        [SerializeField] private int _bodyIndex;
        [SerializeField] private int _clothingIndex;
        [SerializeField] private int _hairIndex;
        [SerializeField] private int _accessoryIndex;

        [SerializeField] private Color _skinColor = Color.white;
        [SerializeField] private Color _hairColor = Color.black;
        [SerializeField] private Color _clothingColor = Color.gray;

        [Header("NPC Stats")]
        [SerializeField] private float _health = 100f;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private bool _isHostile;
        [SerializeField] private bool _isAlerted;

        [Header("AI/Behavior")]
        [SerializeField] private string _currentTaskId = "";
        [SerializeField] private string _aiStateId = "Idle";
        [SerializeField] private float _taskProgress;

        [Header("Faction")]
        [SerializeField] private string _factionId = "Neutral";
        [SerializeField] private int _reputationWithPlayer;

        [Header("Schedule")]
        [SerializeField] private int _currentScheduleIndex;
        [SerializeField] private float _scheduleTimer;

        #endregion

        #region Properties

        public int HeadIndex
        {
            get => _headIndex;
            set { _headIndex = value; MarkDirty(); OnAppearanceChanged(); }
        }

        public int BodyIndex
        {
            get => _bodyIndex;
            set { _bodyIndex = value; MarkDirty(); OnAppearanceChanged(); }
        }

        public int ClothingIndex
        {
            get => _clothingIndex;
            set { _clothingIndex = value; MarkDirty(); OnAppearanceChanged(); }
        }

        public float Health
        {
            get => _health;
            set
            {
                float oldHealth = _health;
                _health = Mathf.Clamp(value, 0f, _maxHealth);
                if (_health != oldHealth)
                {
                    MarkDirty();
                    OnHealthChanged(_health, oldHealth);
                }
            }
        }

        public float MaxHealth
        {
            get => _maxHealth;
            set { _maxHealth = value; MarkDirty(); }
        }

        public bool IsHostile
        {
            get => _isHostile;
            set { _isHostile = value; MarkDirty(); }
        }

        public bool IsAlerted
        {
            get => _isAlerted;
            set { _isAlerted = value; MarkDirty(); }
        }

        public string CurrentTaskId
        {
            get => _currentTaskId;
            set { _currentTaskId = value; MarkDirty(); }
        }

        public string AIStateId
        {
            get => _aiStateId;
            set { _aiStateId = value; MarkDirty(); }
        }

        public string FactionId
        {
            get => _factionId;
            set { _factionId = value; MarkDirty(); }
        }

        public int ReputationWithPlayer
        {
            get => _reputationWithPlayer;
            set { _reputationWithPlayer = value; MarkDirty(); }
        }

        #endregion

        #region Unity Lifecycle

        protected override void Start()
        {
            base.Start();
            // Apply initial appearance
            ApplyAppearance();
        }

        private void Update()
        {
            // Update position tracking (call when NPC moves)
            UpdatePositionTracking();
        }

        #endregion

        #region State Capture/Restore

        public override EntityStateData CaptureState()
        {
            NPCStateData state = new NPCStateData();

            // Base state
            PopulateBaseState(state);

            // Appearance
            state.HeadIndex = _headIndex;
            state.BodyIndex = _bodyIndex;
            state.ClothingIndex = _clothingIndex;
            state.HairIndex = _hairIndex;
            state.AccessoryIndex = _accessoryIndex;
            state.SkinColor = _skinColor;
            state.HairColor = _hairColor;
            state.ClothingColor = _clothingColor;

            // Stats
            state.Health = _health;
            state.MaxHealth = _maxHealth;
            state.IsHostile = _isHostile;
            state.IsAlerted = _isAlerted;

            // AI/Behavior
            state.CurrentTaskID = _currentTaskId;
            state.AIStateID = _aiStateId;
            state.TaskProgress = _taskProgress;

            // Faction
            state.FactionID = _factionId;
            state.ReputationWithPlayer = _reputationWithPlayer;

            // Schedule
            state.CurrentScheduleIndex = _currentScheduleIndex;
            state.ScheduleTimer = _scheduleTimer;

            return state;
        }

        public override void RestoreState(EntityStateData state)
        {
            if (state is not NPCStateData npcState)
            {
                Debug.LogError($"[PersistentNPC] Invalid state data type: {state.GetType()}");
                return;
            }

            // Base state
            RestoreBaseState(state);

            // Appearance
            _headIndex = npcState.HeadIndex;
            _bodyIndex = npcState.BodyIndex;
            _clothingIndex = npcState.ClothingIndex;
            _hairIndex = npcState.HairIndex;
            _accessoryIndex = npcState.AccessoryIndex;
            _skinColor = npcState.SkinColor;
            _hairColor = npcState.HairColor;
            _clothingColor = npcState.ClothingColor;

            // Stats
            _health = npcState.Health;
            _maxHealth = npcState.MaxHealth;
            _isHostile = npcState.IsHostile;
            _isAlerted = npcState.IsAlerted;

            // AI/Behavior
            _currentTaskId = npcState.CurrentTaskID;
            _aiStateId = npcState.AIStateID;
            _taskProgress = npcState.TaskProgress;

            // Faction
            _factionId = npcState.FactionID;
            _reputationWithPlayer = npcState.ReputationWithPlayer;

            // Schedule
            _currentScheduleIndex = npcState.CurrentScheduleIndex;
            _scheduleTimer = npcState.ScheduleTimer;

            // Apply restored appearance
            ApplyAppearance();

            // Restore AI state
            RestoreAIState();
        }

        #endregion

        #region Appearance

        /// <summary>
        /// Applies the current appearance settings to the NPC visuals.
        /// Override this to implement your appearance system.
        /// </summary>
        protected virtual void ApplyAppearance()
        {
            // Example implementation - override in your actual NPC class
            // This would typically:
            // 1. Set mesh/skinned mesh renderer materials
            // 2. Swap head/body/clothing mesh parts
            // 3. Apply color to materials

            // Example:
            // _characterCustomizer.SetHead(_headIndex);
            // _characterCustomizer.SetBody(_bodyIndex);
            // _characterCustomizer.SetClothing(_clothingIndex);
            // _characterCustomizer.SetSkinColor(_skinColor);
        }

        protected virtual void OnAppearanceChanged()
        {
            ApplyAppearance();
        }

        #endregion

        #region Health

        protected virtual void OnHealthChanged(float newHealth, float oldHealth)
        {
            // Handle health change
            if (newHealth <= 0f && oldHealth > 0f)
            {
                OnDeath();
            }
        }

        protected virtual void OnDeath()
        {
            // Handle NPC death
            // This might trigger destruction or state change
            Debug.Log($"[PersistentNPC] {EntityID} died");
        }

        public void TakeDamage(float damage)
        {
            Health -= damage;
        }

        public void Heal(float amount)
        {
            Health += amount;
        }

        #endregion

        #region AI

        /// <summary>
        /// Restores AI state after loading.
        /// Override to implement your AI system integration.
        /// </summary>
        protected virtual void RestoreAIState()
        {
            // Example implementation - override in your actual NPC class
            // This would typically:
            // 1. Set AI state machine to saved state
            // 2. Resume task if one was active
            // 3. Restore schedule progress

            // Example:
            // _aiController.SetState(_aiStateId);
            // if (!string.IsNullOrEmpty(_currentTaskId))
            //     _taskSystem.ResumeTask(_currentTaskId, _taskProgress);
        }

        /// <summary>
        /// Sets the current AI task.
        /// </summary>
        public void SetTask(string taskId, float progress = 0f)
        {
            _currentTaskId = taskId;
            _taskProgress = progress;
            MarkDirty();
        }

        /// <summary>
        /// Updates task progress.
        /// </summary>
        public void UpdateTaskProgress(float progress)
        {
            _taskProgress = progress;
            // Only mark dirty occasionally to avoid excessive saves
            // In a real implementation, you might batch these
        }

        #endregion
    }
}
