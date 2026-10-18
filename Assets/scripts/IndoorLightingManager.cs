using UnityEngine;

public class IndoorLightingManager : MonoBehaviour
{
    [Header("기본 어둠 설정")]
    [Range(0f, 1f)]
    public float ambientBrightness = 0.35f;

    public Color ambientColor = Color.gray;

    [Header("Directional Light")]
    public Light directionalLight;
    [Range(0f, 1f)]
    public float directionalLightIntensity = 0.15f;

    void Start()
    {
        ApplyLighting();
    }

    void OnValidate()
    {
        ApplyLighting();
    }

    void ApplyLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor * ambientBrightness;

        if (directionalLight != null)
        {
            directionalLight.intensity = directionalLightIntensity;
        }
    }
}