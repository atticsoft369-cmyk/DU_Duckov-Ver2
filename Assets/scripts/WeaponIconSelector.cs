using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponIconSelector : MonoBehaviour
{
    public static WeaponIconSelector Instance;

    // 실행 중 무기 해금/선택 상태 보존.
    // Reset 버튼을 누를 때만 이 상태를 초기화합니다.
    private static bool persistentStateInitialized = false;
    private static bool[] persistentUnlocked;
    private static int persistentSelectedIndex = 0;
    private static int persistentHealCount = 0;

    // 무기 전체 초기화는 Reset 버튼을 눌렀을 때만 허용합니다.
    // 웨이브 시작/종료나 다른 스크립트가 ResetUnlockState()를 호출해도 무시합니다.
    private bool explicitResetRequested = false;

    [System.Serializable]
    public class WeaponIcon
    {
        public string weaponName;

        [Tooltip("실제 무기 아이콘 Image")]
        public Image iconImage;

        [Tooltip("슬롯 배경 Image. 평소 UNSELECT, 선택 시 SELECT 이미지로 변경")]
        public Image backgroundImage;

        [Tooltip("선택 시 120% 확대할 슬롯 배경/루트")]
        public RectTransform scaleRoot;

        [HideInInspector]
        public Vector3 originalScale = Vector3.one;

        [HideInInspector]
        public Vector3 originalIconScale = Vector3.one;

        [HideInInspector]
        public bool unlocked;
    }

    [Header("Weapon Icons")]
    public WeaponIcon[] weaponIcons;

    [Header("Slot Index Settings")]
    [Tooltip("Heal 아이템 슬롯의 weaponIcons 인덱스 (F키, 선택형 무기가 아님)")]
    public int healSlotIndex = 4;

    [Tooltip("게임 시작 시 바로 사용 가능한 슬롯 인덱스 (0 = Pistol). 나머지는 정비시간에 지급됩니다.")]
    public int[] startUnlockedSlotIndices = new int[] { 0 };

    [Header("Grant Effect")]
    [Tooltip("지급 시 아이콘이 화면 중앙에 머무는 시간")]
    public float grantCenterHoldDuration = 0.6f;

    [Tooltip("중앙에서 슬롯까지 날아가는 시간")]
    public float grantMoveDuration = 0.6f;

    public Vector2 grantIconSize = new Vector2(160f, 160f);
    public float grantStartScale = 1.4f;

    [Header("Select Settings")]
    public int selectedIndex = 0;

    [Tooltip("선택된 슬롯 크기. 1.2 = 120%")]
    public float selectedScale = 1.2f;

    [Tooltip("선택되지 않은 슬롯 크기")]
    public float unselectedScale = 1f;

    [Header("Background Sprites")]
    [Tooltip("선택되지 않은 슬롯 배경 이미지 (UNSELECT)")]
    public Sprite unselectedBackgroundSprite;

    [Tooltip("선택된 슬롯 배경 이미지 (SELECT)")]
    public Sprite selectedBackgroundSprite;

    [Header("Unselected Icon Settings")]
    public Material grayscaleMaterial;

    [Header("Locked Icon Settings")]
    public bool hideLockedIcons = true;
    public float lockedAlpha = 0.25f;

    [Header("Heal Count")]
    [Tooltip("회복 아이템 보유 개수")]
    public int healCount = 0;

    [Tooltip("회복 개수 글자 위치 (슬롯 기준, 오른쪽 위)")]
    public Vector2 healCountOffset = new Vector2(-14f, -14f);
    public float healCountFontSize = 34f;

    [Header("Selected Slot Ammo")]
    [Tooltip("선택한 무기 슬롯 위에 탄환 표시")]
    public bool showSlotAmmo = true;

    [Tooltip("탄환 표시 배경 (비우면 플레이어의 Ammo Background Sprite = BULLET 사용)")]
    public Sprite slotAmmoBackground;

    public Vector2 slotAmmoSize = new Vector2(110f, 32f);
    [Tooltip("탄환 표시 중심의 세로 위치. 0 = 슬롯 윗 라인에 걸쳐서 표시")]
    public float slotAmmoOffsetY = 0f;
    public float slotAmmoFontSize = 24f;

    private TextMeshProUGUI healCountText;
    private RectTransform slotAmmoRoot;
    private TextMeshProUGUI slotAmmoText;
    private TopDownPlayerController cachedPlayer;

    [Header("Animation Settings")]
    [Tooltip("선택 시 크기가 부드럽게 변경")]
    public bool useSmoothScale = true;

    [Tooltip("크기 변경 속도")]
    public float scaleSpeed = 12f;

    // Enter Play Mode 옵션으로 Domain Reload를 끈 경우에도
    // 플레이를 새로 시작할 때 static 상태가 남지 않도록 초기화합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticStateOnPlay()
    {
        ClearPersistentState();
    }

    static void ClearPersistentState()
    {
        persistentStateInitialized = false;
        persistentUnlocked = null;
        persistentSelectedIndex = 0;
        persistentHealCount = 0;
    }

    void Awake()
    {
        Instance = this;

        // 씬이 새로 로드되면(게임오버 후 재시작 포함) 새 게임으로 보고
        // 무기 해금/선택 상태를 피스톨 기본값으로 되돌립니다.
        // 웨이브 전환은 씬을 다시 로드하지 않으므로 해금 상태가 그대로 유지됩니다.
        ClearPersistentState();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        InitializeIcons();
        RestoreOrInitializePersistentState();
        ApplySelectionInstant();
    }

    void Update()
    {
        if (useSmoothScale)
        {
            UpdateIconScalesSmooth();
        }

        // 다른 스크립트가 Slot/Image를 SetActive(false) 하더라도
        // 해금 상태가 유지되어 있다면 즉시 다시 복구합니다.
        EnforcePersistentWeaponVisibility();
    }

    void LateUpdate()
    {
        UpdateHealCountUI();
        UpdateSlotAmmoUI();
    }

    // ---------------- 회복 아이템 개수 ----------------

    void UpdateHealCountUI()
    {
        if (healSlotIndex < 0 || healSlotIndex >= weaponIcons.Length)
        {
            return;
        }

        WeaponIcon heal = weaponIcons[healSlotIndex];
        RectTransform slot = heal.backgroundImage != null ? heal.backgroundImage.rectTransform : heal.scaleRoot;

        if (slot == null)
        {
            return;
        }

        if (healCountText == null)
        {
            GameObject go = new GameObject("Heal Count", typeof(RectTransform));
            go.layer = slot.gameObject.layer;
            go.transform.SetParent(slot, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(80f, 50f);

            healCountText = go.AddComponent<TextMeshProUGUI>();
            TMP_Text label = slot.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label != healCountText) healCountText.font = label.font;
            healCountText.alignment = TextAlignmentOptions.TopRight;
            healCountText.color = Color.white;
            healCountText.outlineWidth = 0.25f;
            healCountText.outlineColor = new Color32(40, 20, 10, 255);
            healCountText.raycastTarget = false;
        }

        healCountText.rectTransform.anchoredPosition = healCountOffset;
        healCountText.fontSize = healCountFontSize;

        // 한 번 획득한 뒤에는 0개여도 계속 표시
        bool show = heal.unlocked;
        if (healCountText.gameObject.activeSelf != show)
        {
            healCountText.gameObject.SetActive(show);
        }

        string text = healCount.ToString();
        if (healCountText.text != text)
        {
            healCountText.text = text;
        }
    }

    // ---------------- 선택 무기 탄환 표시 ----------------

    void UpdateSlotAmmoUI()
    {
        if (!showSlotAmmo || weaponIcons == null || selectedIndex < 0 || selectedIndex >= weaponIcons.Length)
        {
            return;
        }

        if (cachedPlayer == null)
        {
            cachedPlayer = FindFirstObjectByType<TopDownPlayerController>(FindObjectsInactive.Include);
        }

        WeaponIcon icon = weaponIcons[selectedIndex];
        RectTransform slot = icon.backgroundImage != null ? icon.backgroundImage.rectTransform : icon.scaleRoot;

        if (slot == null || cachedPlayer == null)
        {
            return;
        }

        if (slotAmmoRoot == null)
        {
            Sprite bg = slotAmmoBackground != null ? slotAmmoBackground : cachedPlayer.ammoBackgroundSprite;

            GameObject root = new GameObject("Slot Ammo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            slotAmmoRoot = root.GetComponent<RectTransform>();

            Image img = root.GetComponent<Image>();
            img.sprite = bg;
            img.color = bg != null ? Color.white : new Color(0.12f, 0.08f, 0.05f, 0.9f);
            img.raycastTarget = false;

            GameObject textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(slotAmmoRoot, false);

            RectTransform trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            slotAmmoText = textGo.AddComponent<TextMeshProUGUI>();
            if (cachedPlayer.ammoText != null) slotAmmoText.font = cachedPlayer.ammoText.font;
            slotAmmoText.alignment = TextAlignmentOptions.Center;
            slotAmmoText.color = Color.white;
            slotAmmoText.raycastTarget = false;
            slotAmmoText.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // 선택 슬롯이 바뀌면 그 슬롯 위로 옮깁니다. (슬롯 확대도 같이 따라감)
        if (slotAmmoRoot.parent != slot)
        {
            slotAmmoRoot.SetParent(slot, false);
            slotAmmoRoot.gameObject.layer = slot.gameObject.layer;
        }

        slotAmmoRoot.anchorMin = slotAmmoRoot.anchorMax = new Vector2(0.5f, 1f);
        // 슬롯 윗 라인에 배지 중심을 맞춰 겹치게 표시
        slotAmmoRoot.pivot = new Vector2(0.5f, 0.5f);
        slotAmmoRoot.anchoredPosition = new Vector2(0f, slotAmmoOffsetY);
        slotAmmoRoot.sizeDelta = slotAmmoSize;
        slotAmmoRoot.localScale = Vector3.one;
        slotAmmoText.fontSize = slotAmmoFontSize;

        string ammo = cachedPlayer.ammoText != null ? cachedPlayer.ammoText.text : "";
        bool show = icon.unlocked && !string.IsNullOrEmpty(ammo);

        if (slotAmmoRoot.gameObject.activeSelf != show)
        {
            slotAmmoRoot.gameObject.SetActive(show);
        }

        if (show && slotAmmoText.text != ammo)
        {
            slotAmmoText.text = ammo;
        }
    }

    void InitializeIcons()
    {
        for (int i = 0; i < weaponIcons.Length; i++)
        {
            WeaponIcon weaponIcon = weaponIcons[i];

            // scaleRoot를 직접 넣지 않았으면 배경 Image의 RectTransform을 사용합니다.
            if (weaponIcon.scaleRoot == null &&
                weaponIcon.backgroundImage != null)
            {
                weaponIcon.scaleRoot =
                    weaponIcon.backgroundImage.rectTransform;
            }

            // 그래도 없으면 iconImage 자체를 사용합니다.
            if (weaponIcon.scaleRoot == null &&
                weaponIcon.iconImage != null)
            {
                weaponIcon.scaleRoot =
                    weaponIcon.iconImage.rectTransform;
            }

            if (weaponIcon.scaleRoot != null)
            {
                weaponIcon.originalScale =
                    weaponIcon.scaleRoot.localScale;
            }

            if (weaponIcon.iconImage != null)
            {
                weaponIcon.originalIconScale =
                    weaponIcon.iconImage.rectTransform.localScale;
            }
        }
    }

    void RestoreOrInitializePersistentState()
    {
        int count =
            weaponIcons != null
                ? weaponIcons.Length
                : 0;

        if (count <= 0)
        {
            return;
        }

        // 최초 실행 시에만 피스톨 기본 상태를 만듭니다.
        if (!persistentStateInitialized ||
            persistentUnlocked == null ||
            persistentUnlocked.Length != count)
        {
            persistentUnlocked =
                new bool[count];

            for (int i = 0; i < count; i++)
            {
                persistentUnlocked[i] =
                    IsStartUnlocked(i);
            }

            persistentSelectedIndex = 0;
            persistentHealCount = healCount;
            persistentStateInitialized = true;
        }

        healCount = persistentHealCount;

        selectedIndex =
            Mathf.Clamp(
                persistentSelectedIndex,
                0,
                count - 1
            );

        for (int i = 0; i < count; i++)
        {
            weaponIcons[i].unlocked =
                persistentUnlocked[i];

            if (hideLockedIcons)
            {
                SetSlotActive(
                    i,
                    weaponIcons[i].unlocked
                );
            }
            else
            {
                SetSlotActive(i, true);
            }
        }

        ApplyIconVisuals();

        Debug.Log(
            "무기 상태 복원 / Selected=" +
            selectedIndex
        );
    }

    void SavePersistentState()
    {
        if (weaponIcons == null)
        {
            return;
        }

        int count =
            weaponIcons.Length;

        if (persistentUnlocked == null ||
            persistentUnlocked.Length != count)
        {
            persistentUnlocked =
                new bool[count];
        }

        for (int i = 0; i < count; i++)
        {
            persistentUnlocked[i] =
                weaponIcons[i].unlocked;
        }

        persistentSelectedIndex =
            selectedIndex;

        persistentHealCount = healCount;

        persistentStateInitialized = true;
    }

    /// <summary>
    /// 저장된 무기 해금/선택 상태를 현재 UI에 강제로 다시 적용합니다.
    /// 웨이브 전환 시 호출해도 상태를 초기화하지 않습니다.
    /// </summary>
    public void RefreshPersistentWeaponUI()
    {
        if (!persistentStateInitialized ||
            persistentUnlocked == null ||
            weaponIcons == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                persistentUnlocked.Length,
                weaponIcons.Length
            );

        selectedIndex =
            Mathf.Clamp(
                persistentSelectedIndex,
                0,
                Mathf.Max(0, weaponIcons.Length - 1)
            );

        for (int i = 0; i < count; i++)
        {
            weaponIcons[i].unlocked =
                persistentUnlocked[i];

            SetSlotActive(
                i,
                weaponIcons[i].unlocked || !hideLockedIcons
            );
        }

        ApplyIconVisuals();
    }

    private void EnforcePersistentWeaponVisibility()
    {
        if (!persistentStateInitialized ||
            persistentUnlocked == null ||
            weaponIcons == null)
        {
            return;
        }

        int count =
            Mathf.Min(
                persistentUnlocked.Length,
                weaponIcons.Length
            );

        for (int i = 0; i < count; i++)
        {
            // persistentUnlocked가 true인 슬롯은
            // 외부에서 꺼도 다시 켭니다.
            if (persistentUnlocked[i])
            {
                weaponIcons[i].unlocked = true;

                if (weaponIcons[i].scaleRoot != null &&
                    !weaponIcons[i].scaleRoot.gameObject.activeSelf)
                {
                    weaponIcons[i].scaleRoot.gameObject.SetActive(true);
                }

                if (weaponIcons[i].backgroundImage != null &&
                    !weaponIcons[i].backgroundImage.gameObject.activeSelf)
                {
                    weaponIcons[i].backgroundImage.gameObject.SetActive(true);
                }

                if (weaponIcons[i].iconImage != null &&
                    !weaponIcons[i].iconImage.gameObject.activeSelf)
                {
                    weaponIcons[i].iconImage.gameObject.SetActive(true);
                }
            }
        }
    }

    public bool TrySelectWeapon(int index)
    {
        if (index < 0 || index >= weaponIcons.Length)
        {
            return false;
        }

        // Heal 슬롯은 선택형 무기가 아닙니다. (F키 전용)
        // 숫자 4번으로 선택되지 않도록 차단합니다.
        if (index == healSlotIndex)
        {
            return false;
        }

        if (!weaponIcons[index].unlocked)
        {
            Debug.Log(
                (index + 1) +
                "번 슬롯은 아직 활성화되지 않았습니다."
            );

            return false;
        }

        selectedIndex = index;
        SavePersistentState();

        Debug.Log(
            (selectedIndex + 1) +
            "번 슬롯 선택: " +
            weaponIcons[selectedIndex].weaponName
        );

        ApplyIconVisuals();

        if (!useSmoothScale)
        {
            ApplySelectionInstant();
        }

        return true;
    }

    /// <summary>
    /// 무기 지급 연출: 화면 중앙에 아이콘 표시 -> 해당 슬롯으로 날아간 뒤 슬롯 활성화.
    /// slotNumber는 1부터 (1 Pistol, 2 Rifle, 3 Slime Gun, 4 Grenade, 5 Heal)
    /// </summary>
    public void GrantSlotWithEffect(int slotNumber, float delay = 0f)
    {
        int index = slotNumber - 1;

        if (index < 0 || index >= weaponIcons.Length)
        {
            Debug.LogWarning("잘못된 지급 슬롯 번호입니다: " + slotNumber);
            return;
        }

        // 회복 아이템은 이미 있어도 개수가 늘어나므로 계속 지급합니다.
        if (weaponIcons[index].unlocked && index != healSlotIndex)
        {
            return;
        }

        StartCoroutine(GrantRoutine(slotNumber, delay));
    }

    System.Collections.IEnumerator GrantRoutine(int slotNumber, float delay)
    {
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        int index = slotNumber - 1;
        WeaponIcon icon = weaponIcons[index];

        Image sourceImage = icon.iconImage;
        Canvas canvas = sourceImage != null ? sourceImage.canvas : null;

        if (canvas == null && icon.backgroundImage != null)
        {
            canvas = icon.backgroundImage.canvas;
        }

        if (sourceImage == null || sourceImage.sprite == null || canvas == null)
        {
            UnlockSlot(slotNumber);
            yield break;
        }

        canvas = canvas.rootCanvas;

        GameObject fly = new GameObject("Grant_FlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform flyRect = fly.GetComponent<RectTransform>();
        flyRect.SetParent(canvas.transform, false);
        flyRect.SetAsLastSibling();
        flyRect.anchorMin = flyRect.anchorMax = new Vector2(0.5f, 0.5f);
        flyRect.pivot = new Vector2(0.5f, 0.5f);
        flyRect.sizeDelta = grantIconSize;
        flyRect.anchoredPosition = Vector2.zero;

        Image flyImage = fly.GetComponent<Image>();
        flyImage.sprite = sourceImage.sprite;
        flyImage.preserveAspect = true;
        flyImage.raycastTarget = false;

        // 중앙에서 튀어나오는 연출
        float t = 0f;
        float pop = 0.2f;
        while (t < pop)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / pop);
            flyRect.localScale = Vector3.one * Mathf.Lerp(0.2f, grantStartScale, 1f - (1f - k) * (1f - k));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, grantCenterHoldDuration));

        // 목표 슬롯 위치 (슬롯이 꺼져 있으면 위치 계산을 위해 잠깐 켭니다)
        RectTransform target = icon.backgroundImage != null
            ? icon.backgroundImage.rectTransform
            : sourceImage.rectTransform;

        bool wasActive = target.gameObject.activeSelf;
        target.gameObject.SetActive(true);

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, screenPoint, uiCamera, out Vector2 targetLocal);

        target.gameObject.SetActive(wasActive);

        Vector2 startPos = flyRect.anchoredPosition;
        Vector3 startScale = flyRect.localScale;
        Vector3 endScale = Vector3.one * 0.5f;

        t = 0f;
        float move = Mathf.Max(0.01f, grantMoveDuration);
        while (t < move)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / move);
            float s = k * k * (3f - 2f * k);
            flyRect.anchoredPosition = Vector2.Lerp(startPos, targetLocal, s);
            flyRect.localScale = Vector3.Lerp(startScale, endScale, s);
            yield return null;
        }

        Destroy(fly);

        UnlockSlot(slotNumber);
    }

    public void UnlockSlot(int slotNumber)
    {
        int index = slotNumber - 1;

        if (index < 0 || index >= weaponIcons.Length)
        {
            Debug.LogWarning(
                "잘못된 슬롯 번호입니다: " +
                slotNumber
            );

            return;
        }

        weaponIcons[index].unlocked = true;

        if (index == healSlotIndex)
        {
            healCount++;
        }

        SetSlotActive(index, true);
        SavePersistentState();

        Debug.Log(
            slotNumber +
            "번 슬롯 활성화: " +
            weaponIcons[index].weaponName
        );

        ApplyIconVisuals();
    }

    /// <summary>
    /// 디버그/테스트 용도로 모든 무기 슬롯을 임시로 활성화합니다.
    /// </summary>
    public void UnlockAllSlots()
    {
        for (int i = 0; i < weaponIcons.Length; i++)
        {
            weaponIcons[i].unlocked = true;
            SetSlotActive(i, true);
        }

        healCount = Mathf.Max(healCount, 1);

        SavePersistentState();

        Debug.Log(
            "[디버그] 모든 무기 슬롯이 임시로 활성화되었습니다."
        );

        ApplyIconVisuals();

        if (!useSmoothScale)
        {
            ApplySelectionInstant();
        }
    }

    bool IsStartUnlocked(int index)
    {
        if (index == 0)
        {
            return true;
        }

        if (startUnlockedSlotIndices == null)
        {
            return false;
        }

        for (int i = 0; i < startUnlockedSlotIndices.Length; i++)
        {
            if (startUnlockedSlotIndices[i] == index)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsUnlocked(int index)
    {
        if (index < 0 || index >= weaponIcons.Length)
        {
            return false;
        }

        return weaponIcons[index].unlocked;
    }

    public bool IsHealUnlocked()
    {
        // WeaponIcon 배열 기준 Heal = index 3
        // 실제 UI에서는 Slot 5(F키)로 배치되어 있어도 상관없습니다.
        return IsUnlocked(healSlotIndex) && healCount > 0;
    }

    /// <summary>
    /// Heal 아이템만 소비합니다.
    /// 다른 무기의 Unlock 상태 / 현재 선택 무기 / 선택 연출은 건드리지 않습니다.
    /// </summary>
    public void ConsumeHealSlot()
    {
        int healIndex = healSlotIndex;

        if (healIndex < 0 || healIndex >= weaponIcons.Length)
        {
            return;
        }

        // 개수만 줄이고 아이콘은 계속 표시합니다. (0개면 흐리게)
        healCount = Mathf.Max(0, healCount - 1);

        SavePersistentState();
        ApplyIconVisuals();

        Debug.Log("Heal 아이템 사용. 남은 개수: " + healCount);
    }

    public void ResetUnlockState()
    {
        // 중요:
        // Reset 버튼을 통한 명시적 초기화가 아니면 절대 무기 상태를 지우지 않습니다.
        if (!explicitResetRequested)
        {
            Debug.LogWarning(
                "[WeaponIconSelector] 외부 ResetUnlockState 호출 차단 - 무기 상태 유지"
            );
            return;
        }

        selectedIndex = 0;
        healCount = 0;

        for (int i = 0; i < weaponIcons.Length; i++)
        {
            bool isPistol = i == 0;
            bool startUnlocked = IsStartUnlocked(i);
            WeaponIcon weaponIcon = weaponIcons[i];

            weaponIcon.unlocked = startUnlocked;

            // 모든 슬롯 스케일을 먼저 완전히 초기화
            if (weaponIcon.scaleRoot != null)
            {
                weaponIcon.scaleRoot.localScale =
                    weaponIcon.originalScale *
                    (isPistol ? selectedScale : unselectedScale);
            }

            if (weaponIcon.iconImage != null)
            {
                weaponIcon.iconImage.rectTransform.localScale =
                    weaponIcon.originalIconScale *
                    (isPistol ? selectedScale : unselectedScale);
            }

            // 배경 이미지도 명시적으로 초기 상태로 복원
            if (weaponIcon.backgroundImage != null)
            {
                if (isPistol)
                {
                    if (selectedBackgroundSprite != null)
                    {
                        weaponIcon.backgroundImage.sprite =
                            selectedBackgroundSprite;
                    }
                }
                else
                {
                    if (unselectedBackgroundSprite != null)
                    {
                        weaponIcon.backgroundImage.sprite =
                            unselectedBackgroundSprite;
                    }
                }

                weaponIcon.backgroundImage.color = Color.white;
            }

            // 아이콘 Material / Alpha도 초기화
            if (weaponIcon.iconImage != null)
            {
                if (isPistol)
                {
                    weaponIcon.iconImage.material = null;
                    weaponIcon.iconImage.color = Color.white;
                }
                else if (startUnlocked)
                {
                    // 해금됐지만 선택되지 않은 슬롯
                    weaponIcon.iconImage.material = grayscaleMaterial;
                    weaponIcon.iconImage.color = Color.white;
                }
                else
                {
                    weaponIcon.iconImage.material = grayscaleMaterial;

                    Color lockedColor = Color.white;
                    lockedColor.a = lockedAlpha;
                    weaponIcon.iconImage.color = lockedColor;
                }
            }

            // Reset 시 피스톨만 보이고 나머지는 완전히 숨김
            if (hideLockedIcons)
            {
                SetSlotActive(i, startUnlocked);
            }
            else
            {
                SetSlotActive(i, true);
            }
        }

        SavePersistentState();

        Debug.Log("무기 슬롯 완전 초기화: 기본 무기만 활성화 / 선택 상태 복원");
    }

    public void ResetButton()
    {
        explicitResetRequested = true;

        try
        {
            ResetUnlockState();

            TopDownPlayerController player =
                FindObjectOfType<TopDownPlayerController>();

            if (player != null)
            {
                player.ForceResetToPistol();
            }
        }
        finally
        {
            explicitResetRequested = false;
        }

        Debug.Log(
            "[WeaponIconSelector] Reset 버튼으로 무기 상태 전체 초기화"
        );
    }

    void ApplySelectionInstant()
    {
        ApplyIconVisuals();

        for (int i = 0; i < weaponIcons.Length; i++)
        {
            RectTransform root =
                weaponIcons[i].scaleRoot;

            if (root == null)
            {
                continue;
            }

            root.localScale =
                GetTargetScale(i);

            if (weaponIcons[i].iconImage != null)
            {
                weaponIcons[i].iconImage.rectTransform.localScale =
                    GetTargetIconScale(i);
            }
        }
    }

    void UpdateIconScalesSmooth()
    {
        for (int i = 0; i < weaponIcons.Length; i++)
        {
            RectTransform root =
                weaponIcons[i].scaleRoot;

            if (root == null)
            {
                continue;
            }

            Vector3 targetScale =
                GetTargetScale(i);

            root.localScale =
                Vector3.Lerp(
                    root.localScale,
                    targetScale,
                    scaleSpeed * Time.deltaTime
                );

            if (weaponIcons[i].iconImage != null)
            {
                RectTransform iconRect =
                    weaponIcons[i].iconImage.rectTransform;

                iconRect.localScale =
                    Vector3.Lerp(
                        iconRect.localScale,
                        GetTargetIconScale(i),
                        scaleSpeed * Time.deltaTime
                    );
            }
        }
    }

    Vector3 GetTargetScale(int index)
    {
        float multiplier =
            index == selectedIndex
                ? selectedScale
                : unselectedScale;

        return weaponIcons[index].originalScale *
               multiplier;
    }

    Vector3 GetTargetIconScale(int index)
    {
        float multiplier =
            index == selectedIndex
                ? selectedScale
                : unselectedScale;

        return weaponIcons[index].originalIconScale *
               multiplier;
    }

    void ApplyIconVisuals()
    {
        for (int i = 0; i < weaponIcons.Length; i++)
        {
            WeaponIcon weaponIcon =
                weaponIcons[i];

            bool isSelected =
                i == selectedIndex;

            // -------------------------
            // 슬롯 배경 처리
            // -------------------------
            if (weaponIcon.backgroundImage != null)
            {
                if (isSelected)
                {
                    if (selectedBackgroundSprite != null)
                    {
                        weaponIcon.backgroundImage.sprite =
                            selectedBackgroundSprite;
                    }
                }
                else
                {
                    if (unselectedBackgroundSprite != null)
                    {
                        weaponIcon.backgroundImage.sprite =
                            unselectedBackgroundSprite;
                    }
                }

                weaponIcon.backgroundImage.color =
                    Color.white;
            }

            // -------------------------
            // 아이콘 처리
            // -------------------------
            if (weaponIcon.iconImage == null)
            {
                continue;
            }

            if (!weaponIcon.unlocked)
            {
                weaponIcon.iconImage.material =
                    grayscaleMaterial;

                Color lockedColor =
                    Color.white;

                lockedColor.a =
                    lockedAlpha;

                weaponIcon.iconImage.color =
                    lockedColor;

                if (hideLockedIcons)
                {
                    SetSlotActive(i, false);
                }

                continue;
            }

            SetSlotActive(i, true);

            // 회복 아이템을 다 쓰면 아이콘은 남기고 흐리게
            if (i == healSlotIndex && healCount <= 0)
            {
                weaponIcon.iconImage.material = grayscaleMaterial;
                Color dim = Color.white;
                dim.a = 0.45f;
                weaponIcon.iconImage.color = dim;
                continue;
            }

            // 회복 아이템을 가지고 있으면 선택하지 않아도 밝게 표시
            if (i == healSlotIndex)
            {
                weaponIcon.iconImage.material = null;
                weaponIcon.iconImage.color = Color.white;
                continue;
            }

            if (isSelected)
            {
                weaponIcon.iconImage.material =
                    null;

                weaponIcon.iconImage.color =
                    Color.white;
            }
            else
            {
                weaponIcon.iconImage.material =
                    grayscaleMaterial;

                weaponIcon.iconImage.color =
                    Color.white;
            }
        }
    }

    void SetSlotActive(
        int index,
        bool active
    )
    {
        if (index < 0 ||
            index >= weaponIcons.Length)
        {
            return;
        }

        WeaponIcon weaponIcon =
            weaponIcons[index];

        // 슬롯 배경은 무기가 없어도 처음부터 항상 표시합니다.
        if (weaponIcon.scaleRoot != null &&
            !weaponIcon.scaleRoot.gameObject.activeSelf)
        {
            weaponIcon.scaleRoot.gameObject.SetActive(true);
        }

        if (weaponIcon.backgroundImage != null &&
            !weaponIcon.backgroundImage.gameObject.activeSelf)
        {
            weaponIcon.backgroundImage.gameObject.SetActive(true);
        }

        // 무기 아이콘은 획득한 뒤부터 표시 (한 번 획득하면 사라지지 않음)
        if (weaponIcon.iconImage != null &&
            weaponIcon.iconImage.gameObject.activeSelf != active)
        {
            weaponIcon.iconImage.gameObject.SetActive(active);
        }
    }
}
