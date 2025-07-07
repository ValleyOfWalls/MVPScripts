using UnityEngine;
using DG.Tweening;

public class FloatingObject : MonoBehaviour
{
    [Header("Float Settings")]
    [SerializeField] private float floatAmplitude = 1f;
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private bool useRandomOffset = true;
    
    [Header("Rotation (Optional)")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private Vector3 rotationSpeed = new Vector3(0, 30, 0);
    
    private Vector3 startPosition;
    private float randomOffset;
    private Tween floatTween;

    void Start()
    {
        startPosition = transform.position;
        randomOffset = useRandomOffset ? Random.Range(0f, 2f * Mathf.PI) : 0f;
        
        StartFloating();
    }
    
    void StartFloating()
    {
        StartDOTweenFloating();
    }
    
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
    
    void Update()
    {
        // Handle rotation if enabled
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
        floatTween?.Kill();
    }
    
    void OnDisable()
    {
        floatTween?.Kill();
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
        floatTween?.Kill();
        transform.position = startPosition;
    }
    
    public void ResumeFloating()
    {
        StartFloating();
    }
} 