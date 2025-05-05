using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using TMPro;
using System.Collections;
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
        
        // Combat state
        private readonly SyncVar<bool> _isMyTurn = new SyncVar<bool>();
        private readonly SyncVar<int> _currentEnergy = new SyncVar<int>();
        private readonly SyncVar<int> _maxEnergy = new SyncVar<int>(3);
        
        // References to combat systems
        private CombatManager combatManager;
        private SpriteRenderer spriteRenderer;
        #endregion

        #region Properties
        // Public properties
        public SyncVar<bool> SyncIsMyTurn => _isMyTurn;
        public SyncVar<int> SyncEnergy => _currentEnergy;
        public NetworkPlayer NetworkPlayer => _networkPlayer.Value;
        public NetworkConnection Owner => _networkPlayer.Value != null ? _networkPlayer.Value.Owner : null;
        public bool IsMyTurn => _isMyTurn.Value;
        public int CurrentEnergy => _currentEnergy.Value;
        public int MaxEnergy => _maxEnergy.Value;
        public RuntimeDeck PlayerDeck => playerDeck;
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
            
            // Get sprite renderer
            spriteRenderer = GetComponent<SpriteRenderer>();
            
            // Create a visual placeholder
            CreatePlaceholderIfNeeded();
            
            // Get reference to CombatManager
            combatManager = FindObjectOfType<CombatManager>();
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
            }
        }

        private void OnDestroy()
        {
            // Unregister synced variable change callbacks
            _networkPlayer.OnChange -= OnNetworkPlayerChanged;
            _currentEnergy.OnChange -= OnEnergyChanged;
            _isMyTurn.OnChange -= OnTurnChanged;
        }
        #endregion

        #region Network Callbacks
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Create placeholder if needed
            CreatePlaceholderIfNeeded();
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            // Double-check registration of callbacks
            _isMyTurn.OnChange += OnTurnChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _networkPlayer.OnChange += OnNetworkPlayerChanged;
        }
        #endregion

        #region Initialization and Setup
        private void CreatePlaceholderIfNeeded()
        {
            // Check if player has a sprite renderer with no sprite
            if (spriteRenderer != null && spriteRenderer.sprite == null)
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
                spriteRenderer.sprite = placeholder;
                
                // Ensure renderer is enabled and visible
                spriteRenderer.enabled = true;
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
            _maxEnergy.Value = 3;
            _currentEnergy.Value = _maxEnergy.Value;
            _isMyTurn.Value = false;
            
            // Additional setup after references are assigned
            if (playerNameText != null) 
                playerNameText.text = networkPlayer.GetSteamName();
            
            UpdateEnergyDisplay(0, _currentEnergy.Value, true);
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

            if (energyText == null)
            {
                // Try to find among children TextMeshProUGUI components
                var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach(var text in texts)
                {
                    if (text != playerNameText)
                    {
                        energyText = text;
                        break;
                    }
                }
                if (energyText == null) 
                    Debug.LogError("FALLBACK: Could not find EnergyText component in children");
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

        // End player turn
        [Server]
        public void EndTurn()
        {
            // Set turn state
            SetTurn(false);
            
            // Notify clients explicitly that turn state changed
            RpcNotifyTurnChanged(false);
            
            // Notify combat manager that turn is over
            if (combatManager != null)
            {
                combatManager.PlayerEndedTurn(this);
            }
            else
            {
                Debug.LogError($"FALLBACK: EndTurn - CombatManager reference is null");
            }
        }
        
        // Draw cards on turn start
        [Server]
        private void DrawCardsOnTurnStart(int count)
        {
            // Tell client to draw cards
            TargetDrawCards(Owner, count);
        }
        
        // Server command to end turn
        [ServerRpc(RequireOwnership = true)]
        public void CmdEndTurn()
        {
            if (!IsMyTurn) return;

            // End the turn logic
            SetTurn(false);
            
            // Discard Hand
            if (playerHand != null)
            {
                playerHand.ServerDiscardHand();
            }
            else
            {
                Debug.LogError($"FALLBACK: Cannot discard hand - PlayerHand reference is null");
            }
            
            // Tell the CombatManager to switch turns
            if (combatManager == null)
            {
                // Try to find CombatManager if it's null
                combatManager = FindObjectOfType<CombatManager>();
            }
            
            if (combatManager != null)
            {
                combatManager.PlayerEndedTurn(this);
            }
            else
            {
                Debug.LogError("FALLBACK: CmdEndTurn - CombatManager reference is null");
            }
        }
        #endregion

        #region Card Management
        // Play a card from hand
        [Server]
        public void PlayCard(string cardName, CardType cardType)
        {
            // Log a warning that this method might be deprecated
            Debug.LogWarning($"CombatPlayer.PlayCard({cardName}) called. This logic might be superseded by CardTargetingSystem/CardEffectProcessor.");
            
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
            
            // Find CardData to apply effects using CardEffectProcessor
            CardData cardData = DeckManager.Instance.FindCardByName(cardName);
            if (cardData != null && opponentPet != null) // Need a target for the processor
            {
                // Apply effects targeting the opponent pet by default (this is limited)
                CardEffectProcessor.ApplyCardEffects(cardData, this, opponentPet);
            }
            else
            {
                Debug.LogError($"Could not find CardData for {cardName} or opponentPet is null in PlayCard.");
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
                Debug.LogError("FALLBACK: Cannot draw card: playerDeck is null");
                return null;
            }

            // Draw from the actual RuntimeDeck
            CardData drawnCard = playerDeck.DrawCard();

            // Handle empty deck
            if (drawnCard == null)
            {
                // Handle empty deck by reshuffling discard pile
                if (playerDeck.NeedsReshuffle())
                {
                    playerDeck.Reshuffle();
                    
                    // Try drawing again after reshuffling
                    drawnCard = playerDeck.DrawCard();
                    if (drawnCard == null)
                    {
                        Debug.LogError("FALLBACK: Still drew null card after reshuffling");
                    }
                }
                else
                {
                    Debug.LogError("FALLBACK: No cards available, deck empty and cannot reshuffle");
                }
            }

            return drawnCard;
        }
        
        [Server]
        public void DiscardHand()
        {
            // No server logic needed here for basic implementation
        }
        #endregion

        #region ICombatant Implementation
        // Players themselves don't take damage directly, their pets do
        [Server]
        public void TakeDamage(int amount)
        {
            Debug.LogWarning("FALLBACK: CombatPlayer.TakeDamage called. Should target the pet");
        }

        // Players don't have block, their pets might
        [Server]
        public void AddBlock(int amount)
        {
            Debug.LogWarning("FALLBACK: CombatPlayer.AddBlock called. Should target the pet");
        }

        // Players don't heal directly, their pets might
        [Server]
        public void Heal(int amount)
        {
            Debug.LogWarning("FALLBACK: CombatPlayer.Heal called. Should target the pet");
        }

        // Draw cards is handled by TargetDrawCards RPC
        [Server]
        public void DrawCards(int amount)
        {
            TargetDrawCards(Owner, amount);
        }

        // Apply buff to the player (e.g., energy boost)
        [Server]
        public void ApplyBuff(int buffId)
        {
            Debug.LogWarning("FALLBACK: CombatPlayer.ApplyBuff called, but player buffs not implemented");
        }

        // Apply debuff to the player
        [Server]
        public void ApplyDebuff(int debuffId)
        {
            Debug.LogWarning("FALLBACK: CombatPlayer.ApplyDebuff called, but player debuffs not implemented");
        }

        // CombatPlayer represents the player, typically not an enemy
        public bool IsEnemy()
        {
            return false;
        }
        
        // Players are defeated when their pet is defeated
        public bool IsDefeated()
        {
            // Forward to pet if available
            if (playerPet != null)
            {
                return playerPet.IsDefeated();
            }
            return false; // By default, not defeated if no pet reference
        }
        #endregion

        #region SyncVar Callbacks
        private void OnNetworkPlayerChanged(NetworkPlayer prev, NetworkPlayer next, bool asServer)
        {
            if (next != null && playerNameText != null)
            {
                 playerNameText.text = next.GetSteamName() + (IsMyTurn ? " (Your Turn)" : "");
                 UpdateTurnVisuals(false, IsMyTurn, asServer);
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
        #endregion

        #region UI Update Methods
        private void UpdateEnergyDisplay(int prev, int next, bool asServer)
        {
            if (energyText != null)
            {
                energyText.text = $"Energy: {next}/{_maxEnergy.Value}";
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
        }
        #endregion

        #region RPCs
        [TargetRpc]
        private void TargetDrawCards(NetworkConnection conn, int count)
        {
            if (playerHand == null)
            {
                Debug.LogWarning("FALLBACK: PlayerHand is null during TargetDrawCards, attempting to find it");
                
                // Try to find the PlayerHand in the scene that we actually OWN
                PlayerHand[] hands = FindObjectsByType<PlayerHand>(FindObjectsSortMode.None);
                playerHand = null; // Reset to ensure we don't use a non-owned reference
                
                foreach (PlayerHand hand in hands)
                {
                    if (hand.IsOwner)
                    {
                        playerHand = hand;
                        break;
                    }
                }
                
                // If still not found, trigger the search coroutine
                if (playerHand == null)
                {
                    StartCoroutine(FindHandAndDrawCards(count));
                    return;
                }
            }
            
            // DOUBLE CHECK ownership before proceeding
            if (!playerHand.IsOwner)
            {
                Debug.LogError("FALLBACK: PlayerHand is not owned by this client, looking for a correctly owned hand");
                // Try to find a properly owned hand
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
                
                if (!foundOwnedHand)
                {
                    Debug.LogError("FALLBACK: Failed to find ANY owned PlayerHand, cannot draw cards");
                    return;
                }
            }
            
            // Call the ServerRpc on PlayerHand to request cards from the server
            try 
            {
                playerHand.CmdDrawCards(count);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"FALLBACK: Error calling CmdDrawCards: {e.Message}");
            }
        }
        
        // Coroutine to find hand and draw cards after a delay
        private System.Collections.IEnumerator FindHandAndDrawCards(int count)
        {
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
                    Debug.Log("FALLBACK: Delayed search found owned PlayerHand");
                    break;
                }
            }
            
            if (foundOwnedHand && playerHand != null)
            {
                try 
                {
                    playerHand.CmdDrawCards(count);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"FALLBACK: Error calling delayed CmdDrawCards: {e.Message}");
                }
            }
            else
            {
                Debug.LogError("FALLBACK: Failed to find owned PlayerHand after delay, cannot draw cards");
            }
        }
        
        [TargetRpc]
        private void TargetCardPlayed(NetworkConnection conn, string cardName)
        {
            // Visual feedback handled by Card component
        }

        [TargetRpc]
        private void TargetNotifyInsufficientEnergy(NetworkConnection conn)
        {
            Debug.LogWarning("FALLBACK: Not enough energy to play that card");
        }

        // Parenting RPC
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

        // Add RPC to explicitly notify clients about turn state change
        [ObserversRpc]
        public void RpcNotifyTurnChanged(bool isMyTurn)
        {
            // Force update any UI that needs to respond to turn changes
            CombatCanvasManager canvasManager = FindObjectOfType<CombatCanvasManager>();
            if (canvasManager != null)
            {
                // Use reflection to call OnTurnChanged since it's private
                System.Reflection.MethodInfo method = typeof(CombatCanvasManager).GetMethod(
                    "OnTurnChanged", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                    
                if (method != null)
                {
                    method.Invoke(canvasManager, new object[] { !isMyTurn, isMyTurn, false });
                }
                else
                {
                    Debug.LogError("FALLBACK: Failed to find OnTurnChanged method via reflection");
                }
            }
        }
        #endregion
    }
}