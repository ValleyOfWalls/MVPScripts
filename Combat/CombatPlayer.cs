using UnityEngine;
using UnityEngine.UI; // Added for UI components
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using FishNet.Transporting; // For RPCs

namespace Combat
{
    // The main player class during combat
    public class CombatPlayer : NetworkBehaviour, ICombatant
    {
        #region References and Components
        [Header("References")]
        private readonly SyncVar<NetworkPlayer> _networkPlayer = new SyncVar<NetworkPlayer>();
        private CombatPet playerPet;
        private CombatPet opponentPet;
        private PlayerHand playerHand;
        
        // Reference to the player's deck
        private RuntimeDeck playerDeck;
        
        [Header("Combat UI")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private TextMeshProUGUI healthText;
        [SerializeField] private Transform statusEffectsContainer;
        [SerializeField] private Image playerImage; // Changed from SpriteRenderer to Image
        
        // Combat state
        private readonly SyncVar<bool> _isMyTurn = new SyncVar<bool>();
        private readonly SyncVar<int> _currentEnergy = new SyncVar<int>();
        private readonly SyncVar<int> _maxEnergy = new SyncVar<int>(3);
        private readonly SyncVar<int> _currentHealth = new SyncVar<int>(100);
        private readonly SyncVar<int> _maxHealth = new SyncVar<int>(100);
        private readonly SyncVar<bool> _isDefending = new SyncVar<bool>();
        
        // Status effects tracking
        private readonly SyncDictionary<StatusEffectType, StatusEffectData> _statusEffects = new SyncDictionary<StatusEffectType, StatusEffectData>();
        
        // References to combat systems
        private CombatManager combatManager;
        
        // Collision detection for card targeting - using BoxCollider2D with Image
        private BoxCollider2D targetingCollider;
        #endregion

        #region Properties
        // Public properties
        public SyncVar<bool> SyncIsMyTurn => _isMyTurn;
        public SyncVar<int> SyncEnergy => _currentEnergy;
        public SyncVar<int> SyncHealth => _currentHealth;
        public SyncVar<int> SyncMaxHealth => _maxHealth;
        public NetworkPlayer NetworkPlayer => _networkPlayer.Value;
        public NetworkConnection Owner => _networkPlayer.Value != null ? _networkPlayer.Value.Owner : null;
        public bool IsMyTurn => _isMyTurn.Value;
        public int CurrentEnergy => _currentEnergy.Value;
        public int MaxEnergy => _maxEnergy.Value;
        public int CurrentHealth => _currentHealth.Value;
        public int MaxHealth => _maxHealth.Value;
        public bool IsDefending => _isDefending.Value;
        public RuntimeDeck PlayerDeck => playerDeck;
        public IDictionary<StatusEffectType, StatusEffectData> StatusEffects => _statusEffects;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Find UI elements
            FindUIElements();
            
            // Register synced variable change callbacks
            _networkPlayer.OnChange += OnNetworkPlayerChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _isMyTurn.OnChange += OnTurnChanged;
            _currentHealth.OnChange += OnHealthChanged;
            _maxHealth.OnChange += OnMaxHealthChanged;
            _isDefending.OnChange += OnDefendingChanged;
            
            // Get Image component if not set
            if (playerImage == null)
                playerImage = GetComponentInChildren<Image>();
            if (playerImage == null)
                Debug.LogError("CombatPlayer could not find its Image component!");
            
            // Create a visual placeholder
            CreatePlaceholderIfNeeded();
            
            // Get reference to CombatManager
            combatManager = FindObjectOfType<CombatManager>();
            
            // Setup targeting collider
            SetupTargetingCollider();
        }

        private void Start()
        {
            // Make sure image is visible
            if (playerImage != null)
            {
                playerImage.enabled = true;
                
                // Create placeholder if needed
                if (playerImage.sprite == null)
                    CreatePlaceholderIfNeeded();
                
                // Ensure full opacity
                Color color = playerImage.color;
                color.a = 1f;
                playerImage.color = color;
            }
        }

        private void OnDestroy()
        {
            // Unregister synced variable change callbacks
            _networkPlayer.OnChange -= OnNetworkPlayerChanged;
            _currentEnergy.OnChange -= OnEnergyChanged;
            _isMyTurn.OnChange -= OnTurnChanged;
            _currentHealth.OnChange -= OnHealthChanged;
            _maxHealth.OnChange -= OnMaxHealthChanged;
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
            if (playerImage != null)
            {
                RectTransform rectTransform = playerImage.GetComponent<RectTransform>();
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
        #endregion

        #region Network Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Create placeholder if needed
            CreatePlaceholderIfNeeded();
            
            // Setup collider for card targeting
            SetupTargetingCollider();
            
            // Update UI
            UpdateHealthDisplay(_currentHealth.Value, _currentHealth.Value, false);
            UpdateEnergyDisplay(_currentEnergy.Value, _currentEnergy.Value, false);
            UpdateDefendIcon(_isDefending.Value);
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Double-check registration of callbacks
            _isMyTurn.OnChange += OnTurnChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _networkPlayer.OnChange += OnNetworkPlayerChanged;
            _currentHealth.OnChange += OnHealthChanged;
            _maxHealth.OnChange += OnMaxHealthChanged;
            _isDefending.OnChange += OnDefendingChanged;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Initialize health to maximum on server start
            _currentHealth.Value = _maxHealth.Value;
            _currentEnergy.Value = _maxEnergy.Value;
            _isDefending.Value = false;
        }
        #endregion

        #region Initialization and Setup
        private void CreatePlaceholderIfNeeded()
        {
            // Check if player has an image with no sprite
            if (playerImage != null && playerImage.sprite == null)
            {
                // Create a placeholder texture
                Texture2D texture = new Texture2D(128, 128);
                Color[] colors = new Color[128 * 128];
                
                // Fill with green color
                for (int i = 0; i < colors.Length; i++)
                {
                    colors[i] = new Color(0.2f, 0.8f, 0.3f, 1.0f);
                }
                
                // Add a border
                for (int x = 0; x < 128; x++)
                {
                    for (int y = 0; y < 128; y++)
                    {
                        if (x < 3 || x > 124 || y < 3 || y > 124)
                        {
                            colors[y * 128 + x] = new Color(0.1f, 0.4f, 0.1f, 1.0f);
                        }
                    }
                }
                
                texture.SetPixels(colors);
                texture.Apply();
                
                // Create sprite from texture
                Sprite placeholder = Sprite.Create(texture, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
                playerImage.sprite = placeholder;
                
                // Ensure renderer is enabled and visible
                playerImage.enabled = true;

                // --- Re-run collider setup after creating sprite --- 
                SetupTargetingCollider();
            }
        }
        
        // Initialize player with references
        [Server]
        public void Initialize(NetworkPlayer networkPlayer, CombatPet playerPetRef, CombatPet opponentPetRef, PlayerHand playerHandRef, RuntimeDeck deck)
        {
             if (networkPlayer == null) 
             {
                 Debug.LogError("FALLBACK: NetworkPlayer is null in Initialize");
                 return;
             }
             if (playerPetRef == null)
             {
                 Debug.LogError($"FALLBACK: PlayerPetRef is null for {networkPlayer.GetSteamName()}");
             }
             if (opponentPetRef == null)
             { 
                 Debug.LogError($"FALLBACK: OpponentPetRef is null for {networkPlayer.GetSteamName()}");
             }
             if (playerHandRef == null)
             {
                 Debug.LogError($"FALLBACK: PlayerHandRef is null for {networkPlayer.GetSteamName()}");
             }
            
            _networkPlayer.Value = networkPlayer;
            this.playerPet = playerPetRef;
            this.opponentPet = opponentPetRef;
            this.playerHand = playerHandRef;
            this.playerDeck = deck;
            
            // Initialize combat state
            _maxHealth.Value = 100; // Default value for players
            _currentHealth.Value = _maxHealth.Value;
            _maxEnergy.Value = 3;
            _currentEnergy.Value = _maxEnergy.Value;
            _isMyTurn.Value = false;
            _isDefending.Value = false;
            
            // Additional setup after references are assigned
            if (playerNameText != null) 
                playerNameText.text = networkPlayer.GetSteamName();
            
            UpdateEnergyDisplay(0, _currentEnergy.Value, true);
            UpdateHealthDisplay(0, _currentHealth.Value, true);
            UpdateTurnVisuals(false, _isMyTurn.Value, true);
        }
        
        // Find required UI elements, usually called if IsOwner
        private void FindUIElements()
        {
            // Find player name text
            if (playerNameText == null)
            {
                // Try to find among children
                playerNameText = GetComponentInChildren<TextMeshProUGUI>(true);
                if (playerNameText == null)
                    Debug.LogError("FALLBACK: Could not find TextMeshProUGUI component in children");
            }

            // Try to find energy text among children
            if (energyText == null)
            {
                // Try to find among children TextMeshProUGUI components
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach(var text in texts)
                {
                    if (text != playerNameText && text.name.Contains("Energy"))
                    {
                        energyText = text;
                        break;
                    }
                }
                if (energyText == null) 
                    Debug.LogError("FALLBACK: Could not find EnergyText component in children");
            }
            
            // Try to find health text among children
            if (healthText == null)
            {
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach(var text in texts)
                {
                    if (text != playerNameText && text != energyText && text.name.Contains("Health"))
                    {
                        healthText = text;
                        break;
                    }
                }
                if (healthText == null)
                    Debug.LogError("FALLBACK: Could not find HealthText component in children");
            }
        }
        #endregion

        #region Turn Management
        // Start player turn
        [Server]
        public void StartTurn()
        {
            // Set turn state
            SetTurn(true);
            
            // Reset/increase energy
            _currentEnergy.Value = _maxEnergy.Value;
            
            // Process status effects for new turn
            ProcessStatusEffectsForNewTurn();
            
            // Draw cards
            DrawCardsOnTurnStart(5);
            
            // Notify clients explicitly that turn state changed
            RpcNotifyTurnChanged(true);
        }
        
        // Public method for the server (CombatManager) to set the turn state
        [Server]
        public void SetTurn(bool isMyTurn)
        {
            _isMyTurn.Value = isMyTurn;
        }

        // End the player's turn
        [Server]
        public void EndTurn()
        {
            // Clear turn state
            _isMyTurn.Value = false;
            
            // Discard the player's hand
            DiscardHand();
            
            // Notify clients explicitly that turn has ended
            RpcNotifyTurnChanged(false);
            
            // Notify the combat manager that our turn is over
            if (combatManager != null)
            {
                combatManager.PlayerEndedTurn(this);
            }
        }
        
        [Server]
        private void DrawCardsOnTurnStart(int count)
        {
            DrawCards(count);
        }
        
        [ServerRpc(RequireOwnership = true)]
        public void CmdEndTurn()
        {
            // Verify it's their turn
            if (!_isMyTurn.Value)
            {
                Debug.LogWarning($"Player {_networkPlayer.Value.GetSteamName()} tried to end turn when it's not their turn");
                return;
            }
            
            EndTurn();
        }
        #endregion

        #region Card Interactions
        // Called by the server when a card is played by the player
        [Server]
        public void PlayCard(string cardName, CardType cardType)
        {
            // Find the card in the player's hand
            CardData cardData = null;
            
            if (playerHand != null)
            {
                // Use DeckManager.Instance to find card data
                cardData = DeckManager.Instance.FindCardByName(cardName);
            }
            
            if (cardData == null)
            {
                Debug.LogError($"Card {cardName} not found in player's hand");
                return;
            }
            
            // Check if player has enough energy
            int cost = GetCardCostFromName(cardName);
            if (_currentEnergy.Value < cost)
            {
                Debug.LogWarning($"Player {_networkPlayer.Value.GetSteamName()} tried to play {cardName} but doesn't have enough energy. Has: {_currentEnergy.Value}, Needs: {cost}");
                TargetNotifyInsufficientEnergy(Owner);
                return;
            }
            
            // Deduct energy cost
            _currentEnergy.Value -= cost;
            
            // Remove from hand
            if (playerHand != null)
            {
                playerHand.ServerRemoveCard(cardName);
            }
            
            // Notify player the card was played successfully
            TargetCardPlayed(Owner, cardName);
            
            // Process card effects
            if (cardType == CardType.Attack)
            {
                // Apply to opponent's pet
                CardEffectProcessor.ApplyCardEffects(cardData, this, opponentPet);
            }
            else if (cardType == CardType.Skill)
            {
                // Apply to player's own pet
                CardEffectProcessor.ApplyCardEffects(cardData, this, playerPet);
            }
            else
            {
                // Apply to both or based on card targeting
                // Could be extended based on target types in the future
                CardEffectProcessor.ApplyCardEffects(cardData, this, playerPet);
            }
        }

        private int GetCardCostFromName(string cardName)
        {
            // Get the card cost directly from DeckManager
            CardData card = DeckManager.Instance.FindCardByName(cardName);
            return card != null ? card.manaCost : 1; // Default to 1 if not found
        }
        #endregion

        #region Card Drawing and Discard
        [Server]
        public CardData DrawCardFromDeck()
        {
            if (playerDeck == null || playerDeck.DrawPileCount == 0)
            {
                Debug.LogWarning($"FALLBACK: Player {_networkPlayer.Value.GetSteamName()} tried to draw a card but deck is empty or null");
                return null;
            }

            CardData drawnCard = playerDeck.DrawCard();
            if (drawnCard == null)
            {
                Debug.LogWarning($"FALLBACK: Player {_networkPlayer.Value.GetSteamName()} drew a null card");
                return null;
            }

            return drawnCard;
        }
        
        [Server]
        public void DiscardHand()
        {
            if (playerHand != null)
            {
                playerHand.ServerDiscardHand();
            }
        }
        #endregion

        #region Combat Methods
        [Server]
        public void TakeDamage(int amount)
        {
            int actualDamage = _isDefending.Value ? Mathf.FloorToInt(amount * 0.5f) : amount;
            
            // Apply break status effect if present
            if (HasStatusEffect(StatusEffectType.Break))
            {
                float multiplier = 1f + (_statusEffects[StatusEffectType.Break].Value / 100f);
                actualDamage = Mathf.FloorToInt(actualDamage * multiplier);
            }
            
            _currentHealth.Value = Mathf.Max(_currentHealth.Value - actualDamage, 0);
            
            // Show damage animation
            RpcAnimateDamage(actualDamage);
            
            // Check defeat state
            if (_currentHealth.Value <= 0)
            {
                RpcDefeat();
            }
        }
        
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
            if (playerHand != null)
            {
                for (int i = 0; i < amount; i++)
                {
                    CardData card = DrawCardFromDeck();
                    if (card != null)
                    {
                        // Use the server method to add card to hand
                        playerHand.ServerAddCard(card);
                    }
                }
                
                // Notify the player to draw cards UI-wise
            TargetDrawCards(Owner, amount);
        }
        }
        
        [Server]
        public bool IsDefeated()
        {
            return _currentHealth.Value <= 0;
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
            
            // Reset defending status at start of turn
            _isDefending.Value = false;
            
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

        #region ICombatant Implementation
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
            // Based on ownership perspective
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

        #region UI Updates
        private void OnNetworkPlayerChanged(NetworkPlayer prev, NetworkPlayer next, bool asServer)
        {
            if (next != null && playerNameText != null)
            {
                playerNameText.text = next.GetSteamName();
            }
        }
        
        private void OnEnergyChanged(int prev, int next, bool asServer)
        {
            UpdateEnergyDisplay(prev, next, asServer);
        }

        private void OnTurnChanged(bool prev, bool next, bool asServer)
        {
            UpdateTurnVisuals(prev, next, asServer);
        }
        
        private void OnHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(prev, next, asServer);
        }
        
        private void OnMaxHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(_currentHealth.Value, _currentHealth.Value, asServer);
        }
        
        private void OnDefendingChanged(bool prev, bool next, bool asServer)
        {
            UpdateDefendIcon(next);
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
        
        private void UpdateHealthDisplay(int prev, int next, bool asServer)
        {
            if (healthText != null)
            {
                healthText.text = $"HP: {next}/{_maxHealth.Value}";
                
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
        
        private void UpdateDefendIcon(bool isDefending)
        {
            // If we have a defend icon, update its visibility
            GameObject defendIcon = transform.Find("DefendIcon")?.gameObject;
            if (defendIcon != null)
            {
                defendIcon.SetActive(isDefending);
            }
        }
        
        private void UpdateTurnVisuals(bool prev, bool next, bool asServer)
        {
            // Visual indication of turn state
            if (playerImage != null)
            {
                // Highlight player when it's their turn
                if (next)
                {
                    // Update glow or highlight effect for UI Image
                    // For Image, we'll use DOColor to animate the color property
                    playerImage.DOColor(new Color(1f, 1f, 1f, 1f), 0.5f).SetLoops(-1, LoopType.Yoyo);
                }
                else
                {
                    // Stop any animations and return to normal
                    DOTween.Kill(playerImage);
                    playerImage.color = new Color(1f, 1f, 1f, 0.8f);
                }
            }
        }
        
        [ObserversRpc]
        private void RpcAnimateDamage(int damage)
        {
            // Animate the UI image to show damage
            if (playerImage != null)
            {
                // Flash red for UI Image
                playerImage.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
                
                // Shake the transform
                playerImage.transform.DOShakePosition(0.2f, 0.1f, 10, 90, false, true);
                }
                
            // Show damage number
            ShowDamageNumber(damage);
        }
        
        private void ShowDamageNumber(int damage)
        {
            // Create a floating damage number that works with UI
            if (playerImage != null && playerImage.transform.parent != null)
            {
                GameObject damageObj = new GameObject($"Damage_{damage}");
                // Make the damage number a sibling of the player image to get proper layering
                damageObj.transform.SetParent(playerImage.transform.parent);
                
                // Position it over the player
                RectTransform damageRectTransform = damageObj.AddComponent<RectTransform>();
                damageRectTransform.anchoredPosition = playerImage.rectTransform.anchoredPosition + new Vector2(0, 50f);
                
                // Add text component
                TextMeshProUGUI damageText = damageObj.AddComponent<TextMeshProUGUI>();
                damageText.text = damage.ToString();
                damageText.fontSize = 24;
                damageText.color = Color.red;
                damageText.alignment = TextAlignmentOptions.Center;
                
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
            if (playerImage != null)
            {
                // Fade out image
                playerImage.DOFade(0, 0.5f);
                
                // Shrink down the RectTransform
                playerImage.rectTransform.DOScale(0.1f, 0.5f).OnComplete(() =>
                {
                    // Disable image after animation
                    playerImage.enabled = false;
                });
            }
            
            // Disable collider
            if (targetingCollider != null)
            {
                targetingCollider.enabled = false;
            }
        }
        #endregion

        #region Client RPCs
        [TargetRpc]
        private void TargetDrawCards(NetworkConnection conn, int count)
        {
            // Handle UI card drawing
            if (playerHand == null)
            {
                StartCoroutine(FindHandAndDrawCards(count));
            }
            else
            {
                // Call client-side animation method
                playerHand.ClientAnimateCardDraw(count);
            }
        }
        
        private System.Collections.IEnumerator FindHandAndDrawCards(int count)
        {
            // Wait for a moment to allow references to be found
            yield return new WaitForSeconds(0.5f);
            
            // Try to find PlayerHand in the scene that we own
            PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
            bool foundOwnedHand = false;
            
            foreach (PlayerHand hand in hands)
            {
                if (hand.IsOwner)
                {
                    playerHand = hand;
                    foundOwnedHand = true;
                    break;
                }
            }
            
            if (foundOwnedHand && playerHand != null)
            {
                // Use visual animation method if available, otherwise just receive the cards
                if (playerHand.CanAnimateCardDraw())
                {
                    playerHand.ClientAnimateCardDraw(count);
                }
            }
            else
            {
                Debug.LogError("Failed to find owned PlayerHand after delay, cannot animate drawing cards");
            }
        }
        
        [TargetRpc]
        private void TargetCardPlayed(NetworkConnection conn, string cardName)
        {
            // Visual feedback when a card is played
            if (playerHand != null)
            {
                playerHand.ClientAnimateCardPlayed(cardName);
            }
        }

        [TargetRpc]
        private void TargetNotifyInsufficientEnergy(NetworkConnection conn)
        {
            // Visual feedback when player doesn't have enough energy
            if (energyText != null)
            {
                energyText.color = Color.red;
                energyText.transform.DOShakePosition(0.5f, 0.2f);
                energyText.DOColor(Color.white, 0.5f);
        }
        }
        
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
            if (parentNetworkObject != null)
             {
                // Set the transform parent
                transform.SetParent(parentNetworkObject.transform);
                
                // Reset local position and scale
                transform.localPosition = Vector3.zero;
                transform.localScale = Vector3.one;
             }
             else
             {
                // Unparent if parent is null
                transform.SetParent(null);
             }
        }

        [ObserversRpc]
        public void RpcNotifyTurnChanged(bool isMyTurn)
        {
            // Visual notification of turn change
            if (isMyTurn && IsOwner)
            {
                // Show turn start notification to owner
                Debug.Log("Your turn started!");
                
                // You would add more visual feedback here
            }
        }
        #endregion
    }
}