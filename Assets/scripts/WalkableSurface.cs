using UnityEngine;

/// <summary>
/// 플레이어가 위로 올라가 걸을 수 있는 표면(다리 등) 표시용 컴포넌트.
/// 이 오브젝트(또는 자식)의 콜라이더 윗면을 플레이어가 밟고 지나갑니다.
/// 콜라이더는 Trigger로 두어 플레이어를 막지 않게 하고,
/// 떨어지지 않게 막을 난간은 별도 콜라이더(Side Wall)로 둡니다.
/// </summary>
public class WalkableSurface : MonoBehaviour
{
}
