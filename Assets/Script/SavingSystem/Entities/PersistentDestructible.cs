using UnityEngine;
using SavingSystem.Core;

namespace SavingSystem.Entities
{
    /// <summary>
    /// Example Destructible persistent entity implementation.
    /// Demonstrates how to create a destructible object that saves/loads properly.
    /// </summary>
    public class PersistentDestructible : PersistentEntityBase
    {
        #region Inspector Fields

        [Header("Destructible Stats")]
        [SerializeField] private float _health = 100f;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private int _damageStage;
        [SerializeField] private int _maxDamageStages = 3;

        [Header("Destruction")]
        [SerializeField] private bool _isFullyDestroyed;
        [SerializeField] private bool _hasSpawnedDebris;
        [SerializeField] private string _debrisConfigId = "";

        [Header("Debris")]
        [SerializeField] private GameObject _debrisPrefab;
        [SerializeField] private float _debrisLifetime = 30f;

        [Header("Damage Stage Visuals")]
        [SerializeField] private GameObject[] _damageStageObjects;

        #endregion

        #region Properties

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
                    UpdateDamageStage();
                    if (_health <= 0f && oldHealth > 0f)
                    {
                        OnDestroyed();
                    }
                }
            }
        }

        public float MaxHealth
        {
            get => _maxHealth;
            set { _maxHealth = value; MarkDirty(); }
        }

        public int DamageStage
        {
            get => _damageStage;
            private set
            {
                if (_damageStage != value)
                {
                    _damageStage = value;
                    MarkDirty();
                    ApplyDamageStageVisuals();
                }
            }
        }

        public bool IsFullyDestroyed => _isFullyDestroyed;

        #endregion

        #region Unity Lifecycle

        protected override void Start()
        {
            base.Start();
            ApplyDamageStageVisuals();
        }

        #endregion

        #region State Capture/Restore

        public override EntityStateData CaptureState()
        {
            DestructibleStateData state = new DestructibleStateData();

            // Base state
            PopulateBaseState(state);

            // Destructible data
            state.Health = _health;
            state.MaxHealth = _maxHealth;
            state.DamageStage = _damageStage;
            state.IsFullyDestroyed = _isFullyDestroyed;
            state.HasSpawnedDebris = _hasSpawnedDebris;
            state.DebrisConfigID = _debrisConfigId;

            return state;
        }

        public override void RestoreState(EntityStateData state)
        {
            if (state is not DestructibleStateData destructibleState)
            {
                Debug.LogError($"[PersistentDestructible] Invalid state data type: {state.GetType()}");
                return;
            }

            // Base state
            RestoreBaseState(state);

            // Destructible data
            _maxHealth = destructibleState.MaxHealth;
            _health = destructibleState.Health;
            _damageStage = destructibleState.DamageStage;
            _isFullyDestroyed = destructibleState.IsFullyDestroyed;
            _hasSpawnedDebris = destructibleState.HasSpawnedDebris;
            _debrisConfigId = destructibleState.DebrisConfigID;

            // Apply visual state
            ApplyDamageStageVisuals();

            // If fully destroyed, hide the object
            if (_isFullyDestroyed)
            {
                gameObject.SetActive(false);
            }
        }

        #endregion

        #region Damage

        /// <summary>
        /// Applies damage to the destructible.
        /// </summary>
        public void TakeDamage(float damage)
        {
            if (_isFullyDestroyed)
                return;

            Health -= damage;
        }

        /// <summary>
        /// Repairs the destructible.
        /// </summary>
        public void Repair(float amount)
        {
            if (_isFullyDestroyed)
                return;

            Health += amount;
        }

        /// <summary>
        /// Fully repairs and restores the destructible.
        /// </summary>
        public void FullRestore()
        {
            _isFullyDestroyed = false;
            _hasSpawnedDebris = false;
            _health = _maxHealth;
            _damageStage = 0;
            gameObject.SetActive(true);
            ApplyDamageStageVisuals();
            MarkDirty();
        }

        /// <summary>
        /// Updates the damage stage based on health percentage.
        /// </summary>
        private void UpdateDamageStage()
        {
            if (_maxDamageStages <= 0 || _maxHealth <= 0)
                return;

            float healthPercent = _health / _maxHealth;
            int newStage = Mathf.FloorToInt((1f - healthPercent) * _maxDamageStages);
            newStage = Mathf.Clamp(newStage, 0, _maxDamageStages - 1);

            DamageStage = newStage;
        }

        #endregion

        #region Destruction

        /// <summary>
        /// Called when health reaches zero.
        /// </summary>
        protected virtual void OnDestroyed()
        {
            _isFullyDestroyed = true;
            MarkDirty();

            // Spawn debris if configured
            if (!_hasSpawnedDebris && _debrisPrefab != null)
            {
                SpawnDebris();
            }

            // Hide main object
            gameObject.SetActive(false);

            Debug.Log($"[PersistentDestructible] {EntityID} destroyed");
        }

        /// <summary>
        /// Spawns debris at the destruction point.
        /// </summary>
        protected virtual void SpawnDebris()
        {
            if (_debrisPrefab == null)
                return;

            GameObject debris = Instantiate(_debrisPrefab, transform.position, transform.rotation);
            _hasSpawnedDebris = true;

            // Optionally destroy debris after lifetime
            if (_debrisLifetime > 0)
            {
                Destroy(debris, _debrisLifetime);
            }
        }

        /// <summary>
        /// Immediately destroys the object.
        /// </summary>
        public void InstantDestroy()
        {
            _health = 0;
            OnDestroyed();
        }

        #endregion

        #region Visuals

        /// <summary>
        /// Applies the correct visual state based on damage stage.
        /// </summary>
        protected virtual void ApplyDamageStageVisuals()
        {
            if (_damageStageObjects == null || _damageStageObjects.Length == 0)
                return;

            // Show only the current damage stage object
            for (int i = 0; i < _damageStageObjects.Length; i++)
            {
                if (_damageStageObjects[i] != null)
                {
                    _damageStageObjects[i].SetActive(i == _damageStage);
                }
            }
        }

        #endregion
    }
}
