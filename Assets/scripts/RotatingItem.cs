using UnityEngine;

public class RotatingItem : MonoBehaviour
{
    [Header("Rotate Settings")]
    public float rotateSpeed = 60f;

    void Update()
    {
        transform.Rotate(
            0f,
            rotateSpeed * Time.deltaTime,
            0f,
            Space.World
        );
    }
}