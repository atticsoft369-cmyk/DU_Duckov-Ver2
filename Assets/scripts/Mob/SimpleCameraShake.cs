using System.Collections;
using UnityEngine;

public class SimpleCameraShake : MonoBehaviour
{
    private Coroutine shakeCoroutine;
    private Vector3 currentOffset;

    public void Shake(float duration, float strength)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.localPosition -= currentOffset;
            currentOffset = Vector3.zero;
        }

        shakeCoroutine = StartCoroutine(
            ShakeRoutine(duration, strength)
        );
    }

    private IEnumerator ShakeRoutine(
        float duration,
        float strength
    )
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            transform.localPosition -= currentOffset;

            float fade = 1f - elapsed / duration;

            currentOffset =
                Random.insideUnitSphere *
                strength *
                fade;

            transform.localPosition += currentOffset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition -= currentOffset;
        currentOffset = Vector3.zero;
        shakeCoroutine = null;
    }

    private void OnDisable()
    {
        transform.localPosition -= currentOffset;
        currentOffset = Vector3.zero;
    }
}
