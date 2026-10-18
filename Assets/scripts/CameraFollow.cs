using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 8f, -7f);

    [Header("Look Center Offset")]
    public Vector3 centerOffset = new Vector3(0f, 1.0f, 0f);

    [Header("Fixed Camera Rotation")]
    public Vector3 fixedRotation = new Vector3(60f, 0f, 0f);

    [Header("Follow Settings")]
    public float followSpeed = 8f;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetPosition = target.position + centerOffset + offset;

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            followSpeed * Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(fixedRotation);
    }
}