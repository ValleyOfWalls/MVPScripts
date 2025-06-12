using UnityEngine;

#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

[RequireComponent(typeof(Light))]
public class LightAnimator : MonoBehaviour
{
    [Header("Intensity Animation")]
    [SerializeField] private bool animateIntensity = true;
    [SerializeField] private float baseIntensity = 1f;
    [SerializeField] private float intensityAmplitude = 0.5f;
    [SerializeField] private float intensitySpeed = 2f;
    [SerializeField] private bool useRandomIntensityOffset = true;
    
    [Header("Color Animation")]
    [SerializeField] private bool animateColor = false;
    [SerializeField] private Color baseColor = Color.white;
    [SerializeField] private Color targetColor = Color.yellow;
    [SerializeField] private float colorSpeed = 3f;
    [SerializeField] private bool useRandomColorOffset = true;
    
    [Header("Range Animation (Point/Spot Lights)")]
    [SerializeField] private bool animateRange = false;
    [SerializeField] private float baseRange = 10f;
    [SerializeField] private float rangeAmplitude = 3f;
    [SerializeField] private float rangeSpeed = 1.5f;
    [SerializeField] private bool useRandomRangeOffset = true;
    
    [Header("Spot Angle Animation (Spot Lights Only)")]
    [SerializeField] private bool animateSpotAngle = false;
    [SerializeField] private float baseSpotAngle = 30f;
    [SerializeField] private float spotAngleAmplitude = 15f;
    [SerializeField] private float spotAngleSpeed = 2.5f;
    [SerializeField] private bool useRandomSpotAngleOffset = true;
    
    [Header("Settings")]
    [SerializeField] private bool useDOTween = true;
    [SerializeField] private bool playOnStart = true;
    
    private Light lightComponent;
    private float intensityRandomOffset;
    private float colorRandomOffset;
    private float rangeRandomOffset;
    private float spotAngleRandomOffset;
    
#if DOTWEEN_ENABLED
    private Tween intensityTween;
    private Tween colorTween;
    private Tween rangeTween;
    private Tween spotAngleTween;
#endif

    void Awake()
    {
        lightComponent = GetComponent<Light>();
    }

    void Start()
    {
        if (!lightComponent)
        {
            Debug.LogError("LightAnimator: No Light component found!");
            enabled = false;
            return;
        }

        // Initialize random offsets
        intensityRandomOffset = useRandomIntensityOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        colorRandomOffset = useRandomColorOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        rangeRandomOffset = useRandomRangeOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        spotAngleRandomOffset = useRandomSpotAngleOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        
        // Store initial values
        baseIntensity = lightComponent.intensity;
        baseColor = lightComponent.color;
        baseRange = lightComponent.range;
        
        if (lightComponent.type == LightType.Spot)
        {
            baseSpotAngle = lightComponent.spotAngle;
        }

        if (playOnStart)
        {
            StartAnimating();
        }
    }
    
    public void StartAnimating()
    {
#if DOTWEEN_ENABLED
        if (useDOTween && DOTween.instance != null)
        {
            StartDOTweenAnimations();
        }
        else
        {
            StartBuiltInAnimations();
        }
#else
        StartBuiltInAnimations();
#endif
    }
    
#if DOTWEEN_ENABLED
    void StartDOTweenAnimations()
    {
        StopAllTweens();
        
        if (animateIntensity)
        {
            float targetIntensity = baseIntensity + intensityAmplitude;
            intensityTween = DOTween.To(() => lightComponent.intensity, x => lightComponent.intensity = x, 
                targetIntensity, intensitySpeed / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(intensityRandomOffset * (intensitySpeed / 2f) / (2f * Mathf.PI));
        }
        
        if (animateColor)
        {
            colorTween = DOTween.To(() => lightComponent.color, x => lightComponent.color = x, 
                targetColor, colorSpeed / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(colorRandomOffset * (colorSpeed / 2f) / (2f * Mathf.PI));
        }
        
        if (animateRange && (lightComponent.type == LightType.Point || lightComponent.type == LightType.Spot))
        {
            float targetRange = baseRange + rangeAmplitude;
            rangeTween = DOTween.To(() => lightComponent.range, x => lightComponent.range = x, 
                targetRange, rangeSpeed / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(rangeRandomOffset * (rangeSpeed / 2f) / (2f * Mathf.PI));
        }
        
        if (animateSpotAngle && lightComponent.type == LightType.Spot)
        {
            float targetSpotAngle = Mathf.Clamp(baseSpotAngle + spotAngleAmplitude, 1f, 179f);
            spotAngleTween = DOTween.To(() => lightComponent.spotAngle, x => lightComponent.spotAngle = x, 
                targetSpotAngle, spotAngleSpeed / 2f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetDelay(spotAngleRandomOffset * (spotAngleSpeed / 2f) / (2f * Mathf.PI));
        }
    }
#endif
    
    void StartBuiltInAnimations()
    {
        // Built-in animations will be handled in Update()
    }
    
    void Update()
    {
#if DOTWEEN_ENABLED
        if (useDOTween && DOTween.instance != null && HasActiveTweens())
        {
            return; // DOTween is handling the animations
        }
#endif
        
        // Built-in animation fallback
        HandleBuiltInAnimations();
    }
    
#if DOTWEEN_ENABLED
    bool HasActiveTweens()
    {
        return (intensityTween != null && intensityTween.IsActive()) ||
               (colorTween != null && colorTween.IsActive()) ||
               (rangeTween != null && rangeTween.IsActive()) ||
               (spotAngleTween != null && spotAngleTween.IsActive());
    }
#endif
    
    void HandleBuiltInAnimations()
    {
        if (animateIntensity)
        {
            float intensityOffset = Mathf.Sin((Time.time * intensitySpeed) + intensityRandomOffset) * intensityAmplitude;
            lightComponent.intensity = baseIntensity + intensityOffset;
        }
        
        if (animateColor)
        {
            float colorLerp = (Mathf.Sin((Time.time * colorSpeed) + colorRandomOffset) + 1f) / 2f;
            lightComponent.color = Color.Lerp(baseColor, targetColor, colorLerp);
        }
        
        if (animateRange && (lightComponent.type == LightType.Point || lightComponent.type == LightType.Spot))
        {
            float rangeOffset = Mathf.Sin((Time.time * rangeSpeed) + rangeRandomOffset) * rangeAmplitude;
            lightComponent.range = Mathf.Max(0.1f, baseRange + rangeOffset);
        }
        
        if (animateSpotAngle && lightComponent.type == LightType.Spot)
        {
            float spotAngleOffset = Mathf.Sin((Time.time * spotAngleSpeed) + spotAngleRandomOffset) * spotAngleAmplitude;
            lightComponent.spotAngle = Mathf.Clamp(baseSpotAngle + spotAngleOffset, 1f, 179f);
        }
    }
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        baseIntensity = Mathf.Max(0f, baseIntensity);
        intensityAmplitude = Mathf.Max(0f, intensityAmplitude);
        intensitySpeed = Mathf.Max(0.1f, intensitySpeed);
        
        baseRange = Mathf.Max(0.1f, baseRange);
        rangeAmplitude = Mathf.Max(0f, rangeAmplitude);
        rangeSpeed = Mathf.Max(0.1f, rangeSpeed);
        
        baseSpotAngle = Mathf.Clamp(baseSpotAngle, 1f, 179f);
        spotAngleAmplitude = Mathf.Max(0f, spotAngleAmplitude);
        spotAngleSpeed = Mathf.Max(0.1f, spotAngleSpeed);
        
        colorSpeed = Mathf.Max(0.1f, colorSpeed);
    }
    
    void OnDestroy()
    {
        StopAllTweens();
    }
    
    void OnDisable()
    {
        StopAllTweens();
    }
    
    void StopAllTweens()
    {
#if DOTWEEN_ENABLED
        intensityTween?.Kill();
        colorTween?.Kill();
        rangeTween?.Kill();
        spotAngleTween?.Kill();
#endif
    }
    
    // Public methods for runtime control
    public void SetIntensityAnimation(float baseIntensity, float amplitude, float speed)
    {
        this.baseIntensity = baseIntensity;
        this.intensityAmplitude = amplitude;
        this.intensitySpeed = speed;
        
        if (animateIntensity && gameObject.activeInHierarchy)
        {
            StartAnimating();
        }
    }
    
    public void SetColorAnimation(Color baseColor, Color targetColor, float speed)
    {
        this.baseColor = baseColor;
        this.targetColor = targetColor;
        this.colorSpeed = speed;
        
        if (animateColor && gameObject.activeInHierarchy)
        {
            StartAnimating();
        }
    }
    
    public void StopAnimating()
    {
        StopAllTweens();
        
        // Reset to base values
        if (lightComponent)
        {
            lightComponent.intensity = baseIntensity;
            lightComponent.color = baseColor;
            lightComponent.range = baseRange;
            if (lightComponent.type == LightType.Spot)
            {
                lightComponent.spotAngle = baseSpotAngle;
            }
        }
    }
    
    public void ResumeAnimating()
    {
        StartAnimating();
    }
    
    public void SetAnimationEnabled(bool intensity, bool color, bool range, bool spotAngle)
    {
        animateIntensity = intensity;
        animateColor = color;
        animateRange = range;
        animateSpotAngle = spotAngle;
        
        if (gameObject.activeInHierarchy)
        {
            StartAnimating();
        }
    }
} 