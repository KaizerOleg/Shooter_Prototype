using UnityEngine;

public sealed class ChainLightningVisual : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField]
    private Color lightningColor =
        new Color(0.2f, 0.65f, 1f, 1f);

    [SerializeField, Min(0.01f)]
    private float lineWidth = 0.12f;

    [SerializeField, Min(0.01f)]
    private float duration = 0.2f;

    private float elapsed;
    private LineRenderer[] lines;
    private Material material;

    public void Initialize(Vector3[] points)
    {
        if (points == null || points.Length < 2)
        {
            Destroy(gameObject);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            Destroy(gameObject);
            return;
        }

        material = new Material(shader);

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", lightningColor);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", lightningColor);

        lines = new LineRenderer[points.Length - 1];

        for (int i = 0; i < lines.Length; i++)
        {
            GameObject lineObject = new GameObject(
                $"LightningSegment_{i}");

            lineObject.transform.SetParent(transform);

            LineRenderer line =
                lineObject.AddComponent<LineRenderer>();

            line.positionCount = 2;
            line.useWorldSpace = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth * 0.6f;
            line.material = material;
            line.startColor = lightningColor;
            line.endColor = lightningColor;

            line.numCapVertices = 4;
            line.numCornerVertices = 4;

            line.SetPosition(0, points[i]);
            line.SetPosition(1, points[i + 1]);

            lines[i] = line;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float alpha = Mathf.Lerp(
            1f,
            0f,
            elapsed / duration);

        Color currentColor = lightningColor;
        currentColor.a = alpha;

        if (lines != null)
        {
            foreach (LineRenderer line in lines)
            {
                if (line == null)
                    continue;

                line.startColor = currentColor;
                line.endColor = currentColor;
            }
        }

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (material != null)
            Destroy(material);
    }
}