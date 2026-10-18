using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    [Header("HP Settings")]
    public int maxHp = 10;
    private int currentHp;

    [Header("Respawn Settings")]
    public Transform respawnPoint;
    public float respawnHeightOffset = 0f;

    [Tooltip("사망 애니메이션을 보여준 뒤 부활하기까지의 시간")]
    public float respawnDelay = 1.5f;

    [Header("Respawn Countdown")]
    [Tooltip("사망 후 부활까지 카운트다운(초)")]
    [Min(1)]
    public int respawnCountdownSeconds = 5;

    [Tooltip("카운트다운 표시 텍스트. 비워두면 화면 중앙에 자동 생성합니다.")]
    public TMP_Text respawnCountdownText;

    [Tooltip("카운트다운 문구. {0} = 남은 초")]
    public string respawnCountdownFormat = "RESPAWN {0}";

    [Header("UI")]
    public Image hpFillImage;

    private Rigidbody rb;
    private TopDownPlayerController playerController;

    private bool isDead;

    public bool IsDead => isDead;

    // 게임 시작 시 플레이어 높이. 리스폰 지점의 높이가 달라도 이 높이로 부활합니다.
    private float spawnY;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerController = GetComponent<TopDownPlayerController>();

        if (playerController == null)
        {
            playerController = GetComponentInParent<TopDownPlayerController>();
        }

        // GameControl.csv [PLAYER] player_hp
        maxHp = Mathf.Max(1, GameControl.GetPlayerInt("player_hp", maxHp));

        currentHp = maxHp;
        UpdateHpUI();

        spawnY = transform.position.y;
    }

    public void TakeDamage(int damage)
    {
        // 이미 사망 처리 중이라면 추가 피해를 받지 않음
        if (isDead)
        {
            return;
        }

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        Debug.Log("Player HP: " + currentHp + " / " + maxHp);

        UpdateHpUI();

        if (currentHp <= 0)
        {
            StartDeath();
        }
    }

    void StartDeath()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (playerController != null)
        {
            playerController.PlayDeathAnimation();
        }

        StartCoroutine(RespawnAfterDelay());
    }

    IEnumerator RespawnAfterDelay()
    {
        // 5, 4, 3, 2, 1 카운트 후 부활 (일시정지 중에는 카운트도 멈춤)
        ShowCountdown(true);

        for (int remain = Mathf.Max(1, respawnCountdownSeconds); remain > 0; remain--)
        {
            SetCountdownText(remain);
            yield return new WaitForSeconds(1f);
        }

        ShowCountdown(false);

        Respawn();
    }

    void ShowCountdown(bool show)
    {
        if (respawnCountdownText == null && show)
        {
            CreateCountdownText();
        }

        if (respawnCountdownText != null)
        {
            respawnCountdownText.gameObject.SetActive(show);
        }
    }

    void SetCountdownText(int seconds)
    {
        if (respawnCountdownText != null)
        {
            respawnCountdownText.text = string.Format(respawnCountdownFormat, seconds);
        }
    }

    void CreateCountdownText()
    {
        Canvas canvas = null;

        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (c.isRootCanvas && c.gameObject.activeInHierarchy &&
                (canvas == null || c.sortingOrder > canvas.sortingOrder))
            {
                canvas = c;
            }
        }

        if (canvas == null)
        {
            return;
        }

        GameObject go = new GameObject("Respawn Countdown", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 120f);
        rect.sizeDelta = new Vector2(800f, 150f);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Lilita_One/LilitaOne-Regular SDF");
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = 96f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.outlineWidth = 0.2f;
        text.outlineColor = Color.black;
        text.raycastTarget = false;

        respawnCountdownText = text;
    }

    public void HealFull()
    {
        currentHp = maxHp;
        UpdateHpUI();

        Debug.Log("Player HP 100% 회복");
    }

    void Respawn()
    {
        if (respawnPoint == null)
        {
            Debug.LogWarning(
                "Respawn Point가 연결되지 않았습니다. 현재 위치에서 HP만 회복합니다."
            );
        }
        else
        {
            // X/Z는 리스폰 지점, Y는 시작 높이를 사용합니다. (바닥에 파묻히지 않게)
            Vector3 respawnPosition = respawnPoint.position;
            respawnPosition.y = spawnY + respawnHeightOffset;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = respawnPosition;
            }
            else
            {
                transform.position = respawnPosition;
            }

            Debug.Log("플레이어 리스폰: " + respawnPoint.name);
        }

        HealFull();

        if (playerController != null)
        {
            playerController.ResetAfterRespawn();
        }

        isDead = false;
    }

    void UpdateHpUI()
    {
        if (hpFillImage == null)
        {
            return;
        }

        hpFillImage.fillAmount = (float)currentHp / maxHp;
    }

    public void HealPercent(float percent)
    {
        if (isDead)
        {
            return;
        }

        int healAmount = Mathf.RoundToInt(maxHp * percent);

        currentHp += healAmount;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        UpdateHpUI();

        Debug.Log(
            "Player HP 회복: " +
            healAmount +
            " / 현재 HP: " +
            currentHp +
            " / " +
            maxHp
        );
    }
}