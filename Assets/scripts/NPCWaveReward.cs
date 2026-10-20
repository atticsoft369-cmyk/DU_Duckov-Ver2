using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NPCWaveReward : MonoBehaviour
{
    public enum RewardType { None, Rifle, Grenade, Heal }

    [Header("Interaction")]
    public Transform player;
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.Space;
    public bool requirePlayerInFront = false;
    [Range(-1f, 1f)] public float frontDotThreshold = 0f;

    [Tooltip("NPC 보상/거리/Space 입력 상태를 Console에 표시")]
    public bool debugInteraction = false;

    [Header("Optional Guide UI")]
    public TMP_Text interactGuideText;
    public string interactGuideFormat = "SPACE - Receive {0}";

    [Header("Speech Bubble (머리 위 대사)")]
    public string speechMessage = "지원품을 받아가세요";
    public string speechInteractHint = "[SPACE] 받기";
    [Tooltip("비워두면 Windows 기본 한글 폰트(맑은 고딕)로 자동 생성합니다.")]
    public TMP_FontAsset speechFont;
    public float speechFontSize = 6f;
    public Color speechColor = Color.white;
    public Color speechOutlineColor = new Color(0.25f, 0.13f, 0.05f, 1f);
    [Range(0f, 0.5f)] public float speechOutlineWidth = 0.3f;
    [Tooltip("NPC 모델 머리 위로 띄우는 높이")]
    public float speechHeightOffset = 0.6f;
    public float speechBobHeight = 0.12f;
    public float speechBobSpeed = 2.5f;

    [Header("Stage Reward Grant")]
    [Tooltip("여러 보상을 받을 때 아이콘이 날아가는 간격")]
    public float pendingGrantInterval = 1.2f;

    [Header("Reward Animation")]
    public Canvas rewardCanvas;
    public float centerHoldDuration = 0.45f;
    public float moveDuration = 0.65f;
    public float startScale = 1.35f;
    public float endScale = 0.75f;
    public Vector2 rewardIconSize = new Vector2(110f, 110f);

    [Header("Reward Slot Targets")]
    public RectTransform rifleSlotTarget;
    public RectTransform grenadeSlotTarget;
    public RectTransform healSlotTarget;

    bool rewardAvailable;
    int rewardWaveNumber;
    RewardType currentRewardType = RewardType.None;
    Sprite currentRewardSprite;
    Coroutine rewardAnimationCoroutine;

    // 정비시간에 등록된 스테이지 보상(슬롯 번호 1부터)
    readonly List<int> pendingSlots = new List<int>();
    readonly List<string> pendingNames = new List<string>();

    TextMeshPro speechText;
    float speechBaseHeight;
    static TMP_FontAsset cachedKoreanFont;

    public bool HasPendingRewards => pendingSlots.Count > 0;

    void Start()
    {
        FindPlayerIfNeeded();
        HideGuide();
        CreateSpeechBubble();
        SetSpeechVisible(false, false);
    }

    void LateUpdate()
    {
        UpdateSpeechBubbleTransform();
    }

    /// <summary>
    /// 정비시간에 받을 스테이지 보상을 등록합니다. NPC 앞에서 Space로 받습니다.
    /// </summary>
    public void AddPendingReward(int slotNumber, string rewardName)
    {
        // 회복약처럼 같은 슬롯도 여러 개 쌓일 수 있습니다. (무기는 이미 있으면 지급 시 무시됨)
        if (slotNumber <= 0)
        {
            return;
        }

        pendingSlots.Add(slotNumber);
        pendingNames.Add(rewardName);

        Debug.Log($"[NPC] 지원품 등록: {rewardName} (슬롯 {slotNumber})", this);
    }

    void ReceivePendingRewards()
    {
        if (WeaponIconSelector.Instance == null)
        {
            Debug.LogWarning("[NPC] WeaponIconSelector가 없어 지원품을 지급할 수 없습니다.", this);
            return;
        }

        float delay = 0f;

        for (int i = 0; i < pendingSlots.Count; i++)
        {
            Debug.Log($"[NPC] 지원품 지급: {pendingNames[i]} (슬롯 {pendingSlots[i]})", this);
            WeaponIconSelector.Instance.GrantSlotWithEffect(pendingSlots[i], delay);
            delay += Mathf.Max(0f, pendingGrantInterval);
        }

        pendingSlots.Clear();
        pendingNames.Clear();
        SetSpeechVisible(false, false);
    }

    void Update()
    {
        FindPlayerIfNeeded();

        if (player == null)
        {
            HideGuide();
            SetSpeechVisible(HasPendingRewards, false);

            if (Input.GetKeyDown(interactKey) && debugInteraction)
            {
                Debug.LogWarning(
                    "[NPC] Space 입력 감지했지만 Player를 찾지 못했습니다.",
                    this
                );
            }

            return;
        }

        Vector3 npcPos =
            new Vector3(
                transform.position.x,
                0f,
                transform.position.z
            );

        Vector3 playerPos =
            new Vector3(
                player.position.x,
                0f,
                player.position.z
            );

        float distance =
            Vector3.Distance(
                npcPos,
                playerPos
            );

        bool inDistance =
            distance <= interactDistance;

        // Space 입력 자체는 보상 유무와 상관없이 먼저 감지합니다.
        if (Input.GetKeyDown(interactKey))
        {
            if (debugInteraction)
            {
                Debug.Log(
                    "[NPC] Space 입력 감지 / 거리=" +
                    distance.ToString("F2") +
                    " / 범위안=" +
                    inDistance +
                    " / 보상있음=" +
                    rewardAvailable +
                    " / 보상종류=" +
                    currentRewardType,
                    this
                );
            }

            if (!inDistance)
            {
                return;
            }

            if (HasPendingRewards)
            {
                ReceivePendingRewards();
                return;
            }

            if (!rewardAvailable)
            {
                Debug.Log(
                    "[NPC] 현재 받을 수 있는 웨이브 보상이 없습니다.",
                    this
                );
                return;
            }

            ReceiveCurrentReward();
            return;
        }

        // 머리 위 대사: 받을 지원품이 있으면 표시, 가까이 가면 SPACE 안내 추가
        SetSpeechVisible(HasPendingRewards, inDistance);

        // 안내 메시지는 실제로 받을 보상이 있을 때만 표시합니다.
        if (rewardAvailable && inDistance)
        {
            ShowGuide();
        }
        else
        {
            HideGuide();
        }
    }

    public void SetWaveReward(int waveNumber, GameObject rewardItemPrefab)
    {
        if (rewardItemPrefab == null)
        {
            ClearReward();
            return;
        }

        RewardType rewardType = GetRewardTypeFromPrefab(rewardItemPrefab);

        if (rewardType == RewardType.None)
        {
            Debug.LogWarning(
                "[NPC] 보상 종류 판정 실패 / Prefab=" +
                rewardItemPrefab.name +
                " / Tag=" +
                rewardItemPrefab.tag,
                this
            );
            ClearReward();
            return;
        }

        rewardWaveNumber = waveNumber;
        currentRewardType = rewardType;
        currentRewardSprite = GetRewardSpriteFromPrefab(rewardItemPrefab);
        rewardAvailable = true;

        Debug.Log(
            "[NPC] 보상 등록 성공 / Wave=" +
            waveNumber +
            " / Reward=" +
            rewardType +
            " / Prefab=" +
            rewardItemPrefab.name,
            this
        );
    }

    public void ClearReward()
    {
        rewardAvailable = false;
        rewardWaveNumber = 0;
        currentRewardType = RewardType.None;
        currentRewardSprite = null;
        HideGuide();
    }

    void ReceiveCurrentReward()
    {
        if (!rewardAvailable || WeaponIconSelector.Instance == null)
        {
            return;
        }

        int slot = GetUnlockSlotNumber(currentRewardType);
        if (slot <= 0) return;

        RewardType rewardType = currentRewardType;
        Sprite rewardSprite = currentRewardSprite;

        rewardAvailable = false;
        HideGuide();

        WeaponIconSelector.Instance.UnlockSlot(slot);
        PlayRewardAnimation(rewardType, rewardSprite);

        Debug.Log($"NPC 보상 획득 / Wave {rewardWaveNumber} / {rewardType}", this);

        rewardWaveNumber = 0;
        currentRewardType = RewardType.None;
        currentRewardSprite = null;
    }

    RewardType GetRewardTypeFromPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return RewardType.None;
        }

        // 1) 기존 Tag 방식 우선
        if (SafeCompareTag(prefab, "item_rifle"))
        {
            return RewardType.Rifle;
        }

        if (SafeCompareTag(prefab, "item_grenade"))
        {
            return RewardType.Grenade;
        }

        if (SafeCompareTag(prefab, "item_heal"))
        {
            return RewardType.Heal;
        }

        // 2) 프리팹 루트에 Tag가 없더라도 자식에 ItemPickup/Tag가 있으면 인식
        Transform[] children =
            prefab.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == null)
            {
                continue;
            }

            GameObject childObject =
                child.gameObject;

            if (SafeCompareTag(childObject, "item_rifle"))
            {
                return RewardType.Rifle;
            }

            if (SafeCompareTag(childObject, "item_grenade"))
            {
                return RewardType.Grenade;
            }

            if (SafeCompareTag(childObject, "item_heal"))
            {
                return RewardType.Heal;
            }
        }

        // 3) 마지막 fallback:
        // 현재 프로젝트의 rifle (1), boom (1), item_heal 같은 프리팹 이름도 인식
        string prefabName =
            prefab.name
                .ToLowerInvariant()
                .Replace("(clone)", "")
                .Trim();

        if (prefabName.Contains("rifle"))
        {
            return RewardType.Rifle;
        }

        if (prefabName.Contains("grenade") ||
            prefabName.Contains("boom"))
        {
            return RewardType.Grenade;
        }

        if (prefabName.Contains("heal"))
        {
            return RewardType.Heal;
        }

        return RewardType.None;
    }

    bool SafeCompareTag(
        GameObject target,
        string tagName)
    {
        if (target == null)
        {
            return false;
        }

        try
        {
            return target.CompareTag(tagName);
        }
        catch
        {
            return false;
        }
    }

    int GetUnlockSlotNumber(RewardType type)
    {
        switch (type)
        {
            // 슬롯 번호(1부터): 1 Pistol, 2 Rifle, 3 Slime Gun, 4 Grenade, 5 Heal(F)
            case RewardType.Rifle: return 2;
            case RewardType.Grenade: return 4;
            case RewardType.Heal: return 5;
            default: return -1;
        }
    }

    RectTransform GetTargetRect(RewardType type)
    {
        switch (type)
        {
            case RewardType.Rifle: return rifleSlotTarget;
            case RewardType.Grenade: return grenadeSlotTarget;
            case RewardType.Heal: return healSlotTarget;
            default: return null;
        }
    }

    Sprite GetRewardSpriteFromPrefab(GameObject prefab)
    {
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
        if (sr != null && sr.sprite != null) return sr.sprite;

        Image img = prefab.GetComponentInChildren<Image>(true);
        if (img != null) return img.sprite;

        return null;
    }

    void PlayRewardAnimation(RewardType type, Sprite sprite)
    {
        RectTransform targetRect = GetTargetRect(type);
        if (rewardCanvas == null || targetRect == null || sprite == null) return;

        if (rewardAnimationCoroutine != null) StopCoroutine(rewardAnimationCoroutine);
        rewardAnimationCoroutine = StartCoroutine(RewardAnimationRoutine(sprite, targetRect));
    }

    IEnumerator RewardAnimationRoutine(Sprite sprite, RectTransform targetRect)
    {
        GameObject iconObject = new GameObject(
            "NPC_Reward_FlyIcon",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(rewardCanvas.transform, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = rewardIconSize;
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.localScale = Vector3.one * startScale;

        Image image = iconObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        float hold = 0f;
        while (hold < centerHoldDuration)
        {
            hold += Time.unscaledDeltaTime;
            yield return null;
        }

        RectTransform canvasRect = rewardCanvas.transform as RectTransform;
        Camera uiCamera = rewardCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : rewardCanvas.worldCamera;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, targetRect.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, uiCamera, out Vector2 targetLocalPosition);

        Vector2 startPos = iconRect.anchoredPosition;
        Vector3 startScaleVector = Vector3.one * startScale;
        Vector3 endScaleVector = Vector3.one * endScale;

        float timer = 0f;
        while (timer < moveDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(timer / moveDuration);
            float s = t * t * (3f - 2f * t);

            iconRect.anchoredPosition = Vector2.Lerp(startPos, targetLocalPosition, s);
            iconRect.localScale = Vector3.Lerp(startScaleVector, endScaleVector, s);

            yield return null;
        }

        Destroy(iconObject);
        rewardAnimationCoroutine = null;
    }

    bool IsPlayerInFront()
    {
        if (player == null)
        {
            return false;
        }

        Vector3 toPlayer =
            player.position -
            transform.position;

        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        Vector3 forward =
            transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude <= 0.001f)
        {
            return true;
        }

        forward.Normalize();

        float dot =
            Vector3.Dot(
                forward,
                toPlayer.normalized
            );

        return dot >= frontDotThreshold;
    }

    // ---------------- 머리 위 대사 ----------------

    void CreateSpeechBubble()
    {
        if (speechText != null)
        {
            return;
        }

        GameObject go = new GameObject("NPC_SpeechBubble");
        go.transform.SetParent(transform, false);

        speechText = go.AddComponent<TextMeshPro>();

        TMP_FontAsset font = speechFont != null ? speechFont : GetKoreanFont();
        if (font != null)
        {
            speechText.font = font;
        }

        speechText.fontSize = speechFontSize;
        speechText.fontStyle = FontStyles.Bold;
        speechText.alignment = TextAlignmentOptions.Center;
        speechText.textWrappingMode = TextWrappingModes.NoWrap;
        speechText.color = speechColor;
        speechText.outlineWidth = speechOutlineWidth;
        speechText.outlineColor = speechOutlineColor;
        speechText.rectTransform.sizeDelta = new Vector2(20f, 4f);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        // NPC 모델의 가장 높은 지점(머리) 기준 높이 계산
        float top = transform.position.y + 2f;
        bool found = false;

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r == null || r.gameObject == go || !r.enabled ||
                !(r is MeshRenderer || r is SkinnedMeshRenderer))
            {
                continue;
            }

            top = found ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            found = true;
        }

        speechBaseHeight = top - transform.position.y + speechHeightOffset;
    }

    static TMP_FontAsset GetKoreanFont()
    {
        if (cachedKoreanFont != null)
        {
            return cachedKoreanFont;
        }

        string[,] candidates =
        {
            { "Malgun Gothic", "Bold" },
            { "Malgun Gothic", "Regular" },
            { "맑은 고딕", "Bold" },
            { "Apple SD Gothic Neo", "Bold" },
            { "Noto Sans CJK KR", "Bold" },
        };

        for (int i = 0; i < candidates.GetLength(0); i++)
        {
            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(candidates[i, 0], candidates[i, 1], 90);
                if (asset != null)
                {
                    asset.name = candidates[i, 0] + " (Runtime)";
                    cachedKoreanFont = asset;
                    return asset;
                }
            }
            catch (System.Exception)
            {
            }
        }

        Debug.LogWarning("[NPC] 한글 시스템 폰트를 찾지 못했습니다. Speech Font에 한글 TMP 폰트를 지정하세요.");
        return null;
    }

    void SetSpeechVisible(bool visible, bool showHint)
    {
        if (speechText == null)
        {
            return;
        }

        if (speechText.gameObject.activeSelf != visible)
        {
            speechText.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        string text = showHint && !string.IsNullOrEmpty(speechInteractHint)
            ? speechMessage + "\n<size=70%><color=#FFE066>" + speechInteractHint + "</color></size>"
            : speechMessage;

        if (speechText.text != text)
        {
            speechText.text = text;
        }
    }

    void UpdateSpeechBubbleTransform()
    {
        if (speechText == null || !speechText.gameObject.activeSelf)
        {
            return;
        }

        float bob = Mathf.Sin(Time.unscaledTime * speechBobSpeed) * speechBobHeight;
        Transform t = speechText.transform;
        t.position = transform.position + Vector3.up * (speechBaseHeight + bob);

        // 부모(NPC) 크기/회전과 무관하게 항상 카메라를 향하고 같은 크기로 보이게
        Vector3 ls = transform.lossyScale;
        t.localScale = new Vector3(
            ls.x != 0f ? 1f / ls.x : 1f,
            ls.y != 0f ? 1f / ls.y : 1f,
            ls.z != 0f ? 1f / ls.z : 1f
        );

        Camera cam = Camera.main;
        if (cam != null)
        {
            t.rotation = cam.transform.rotation;
        }
    }

    void FindPlayerIfNeeded()
    {
        if (player != null) return;

        GameObject found = null;
        try { found = GameObject.FindGameObjectWithTag("Player"); } catch { }

        if (found == null) found = GameObject.Find("Player");
        if (found != null) player = found.transform;
    }

    void ShowGuide()
    {
        if (interactGuideText == null) return;
        interactGuideText.text = string.Format(interactGuideFormat, currentRewardType);
        interactGuideText.gameObject.SetActive(true);
    }

    void HideGuide()
    {
        if (interactGuideText != null)
            interactGuideText.gameObject.SetActive(false);
    }
}
