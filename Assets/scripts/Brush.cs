using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class Brush : MonoBehaviour
{
    [Header("=== RÉFÉRENCES ===")]
    [SerializeField] private Renderer brushRenderer;    // Partie colorée du pinceau
    [SerializeField] private Transform brushTip;        // Point d'origine du rayon
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    
    [Header("=== PARAMÈTRES RAYCAST ===")]
    [SerializeField] private float rayDistance = 0.3f;
    [SerializeField] private LayerMask boardMask;
    [SerializeField] private bool useBrushTipForward = true;
    [SerializeField] private Vector3 rayDirectionOffset = Vector3.zero;
    [SerializeField] private Vector3 brushTipLocalOffset = Vector3.zero; // Calibration: decalage local de la pointero;
    
    [Header("=== PARAMÈTRES PINCEAU ===")]
    [SerializeField] private Color brushColor = Color.red;
    [SerializeField] private int brushSize = 20;
    [SerializeField] private float brushHardness = 1f;
    [SerializeField] private float brushOpacity = 1f;
    
    [Header("=== EFFETS VISUELS ===")]
    [SerializeField] private ParticleSystem paintParticles;
    [SerializeField] private LineRenderer rayVisualizer;
    [SerializeField] private GameObject brushCursor;
    
    [Header("=== DÉBOGAGE ===")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool showRay = true;
    
    // Variables privées
    private DrawingBoard currentBoard;
    private bool isGrabbed = false;
    private Vector3 lastBrushPosition;
    private Quaternion lastBrushRotation;
    private Vector2 lastPaintUV;
    private bool hasLastPaintUV = false;
    
    #region PROPRIÉTÉS PUBLIQUES
    
    /// <summary>
    /// Couleur actuelle du pinceau
    /// </summary>
    public Color CurrentColor => brushColor;
    
    /// <summary>
    /// Taille actuelle du pinceau
    /// </summary>
    public int CurrentBrushSize => brushSize;
    
    /// <summary>
    /// Le pinceau est-il actuellement attrapé ?
    /// </summary>
    public bool IsGrabbed => isGrabbed;
    
    #endregion
    
    #region MÉTHODES UNITY
    
    void Start()
    {
        InitializeComponents();
        SetupVisuals();
        
        if (debugMode)
            Debug.Log($"🖌️ Pinceau initialisé: {gameObject.name}");
    }
    
    void Update()
    {
        if (!isGrabbed) return;
        
        UpdateBrushVisuals();
        HandlePainting();
    }
    
    #endregion
    
    #region INITIALISATION
    
    /// <summary>
    /// Initialise tous les composants nécessaires
    /// </summary>
    private void InitializeComponents()
    {
        // Trouver le renderer si non assigné
        if (brushRenderer == null)
            brushRenderer = GetComponentInChildren<Renderer>();
        
        // Trouver la pointe si non assignée
        if (brushTip == null)
        {
            brushTip = transform;
            if (brushRenderer != null)
                brushTip = brushRenderer.transform;
        }
        
        // Trouver le composant d'interaction
        if (grabInteractable == null)
            grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        
        // S'abonner aux événements de grab
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
        
        // Initialiser la couleur
        UpdateBrushColor(brushColor);
    }
    
    /// <summary>
    /// Configure les effets visuels
    /// </summary>
    private void SetupVisuals()
    {
        // Créer un visualiseur de rayon si demandé
        if (showRay && rayVisualizer == null)
        {
            GameObject rayObj = new GameObject("RayVisualizer");
            rayObj.transform.parent = transform;
            rayVisualizer = rayObj.AddComponent<LineRenderer>();
            rayVisualizer.startWidth = 0.002f;
            rayVisualizer.endWidth = 0.002f;
            rayVisualizer.material = new Material(Shader.Find("Sprites/Default"));
            rayVisualizer.startColor = Color.cyan;
            rayVisualizer.endColor = new Color(0, 1, 1, 0.3f);
        }
        
        // Créer un curseur si demandé
        if (brushCursor == null && debugMode)
        {
            brushCursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            brushCursor.name = "BrushCursor";
            brushCursor.transform.localScale = Vector3.one * 0.02f;
            brushCursor.GetComponent<Renderer>().material.color = Color.yellow;
            Destroy(brushCursor.GetComponent<Collider>());
        }
    }
    
    #endregion
    
    #region GESTION DU DESSIN
    
    /// <summary>
    /// Gère la logique de peinture
    /// </summary>
    private void HandlePainting()
    {
        Vector3 rayOrigin = GetRayOrigin();
        Vector3 rayDirection = GetRayDirection();
        
        // Visualisation du rayon
        VisualizeRay(rayOrigin, rayDirection);
        
        // Raycast
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, boardMask))
        {
            // Mettre à jour le curseur visuel
            UpdateCursor(hit.point, hit.normal);
            
            HSVColorWheel colorWheel = hit.collider.GetComponent<HSVColorWheel>();
            if (colorWheel != null)
            {
                Color picked = colorWheel.PickColorAt(hit.textureCoord);
                SetColor(picked);

                if (debugMode)
                    Debug.Log($"🎨 Couleur choisie sur la palette: {picked}");
            }

            // Vérifier si on a touché un DrawingBoard
            DrawingBoard board = hit.collider.GetComponent<DrawingBoard>();
            if (board != null)
            {
                currentBoard = board;
                
                // Dessiner à la position UV
                Vector2 uv = hit.textureCoord;

                if (hasLastPaintUV)
                {
                    board.DrawLine(lastPaintUV, uv, brushColor, brushSize, brushHardness, brushOpacity);
                }
                else
                {
                    board.DrawAt(uv, brushColor, brushSize, brushHardness, brushOpacity);
                }

                lastPaintUV = uv;
                hasLastPaintUV = true;
                
                // Effets de particules
                if (paintParticles != null && !paintParticles.isPlaying)
                    paintParticles.Play();
                
                if (debugMode)
                    Debug.Log($"🎨 Dessin à UV: {uv} | Distance: {hit.distance:F2}");
            }
        }
        else
        {
            // Pas de contact avec le tableau
            if (currentBoard != null)
            {
                currentBoard.StopDrawing();
                currentBoard = null;
                hasLastPaintUV = false;
            }
            
            // Cacher le curseur
            if (brushCursor != null)
                brushCursor.SetActive(false);
            
            // Arrêter les particules
            if (paintParticles != null && paintParticles.isPlaying)
                paintParticles.Stop();
        }
    }
    
    /// <summary>
    /// Obtient l'origine du rayon
    /// </summary>
    private Vector3 GetRayOrigin()
    {
        return brushTip.TransformPoint(brushTipLocalOffset);
    }
    
    /// <summary>
    /// Obtient la direction du rayon
    /// </summary>
    private Vector3 GetRayDirection()
    {
        Vector3 direction = useBrushTipForward ? brushTip.forward : -brushTip.up;
        direction += rayDirectionOffset;
        return direction.normalized;
    }
    
    /// <summary>
    /// Visualise le rayon de détection
    /// </summary>
    private void VisualizeRay(Vector3 origin, Vector3 direction)
    {
        // Debug Draw
        if (showRay)
            Debug.DrawRay(origin, direction * rayDistance, Color.green, 0.1f);
        
        // LineRenderer
        if (rayVisualizer != null)
        {
            rayVisualizer.positionCount = 2;
            rayVisualizer.SetPosition(0, origin);
            rayVisualizer.SetPosition(1, origin + direction * rayDistance);
            
            // Changer la couleur selon la distance
            float alpha = Mathf.Clamp01(1 - (origin - brushTip.position).magnitude / rayDistance);
            rayVisualizer.startColor = new Color(0, 1, 1, alpha);
        }
    }
    
    /// <summary>
    /// Met à jour le curseur visuel
    /// </summary>
    private void UpdateCursor(Vector3 position, Vector3 normal)
    {
        if (brushCursor == null) return;
        
        brushCursor.SetActive(true);
        brushCursor.transform.position = position + normal * 0.01f;
        brushCursor.transform.rotation = Quaternion.LookRotation(normal);
        
        // Ajuster la taille selon la distance
        float distance = Vector3.Distance(GetRayOrigin(), position);
        float scale = Mathf.Lerp(0.01f, 0.05f, distance / rayDistance);
        brushCursor.transform.localScale = Vector3.one * scale;
        
        // Changer la couleur selon l'angle
        float angle = Vector3.Angle(normal, -GetRayDirection());
        brushCursor.GetComponent<Renderer>().material.color = 
            angle < 30f ? Color.green : Color.yellow;
    }
    
    #endregion
    
    #region MÉTHODES D'INTERACTION
    
    /// <summary>
    /// Appelé quand le pinceau est attrapé
    /// </summary>
    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        lastBrushPosition = transform.position;
        lastBrushRotation = transform.rotation;
        
        if (debugMode)
            Debug.Log("🤚 Pinceau attrapé");
    }
    
    /// <summary>
    /// Appelé quand le pinceau est relâché
    /// </summary>
    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        
        // Arrêter le dessin sur le tableau
        if (currentBoard != null)
        {
            currentBoard.StopDrawing();
            currentBoard = null;
            hasLastPaintUV = false;
        }
        
        // Arrêter les effets
        if (paintParticles != null)
            paintParticles.Stop();
        
        // Cacher le curseur
        if (brushCursor != null)
            brushCursor.SetActive(false);
        
        if (debugMode)
            Debug.Log("🖐️ Pinceau relâché");
    }
    
    #endregion
    
    #region MÉTHODES PUBLIQUES
    
    /// <summary>
    /// Change la couleur du pinceau
    /// </summary>
    public void SetColor(Color newColor)
    {
        brushColor = newColor;
        UpdateBrushColor(newColor);
        
        if (debugMode)
            Debug.Log($"🟣 Couleur changée: {newColor}");
    }
    
    /// <summary>
    /// Change la taille du pinceau
    /// </summary>
    public void SetBrushSize(int newSize)
    {
        brushSize = Mathf.Clamp(newSize, 1, 100);
        
        if (debugMode)
            Debug.Log($"📏 Taille changée: {brushSize}px");
    }
    
    /// <summary>
    /// Change la dureté du pinceau
    /// </summary>
    public void SetBrushHardness(float hardness)
    {
        brushHardness = Mathf.Clamp(hardness, 0.1f, 2f);
    }
    
    /// <summary>
    /// Change l'opacité du pinceau
    /// </summary>
    public void SetBrushOpacity(float opacity)
    {
        brushOpacity = Mathf.Clamp01(opacity);
    }
    
    /// <summary>
    /// Met à jour la couleur visuelle du pinceau
    /// </summary>
    private void UpdateBrushColor(Color color)
    {
        if (brushRenderer != null)
        {
            // Créer un nouveau matériel pour éviter les partages
            Material brushMaterial = new Material(brushRenderer.material)
            {
                color = color
            };
            
            // Émission pour un effet lumineux
            brushMaterial.SetColor("_EmissionColor", color * 0.3f);
            brushRenderer.material = brushMaterial;
        }
    }
    
    /// <summary>
    /// Met à jour les effets visuels du pinceau
    /// </summary>
    private void UpdateBrushVisuals()
    {
        // Mettre à jour la couleur des particules
        if (paintParticles != null)
        {
            var main = paintParticles.main;
            main.startColor = brushColor;
        }
    }
    
    #endregion
    
    #region MÉTHODES DÉBOGAGE
    
    /// <summary>
    /// Affiche les informations de débogage
    /// </summary>
    [ContextMenu("Afficher infos débogage")]
    public void PrintDebugInfo()
    {
        Debug.Log($"=== INFOS PINCEAU ===");
        Debug.Log($"Nom: {gameObject.name}");
        Debug.Log($"Attrapé: {isGrabbed}");
        Debug.Log($"Couleur: {brushColor}");
        Debug.Log($"Taille: {brushSize}px");
        Debug.Log($"Distance rayon: {rayDistance}");
        Debug.Log($"Direction: {GetRayDirection()}");
        Debug.Log($"Tableau actuel: {currentBoard?.gameObject.name ?? "Aucun"}");
    }
    
    /// <summary>
    /// Teste le pinceau sur le tableau actuel
    /// </summary>
    [ContextMenu("Tester dessin")]
    public void TestDraw()
    {
        if (currentBoard != null)
        {
            currentBoard.DrawAt(new Vector2(0.5f, 0.5f), brushColor, brushSize, brushHardness);
            Debug.Log("🧪 Test de dessin effectué");
        }
        else
        {
            Debug.LogWarning("⚠️ Aucun tableau détecté pour tester");
        }
    }
    
    #endregion
    
    #region SUPPORT ÉDITEUR (sans VR)
    
    #if UNITY_EDITOR
    
    private Vector3? mouseDragStart = null;
    
    void OnMouseDown()
    {
        if (!Application.isPlaying) return;
        
        isGrabbed = true;
        mouseDragStart = Input.mousePosition;
        
        if (debugMode)
            Debug.Log("🖱️ Pinceau attrapé (souris)");
    }
    
    void OnMouseDrag()
    {
        if (!Application.isPlaying || !isGrabbed) return;
        
        // Faire suivre la souris
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            transform.position = hit.point + Vector3.up * 0.1f;
            transform.rotation = Quaternion.LookRotation(-hit.normal);
        }
    }
    
    void OnMouseUp()
    {
        if (!Application.isPlaying) return;
        
        isGrabbed = false;
        
        if (currentBoard != null)
        {
            currentBoard.StopDrawing();
            currentBoard = null;
        }
        
        if (debugMode)
            Debug.Log("🖱️ Pinceau relâché (souris)");
    }
    
    #endif
    
    #endregion
}