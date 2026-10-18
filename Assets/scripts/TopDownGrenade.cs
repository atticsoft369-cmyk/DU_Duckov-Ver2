using UnityEngine;

public class TopDownGrenade : MonoBehaviour
{
    [Header("Spin Settings")]
    public bool useRandomSpin = true;
    public Vector3 spinAxis = new Vector3(1f, 1f, 0.5f);
    public float spinSpeed = 720f;

    [Header("Explosion Settings")]
    public float explosionEffectDestroyDelay = 2f;

    private GameObject explosionPrefab;
    private float explosionDelay;

    private int explosionDamage;
    private float explosionRadius;
    private float damageColliderDuration;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 currentSpinAxis;

    private float arcHeight;
    private float flightDuration;
    private float timer;

    private bool isFlying;
    private bool hasExploded;

    void Awake()
    {
        SetupSpinAxis();

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        if (!isFlying || hasExploded)
        {
            return;
        }

        timer += Time.deltaTime;

        float t = timer / flightDuration;
        t = Mathf.Clamp01(t);

        Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, t);
        float height = Mathf.Sin(t * Mathf.PI) * arcHeight;

        transform.position = flatPosition + Vector3.up * height;

        transform.Rotate(
            currentSpinAxis,
            spinSpeed * Time.deltaTime,
            Space.Self
        );

        if (t >= 1f)
        {
            Invoke(nameof(Explode), explosionDelay);
            isFlying = false;
        }
    }

    void SetupSpinAxis()
    {
        if (useRandomSpin)
        {
            currentSpinAxis = Random.onUnitSphere;
        }
        else
        {
            currentSpinAxis = spinAxis.normalized;
        }

        if (currentSpinAxis.sqrMagnitude <= 0.01f)
        {
            currentSpinAxis = Vector3.right;
        }
    }

    public void Throw(
        Vector3 target,
        float arc,
        float duration,
        GameObject explosionEffectPrefab,
        float delayBeforeExplosion,
        int damage,
        float radius,
        float colliderDuration
    )
    {
        startPosition = transform.position;
        targetPosition = target;

        targetPosition.y = startPosition.y;

        arcHeight = arc;
        flightDuration = Mathf.Max(0.1f, duration);

        explosionPrefab = explosionEffectPrefab;
        explosionDelay = delayBeforeExplosion;

        explosionDamage = damage;
        explosionRadius = radius;
        damageColliderDuration = colliderDuration;

        timer = 0f;
        hasExploded = false;
        isFlying = true;

        SetupSpinAxis();
    }

    void Explode()
    {
        if (hasExploded)
        {
            return;
        }

        hasExploded = true;

        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(
                explosionPrefab,
                transform.position,
                Quaternion.identity
            );

            Destroy(effect, explosionEffectDestroyDelay);
        }

        CreateDamageCollider();

        Destroy(gameObject);
    }

    void CreateDamageCollider()
    {
        GameObject damageObject = new GameObject("Grenade Damage Collider");
        damageObject.transform.position = transform.position;

        SphereCollider sphereCollider = damageObject.AddComponent<SphereCollider>();
        sphereCollider.isTrigger = true;
        sphereCollider.radius = explosionRadius;

        Rigidbody damageRb = damageObject.AddComponent<Rigidbody>();
        damageRb.useGravity = false;
        damageRb.isKinematic = true;

        GrenadeExplosionDamage damageScript = damageObject.AddComponent<GrenadeExplosionDamage>();
        damageScript.Initialize(explosionDamage, damageColliderDuration);

        // 즉시 범위 피해도 같은 damageScript를 통해 처리해서
        // 트리거 판정과 합쳐 대상당 1회만 피해가 들어가게 합니다. (보스 포함)
        damageScript.DamageInstant(transform.position, explosionRadius);
    }
}