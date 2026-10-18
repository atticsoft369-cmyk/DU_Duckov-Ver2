using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamHealth : MonoBehaviour
{
    [Header("HP Settings")]
    public int maxHp = 100;
    private int currentHp;

    [Header("HP UI")]
    public Image hpFillImage;

    [Header("Dam Attack Warning Image")]
    public Image damAttackWarningImage;

    [Tooltip("이미지가 0%에서 100%로 나타나는 시간")]
    public float fadeInTime = 0.5f;

    [Tooltip("깜빡이는 간격")]
    public float blinkInterval = 0.5f;

    [Tooltip("한 번의 경고 이미지 재생 시간")]
    public float warningDuration = 3f;

    [Tooltip("공격 상태를 다시 확인하는 간격")]
    public float attackCheckInterval = 5f;

    [Header("Game Manager")]
    public GameStartManager gameStartManager;

    private bool isDestroyed;

    // 마지막으로 댐이 공격받은 시각
    private float lastDamageTime = -999f;

    // 경고 표시 루프
    private Coroutine warningCoroutine;

    private void Awake()
    {
        currentHp = maxHp;
        isDestroyed = false;

        if (gameStartManager == null)
        {
            gameStartManager =
                Object.FindFirstObjectByType<GameStartManager>();
        }

        InitializeWarningImage();
        UpdateHpUI();
    }

    private void InitializeWarningImage()
    {
        if (damAttackWarningImage == null)
        {
            return;
        }

        SetWarningAlpha(0f);
        damAttackWarningImage.gameObject.SetActive(false);
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed || damage <= 0)
        {
            return;
        }

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        // 마지막 공격 시각 갱신
        lastDamageTime = Time.time;

        Debug.Log($"Dam HP: {currentHp} / {maxHp}");

        UpdateHpUI();
        StartAttackWarning();

        if (currentHp <= 0)
        {
            Die();
        }
    }

    private void StartAttackWarning()
    {
        if (damAttackWarningImage == null)
        {
            Debug.LogWarning(
                "Dam Attack Warning Image가 연결되지 않았습니다.",
                this
            );

            return;
        }

        // 이미 경고 루프가 실행 중이면 새로 만들지 않음
        if (warningCoroutine == null)
        {
            warningCoroutine = StartCoroutine(
                AttackWarningLoop()
            );
        }
    }

    private IEnumerator AttackWarningLoop()
    {
        while (!isDestroyed)
        {
            float cycleStartTime = Time.time;

            // 1. 3초 동안 등장 및 깜빡임
            yield return StartCoroutine(
                PlayWarningAnimation()
            );

            // 경고 시작 시점으로부터 총 5초가 되도록 대기
            float elapsedTime = Time.time - cycleStartTime;
            float remainingCheckTime =
                attackCheckInterval - elapsedTime;

            if (remainingCheckTime > 0f)
            {
                yield return new WaitForSeconds(
                    remainingCheckTime
                );
            }

            /*
             * 최근 5초 이내에 공격을 다시 받았으면
             * 경고 애니메이션을 반복합니다.
             */
            bool wasAttackedRecently =
                Time.time - lastDamageTime <=
                attackCheckInterval;

            if (!wasAttackedRecently)
            {
                break;
            }
        }

        HideWarningImage();
        warningCoroutine = null;
    }

    private IEnumerator PlayWarningAnimation()
    {
        damAttackWarningImage.gameObject.SetActive(true);

        // 0초부터 0.5초까지 알파 0 → 1
        float fadeTimer = 0f;

        while (fadeTimer < fadeInTime)
        {
            fadeTimer += Time.deltaTime;

            float alpha = Mathf.Clamp01(
                fadeTimer / fadeInTime
            );

            SetWarningAlpha(alpha);

            yield return null;
        }

        SetWarningAlpha(1f);

        // 페이드 인이 끝난 이후 남은 시간 동안 깜빡임
        float blinkTimer = fadeInTime;
        bool isVisible = true;

        while (blinkTimer < warningDuration)
        {
            yield return new WaitForSeconds(
                blinkInterval
            );

            blinkTimer += blinkInterval;
            isVisible = !isVisible;

            SetWarningAlpha(
                isVisible ? 1f : 0f
            );
        }

        HideWarningImage();
    }

    private void SetWarningAlpha(float alpha)
    {
        if (damAttackWarningImage == null)
        {
            return;
        }

        Color imageColor =
            damAttackWarningImage.color;

        imageColor.a = alpha;
        damAttackWarningImage.color = imageColor;
    }

    private void HideWarningImage()
    {
        if (damAttackWarningImage == null)
        {
            return;
        }

        SetWarningAlpha(0f);
        damAttackWarningImage.gameObject.SetActive(false);
    }

    private void UpdateHpUI()
    {
        if (hpFillImage == null)
        {
            return;
        }

        hpFillImage.fillAmount =
            maxHp > 0
                ? (float)currentHp / maxHp
                : 0f;
    }

    private void Die()
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;

        if (warningCoroutine != null)
        {
            StopCoroutine(warningCoroutine);
            warningCoroutine = null;
        }

        HideWarningImage();

        Debug.Log("Dam destroyed!");

        if (gameStartManager != null)
        {
            gameStartManager.GameOverByDamDestroyed();
        }
    }
}