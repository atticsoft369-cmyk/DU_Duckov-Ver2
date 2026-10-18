using UnityEngine;

public class TopDownBullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 25f;
    public float lifeTime = 3f;

    [Tooltip("이 총알 1발의 공격력. 발사할 때 Pistol/Rifle 값으로 덮어씁니다.")]
    public int damage = 1;

    [Header("Collision Settings")]
    [Tooltip("총알이 충돌을 검사할 레이어입니다. Mob을 반드시 포함하세요.")]
    public LayerMask collisionMask = ~0;

    [Tooltip("얇거나 빠르게 움직이는 총알의 충돌 누락을 방지합니다.")]
    public float sphereCastRadius = 0.08f;

    [Header("Y Position Correction")]
    public float yOffset = 0f;
    public bool keepFixedY = true;

    private Vector3 moveDirection;
    private float fixedY;
    private bool hasHit;

    void Start()
    {
        Vector3 correctedPosition = transform.position;
        correctedPosition.y += yOffset;
        transform.position = correctedPosition;

        fixedY = transform.position.y;

        Destroy(gameObject, lifeTime);
    }

    public void SetDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }

        moveDirection = direction.normalized;
    }

    public void SetDamage(int value)
    {
        damage = Mathf.Max(0, value);
    }

    /// <summary>
    /// 이 총알의 명중 처리 권한을 한 번만 가져갑니다.
    /// 총알 스크립트와 몬스터 스크립트가 같은 충돌을 동시에 처리해
    /// 피해가 두 번 들어가는 것을 막기 위해 사용합니다.
    /// 이미 명중 처리된 총알이면 false를 반환합니다.
    /// </summary>
    public bool TryConsumeHit()
    {
        if (hasHit)
        {
            return false;
        }

        hasHit = true;
        return true;
    }

    void Update()
    {
        if (hasHit)
        {
            return;
        }

        if (moveDirection.sqrMagnitude <= 0.01f)
        {
            moveDirection = transform.forward;
            moveDirection.y = 0f;
            moveDirection.Normalize();
        }

        Vector3 currentPosition = transform.position;

        Vector3 nextPosition =
            currentPosition +
            moveDirection * speed * Time.deltaTime;

        if (keepFixedY)
        {
            nextPosition.y = fixedY;
        }

        Vector3 movement = nextPosition - currentPosition;
        float moveDistance = movement.magnitude;

        if (moveDistance <= 0f)
        {
            return;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            currentPosition,
            sphereCastRadius,
            movement.normalized,
            moveDistance,
            collisionMask,
            QueryTriggerInteraction.Collide
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            if (ShouldIgnoreCollider(hit.collider))
            {
                continue;
            }

            if (TryHitMonster(hit.collider))
            {
                return;
            }

            hasHit = true;
            Destroy(gameObject);
            return;
        }

        transform.position = nextPosition;
    }

    bool ShouldIgnoreCollider(Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return true;
        }

        if (targetCollider.transform == transform ||
            targetCollider.transform.IsChildOf(transform))
        {
            return true;
        }

        // 다리 같은 '걸을 수 있는 영역'(트리거)은 총알이 통과합니다.
        if (targetCollider.isTrigger &&
            targetCollider.GetComponentInParent<WalkableSurface>() != null)
        {
            return true;
        }

        TopDownPlayerController playerController =
            targetCollider.GetComponentInParent<TopDownPlayerController>();

        if (playerController != null)
        {
            return true;
        }

        return false;
    }

    bool TryHitMonster(Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        // 이미 다른 경로(몬스터 쪽 충돌 처리 등)에서 명중 처리된 총알
        if (hasHit)
        {
            return true;
        }

        // 일반 몬스터
        MonsterChaser monster =
            targetCollider.GetComponent<MonsterChaser>();

        if (monster == null)
        {
            monster =
                targetCollider.GetComponentInParent<MonsterChaser>();
        }

        if (monster == null)
        {
            monster =
                targetCollider.GetComponentInChildren<MonsterChaser>();
        }

        if (monster != null)
        {
            hasHit = true;

            Debug.Log(
                "총알 명중: " +
                targetCollider.name +
                " / 몬스터: " +
                monster.name +
                " / Damage: " +
                damage
            );

            monster.TakeDamage(damage);

            Destroy(gameObject);

            return true;
        }

        // 보스 몬스터
        BossMonsterChaser boss =
            targetCollider.GetComponent<BossMonsterChaser>();

        if (boss == null)
        {
            boss =
                targetCollider.GetComponentInParent<BossMonsterChaser>();
        }

        if (boss == null)
        {
            boss =
                targetCollider.GetComponentInChildren<BossMonsterChaser>();
        }

        if (boss != null)
        {
            hasHit = true;

            Debug.Log(
                "총알 명중: " +
                targetCollider.name +
                " / 보스: " +
                boss.name +
                " / Damage: " +
                damage
            );

            boss.TakeDamage(damage);

            Destroy(gameObject);

            return true;
        }

        return false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasHit || ShouldIgnoreCollider(other))
        {
            return;
        }

        if (TryHitMonster(other))
        {
            return;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasHit || collision == null)
        {
            return;
        }

        Collider other = collision.collider;

        if (ShouldIgnoreCollider(other))
        {
            return;
        }

        if (TryHitMonster(other))
        {
            return;
        }

        hasHit = true;
        Destroy(gameObject);
    }
}
