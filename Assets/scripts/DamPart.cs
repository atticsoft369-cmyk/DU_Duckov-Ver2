using UnityEngine;

public class DamPart : MonoBehaviour
{
    [Header("Shared HP")]
    public DamHealth sharedHealth;

    [Tooltip("몬스터가 접근/공격 판정에 쓸 콜라이더. 비워두면 이 오브젝트의 콜라이더를 사용합니다.")]
    public Collider targetCollider;

    public void TakeDamage(int damage)
    {
        if (sharedHealth == null)
        {
            Debug.LogWarning("DamPart에 Shared Health가 연결되지 않았습니다: " + name);
            return;
        }

        Debug.Log("DamPart 데미지 전달: " + damage + " / " + name);

        sharedHealth.TakeDamage(damage);
    }
}