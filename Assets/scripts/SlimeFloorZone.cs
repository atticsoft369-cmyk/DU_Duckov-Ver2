using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 슬라임 건이 만든 바닥 장판.
/// 타원 영역(가로 size.x, 세로 size.y) 안에 있는 몬스터에게 슬로우를 계속 걸어줍니다.
/// 콜라이더/레이어 설정에 의존하지 않도록 범위 검사로 판정합니다.
/// </summary>
public class SlimeFloorZone : MonoBehaviour
{
    [Header("Zone")]
    public Vector2 size = new Vector2(5f, 2.6f);

    [Range(0f, 1f)]
    public float slowMultiplier = 0.4f;

    public float slowLingerDuration = 0.3f;

    [Header("Life")]
    public float lifetime = 5f;
    public float fadeDuration = 1f;

    [Tooltip("생성 시 커지는 연출 시간")]
    public float spawnGrowDuration = 0.15f;

    [Tooltip("몬스터 검사 간격")]
    public float checkInterval = 0.1f;

    private float elapsed;
    private float checkTimer;
    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;
    private Vector3 originalScale;
    private bool initialized;

    private static readonly Collider[] hitBuffer = new Collider[128];
    private readonly HashSet<Object> slowedThisCheck = new HashSet<Object>();

    public void Initialize(
        Vector2 zoneSize,
        float multiplier,
        float linger,
        float life,
        float fade)
    {
        size = zoneSize;
        slowMultiplier = multiplier;
        slowLingerDuration = linger;
        lifetime = life;
        fadeDuration = fade;

        Setup();
    }

    void Start()
    {
        Setup();
    }

    void Setup()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;
        elapsed = 0f;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i].color;
        }

        originalScale = transform.localScale;

        if (spawnGrowDuration > 0f)
        {
            transform.localScale = originalScale * 0.1f;
        }
    }

    void Update()
    {
        elapsed += Time.deltaTime;

        UpdateGrow();
        UpdateFade();

        checkTimer -= Time.deltaTime;

        if (checkTimer <= 0f)
        {
            checkTimer = Mathf.Max(0.02f, checkInterval);
            ApplySlowToMonstersInside();
        }

        if (elapsed >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    void UpdateGrow()
    {
        if (spawnGrowDuration <= 0f)
        {
            return;
        }

        float t = Mathf.Clamp01(elapsed / spawnGrowDuration);
        float eased = 1f - (1f - t) * (1f - t);

        transform.localScale = Vector3.Lerp(originalScale * 0.1f, originalScale, eased);
    }

    void UpdateFade()
    {
        if (fadeDuration <= 0f || spriteRenderers == null)
        {
            return;
        }

        float fadeStart = lifetime - fadeDuration;

        if (elapsed < fadeStart)
        {
            return;
        }

        float alpha = 1f - Mathf.Clamp01((elapsed - fadeStart) / fadeDuration);

        // 3D 모델 장판(스프라이트 없음)은 크기를 줄이며 사라집니다.
        if (spriteRenderers.Length == 0)
        {
            transform.localScale = originalScale * Mathf.Max(0.01f, alpha);
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color c = originalColors[i];
            c.a *= alpha;
            spriteRenderers[i].color = c;
        }
    }

    void ApplySlowToMonstersInside()
    {
        float radius = Mathf.Max(size.x, size.y) * 0.5f + 1f;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position,
            radius,
            hitBuffer,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide
        );

        slowedThisCheck.Clear();

        // 슬로우가 다음 검사 전까지 끊기지 않도록 검사 간격만큼 여유를 줍니다.
        float duration = Mathf.Max(slowLingerDuration, checkInterval + 0.05f);

        for (int i = 0; i < count; i++)
        {
            Collider col = hitBuffer[i];

            if (col == null)
            {
                continue;
            }

            MonsterChaser monster = col.GetComponentInParent<MonsterChaser>();

            if (monster != null)
            {
                if (slowedThisCheck.Add(monster) &&
                    IsInsideZone(monster.transform.position))
                {
                    monster.ApplySlow(slowMultiplier, duration);
                }

                continue;
            }

            BossMonsterChaser boss = col.GetComponentInParent<BossMonsterChaser>();

            if (boss != null &&
                slowedThisCheck.Add(boss) &&
                IsInsideZone(boss.transform.position))
            {
                boss.ApplySlow(slowMultiplier, duration);
            }
        }
    }

    bool IsInsideZone(Vector3 worldPosition)
    {
        // 장판의 로컬 X/Z 기준 타원 판정
        Vector3 local = Quaternion.Inverse(transform.rotation) * (worldPosition - transform.position);

        float halfX = Mathf.Max(0.01f, size.x * 0.5f);
        float halfZ = Mathf.Max(0.01f, size.y * 0.5f);

        float nx = local.x / halfX;
        float nz = local.z / halfZ;

        return nx * nx + nz * nz <= 1f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, new Vector3(size.x, 0.01f, size.y));
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);
    }
}
