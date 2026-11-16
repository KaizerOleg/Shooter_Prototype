
using Unity.Netcode;
using UnityEngine;

public sealed class BuildingPlacementPreview : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField]
    private Color validColor = new(0.1f, 1f, 0.2f, 0.45f);

    [SerializeField]
    private Color invalidColor = new(1f, 0.1f, 0.1f, 0.45f);

    private Renderer[] renderers;
    private Collider[] colliders;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    public void Initialize(GameObject sourcePrefab)
    {
        if (sourcePrefab == null)
            return;

        GameObject preview = Instantiate(
            sourcePrefab,
            transform.position,
            transform.rotation,
            transform);

        preview.name = "BuildingPreviewModel";

        NetworkObject networkObject =
            preview.GetComponent<NetworkObject>();

        if (networkObject != null)
            networkObject.enabled = false;

        BuildableStructure structure =
            preview.GetComponent<BuildableStructure>();

        if (structure != null)
            structure.enabled = false;

        colliders = preview.GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            if (col != null)
                col.enabled = false;
        }

        renderers = preview.GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();

        SetValid(false);
    }

    public void SetPosition(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    public void SetValid(bool valid)
    {
        if (renderers == null)
            return;

        Color color = valid ? validColor : invalidColor;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);

            Material material = renderer.sharedMaterial;

            if (material != null)
            {
                if (material.HasProperty(BaseColorId))
                    propertyBlock.SetColor(BaseColorId, color);
                else if (material.HasProperty(ColorId))
                    propertyBlock.SetColor(ColorId, color);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}
