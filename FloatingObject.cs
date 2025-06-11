using UnityEngine;

#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

public class FloatingObject : MonoBehaviour
{
    [Header("Float Settings")]
    [SerializeField] private float floatAmplitude = 1f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private bool useRandomOffset = true;
    [SerializeField] private bool useDOTween = true;
    
    [Header("Rotation (Optional)")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 30, 0);
    
    private Vector3 startPosition;
    private float randomOffset;
    
#if DOTWEEN_ENABLED
    private Tween floatTween;
#endif

    void Start()
    {
        startPosition = transform.position;
        randomOffset = useRandomOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        
        StartFloating();
    }
    
    void StartFloating()
    {
#if DOTWEEN_ENABLED
        if (useDOTween && DOTween.instance != null)
        {
            StartDOTweenFloating();
        }
        else
        {
            StartBuiltInFloating();
        }
#else
        // DOTween not available, always use built-in floating
        StartBuiltInFloating();
#endif
    }
    
#if DOTWEEN_ENABLED
    void StartDOTweenFloating()
    {
        // Kill any existing tween
        floatTween?.Kill();
        
        // Create a smooth floating animation using DOTween
        Vector3 targetPos = startPosition + Vector3.up * floatAmplitude;
        
        floatTween = transform.DOMove(targetPos, floatSpeed / 2f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetDelay(randomOffset * (floatSpeed / 2f) / (2f * Mathf.PI));
    }
#endif
    
    void StartBuiltInFloating()
    {
        // Use Unity's built-in animation system as fallback
        // This will be handled in Update()
    }
    
    void Update()
    {
#if DOTWEEN_ENABLED
        if (useDOTween && DOTween.instance != null && floatTween != null && floatTween.IsActive())
        {
            // DOTween is handling the floating, just handle rotation if enabled
            HandleRotation();
            return;
        }
#endif
        
        // Built-in floating animation
        HandleBuiltInFloating();
        HandleRotation();
    }
    
    void HandleBuiltInFloating()
    {
        float yOffset = Mathf.Sin((Time.time * floatSpeed) + randomOffset) * floatAmplitude;
        transform.position = startPosition + Vector3.up * yOffset;
    }
    
    void HandleRotation()
    {
        if (enableRotation)
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);
        }
    }
    
    void OnValidate()
    {
        // Clamp values to reasonable ranges
        floatAmplitude = Mathf.Max(0f, floatAmplitude);
        floatSpeed = Mathf.Max(0.1f, floatSpeed);
    }
    
    void OnDestroy()
    {
#if DOTWEEN_ENABLED
        floatTween?.Kill();
#endif
    }
    
    void OnDisable()
    {
#if DOTWEEN_ENABLED
        floatTween?.Kill();
#endif
    }
    
    // Public methods to control the floating at runtime
    public void SetFloatAmplitude(float amplitude)
    {
        floatAmplitude = amplitude;
        if (gameObject.activeInHierarchy)
        {
            StartFloating();
        }
    }
    
    public void SetFloatSpeed(float speed)
    {
        floatSpeed = speed;
        if (gameObject.activeInHierarchy)
        {
            StartFloating();
        }
    }
    
    public void StopFloating()
    {
#if DOTWEEN_ENABLED
        floatTween?.Kill();
#endif
        transform.position = startPosition;
    }
    
    public void ResumeFloating()
    {
        StartFloating();
    }
} 