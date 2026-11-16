using UnityEngine;

public sealed class PerformanceSettings : MonoBehaviour
{
    [SerializeField, Min(30)] private int targetFPS = 120;

    private void Awake()
    {
        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0;
    }
}
