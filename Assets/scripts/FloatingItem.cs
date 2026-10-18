using UnityEngine;

public class FloatingItem : MonoBehaviour
{
    [Header("Floating Settings")]
    public float floatHeight = 0.3f;   // 위아래 이동 높이
    public float floatSpeed = 1.5f;    // 움직이는 속도

    private Vector3 startPosition;

    void Start()
    {
        // 처음 배치된 위치를 기준점으로 저장
        startPosition = transform.position;
    }

    void Update()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;

        transform.position = new Vector3(
            startPosition.x,
            newY,
            startPosition.z
        );
    }
}