using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SecurityCamera2D : MonoBehaviour
{
    [Header("Visão")]
    [SerializeField, Min(0.1f)] private float viewDistance = 7f;
    [SerializeField, Range(1f, 179f)] private float viewAngle = 65f;
    [SerializeField, Range(8, 256)] private int rayCount = 100;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private LayerMask playerLayers;

    [Header("Visual do cone")]
    [SerializeField] private Material visionMaterial;
    [SerializeField] private Color visionColor = new Color(1f, 0.85f, 0.2f, 0.22f);
    [SerializeField] private string visionSortingLayer = "Default";
    [SerializeField] private int visionSortingOrder = 5;

    [Header("Varredura da câmera")]
    [Tooltip("Graus para cada lado da direção inicial.")]
    [SerializeField, Range(0f, 180f)] private float sweepAngle = 45f;
    [SerializeField, Min(0.1f)] private float rotationSpeed = 30f;
    [SerializeField, Min(0f)] private float waitAtEnds = 1f;
    [SerializeField] private bool startClockwise = true;

    [Header("Detecção")]
    [SerializeField, Min(0.1f)] private float detectionTime = 2f;
    [SerializeField] private Sprite alertSprite;
    [SerializeField] private Material alertMaterial;
    [SerializeField] private Vector2 alertOffset = new Vector2(0f, 1.5f);
    [SerializeField, Min(0.01f)] private float alertScale = 1f;
    [SerializeField] private int alertSortingOrder = 100;
    [SerializeField] private Color alertScreenColor = new Color(0.85f, 0.05f, 0.05f, 1f);

    [Header("Captura")]
    [SerializeField, Min(0f)] private float restartDelay = 0.4f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.4f;

    private Mesh visionMesh;
    private MeshFilter visionMeshFilter;
    private MeshRenderer visionMeshRenderer;
    private MaterialPropertyBlock visionPropertyBlock;

    private float baseAngle;
    private float sweepOffset;
    private int sweepDirection;
    private bool waitingAtEnd;
    private float waitTimer;

    private float detectionTimer;
    private bool caught;
    private PlayerController2D currentPlayer;
    private GameObject alertIcon;

    private Vector3[] vertices;
    private int[] triangles;

    private void Awake()
    {
        baseAngle = transform.eulerAngles.z;
        sweepDirection = startClockwise ? -1 : 1;

        CreateVisionMesh();
        UpdateVisionMesh();
    }

    private void Update()
    {
        if (caught)
        {
            StealthAlertOverlay.Report(1f, alertScreenColor);
            return;
        }

        SweepCamera();
        UpdateVisionMesh();
        CheckForPlayer();
    }

    private void SweepCamera()
    {
        if (waitingAtEnd)
        {
            waitTimer -= Time.deltaTime;

            if (waitTimer <= 0f)
            {
                waitingAtEnd = false;
                sweepDirection *= -1;
            }

            return;
        }

        float targetOffset = sweepDirection * sweepAngle;
        sweepOffset = Mathf.MoveTowards(
            sweepOffset,
            targetOffset,
            rotationSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(0f, 0f, baseAngle + sweepOffset);

        if (Mathf.Abs(sweepOffset - targetOffset) < 0.01f)
        {
            waitingAtEnd = true;
            waitTimer = waitAtEnds;
        }
    }

    private void CreateVisionMesh()
    {
        GameObject coneObject = new GameObject("DynamicVisionCone");
        coneObject.transform.SetParent(transform, false);

        visionMeshFilter = coneObject.AddComponent<MeshFilter>();
        visionMeshRenderer = coneObject.AddComponent<MeshRenderer>();

        visionMesh = new Mesh();
        visionMesh.name = "SecurityCameraVisionMesh";
        visionMesh.MarkDynamic();
        visionMeshFilter.sharedMesh = visionMesh;

        if (visionMaterial != null)
        {
            visionMeshRenderer.sharedMaterial = visionMaterial;
            visionMeshRenderer.enabled = true;
        }
        else
        {
            visionMeshRenderer.enabled = false;
            Debug.LogWarning(
                "SecurityCamera2D: atribua um Material ao campo Vision Material para renderizar o cone.",
                this
            );
        }

        visionMeshRenderer.sortingLayerName = visionSortingLayer;
        visionMeshRenderer.sortingOrder = visionSortingOrder;

        visionPropertyBlock = new MaterialPropertyBlock();
        visionMeshRenderer.GetPropertyBlock(visionPropertyBlock);
        visionPropertyBlock.SetColor("_Color", visionColor);
        visionPropertyBlock.SetColor("_BaseColor", visionColor);
        visionMeshRenderer.SetPropertyBlock(visionPropertyBlock);

        vertices = new Vector3[rayCount + 2];
        triangles = new int[rayCount * 3];
    }

    private void UpdateVisionMesh()
    {
        if (visionMesh == null)
            return;

        if (vertices.Length != rayCount + 2)
        {
            vertices = new Vector3[rayCount + 2];
            triangles = new int[rayCount * 3];
        }

        vertices[0] = Vector3.zero;

        float halfAngle = viewAngle * 0.5f;

        for (int i = 0; i <= rayCount; i++)
        {
            float angle = -halfAngle + (viewAngle * i / rayCount);

            Vector2 localDirection = RotateVector(Vector2.down, angle);
            Vector2 worldDirection = transform.TransformDirection(localDirection);

            RaycastHit2D hit = Physics2D.Raycast(
                transform.position,
                worldDirection,
                viewDistance,
                obstacleLayers
            );

            Vector3 worldPoint = hit.collider != null
                ? (Vector3)hit.point
                : transform.position + (Vector3)(worldDirection * viewDistance);

            vertices[i + 1] = transform.InverseTransformPoint(worldPoint);
        }

        int triangleIndex = 0;
        for (int i = 0; i < rayCount; i++)
        {
            triangles[triangleIndex++] = 0;
            triangles[triangleIndex++] = i + 1;
            triangles[triangleIndex++] = i + 2;
        }

        visionMesh.Clear();
        visionMesh.vertices = vertices;
        visionMesh.triangles = triangles;
        visionMesh.RecalculateBounds();
    }

    private Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }

    private void CheckForPlayer()
    {
        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(
            transform.position,
            viewDistance,
            playerLayers
        );

        PlayerController2D visiblePlayer = null;

        foreach (Collider2D candidateCollider in nearbyColliders)
        {
            if (candidateCollider == null || !candidateCollider.enabled)
                continue;

            PlayerController2D candidate =
                candidateCollider.GetComponentInParent<PlayerController2D>();

            if (candidate == null)
                continue;

            Vector2 targetPoint = candidateCollider.ClosestPoint(transform.position);
            Vector2 toTarget = targetPoint - (Vector2)transform.position;
            float distance = toTarget.magnitude;

            if (distance <= 0.001f || distance > viewDistance)
                continue;

            Vector2 direction = toTarget / distance;
            Vector2 localDirection = transform.InverseTransformDirection(direction);

            float angleFromDown = Vector2.Angle(Vector2.down, localDirection);
            if (angleFromDown > viewAngle * 0.5f)
                continue;

            RaycastHit2D obstruction = Physics2D.Raycast(
                transform.position,
                direction,
                distance,
                obstacleLayers
            );

            if (obstruction.collider != null && obstruction.distance < distance - 0.03f)
                continue;

            visiblePlayer = candidate;
            break;
        }

        if (visiblePlayer != null)
        {
            currentPlayer = visiblePlayer;
            detectionTimer += Time.deltaTime;
            ShowAlertIcon(true);
            StealthAlertOverlay.Report(
                Mathf.Clamp01(detectionTimer / detectionTime),
                alertScreenColor
            );

            if (detectionTimer >= detectionTime)
                CatchPlayer();
        }
        else
        {
            detectionTimer = 0f;
            currentPlayer = null;
            ShowAlertIcon(false);
        }
    }

    private void ShowAlertIcon(bool show)
    {
        if (alertSprite == null)
            return;

        if (show && alertIcon == null && currentPlayer != null)
        {
            alertIcon = new GameObject("SecurityAlertIcon");
            alertIcon.transform.SetParent(currentPlayer.transform, false);
            alertIcon.transform.localPosition = alertOffset;
            alertIcon.transform.localScale = Vector3.one * alertScale;

            SpriteRenderer spriteRenderer = alertIcon.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = alertSprite;
            spriteRenderer.sortingOrder = alertSortingOrder;

            if (alertMaterial != null)
                spriteRenderer.sharedMaterial = alertMaterial;
        }

        if (alertIcon != null)
            alertIcon.SetActive(show);
    }

    private void CatchPlayer()
    {
        if (caught)
            return;

        caught = true;

        if (currentPlayer != null)
            currentPlayer.SetControlEnabled(false);

        ShowAlertIcon(false);

        SceneTransition.LoadScene(
            SceneManager.GetActiveScene().name,
            restartDelay,
            fadeDuration
        );
    }

    private void OnDisable()
    {
        detectionTimer = 0f;

        if (alertIcon != null)
            alertIcon.SetActive(false);
    }

    private void OnDestroy()
    {
        if (visionMesh != null)
            Destroy(visionMesh);

        if (alertIcon != null)
            Destroy(alertIcon);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        float centreAngle = Application.isPlaying
            ? baseAngle
            : transform.eulerAngles.z;

        for (int side = -1; side <= 1; side += 2)
        {
            Quaternion rotation = Quaternion.Euler(
                0f,
                0f,
                centreAngle + side * sweepAngle
            );

            Gizmos.DrawLine(
                transform.position,
                transform.position + rotation * Vector3.down * viewDistance
            );
        }
    }

    private void OnValidate()
    {
        rayCount = Mathf.Clamp(rayCount, 8, 256);
        viewDistance = Mathf.Max(0.1f, viewDistance);
        detectionTime = Mathf.Max(0.1f, detectionTime);
    }
}

public class StealthAlertOverlay : MonoBehaviour
{
    private static StealthAlertOverlay instance;
    private static float requested;

    private const float RiseSpeed = 0.8f;
    private const float FallSpeed = 1.5f;

    private CanvasGroup canvasGroup;
    private Texture2D gradientTexture;
    private Sprite gradientSprite;
    private float currentAlpha;

    public static void Report(float level, Color color)
    {
        if (level <= 0f)
            return;

        if (instance == null)
        {
            GameObject overlayObject = new GameObject("StealthAlertOverlay");
            instance = overlayObject.AddComponent<StealthAlertOverlay>();
            instance.Build(color);
        }

        requested = Mathf.Max(requested, Mathf.Clamp01(level));
    }

    private void Build(Color color)
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        const int size = 128;
        gradientTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        );

        gradientTexture.wrapMode = TextureWrapMode.Clamp;
        gradientTexture.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                float t = Mathf.InverseLerp(0.35f, 1.15f, distance);
                float edge = t * t * (3f - 2f * t);
                float alpha = Mathf.Lerp(0.10f, 0.90f, edge);

                pixels[y * size + x] = new Color(
                    color.r,
                    color.g,
                    color.b,
                    alpha
                );
            }
        }

        gradientTexture.SetPixels(pixels);
        gradientTexture.Apply();

        gradientSprite = Sprite.Create(
            gradientTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        GameObject imageObject = new GameObject(
            "RedStealthVignette",
            typeof(RectTransform),
            typeof(Image)
        );

        imageObject.transform.SetParent(transform, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = gradientSprite;
        image.raycastTarget = false;
    }

    private void LateUpdate()
    {
        if (canvasGroup == null)
            return;

        float speed = requested > currentAlpha ? RiseSpeed : FallSpeed;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            requested,
            speed * Time.unscaledDeltaTime
        );

        canvasGroup.alpha = currentAlpha;
        requested = 0f;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            requested = 0f;
        }

        if (gradientSprite != null)
            Destroy(gradientSprite);

        if (gradientTexture != null)
            Destroy(gradientTexture);
    }
}