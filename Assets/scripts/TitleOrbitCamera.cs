using UnityEngine;

public class TitleOrbitCamera : MonoBehaviour
{
    [Header("Orbit Target")]
    public Transform pivot;

    [Header("Orbit Settings")]
    public float orbitSpeed = 3f;
    public float distance = 40f;
    public float height = 20f;
    public float lookAtHeight = 3f;

    [Header("Angle Settings")]
    public bool useCustomStartAngle = true;
    public float startAngle = 0f;

    [Header("State")]
    public bool orbitEnabled = true;

    private float angle;

    void Start()
    {
        if (pivot == null)
        {
            Debug.LogWarning("TitleOrbitCamera: pivot이 지정되지 않았습니다.");
            enabled = false;
            return;
        }

        if (useCustomStartAngle)
        {
            angle = startAngle;
        }
        else
        {
            Vector3 offset = transform.position - pivot.position;
            angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        UpdateCameraPosition();
    }

    void LateUpdate()
    {
        if (!orbitEnabled || pivot == null)
        {
            return;
        }

        angle += orbitSpeed * Time.unscaledDeltaTime;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        float rad = angle * Mathf.Deg2Rad;

        Vector3 cameraPos = pivot.position + new Vector3(
            Mathf.Sin(rad) * distance,
            height,
            Mathf.Cos(rad) * distance
        );

        transform.position = cameraPos;

        Vector3 lookTarget = pivot.position + Vector3.up * lookAtHeight;
        transform.LookAt(lookTarget);
    }

    public void SetOrbit(bool enabled)
    {
        orbitEnabled = enabled;
    }
}