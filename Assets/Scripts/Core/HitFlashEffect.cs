using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HitFlashEffect : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform modelRoot;

    [Header("Appearance")]
    [SerializeField] private Color hitColor = Color.red;

    [SerializeField, Range(0.05f, 1f)]
    private float flashDuration = 0.2f;

    [SerializeField, Range(0f, 1f)]
    private float flashIntensity = 1f;

    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Coroutine activeFlash;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private void Awake()
    {
        block = new MaterialPropertyBlock();

        if (modelRoot == null)
            modelRoot = transform;

        CacheRenderers();
    }

    private void CacheRenderers()
    {
        if (modelRoot == null)
            modelRoot = transform;

        renderers = modelRoot.GetComponentsInChildren<Renderer>(true);
    }

    public void Play()
    {
        if (renderers == null || renderers.Length == 0)
            CacheRenderers();

        if (renderers == null || renderers.Length == 0)
            return;

        if (activeFlash != null)
            StopCoroutine(activeFlash);

        activeFlash = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float elapsed = 0f;

        ApplyFlash(hitColor * flashIntensity);

        yield return new WaitForSeconds(flashDuration * 0.25f);

        while (elapsed < flashDuration * 0.75f)
        {
            float progress = elapsed / (flashDuration * 0.75f);

            float intensity = Mathf.Lerp(
                flashIntensity,
                0f,
                progress);

            ApplyFlash(hitColor * intensity);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearFlash();
        activeFlash = null;
    }

    private void ApplyFlash(Color color)
    {
        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            Material[] materials = rend.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];

                if (material == null)
                    continue;

                rend.GetPropertyBlock(block, i);

                bool hasColorProperty = false;

                if (material.HasProperty(BaseColorId))
                {
                    block.SetColor(BaseColorId, color);
                    hasColorProperty = true;
                }

                if (material.HasProperty(ColorId))
                {
                    block.SetColor(ColorId, color);
                    hasColorProperty = true;
                }

                if (hasColorProperty)
                    rend.SetPropertyBlock(block, i);
            }
        }
    }

    private void ClearFlash()
    {
        if (renderers == null)
            return;

        foreach (Renderer rend in renderers)
        {
            if (rend == null)
                continue;

            Material[] materials = rend.sharedMaterials;

            for (int i = 0; i < materials.Length; i++)
                rend.SetPropertyBlock(null, i);
        }
    }

    private void OnDisable()
    {
        if (activeFlash != null)
            StopCoroutine(activeFlash);

        activeFlash = null;
        ClearFlash();
    }
}