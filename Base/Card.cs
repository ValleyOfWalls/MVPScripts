using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Component.Transforming;
using System.Collections;
using FishNet.Connection;

namespace Combat
{
    // Card types similar to Slay the Spire
    public enum CardType
    {
        Attack,
        Skill,
        Power
    }
    
    public class Card : NetworkBehaviour
    {
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
        
        // Network Component
        private NetworkTransform networkTransform;
        
        // Properties - Now read directly from the locally assigned cardData
        public CardData Data => cardData;
        public string CardName => cardData != null ? cardData.cardName : (syncedCardDataName.Value ?? "NO_DATA_NAME");
        public string Description => cardData != null ? cardData.description : "NO_DATA";
        public int ManaCost => cardData != null ? cardData.manaCost : 0;
        public CardType Type => cardData != null ? cardData.cardType : default;
        public int BaseValue => cardData != null ? cardData.baseValue : 0;
        
        // Card state
        private bool isPlayable = true;
        private bool isDragging = false;
        private Vector3 originalPosition;
        private Vector3 originalScale;
        private PlayerHand owningHand;
        private ICombatant owner;
        
        private Canvas cardCanvas;
        private CanvasGroup cardCanvasGroup;
        
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
                // Configure NetworkTransform using the correct properties
            }
            
            // Register SyncVar callbacks
            _syncedCardObjectName.OnChange += OnSyncedCardObjectNameChanged;
            syncedCardDataName.OnChange += OnSyncedCardDataNameChanged;
            Debug.Log($"[Card:{GetInstanceID()}] Awake: Registered SyncVar callbacks."); // Use InstanceID pre-network
            
            // Initial UI update attempt (might not have data yet)
            // UpdateCardUI(); // Moved to OnStartClient / OnChange
        }

        private void OnDestroy()
        {
            // Unsubscribe from events to prevent memory leaks
             Debug.Log($"[Card:{(NetworkObject != null ? NetworkObject.ObjectId.ToString() : GetInstanceID().ToString())}] OnDestroy: Unregistering SyncVar callbacks.");
            _syncedCardObjectName.OnChange -= OnSyncedCardObjectNameChanged;
            syncedCardDataName.OnChange -= OnSyncedCardDataNameChanged;
        }
        
        // Called when the syncedCardName changes
        private void OnSyncedCardDataNameChanged(string prev, string next, bool asServer)
        {
            // Only clients need to react to find the CardData
            if (!asServer)
            {
                // More detailed log
                Debug.Log($"[Client {NetworkObject.ObjectId}] OnSyncedCardDataNameChanged: Prev='{prev}', Next='{next}'. IsOwner={IsOwner}");
                
                // Check DeckManager availability right here
                if (DeckManager.Instance == null)
                {
                    Debug.LogError($"[Client {NetworkObject.ObjectId}] DeckManager.Instance is NULL inside OnSyncedCardDataNameChanged!");
                    return;
                }
                
                FindCardDataAndUpdateUI(next);
            }
        }
        
        // Called when the synced GameObject name changes
        private void OnSyncedCardObjectNameChanged(string prevName, string newName, bool asServer)
        {
            if (asServer) return; // Server sets the value, clients react

            // --- Add detailed log here ---
            Debug.Log($"[Card:{NetworkObject.ObjectId}] OnSyncedCardObjectNameChanged fired. IsOwner: {IsOwner}. Prev: '{prevName}', New: '{newName}'");

            if (!string.IsNullOrEmpty(newName))
            {
                gameObject.name = newName;
                Debug.Log($"[Card:{NetworkObject.ObjectId}] --- Set gameObject.name to: {newName}");
            }
            else
            {
                Debug.LogWarning($"[Card:{NetworkObject.ObjectId}] --- Synced object name was null or empty, not setting name.");
            }
        }
        
        // Helper to find CardData and update UI
        private void FindCardDataAndUpdateUI(string cardName)
        {
            if (string.IsNullOrEmpty(cardName))
            {
                Debug.LogWarning($"[Client {NetworkObject.ObjectId}] Synced card name is empty in FindCardDataAndUpdateUI.");
                return;
            }

            if (DeckManager.Instance == null)
            {
                Debug.LogError($"[Client {NetworkObject.ObjectId}] DeckManager instance is null in FindCardDataAndUpdateUI. Cannot find CardData for '{cardName}'.");
                return;
            }
            
            Debug.Log($"[Client {NetworkObject.ObjectId}] Attempting to find CardData for name: '{cardName}' using DeckManager.");
            this.cardData = DeckManager.Instance.FindCardByName(cardName);
            
            if (this.cardData == null)
            {
                Debug.LogError($"[Client {NetworkObject.ObjectId}] FindCardByName FAILED for name: '{cardName}'");
                // Optionally create a placeholder UI here
            }
            else
            {
                Debug.Log($"[Client {NetworkObject.ObjectId}] FindCardByName SUCCESS for '{cardName}'. Updating UI and Interactivity.");
                UpdateCardUI();
                UpdateInteractivity(); // Update based on ownership
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            // IsOwner check removed as per previous fix (use base.Owner.IsLocalClient)
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"[Card:{NetworkObject.ObjectId}] OnStartClient. IsOwner: {IsOwner}. Name: {gameObject.name} SyncedName: '{_syncedCardObjectName.Value}' Parent: {(transform.parent != null ? transform.parent.name : "null")}");

            // --- Manually trigger object name update if the value arrived before OnChange ---
            if (!string.IsNullOrEmpty(_syncedCardObjectName.Value))
            {
                Debug.Log($"[Card:{NetworkObject.ObjectId}] OnStartClient: Manually calling OnSyncedCardObjectNameChanged with initial value '{_syncedCardObjectName.Value}'.");
                OnSyncedCardObjectNameChanged(null, _syncedCardObjectName.Value, false); // Manually call the callback
            }
            else
            {
                 Debug.Log($"[Card:{NetworkObject.ObjectId}] OnStartClient: _syncedCardObjectName is empty, waiting for OnChange callback.");
            }
            
            // Initial attempt to find data if SyncVar already arrived
            if (!string.IsNullOrEmpty(syncedCardDataName.Value))
            {
                 FindCardDataAndUpdateUI(syncedCardDataName.Value);
            }
            else
            {
                Debug.Log($"[Card:{NetworkObject.ObjectId}] waiting for syncedCardDataName...");
            }

            // Register with parent PlayerHand if available
            // We might need a small delay here if the hand isn't ready yet
            StartCoroutine(RegisterWithHandAfterDelay());
        }

        private IEnumerator RegisterWithHandAfterDelay()
        {
            yield return new WaitForSeconds(0.1f); // Small delay
            PlayerHand playerHand = GetComponentInParent<PlayerHand>();
            if (playerHand != null)
            {
                playerHand.RegisterNetworkedCard(this);
                // Debug.Log($"[Client] Card {CardName} registered with PlayerHand"); // Log inside RegisterNetworkedCard
            }
            // TODO: Add similar logic for PetHand if necessary
        }

        public void ServerInitialize(CardData data, ICombatant cardOwner, PlayerHand hand = null, PetHand petHand = null)
        {
            if (data == null)
            {
                Debug.LogError("[Server] Attempted to initialize Card with null CardData!");
                return;
            }
            
            // Set server-side references (needed before OnStartServer potentially)
            this.cardData = data; 
            this.owner = cardOwner;
            this.owningHand = hand; // Store player hand if applicable
            // We don't store PetHand reference in owningHand field
            
            // Set the synced data name immediately on the server
            // This ensures the SyncVar is set *before* OnStartServer might be called
            // If cardData is null, something went wrong earlier.
            if(this.cardData != null)
            {
                 syncedCardDataName.Value = this.cardData.cardName;
                 Debug.Log($"[Server Initialize Method] Set syncedCardDataName for {data.cardName}");
            }
            else
            {
                Debug.LogError($"[Server Initialize Method] CardData is NULL when trying to set syncedCardDataName!");
            }
        }
        
        // REMOVED RpcInitializeCard - Replaced by SyncVar + OnStartClient logic

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            // Now that the object is spawned, ensure SyncVars are set
            // (syncedCardDataName should ideally be set in ServerInitialize now)
            if (this.cardData != null && string.IsNullOrEmpty(syncedCardDataName.Value))
            {
                // Fallback in case ServerInitialize didn't run first somehow
                syncedCardDataName.Value = this.cardData.cardName;
                Debug.LogWarning($"[Server - OnStartServer] Set syncedCardName as fallback for {this.cardData.cardName} (ObjectId: {NetworkObject.ObjectId})");
            }
            else if (this.cardData == null)
            {
                Debug.LogError($"[Server - OnStartServer] CardData is null for Card (ObjectId: {NetworkObject.ObjectId})! Cannot ensure SyncVar.");
            }
        }

        // Server-side method to set the card's object name (called after spawning, before parenting RPC)
        [Server]
        public void SetCardObjectName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"[Server Card:{NetworkObject.ObjectId}] SetCardObjectName called with empty name.");
                name = "UnnamedCard";
            }
            string uniqueName = $"Card_{NetworkObject.ObjectId}_{name.Replace(' ', '_')}"; // Ensure unique name in hierarchy
            Debug.Log($"[Server Card:{NetworkObject.ObjectId}] Preparing to set _syncedCardObjectName to: {uniqueName}"); // Log before setting
            _syncedCardObjectName.Value = uniqueName;
            // Set server-side name immediately too, client callback will handle clients
            gameObject.name = uniqueName;
             Debug.Log($"[Server Card:{NetworkObject.ObjectId}] Set synced card object name to: {uniqueName}");
        }

        // --- Parenting RPC --- 
        [ObserversRpc(ExcludeOwner = false, BufferLast = true)]
        public void RpcSetParent(NetworkObject parentNetworkObject)
        {
             if (parentNetworkObject == null)
             {
                 Debug.LogError($"[Card:{NetworkObject.ObjectId}] RpcSetParent received null parentNetworkObject.");
                 return;
             }

             Transform parentTransform = parentNetworkObject.transform;
             if (parentTransform != null)
             {
                 // Use worldPositionStays = false to inherit parent's scale and position cleanly
                 transform.SetParent(parentTransform, false); 
                 Debug.Log($"[Card:{NetworkObject.ObjectId}] Set parent to {parentTransform.name} ({parentNetworkObject.ObjectId}) via RPC.");
             }
             else
             {
                  Debug.LogError($"[Card:{NetworkObject.ObjectId}] Could not find transform for parent NetworkObject {parentNetworkObject.ObjectId} in RpcSetParent.");
             }
        }

        private void UpdateInteractivity()
        {
            // Only the owner should interact with their cards in PlayerHand
            bool isPlayerCard = owningHand != null; 
            bool canInteract = isPlayerCard && IsOwner;

            if (cardCanvasGroup != null)
            {
                cardCanvasGroup.interactable = canInteract;
                cardCanvasGroup.blocksRaycasts = canInteract;
                // Keep alpha at 1, use SetPlayable for visual state
                // cardCanvasGroup.alpha = 1f; 
            }
            Debug.Log($"[Client] Card {CardName} interactivity set: {canInteract} (IsOwner: {IsOwner}, IsPlayerCard: {isPlayerCard})");
        }
        
        private void UpdateCardUI()
        {
            // Debug.Log($"Updating UI for card: {CardName}");
            
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
                        Debug.LogWarning($"Card {CardName}: Could not load artwork from path: {cardData.cardArtworkPath}");
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
                Debug.LogError($"Card {CardName} has no Image component assigned to cardImage");
            }
            
            // Set card type icon
            UpdateCardTypeIcon();
        }
        
        private void CreatePlaceholderCardBackground()
        {
            Debug.LogWarning($"No sprite assigned to Card {CardName} - creating placeholder");
            
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
            
            Debug.Log("Created placeholder sprite for card background");
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
                         Debug.LogWarning($"Card {CardName}: Could not load type icon from path: {cardData.typeIconPath}");
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
        
        // Called when the card is clicked
        public void OnCardClick()
        {
            // If card is not playable, don't do anything
            if (!isPlayable) return;
            
            Debug.Log($"Card clicked: {CardName}");
            
            // Highlight on click
            transform.DOScale(originalScale * 1.1f, 0.2f);
        }
        
        // Called when the card starts being dragged
        public void OnBeginDrag()
        {
            if (!isPlayable) return;
            
            isDragging = true;
            
            Debug.Log($"Begin dragging card: {CardName}");
            
            // Store original position
            originalPosition = transform.position;
            
            // Visual feedback
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring card to front during drag
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 10;
        }
        
        // Called while the card is being dragged
        public void OnDrag(Vector3 position)
        {
            if (!isDragging) return;
            
            // Follow the cursor/touch position
            transform.position = position;
        }
        
        // Called when the card is released after dragging
        public void OnEndDrag(bool validTarget, ICombatant target = null)
        {
            if (!isDragging) return;
            
            isDragging = false;
            
            Debug.Log($"End dragging card: {CardName}, Valid target: {validTarget}");
            
            // Reset sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 10;
            
            if (validTarget)
            {
                // Card was played successfully
                PlayCard(target);
            }
            else
            {
                // Card was not played, return to hand
                ReturnToHand();
            }
        }
        
        // Play the card
        public void PlayCard(ICombatant target = null)
        {
            Debug.Log($"Playing card: {CardName}, Target: {(target != null ? "Found" : "None")}");
            
            // Apply card effects
            if (cardData != null && cardData.cardEffects != null)
            {
                ApplyCardEffects(target);
            }
            
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

        private void ApplyCardEffects(ICombatant target)
        {
            if (owner == null) return;
            
            // Process each effect
            foreach (CardEffect effect in cardData.cardEffects)
            {
                switch (effect.targetType)
                {
                    case TargetType.Self:
                        ApplyEffect(effect, owner);
                        break;
                    case TargetType.SingleEnemy:
                        if (target != null)
                            ApplyEffect(effect, target);
                        break;
                    case TargetType.AllEnemies:
                        foreach (ICombatant enemy in CombatManager.Instance.GetEnemies())
                            ApplyEffect(effect, enemy);
                        break;
                    case TargetType.SingleAlly:
                        if (target != null && !target.IsEnemy())
                            ApplyEffect(effect, target);
                        break;
                    case TargetType.AllAllies:
                        foreach (ICombatant ally in CombatManager.Instance.GetAllies())
                            ApplyEffect(effect, ally);
                        break;
                }
            }
        }
        
        private void ApplyEffect(CardEffect effect, ICombatant target)
        {
            if (target == null) return;
            
            Debug.Log($"Applying effect: {effect.effectType} with value {effect.effectValue} to target");
            
            switch (effect.effectType)
            {
                case EffectType.Damage:
                    target.TakeDamage(effect.effectValue);
                    break;
                case EffectType.Block:
                    target.AddBlock(effect.effectValue);
                    break;
                case EffectType.Heal:
                    target.Heal(effect.effectValue);
                    break;
                case EffectType.DrawCard:
                    target.DrawCards(effect.effectValue);
                    break;
                case EffectType.ApplyBuff:
                    target.ApplyBuff(effect.effectValue);
                    break;
                case EffectType.ApplyDebuff:
                    target.ApplyDebuff(effect.effectValue);
                    break;
            }
        }
        
        public void ReturnToHand()
        {
            Debug.Log($"Returning card to hand: {CardName}");
            
            // Return to original position and scale
            transform.DOMove(originalPosition, 0.3f)
                .SetEase(Ease.OutQuint);
            
            transform.DOScale(originalScale, 0.3f)
                .SetEase(Ease.OutQuint);
        }
        
        public void OnPointerEnter()
        {
            if (isDragging) return;
            
            // Zoom effect when hovering
            transform.DOScale(originalScale * 1.2f, 0.2f);
            
            // Bring to front
            if (cardCanvas != null)
                cardCanvas.sortingOrder += 5;
        }
        
        public void OnPointerExit()
        {
            if (isDragging) return;
            
            // Return to normal size when not hovering
            transform.DOScale(originalScale, 0.2f);
            
            // Return to normal sorting order
            if (cardCanvas != null)
                cardCanvas.sortingOrder -= 5;
        }
    }
}