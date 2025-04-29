using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;

namespace Combat
{
    public class Pet : NetworkBehaviour
    {
        [Header("Pet Stats")]
        [SerializeField] private int maxHealth = 100;
        // Refactored SyncVars
        private readonly SyncVar<int> _currentHealth = new SyncVar<int>();
        private readonly SyncVar<bool> _isDefending = new SyncVar<bool>();
        
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer petSprite;
        [SerializeField] private TMPro.TextMeshProUGUI healthText;
        [SerializeField] private GameObject defendIcon;
        
        // Reference to the owner player
        // Renamed SyncVar to avoid conflict
        private readonly SyncVar<NetworkPlayer> _playerOwner = new SyncVar<NetworkPlayer>();
        
        // Public properties to access SyncVar values
        public int CurrentHealth => _currentHealth.Value;
        public bool IsDefending => _isDefending.Value;
        // Renamed property to avoid conflict
        public NetworkPlayer PlayerOwner => _playerOwner.Value;
        
        private void Awake()
        {
            if (defendIcon != null)
                defendIcon.SetActive(false);
                
            // Register OnChange callbacks
            _currentHealth.OnChange += OnHealthChanged;
            _isDefending.OnChange += OnDefendingChanged;
        }

        private void OnDestroy()
        {
             // Unregister callbacks
            _currentHealth.OnChange -= OnHealthChanged;
            _isDefending.OnChange -= OnDefendingChanged;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            _currentHealth.Value = maxHealth;
            _isDefending.Value = false;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            // Initial UI update
            UpdateHealthDisplay(0, _currentHealth.Value, false);
            UpdateDefendIcon(_isDefending.Value);
        }
        
        // Called by server to initialize the pet with its owner
        [Server]
        public void Initialize(NetworkPlayer petOwner)
        {
            // Use the renamed SyncVar
            _playerOwner.Value = petOwner;
            _currentHealth.Value = maxHealth;
        }
        
        // Called when the pet takes damage
        [Server]
        public void TakeDamage(int damage)
        {
            int actualDamage = _isDefending.Value ? Mathf.FloorToInt(damage * 0.5f) : damage;
            _currentHealth.Value = Mathf.Max(_currentHealth.Value - actualDamage, 0);
            
            // Notify clients to animate damage
            RpcAnimateDamage(actualDamage);
            
            // Reset defense status after taking damage
            if (_isDefending.Value)
            {
                _isDefending.Value = false;
            }
            
            if (_currentHealth.Value <= 0)
            {
                // Pet is defeated
                RpcDefeat();
            }
        }
        
        // Called by player to set the pet to defending
        [Server]
        public void SetDefending(bool defending)
        {
            _isDefending.Value = defending;
        }
        
        // Check if the pet is defeated
        [Server]
        public bool IsDefeated()
        {
            return _currentHealth.Value <= 0;
        }
        
        // Animation for taking damage
        [ObserversRpc]
        private void RpcAnimateDamage(int damage)
        {
            // Update health UI (already handled by OnChange)
            // UpdateHealthDisplay();
            
            // Animate damage
            if (petSprite != null)
            {
                // Flash red to indicate damage
                petSprite.DOColor(Color.red, 0.1f).OnComplete(() => 
                {
                    petSprite.DOColor(Color.white, 0.1f);
                });
                
                // Shake the pet
                transform.DOShakePosition(0.3f, 0.3f, 10, 90, false, true);
            }
            
            // Show floating damage number
            ShowDamageNumber(damage);
        }
        
        // Create floating damage text
        private void ShowDamageNumber(int damage)
        {
            // This would be implemented with a damage number prefab
            Debug.Log($"Pet took {damage} damage!");
        }
        
        // Animation for pet defeat
        [ObserversRpc]
        private void RpcDefeat()
        {
            if (petSprite != null)
            {
                // Fade out the pet
                petSprite.DOFade(0, 1f);
                
                // Scale down
                transform.DOScale(0, 1f).SetEase(Ease.InBack);
            }
            
            // Notify CombatManager that pet is defeated
            // Check if CombatManager exists before calling
            if (CombatManager.Instance != null)
                CombatManager.Instance.HandlePetDefeat(this);
        }
        
        // Update the health display (Called by OnChange)
        private void UpdateHealthDisplay(int prev, int next, bool asServer)
        {
            if (healthText != null)
            {
                healthText.text = $"HP: {next}/{maxHealth}";
                
                // Animate health change only if it decreased (took damage)
                if (next < prev)
                {
                    healthText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
                }
            }
        }

        // Update the defense icon display (Called by OnChange)
        private void UpdateDefendIcon(bool prev, bool next, bool asServer)
        {
            UpdateDefendIcon(next);
        }
        
        // Helper to update defend icon directly
        private void UpdateDefendIcon(bool isDefending)
        {
            if (defendIcon != null)
            {
                defendIcon.SetActive(isDefending);
                 // Simple pop animation
                if(isDefending) defendIcon.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 1, 0.5f);
            }
        }
        
        // OnChange callback for _currentHealth
        private void OnHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(prev, next, asServer);
        }
        
        // OnChange callback for _isDefending
        private void OnDefendingChanged(bool prev, bool next, bool asServer)
        {
            UpdateDefendIcon(prev, next, asServer);
        }

        // Removed the override OnSyncVarChanged method as it's obsolete
        // public override void OnSyncVarChanged()
        // {
        //     base.OnSyncVarChanged();
        //     UpdateHealthDisplay();
        // }
    }
} 