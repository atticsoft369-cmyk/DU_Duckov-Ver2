using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        if (CompareTag("item_rifle"))
        {
            Debug.Log("2번 선언");
            Debug.Log("Rifle 아이템 획득");

            if (WeaponIconSelector.Instance != null)
            {
                WeaponIconSelector.Instance.UnlockSlot(2);
            }

            Destroy(gameObject);
            return;
        }

        if (CompareTag("item_grenade"))
        {
            Debug.Log("4번 선언");
            Debug.Log("Grenade 아이템 획득");

            if (WeaponIconSelector.Instance != null)
            {
                WeaponIconSelector.Instance.UnlockSlot(4);
            }

            Destroy(gameObject);
            return;
        }

        if (CompareTag("item_heal"))
        {
            Debug.Log("5번 선언");
            Debug.Log("Heal 아이템 획득");

            if (WeaponIconSelector.Instance != null)
            {
                WeaponIconSelector.Instance.UnlockSlot(5);
            }

            Destroy(gameObject);
            return;
        }
    }
}