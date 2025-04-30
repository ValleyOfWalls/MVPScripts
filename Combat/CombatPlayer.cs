using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using TMPro;
using System.Collections;
using UnityEngine.UI;

namespace Combat
{
    // The main player class during combat
    public class CombatPlayer : NetworkBehaviour, ICombatant
    {
        [Header("References")]
        // Changed networkPlayer to a SyncVar
        private readonly SyncVar<NetworkPlayer> _networkPlayer = new SyncVar<NetworkPlayer>();
        // Removed SerializeField, assigned via Initialize
        private CombatPet playerPet;
        private CombatPet opponentPet;
        private PlayerHand playerHand;
        
        // Reference to the player's deck
        private RuntimeDeck playerDeck;
        
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
        
        // Reference to the player's deck (Property for potential external access)
        public RuntimeDeck PlayerDeck => playerDeck;
        
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
            
            // Removed delayed reference search - References are now set via Initialize
            // Invoke("FindCombatReferences", 0.5f);
            
            // Removed immediate reference finding logic
            // if (IsOwner)
            // {
            // ... (removed ~70 lines of finding logic)
            // }
        }
        
        // Removed FindCombatReferences method entirely
        // private void FindCombatReferences()
        // {
        // ... (removed ~80 lines of finding logic)
        // }
        
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
        public void Initialize(NetworkPlayer networkPlayer, CombatPet playerPetRef, CombatPet opponentPetRef, PlayerHand playerHandRef, RuntimeDeck deck)
        {
             if (networkPlayer == null) 
             {
                 Debug.LogError("[CombatPlayer.Initialize] NetworkPlayer is null!");
                 return;
             }
             if (playerPetRef == null)
             {
                 Debug.LogError($"[CombatPlayer.Initialize] PlayerPetRef is null for {networkPlayer.GetSteamName()}!");
                 // Decide how to handle this - return? proceed? Throw exception?
                 // return; 
             }
             if (opponentPetRef == null)
             { 
                 Debug.LogError($"[CombatPlayer.Initialize] OpponentPetRef is null for {networkPlayer.GetSteamName()}!");
                 // return;
             }
             if (playerHandRef == null)
             {
                 Debug.LogError($"[CombatPlayer.Initialize] PlayerHandRef is null for {networkPlayer.GetSteamName()}!");
                 // return;
             }
            
            _networkPlayer.Value = networkPlayer;
            this.playerPet = playerPetRef; // Assign player pet
            this.opponentPet = opponentPetRef; // Assign opponent pet
            this.playerHand = playerHandRef; // Assign player hand
            this.playerDeck = deck; // Assign the player deck
            
            Debug.Log($"[CombatPlayer.Initialize] Initialized for {networkPlayer.GetSteamName()} - PlayerPet: {(this.playerPet != null ? this.playerPet.name : "null")}, OpponentPet: {(this.opponentPet != null ? this.opponentPet.name : "null")}, Hand: {this.playerHand.name}");
            
            // Initialize combat state
            _maxEnergy.Value = 3; // Set max energy (or get from player data if needed)
            _currentEnergy.Value = _maxEnergy.Value;
            _isMyTurn.Value = false; // Will be set true by CombatManager when turn starts
            
            // Additional setup after references are assigned
            // FindUIElements(); // Call this if UI elements are children/need finding
            if (playerNameText != null) 
                playerNameText.text = networkPlayer.GetSteamName();
            
            UpdateEnergyDisplay(0, _currentEnergy.Value, true); // Update visuals on server
            UpdateTurnVisuals(false, _isMyTurn.Value, true);
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
            if (playerDeck == null)
            {
                Debug.LogError($"[Server] CombatPlayer {NetworkPlayer?.GetSteamName()} cannot draw card: playerDeck is null!");
                return null;
            }

            // Draw from the actual RuntimeDeck
            CardData drawnCard = playerDeck.DrawCard();

            // Log results
            if (drawnCard == null)
            {
                Debug.LogWarning($"[Server] CombatPlayer {NetworkPlayer?.GetSteamName()} drew null card (Deck empty or reshuffled?).");
                // Handle empty deck / reshuffle if needed
                // Potentially reshuffle discard into draw pile here if deck is empty
                // Example: 
                // if (playerDeck.NeedsReshuffle()) 
                // {
                //     playerDeck.Reshuffle(); 
                //     drawnCard = playerDeck.DrawCard(); 
                //     if(drawnCard != null) Debug.Log($"[Server] Reshuffled and drew {drawnCard.cardName}");
                // }
            }
            else
            {
                 Debug.Log($"[Server] CombatPlayer {NetworkPlayer?.GetSteamName()} drew card: {drawnCard.cardName}");
            }

            return drawnCard;
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
            // FindCombatReferences();
            
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

        // Control visibility and interactivity for spectator mode
        public void SetSpectatorMode(bool isSpectating)
        {
            // When in spectator mode, we may want to disable certain UI elements or interactions
            if (IsOwner)
            {
                // Only the owner needs to handle UI changes
                if (endTurnButton != null)
                {
                    endTurnButton.gameObject.SetActive(!isSpectating);
                }
                
                // Find any other owner-only UI elements and disable them in spectator mode
                Button[] interactiveButtons = GetComponentsInChildren<Button>(true);
                foreach (Button button in interactiveButtons)
                {
                    if (button != endTurnButton) // We already handled the end turn button
                    {
                        button.interactable = !isSpectating;
                    }
                }
            }
            
            // Visual feedback that this is not the active combat (e.g., slight transparency)
            if (spriteRenderer != null)
            {
                Color color = spriteRenderer.color;
                color.a = isSpectating ? 0.7f : 1.0f; // Slightly transparent when spectating
                spriteRenderer.color = color;
            }
            
            // Notify any child objects that might need to respond to spectator mode
            // For example, cards in hand might need to be non-interactive
            SendMessageUpwards("OnSpectatorModeChanged", isSpectating, SendMessageOptions.DontRequireReceiver);
        }
        
        // Toggle gameObject active state (usually called when cycling between combats)
        public void SetActive(bool active)
        {
            if (gameObject != null)
            {
                gameObject.SetActive(active);
            }
        }
    }
} 