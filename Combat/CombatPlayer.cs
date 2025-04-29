using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using TMPro;
using System.Collections;

namespace Combat
{
    // The main player class during combat
    public class CombatPlayer : NetworkBehaviour, ICombatant
    {
        [Header("References")]
        // Changed networkPlayer to a SyncVar
        private readonly SyncVar<NetworkPlayer> _networkPlayer = new SyncVar<NetworkPlayer>();
        [SerializeField] private Pet playerPet;
        [SerializeField] private Pet opponentPet;
        [SerializeField] private PlayerHand playerHand;
        
        [Header("Combat UI")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI energyText;
        [SerializeField] private UnityEngine.UI.Button endTurnButton;
        
        // Combat state (Refactored)
        private readonly SyncVar<bool> _isMyTurn = new SyncVar<bool>();
        private readonly SyncVar<int> _currentEnergy = new SyncVar<int>();
        private readonly SyncVar<int> _maxEnergy = new SyncVar<int>(3); // Initialize default value
        
        // Public properties
        public NetworkPlayer NetworkPlayer => _networkPlayer.Value;
        public bool IsMyTurn => _isMyTurn.Value;
        public int CurrentEnergy => _currentEnergy.Value;
        public int MaxEnergy => _maxEnergy.Value;
        
        // References to combat systems
        private CombatManager combatManager;
        private SpriteRenderer spriteRenderer;
        private bool isInitialized = false;
        
        private void Awake()
        {
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
                
            // Register OnChange callbacks
            _networkPlayer.OnChange += OnNetworkPlayerChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _isMyTurn.OnChange += OnTurnChanged;
            
            // Get sprite renderer
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            Debug.Log($"CombatPlayer Awake - SpriteRenderer: {(spriteRenderer != null ? "Found" : "Missing")}");
        }

        private void Start()
        {
            // Make sure sprite is visible
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                
                // Create placeholder if needed
                if (spriteRenderer.sprite == null)
                    CreatePlaceholderIfNeeded();
                
                // Ensure full opacity
                Color color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
                
                Debug.Log($"CombatPlayer Start - Sprite: {(spriteRenderer.sprite != null ? "Assigned" : "Missing")}, Alpha: {color.a}");
            }
        }

        private void OnDestroy()
        {
            // Unregister callbacks
            _networkPlayer.OnChange -= OnNetworkPlayerChanged;
            _currentEnergy.OnChange -= OnEnergyChanged;
            _isMyTurn.OnChange -= OnTurnChanged;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            Debug.Log($"CombatPlayer OnStartClient - IsOwner: {IsOwner}, NetworkPlayer: {(_networkPlayer.Value != null ? _networkPlayer.Value.GetSteamName() : "null")}");
            
            // Check if we need placeholders
            CreatePlaceholderIfNeeded();
            
            // Update visuals that don't depend on _networkPlayer yet
            // Player name will be set by OnNetworkPlayerChanged callback
            
            // Only show the end turn button to the owner
            if (endTurnButton != null)
                endTurnButton.gameObject.SetActive(IsOwner);
            
            // Initialize energy display & turn button state
            // Check if _networkPlayer is already set (might happen if value arrives before OnStartClient)
            if (_networkPlayer.Value != null)
            {
                OnNetworkPlayerChanged(null, _networkPlayer.Value, false);
            }
            UpdateEnergyDisplay(0, _currentEnergy.Value, false);
            UpdateTurnVisuals(false, _isMyTurn.Value, false);
            
            // ALWAYS schedule a delayed reference search to ensure connections are made
            Invoke("FindCombatReferences", 0.5f);
            
            // But also try to find immediate references if possible
            if (IsOwner)
            {
                // Try to find references in parent or siblings first
                if (transform.parent != null)
                {
                    // Attempt to find hand
                    if (playerHand == null)
                    {
                        PlayerHand[] hands = transform.parent.GetComponentsInChildren<PlayerHand>();
                        foreach (PlayerHand hand in hands)
                        {
                            if (hand.IsOwner)
                            {
                                playerHand = hand;
                                Debug.Log($"Found owned PlayerHand in parent's children: {hand.name}");
                                break;
                            }
                        }
                    }

                    // Attempt to find pets
                    if (playerPet == null || opponentPet == null)
                    {
                        Pet[] allPets = transform.parent.GetComponentsInChildren<Pet>();
                        if (allPets.Length > 0)
                        {
                            Debug.Log($"Found {allPets.Length} pets in parent's children.");
                            
                            // If we have network player information, use it to match
                            if (_networkPlayer.Value != null)
                            {
                                foreach (Pet pet in allPets)
                                {
                                    if (pet.PlayerOwner != null)
                                    {
                                        if (pet.PlayerOwner.GetSteamName() == _networkPlayer.Value.GetSteamName())
                                        {
                                            playerPet = pet;
                                            Debug.Log($"Assigned playerPet: {pet.name}");
                                        }
                                    }
                                }
                            }
                            
                            // If we found player pet but not opponent pet, assign any non-matching pet
                            if (playerPet != null && opponentPet == null && allPets.Length >= 2)
                            {
                                foreach (Pet pet in allPets)
                                {
                                    if (pet != playerPet)
                                    {
                                        opponentPet = pet;
                                        Debug.Log($"Assigned opponentPet: {pet.name}");
                                        break;
                                    }
                                }
                            }
                            // If we didn't find any pets with matching players but have exactly 2, just assign them
                            else if (playerPet == null && opponentPet == null && allPets.Length == 2)
                            {
                                playerPet = allPets[0];
                                opponentPet = allPets[1];
                                Debug.Log($"Assigned pets by index - playerPet: {playerPet.name}, opponentPet: {opponentPet.name}");
                            }
                        }
                    }
                }
                
                // Log the state of our references
                int foundCount = (playerHand != null ? 1 : 0) + (playerPet != null ? 1 : 0) + (opponentPet != null ? 1 : 0);
                if (foundCount == 3)
                {
                    Debug.Log($"Successfully found all combat references during OnStartClient!");
                }
                else
                {
                    Debug.Log($"Found {foundCount}/3 combat references during OnStartClient. Will try again after delay.");
                }
            }
        }
        
        // This method is called a short delay after OnStartClient
        // to give all combat objects time to spawn
        private void FindCombatReferences()
        {
            Debug.Log("FindCombatReferences - Attempting to find combat elements after delay");

            // --- Player Hand Assignment --- 
            // Look for PlayerHand first - PRIORITIZE OWNERSHIP
            if (playerHand == null || !playerHand.IsOwner) // Note: For non-owner, IsOwner check might be false positive if called before ownership is fully set.
            {
                // First priority: Find any PlayerHand that we own (for the owner instance)
                // Or find the hand matching our NetworkPlayer's owner (potentially more robust)
                PlayerHand[] allHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
                PlayerHand foundHand = null; 

                foreach (PlayerHand hand in allHands)
                {
                    // Try to find the hand matching this specific CombatPlayer's controlling connection
                    if (hand.Owner != null && this.Owner != null && hand.Owner.ClientId == this.Owner.ClientId)
                    {
                        foundHand = hand;
                        Debug.Log($"FindCombatReferences: Found hand '{hand.name}' matching Owner ClientId {this.Owner.ClientId}");
                        break;
                    }
                }

                // If direct owner match failed, try IsOwner (less reliable during startup)
                if (foundHand == null)
                {
                     foreach (PlayerHand hand in allHands)
                     {
                          if (hand.IsOwner == this.IsOwner) // Fallback check
                          {
                              foundHand = hand;
                              Debug.Log($"FindCombatReferences: Found hand '{hand.name}' via IsOwner fallback ({this.IsOwner})");
                              break;
                          }
                     }
                }
                
                // Assign if found
                if (foundHand != null) 
                {
                     playerHand = foundHand;
                }
                else
                {
                     // If we still don't have a reference after checks, log a warning
                     Debug.LogWarning($"FindCombatReferences: Could not find matching PlayerHand for CombatPlayer {this.name} (Owner ClientId: {this.Owner?.ClientId ?? -1}, IsOwner: {this.IsOwner}). Current playerHand is {(playerHand == null ? "null" : playerHand.name)}.");
                }
            }
            else
            {
                Debug.Log($"FindCombatReferences: Using existing PlayerHand reference: {playerHand.name}, IsOwner: {playerHand.IsOwner}");
            }
            // --- End Player Hand Assignment ---

            // Get all players for reference
            NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
            
            // Look for pet references
            Pet[] allPets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
            
            // Try to match player pet by network player reference first
            if (playerPet == null && _networkPlayer.Value != null)
            {
                foreach (Pet pet in allPets)
                {
                    if (pet.PlayerOwner != null && pet.PlayerOwner.GetSteamName() == _networkPlayer.Value.GetSteamName())
                    {
                        playerPet = pet;
                        Debug.Log($"Found matching playerPet: {pet.name}");
                        break;
                    }
                }
            }
            
            // If we still don't have a pet reference and we have exactly 2 pets, just assign them
            if ((playerPet == null || opponentPet == null) && allPets.Length == 2)
            {
                if (playerPet == null && opponentPet == null)
                {
                    // If no pets are assigned yet, assume first pet is yours and second is opponent's
                    playerPet = allPets[0];
                    opponentPet = allPets[1];
                    Debug.Log($"Assigned pets by index - playerPet: {playerPet.name}, opponentPet: {opponentPet.name}");
                }
                else if (playerPet != null)
                {
                    // If player pet is already assigned, set other pet as opponent
                    opponentPet = (allPets[0] == playerPet) ? allPets[1] : allPets[0];
                    Debug.Log($"Found potential opponentPet: {opponentPet.name}");
                }
                else if (opponentPet != null)
                {
                    // If opponent pet is already assigned, set other pet as player's
                    playerPet = (allPets[0] == opponentPet) ? allPets[1] : allPets[0];
                    Debug.Log($"Found potential playerPet: {playerPet.name}");
                }
            }
            
            // Final validation to prevent null references
            if (playerHand == null)
            {
                Debug.LogError("Failed to find a valid PlayerHand reference after delayed search");
            }
            
            if (playerPet == null)
            {
                Debug.LogError("Failed to find a valid PlayerPet reference after delayed search");
            }
            
            if (opponentPet == null)
            {
                Debug.LogError("Failed to find a valid OpponentPet reference after delayed search");
            }
            
            // Log summary of found references
            int foundCount = (playerHand != null ? 1 : 0) + (playerPet != null ? 1 : 0) + (opponentPet != null ? 1 : 0);
            if (foundCount == 3)
            {
                Debug.Log($"Successfully found all combat references after delayed search! PlayerHand is {(playerHand.IsOwner ? "owned" : "not owned")}.");
            }
            else
            {
                Debug.LogWarning($"Found {foundCount}/3 combat references after delayed search.");
            }
        }
        
        private void CreatePlaceholderIfNeeded()
        {
            // Check if player has a sprite renderer with no sprite
            if (spriteRenderer != null && spriteRenderer.sprite == null)
            {
                Debug.LogWarning("No sprite assigned to CombatPlayer - creating placeholder");
                
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
                spriteRenderer.sprite = placeholder;
                
                // Ensure renderer is enabled and visible
                spriteRenderer.enabled = true;
                
                Debug.Log("Created placeholder sprite for CombatPlayer");
            }
        }
        
        // Initialize player with references
        [Server]
        public void Initialize(NetworkPlayer networkPlayer, Pet playerPet, Pet opponentPet, PlayerHand playerHand)
        {
            if (networkPlayer == null)
            {
                Debug.LogError("[CombatPlayer] Cannot initialize with null NetworkPlayer");
                return;
            }

            // Validate that we don't have the same pet assigned to both roles
            if (playerPet == opponentPet && playerPet != null)
            {
                Debug.LogError($"[CombatPlayer] CRITICAL ERROR: Same pet '{playerPet.name}' assigned as both player and opponent pet!");
                
                // Try to find an alternative pet
                Pet[] allPets = FindObjectsOfType<Pet>();
                if (allPets.Length > 1)
                {
                    foreach (Pet pet in allPets)
                    {
                        if (pet != playerPet)
                        {
                            Debug.Log($"[CombatPlayer] Fixed duplicate pet assignment by changing opponent pet to: {pet.name}");
                            opponentPet = pet;
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError("[CombatPlayer] Could not find alternative pet to assign!");
                }
            }

            // Debug log to show what we're initializing with
            string playerPetName = playerPet != null ? playerPet.name : "null";
            string playerPetOwner = playerPet != null && playerPet.PlayerOwner != null ? playerPet.PlayerOwner.GetSteamName() : "null";
            string opponentPetName = opponentPet != null ? opponentPet.name : "null";
            string opponentPetOwner = opponentPet != null && opponentPet.PlayerOwner != null ? opponentPet.PlayerOwner.GetSteamName() : "null";
            string handOwnerInfo = playerHand != null && playerHand.Owner != null ? $"ClientId: {playerHand.Owner.ClientId}" : "null";
            
            Debug.Log($"[CombatPlayer] Initializing {name} with:" +
                      $"\n  - NetworkPlayer: {networkPlayer.GetSteamName()}" +
                      $"\n  - Player Pet: {playerPetName} (Owner: {playerPetOwner})" +
                      $"\n  - Opponent Pet: {opponentPetName} (Owner: {opponentPetOwner})" +
                      $"\n  - Player Hand: {(playerHand != null ? playerHand.name : "null")} (Owner: {handOwnerInfo})" +
                      $"\n  - IsOwner: {IsOwner}");

            // Verify hand ownership matches player
            if (playerHand != null && playerHand.Owner != null && networkPlayer != null &&
                playerHand.Owner.ClientId != networkPlayer.Owner.ClientId)
            {
                Debug.LogWarning($"[CombatPlayer] Player hand owner ({playerHand.Owner.ClientId}) doesn't match player ({networkPlayer.Owner.ClientId})");
                
                // Try to find the correct hand if available
                PlayerHand[] allHands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
                foreach (PlayerHand hand in allHands)
                {
                    if (hand.Owner.ClientId == networkPlayer.Owner.ClientId)
                    {
                        Debug.Log($"[CombatPlayer] Found matching player hand with correct ownership");
                        playerHand = hand;
                        break;
                    }
                }
            }

            _networkPlayer.Value = networkPlayer;
            this.playerPet = playerPet;
            this.opponentPet = opponentPet;
            this.playerHand = playerHand;
            
            if (IsOwner)
            {
                // Additional setup for the local player's combat instance
                Debug.Log("[CombatPlayer] This is the local player's combat instance");
            }
            
            // Final verification check
            if (this.playerPet == this.opponentPet && this.playerPet != null)
            {
                Debug.LogError("[CombatPlayer] CRITICAL ERROR: After initialization, the same pet is still assigned to both player and opponent!");
            }
            
            isInitialized = true;
        }
        
        // Find required UI elements, usually called if IsOwner
        private void FindUIElements()
        {
            // These should ideally be assigned in the Inspector, 
            // but we can try finding them if they are null.
            if (playerNameText == null)
            {
                // Assuming it's a child object named "PlayerNameText" or similar
                playerNameText = GetComponentInChildren<TextMeshProUGUI>(true); // Search inactive too, by name might be safer
                if (playerNameText == null) 
                    Debug.LogError("Could not find PlayerNameText component in children.");
                else
                    Debug.Log("Found PlayerNameText in children.");
            }

            if (energyText == null)
            {
                // Assuming it's a child object named "EnergyText" or similar
                // Might need a more specific search if multiple TextMeshProUGUI exist
                // Example: transform.Find("UIContainer/EnergyText").GetComponent<TextMeshProUGUI>();
                // For now, just grab the first one found besides player name
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach(var text in texts)
                {
                    if (text != playerNameText) // Avoid re-assigning player name
                    {
                        energyText = text;
                        Debug.Log("Found EnergyText in children.");
                        break;
                    }
                }
                if (energyText == null) Debug.LogError("Could not find EnergyText component in children.");
            }

            if (endTurnButton == null)
            {
                // Assuming it's a child object named "EndTurnButton" or similar
                endTurnButton = GetComponentInChildren<UnityEngine.UI.Button>(true);
                 if (endTurnButton == null) 
                    Debug.LogError("Could not find EndTurnButton component in children.");
                else
                    Debug.Log("Found EndTurnButton in children.");
            }
        }
        
        // Start player turn
        [Server]
        public void StartTurn()
        {
            // Set turn state
            _isMyTurn.Value = true;
            
            // Reset/increase energy
            _currentEnergy.Value = _maxEnergy.Value;
            
            // Draw cards
            DrawCardsOnTurnStart(5); // Draw 5 cards at start of turn
            
            Debug.Log($"Started turn for player {(_networkPlayer.Value != null ? _networkPlayer.Value.GetSteamName() : "Unknown")}, Energy: {_currentEnergy.Value}");
        }
        
        // End player turn
        [Server]
        public void EndTurn()
        {
            // Set turn state
            _isMyTurn.Value = false;
            
            // Clear hand at end of turn
            TargetDiscardHand(Owner);
            
            // Notify combat manager that turn is over
            combatManager.PlayerEndedTurn(this);
            
            Debug.Log($"Ended turn for player {(_networkPlayer.Value != null ? _networkPlayer.Value.GetSteamName() : "Unknown")}");
        }
        
        // Draw cards on turn start
        [Server]
        private void DrawCardsOnTurnStart(int count)
        {
            Debug.Log($"[Combat] {_networkPlayer.Value.GetSteamName()}'s turn started - drawing {count} cards");
            // Tell client to draw cards
            TargetDrawCards(Owner, count);
        }
        
        // Called when the end turn button is clicked
        private void OnEndTurnButtonClicked()
        {
            if (!IsOwner) return;
            
            Debug.Log("End Turn button clicked");
            
            // Tell server to end turn
            CmdEndTurn();
        }
        
        // Server command to end turn
        [ServerRpc]
        private void CmdEndTurn()
        {
            if (!IsOwner || !_isMyTurn.Value) return;
            
            // End the turn on the server
            EndTurn();
        }
        
        // Play a card from hand
        [Server]
        public void PlayCard(string cardName, CardType cardType, int baseValue)
        {
            if (!_isMyTurn.Value) return;
            
            // Check if player has enough energy
            int cardCost = GetCardCostFromName(cardName); 
            
            if (_currentEnergy.Value < cardCost)
            {
                TargetNotifyInsufficientEnergy(Owner);
                return;
            }
            
            // Spend energy
            _currentEnergy.Value -= cardCost;
            
            Debug.Log($"[Combat] {_networkPlayer.Value.GetSteamName()} played {cardName} ({cardType}) with value {baseValue}, cost {cardCost}");
            
            // Apply card effect based on type
            switch (cardType)
            {
                case CardType.Attack:
                    if (opponentPet != null)
                        opponentPet.TakeDamage(baseValue);
                    else
                        Debug.LogError("Cannot play attack card - opponentPet is null");
                    break;
                case CardType.Skill:
                    if (playerPet != null)
                        playerPet.SetDefending(true);
                    else
                        Debug.LogError("Cannot play skill card - playerPet is null");
                    break;
                case CardType.Power:
                    Debug.Log($"Power card played: {cardName}");
                    break;
            }
            
            TargetCardPlayed(Owner, cardName);
        }

        private int GetCardCostFromName(string cardName)
        {
            if (cardName.Contains("Strike")) return 1;
            if (cardName.Contains("Defend")) return 1;
            if (cardName.Contains("Bash")) return 2;
            return 1;
        }
        
        [Server]
        public CardData DrawCardFromDeck()
        {
            // Create a very basic CardData object for testing
            CardData card = ScriptableObject.CreateInstance<CardData>();
            
            // Determine a random card type and set appropriate values
            int randomType = Random.Range(0, 3);
            CardType type = (CardType)randomType;
            
            card.cardName = type == CardType.Attack ? "Strike" : 
                           (type == CardType.Skill ? "Defend" : "Power Up");
            card.description = type == CardType.Attack ? "Deal 6 damage" : 
                              (type == CardType.Skill ? "Gain 5 block" : "Gain 1 energy next turn");
            card.manaCost = type == CardType.Power ? 2 : 1;
            card.cardType = type;
            card.baseValue = type == CardType.Attack ? 6 : 5;
            
            // Set up a basic card effect
            CardEffect effect = new CardEffect();
            effect.effectType = type == CardType.Attack ? EffectType.Damage : 
                               (type == CardType.Skill ? EffectType.Block : EffectType.DrawCard);
            effect.effectValue = type == CardType.Attack ? 6 : 
                                (type == CardType.Skill ? 5 : 1);
            effect.targetType = type == CardType.Attack ? TargetType.SingleEnemy : 
                               (type == CardType.Skill ? TargetType.Self : TargetType.Self);
            
            card.cardEffects = new CardEffect[] { effect };
            
            Debug.Log($"Created card: {card.cardName} ({card.cardType}) - Cost: {card.manaCost}");
            
            return card;
        }
        
        [Server]
        public void DiscardHand()
        {
            // No server logic needed here for basic implementation
        }
        
        // --- ICombatant Implementation ---

        // Players themselves don't take damage directly, their pets do
        [Server]
        public void TakeDamage(int amount)
        {
            Debug.LogWarning("CombatPlayer.TakeDamage called. Should target the pet.");
            // Potentially redirect to playerPet?
            // if (playerPet != null) playerPet.TakeDamage(amount);
        }

        // Players don't have block, their pets might
        [Server]
        public void AddBlock(int amount)
        {
            Debug.LogWarning("CombatPlayer.AddBlock called. Should target the pet.");
            // Potentially redirect to playerPet?
            // if (playerPet != null) playerPet.AddBlock(amount);
        }

        // Players don't heal directly, their pets might
        [Server]
        public void Heal(int amount)
        {
            Debug.LogWarning("CombatPlayer.Heal called. Should target the pet.");
            // Potentially redirect to playerPet?
            // if (playerPet != null) playerPet.Heal(amount);
        }

        // Draw cards is handled by TargetDrawCards RPC
        [Server]
        public void DrawCards(int amount)
        {
            Debug.Log($"CombatPlayer asked to draw {amount} cards.");
            TargetDrawCards(Owner, amount);
        }

        // Apply buff to the player (e.g., energy boost)
        [Server]
        public void ApplyBuff(int buffId)
        {
            // Implement player-specific buffs
            Debug.LogWarning($"CombatPlayer.ApplyBuff({buffId}) called, but player buffs not implemented.");
            // Example: Increase max energy, gain temp energy, etc.
        }

        // Apply debuff to the player
        [Server]
        public void ApplyDebuff(int debuffId)
        {
            // Implement player-specific debuffs
            Debug.LogWarning($"CombatPlayer.ApplyDebuff({debuffId}) called, but player debuffs not implemented.");
            // Example: Reduce energy, skip draw, etc.
        }

        // CombatPlayer represents the player, typically not an enemy
        public bool IsEnemy()
        {
            return false;
        }

        // --- End ICombatant Implementation ---

        // --- OnChange Callbacks --- 
        
        private void OnNetworkPlayerChanged(NetworkPlayer prev, NetworkPlayer next, bool asServer)
        {
            if (next != null && playerNameText != null)
            {
                 playerNameText.text = next.GetSteamName() + (IsMyTurn ? " (Your Turn)" : "");
                 UpdateTurnVisuals(false, IsMyTurn, asServer); // Re-apply turn visuals with the correct name
                 
                 Debug.Log($"Updated player name to {next.GetSteamName()}");
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
        
        // --- Update Visual Methods --- 
        
        private void UpdateEnergyDisplay(int prev, int next, bool asServer)
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {next}/{_maxEnergy.Value}";
                Debug.Log($"Updated energy display: {next}/{_maxEnergy.Value}");
            }
        }
        
        private void UpdateTurnVisuals(bool prev, bool next, bool asServer)
        {
            // Update turn indicator
            if (playerNameText != null && _networkPlayer.Value != null)
            {
                playerNameText.text = _networkPlayer.Value.GetSteamName() + (next ? " (Your Turn)" : "");
                
                // Highlight when it's player's turn
                if (next)
                {
                    playerNameText.color = Color.yellow;
                    playerNameText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
                }
                else
                {
                    playerNameText.color = Color.white;
                }
            }
            
            // Update end turn button
            if (endTurnButton != null && IsOwner)
            {
                endTurnButton.gameObject.SetActive(next);
                
                // Animate button appearance
                if (next)
                {
                    endTurnButton.transform.localScale = Vector3.zero;
                    endTurnButton.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
                }
            }
            
            Debug.Log($"Updated turn visuals: {(_networkPlayer.Value != null ? _networkPlayer.Value.GetSteamName() : "Unknown")} - IsMyTurn: {next}");
        }
        
        // --- RPCs ---
        
        [TargetRpc]
        private void TargetDrawCards(NetworkConnection conn, int count)
        {
            Debug.Log("TargetDrawCards called, attempting to draw " + count + " cards");
            
            if (playerHand == null)
            {
                Debug.LogWarning("PlayerHand is null during TargetDrawCards, attempting to find it");
                
                // Try to find the PlayerHand in the scene that we actually OWN
                PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
                playerHand = null; // Reset to ensure we don't use a non-owned reference
                
                foreach (PlayerHand hand in hands)
                {
                    if (hand.IsOwner)
                    {
                        playerHand = hand;
                        Debug.Log("Found owned PlayerHand for TargetDrawCards: " + hand.name);
                        break;
                    }
                }
                
                // If still not found, trigger the FindCombatReferences method
                if (playerHand == null)
                {
                    Debug.LogWarning("Could not find owned PlayerHand for TargetDrawCards, queueing search");
                    StartCoroutine(FindHandAndDrawCards(count));
                    return;
                }
            }
            
            // DOUBLE CHECK ownership before proceeding
            if (!playerHand.IsOwner)
            {
                Debug.LogError("PlayerHand is not owned by this client! Looking for a correctly owned hand.");
                // Try to find a properly owned hand
                PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
                bool foundOwnedHand = false;
                
                foreach (PlayerHand hand in hands)
                {
                    if (hand.IsOwner)
                    {
                        playerHand = hand;
                        foundOwnedHand = true;
                        Debug.Log("Switched to properly owned PlayerHand: " + hand.name);
                        break;
                    }
                }
                
                if (!foundOwnedHand)
                {
                    Debug.LogError("Failed to find ANY owned PlayerHand, cannot draw cards");
                    return;
                }
            }
            
            // If we reach here, we have a non-null playerHand that should be properly owned
            Debug.Log("Using PlayerHand " + playerHand.name + " with IsOwner: " + playerHand.IsOwner + " to draw cards");
            
            // Call the ServerRpc on PlayerHand to request cards from the server
            try 
            {
                playerHand.CmdDrawCards(count);
                Debug.Log("Successfully called CmdDrawCards for " + count + " cards");
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error calling CmdDrawCards: " + e.Message);
            }
        }
        
        // Coroutine to find hand and draw cards after a delay
        private System.Collections.IEnumerator FindHandAndDrawCards(int count)
        {
            // Try to find combat references
            FindCombatReferences();
            
            // Wait for a moment to allow references to be found
            yield return new WaitForSeconds(0.5f);
            
            // Try drawing cards again, but this time ensure we have an owned hand
            PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
            bool foundOwnedHand = false;
            
            foreach (PlayerHand hand in hands)
            {
                if (hand.IsOwner)
                {
                    playerHand = hand;
                    foundOwnedHand = true;
                    Debug.Log("Delayed search found owned PlayerHand: " + hand.name);
                    break;
                }
            }
            
            if (foundOwnedHand && playerHand != null)
            {
                Debug.Log("Found PlayerHand after delay, drawing cards now with IsOwner: " + playerHand.IsOwner);
                try 
                {
                    playerHand.CmdDrawCards(count);
                    Debug.Log("Successfully called delayed CmdDrawCards for " + count + " cards");
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error calling delayed CmdDrawCards: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Failed to find owned PlayerHand after delay, cannot draw cards");
            }
        }
        
        [TargetRpc]
        private void TargetDiscardHand(NetworkConnection conn)
        {
            Debug.Log("Client received request to discard hand");
            if (playerHand != null)
                playerHand.DiscardHand();
        }
        
        [TargetRpc]
        private void TargetCardPlayed(NetworkConnection conn, string cardName)
        {
            Debug.Log($"Successfully played card: {cardName}");
            // Show a visual effect or notification on the client
        }

        [TargetRpc]
        private void TargetNotifyInsufficientEnergy(NetworkConnection conn)
        {
            Debug.LogWarning("Not enough energy to play that card!");
            // Show a message to the player
        }
    }
} 