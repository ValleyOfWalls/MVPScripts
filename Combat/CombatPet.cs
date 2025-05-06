using UnityEngine;
using UnityEngine.UI; // Added for UI components
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using DG.Tweening;
using FishNet.Transporting; // Needed for ObserversRpc
using System.Linq;

namespace Combat
{
    public class CombatPet : NetworkBehaviour, ICombatant
    {
        #region Properties and Fields
        [Header("Combat Stats")]
        private readonly SyncVar<int> _currentHealth = new SyncVar<int>();
        private readonly SyncVar<int> _maxHealth = new SyncVar<int>();
        private readonly SyncVar<int> _currentEnergy = new SyncVar<int>();
        private readonly SyncVar<int> _maxEnergy = new SyncVar<int>(2); // Default max energy for pets
        private readonly SyncVar<bool> _isDefending = new SyncVar<bool>();
        
        // Status effects tracking
        private readonly SyncDictionary<StatusEffectType, StatusEffectData> _statusEffects = new SyncDictionary<StatusEffectType, StatusEffectData>();
        
        // Public accessor for health SyncVar needed by UI
        public SyncVar<int> SyncHealth => _currentHealth;
        public SyncVar<int> SyncMaxHealth => _maxHealth;
        public SyncVar<int> SyncEnergy => _currentEnergy;
        public SyncVar<int> SyncMaxEnergy => _maxEnergy;
        
        [Header("References")]
        [SerializeField] private Pet referencePet; // The persistent pet this combat pet represents
        [SerializeField] private PetHand petHand;
        [SerializeField] private Image petImage; // Changed from SpriteRenderer to Image
        [SerializeField] private TMPro.TextMeshProUGUI healthText;
        [SerializeField] private TMPro.TextMeshProUGUI energyText;
        [SerializeField] private GameObject defendIcon;
        [SerializeField] private Transform statusEffectsContainer;
        
        // Runtime deck for combat
        private RuntimeDeck petDeck;
        
        // Collision detection for card targeting
        private BoxCollider2D targetingCollider;
        
        // Properties
        public int CurrentHealth => _currentHealth.Value;
        public int MaxHealth => _maxHealth.Value;
        public int CurrentEnergy => _currentEnergy.Value;
        public int MaxEnergy => _maxEnergy.Value;
        public bool IsDefending => _isDefending.Value;
        public Pet ReferencePet => referencePet;
        public RuntimeDeck PetDeck => petDeck;
        public IDictionary<StatusEffectType, StatusEffectData> StatusEffects => _statusEffects;
        #endregion

        #region Unity Lifecycle Methods
        private void Awake()
        {
            // Register OnChange callbacks
            _currentHealth.OnChange += OnHealthChanged;
            _maxHealth.OnChange += OnMaxHealthChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _isDefending.OnChange += OnDefendingChanged;
            
            // Get Image component if not set
            if (petImage == null)
                petImage = GetComponentInChildren<Image>();
            if (petImage == null)
                Debug.LogError("CombatPet could not find its Image component!");
                
            // Create or get targeting collider
            SetupTargetingCollider();
            
            // Create placeholder sprite if needed
            CreatePlaceholderIfNeeded();
        }
        
        private void OnDestroy()
        {
            // Unregister callbacks
            _currentHealth.OnChange -= OnHealthChanged;
            _maxHealth.OnChange -= OnMaxHealthChanged;
            _currentEnergy.OnChange -= OnEnergyChanged;
            _isDefending.OnChange -= OnDefendingChanged;
        }
        
        private void SetupTargetingCollider()
        {
            // Create or get targeting collider for card drops
            targetingCollider = GetComponent<BoxCollider2D>();
            if (targetingCollider == null)
            {
                targetingCollider = gameObject.AddComponent<BoxCollider2D>();
            }
            
            // Configure collider for targeting
            targetingCollider.isTrigger = true;
            
            // Size based on RectTransform if available
            if (petImage != null)
            {
                RectTransform rectTransform = petImage.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    // Convert rect size to world space for the collider
                    Vector2 size = rectTransform.rect.size;
                    // Account for scaling if needed
                    size.x *= rectTransform.lossyScale.x;
                    size.y *= rectTransform.lossyScale.y;
                    
                    targetingCollider.size = size;
                    
                    // Offset might be needed depending on pivot and anchors
                    // This is a simplified version - might need adjustment based on the actual layout
                    Vector2 offset = Vector2.zero;
                    targetingCollider.offset = offset;
                }
                else
                {
                    // Fallback if no RectTransform found
                    targetingCollider.size = new Vector2(2f, 2f);
                    targetingCollider.offset = Vector2.zero;
                }
            }
            else
            {
                // Default size if no Image
                targetingCollider.size = new Vector2(2f, 2f);
                targetingCollider.offset = Vector2.zero;
            }
        }
        
        private void CreatePlaceholderIfNeeded()
        {
            // Ensure we have an Image component
            if (petImage == null) 
            {
                petImage = GetComponentInChildren<Image>();
                if (petImage == null) 
                {
                    Debug.LogError("Cannot create placeholder, Image component not found!");
                    return; // Exit if no Image found
                }
            }
            
            // Check if the image has no sprite assigned
            if (petImage.sprite == null)
            {
                // Create a placeholder texture
                Texture2D texture = new Texture2D(128, 128);
                Color[] colors = new Color[128 * 128];
                
                // Fill with a reddish color for pet
                Color fillColor = new Color(0.8f, 0.3f, 0.3f, 1.0f);
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = fillColor;
                }
                
                // Add a darker border
                Color borderColor = new Color(0.5f, 0.1f, 0.1f, 1.0f);
                for (int x = 0; x < 128; x++)
                {
                    for (int y = 0; y < 128; y++)
                    {
                        if (x < 3 || x > 124 || y < 3 || y > 124)
                        {
                            colors[y * 128 + x] = borderColor;
                        }
                    }
                }
                
                texture.SetPixels(colors);
                texture.Apply();
                
                // Create sprite from texture
                Sprite placeholder = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
                petImage.sprite = placeholder;
                
                // Ensure renderer is enabled and visible
                petImage.enabled = true;
                
                // --- Re-run collider setup after creating sprite --- 
                SetupTargetingCollider(); 
            }
        }
        #endregion

        #region Network Callbacks
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Set initial state
            if (referencePet != null)
            {
                _maxHealth.Value = referencePet.MaxHealth;
                _currentHealth.Value = _maxHealth.Value;
                _maxEnergy.Value = 2; // Default energy for pets
                _currentEnergy.Value = _maxEnergy.Value;
            }
            else
            {
                _maxHealth.Value = 100; // Default value
                _currentHealth.Value = _maxHealth.Value;
                _maxEnergy.Value = 2;
                _currentEnergy.Value = _maxEnergy.Value;
            }
            _isDefending.Value = false;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Initial UI update for all clients
            UpdateHealthDisplay(_currentHealth.Value, _currentHealth.Value, false);
            UpdateEnergyDisplay(_currentEnergy.Value, _currentEnergy.Value, false);
            UpdateDefendIcon(_isDefending.Value);
            
            // Set visual appearance from reference pet if available
            if (referencePet != null && petImage != null)
            {
                if (referencePet.PetSprite != null)
                {
                    petImage.sprite = referencePet.PetSprite;
                }
            }
            
            // Create placeholder if needed (runs before FindReferencesOnClient)
            CreatePlaceholderIfNeeded();
            
            // Try to find references if they're missing on client (for ALL clients)
            if (referencePet == null || petHand == null)
            {
                StartCoroutine(FindReferencesOnClient());
            }
            
            // Make sure targeting collider is set up (might run again if placeholder was created)
            SetupTargetingCollider();
        }
        
        private System.Collections.IEnumerator FindReferencesOnClient()
        {
            // Wait a bit for network sync to complete - try more times with longer interval
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(0.3f);
                
                // Try to find the references via the player's synced combat reference
                NetworkPlayer[] players = FindObjectsByType<NetworkPlayer>(FindObjectsSortMode.None);
                foreach (var player in players)
                {
                    // Check if this is the reference pet for any player
                    if (player.SyncedCombatPet.Value == this)
                    {
                        // Get reference pet from the player
                        referencePet = player.playerPet.Value;
                        
                        // Also get the pet hand
                        petHand = player.SyncedPetHand.Value;
                        
                        Debug.Log($"[CLIENT] Found referencePet:{referencePet != null} and petHand:{petHand != null} for CombatPet on client");
                        
                        if (referencePet != null && petHand != null)
                        {
                            // Update sprite if needed
                            if (petImage != null && referencePet.PetSprite != null)
                            {
                                petImage.sprite = referencePet.PetSprite;
                            }
                            yield break;
                        }
                    }
                }
                
                // Alternative approach: Find the NetworkPlayer that owns this pet via parent structure
                if (referencePet == null)
                {
                    // Find all pets in the scene
                    Pet[] allPets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
                    foreach (var pet in allPets)
                    {
                        if (pet != null && pet.transform.IsChildOf(transform.parent))
                        {
                            referencePet = pet;
                            Debug.Log($"[CLIENT] Found referencePet:{referencePet != null} via parent structure");
                            break;
                        }
                    }
                }
                
                // If we found the reference pet but not the hand, try to find the hand
                if (referencePet != null && petHand == null)
                {
                    PetHand[] allHands = FindObjectsByType<PetHand>(FindObjectsSortMode.None);
                    foreach (var hand in allHands)
                    {
                        // Try to find a hand that belongs to the same pet owner
                        if (hand != null && referencePet.PlayerOwner != null && 
                            hand.gameObject.name.Contains(referencePet.PlayerOwner.GetSteamName()))
                        {
                            petHand = hand;
                            Debug.Log($"[CLIENT] Found petHand:{petHand != null} via name matching");
                            break;
                        }
                    }
                }
                
                // If we've found both references, we can exit
                if (referencePet != null && petHand != null)
                {
                    yield break;
                }
            }
            
            Debug.LogWarning($"[CLIENT] Failed to find referencePet:{referencePet != null} or petHand:{petHand != null} after multiple attempts");
        }
        #endregion

        #region Initialization
        [Server]
        public void Initialize(Pet parentPet)
        {
            if (parentPet == null)
            {
                Debug.LogError("FALLBACK: Initialize called with null parentPet");
                return;
            }

            referencePet = parentPet;
            
            // Set initial health based on pet's max health
            _maxHealth.Value = referencePet.MaxHealth;
            _currentHealth.Value = _maxHealth.Value;
            _maxEnergy.Value = 2; // Default energy for pets
            _currentEnergy.Value = _maxEnergy.Value;
            _isDefending.Value = false;
        }

        [Server]
        public void AssignHand(PetHand hand)
        {
             petHand = hand;
        }
        
        [Server]
        public void CreateRuntimeDeck()
        {
            // Create a new empty runtime deck
            petDeck = new RuntimeDeck("Pet Combat Deck", DeckType.PetDeck);
            
            // Check if pet has an initialized deck
            if (referencePet != null && referencePet.persistentDeckCardIDs.Count > 0)
            {
                // Populate deck from persistent deck IDs
                foreach (string cardID in referencePet.persistentDeckCardIDs)
                {
                    CardData cardData = DeckManager.Instance.FindCardByName(cardID);
                    if (cardData != null)
                    {
                        petDeck.AddCard(cardData);
                    }
                    else
                    {
                        Debug.LogWarning($"FALLBACK: Could not find CardData for {cardID}");
                    }
                }
                
                // Shuffle the deck
                petDeck.Shuffle();
            }
            else
            {
                // Fallback to starter deck
                Debug.LogWarning("FALLBACK: No persistent deck found, using starter deck");
                if (DeckManager.Instance != null)
                {
                    int petIndex = 0;
                    if (referencePet != null && referencePet.PlayerOwner != null)
                    {
                        petIndex = referencePet.PlayerOwner.GetInstanceID();
                    }
                    petDeck = DeckManager.Instance.GetPetStarterDeck(petIndex);
                }
            }
        }
        #endregion

        #region Combat Methods
        [Server]
        public void TakeDamage(int damage)
        {
            int actualDamage = _isDefending.Value ? Mathf.FloorToInt(damage * 0.5f) : damage;
            
            // Check for break status effect which increases damage taken
            if (HasStatusEffect(StatusEffectType.Break))
            {
                float multiplier = 1f + (_statusEffects[StatusEffectType.Break].Value / 100f);
                actualDamage = Mathf.FloorToInt(actualDamage * multiplier);
            }
            
            _currentHealth.Value = Mathf.Max(_currentHealth.Value - actualDamage, 0);
            
            // Animate damage on all clients
            RpcAnimateDamage(actualDamage);
            
            // Check defeat state
            if (_currentHealth.Value <= 0)
            {
                RpcDefeat();
            }
        }
        
        [Server]
        public void SetDefending(bool defending)
        {
            _isDefending.Value = defending;
        }
        
        [Server]
        public bool IsDefeated()
        {
            return _currentHealth.Value <= 0;
        }
        
        [Server]
        public void SetEnergy(int energy)
        {
            _currentEnergy.Value = Mathf.Clamp(energy, 0, _maxEnergy.Value);
        }
        
        [Server]
        public void ChangeEnergy(int amount)
        {
            _currentEnergy.Value = Mathf.Clamp(_currentEnergy.Value + amount, 0, _maxEnergy.Value);
        }
        
        [Server]
        public void UseEnergy(int amount)
        {
            _currentEnergy.Value = Mathf.Max(_currentEnergy.Value - amount, 0);
        }
        #endregion

        #region Status Effects
        [Server]
        public void ApplyStatusEffect(StatusEffectType type, int value, int duration)
        {
            if (_statusEffects.ContainsKey(type))
            {
                // Update existing effect
                StatusEffectData existingEffect = _statusEffects[type];
                existingEffect.Value = Mathf.Max(existingEffect.Value, value);
                existingEffect.Duration = Mathf.Max(existingEffect.Duration, duration);
                _statusEffects[type] = existingEffect;
            }
            else
            {
                // Add new effect
                _statusEffects.Add(type, new StatusEffectData { Value = value, Duration = duration });
            }
            
            // Notify clients to update status effect display
            RpcUpdateStatusEffects();
        }
        
        [Server]
        public void RemoveStatusEffect(StatusEffectType type)
        {
            if (_statusEffects.ContainsKey(type))
            {
                _statusEffects.Remove(type);
                
                // Notify clients to update status effect display
                RpcUpdateStatusEffects();
            }
        }
        
        [Server]
        public bool HasStatusEffect(StatusEffectType type)
        {
            return _statusEffects.ContainsKey(type) && _statusEffects[type].Duration > 0;
        }
        
        [Server]
        public void ProcessStatusEffectsForNewTurn()
        {
            List<StatusEffectType> effectsToRemove = new List<StatusEffectType>();
            
            // Process each status effect
            foreach (var kvp in _statusEffects)
            {
                StatusEffectType type = kvp.Key;
                StatusEffectData data = kvp.Value;
                
                // Reduce duration
                data.Duration--;
                
                // Apply effect based on type
                switch (type)
                {
                    case StatusEffectType.DoT:
                        // Apply damage over time
                        int dotDamage = data.Value;
                        _currentHealth.Value = Mathf.Max(_currentHealth.Value - dotDamage, 0);
                        RpcAnimateDamage(dotDamage);
                        break;
                        
                    // Other status effect processing as needed
                }
                
                // Mark for removal if duration reached zero
                if (data.Duration <= 0)
                {
                    effectsToRemove.Add(type);
                }
                else
                {
                    // Update duration
                    _statusEffects[type] = data;
                }
            }
            
            // Remove expired effects
            foreach (StatusEffectType type in effectsToRemove)
            {
                _statusEffects.Remove(type);
            }
            
            // Notify clients to update status effect display
            if (effectsToRemove.Count > 0)
            {
                RpcUpdateStatusEffects();
            }
            
            // Check defeat state after DoT effects
            if (_currentHealth.Value <= 0)
            {
                RpcDefeat();
            }
        }
        
        [ObserversRpc]
        private void RpcUpdateStatusEffects()
        {
            // Update status effect display on clients
            UpdateStatusEffectsDisplay();
        }
        
        private void UpdateStatusEffectsDisplay()
        {
            // Find or create a single status effect text display
            TMPro.TextMeshProUGUI statusText = null;
            
            // Clear existing status effect elements
            if (statusEffectsContainer != null)
            {
                // Check if we already have a status text element
                statusText = statusEffectsContainer.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                
                // Clear any old elements
                foreach (Transform child in statusEffectsContainer)
                {
                    Destroy(child.gameObject);
                }
                
                // Create a new game object for our status text if needed
                GameObject statusObj = new GameObject("StatusEffectsText");
                statusObj.transform.SetParent(statusEffectsContainer, false);
                
                // Add text component if it doesn't exist
                statusText = statusObj.AddComponent<TMPro.TextMeshProUGUI>();
                statusText.fontSize = 12;
                statusText.alignment = TMPro.TextAlignmentOptions.Center;
                
                // Build a string of all current status effects
                string statusString = "";
                
                // Check if we have any status effects
                if (_statusEffects.Count > 0)
                {
                    foreach (var kvp in _statusEffects)
                    {
                        // Add each status effect to the string
                        if (statusString.Length > 0)
                            statusString += ", "; // Add comma between effects
                            
                        statusString += $"{kvp.Key}: {kvp.Value.Value} ({kvp.Value.Duration})";
                    }
                }
                else
                {
                    statusString = "No effects";
                }
                
                // Set the text
                statusText.text = statusString;
            }
        }
        #endregion

        #region UI Updates
        [ObserversRpc]
        private void RpcAnimateDamage(int damage)
        {
            // Animate the UI image to show damage
            if (petImage != null)
            {
                // Flash red for UI Image
                petImage.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
                
                // Shake the transform
                petImage.transform.DOShakePosition(0.2f, 0.1f, 10, 90, false, true);
            }
            
            // Show damage number
            ShowDamageNumber(damage);
        }
        
        // Create floating damage text
        private void ShowDamageNumber(int damage)
        {
            // Create a floating damage number that works with UI
            if (petImage != null && petImage.transform.parent != null)
            {
                GameObject damageObj = new GameObject($"Damage_{damage}");
                // Make the damage number a sibling of the pet image to get proper layering
                damageObj.transform.SetParent(petImage.transform.parent);
                
                // Position it over the pet
                RectTransform damageRectTransform = damageObj.AddComponent<RectTransform>();
                damageRectTransform.anchoredPosition = petImage.rectTransform.anchoredPosition + new Vector2(0, 50f);
                
                // Add text component
                TMPro.TextMeshProUGUI damageText = damageObj.AddComponent<TMPro.TextMeshProUGUI>();
                damageText.text = damage.ToString();
                damageText.fontSize = 24;
                damageText.color = Color.red;
                damageText.alignment = TMPro.TextAlignmentOptions.Center;
                
                // Animate the text floating up and fading
                damageRectTransform.DOAnchorPosY(damageRectTransform.anchoredPosition.y + 100f, 1f);
                damageText.DOFade(0, 1f).OnComplete(() => {
                    Destroy(damageObj);
                });
            }
        }
        
        [ObserversRpc]
        private void RpcDefeat()
        {
            // Visual defeat effect for UI Image
            if (petImage != null)
            {
                // Fade out image
                petImage.DOFade(0, 0.5f);
                
                // Shrink down the RectTransform
                petImage.rectTransform.DOScale(0.1f, 0.5f).OnComplete(() =>
                {
                    // Disable image after animation
                    petImage.enabled = false;
                });
            }
            
            // Disable collider
            if (targetingCollider != null)
            {
                targetingCollider.enabled = false;
            }
            
            // Notify CombatManager that pet is defeated
            if (CombatManager.Instance != null)
                CombatManager.Instance.HandlePetDefeat(this);
        }
        
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
            if (parentNetworkObject == null)
            {
                Debug.LogError("FALLBACK: RpcSetParent received null parentNetworkObject");
                return;
            }
            
            Transform parentTransform = parentNetworkObject.transform;
            if (parentTransform != null)
            {
                transform.SetParent(parentTransform, false);
            }
            else
            {
                Debug.LogError("FALLBACK: Could not find transform for parent NetworkObject in RpcSetParent");
            }
        }
        
        private void UpdateHealthDisplay(int prev, int next, bool asServer)
        {
            if (healthText != null)
            {
                healthText.text = $"{next}/{_maxHealth.Value}";
                
                // Visual feedback for health changes
                if (next < prev)
                {
                    // Health decreased
                    healthText.color = Color.red;
                    healthText.DOColor(Color.white, 0.5f);
                }
                else if (next > prev)
                {
                    // Health increased
                    healthText.color = Color.green;
                    healthText.DOColor(Color.white, 0.5f);
                }
            }
        }
        
        private void UpdateEnergyDisplay(int prev, int next, bool asServer)
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {next}/{_maxEnergy.Value}";
                
                // Visual feedback for energy changes
                if (next < prev)
                {
                    // Energy decreased
                    energyText.color = Color.yellow;
                    energyText.DOColor(Color.white, 0.5f);
                }
                else if (next > prev)
                {
                    // Energy increased
                    energyText.color = Color.cyan;
                    energyText.DOColor(Color.white, 0.5f);
                }
            }
        }
        
        private void UpdateDefendIcon(bool isDefending)
        {
            if (defendIcon != null)
            {
                defendIcon.SetActive(isDefending);
            }
        }
        
        private void OnHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(prev, next, asServer);
        }
        
        private void OnMaxHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(_currentHealth.Value, _currentHealth.Value, asServer);
        }
        
        private void OnEnergyChanged(int prev, int next, bool asServer)
        {
            UpdateEnergyDisplay(prev, next, asServer);
        }
        
        private void OnDefendingChanged(bool prev, bool next, bool asServer)
        {
            UpdateDefendIcon(next);
        }
        #endregion

        #region ICombatant Implementation
        [Server]
        public void AddBlock(int amount)
        {
            _isDefending.Value = true;
        }
        
        [Server]
        public void Heal(int amount)
        {
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, _maxHealth.Value);
        }
        
        [Server]
        public void DrawCards(int amount)
        {
            if (petHand != null)
            {
                // Use the PetHand's DrawCards method to handle the logic
                // This ensures PetHand.DrawCards handles all card creation consistently
                petHand.DrawCards(amount);
            }
            else
            {
                Debug.LogError("FALLBACK: Cannot draw cards - petHand is null");
            }
        }
        
        [Server]
        public CardData DrawCardFromDeck()
        {
            if (petDeck == null)
            {
                Debug.LogError("[DIAGNOSTIC] CombatPet cannot draw card: petDeck is null");
                return null;
            }

            // First check if we need to reshuffle the discard pile
            if (petDeck.DrawPileCount == 0 && petDeck.NeedsReshuffle())
            {
                Debug.Log($"[DIAGNOSTIC] CombatPet {this.name} needs to reshuffle their discard pile. Discard pile has {petDeck.DiscardPileCount} cards.");
                petDeck.Reshuffle();
            }

            // Draw from the actual RuntimeDeck
            CardData drawnCard = petDeck.DrawCard();
            
            // Handle results
            if (drawnCard == null)
            {
                // Try one more explicit reshuffle if needed
                if (petDeck.NeedsReshuffle())
                {
                    Debug.Log($"[DIAGNOSTIC] CombatPet {this.name} attempting explicit reshuffle after drawing null card");
                    petDeck.Reshuffle();
                    drawnCard = petDeck.DrawCard(); // Try drawing again
                }
                
                if (drawnCard == null) // If still null after reshuffling, log error
                {
                    Debug.LogError("[DIAGNOSTIC] CombatPet no cards available, deck empty and cannot reshuffle");
                    return null;
                }
            }

            Debug.Log($"[DIAGNOSTIC] CombatPet {this.name} drew card: {drawnCard.cardName}");
            return drawnCard;
        }
        
        [Server]
        public void ApplyBuff(int buffId)
        {
            // Convert to appropriate status effect
            switch(buffId)
            {
                case 0: // Critical
                    ApplyStatusEffect(StatusEffectType.Critical, 25, 2); // 25% crit chance for 2 turns
                    break;
                case 1: // Thorns
                    ApplyStatusEffect(StatusEffectType.Thorns, 3, 2); // Return 3 damage for 2 turns
                    break;
                // Add more buffs as needed
            }
        }
        
        [Server]
        public void ApplyDebuff(int debuffId)
        {
            // Convert to appropriate status effect
            switch(debuffId)
            {
                case 0: // Break
                    ApplyStatusEffect(StatusEffectType.Break, 25, 2); // Take 25% more damage for 2 turns
                    break;
                case 1: // Weak
                    ApplyStatusEffect(StatusEffectType.Weak, 25, 2); // Deal 25% less damage for 2 turns
                    break;
                case 2: // DoT
                    ApplyStatusEffect(StatusEffectType.DoT, 3, 3); // Take 3 damage per turn for 3 turns
                    break;
                // Add more debuffs as needed
            }
        }
        
        public bool IsEnemy()
        {
            // Determine if this pet is an enemy based on the connection owner
            // This is used by the card targeting system
            if (IsOwner)
            {
                return false; // Not an enemy to the owner
            }
            else
            {
                return true; // An enemy to other players
            }
        }
        #endregion

        #region Turn Management
        [Server]
        public void StartTurn()
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.StartTurn called for {this.name} (NetworkObject ID: {NetworkObject.ObjectId})");
            _isDefending.Value = true; // Mark as the currently active pet
            
            // Note: We don't draw cards at the start of pet's turn anymore
            // Cards should be drawn at the start of combat round (player turn)
            
            // Take AI turn after a short delay
            StartCoroutine(TakeTurnAfterDelay(1.0f));
        }

        // Coroutine to introduce a delay before AI actions
        private System.Collections.IEnumerator TakeTurnAfterDelay(float delay)
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.TakeTurnAfterDelay started for {this.name}, waiting {delay} seconds");
            yield return new WaitForSeconds(delay);
            
            Debug.Log($"[DIAGNOSTIC] CombatPet.TakeTurnAfterDelay delay completed, calling TakeTurn for {this.name}");
            // Take the AI turn
            TakeTurn();
        }

        // Execute the AI turn logic
        [Server]
        private void TakeTurn()
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.TakeTurn called for {this.name}");
            
            // Simple AI: Play all cards in hand
            if (petHand != null)
            {
                // Get cards in the pet's hand
                List<Card> cardsInHand = petHand.GetCardsInHand();
                Debug.Log($"[DIAGNOSTIC] CombatPet has {cardsInHand.Count} cards in hand");
                
                if (cardsInHand.Count > 0)
                {
                    string cardNames = string.Join(", ", cardsInHand.Select(c => c?.CardName ?? "NULL_CARD"));
                    Debug.Log($"[DIAGNOSTIC] CombatPet cards in hand: {cardNames}");
                }
                
                // Play each card with a delay
                StartCoroutine(PlayCardsSequentially(cardsInHand));
            }
            else
            {
                Debug.LogError("FALLBACK: Cannot take turn - petHand is null");
                // End turn immediately since there's nothing to do
                EndTurn();
            }
        }

        // Find the opponent's pet (keep existing logic)
        private CombatPet FindOpponentPet()
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.FindOpponentPet called for {this.name}");
            // Actually, we don't need to target other pets - we only target players
            // This is kept only for backward compatibility but returns null
            Debug.Log($"[DIAGNOSTIC] As per design, pets only target players, not other pets");
            return null;
        }

        // Add a helper to find the opponent player
        private CombatPlayer FindOpponentPlayer()
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.FindOpponentPlayer called for {this.name}");
            CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            Debug.Log($"[DIAGNOSTIC] Found {players.Length} total players in scene");
            
            // Try to find through owned relationship first
            Debug.Log($"[DIAGNOSTIC] Trying to find opponent via owner relationship");
            foreach(CombatPlayer player in players)
            {
                // Check if this player owns this pet
                if(player.NetworkPlayer != null && player.NetworkPlayer.CombatPet == this)
                {
                    Debug.Log($"[DIAGNOSTIC] Found owner player: {player.name}");
                    // If so, find *their* opponent
                    if(player.NetworkPlayer.OpponentNetworkPlayer != null)
                    {
                        Debug.Log($"[DIAGNOSTIC] Found opponent player: {player.NetworkPlayer.OpponentNetworkPlayer.name}");
                        return player.NetworkPlayer.OpponentNetworkPlayer.CombatPlayer;
                    }
                    else
                    {
                        Debug.LogWarning($"[DIAGNOSTIC] Found owner player {player.name} but they have no OpponentNetworkPlayer");
                    }
                }
            }
            
            // Fallback: find any player that doesn't own this pet
            Debug.Log($"[DIAGNOSTIC] Falling back to finding any player that doesn't own this pet");
            foreach(CombatPlayer player in players)
            {
                if(player.NetworkPlayer != null && player.NetworkPlayer.CombatPet != this)
                {
                    Debug.Log($"[DIAGNOSTIC] Found fallback opponent player: {player.name}");
                    return player;
                }
            }
            Debug.LogWarning($"[DIAGNOSTIC] Could not find any opponent player for {this.name}");
            return null;
        }

        // Play cards one after another with delay
        private System.Collections.IEnumerator PlayCardsSequentially(List<Card> cards)
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.PlayCardsSequentially called for {this.name} with {cards?.Count ?? 0} cards");
            
            if (cards == null || cards.Count == 0)
            {
                Debug.Log($"[DIAGNOSTIC] CombatPet has no cards to play, ending turn immediately");
                EndTurn();
                yield break;
            }
            
            // Make a COPY of the cards list to avoid modification issues during iteration
            List<Card> cardsCopy = new List<Card>();
            foreach (Card card in cards)
            {
                if (card != null && card.Data != null && card.gameObject.activeInHierarchy) 
                {
                    cardsCopy.Add(card);
                }
            }
            
            Debug.Log($"[DIAGNOSTIC] CombatPet has {cardsCopy.Count} valid cards to play (after filtering nulls)");
            
            if (cardsCopy.Count == 0)
            {
                Debug.Log($"[DIAGNOSTIC] CombatPet has no valid cards to play after filtering, ending turn immediately");
                EndTurn();
                yield break;
            }
            
            // Find ONLY player targets - pets should always target players in combat
            CombatPlayer targetPlayer = FindOpponentPlayer(); 
            
            // We ONLY use player targets, never pet targets
            ICombatant target = targetPlayer;
            
            if (target == null)
            {
                Debug.LogError("[DIAGNOSTIC] Pet AI could not find a valid player target! Ending turn.");
                EndTurn();
                yield break;
            }
            
            Debug.Log($"[DIAGNOSTIC] CombatPet targeting player: {targetPlayer.name}");

            int cardsPlayed = 0;
            // Use the copied list to avoid modification issues
            for (int i = 0; i < cardsCopy.Count; i++)
            {
                Card card = cardsCopy[i];
                
                // Double-check the card is still valid (may have been destroyed by another operation)
                if (card == null || card.Data == null || !card.gameObject.activeInHierarchy) 
                {
                    Debug.LogWarning($"[DIAGNOSTIC] Pet AI skipping card at index {i} - card is null, inactive, or has null data");
                    continue; // Skip this card
                }

                string cardName = card.CardName;
                Debug.Log($"[DIAGNOSTIC] CombatPet playing card {cardName} (card {cardsPlayed+1}/{cardsCopy.Count})");
                
                try
                {
                    // Use the CardEffectProcessor to apply the card's effects
                    CardEffectProcessor.ApplyCardEffects(card.Data, this, target); 
                    
                    // Get the NetworkObject reference BEFORE removing from hand (to avoid null refs)
                    NetworkObject cardNetworkObject = card.NetworkObject;
                    
                    // Remove card from hand (server-side) - using a safe method that checks for nulls
                    if (petHand != null)
                    {
                        Debug.Log($"[DIAGNOSTIC] CombatPet removing card {cardName} from hand");
                        petHand.RemoveCard(card);
                        cardsPlayed++;
                    }
                    else
                    {
                        Debug.LogError($"[DIAGNOSTIC] PetHand is null when trying to remove card {cardName}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[DIAGNOSTIC] Error playing card {cardName}: {ex.Message}");
                }
                
                Debug.Log($"[DIAGNOSTIC] CombatPet waiting before playing next card");
                // Wait before playing next card
                yield return new WaitForSeconds(1.0f);
            }
            
            Debug.Log($"[DIAGNOSTIC] CombatPet played {cardsPlayed} cards, ending turn");
            // End turn after all cards played
            EndTurn();
        }

        // End the pet's turn
        [Server]
        private void EndTurn()
        {
            Debug.Log($"[DIAGNOSTIC] CombatPet.EndTurn called for {this.name}");
            
            // Discard remaining cards
            if (petHand != null)
            {
                Debug.Log($"[DIAGNOSTIC] CombatPet discarding hand at end of turn");
                
                // Check if the gameObject is active before calling ServerDiscardHand to prevent coroutine errors
                if (petHand.gameObject.activeInHierarchy)
                {
                    petHand.ServerDiscardHand();
                }
                else
                {
                    Debug.LogWarning($"[DIAGNOSTIC] CombatPet.EndTurn: PetHand gameObject is inactive, skipping ServerDiscardHand");
                }
            }
            else
            {
                Debug.LogWarning($"[DIAGNOSTIC] CombatPet.EndTurn: petHand is null, cannot discard hand");
            }
            
            // Mark as no longer the active pet
            _isDefending.Value = false;
            
            // Notify combat manager that turn is over
            if (CombatManager.Instance != null)
            {
                Debug.Log($"[DIAGNOSTIC] CombatPet notifying CombatManager that turn is over");
                CombatManager.Instance.PetEndedTurn(this);
            }
            else
            {
                Debug.LogError($"[DIAGNOSTIC] CombatManager.Instance is null, cannot notify pet turn ended");
            }
        }
        #endregion
    }
} 