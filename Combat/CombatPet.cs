using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;
using DG.Tweening;

namespace Combat
{
    public class CombatPet : NetworkBehaviour, ICombatant
    {
        [Header("Combat Stats")]
        private readonly SyncVar<int> _currentHealth = new SyncVar<int>();
        private readonly SyncVar<bool> _isDefending = new SyncVar<bool>();
        
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
            
            // Initial UI update
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
        }
        
        // New Initialize method called right after instantiation on server
        [Server]
        public void Initialize(Pet parentPet)
        {
            if (parentPet == null)
            {
                Debug.LogError("[CombatPet] Initialize called with null parentPet!");
                // Optionally destroy self or handle error
                // Destroy(gameObject);
                return;
            }

            referencePet = parentPet;
            
            // Set initial health based on pet's max health
            _currentHealth.Value = referencePet.MaxHealth;
            _isDefending.Value = false;
            
            // Create runtime deck from the persistent pet's deck
            CreateRuntimeDeck();
            
            Debug.Log($"[CombatPet] Initialized with reference to persistent pet owned by {referencePet?.PlayerOwner?.GetSteamName() ?? "Unknown"}");
        }

        // Optional: Keep this if CombatManager still needs to assign the hand later
        // Or remove if hand assignment is handled elsewhere
        [Server]
        public void AssignHand(PetHand hand)
        {
             petHand = hand;
             Debug.Log($"[CombatPet] Assigned hand {hand?.name ?? "null"}");
        }
        
        [Server]
        public void CreateRuntimeDeck()
        {
            // Create a new empty runtime deck
            petDeck = new RuntimeDeck("Pet Combat Deck", DeckType.PetDeck);
            
            // Check if pet has an initialized deck
            if (referencePet != null && referencePet.persistentDeckCardIDs.Count > 0)
            {
                Debug.Log($"[CombatPet] Creating runtime deck from persistent deck ({referencePet.persistentDeckCardIDs.Count} cards)");
                
                // Populate deck from persistent deck IDs
                foreach (string cardID in referencePet.persistentDeckCardIDs)
                {
                    CardData cardData = DeckManager.Instance.FindCardByName(cardID);
                    if (cardData != null)
                    {
                        petDeck.AddCard(cardData);
                        Debug.Log($"[CombatPet] Added card {cardID} to runtime deck");
                    }
                    else
                    {
                        Debug.LogWarning($"[CombatPet] Could not find CardData for {cardID}");
                    }
                }
                
                // Shuffle the deck
                petDeck.Shuffle();
                Debug.Log($"[CombatPet] Shuffled runtime deck with {petDeck.DrawPileCount} cards");
            }
            else
            {
                // Fallback to starter deck
                Debug.LogWarning("[CombatPet] No persistent deck found, using starter deck");
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
        
        // --- Combat Methods ---
        
        [Server]
        public void TakeDamage(int damage)
        {
            Debug.Log($"CombatPet TakeDamage called - Current Health: {_currentHealth.Value}, Damage: {damage}, IsDefending: {_isDefending.Value}");
            
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
        
        // Animation for taking damage
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
            // This would be implemented with a damage number prefab
            Debug.Log($"CombatPet took {damage} damage!");
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
            if (CombatManager.Instance != null)
                CombatManager.Instance.HandlePetDefeat(this);
        }
        
        // UI update methods
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
        
        // --- ICombatant Implementation ---
        
        [Server]
        public void AddBlock(int amount)
        {
            // Pets typically don't use block, but implement the interface method
            SetDefending(true); // Treat block as defense for pets
            Debug.Log($"[CombatPet] Added block, setting to defending mode");
        }
        
        [Server]
        public void Heal(int amount)
        {
            _currentHealth.Value = Mathf.Min(_currentHealth.Value + amount, MaxHealth);
            Debug.Log($"CombatPet healed for {amount}, current health: {_currentHealth.Value}");
        }
        
        [Server]
        public void DrawCards(int amount)
        {
            // Draw cards using the pet hand
            if (petHand != null)
            {
                petHand.DrawCards(amount);
                Debug.Log($"[CombatPet] Requested to draw {amount} cards through PetHand");
            }
            else
            {
                Debug.LogWarning($"[CombatPet] Cannot draw cards - petHand is null");
            }
        }
        
        [Server]
        public CardData DrawCardFromDeck()
        {
            if (petDeck == null)
            {
                Debug.LogError($"[Server] CombatPet cannot draw card: petDeck is null!");
                return null;
            }
            
            // Draw from the RuntimeDeck
            CardData drawnCard = petDeck.DrawCard();
            
            if (drawnCard == null)
            {
                Debug.LogWarning($"[Server] CombatPet drew null card (Deck empty or needs reshuffle)");
            }
            else
            {
                Debug.Log($"[Server] CombatPet drew card: {drawnCard.cardName}");
            }
            
            return drawnCard;
        }
        
        [Server]
        public void ApplyBuff(int buffId)
        {
            // Implement buff logic based on buffId
            Debug.LogWarning($"CombatPet.ApplyBuff({buffId}) called, but buff system not implemented.");
        }
        
        [Server]
        public void ApplyDebuff(int debuffId)
        {
            // Implement debuff logic based on debuffId
            Debug.LogWarning($"CombatPet.ApplyDebuff({debuffId}) called, but debuff system not implemented.");
        }
        
        public bool IsEnemy()
        {
            // This depends on context - usually a pet is an enemy to the opponent player
            // This would be set during combat setup
            return false;
        }
    }
} 