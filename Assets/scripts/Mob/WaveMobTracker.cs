using UnityEngine;

public class WaveMobTracker : MonoBehaviour
{
    private MobTableSpawner owner;
    private int ownerWaveIndex = -1;

    private bool hasInitialized;
    private bool hasNotifiedDeath;

    public void Initialize(
        MobTableSpawner spawner,
        int waveIndex)
    {
        owner = spawner;
        ownerWaveIndex = waveIndex;

        hasInitialized = true;
        hasNotifiedDeath = false;
    }

    public void NotifyKilled()
    {
        if (!hasInitialized)
        {
            Debug.LogWarning(
                name + ": WaveMobTracker가 초기화되지 않았습니다.",
                this
            );

            return;
        }

        if (hasNotifiedDeath)
        {
            return;
        }

        hasNotifiedDeath = true;

        if (owner == null)
        {
            Debug.LogWarning(
                name + ": MobTableSpawner 참조가 없습니다.",
                this
            );

            return;
        }

        owner.NotifyMonsterDestroyed(
            ownerWaveIndex
        );
    }

    private void OnDestroy()
    {
        /*
         * OnDestroy에서는 몬스터 사망을 알리지 않습니다.
         * 실제 HP가 0이 되어 Die()가 호출될 때만
         * NotifyKilled()를 실행합니다.
         */
        owner = null;
    }
}