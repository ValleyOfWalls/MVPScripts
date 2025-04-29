using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;

namespace Combat
{
    public class Pet : NetworkBehaviour, ICombatant
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
        public int MaxHealth => maxHealth;
        public bool IsDefending => _isDefending.Value;
        // Renamed property to avoid conflict
        public NetworkPlayer PlayerOwner => _playerOwner.Value;
        
        private SpriteRenderer spriteRenderer;
        
        private void Awake()
        {
            // Get sprite renderer if not set
            if (petSprite == null)
                petSprite = GetComponent<SpriteRenderer>();
            
            spriteRenderer = petSprite != null ? petSprite : GetComponent<SpriteRenderer>();
            
            // Register OnChange callbacks
            _currentHealth.OnChange += OnHealthChanged;
            _isDefending.OnChange += OnDefendingChanged;
        }

        private void Start()
        {
            // Create placeholder if no sprite assigned
            CreatePlaceholderIfNeeded();
            
            // Make sure the sprite is visible
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                Color currentColor = spriteRenderer.color;
                currentColor.a = 1f;
                spriteRenderer.color = currentColor;
            }
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
            
            // Log that Pet is initializing on client
            Debug.Log($"Pet OnStartClient - SpriteRenderer: {(spriteRenderer != null ? "Found" : "Missing")}, Sprite: {(spriteRenderer != null && spriteRenderer.sprite != null ? "Assigned" : "Missing")}");
            
            // Ensure sprite is created
            CreatePlaceholderIfNeeded();
        }
        
        // Called by server to initialize the pet with its owner
        [Server]
        public void Initialize(NetworkPlayer petOwner)
        {
            // Use the renamed SyncVar
            _playerOwner.Value = petOwner;
            _currentHealth.Value = maxHealth;
            
            // Log initialization
            Debug.Log($"Pet initialized with owner {petOwner.GetSteamName()}, health: {_currentHealth.Value}");
        }
        
        // Called when the pet takes damage
        [Server]
        public void TakeDamage(int damage)
        {
            Debug.Log($"Pet TakeDamage called - Current Health: {_currentHealth.Value}, Damage: {damage}, IsDefending: {_isDefending.Value}");
            
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

        private void CreatePlaceholderIfNeeded()
        {
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                Debug.LogWarning("No sprite assigned to Pet - creating placeholder");
                
                // Create a placeholder sprite
                Texture2D texture = new Texture2D(128, 128);
                Color[] colors = new Color[128 * 128];
                
                // Fill with blue color
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(0.3f, 0.5f, 0.9f, 1.0f);
                }
                
                // Add a border
                for (int x = 0; x < 128; x++)
                {
                    for (int y = 0; y < 128; y++)
                    {
                        if (x < 3 || x > 124 || y < 3 || y > 124)
                        {
                            colors[y * 128 + x] = new Color(0.1f, 0.2f, 0.5f, 1.0f);
                        }
                    }
                }
                
                texture.SetPixels(colors);
                texture.Apply();
                
                // Create sprite from texture
                Sprite placeholder = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
                spriteRenderer.sprite = placeholder;
                
                // Ensure sprite renderer is enabled and visible
                spriteRenderer.enabled = true;
                Color color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
                
                Debug.Log("Created placeholder sprite for Pet");
            }
        }

        // --- ICombatant Implementation ---

        [Server]
        public void AddBlock(int amount)
        {
            // Pets typically don't use block, but implement the interface method
            Debug.LogWarning("Pet.AddBlock called, but not implemented.");
            // Optionally, set defending state or a temporary shield?
            // SetDefending(true); // Example: Treat block as defense
        }

        [Server]
        public void Heal(int amount)
        {
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, maxHealth);
            Debug.Log($"Pet healed for {amount}, current health: {_currentHealth.Value}");
            // Optionally, add visual feedback for healing
        }

        [Server]
        public void DrawCards(int amount)
        {
            // Pets don't have decks/hands
            Debug.LogWarning("Pet.DrawCards called, but pets cannot draw cards.");
        }

        [Server]
        public void ApplyBuff(int buffId)
        {
            // Implement buff logic based on buffId
            Debug.LogWarning($"Pet.ApplyBuff({buffId}) called, but buff system not implemented.");
            // Example: Apply a speed buff, attack buff, etc.
        }

        [Server]
        public void ApplyDebuff(int debuffId)
        {
            // Implement debuff logic based on debuffId
            Debug.LogWarning($"Pet.ApplyDebuff({debuffId}) called, but debuff system not implemented.");
            // Example: Apply slow, weakness, vulnerability etc.
        }

        public bool IsEnemy()
        {
            // Pets are typically allies of the player
            return false;
        }

        // --- End ICombatant Implementation ---
    }
} 