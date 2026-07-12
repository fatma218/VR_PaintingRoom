using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Renderer))]
public class DrawingBoard : MonoBehaviour
{
    [Header("=== PARAMÈTRES TEXTURE ===")]
    [SerializeField] private int textureWidth = 1024;
    [SerializeField] private int textureHeight = 1024;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private FilterMode textureFilterMode = FilterMode.Bilinear;
    
    [Header("=== PARAMÈTRES PINCEAU ===")]
    [SerializeField] private float brushSoundDelay = 0.1f;
    [SerializeField] private AudioSource brushSound;
    
    [Header("=== RÉFÉRENCES ===")]
    [SerializeField] private Renderer boardRenderer;
    
    [Header("=== DÉBOGAGE ===")]
    [SerializeField] private bool debugMode = false;
    
    // Variables privées
    private Texture2D drawTexture;
    private bool isDrawing = false;
    private float lastDrawTime = 0f;
    
    #region PROPRIÉTÉS PUBLIQUES
    
    /// <summary>
    /// Texture actuelle du canvas (lecture seule)
    /// </summary>
    public Texture2D CurrentTexture => drawTexture;
    
    /// <summary>
    /// Dimensions de la texture
    /// </summary>
    public Vector2Int TextureSize => new Vector2Int(textureWidth, textureHeight);
    
    /// <summary>
    /// Couleur de fond
    /// </summary>
    public Color BackgroundColor => backgroundColor;
    
    #endregion
    
    #region MÉTHODES UNITY
    
    void Start()
    {
        InitializeBoard();
    }
    
    void Update()
    {
        // Arrêter le son après un délai d'inactivité
        if (isDrawing && Time.time - lastDrawTime > brushSoundDelay)
        {
            StopBrushSound();
        }
    }
    
    #endregion
    
    #region INITIALISATION
    
    /// <summary>
    /// Initialise le canvas avec une texture vierge
    /// </summary>
    private void InitializeBoard()
    {
        // Trouver le renderer si non assigné
        if (boardRenderer == null)
            boardRenderer = GetComponent<Renderer>();
        
        // Créer la texture
        CreateBlankTexture();
        
        // Remplir avec la couleur de fond
        FillWithBackgroundColor();
        
        // Appliquer au matériel
        ApplyTextureToMaterial();
        
        if (debugMode)
            Debug.Log($"✅ Canvas initialisé: {textureWidth}x{textureHeight}");
    }
    
    /// <summary>
    /// Crée une texture vierge
    /// </summary>
    private void CreateBlankTexture()
    {
        drawTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            filterMode = textureFilterMode,
            wrapMode = TextureWrapMode.Clamp
        };
    }
    
    /// <summary>
    /// Remplit la texture avec la couleur de fond
    /// </summary>
    private void FillWithBackgroundColor()
    {
        if (drawTexture == null) return;
        
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;
        
        drawTexture.SetPixels(pixels);
        drawTexture.Apply();
    }
    
    /// <summary>
    /// Applique la texture au matériel du renderer
    /// </summary>
    private void ApplyTextureToMaterial()
    {
        if (boardRenderer != null && drawTexture != null)
        {
            // Créer une copie du matériel pour éviter les partages
            Material newMaterial = new Material(boardRenderer.material);
            newMaterial.mainTexture = drawTexture;
            boardRenderer.material = newMaterial;
        }
    }
    
    #endregion
    
    #region MÉTHODES DE DESSIN
    
    /// <summary>
    /// Dessine à une position UV spécifique
    /// </summary>
    /// <param name="uv">Coordonnées UV (0-1)</param>
    /// <param name="color">Couleur du pinceau</param>
    /// <param name="brushSize">Taille du pinceau en pixels</param>
    /// <param name="brushHardness">Dureté du pinceau (0.1 = doux, 2 = dur)</param>
    /// <param name="brushOpacity">Opacité (0-1)</param>
    public void DrawAt(Vector2 uv, Color color, int brushSize = 20, 
                      float brushHardness = 1f, float brushOpacity = 1f)
    {
        if (drawTexture == null) 
        {
            if (debugMode) Debug.LogWarning("Texture non initialisée!");
            return;
        }
        
        bool paintedSomething = false;
        
        // Convertir UV en coordonnées pixels
        int centerX = Mathf.RoundToInt(uv.x * textureWidth);
        int centerY = Mathf.RoundToInt(uv.y * textureHeight);
        
        // Calculer les limites pour l'optimisation
        int startX = Mathf.Max(0, centerX - brushSize);
        int endX = Mathf.Min(textureWidth - 1, centerX + brushSize);
        int startY = Mathf.Max(0, centerY - brushSize);
        int endY = Mathf.Min(textureHeight - 1, centerY + brushSize);
        
        // Parcourir la zone du pinceau
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), 
                                                 new Vector2(centerX, centerY));
                
                if (distance < brushSize)
                {
                    // Calculer l'opacité avec fonction de décroissance
                    float normalizedDistance = distance / brushSize;
                    float falloff = Mathf.Pow(1 - normalizedDistance, brushHardness);
                    float opacity = falloff * brushOpacity;
                    
                    // Préparer la couleur avec alpha
                    Color paintColor = color;
                    paintColor.a = opacity;
                    
                    // Lire la couleur actuelle
                    Color currentColor = drawTexture.GetPixel(x, y);
                    
                    // Mélange alpha
                    Color blendedColor = BlendAlpha(currentColor, paintColor);
                    
                    // Appliquer
                    drawTexture.SetPixel(x, y, blendedColor);
                    paintedSomething = true;
                }
            }
        }
        
        if (paintedSomething)
        {
            // Mettre à jour le temps du dernier dessin
            lastDrawTime = Time.time;
            
            // Gérer le son du pinceau
            if (!isDrawing)
            {
                isDrawing = true;
                StartBrushSound();
            }
            
            // Appliquer les modifications à la texture
            drawTexture.Apply();
            
            if (debugMode)
                Debug.Log($"🎨 Dessin à UV: {uv} | Couleur: {color}");
        }
    }
    
    /// <summary>
    /// Mélange deux couleurs avec alpha blending
    /// </summary>
    private Color BlendAlpha(Color baseColor, Color overlayColor)
    {
        float alpha = overlayColor.a;
return baseColor * (1 - alpha) + overlayColor * alpha;
    }

    /// <summary>
    /// Dessine une ligne continue entre deux points UV (interpolation)
    /// Evite l'effet "cercles separes" quand le pinceau se deplace vite
    /// </summary>
    public void DrawLine(Vector2 uvFrom, Vector2 uvTo, Color color, int brushSize = 20,
                          float brushHardness = 1f, float brushOpacity = 1f)
    {
        if (drawTexture == null)
        {
            if (debugMode) Debug.LogWarning("Texture non initialisee!");
            return;
        }

        // Distance entre les deux points en pixels pour determiner le nombre de pas
        float pixelDist = Vector2.Distance(
            new Vector2(uvFrom.x * textureWidth, uvFrom.y * textureHeight),
            new Vector2(uvTo.x * textureWidth, uvTo.y * textureHeight));

        // Pas calcule pour garantir un chevauchement entre tampons (la moitie de la taille du pinceau)
        float step = Mathf.Max(1f, brushSize * 0.5f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(pixelDist / step));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector2 uv = Vector2.Lerp(uvFrom, uvTo, t);
            DrawAt(uv, color, brushSize, brushHardness, brushOpacity);
}
    }
    
    #endregion
    
    #region MÉTHODES DE GESTION
    
    /// <summary>
    /// Efface complètement le canvas (remet la couleur de fond)
    /// </summary>
    public void ClearCanvas()
    {
        FillWithBackgroundColor();
        
        if (debugMode)
            Debug.Log("🧹 Canvas effacé");
    }
    
    /// <summary>
    /// Arrête le dessin (appelé quand on lève le pinceau)
    /// </summary>
    public void StopDrawing()
    {
        StopBrushSound();
    }
    
    /// <summary>
    /// Sauvegarde la texture dans un fichier PNG
    /// </summary>
    public void SaveTexture(string fileName = "MonDessin")
    {
        if (drawTexture == null) return;
        
        byte[] bytes = drawTexture.EncodeToPNG();
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = System.IO.Path.Combine(
            Application.persistentDataPath, 
            $"{fileName}_{timestamp}.png"
        );
        
        System.IO.File.WriteAllBytes(path, bytes);
        
        Debug.Log($"💾 Texture sauvegardée: {path}");
        
        // Ouvrir le dossier (éditeur seulement)
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(path);
        #endif
    }
    
    /// <summary>
    /// Charge une texture depuis un fichier
    /// </summary>
    public void LoadTexture(Texture2D texture)
    {
        if (texture == null) return;
        
        // Redimensionner si nécessaire
        if (texture.width != textureWidth || texture.height != textureHeight)
        {
            Texture2D resized = ResizeTexture(texture, textureWidth, textureHeight);
            drawTexture.SetPixels(resized.GetPixels());
        }
        else
        {
            drawTexture.SetPixels(texture.GetPixels());
        }
        
        drawTexture.Apply();
        
        if (debugMode)
            Debug.Log($"📂 Texture chargée: {texture.width}x{texture.height}");
    }
    
    #endregion
    
    #region MÉTHODES AUDIO
    
    /// <summary>
    /// Démarre le son du pinceau
    /// </summary>
    private void StartBrushSound()
    {
        if (brushSound != null && !brushSound.isPlaying)
        {
            brushSound.loop = true;
            brushSound.Play();
        }
    }
    
    /// <summary>
    /// Arrête le son du pinceau
    /// </summary>
    private void StopBrushSound()
    {
        if (isDrawing)
        {
            isDrawing = false;
            
            if (brushSound != null && brushSound.isPlaying)
            {
                brushSound.loop = false;
                brushSound.Stop();
            }
        }
    }
    
    #endregion
    
    #region MÉTHODES UTILITAIRES
    
    /// <summary>
    /// Redimensionne une texture
    /// </summary>
    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height);
        RenderTexture.active = rt;
        
        Graphics.Blit(source, rt);
        
        Texture2D result = new Texture2D(width, height);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();
        
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        
        return result;
    }
    
    /// <summary>
    /// Dessine un cercle de test au centre
    /// </summary>
    [ContextMenu("Tester le dessin au centre")]
    public void TestDrawCenter()
    {
        DrawAt(new Vector2(0.5f, 0.5f), Color.red, 50, 1f);
        Debug.Log("🔴 Cercle de test dessiné au centre");
    }
    
    /// <summary>
    /// Dessine un motif de test
    /// </summary>
    [ContextMenu("Tester motif")]
    public void TestPattern()
    {
        ClearCanvas();
        
        // Carré rouge
        DrawAt(new Vector2(0.25f, 0.5f), Color.red, 40, 2f);
        // Cercle vert
        DrawAt(new Vector2(0.5f, 0.5f), Color.green, 40, 0.5f);
        // Triangle bleu
        DrawAt(new Vector2(0.75f, 0.5f), Color.blue, 40, 1f);
        
        Debug.Log("🌈 Motif de test dessiné");
    }
    
    #endregion
}