using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;
using System.Collections;
using FishNet.Connection;
using Combat;
using UnityEngine.EventSystems;

namespace Combat
{
    // Card types similar to Slay the Spire
    public enum CardType
    {
        Attack,
        Skill,
        Power
    }
    
    public class Card : NetworkBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        #region Fields and Properties
        [Header("Card Data")]
        // This will be assigned locally on each client based on syncedCardName
        private CardData cardData;
        
        [Header("UI Elements")]
        [SerializeField] private Image cardImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI costText;
        [SerializeField] private Image cardTypeIcon;
        
        // Synced variable to identify the card across the network
        private readonly SyncVar<string> syncedCardDataName = new SyncVar<string>();
        
        // Synced variable for GameObject name
        private readonly SyncVar<string> _syncedCardObjectName = new SyncVar<string>();
        
        // Synced variable for owning hand reference
        private readonly SyncVar<NetworkObject> _syncedOwningHandObject = new SyncVar<NetworkObject>();
        
        // Network Component
        private NetworkTransform networkTransform;
        
        // Properties - Now read directly from the locally assigned cardData
        public CardData Data => cardData;
        public string CardName => cardData != null ? cardData.cardName : (syncedCardDataName.Value ?? "NO_DATA_NAME");
        public string Description => cardData != null ? cardData.description : "NO_DATA";
        public int ManaCost => cardData != null ? cardData.manaCost : 0;
        public CardType Type => cardData != null ? cardData.cardType : default;
        
        // Card state
        private bool isPlayable = true;
        private bool isDragging = false;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private PlayerHand owningHand;
        private ICombatant owner;
        public ICombatant Owner => owner;
        
        private Canvas cardCanvas;
        private CanvasGroup cardCanvasGroup;
        
        // References to combat systems
        private CardTargetingSystem targetingSystem;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // Store original transform values
            originalPosition = transform.position;
            originalScale = transform.localScale;
            
            // Get or add required components
            cardCanvas = GetComponent<Canvas>();
            if (cardCanvas == null)
            {
                cardCanvas = gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = true;
                cardCanvas.sortingOrder = 100;
            }
            
            cardCanvasGroup = GetComponent<CanvasGroup>();
            if (cardCanvasGroup == null)
            {
                cardCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // Get or add NetworkTransform component
            networkTransform = GetComponent<NetworkTransform>();
            if (networkTransform == null)
            {
                networkTransform = gameObject.AddComponent<NetworkTransform>();
            }
            
            // Register SyncVar callbacks
            _syncedCardObjectName.OnChange += OnSyncedCardObjectNameChanged;
            syncedCardDataName.OnChange += OnSyncedCardDataNameChanged;
            _syncedOwningHandObject.OnChange += OnSyncedOwningHandObjectChanged;
        }

        private void Start()
        {
            // Find the targeting system
            targetingSystem = CardTargetingSystem.Instance;
            if (targetingSystem == null)
            {
                Debug.LogWarning("CardTargetingSystem not found. Card targeting will not work.");
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
            _syncedCardObjectName.OnChange -= OnSyncedCardObjectNameChanged;
            syncedCardDataName.OnChange -= OnSyncedCardDataNameChanged;
            _syncedOwningHandObject.OnChange -= OnSyncedOwningHandObjectChanged;
        }
        #endregion

        #region SyncVar Callbacks
        // Called when the syncedCardName changes
        private void OnSyncedCardDataNameChanged(string prev, string next, bool asServer)
        {
            // Only clients need to react to find the CardData
            if (!asServer)
            {
                // Check DeckManager availability right here
                if (DeckManager.Instance == null)
                {
                    Debug.LogError($"[FALLBACK] DeckManager.Instance is NULL inside OnSyncedCardDataNameChanged!");
                    return;
                }
                
                FindCardDataAndUpdateUI(next);
            }
        }
        
        // Called when the synced GameObject name changes
        private void OnSyncedCardObjectNameChanged(string prevName, string newName, bool asServer)
        {
            if (!string.IsNullOrEmpty(newName))
            {
                // Ensure we update the actual GameObject name
                gameObject.name = newName;
                
                // Force UI update if card data is already set
                if (cardData != null)
                {
                    UpdateCardUI();
                }
            }
            else
            {
                Debug.LogWarning($"[FALLBACK] Synced object name was null or empty, not setting name.");
            }
        }
        
        // Called when the synced owning hand reference changes
        private void OnSyncedOwningHandObjectChanged(NetworkObject prev, NetworkObject next, bool asServer)
        {
            if (!asServer && next != null)
            {
                // Try to get the PlayerHand component from the NetworkObject
                PlayerHand hand = next.GetComponent<PlayerHand>();
                if (hand != null)
                {
                    owningHand = hand;
                    
                    // Set the owner combatant reference
                    NetworkPlayer networkPlayer = hand.NetworkPlayer;
                    if (networkPlayer != null)
                    {
                        owner = networkPlayer.CombatPlayer;
                    }
                    
                    // Update interactivity now that we have a hand reference
                    UpdateInteractivity();
                }
                else
                {
                    Debug.LogWarning($"[FALLBACK] Card {CardName} received owningHand NetworkObject but couldn't find PlayerHand component");
                }
            }
        }
        #endregion

        #region Network Callbacks
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();

            // Manually trigger object name update if the value arrived before OnChange
            if (!string.IsNullOrEmpty(_syncedCardObjectName.Value))
            {
                OnSyncedCardObjectNameChanged(null, _syncedCardObjectName.Value, false);
            }
            
            // Initial attempt to find data if SyncVar already arrived
            if (!string.IsNullOrEmpty(syncedCardDataName.Value))
            {
                FindCardDataAndUpdateUI(syncedCardDataName.Value);
            }

            // Register with parent PlayerHand if available
            StartCoroutine(RegisterWithHandAfterDelay());
        }

        private IEnumerator RegisterWithHandAfterDelay()
        {
            yield return new WaitForSeconds(0.1f); // Small delay
            PlayerHand playerHand = GetComponentInParent<PlayerHand>();
            if (playerHand != null)
            {
                playerHand.RegisterNetworkedCard(this);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Now that the object is spawned, ensure SyncVars are set
            if (this.cardData != null && string.IsNullOrEmpty(syncedCardDataName.Value))
            {
                // Fallback in case ServerInitialize didn't run first somehow
                syncedCardDataName.Value = this.cardData.cardName;
                Debug.LogWarning($"[FALLBACK] Set syncedCardName as fallback for {this.cardData.cardName}");
            }
            else if (this.cardData == null)
            {
                Debug.LogError($"[FALLBACK] CardData is null for Card! Cannot ensure SyncVar.");
            }
            
            // Make sure name is properly set on the host too
            if (!string.IsNullOrEmpty(_syncedCardObjectName.Value) && gameObject.name.Contains("PreSpawn"))
            {
                // The host also needs to update its GameObject name
                gameObject.name = _syncedCardObjectName.Value;
            }
        }
        #endregion

        #region Card Data Management
        // Helper to find CardData and update UI
        private void FindCardDataAndUpdateUI(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                Debug.LogWarning($"[FALLBACK] Synced card name is empty in FindCardDataAndUpdateUI.");
                return;
            }

            if (DeckManager.Instance == null)
            {
                Debug.LogError($"[FALLBACK] DeckManager instance is null in FindCardDataAndUpdateUI. Cannot find CardData for '{cardName}'.");
                return;
            }
            
            this.cardData = DeckManager.Instance.FindCardByName(cardName);
            
            if (this.cardData == null)
            {
                Debug.LogError($"[FALLBACK] FindCardByName FAILED for name: '{cardName}'");
                // Create a placeholder UI to show something is wrong
                nameText.text = "CARD NOT FOUND";
                descriptionText.text = $"Missing card: {cardName}";
                costText.text = "?";
            }
            else
            {
                // Ensure we have the correct name displayed in the inspector
                if (!gameObject.name.Contains(cardData.cardName))
                {
                    string newName = $"Card_{NetworkObject.ObjectId}_{cardData.cardName.Replace(' ', '_')}";
                    gameObject.name = newName;
                }
                
                // Update the UI with the found card data
                UpdateCardUI();
                UpdateInteractivity(); // Update based on ownership
            }
        }

        public void ServerInitialize(CardData data, ICombatant cardOwner, PlayerHand hand = null, PetHand petHand = null)
        {
            if (data == null)
            {
                Debug.LogError("[FALLBACK] Attempted to initialize Card with null CardData!");
                return;
            }
            
            // Set server-side references (needed before OnStartServer potentially)
            this.cardData = data; 
            this.owner = cardOwner;
            this.owningHand = hand; // Store player hand if applicable
            
            // Set the owning hand NetworkObject sync var if applicable
            if (hand != null && hand.NetworkObject != null)
            {
                _syncedOwningHandObject.Value = hand.NetworkObject;
            }
            
            // Set the synced data name immediately on the server
            if (this.cardData != null)
            {
                // Make sure we set the syncedCardDataName first
                syncedCardDataName.Value = this.cardData.cardName;
                
                // Also update our local UI immediately on server
                UpdateCardUI();
                
                // Set ownership of the card to the player who should own it
                if (cardOwner != null && cardOwner is CombatPlayer player && player.Owner != null)
                {
                    SetOwnership(player.Owner);
                }
            }
            else
            {
                Debug.LogError($"[FALLBACK] CardData is NULL when trying to set syncedCardDataName!");
            }
        }
        
        [Server]
        public void SetOwnership(NetworkConnection conn)
        {
            if (conn != null && NetworkObject != null)
            {
                NetworkObject.GiveOwnership(conn);
            }
        }
        
        // Server-side method to set the card's object name (called after spawning, before parenting RPC)
        [Server]
        public void SetCardObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"[FALLBACK] SetCardObjectName called with empty name.");
                name = "UnnamedCard";
            }
            
            // Set the synced card data name first to ensure clients have access to the proper card data
            syncedCardDataName.Value = name;
            
            // Set the object name with a unique identifier to avoid name conflicts
            string uniqueName = $"Card_{NetworkObject.ObjectId}_{name.Replace(' ', '_')}"; 
            
            // Set the synced name that clients will receive
            _syncedCardObjectName.Value = uniqueName;
            
            // The server's callback won't fire for its own changes, so we need to explicitly update it
            // This ensures host and clients have consistent naming
            gameObject.name = uniqueName;
        }
        #endregion

        #region UI Updates
        // Made public so it can be called from PlayerHand
        public void UpdateInteractivity()
        {
            // Only the owner should interact with their cards in PlayerHand
            bool isPlayerCard = owningHand != null; 
            bool canInteract = isPlayerCard && IsOwner;

            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.interactable = canInteract;
                cardCanvasGroup.blocksRaycasts = canInteract;
            }
        }
        
        private void UpdateCardUI()
        {
            // Set up card UI
            if (nameText != null) nameText.text = CardName;
            if (descriptionText != null) descriptionText.text = Description;
            if (costText != null) costText.text = ManaCost.ToString();
            
            // Update card image if available
            if (cardImage != null)
            {
                // Load sprite from path if CardData exists
                if (cardData != null && !string.IsNullOrEmpty(cardData.cardArtworkPath))
                {
                    cardImage.sprite = Resources.Load<Sprite>(cardData.cardArtworkPath);
                    if (cardImage.sprite == null) 
                    {
                        Debug.LogWarning($"[FALLBACK] Card {CardName}: Could not load artwork from path: {cardData.cardArtworkPath}");
                        CreatePlaceholderCardBackground(); // Fallback placeholder
                    }
                }
                else if (cardImage.sprite == null) // If no CardData or path, or loading failed
                {
                    CreatePlaceholderCardBackground();
                }
                
                // Ensure image is displayed properly
                cardImage.enabled = true;
                cardImage.preserveAspect = true;
                
                // Set full opacity for image
                Color imgColor = cardImage.color;
                imgColor.a = 1f;
                cardImage.color = imgColor;
            }
            else
            {
                Debug.LogError($"[FALLBACK] Card {CardName} has no Image component assigned to cardImage");
            }
            
            // Set card type icon
            UpdateCardTypeIcon();
        }
        
        private void CreatePlaceholderCardBackground()
        {
            Debug.LogWarning($"[FALLBACK] No sprite assigned to Card {CardName} - creating placeholder");
            
            // Create a placeholder texture
            Texture2D texture = new Texture2D(200, 300);
            Color[] colors = new Color[200 * 300];
            
            // Fill with color based on card type
            Color fillColor;
            switch (Type)
            {
                case CardType.Attack:
                    fillColor = new Color(0.8f, 0.2f, 0.2f, 1.0f); // Red for attack
                    break;
                case CardType.Skill:
                    fillColor = new Color(0.2f, 0.4f, 0.8f, 1.0f); // Blue for skill
                    break;
                case CardType.Power:
                    fillColor = new Color(0.6f, 0.2f, 0.8f, 1.0f); // Purple for power
                    break;
                default:
                    fillColor = new Color(0.5f, 0.5f, 0.5f, 1.0f); // Gray default
                    break;
            }
            
            // Fill with type color
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = fillColor;
            }
            
            // Add a border
            for (int x = 0; x < 200; x++)
            {
                for (int y = 0; y < 300; y++)
                {
                    if (x < 5 || x > 194 || y < 5 || y > 294)
                    {
                        colors[y * 200 + x] = new Color(0.2f, 0.2f, 0.2f, 1.0f);
                    }
                }
            }
            
            texture.SetPixels(colors);
            texture.Apply();
            
            // Create sprite from texture
            Sprite placeholder = Sprite.Create(texture, new Rect(0, 0, 200, 300), new Vector2(0.5f, 0.5f), 100f);
            cardImage.sprite = placeholder;
        }
        
        private void UpdateCardTypeIcon()
        {            
            if (cardTypeIcon != null)
            {
                // Load icon from path if CardData exists
                if (cardData != null && !string.IsNullOrEmpty(cardData.typeIconPath))
                {
                    cardTypeIcon.sprite = Resources.Load<Sprite>(cardData.typeIconPath);
                    if (cardTypeIcon.sprite != null)
                    {
                        cardTypeIcon.color = Color.white; // Reset color if using sprite
                    }
                    else
                    {
                         Debug.LogWarning($"[FALLBACK] Card {CardName}: Could not load type icon from path: {cardData.typeIconPath}");
                         SetTypeIconColorFallback(); // Fallback to color
                    }
                }
                else
                {
                    SetTypeIconColorFallback(); // Fallback to color if no path
                }
                
                // Ensure icon is enabled
                cardTypeIcon.enabled = true;
            }
        }

        // Helper method for fallback icon color
        private void SetTypeIconColorFallback()
        {
             if (cardTypeIcon == null) return;
             cardTypeIcon.sprite = null; // Ensure no sprite is assigned
             switch (Type)
             {
                 case CardType.Attack:
                     cardTypeIcon.color = Color.red;
                     break;
                 case CardType.Skill:
                     cardTypeIcon.color = Color.blue;
                     break;
                 case CardType.Power:
                     cardTypeIcon.color = Color.magenta;
                     break;
                 default:
                     cardTypeIcon.color = Color.gray;
                     break;
             }
        }
        #endregion

        #region Card Interaction
        public void SetPlayable(bool playable)
        {
            isPlayable = playable;
            
            // Visual update
            if (cardImage != null)
            {
                Color color = cardImage.color;
                color.a = playable ? 1f : 0.5f;
                cardImage.color = color;
            }
            
            // Update canvas group interactable state
            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.interactable = playable;
            }
        }
        
        // Implements IBeginDragHandler
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!isPlayable || !IsOwner) return;
            
            isDragging = true;
            
            // Store original position
            originalPosition = transform.position;
            
            // Visual feedback
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring card to front during drag
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 10;
            
            // Start targeting with the card targeting system
            if (targetingSystem != null)
            {
                targetingSystem.StartTargeting(this, transform.position);
            }
        }
        
        // Implements IDragHandler
        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || !IsOwner) return;
            
            // Get correct position in world space
            Vector3 pointerPosition = Input.mousePosition;
            
            // Move the card to the pointer position
            transform.position = pointerPosition;
            
            // Update targeting if we have a targeting system
            if (targetingSystem != null)
            {
                targetingSystem.UpdateTargeting(pointerPosition);
            }
        }
        
        // Implements IEndDragHandler
        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging || !IsOwner) return;
            
            isDragging = false;
            
            // Reset sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 10;
            
            bool validTarget = false;
            
            // End targeting if we have a targeting system
            if (targetingSystem != null)
            {
                validTarget = targetingSystem.EndTargeting(Input.mousePosition);
            }
            
            if (validTarget)
            {
                // Card was played successfully - it will be destroyed via the targeting system
                // Notify the owning hand that this card was played
                if (owningHand != null)
                {
                    owningHand.OnCardPlayed(this);
                }
                
                // Animate card being played
                transform.DOMove(new Vector3(Screen.width / 2, Screen.height / 2, 0), 0.3f)
                    .SetEase(Ease.OutQuint);
                
                transform.DOScale(originalScale * 1.5f, 0.3f)
                    .SetEase(Ease.OutQuint);
                
                // Fade out and destroy
                if (cardCanvasGroup != null)
                {
                    cardCanvasGroup.DOFade(0, 0.3f)
                        .SetDelay(0.3f)
                        .OnComplete(() => {
                            Destroy(gameObject);
                        });
                }
                else
                {
                    // If no canvas group, destroy after delay
                    Destroy(gameObject, 0.6f);
                }
            }
            else
            {
                // Card was not played, return to hand
                ReturnToHand();
            }
        }
        
        public void ReturnToHand()
        {
            // Return to original position and scale
            // We need to make sure the Z position doesn't get pushed back behind the canvas
            Vector3 returnPosition = originalPosition;
            
            // Return to position with controlled animation
            transform.DOMove(returnPosition, 0.3f)
                .SetEase(Ease.OutQuint);
            
            transform.DOScale(originalScale, 0.3f)
                .SetEase(Ease.OutQuint);
            
            // Make sure the hand knows to arrange cards
            PlayerHand playerHand = GetComponentInParent<PlayerHand>();
            if (playerHand != null)
            {
                // Wait briefly for animation then arrange cards
                StartCoroutine(DelayedHandArrangement(playerHand));
            }
        }
        
        private IEnumerator DelayedHandArrangement(PlayerHand hand)
        {
            // Wait for return animation to complete
            yield return new WaitForSeconds(0.35f);
            
            // Ask the hand to arrange all cards
            hand.ArrangeCardsInHand();
        }
        
        // Implements IPointerEnterHandler
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isDragging) return;
            
            // Zoom effect when hovering
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring to front
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 5;
        }
        
        // Implements IPointerExitHandler
        public void OnPointerExit(PointerEventData eventData)
        {
            if (isDragging) return;
            
            // Return to normal size when not hovering
            transform.DOScale(originalScale, 0.2f);
            
            // Return to normal sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 5;
        }
        #endregion

        #region RPCs
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
             if (parentNetworkObject == null)
             {
                 Debug.LogError($"[FALLBACK] RpcSetParent received null parentNetworkObject.");
                 return;
             }

             Transform parentTransform = parentNetworkObject.transform;
             if (parentTransform != null)
             {
                 // Use worldPositionStays = false to inherit parent's scale and position cleanly
                 transform.SetParent(parentTransform, false); 
             }
             else
             {
                  Debug.LogError($"[FALLBACK] Could not find transform for parent NetworkObject in RpcSetParent.");
             }
        }
        #endregion
    }
}