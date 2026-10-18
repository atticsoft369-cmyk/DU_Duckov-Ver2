using System.Collections.Generic;
using UnityEngine;

public class GrenadeExplosionDamage : MonoBehaviour
{
    private int damage;
    private float lifeTime;

    // 이 폭발 1회에서 이미 피해를 받은 대상(일반 몬스터 / 보스).
    // 즉시 범위 판정과 트리거 판정이 같은 대상을 두 번 때리지 않도록 공유합니다.
    private readonly HashSet<Component> damagedTargets = new HashSet<Component>();

    public void Initialize(int damageAmount, float duration)
    {
        damage = damageAmount;
        lifeTime = duration;

        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 폭발 순간 범위 안의 대상에게 즉시 피해를 줍니다.
    /// 이후 트리거로 들어오는 대상은 아직 맞지 않은 경우에만 피해를 받습니다.
    /// </summary>
    public void DamageInstant(Vector3 center, float radius)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, radius);

        foreach (Collider hitCollider in hitColliders)
        {
            TryDamage(hitCollider);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryDamage(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryDamage(other);
    }

    void TryDamage(Collider other)
    {
        if (other == null || damage <= 0)
        {
            return;
        }

        // 일반 몬스터
        MonsterChaser monster = other.GetComponentInParent<MonsterChaser>();

        if (monster != null)
        {
            if (damagedTargets.Add(monster))
            {
                monster.TakeDamage(damage);
            }

            return;
        }

        // 보스 몬스터
        BossMonsterChaser boss = other.GetComponentInParent<BossMonsterChaser>();

        if (boss != null)
        {
            if (damagedTargets.Add(boss))
            {
                boss.TakeDamage(damage);
            }
        }
    }
}
