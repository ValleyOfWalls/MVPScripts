using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TextMeshProUGUI))]
public class ReliableMenuText : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [System.Serializable]
    public class MenuStyle
    {
        [Header("Basic Properties")]
        public string styleName = "Default";
        public Color normalColor = Color.white;
        public Color hoverColor = Color.yellow;
        public float normalSize = 24f;
        public float hoverSize = 28f;
        
        [Header("Animation")]
        public float animationSpeed = 8f;
        public float hoverScale = 1.05f;
        public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 1.1f);
        
        [Header("Professional Effects")]
        public bool enableGlow = true;
        public Color glowColor = new Color(1f, 1f, 1f, 0.5f);
        public float glowDistance = 3f;
        public bool enableShadow = true;
        public Color shadowColor = new Color(0, 0, 0, 0.5f);
        public Vector2 shadowDistance = new Vector2(2, -2);
        
        [Header("TextMeshPro Gradient")]
        public bool useGradient = true;
        public Color gradientTopLeft = Color.white;
        public Color gradientTopRight = Color.white;
        public Color gradientBottomLeft = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color gradientBottomRight = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Color hoverGradientTopLeft = Color.yellow;
        public Color hoverGradientTopRight = Color.yellow;
        public Color hoverGradientBottomLeft = new Color(1f, 0.7f, 0f, 1f);
        public Color hoverGradientBottomRight = new Color(1f, 0.7f, 0f, 1f);
    }

    [Header("Style Selection")]
    [SerializeField] private int currentStyleIndex = 0;
    [SerializeField] private MenuStyle[] menuStyles = new MenuStyle[0];
    
    [Header("Runtime Controls")]
    [SerializeField] private bool enableHoverEffects = true;
    [SerializeField] private bool enableSparkles = true;
    [SerializeField] private bool enableBreathingEffect = false;
    
    [Header("Sparkle Settings")]
    [SerializeField] private int sparkleCount = 5;
    [SerializeField] private float sparkleRadius = 30f;
    [SerializeField] private float sparkleLifetime = 1.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private float audioVolume = 0.5f;

    // Components
    private TextMeshProUGUI textComponent;
    private RectTransform rectTransform;
    private AudioSource audioSource;
    
    // UI Effect Components
    private Shadow textShadow;
    private Outline textGlow;
    
    // Animation
    private bool isHovering = false;
    private Vector3 originalScale;
    private Coroutine hoverCoroutine;
    private Coroutine breathingCoroutine;

    private void Awake()
    {
        InitializeComponents();
        SetupDefaultStyles();
    }

    private void Start()
    {
        originalScale = transform.localScale;
        RefreshUIEffects();
        ApplyCurrentStyle();
        
        if (enableBreathingEffect)
        {
            StartBreathingEffect();
        }
    }

    private void InitializeComponents()
    {
        textComponent = GetComponent<TextMeshProUGUI>();
        rectTransform = GetComponent<RectTransform>();
        
        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.volume = audioVolume;
    }

    private void SetupDefaultStyles()
    {
        if (menuStyles == null || menuStyles.Length == 0)
        {
            menuStyles = new MenuStyle[4];
            
            // Professional Gold
            menuStyles[0] = new MenuStyle
            {
                styleName = "Professional Gold",
                normalColor = new Color(1f, 0.84f, 0f, 1f),
                hoverColor = new Color(1f, 1f, 0.6f, 1f),
                normalSize = 24f,
                hoverSize = 28f,
                useGradient = true,
                gradientTopLeft = new Color(1f, 0.95f, 0.4f, 1f),
                gradientTopRight = new Color(1f, 0.95f, 0.4f, 1f),
                gradientBottomLeft = new Color(0.8f, 0.6f, 0f, 1f),
                gradientBottomRight = new Color(0.8f, 0.6f, 0f, 1f),
                hoverGradientTopLeft = new Color(1f, 1f, 0.7f, 1f),
                hoverGradientTopRight = new Color(1f, 1f, 0.7f, 1f),
                hoverGradientBottomLeft = new Color(1f, 0.8f, 0.2f, 1f),
                hoverGradientBottomRight = new Color(1f, 0.8f, 0.2f, 1f),
                enableGlow = true,
                glowColor = new Color(1f, 0.9f, 0.3f, 0.8f),
                glowDistance = 4f,
                enableShadow = true,
                shadowColor = new Color(0.3f, 0.2f, 0f, 0.6f),
                shadowDistance = new Vector2(2, -2)
            };
            
            // Elegant Silver
            menuStyles[1] = new MenuStyle
            {
                styleName = "Elegant Silver",
                normalColor = new Color(0.9f, 0.9f, 0.95f, 1f),
                hoverColor = new Color(1f, 1f, 1f, 1f),
                normalSize = 24f,
                hoverSize = 26f,
                useGradient = true,
                gradientTopLeft = new Color(1f, 1f, 1f, 1f),
                gradientTopRight = new Color(1f, 1f, 1f, 1f),
                gradientBottomLeft = new Color(0.7f, 0.7f, 0.8f, 1f),
                gradientBottomRight = new Color(0.7f, 0.7f, 0.8f, 1f),
                hoverGradientTopLeft = new Color(1f, 1f, 1f, 1f),
                hoverGradientTopRight = new Color(1f, 1f, 1f, 1f),
                hoverGradientBottomLeft = new Color(0.8f, 0.9f, 1f, 1f),
                hoverGradientBottomRight = new Color(0.8f, 0.9f, 1f, 1f),
                enableGlow = true,
                glowColor = new Color(0.8f, 0.9f, 1f, 0.6f),
                glowDistance = 3f,
                enableShadow = true,
                shadowColor = new Color(0, 0, 0, 0.4f),
                shadowDistance = new Vector2(1, -1)
            };
            
            // Combat Red
            menuStyles[2] = new MenuStyle
            {
                styleName = "Combat Red",
                normalColor = new Color(0.9f, 0.2f, 0.2f, 1f),
                hoverColor = new Color(1f, 0.4f, 0.4f, 1f),
                normalSize = 24f,
                hoverSize = 30f,
                useGradient = true,
                gradientTopLeft = new Color(1f, 0.3f, 0.3f, 1f),
                gradientTopRight = new Color(1f, 0.3f, 0.3f, 1f),
                gradientBottomLeft = new Color(0.7f, 0.1f, 0.1f, 1f),
                gradientBottomRight = new Color(0.7f, 0.1f, 0.1f, 1f),
                hoverGradientTopLeft = new Color(1f, 0.5f, 0.5f, 1f),
                hoverGradientTopRight = new Color(1f, 0.5f, 0.5f, 1f),
                hoverGradientBottomLeft = new Color(0.9f, 0.2f, 0.2f, 1f),
                hoverGradientBottomRight = new Color(0.9f, 0.2f, 0.2f, 1f),
                enableGlow = true,
                glowColor = new Color(1f, 0.3f, 0.3f, 0.9f),
                glowDistance = 5f,
                enableShadow = true,
                shadowColor = new Color(0.4f, 0f, 0f, 0.7f),
                shadowDistance = new Vector2(3, -3)
            };
            
            // Mystical Purple
            menuStyles[3] = new MenuStyle
            {
                styleName = "Mystical Purple",
                normalColor = new Color(0.6f, 0.3f, 0.9f, 1f),
                hoverColor = new Color(0.8f, 0.5f, 1f, 1f),
                normalSize = 24f,
                hoverSize = 27f,
                useGradient = true,
                gradientTopLeft = new Color(0.7f, 0.4f, 1f, 1f),
                gradientTopRight = new Color(0.7f, 0.4f, 1f, 1f),
                gradientBottomLeft = new Color(0.4f, 0.2f, 0.7f, 1f),
                gradientBottomRight = new Color(0.4f, 0.2f, 0.7f, 1f),
                hoverGradientTopLeft = new Color(0.9f, 0.6f, 1f, 1f),
                hoverGradientTopRight = new Color(0.9f, 0.6f, 1f, 1f),
                hoverGradientBottomLeft = new Color(0.6f, 0.3f, 0.9f, 1f),
                hoverGradientBottomRight = new Color(0.6f, 0.3f, 0.9f, 1f),
                enableGlow = true,
                glowColor = new Color(0.7f, 0.4f, 1f, 0.7f),
                glowDistance = 4f,
                enableShadow = true,
                shadowColor = new Color(0.2f, 0.1f, 0.3f, 0.6f),
                shadowDistance = new Vector2(2, -2)
            };
        }
        
        // Expand the array to include the new eggshell + gold style
        if (menuStyles.Length == 4)
        {
            var expandedStyles = new MenuStyle[5];
            for (int i = 0; i < menuStyles.Length; i++)
            {
                expandedStyles[i] = menuStyles[i];
            }
            
            // Eggshell + Gold
            expandedStyles[4] = new MenuStyle
            {
                styleName = "Eggshell Gold",
                normalColor = new Color(0.96f, 0.94f, 0.87f, 1f), // Eggshell base
                hoverColor = new Color(0.98f, 0.96f, 0.89f, 1f), // Light eggshell on hover
                normalSize = 24f,
                hoverSize = 27f,
                useGradient = true,
                gradientTopLeft = new Color(0.98f, 0.96f, 0.89f, 1f), // Light eggshell
                gradientTopRight = new Color(0.98f, 0.96f, 0.89f, 1f),
                gradientBottomLeft = new Color(1f, 0.84f, 0.4f, 1f), // Gold
                gradientBottomRight = new Color(1f, 0.84f, 0.4f, 1f),
                hoverGradientTopLeft = new Color(1f, 0.98f, 0.92f, 1f), // Brighter eggshell
                hoverGradientTopRight = new Color(1f, 0.98f, 0.92f, 1f),
                hoverGradientBottomLeft = new Color(1f, 0.88f, 0.5f, 1f), // Brighter gold
                hoverGradientBottomRight = new Color(1f, 0.88f, 0.5f, 1f),
                enableGlow = true,
                glowColor = new Color(1f, 0.9f, 0.6f, 0.7f), // Warm golden glow
                glowDistance = 3f,
                enableShadow = true,
                shadowColor = new Color(0.4f, 0.3f, 0.2f, 0.5f), // Warm brown shadow
                shadowDistance = new Vector2(2, -2)
            };
            
            menuStyles = expandedStyles;
        }
    }

    private void RefreshUIEffects()
    {
        if (currentStyleIndex < 0 || currentStyleIndex >= menuStyles.Length) return;
        MenuStyle style = menuStyles[currentStyleIndex];
        
        // Remove existing effects
        if (textShadow != null)
        {
            if (Application.isPlaying)
                Destroy(textShadow);
            else
                DestroyImmediate(textShadow);
        }
        
        if (textGlow != null)
        {
            if (Application.isPlaying)
                Destroy(textGlow);
            else
                DestroyImmediate(textGlow);
        }
        
        // Add shadow if enabled
        if (style.enableShadow)
        {
            textShadow = gameObject.AddComponent<Shadow>();
            textShadow.effectColor = style.shadowColor;
            textShadow.effectDistance = style.shadowDistance;
        }
        
        // Add glow effect using Outline
        if (style.enableGlow)
        {
            textGlow = gameObject.AddComponent<Outline>();
            textGlow.effectColor = style.glowColor;
            textGlow.effectDistance = new Vector2(style.glowDistance, style.glowDistance);
        }
    }

    public void ApplyCurrentStyle()
    {
        if (currentStyleIndex < 0 || currentStyleIndex >= menuStyles.Length) return;
        MenuStyle style = menuStyles[currentStyleIndex];
        
        // Apply gradient using TextMeshPro's built-in gradient
        if (style.useGradient)
        {
            VertexGradient gradient = new VertexGradient();
            if (isHovering)
            {
                gradient.topLeft = style.hoverGradientTopLeft;
                gradient.topRight = style.hoverGradientTopRight;
                gradient.bottomLeft = style.hoverGradientBottomLeft;
                gradient.bottomRight = style.hoverGradientBottomRight;
            }
            else
            {
                gradient.topLeft = style.gradientTopLeft;
                gradient.topRight = style.gradientTopRight;
                gradient.bottomLeft = style.gradientBottomLeft;
                gradient.bottomRight = style.gradientBottomRight;
            }
            textComponent.colorGradient = gradient;
            textComponent.enableVertexGradient = true;
        }
        else
        {
            textComponent.enableVertexGradient = false;
            textComponent.color = isHovering ? style.hoverColor : style.normalColor;
        }
        
        // Apply size
        textComponent.fontSize = isHovering ? style.hoverSize : style.normalSize;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!enableHoverEffects) return;
        
        isHovering = true;
        
        // Play sound
        if (hoverSound != null)
        {
            audioSource.PlayOneShot(hoverSound);
        }
        
        // Start hover animation
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateHover(true));
        
        // Play sparkles
        if (enableSparkles)
        {
            StartCoroutine(PlaySparkles());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableHoverEffects) return;
        
        isHovering = false;
        
        // Start exit animation
        if (hoverCoroutine != null) StopCoroutine(hoverCoroutine);
        hoverCoroutine = StartCoroutine(AnimateHover(false));
    }

    private IEnumerator AnimateHover(bool entering)
    {
        if (currentStyleIndex < 0 || currentStyleIndex >= menuStyles.Length) yield break;
        MenuStyle style = menuStyles[currentStyleIndex];
        
        float duration = 1f / style.animationSpeed;
        float elapsed = 0f;
        
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = entering ? originalScale * style.hoverScale : originalScale;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Animate scale
            transform.localScale = Vector3.Lerp(startScale, targetScale, style.scaleCurve.Evaluate(t));
            
            // Update style colors/gradients
            ApplyCurrentStyle();
            
            yield return null;
        }
        
        transform.localScale = targetScale;
        ApplyCurrentStyle();
    }

    private IEnumerator PlaySparkles()
    {
        for (int i = 0; i < sparkleCount; i++)
        {
            CreateSparkle();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void CreateSparkle()
    {
        // Create a simple sparkle using UI elements
        GameObject sparkle = new GameObject("Sparkle");
        sparkle.transform.SetParent(transform.parent, false);
        
        Image sparkleImage = sparkle.AddComponent<Image>();
        RectTransform sparkleRect = sparkle.GetComponent<RectTransform>();
        
        // Position randomly around text
        Vector2 randomPos = Random.insideUnitCircle * sparkleRadius;
        sparkleRect.anchoredPosition = rectTransform.anchoredPosition + randomPos;
        sparkleRect.sizeDelta = Vector2.one * 4f;
        
        // Create simple star texture
        sparkleImage.sprite = CreateStarSprite();
        sparkleImage.color = menuStyles[currentStyleIndex].glowColor;
        
        // Animate sparkle
        StartCoroutine(AnimateSparkle(sparkle));
    }

    private IEnumerator AnimateSparkle(GameObject sparkle)
    {
        if (sparkle == null) yield break;
        
        Image image = sparkle.GetComponent<Image>();
        RectTransform rect = sparkle.GetComponent<RectTransform>();
        
        float elapsed = 0f;
        Color startColor = image.color;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one;
        
        // Fade in and scale up
        while (elapsed < sparkleLifetime * 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (sparkleLifetime * 0.3f);
            
            image.color = Color.Lerp(new Color(startColor.r, startColor.g, startColor.b, 0), startColor, t);
            rect.localScale = Vector3.Lerp(startScale, endScale, t);
            
            yield return null;
        }
        
        // Hold
        yield return new WaitForSeconds(sparkleLifetime * 0.4f);
        
        // Fade out
        elapsed = 0f;
        while (elapsed < sparkleLifetime * 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (sparkleLifetime * 0.3f);
            
            image.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0), t);
            rect.localScale = Vector3.Lerp(endScale, startScale, t);
            
            yield return null;
        }
        
        if (sparkle != null)
        {
            Destroy(sparkle);
        }
    }

    private Sprite CreateStarSprite()
    {
        // Create a simple 8x8 star texture
        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Color[] colors = new Color[64];
        
        // Simple star pattern
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool isStar = (x == 4 || y == 4 || x == y || x + y == 7);
                colors[y * 8 + x] = isStar ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f));
    }

    private void StartBreathingEffect()
    {
        if (breathingCoroutine != null) StopCoroutine(breathingCoroutine);
        breathingCoroutine = StartCoroutine(BreathingLoop());
    }

    private IEnumerator BreathingLoop()
    {
        while (enableBreathingEffect)
        {
            float breathe = Mathf.Sin(Time.time) * 0.02f;
            transform.localScale = originalScale + Vector3.one * breathe;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }

    // Public methods for runtime control
    [ContextMenu("Next Style")]
    public void NextStyle()
    {
        currentStyleIndex = (currentStyleIndex + 1) % menuStyles.Length;
        RefreshUIEffects();
        ApplyCurrentStyle();
    }

    [ContextMenu("Previous Style")]
    public void PreviousStyle()
    {
        currentStyleIndex = (currentStyleIndex - 1 + menuStyles.Length) % menuStyles.Length;
        RefreshUIEffects();
        ApplyCurrentStyle();
    }

    public void SetStyle(int index)
    {
        if (index >= 0 && index < menuStyles.Length)
        {
            currentStyleIndex = index;
            RefreshUIEffects();
            ApplyCurrentStyle();
        }
    }

    // Public properties for external access to runtime controls
    public bool EnableHoverEffects
    {
        get { return enableHoverEffects; }
        set { enableHoverEffects = value; }
    }

    public bool EnableSparkles
    {
        get { return enableSparkles; }
        set { enableSparkles = value; }
    }

    public bool EnableBreathingEffect
    {
        get { return enableBreathingEffect; }
        set 
        { 
            enableBreathingEffect = value;
            if (value && Application.isPlaying)
            {
                StartBreathingEffect();
            }
            else if (!value && breathingCoroutine != null)
            {
                StopCoroutine(breathingCoroutine);
                transform.localScale = originalScale;
            }
        }
    }

    public int CurrentStyleIndex
    {
        get { return currentStyleIndex; }
        set { SetStyle(value); }
    }

    private void OnValidate()
    {
        if (menuStyles == null || menuStyles.Length == 0)
        {
            SetupDefaultStyles();
        }
        
        if (Application.isPlaying && textComponent != null)
        {
            RefreshUIEffects();
            ApplyCurrentStyle();
        }
    }
} 