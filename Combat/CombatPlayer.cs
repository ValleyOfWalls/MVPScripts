using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using DG.Tweening;
using TMPro;

namespace Combat
{
    // The main player class during combat
    public class CombatPlayer : NetworkBehaviour
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
        
        private void Awake()
        {
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(OnEndTurnButtonClicked);
                
            // Register OnChange callbacks
            _networkPlayer.OnChange += OnNetworkPlayerChanged;
            _currentEnergy.OnChange += OnEnergyChanged;
            _isMyTurn.OnChange += OnTurnChanged;
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
        }
        
        // Initialize player with references
        [Server]
        public void Initialize(NetworkPlayer player, Pet myPet, Pet enemyPet, CombatManager manager)
        {
            _networkPlayer.Value = player; // Set the SyncVar value
            playerPet = myPet;
            opponentPet = enemyPet;
            combatManager = manager;
            
            // Initialize the player's hand
            if (playerHand != null)
                playerHand.Initialize(player, this);
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
        }
        
        // Draw cards on turn start
        [Server]
        private void DrawCardsOnTurnStart(int count)
        {
            // Tell client to draw cards
            TargetDrawCards(Owner, count);
        }
        
        // Called when the end turn button is clicked
        private void OnEndTurnButtonClicked()
        {
            if (!IsOwner) return;
            
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
            
            // Apply card effect based on type
            switch (cardType)
            {
                case CardType.Attack:
                    opponentPet.TakeDamage(baseValue);
                    break;
                case CardType.Skill:
                    playerPet.SetDefending(true);
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
            string[] cardNames = { "Strike", "Defend", "Bash", "Slice", "Shrug It Off" };
            string[] descriptions = { "Deal {0} damage", "Gain {0} block", "Deal {0} damage and apply 2 Vulnerable", "Deal {0} damage", "Gain {0} Block" };
            CardType type = Random.value < 0.6f ? CardType.Attack : CardType.Skill;
            if (Random.value < 0.1f) type = CardType.Power;
            int value = Mathf.Clamp(Mathf.FloorToInt(Random.value * 8) + 3, 3, 10);
            string name = cardNames[Random.Range(0, cardNames.Length)];
            int cost = GetCardCostFromName(name);
            string desc = string.Format(descriptions[Random.Range(0, descriptions.Length)], value);
            return new CardData(name, desc, cost, type, value);
        }
        
        [Server]
        public void DiscardHand()
        {
            // No server logic needed here for basic implementation
        }
        
        // --- OnChange Callbacks --- 
        
        private void OnNetworkPlayerChanged(NetworkPlayer prev, NetworkPlayer next, bool asServer)
        {
            if (next != null && playerNameText != null)
            {
                 playerNameText.text = next.GetSteamName() + (IsMyTurn ? " (Your Turn)" : "");
                 UpdateTurnVisuals(false, IsMyTurn, asServer); // Re-apply turn visuals with the correct name
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
                energyText.text = $"Energy: {next}/{_maxEnergy.Value}";
        }

        private void UpdateTurnVisuals(bool prev, bool next, bool asServer)
        {
             if (!IsOwner) return;
             
             NetworkPlayer currentPlayer = _networkPlayer.Value;
             
            // Animate turn changes
            if (playerNameText != null && currentPlayer != null) // Check if currentPlayer is available
            {
                playerNameText.text = currentPlayer.GetSteamName() + (next ? " (Your Turn)" : "");
                playerNameText.color = next ? Color.green : Color.white;
                if (next && !prev) // Animate only when turn starts
                    playerNameText.transform.DOPunchScale(Vector3.one * 0.2f, 0.5f, 2, 0.5f);
            }
            
            // Enable/disable the end turn button
            if (endTurnButton != null)
                endTurnButton.interactable = next;
        }
        
        #region TargetRPCs (Client Notifications)
        
        [TargetRpc]
        private void TargetDrawCards(NetworkConnection conn, int count)
        {
            if (playerHand != null)
                playerHand.DrawInitialHand(count);
        }
        
        [TargetRpc]
        private void TargetDiscardHand(NetworkConnection conn)
        {
            if (playerHand != null)
                playerHand.DiscardHand();
        }
        
        [TargetRpc]
        private void TargetCardPlayed(NetworkConnection conn, string cardName)
        {
            Debug.Log($"Card played successfully: {cardName}");
        }
        
        [TargetRpc]
        private void TargetNotifyInsufficientEnergy(NetworkConnection conn)
        {
            Debug.Log("Not enough energy to play that card!");
            // TODO: Show a UI notification
        }
        
        #endregion
    }
} 