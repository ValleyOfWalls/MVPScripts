using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Connection;
using System.Collections.Generic;

namespace Combat
{
    public class Pet : NetworkBehaviour
    {
        [Header("Pet Stats")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private string petName = "Pet";
        [SerializeField] private Sprite petSprite;
        
        [Header("Visual References")]
        [SerializeField] private SpriteRenderer petRenderer;
        
        // Reference to the owner player
        private readonly SyncVar<NetworkPlayer> _playerOwner = new SyncVar<NetworkPlayer>();
        
        // Persistent deck - synced across the network
        [Header("Pet Deck")]
        public readonly SyncList<string> persistentDeckCardIDs = new SyncList<string>();
        private bool deckInitialized = false;
        
        // Public properties
        public int MaxHealth => maxHealth;
        public string PetName => petName;
        public Sprite PetSprite => petSprite;
        public NetworkPlayer PlayerOwner => _playerOwner.Value;
        
        private void Awake()
        {
            // Get sprite renderer if not set
            if (petRenderer == null)
                petRenderer = GetComponent<SpriteRenderer>();
        }

        private void Start()
        {
            // Create placeholder if no sprite assigned
            CreatePlaceholderIfNeeded();
            
            // Make sure the sprite is visible
            if (petRenderer != null)
            {
                petRenderer.enabled = true;
                Color currentColor = petRenderer.color;
                currentColor.a = 1f;
                petRenderer.color = currentColor;
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            // Log that Pet is initializing on client
            Debug.Log($"Pet OnStartClient - SpriteRenderer: {(petRenderer != null ? "Found" : "Missing")}, Sprite: {(petRenderer != null && petRenderer.sprite != null ? "Assigned" : "Missing")}");
            
            // Ensure sprite is created
            CreatePlaceholderIfNeeded();
        }
        
        // Called by server to initialize the pet with its owner
        [Server]
        public void Initialize(NetworkPlayer owner)
        {
            _playerOwner.Value = owner;
            
            // Initialize the pet's deck if needed
            if (!deckInitialized)
            {
                InitializePetDeck();
            }
            
            Debug.Log($"Pet initialized for {owner.GetSteamName()} with {persistentDeckCardIDs.Count} cards in deck");
        }

        #region Pet Deck Methods
        [Server]
        public void InitializePetDeck()
        {
            // Only initialize on the server and if not already initialized
            if (!IsServerInitialized || deckInitialized)
                return;
                
            // Check if DeckManager exists
            if (DeckManager.Instance == null)
            {
                Debug.LogError($"[Pet] Cannot initialize deck - DeckManager.Instance is null!");
                return;
            }
            
            // Clear existing deck (if any)
            persistentDeckCardIDs.Clear();
            
            // Get a pet starter deck from DeckManager
            // Use the owner's index to get a pet-specific deck if possible
            int petIndex = 0;
            if (_playerOwner.Value != null)
            {
                // Try to get a deterministic index based on the owner
                petIndex = _playerOwner.Value.GetInstanceID();
            }
            
            Deck petStarterDeck = DeckManager.Instance.GetPetStarterDeckTemplate(petIndex);
            
            if (petStarterDeck == null)
            {
                Debug.LogError($"[Pet] Failed to get pet starter deck template from DeckManager");
                return;
            }
            
            // Add each card from the starter deck to the persistent deck by name/ID
            foreach (CardData card in petStarterDeck.Cards)
            {
                if (card != null)
                {
                    persistentDeckCardIDs.Add(card.cardName); // Using card name as ID
                    Debug.Log($"[Pet] Added card {card.cardName} to persistent deck");
                }
            }
            
            deckInitialized = true;
            Debug.Log($"[Pet] Initialized persistent deck with {persistentDeckCardIDs.Count} cards");
        }
        
        // Helper method to get the number of cards in the persistent deck
        public int GetPersistentDeckCount()
        {
            return persistentDeckCardIDs.Count;
        }
        #endregion
        
        private void CreatePlaceholderIfNeeded()
        {
            if (petRenderer != null && petRenderer.sprite == null)
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
                petRenderer.sprite = placeholder;
                
                // Ensure sprite renderer is enabled and visible
                petRenderer.enabled = true;
                Color color = petRenderer.color;
                color.a = 1f;
                petRenderer.color = color;
                
                Debug.Log("Created placeholder sprite for Pet");
            }
        }
    }
} 