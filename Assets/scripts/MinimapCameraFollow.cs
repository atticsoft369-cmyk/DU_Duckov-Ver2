using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Height")]
    public float height = 50f;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = new Vector3(
            target.position.x,
            height,
            target.position.z
        );

        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}