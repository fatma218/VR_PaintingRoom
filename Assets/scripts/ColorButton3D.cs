using UnityEngine;

public class ColorChangerButton : MonoBehaviour
{
    [Header("Configuration")]
    public Color buttonColor = Color.red;
    
    [Header("Références")]
    public Brush brushToChange; // GLISSE le pinceau ici !
    
    [Header("Effets")]
    public AudioClip clickSound;
    private Vector3 originalScale;
    private Renderer buttonRenderer;
    private Material buttonMaterial;
    
    void Start()
    {
        originalScale = transform.localScale;
        buttonRenderer = GetComponent<Renderer>();
        
        // Créer un matériel unique pour le bouton
        if (buttonRenderer != null)
        {
            buttonMaterial = new Material(buttonRenderer.material);
            buttonMaterial.color = buttonColor;
            buttonRenderer.material = buttonMaterial;
        }
        
        // Trouver le pinceau si non assigné
        if (brushToChange == null)
        {
            brushToChange = FindObjectOfType<Brush>();
            if (brushToChange != null)
                Debug.Log($"✅ Pinceau trouvé: {brushToChange.name}");
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Vérifier si c'est le pinceau
        Brush enteringBrush = other.GetComponent<Brush>();
        if (enteringBrush != null)
        {
            ChangeBrushColor(enteringBrush);
            return;
        }
        
        // Vérifier dans le parent (pour modèles complexes)
        Brush parentBrush = other.GetComponentInParent<Brush>();
        if (parentBrush != null)
        {
            ChangeBrushColor(parentBrush);
            return;
        }
        
        // Si on a une référence directe
        if (brushToChange != null && other.gameObject == brushToChange.gameObject)
        {
            ChangeBrushColor(brushToChange);
        }
    }
    
    void ChangeBrushColor(Brush brush)
    {
        if (brush == null) return;
        
        // 1. Changer la couleur du pinceau (ça va aussi changer la pointe)
        brush.SetColor(buttonColor);
        
        // 2. Effet sur le bouton
        StartCoroutine(ButtonEffect());
        
        // 3. Son
        if (clickSound != null)
            AudioSource.PlayClipAtPoint(clickSound, transform.position);
        
        Debug.Log($"🎨 Pinceau changé en {buttonColor}");
    }
    
    System.Collections.IEnumerator ButtonEffect()
    {
        // Effet de pulsation
        transform.localScale = originalScale * 1.4f;
        if (buttonMaterial != null)
            buttonMaterial.SetColor("_EmissionColor", buttonColor * 0.5f);
        
        yield return new WaitForSeconds(0.15f);
        
        transform.localScale = originalScale;
        if (buttonMaterial != null)
            buttonMaterial.SetColor("_EmissionColor", Color.black);
    }
    
    // Pour tester avec la souris
    #if UNITY_EDITOR
    void OnMouseDown()
    {
        if (!Application.isPlaying) return;
        
        if (brushToChange != null)
        {
            brushToChange.SetColor(buttonColor);
            StartCoroutine(ButtonEffect());
        }
    }
    #endif
}