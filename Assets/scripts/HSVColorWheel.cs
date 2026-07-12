using UnityEngine;

/// <summary>
/// Roue de couleurs HSV interactive (Quad plat) pour VR.
/// Genere une texture procedurale: teinte/saturation sur un cercle, 
/// avec un slider de luminosite (value) sur le cote droit.
/// Le pinceau (Brush) detecte la couleur sous son rayon et l'applique.
/// A placer sur un GameObject Quad avec un Collider (BoxCollider/MeshCollider).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class HSVColorWheel : MonoBehaviour
{
    [Header("=== PARAMETRES TEXTURE ===")]
    [SerializeField] private int textureSize = 512;

    [Header("=== ZONE ROUE (en UV 0-1) ===")]
    [Tooltip("Centre de la roue HS en coordonnees UV")]
    [SerializeField] private Vector2 wheelCenter = new Vector2(0.4f, 0.5f);
    [Tooltip("Rayon de la roue HS en UV")]
    [SerializeField] private float wheelRadius = 0.4f;

    [Header("=== ZONE SLIDER VALUE (en UV 0-1) ===")]
    [Tooltip("X minimum du slider de luminosite")]
    [SerializeField] private float valueSliderXMin = 0.85f;
    [Tooltip("X maximum du slider de luminosite")]
    [SerializeField] private float valueSliderXMax = 0.98f;

    [Header("=== REFERENCES ===")]
    [SerializeField] private Renderer wheelRenderer;
    [Tooltip("Indicateur visuel de la couleur actuellement selectionnee (optionnel)")]
    [SerializeField] private Renderer selectionPreviewRenderer;

    [Header("=== DEBOGAGE ===")]
    [SerializeField] private bool debugMode = false;

    private Texture2D wheelTexture;
    private float currentHue = 0f;
    private float currentSaturation = 0f;
    private float currentValue = 1f;

    #region PROPRIETES PUBLIQUES

    /// <summary>
    /// Couleur RGB actuellement selectionnee sur la roue
    /// </summary>
    public Color CurrentColor => Color.HSVToRGB(currentHue, currentSaturation, currentValue);

    #endregion

    #region METHODES UNITY

    void Start()
    {
        InitializeWheel();
    }

    #endregion

    #region INITIALISATION

    private void InitializeWheel()
    {
        if (wheelRenderer == null)
            wheelRenderer = GetComponent<Renderer>();

        GenerateWheelTexture();
        ApplyTextureToMaterial();
        UpdateSelectionPreview();

        if (debugMode)
            Debug.Log($"HSVColorWheel initialise: {textureSize}x{textureSize}");
    }

    /// <summary>
    /// Genere la texture: roue HS (teinte/saturation) + slider de luminosite
    /// </summary>
    private void GenerateWheelTexture()
    {
        wheelTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[textureSize * textureSize];

        for (int x = 0; x < textureSize; x++)
        {
            for (int y = 0; y < textureSize; y++)
            {
                float u = (float)x / textureSize;
                float v = (float)y / textureSize;

                Color pixelColor;

                // Zone du slider de luminosite (bande verticale a droite)
                if (u >= valueSliderXMin && u <= valueSliderXMax)
                {
                    // De bas (noir) a haut (couleur courante a value max)
                    float val = v;
                    pixelColor = Color.HSVToRGB(currentHue, currentSaturation, val);
                }
                else
                {
                    // Roue HS : distance/angle par rapport au centre
                    Vector2 pos = new Vector2(u, v) - wheelCenter;
                    float dist = pos.magnitude;

                    if (dist <= wheelRadius)
                    {
                        float hue = (Mathf.Atan2(pos.y, pos.x) / (2f * Mathf.PI) + 1f) % 1f;
                        float sat = Mathf.Clamp01(dist / wheelRadius);
                        pixelColor = Color.HSVToRGB(hue, sat, currentValue);
                    }
                    else
                    {
                        // Hors roue : transparent / fond neutre
                        pixelColor = new Color(0.15f, 0.15f, 0.15f, 1f);
                    }
                }

                pixels[y * textureSize + x] = pixelColor;
            }
        }

        wheelTexture.SetPixels(pixels);
        wheelTexture.Apply();
    }

    /// <summary>
    /// Applique la texture au materiau du renderer
    /// </summary>
    private void ApplyTextureToMaterial()
    {
        if (wheelRenderer == null || wheelTexture == null) return;

        Material newMaterial = new Material(wheelRenderer.material);
        newMaterial.mainTexture = wheelTexture;
        wheelRenderer.material = newMaterial;
    }

    #endregion

    #region INTERACTION

    /// <summary>
    /// Appelee par le Brush (raycast) quand il touche cette roue.
    /// uv: coordonnees UV du point touche.
    /// Retourne la couleur correspondante et met a jour la selection.
    /// </summary>
    public Color PickColorAt(Vector2 uv)
    {
        float u = uv.x;
        float v = uv.y;

        // Slider de luminosite
        if (u >= valueSliderXMin && u <= valueSliderXMax)
        {
            currentValue = Mathf.Clamp01(v);
            RegenerateAndApply();

            if (debugMode)
                Debug.Log($"Value slider -> V={currentValue:F2} | Couleur: {CurrentColor}");

            return CurrentColor;
        }

        // Roue HS
        Vector2 pos = new Vector2(u, v) - wheelCenter;
        float dist = pos.magnitude;

        if (dist <= wheelRadius)
        {
            currentHue = (Mathf.Atan2(pos.y, pos.x) / (2f * Mathf.PI) + 1f) % 1f;
            currentSaturation = Mathf.Clamp01(dist / wheelRadius);
            RegenerateAndApply();

            if (debugMode)
                Debug.Log($"Roue HS -> H={currentHue:F2} S={currentSaturation:F2} | Couleur: {CurrentColor}");

            return CurrentColor;
        }

        // Hors zone interactive : pas de changement
        return CurrentColor;
    }

    /// <summary>
    /// Regenere la texture (utile car le slider de value depend de H/S courants)
    /// et met a jour le previsualisateur
    /// </summary>
    private void RegenerateAndApply()
    {
        GenerateWheelTexture();
        ApplyTextureToMaterial();
        UpdateSelectionPreview();
    }

    /// <summary>
    /// Met a jour l'objet de previsualisation (s'il existe) avec la couleur actuelle
    /// </summary>
    private void UpdateSelectionPreview()
    {
        if (selectionPreviewRenderer != null)
        {
            Material mat = new Material(selectionPreviewRenderer.material);
            mat.color = CurrentColor;
            selectionPreviewRenderer.material = mat;
        }
    }

    #endregion
}
