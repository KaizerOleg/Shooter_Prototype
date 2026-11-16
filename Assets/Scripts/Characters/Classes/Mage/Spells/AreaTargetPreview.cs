
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public sealed class AreaTargetPreview : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField]
    private Color previewColor = new Color(0.1f, 0.65f, 1f, 0.25f);

    [SerializeField]
    private Color outlineColor = new Color(0.1f, 0.75f, 1f, 1f);

    [Header("Circle")]
    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.06f;

    [SerializeField, Min(8)]
    private int segments = 64;

    [SerializeField]
    private float verticalOffset = 0.05f;

    private LineRenderer circle;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    private Mesh previewMesh;
    private Material previewMaterial;
    private Material outlineMaterial;

    private float currentRadius;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        CreateMaterials();
        CreateCircle();
        CreateFilledArea();
    }

    private void CreateMaterials()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Debug.LogError(
                "AreaTargetPreview: Не знайдено Shader для індикатора.");
            return;
        }

        previewMaterial = new Material(shader);
        ConfigureTransparentMaterial(previewMaterial, previewColor);

        outlineMaterial = new Material(shader);
        ConfigureTransparentMaterial(outlineMaterial, outlineColor);

        meshRenderer.sharedMaterial = previewMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    private void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private void CreateCircle()
    {
        circle = gameObject.AddComponent<LineRenderer>();

        circle.useWorldSpace = false;
        circle.loop = true;
        circle.positionCount = segments;

        circle.startWidth = lineWidth;
        circle.endWidth = lineWidth;

        circle.startColor = outlineColor;
        circle.endColor = outlineColor;

        circle.material = outlineMaterial;

        circle.shadowCastingMode = ShadowCastingMode.Off;
        circle.receiveShadows = false;

        circle.numCapVertices = 4;
        circle.numCornerVertices = 4;
    }

    private void CreateFilledArea()
    {
        previewMesh = new Mesh
        {
            name = "AreaTargetPreviewMesh"
        };

        meshFilter.sharedMesh = previewMesh;
    }

    public void SetRadius(float radius)
    {
        currentRadius = Mathf.Max(0.1f, radius);

        UpdateFilledArea();
        UpdateCircle();
    }

    private void UpdateFilledArea()
    {
        if (previewMesh == null)
            return;

        Vector3[] vertices = new Vector3[segments + 1];
        Vector2[] uv = new Vector2[segments + 1];
        int[] triangles = new int[segments * 3];

        vertices[0] = Vector3.zero;
        uv[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            float x = Mathf.Cos(angle) * currentRadius;
            float z = Mathf.Sin(angle) * currentRadius;

            vertices[i + 1] = new Vector3(x, 0f, z);

            uv[i + 1] = new Vector2(
                x / (currentRadius * 2f) + 0.5f,
                z / (currentRadius * 2f) + 0.5f);

            int triangleIndex = i * 3;

            triangles[triangleIndex] = 0;
            triangles[triangleIndex + 1] = (i + 1) % segments + 1;
            triangles[triangleIndex + 2] = i + 1;
        }

        previewMesh.Clear();
        previewMesh.vertices = vertices;
        previewMesh.uv = uv;
        previewMesh.triangles = triangles;
        previewMesh.RecalculateNormals();
        previewMesh.RecalculateBounds();
    }

    private void UpdateCircle()
    {
        if (circle == null)
            return;

        Vector3[] points = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;

            points[i] = new Vector3(
                Mathf.Cos(angle) * currentRadius,
                0.01f,
                Mathf.Sin(angle) * currentRadius);
        }

        circle.positionCount = segments;
        circle.SetPositions(points);
    }

    public void SetSurface(Vector3 position, Vector3 normal)
    {
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.up;

        normal.Normalize();

        transform.position = position + normal * verticalOffset;

        // Локальна вісь Y індикатора дивиться вздовж нормалі поверхні.
        transform.rotation = Quaternion.FromToRotation(
            Vector3.up,
            normal);
    }

    // Залишаємо для сумісності зі старими викликами.
    public void SetPosition(Vector3 position)
    {
        SetSurface(position, Vector3.up);
    }

    private void OnDestroy()
    {
        if (previewMesh != null)
            Destroy(previewMesh);

        if (previewMaterial != null)
            Destroy(previewMaterial);

        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }
}
