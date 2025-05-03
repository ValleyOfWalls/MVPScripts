using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class Outline : MonoBehaviour
{
    [Header("Outline Settings")]
    [SerializeField] private Color outlineColor = Color.yellow;
    [SerializeField] private float outlineWidth = 0.1f;
    
    private Renderer targetRenderer;
    private Material outlineMaterial;
    private GameObject outlineObject;
    
    private void Awake()
    {
        // Get the renderer
        targetRenderer = GetComponent<Renderer>();
        
        // Create outline object
        CreateOutlineObject();
        
        // Disable by default
        enabled = false;
    }
    
    private void CreateOutlineObject()
    {
        // Create a child game object for the outline
        outlineObject = new GameObject("Outline");
        outlineObject.transform.SetParent(transform);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * (1f + outlineWidth);
        
        // Add renderer
        SpriteRenderer outlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        
        // Copy properties from the target renderer
        if (targetRenderer is SpriteRenderer spriteRenderer)
        {
            outlineRenderer.sprite = spriteRenderer.sprite;
            
            // Create outline material
            outlineMaterial = new Material(Shader.Find("Sprites/Default"));
            outlineMaterial.color = outlineColor;
            
            // Assign material
            outlineRenderer.material = outlineMaterial;
            
            // Set renderer order
            outlineRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
            outlineRenderer.sortingOrder = spriteRenderer.sortingOrder - 1; // Behind the target sprite
        }
        
        // Initially disabled
        outlineObject.SetActive(false);
    }
    
    private void OnEnable()
    {
        // Show outline
        if (outlineObject != null)
        {
            outlineObject.SetActive(true);
        }
    }
    
    private void OnDisable()
    {
        // Hide outline
        if (outlineObject != null)
        {
            outlineObject.SetActive(false);
        }
    }
    
    public void SetColor(Color color)
    {
        outlineColor = color;
        if (outlineMaterial != null)
        {
            outlineMaterial.color = outlineColor;
        }
    }
} 