using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using DG.Tweening;
using FishNet.Transporting; // Needed for ObserversRpc

namespace Combat
{
    public class CombatPet : NetworkBehaviour, ICombatant
    {
        #region Properties and Fields
        [Header("Combat Stats")]
        private readonly SyncVar<int> _currentHealth = new SyncVar<int>();
        private readonly SyncVar<bool> _isDefending = new SyncVar<bool>();
        
        // Public accessor for health SyncVar needed by UI
        public SyncVar<int> SyncHealth => _currentHealth;
        
        [Header("References")]
        [SerializeField] private Pet referencePet; // The persistent pet this combat pet represents
        [SerializeField] private PetHand petHand;
        [SerializeField] private SpriteRenderer petSprite;
        [SerializeField] private TMPro.TextMeshProUGUI healthText;
        [SerializeField] private GameObject defendIcon;
        
        // Runtime deck for combat
        private RuntimeDeck petDeck;
        
        // Properties
        public int CurrentHealth => _currentHealth.Value;
        public int MaxHealth => referencePet != null ? referencePet.MaxHealth : 100;
        public bool IsDefending => _isDefending.Value;
        public Pet ReferencePet => referencePet;
        public RuntimeDeck PetDeck => petDeck;
        #endregion

        #region Unity Lifecycle Methods
        private void Awake()
        {
            // Register OnChange callbacks
            _currentHealth.OnChange += OnHealthChanged;
            _isDefending.OnChange += OnDefendingChanged;
            
            // Get sprite renderer if not set
            if (petSprite == null)
                petSprite = GetComponent<SpriteRenderer>();
        }
        
        private void OnDestroy()
        {
            // Unregister callbacks
            _currentHealth.OnChange -= OnHealthChanged;
            _isDefending.OnChange -= OnDefendingChanged;
        }
        #endregion

        #region Network Callbacks
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Set initial state
            if (referencePet != null)
            {
                _currentHealth.Value = referencePet.MaxHealth;
            }
            else
            {
                _currentHealth.Value = 100; // Default value
            }
            _isDefending.Value = false;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Initial UI update for all clients - removed the early return
            UpdateHealthDisplay(_currentHealth.Value, _currentHealth.Value, false);
            UpdateDefendIcon(_isDefending.Value);
            
            // Set visual appearance from reference pet if available
            if (referencePet != null && petSprite != null)
            {
                if (referencePet.PetSprite != null)
                {
                    petSprite.sprite = referencePet.PetSprite;
                }
            }
            
            // Try to find references if they're missing on client (for ALL clients)
            if (referencePet == null || petHand == null)
            {
                StartCoroutine(FindReferencesOnClient());
            }
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
                            if (petSprite != null && referencePet.PetSprite != null)
                            {
                                petSprite.sprite = referencePet.PetSprite;
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
            _currentHealth.Value = referencePet.MaxHealth;
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
        #endregion

        #region RPCs and Animations
        [ObserversRpc]
        private void RpcAnimateDamage(int damage)
        {
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
            // Simplified - we'll just log it instead of creating a visual element
        }
        
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
        #endregion

        #region UI Updates
        private void UpdateHealthDisplay(int prev, int next, bool asServer)
        {
            if (healthText != null)
            {
                healthText.text = $"HP: {next}/{MaxHealth}";
                
                // Animate health change only if it decreased (took damage)
                if (next < prev)
                {
                    healthText.transform.DOPunchScale(Vector3.one * 0.2f, 0.3f, 1, 0.5f);
                }
            }
        }
        
        private void UpdateDefendIcon(bool isDefending)
        {
            if (defendIcon != null)
            {
                defendIcon.SetActive(isDefending);
                // Simple pop animation
                if (isDefending) defendIcon.transform.DOPunchScale(Vector3.one * 0.3f, 0.3f, 1, 0.5f);
            }
        }
        
        // OnChange callbacks
        private void OnHealthChanged(int prev, int next, bool asServer)
        {
            UpdateHealthDisplay(prev, next, asServer);
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
            // Pets typically don't use block, but implement the interface method
            SetDefending(true); // Treat block as defense for pets
        }
        
        [Server]
        public void Heal(int amount)
        {
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, MaxHealth);
        }
        
        [Server]
        public void DrawCards(int amount)
        {
            // Draw cards using the pet hand
            if (petHand != null)
            {
                petHand.DrawCards(amount);
            }
            else
            {
                Debug.LogWarning("FALLBACK: Cannot draw cards - petHand is null");
            }
        }
        
        [Server]
        public CardData DrawCardFromDeck()
        {
            if (petDeck == null)
            {
                Debug.LogError("FALLBACK: CombatPet cannot draw card: petDeck is null");
                return null;
            }

            // Draw from the actual RuntimeDeck
            CardData drawnCard = petDeck.DrawCard();
            
            // Handle results
            if (drawnCard == null)
            {
                // Handle empty deck by reshuffling discard pile
                if (petDeck.NeedsReshuffle())
                {
                    petDeck.Reshuffle();
                    
                    // Try drawing again after reshuffle
                    drawnCard = petDeck.DrawCard();
                    
                    if (drawnCard == null)
                    {
                        Debug.LogError("FALLBACK: CombatPet no cards available, deck empty and cannot reshuffle");
                    }
                }
            }

            return drawnCard;
        }
        
        [Server]
        public void ApplyBuff(int buffId)
        {
            Debug.LogWarning($"FALLBACK: CombatPet.ApplyBuff({buffId}) called, but buff system not implemented");
        }
        
        [Server]
        public void ApplyDebuff(int debuffId)
        {
            Debug.LogWarning($"FALLBACK: CombatPet.ApplyDebuff({debuffId}) called, but debuff system not implemented");
        }
        
        public bool IsEnemy()
        {
            // This depends on context - usually a pet is an enemy to the opponent player
            return false;
        }
        #endregion

        #region Turn Management
        [Server]
        public void StartTurn()
        {
            // Take AI turn after a short delay
            StartCoroutine(TakeTurnAfterDelay(1.0f));
        }

        // Coroutine to introduce a delay before AI actions
        private System.Collections.IEnumerator TakeTurnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            
            // Take the AI turn
            TakeTurn();
        }

        // Execute the AI turn logic
        [Server]
        private void TakeTurn()
        {
            // Simple AI: Play all cards in hand
            if (petHand != null)
            {
                // Get cards in the pet's hand
                List<Card> cardsInHand = petHand.GetCardsInHand();
                
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

        // Play cards one after another with delay
        private System.Collections.IEnumerator PlayCardsSequentially(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
            {
                EndTurn();
                yield break;
            }
            
            // Find the target ONCE (opponent player's pet, or just any other pet)
            CombatPet targetPet = FindOpponentPet(); 
            CombatPlayer targetPlayer = FindOpponentPlayer(); // Find the opponent player
            ICombatant target = null;
            if(targetPet != null) 
                target = targetPet; // Prioritize targeting the pet
            else if (targetPlayer != null)
                target = targetPlayer; // Fallback to player if pet not found
            
            if (target == null)
            {
                Debug.LogError("Pet AI could not find any valid target!");
                EndTurn();
                yield break;
            }

            foreach (Card card in new List<Card>(cards)) // Create a copy of the list to avoid modification issues
            {
                if (card == null || card.Data == null) 
                {
                    Debug.LogWarning("Pet AI found null card or card data in hand, skipping.");
                    continue; // Skip this card
                }

                // Use the CardEffectProcessor to apply the card's effects
                // The processor will handle targeting logic (Self, Enemy, Ally) based on the card's effects
                // For now, the default enemy target is passed
                CardEffectProcessor.ApplyCardEffects(card.Data, this, target); 
                
                // Remove card from hand (server-side)
                petHand.RemoveCard(card);
                
                // Wait before playing next card
                yield return new WaitForSeconds(1.0f);
            }
            
            // End turn after all cards played
            EndTurn();
        }

        // Find the opponent's pet (keep existing logic)
        private CombatPet FindOpponentPet()
        {
            CombatPet[] pets = FindObjectsByType<CombatPet>(FindObjectsSortMode.None);
            foreach (CombatPet pet in pets)
            {
                if (pet != this)
                {
                    return pet;
                }
            }
            return null;
        }

        // Add a helper to find the opponent player
        private CombatPlayer FindOpponentPlayer()
        {
            CombatPlayer[] players = FindObjectsByType<CombatPlayer>(FindObjectsSortMode.None);
            foreach(CombatPlayer player in players)
            {
                // Check if this player owns this pet
                if(player.NetworkPlayer != null && player.NetworkPlayer.CombatPet == this)
                {
                    // If so, find *their* opponent
                    if(player.NetworkPlayer.OpponentNetworkPlayer != null)
                    {
                        return player.NetworkPlayer.OpponentNetworkPlayer.CombatPlayer;
                    }
                }
            }
            // Fallback: find any player that doesn't own this pet
            foreach(CombatPlayer player in players)
            {
                if(player.NetworkPlayer != null && player.NetworkPlayer.CombatPet != this)
                {
                    return player;
                }
            }
            return null;
        }

        // End the pet's turn
        [Server]
        private void EndTurn()
        {
            // Discard remaining cards
            if (petHand != null)
            {
                petHand.ServerDiscardHand();
            }
            
            // Notify combat manager that turn is over
            CombatManager.Instance.PetEndedTurn(this);
        }
        #endregion
    }
} 