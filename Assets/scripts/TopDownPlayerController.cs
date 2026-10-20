using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TopDownPlayerController : MonoBehaviour
{
    public enum WeaponType
    {
        // WeaponIconSelector.weaponIcons 인덱스와 같아야 합니다.
        Pistol = 0,
        Rifle = 1,
        SlimeGun = 2,
        Grenade = 3,
        Heal = 4
    }

    [Header("Move Settings")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 12f;

    [Header("Dash Settings")]
    [Tooltip("대쉬 거리. 예: 5면 5미터 이동")]
    public float dashDistance = 5f;

    [Header("Dash Collision Settings")]
    [Tooltip("대쉬 중 막히는 오브젝트 레이어. Wall, Obstacle 같은 레이어만 지정하세요. Player 레이어는 제외하세요.")]
    public LayerMask dashObstacleMask;

    [Tooltip("대쉬 충돌 검사 반지름. 플레이어 몸통 반지름 정도로 설정")]
    public float dashCollisionRadius = 0.4f;

    [Tooltip("벽에 너무 딱 붙지 않게 살짝 띄우는 거리")]
    public float dashCollisionSkin = 0.05f;

    [Tooltip("대쉬 시간. 예: 0.25면 0.25초 안에 dashDistance만큼 이동")]
    public float dashDuration = 0.25f;

    [Tooltip("대쉬 키")]
    public KeyCode dashKey = KeyCode.LeftShift;

    [Header("Heal Item Settings")]
    public KeyCode healKey = KeyCode.F;

    [Range(0f, 1f)]
    public float healPercent = 0.5f;
    public bool consumeHealOnUse = true;

    [Header("Animation")]
    [Tooltip("이동 판정 Float. 0.1 초과 = Move / 0.1 미만 = Idle")]
    public string speedParameterName = "Speed";

    [Tooltip("발사 상태 Bool. 발사 중 = true / 발사하지 않음 = false")]
    public string shootParameterName = "Shoot";

    [Tooltip("사망 Trigger")]
    public string dieParameterName = "Die";

    [Tooltip("무기 모션 구분용 Int. 0 = Pistol, 1 = Rifle")]
    public string weaponModeParameterName = "WeaponMode";

    private bool isDead;

    [Header("Physics")]
    [Tooltip("몬스터에게 밀리지 않도록 설정할 플레이어 Rigidbody 질량")]
    public float pushResistMass = 500f;

    [Header("Walkable Surface (다리 등 올라갈 수 있는 곳)")]
    [Tooltip("WalkableSurface가 붙은 곳에 올라갈 수 있는 최대 높이")]
    public float walkableStepHeight = 2f;

    [Tooltip("올라가고 내려오는 속도")]
    public float walkableClimbSpeed = 8f;

    [Tooltip("이동 방향 앞쪽을 미리 검사하는 거리 (다리 입구에서 막히지 않게)")]
    public float walkableProbeAhead = 0.6f;

    private float baseY;
    private float feetOffset;
    private bool walkableInitialized;

    [Header("Character Model")]
    [Tooltip("캐릭터 모델의 Animator. 비워두면 Player 자신의 Animator를 사용합니다.")]
    public Animator characterAnimator;

    [Header("References")]
    public Camera mainCamera;
    public WeaponIconSelector weaponIconSelector;

    [Header("Ammo UI")]
    public TMP_Text ammoText;
    public string ammoTextFormat = "{0} / {1}";
    public string noAmmoText = "- / -";

    [Tooltip("Ammo Text가 플레이어를 화면상에서 즉시 따라가게 할지 여부")]
    public bool ammoFollowPlayer = true;

    [Tooltip("플레이어 월드 좌표 기준 Ammo Text 표시 위치")]
    public Vector3 ammoWorldOffset = new Vector3(0f, 2.2f, 0f);

    [Tooltip("탄환 표시 뒤에 깔리는 배경 이미지 (BULLET)")]
    public Sprite ammoBackgroundSprite;

    [Tooltip("탄환 배경 크기 (UI 픽셀)")]
    public Vector2 ammoBackgroundSize = new Vector2(130f, 36f);

    [Tooltip("탄환 배경 크기 배율 (1.1 = 110%)")]
    public float ammoBackgroundScale = 1.1f;

    public Color ammoBackgroundColor = new Color(1f, 1f, 1f, 0.9f);

    private RectTransform ammoBackgroundRect;

#if UNITY_EDITOR
    const string DefaultAmmoBackgroundPath = "Assets/(1013) New Data/UI/BULLET.png";

    // 에디터에서 비어 있으면 BULLET 이미지를 자동으로 채웁니다. (씬 저장 시 함께 저장됨)
    void OnValidate()
    {
        if (ammoBackgroundSprite == null)
        {
            ammoBackgroundSprite =
                UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultAmmoBackgroundPath);
        }
    }
#endif

    [Header("Targeting Guide Settings")]
    [Tooltip("마우스 커서 대신 표시할 조준점 UI의 RectTransform (targeting guide 스프라이트를 사용하는 Image)")]
    public RectTransform targetingGuideRectTransform;

    [Tooltip("조준선 끝에 함께 표시할 붉은 점 UI의 RectTransform")]
    public RectTransform targetingPointRectTransform;

    [Tooltip("붉은 점을 십자 조준점과 같은 위치에 표시할지 여부")]
    public bool showTargetingPoint = true;

    [Tooltip("사격 시 회전할 각도. 45도로 두면 십자(+) 모양이 X자 모양으로 바뀐다.")]
    public float targetingGuideFireRotation = 45f;

    [Tooltip("사격 시 축소되는 배율 (0.75 = 75% 크기, 25% 축소)")]
    public float targetingGuideFireScale = 0.75f;

    [Tooltip("사격 직후 원래 크기(십자 모양의 크기)로 돌아오는데 걸리는 시간")]
    public float targetingGuideReturnDuration = 0.2f;

    [Tooltip("십자<->X자로 회전이 전환되는데 걸리는 시간 (상태가 바뀌는 순간에만 재생됨)")]
    public float targetingGuideRotateDuration = 0.15f;

    [Tooltip("마지막 발사 후 이 시간 동안 추가 발사가 없으면 확실하게 기본 십자 상태로 고정한다.")]
    public float targetingGuideIdleResetDelay = 1f;

    [Header("Weapon Visual Root")]
    public Transform weaponTransform;

    [Tooltip("플레이어 기준으로 무기가 앞으로 떨어질 거리")]
    public float weaponForwardOffset = 0.8f;

    [Tooltip("플레이어 기준으로 무기가 위로 올라갈 높이")]
    public float weaponHeightOffset = 1.0f;

    [Tooltip("무기 전체의 회전 보정값")]
    public Vector3 weaponRotationOffsetEuler = Vector3.zero;

    [Header("Held Weapon Objects")]
    [Tooltip("Hierarchy 안의 Player > Weapon > pistol 오브젝트를 넣으세요. 프리팹 말고 씬 오브젝트입니다.")]
    public GameObject pistolObject;

    [Tooltip("Hierarchy 안의 Player > Weapon > rifle 오브젝트를 넣으세요. 프리팹 말고 씬 오브젝트입니다.")]
    public GameObject rifleObject;

    [Tooltip("Hierarchy 안의 Player > Weapon > weapon_grenade 오브젝트를 넣으세요. 프리팹 말고 씬 오브젝트입니다.")]
    public GameObject grenadeObject;

    [Header("Bullet Settings")]
    public GameObject bulletPrefab;
    public float bulletMoveSpeed = 25f;
    public float bulletSpawnHeight = 0.5f;
    public Vector3 bulletRotationOffsetEuler = Vector3.zero;

    [Header("Pistol Settings")]
    [Tooltip("권총 1발당 공격력")]
    [Min(0)]
    public int pistolDamage = 1;

    [Tooltip("권총 탄알 속도. 0이면 Bullet Move Speed 사용")]
    public float pistolBulletSpeed = 0f;

    public float pistolFireRate = 5f;
    public int pistolMaxShotsBeforeCooldown = 12;
    public float pistolCooldownDuration = 3f;
    public float pistolBulletForwardOffset = 1.1f;

    [Header("Rifle Settings")]
    [Tooltip("라이플 1발당 공격력")]
    [Min(0)]
    public int rifleDamage = 1;

    [Tooltip("라이플 탄알 속도. 0이면 Bullet Move Speed 사용")]
    public float rifleBulletSpeed = 0f;

    public float rifleFireRate = 15f;
    public int rifleMaxShotsBeforeCooldown = 30;
    public float rifleCooldownDuration = 3f;
    public float rifleBulletForwardOffset = 1.5f;

    [Header("Grenade Settings")]
    public GameObject grenadePrefab;
    public GameObject grenadeExplosionPrefab;
    public float grenadeFireRate = 1f;
    public float grenadeSpawnHeight = 0.5f;
    public float grenadeSpawnForwardOffset = 1.0f;
    public float grenadeArcHeight = 3f;
    public float grenadeFlightDuration = 0.8f;
    public float grenadeExplosionDelay = 0.1f;

    [Header("Grenade Damage Settings")]
    public int grenadeDamage = 3;
    public float grenadeExplosionRadius = 2.5f;
    public float grenadeDamageColliderDuration = 0.2f;

    [Header("Slime Gun Settings")]
    [Tooltip("슬라임 건 게이지/발사 담당. 비워두면 씬에서 자동으로 찾습니다.")]
    public SlimeGunController slimeGun;

    [Tooltip("슬라임 건 선택 시 손에 들 모델 (선택). 비워두면 마지막 총기 외형을 유지합니다.")]
    public GameObject slimeGunObject;

    [Tooltip("슬라임 건 선택 시 Ammo Text 형식. {0} = 게이지 %")]
    public string slimeGunAmmoFormat = "SLIME {0}%";

    [Tooltip("슬라임 건 발사 후 다음 입력까지 대기 시간")]
    public float slimeGunFireCooldown = 0.3f;

    [Header("Held Grenade Settings")]
    public float heldGrenadeReturnDelay = 0.5f;

    [Header("Reload Indicator Settings")]
    [Tooltip("리로드 중 조준점을 따라다닐 원형 UI의 RectTransform")]
    public RectTransform reloadIndicatorRectTransform;

    [Tooltip("조준점 기준 리로드 UI 위치 보정값")]
    public Vector2 reloadIndicatorScreenOffset = Vector2.zero;

    [Tooltip("리로드 UI 회전 속도 (도/초). 양수는 시계 반대 방향, 음수는 시계 방향")]
    public float reloadIndicatorRotationSpeed = -360f;

    [Tooltip("리로드가 끝났을 때 UI 회전을 0도로 초기화")]
    public bool resetReloadRotationWhenDone = true;

    private Rigidbody rb;
    private Animator animator;

    private Vector3 moveInput;
    private Vector3 lastMoveDirection = Vector3.forward;
    private Vector3 mouseAimDirection = Vector3.forward;
    private Vector3 mouseWorldPosition;

    private WeaponType currentWeapon = WeaponType.Pistol;

    // Grenade를 선택해도 총기 외형/모션은 바뀌지 않도록
    // 마지막으로 사용한 총기(Pistol/Rifle)를 기억합니다.
    private WeaponType lastFirearmWeapon = WeaponType.Pistol;

    private float nextFireTime;

    private int pistolCurrentAmmo;
    private float pistolCooldownTimer;

    private int rifleCurrentAmmo;
    private float rifleCooldownTimer;

    private bool grenadeInputLocked;
    private Coroutine grenadeReturnCoroutine;

    private bool isDashing;
    private Coroutine dashCoroutine;

    private float lastShotTime = -999f;
    private bool targetingGuideIsX;
    private Coroutine targetingGuideRotationCoroutine;
    private Coroutine targetingGuideScaleCoroutine;

    // true일 때만 일반 마우스 커서를 숨기고 타겟 가이드를 표시합니다.
    private bool isWaveCombatActive;

    public bool IsWaveCombatActive => isWaveCombatActive;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 몬스터와 부딪혀도 밀리지 않도록 플레이어를 무겁게 만듭니다.
        if (rb != null)
        {
            rb.mass = Mathf.Max(rb.mass, pushResistMass);
        }

        InitWalkableSurface();
        ApplyGameControl();
        // 캐릭터 모델이 자식으로 들어간 경우 그 모델의 Animator를 사용합니다.
        animator = characterAnimator != null
            ? characterAnimator
            : GetComponent<Animator>();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (rb == null)
        {
            Debug.LogError("Player에 Rigidbody가 없습니다.");
        }

        if (weaponIconSelector == null)
        {
            weaponIconSelector = FindObjectOfType<WeaponIconSelector>();
        }

        AutoFindHeldWeapons();

        // 타이틀 화면(컨트롤러 비활성 상태)에서는 손에 든 무기를 모두 숨깁니다.
        // 게임 시작으로 컨트롤러가 켜지면 Start()에서 기본 무기(권총)만 다시 표시됩니다.
        HideAllHeldWeapons();

        if (slimeGun == null)
        {
            slimeGun = FindFirstObjectByType<SlimeGunController>();
        }

        if (targetingGuideRectTransform != null)
        {
            targetingGuideRectTransform.localRotation = Quaternion.identity;
            targetingGuideRectTransform.localScale = Vector3.one;

            // 조준점 이미지가 UI 버튼 클릭을 가로채지 않도록 합니다.
            Image targetingGuideImage =
                targetingGuideRectTransform.GetComponent<Image>();

            if (targetingGuideImage != null)
            {
                targetingGuideImage.raycastTarget = false;
            }
        }

        if (targetingPointRectTransform != null)
        {
            Image targetingPointImage =
                targetingPointRectTransform.GetComponent<Image>();

            if (targetingPointImage != null)
            {
                targetingPointImage.raycastTarget = false;
            }

            targetingPointRectTransform.gameObject.SetActive(false);
        }

        if (reloadIndicatorRectTransform != null)
        {
            Image reloadIndicatorImage =
                reloadIndicatorRectTransform.GetComponent<Image>();

            if (reloadIndicatorImage != null)
            {
                reloadIndicatorImage.raycastTarget = false;
            }

            reloadIndicatorRectTransform.localRotation = Quaternion.identity;
            reloadIndicatorRectTransform.gameObject.SetActive(false);
        }

        // 타이틀 상태에서는 일반 마우스 커서를 표시합니다.
        SetWaveCombatActive(false);
    }

    void OnDestroy()
    {
        Cursor.visible = true;
    }

    void Start()
    {
        pistolCurrentAmmo = pistolMaxShotsBeforeCooldown;
        rifleCurrentAmmo = rifleMaxShotsBeforeCooldown;

        // 자동 무기 초기화 금지.
        // 최초 기본값은 currentWeapon = Pistol 이므로 별도 Reset이 필요 없습니다.
        // 전체 무기 초기화는 Reset 버튼에서 ForceResetToPistol()을 호출할 때만 수행합니다.
        UpdateHeldWeaponObjects();
        SetWeaponAnimationMode(lastFirearmWeapon);

        if (reloadIndicatorRectTransform != null)
        {
            reloadIndicatorRectTransform.gameObject.SetActive(false);
            reloadIndicatorRectTransform.localRotation = Quaternion.identity;
        }

        UpdateAmmoText();
    }

    void Update()
    {
        UpdateTargetingGuide();

        if (isDead)
        {
            UpdateReloadIndicator();
            UpdateAmmoText();
            return;
        }

        ReadInput();
        UpdateMouseAimDirection();
        UpdateSlimeGunAimIndicator();
        UpdateHeldWeaponTransform();
        UpdateAnimation();
        UpdateCooldowns();
        UpdateReloadIndicator();
        HandleHealInput();
        HandleWeaponInput();
        HandleReloadInput();
        HandleDashInput();
        Shoot();
        UpdateAmmoText();
    }
    void LateUpdate()
    {
        // 카메라 이동/스크롤이 모두 끝난 뒤 같은 프레임에 위치를 다시 계산합니다.
        // Lerp나 SmoothDamp를 사용하지 않으므로 Ammo Text가 지연 없이 즉시 따라옵니다.
        UpdateAmmoScreenPositionImmediate();
        UpdateAmmoBackground();
    }

    void UpdateAmmoBackground()
    {
        if (ammoText == null || ammoBackgroundSprite == null)
        {
            return;
        }

        if (ammoBackgroundRect == null)
        {
            RectTransform textRect = ammoText.rectTransform;
            Transform parent = textRect.parent;

            if (parent == null)
            {
                return;
            }

            GameObject bg = new GameObject("Ammo BG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.layer = ammoText.gameObject.layer;
            ammoBackgroundRect = bg.GetComponent<RectTransform>();
            ammoBackgroundRect.SetParent(parent, false);

            // 텍스트보다 먼저 그려지도록 바로 앞 순서에 둡니다.
            ammoBackgroundRect.SetSiblingIndex(textRect.GetSiblingIndex());

            Image img = bg.GetComponent<Image>();
            img.sprite = ammoBackgroundSprite;
            img.color = ammoBackgroundColor;
            img.raycastTarget = false;
            img.type = ammoBackgroundSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        }

        RectTransform tr = ammoText.rectTransform;
        ammoBackgroundRect.anchorMin = tr.anchorMin;
        ammoBackgroundRect.anchorMax = tr.anchorMax;
        ammoBackgroundRect.pivot = new Vector2(0.5f, 0.5f);

        // 실제로 그려진 글자 영역의 정중앙에 배경을 맞춥니다.
        // (textBounds는 텍스트 RectTransform 피벗 기준 로컬 좌표)
        Vector2 centerOffset;
        Bounds tb = ammoText.textBounds;

        if (tb.size.x > 0.001f)
        {
            centerOffset = new Vector2(tb.center.x * tr.localScale.x, tb.center.y * tr.localScale.y);
        }
        else
        {
            centerOffset = new Vector2(
                (0.5f - tr.pivot.x) * tr.rect.width,
                (0.5f - tr.pivot.y) * tr.rect.height
            );
        }

        ammoBackgroundRect.anchoredPosition = tr.anchoredPosition + centerOffset;
        ammoBackgroundRect.sizeDelta = ammoBackgroundSize * ammoBackgroundScale;
        ammoBackgroundRect.localScale = tr.localScale;

        bool visible = ammoText.gameObject.activeInHierarchy && ammoText.enabled && !string.IsNullOrEmpty(ammoText.text);

        if (ammoBackgroundRect.gameObject.activeSelf != visible)
        {
            ammoBackgroundRect.gameObject.SetActive(visible);
        }
    }

    void UpdateAmmoScreenPositionImmediate()
    {
        if (!ammoFollowPlayer ||
            ammoText == null ||
            mainCamera == null)
        {
            return;
        }

        RectTransform ammoRect =
            ammoText.rectTransform;

        RectTransform parentRect =
            ammoRect.parent as RectTransform;

        if (parentRect == null)
        {
            return;
        }

        Canvas canvas =
            ammoText.GetComponentInParent<Canvas>();

        Vector3 worldPosition =
            transform.position +
            ammoWorldOffset;

        Vector2 screenPoint =
            mainCamera.WorldToScreenPoint(
                worldPosition
            );

        Camera uiCamera = null;

        if (canvas != null &&
            canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera =
                canvas.worldCamera != null
                    ? canvas.worldCamera
                    : mainCamera;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            // 보간 없이 즉시 대입
            ammoRect.anchoredPosition =
                localPoint;
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
            }

            return;
        }

        if (isDashing)
        {
            return;
        }

        Move();
        RotateToMouseDirection();
    }

    void UpdateTargetingGuide()
    {
        if (targetingGuideRectTransform == null)
        {
            return;
        }

        // 웨이브 전투 중이 아니면 타겟 가이드를 갱신하지 않습니다.
        if (!isWaveCombatActive)
        {
            return;
        }

        targetingGuideRectTransform.position = Input.mousePosition;

        // 붉은 점도 십자 조준점과 정확히 같은 화면 위치를 따라갑니다.
        if (targetingPointRectTransform != null && showTargetingPoint)
        {
            targetingPointRectTransform.position = Input.mousePosition;
        }

        // 마지막 발사 후 지정 시간이 지나면 기본 십자 상태로 돌아갑니다.
        if (targetingGuideIsX &&
            Time.time - lastShotTime >= targetingGuideIdleResetDelay)
        {
            targetingGuideIsX = false;

            if (targetingGuideRotationCoroutine != null)
            {
                StopCoroutine(targetingGuideRotationCoroutine);
            }

            targetingGuideRotationCoroutine =
                StartCoroutine(RotateTargetingGuideTo(0f));

            if (targetingGuideScaleCoroutine != null)
            {
                StopCoroutine(targetingGuideScaleCoroutine);
                targetingGuideScaleCoroutine = null;
            }

            targetingGuideRectTransform.localScale = Vector3.one;
        }
    }

    public void SetWaveCombatActive(bool active)
    {
        isWaveCombatActive = active;

        // 웨이브 중: 기본 커서 숨김 / 타겟 가이드 표시
        // 타이틀·웨이브 대기·Next Wave 상태: 기본 커서 표시 / 타겟 가이드 숨김
        Cursor.visible = !active;
        Cursor.lockState = CursorLockMode.None;

        if (targetingPointRectTransform != null)
        {
            targetingPointRectTransform.gameObject.SetActive(
                active && showTargetingPoint
            );

            if (active)
            {
                targetingPointRectTransform.position = Input.mousePosition;
            }
        }

        if (targetingGuideRectTransform != null)
        {
            targetingGuideRectTransform.gameObject.SetActive(active);

            if (!active)
            {
                if (targetingGuideRotationCoroutine != null)
                {
                    StopCoroutine(targetingGuideRotationCoroutine);
                    targetingGuideRotationCoroutine = null;
                }

                if (targetingGuideScaleCoroutine != null)
                {
                    StopCoroutine(targetingGuideScaleCoroutine);
                    targetingGuideScaleCoroutine = null;
                }

                targetingGuideIsX = false;
                targetingGuideRectTransform.localRotation = Quaternion.identity;
                targetingGuideRectTransform.localScale = Vector3.one;
            }
            else
            {
                targetingGuideRectTransform.position = Input.mousePosition;
            }
        }
    }

    void TriggerTargetingGuidePulse()
    {
        lastShotTime = Time.time;

        if (targetingGuideRectTransform == null)
        {
            return;
        }

        // 처음 쏘는 순간(십자 -> X자)에만 회전 전환 애니메이션을 시작한다.
        // 이미 X자 상태(연속 발사 중)라면 회전은 그대로 유지된다.
        if (!targetingGuideIsX)
        {
            targetingGuideIsX = true;

            if (targetingGuideRotationCoroutine != null)
            {
                StopCoroutine(targetingGuideRotationCoroutine);
            }

            targetingGuideRotationCoroutine =
                StartCoroutine(RotateTargetingGuideTo(targetingGuideFireRotation));
        }

        // 크기 펄스는 발사할 때마다 항상 재생된다.
        if (targetingGuideScaleCoroutine != null)
        {
            StopCoroutine(targetingGuideScaleCoroutine);
        }

        targetingGuideScaleCoroutine = StartCoroutine(TargetingGuideScalePulseRoutine());
    }

    IEnumerator RotateTargetingGuideTo(float targetZAngle)
    {
        Quaternion startRotation = targetingGuideRectTransform.localRotation;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetZAngle);

        float duration = Mathf.Max(0.01f, targetingGuideRotateDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float ratio = Mathf.Clamp01(elapsed / duration);

            targetingGuideRectTransform.localRotation =
                Quaternion.Slerp(startRotation, targetRotation, ratio);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetingGuideRectTransform.localRotation = targetRotation;

        targetingGuideRotationCoroutine = null;
    }

    IEnumerator TargetingGuideScalePulseRoutine()
    {
        // 발사 즉시 축소. 회전은 이 코루틴에서 건드리지 않는다.
        targetingGuideRectTransform.localScale =
            Vector3.one * targetingGuideFireScale;

        Vector3 startScale = targetingGuideRectTransform.localScale;

        float duration = Mathf.Max(0.01f, targetingGuideReturnDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float ratio = Mathf.Clamp01(elapsed / duration);

            targetingGuideRectTransform.localScale =
                Vector3.Lerp(startScale, Vector3.one, ratio);

            elapsed += Time.deltaTime;
            yield return null;
        }

        targetingGuideRectTransform.localScale = Vector3.one;

        targetingGuideScaleCoroutine = null;
    }

    void PlayShootAnimation()
    {
        // Shoot은 Trigger가 아니라 Bool 상태로 관리합니다.
        // 실제 true/false 전환은 UpdateAnimation()에서 매 프레임 처리합니다.
    }

    void SetWeaponAnimationMode(WeaponType weaponType)
    {
        if (animator == null || isDead)
        {
            return;
        }

        // 무기 변경 순간 기존 Shoot 상태가 남아 있지 않도록 먼저 해제합니다.
        animator.SetBool(
            shootParameterName,
            false
        );

        if (weaponType == WeaponType.Pistol)
        {
            animator.SetInteger(
                weaponModeParameterName,
                0
            );
        }
        else if (weaponType == WeaponType.Rifle)
        {
            animator.SetInteger(
                weaponModeParameterName,
                1
            );
        }
        else if (weaponType == WeaponType.SlimeGun)
        {
            // 슬라임 런처 전용 모션 (Slime_Luncher / Luncher_Move)
            animator.SetInteger(
                weaponModeParameterName,
                2
            );
        }

        animator.SetFloat(
            speedParameterName,
            moveInput.magnitude
        );
    }

    void HideAllHeldWeapons()
    {
        if (pistolObject != null) pistolObject.SetActive(false);
        if (rifleObject != null) rifleObject.SetActive(false);
        if (slimeGunObject != null) slimeGunObject.SetActive(false);
        if (grenadeObject != null) grenadeObject.SetActive(false);
    }

    void AutoFindHeldWeapons()
    {
        if (weaponTransform == null)
        {
            return;
        }

        if (pistolObject == null)
        {
            Transform found = weaponTransform.Find("pistol");

            if (found != null)
            {
                pistolObject = found.gameObject;
            }
        }

        if (rifleObject == null)
        {
            Transform found = weaponTransform.Find("rifle");

            if (found != null)
            {
                rifleObject = found.gameObject;
            }
        }

        if (slimeGunObject == null)
        {
            Transform found = weaponTransform.Find("Slime gun");

            if (found != null)
            {
                slimeGunObject = found.gameObject;
            }
        }

        if (grenadeObject == null)
        {
            Transform found = weaponTransform.Find("weapon_grenade");

            if (found != null)
            {
                grenadeObject = found.gameObject;
            }
        }
    }

    void ReadInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        moveInput = new Vector3(horizontal, 0f, vertical).normalized;

        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = moveInput;
        }
    }

    void Move()
    {
        if (rb == null)
        {
            return;
        }

        if (moveInput.sqrMagnitude <= 0.01f)
        {
            rb.linearVelocity = Vector3.zero;

            // 멈춰 있어도 다리 위/아래 높이는 맞춰 줍니다.
            ApplyWalkableHeight(GetWalkableTargetY(rb.position, Vector3.zero));
            return;
        }

        Vector3 nextPosition = rb.position + moveInput * moveSpeed * Time.fixedDeltaTime;
        float nextY = GetWalkableTargetY(nextPosition, moveInput);
        ApplyWalkableHeight(nextY);

        nextPosition.y = nextY;
        rb.MovePosition(nextPosition);
    }

    void ApplyWalkableHeight(float y)
    {
        if (rb == null || Mathf.Abs(rb.position.y - y) < 0.0005f)
        {
            return;
        }

        // Y가 고정된 Rigidbody라도 위치를 직접 지정하면 높이가 바뀝니다.
        Vector3 p = rb.position;
        p.y = y;
        rb.position = p;
        transform.position = p;
    }

    // ---------------- Walkable Surface ----------------

    void InitWalkableSurface()
    {
        if (rb == null || walkableInitialized)
        {
            return;
        }

        walkableInitialized = true;
        baseY = transform.position.y;

        // 루트 위치와 발바닥 사이 거리
        float minY = float.MaxValue;
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            if (c != null && c.enabled && !c.isTrigger)
            {
                minY = Mathf.Min(minY, c.bounds.min.y);
            }
        }

        feetOffset = minY < float.MaxValue ? Mathf.Max(0f, baseY - minY) : 0f;

        // Y 고정은 유지합니다(물리 충돌로 위아래 들썩임 방지).
        // 다리 위 높이는 ApplyWalkableHeight에서 위치를 직접 지정합니다.
        rb.useGravity = false;
        rb.constraints |= RigidbodyConstraints.FreezePositionY;
    }

    float GetWalkableTargetY(Vector3 position, Vector3 moveDirection)
    {
        float target = baseY;
        float feetBaseY = baseY - feetOffset;

        target = Mathf.Max(target, SampleWalkableY(position, feetBaseY));

        if (moveDirection.sqrMagnitude > 0.01f && walkableProbeAhead > 0f)
        {
            Vector3 ahead = position + moveDirection.normalized * walkableProbeAhead;
            target = Mathf.Max(target, SampleWalkableY(ahead, feetBaseY));
        }

        return Mathf.MoveTowards(
            rb.position.y,
            target,
            Mathf.Max(0.01f, walkableClimbSpeed) * Time.fixedDeltaTime
        );
    }

    float SampleWalkableY(Vector3 position, float feetBaseY)
    {
        float best = float.MinValue;
        float topLimit = feetBaseY + walkableStepHeight;

        Vector3 origin = new Vector3(position.x, topLimit + 0.5f, position.z);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            walkableStepHeight + 3f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null ||
                hit.collider.GetComponentInParent<WalkableSurface>() == null)
            {
                continue;
            }

            if (hit.point.y > topLimit)
            {
                continue;
            }

            best = Mathf.Max(best, hit.point.y + feetOffset);
        }

        return best;
    }

    void RotateToMouseDirection()
    {
        if (rb == null)
        {
            return;
        }

        Vector3 aimDirection = GetAimDirection();
        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude <= 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

        Quaternion nextRotation = Quaternion.Slerp(
            rb.rotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime
        );

        rb.MoveRotation(nextRotation);
    }

    void HandleDashInput()
    {
        if (isDashing)
        {
            return;
        }

        if (!Input.GetKeyDown(dashKey))
        {
            return;
        }

        if (moveInput.sqrMagnitude <= 0.01f)
        {
            return;
        }

        StartDash(moveInput);
    }

    void StartDash(Vector3 direction)
    {
        if (rb == null)
        {
            return;
        }

        if (dashDuration <= 0f)
        {
            Debug.LogWarning("Dash Duration은 0보다 커야 합니다.");
            return;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = lastMoveDirection;
        }

        direction.Normalize();

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
        }

        dashCoroutine = StartCoroutine(DashRoutine(direction));
    }

    IEnumerator DashRoutine(Vector3 direction)
    {
        isDashing = true;

        nextFireTime = Time.time + dashDuration;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        Quaternion dashRotation = Quaternion.LookRotation(direction, Vector3.up);

        float elapsedTime = 0f;
        Vector3 startPosition = rb.position;

        Vector3 targetPosition = GetSafeDashTargetPosition(
            startPosition,
            direction,
            dashDistance
        );

        while (elapsedTime < dashDuration)
        {
            float ratio = elapsedTime / dashDuration;

            Vector3 desiredPosition = Vector3.Lerp(startPosition, targetPosition, ratio);
            Vector3 currentPosition = rb.position;

            Vector3 moveDelta = desiredPosition - currentPosition;
            moveDelta.y = 0f;

            if (moveDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 moveDirection = moveDelta.normalized;
                float moveDistance = moveDelta.magnitude;

                if (Physics.SphereCast(
                    currentPosition,
                    dashCollisionRadius,
                    moveDirection,
                    out RaycastHit hit,
                    moveDistance + dashCollisionSkin,
                    dashObstacleMask,
                    QueryTriggerInteraction.Ignore
                ))
                {
                    Vector3 blockedPosition =
                        currentPosition +
                        moveDirection * Mathf.Max(0f, hit.distance - dashCollisionSkin);

                    rb.MovePosition(blockedPosition);
                    rb.linearVelocity = Vector3.zero;

                    isDashing = false;
                    dashCoroutine = null;
                    nextFireTime = 0f;

                    yield break;
                }
            }

            rb.MovePosition(desiredPosition);
            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    dashRotation,
                    rotationSpeed * Time.fixedDeltaTime
                )
            );

            elapsedTime += Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(targetPosition);
        rb.linearVelocity = Vector3.zero;

        isDashing = false;
        dashCoroutine = null;

        nextFireTime = 0f;
    }

    void UpdateMouseAimDirection()
    {
        if (mainCamera == null)
        {
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));

        if (groundPlane.Raycast(ray, out float enter))
        {
            mouseWorldPosition = ray.GetPoint(enter);

            mouseAimDirection = mouseWorldPosition - transform.position;
            mouseAimDirection.y = 0f;

            if (mouseAimDirection.sqrMagnitude > 0.01f)
            {
                mouseAimDirection.Normalize();
            }
        }
    }

    void UpdateHeldWeaponTransform()
    {
        if (weaponTransform == null)
        {
            return;
        }

        Vector3 aimDirection = GetAimDirection();

        weaponTransform.position =
            transform.position +
            Vector3.up * weaponHeightOffset +
            aimDirection * weaponForwardOffset;

        Quaternion aimRotation = Quaternion.LookRotation(aimDirection, Vector3.up);

        weaponTransform.rotation =
            aimRotation * Quaternion.Euler(weaponRotationOffsetEuler);
    }

    Vector3 GetAimDirection()
    {
        Vector3 aimDirection = mouseAimDirection;
        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude <= 0.01f)
        {
            aimDirection = transform.forward;
        }

        aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude <= 0.01f)
        {
            aimDirection = Vector3.forward;
        }

        return aimDirection.normalized;
    }

    void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (isDead)
        {
            animator.SetFloat(speedParameterName, 0f);
            animator.SetBool(shootParameterName, false);
            return;
        }

        // 이동:
        // Speed > 0.1  -> Move
        // Speed < 0.1  -> Idle
        float animationSpeed =
            isDashing
                ? 1f
                : moveInput.magnitude;

        animator.SetFloat(
            speedParameterName,
            animationSpeed
        );

        // 발사:
        // 마우스를 누르고 있고 실제 총기(Pistol/Rifle)를 사용 가능한 상태일 때만 true.
        // 버튼을 놓는 즉시 false가 되어 Shoot -> Idle/Move Transition이 작동합니다.
        // 발사 간격(nextFireTime)으로 막지 않습니다.
        // 막으면 발사 직후마다 Shoot이 false가 되어 사격 모션이 1프레임만 켜졌다 꺼집니다.
        bool canUseShootMotion =
            isWaveCombatActive &&
            !isDashing &&
            (
                currentWeapon == WeaponType.Pistol ||
                currentWeapon == WeaponType.Rifle ||
                currentWeapon == WeaponType.SlimeGun
            ) &&
            !IsCurrentWeaponReloading();

        bool isShooting =
            canUseShootMotion &&
            Input.GetMouseButton(0);

        animator.SetBool(
            shootParameterName,
            isShooting
        );
    }

    void UpdateCooldowns()
    {
        if (pistolCooldownTimer > 0f)
        {
            pistolCooldownTimer -= Time.deltaTime;

            if (pistolCooldownTimer <= 0f)
            {
                pistolCooldownTimer = 0f;
                pistolCurrentAmmo = pistolMaxShotsBeforeCooldown;
                Debug.Log("피스톨 리로드 완료");
            }
        }

        if (rifleCooldownTimer > 0f)
        {
            rifleCooldownTimer -= Time.deltaTime;

            if (rifleCooldownTimer <= 0f)
            {
                rifleCooldownTimer = 0f;
                rifleCurrentAmmo = rifleMaxShotsBeforeCooldown;
                Debug.Log("라이플 리로드 완료");
            }
        }
    }

    void UpdateReloadIndicator()
    {
        if (reloadIndicatorRectTransform == null)
        {
            return;
        }

        bool isReloading = IsCurrentWeaponReloading();

        reloadIndicatorRectTransform.gameObject.SetActive(isReloading);

        if (!isReloading)
        {
            if (resetReloadRotationWhenDone)
            {
                reloadIndicatorRectTransform.localRotation = Quaternion.identity;
            }

            return;
        }

        // 조준선과 동일하게 마우스 위치를 따라다닙니다.
        Vector2 targetScreenPosition =
            (Vector2)Input.mousePosition + reloadIndicatorScreenOffset;

        reloadIndicatorRectTransform.position = targetScreenPosition;

        // 리로드가 진행되는 동안 원형 UI를 계속 회전시킵니다.
        reloadIndicatorRectTransform.Rotate(
            0f,
            0f,
            reloadIndicatorRotationSpeed * Time.deltaTime
        );
    }

    bool IsCurrentWeaponReloading()
    {
        if (currentWeapon == WeaponType.Pistol && pistolCooldownTimer > 0f)
        {
            return true;
        }

        if (currentWeapon == WeaponType.Rifle && rifleCooldownTimer > 0f)
        {
            return true;
        }

        return false;
    }

    void HandleWeaponInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TrySelectWeapon(WeaponType.Pistol);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TrySelectWeapon(WeaponType.Rifle);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TrySelectWeapon(WeaponType.SlimeGun);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            TrySelectWeapon(WeaponType.Grenade);
        }
        // Heal은 F 키 전용 아이템이며 무기 선택 슬롯이 아닙니다.

        if ((currentWeapon == WeaponType.Grenade ||
             currentWeapon == WeaponType.SlimeGun) &&
            grenadeInputLocked)
        {
            if (!Input.GetMouseButton(0))
            {
                grenadeInputLocked = false;
            }
        }
    }

    void HandleReloadInput()
    {
        if (!Input.GetKeyDown(KeyCode.R))
        {
            return;
        }

        if (currentWeapon == WeaponType.Pistol)
        {
            TryStartPistolReload();
            return;
        }

        if (currentWeapon == WeaponType.Rifle)
        {
            TryStartRifleReload();
            return;
        }

        // Grenade, Heal은 리로드 없음
    }

    bool TrySelectWeapon(WeaponType weaponType)
    {
        int index = (int)weaponType;

        if (weaponIconSelector == null)
        {
            weaponIconSelector = FindObjectOfType<WeaponIconSelector>();
        }

        if (weaponIconSelector != null)
        {
            if (!weaponIconSelector.IsUnlocked(index))
            {
                Debug.Log((index + 1) + "번은 아직 사용할 수 없습니다.");
                return false;
            }

            bool uiSelected = weaponIconSelector.TrySelectWeapon(index);

            if (!uiSelected)
            {
                return false;
            }
        }

        ApplyWeapon(weaponType);
        return true;
    }

    void ApplyWeapon(WeaponType weaponType)
    {
        currentWeapon = weaponType;
        nextFireTime = 0f;

        if (weaponType == WeaponType.Pistol)
        {
            lastFirearmWeapon = WeaponType.Pistol;
            grenadeInputLocked = false;

            // 권총 모션 세트로 변경
            SetWeaponAnimationMode(WeaponType.Pistol);
        }
        else if (weaponType == WeaponType.Rifle)
        {
            lastFirearmWeapon = WeaponType.Rifle;
            grenadeInputLocked = false;

            // 라이플 모션 세트로 변경
            SetWeaponAnimationMode(WeaponType.Rifle);
        }
        else if (weaponType == WeaponType.SlimeGun)
        {
            lastFirearmWeapon = WeaponType.SlimeGun;

            // 선택 순간 눌려 있던 클릭으로 발사되지 않게 잠급니다.
            grenadeInputLocked = Input.GetMouseButton(0);

            // 라이플 모션 세트 공유
            SetWeaponAnimationMode(WeaponType.SlimeGun);
        }
        else if (weaponType == WeaponType.Grenade)
        {
            // 중요:
            // Grenade는 선택만 변경하고 Pistol/Rifle 모션 Trigger를 건드리지 않습니다.
            // 현재 들고 있던 총기 모션과 외형을 그대로 유지합니다.
            grenadeInputLocked = Input.GetMouseButton(0);
        }
        else
        {
            grenadeInputLocked = false;
        }

        UpdateHeldWeaponObjects();
        UpdateReloadIndicator();
        UpdateAmmoText();

        Debug.Log(
            "현재 선택: " + weaponType +
            " / 유지 중인 총기 모션: " + lastFirearmWeapon
        );
    }

    public void ForceResetToPistol()
    {
        currentWeapon = WeaponType.Pistol;
        lastFirearmWeapon = WeaponType.Pistol;
        nextFireTime = 0f;
        grenadeInputLocked = false;

        pistolCurrentAmmo = pistolMaxShotsBeforeCooldown;
        rifleCurrentAmmo = rifleMaxShotsBeforeCooldown;

        pistolCooldownTimer = 0f;
        rifleCooldownTimer = 0f;

        if (weaponIconSelector == null)
        {
            weaponIconSelector = FindObjectOfType<WeaponIconSelector>();
        }

        if (weaponIconSelector != null)
        {
            weaponIconSelector.TrySelectWeapon(0);
        }

        if (animator != null)
        {
            // 상태를 Play/CrossFade로 강제하지 않고 Parameter만 초기화합니다.
            animator.SetBool(shootParameterName, false);
            animator.SetFloat(speedParameterName, 0f);
            animator.SetInteger(weaponModeParameterName, 0);
        }

        UpdateHeldWeaponObjects();
        UpdateReloadIndicator();
        UpdateAmmoText();

        Debug.Log("플레이어 무기 상태 초기화: 피스톨");
    }

    void UpdateHeldWeaponObjects()
    {
        // Grenade 선택 중에는 마지막 총기의 외형을 그대로 유지합니다.
        // Grenade 선택 중에는 마지막 총기(슬라임 건 포함)의 외형을 그대로 유지합니다.
        WeaponType displayedWeapon =
            currentWeapon == WeaponType.Grenade
                ? lastFirearmWeapon
                : currentWeapon;

        if (slimeGunObject != null)
        {
            slimeGunObject.SetActive(displayedWeapon == WeaponType.SlimeGun);
        }

        if (pistolObject != null)
        {
            pistolObject.SetActive(displayedWeapon == WeaponType.Pistol);
        }

        if (rifleObject != null)
        {
            rifleObject.SetActive(displayedWeapon == WeaponType.Rifle);
        }

        // 수류탄은 손에 드는 무기로 교체하지 않습니다.
        if (grenadeObject != null)
        {
            grenadeObject.SetActive(false);
        }

        if (currentWeapon == WeaponType.Heal)
        {
            if (pistolObject != null)
            {
                pistolObject.SetActive(false);
            }

            if (rifleObject != null)
            {
                rifleObject.SetActive(false);
            }
        }
    }

    void Shoot()
    {
        if (!isWaveCombatActive)
        {
            return;
        }

        if (isDashing)
        {
            return;
        }

        if (Time.time < nextFireTime)
        {
            return;
        }

        switch (currentWeapon)
        {
            case WeaponType.Pistol:
                if (Input.GetMouseButton(0))
                {
                    ShootPistol();
                }
                break;

            case WeaponType.Rifle:
                if (Input.GetMouseButton(0))
                {
                    ShootRifle();
                }
                break;

            case WeaponType.SlimeGun:
                if (Input.GetMouseButtonDown(0))
                {
                    ShootSlimeGun();
                }
                break;

            case WeaponType.Grenade:
                if (Input.GetMouseButtonDown(0))
                {
                    ShootGrenade();
                }
                break;

            case WeaponType.Heal:
                // 현재 기능 없음
                break;
        }
    }

    void ShootPistol()
    {
        if (pistolCooldownTimer > 0f)
        {
            return;
        }

        if (pistolCurrentAmmo <= 0)
        {
            TryStartPistolReload();
            return;
        }

        FireBullet(pistolBulletForwardOffset, pistolDamage, pistolBulletSpeed);
        PlayShootAnimation();
        TriggerTargetingGuidePulse();

        pistolCurrentAmmo--;

        if (pistolCurrentAmmo <= 0)
        {
            TryStartPistolReload();
        }

        nextFireTime = Time.time + 1f / pistolFireRate;
    }

    void ShootRifle()
    {
        if (rifleCooldownTimer > 0f)
        {
            return;
        }

        if (rifleCurrentAmmo <= 0)
        {
            TryStartRifleReload();
            return;
        }

        FireBullet(rifleBulletForwardOffset, rifleDamage, rifleBulletSpeed);
        PlayShootAnimation();
        TriggerTargetingGuidePulse();

        rifleCurrentAmmo--;

        if (rifleCurrentAmmo <= 0)
        {
            TryStartRifleReload();
        }

        nextFireTime = Time.time + 1f / rifleFireRate;
    }

    void TryStartPistolReload()
    {
        if (pistolCooldownTimer > 0f)
        {
            return;
        }

        if (pistolCurrentAmmo >= pistolMaxShotsBeforeCooldown)
        {
            return;
        }

        pistolCooldownTimer = pistolCooldownDuration;
        nextFireTime = Time.time + pistolCooldownDuration;

        Debug.Log("피스톨 리로드 시작");
    }

    void TryStartRifleReload()
    {
        if (rifleCooldownTimer > 0f)
        {
            return;
        }

        if (rifleCurrentAmmo >= rifleMaxShotsBeforeCooldown)
        {
            return;
        }

        rifleCooldownTimer = rifleCooldownDuration;
        nextFireTime = Time.time + rifleCooldownDuration;

        Debug.Log("라이플 리로드 시작");
    }

    // GameControl.csv [PLAYER] 값 적용 (빌드 후에도 수정 가능)
    void ApplyGameControl()
    {
        moveSpeed = GameControl.GetPlayer("player_move_speed", moveSpeed);

        pistolBulletSpeed = GameControl.GetPlayer(
            "pistol_bullet_speed",
            pistolBulletSpeed > 0f ? pistolBulletSpeed : bulletMoveSpeed);
        pistolDamage = Mathf.Max(0, GameControl.GetPlayerInt("pistol_damage", pistolDamage));

        rifleBulletSpeed = GameControl.GetPlayer(
            "rifle_bullet_speed",
            rifleBulletSpeed > 0f ? rifleBulletSpeed : bulletMoveSpeed);
        rifleDamage = Mathf.Max(0, GameControl.GetPlayerInt("rifle_damage", rifleDamage));
    }

    void FireBullet(
        float forwardOffset,
        int damage,
        float speed = 0f)
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("Bullet Prefab이 지정되지 않았습니다.");
            return;
        }

        Vector3 shootDirection = GetAimDirection();

        Vector3 spawnPosition = GetProjectileSpawnPosition(
            shootDirection,
            bulletSpawnHeight,
            forwardOffset
        );

        Quaternion aimRotation = Quaternion.LookRotation(shootDirection, Vector3.up);
        Quaternion bulletRotation = aimRotation * Quaternion.Euler(bulletRotationOffsetEuler);

        GameObject bullet = Instantiate(
            bulletPrefab,
            spawnPosition,
            bulletRotation
        );

        TopDownBullet bulletScript =
            bullet.GetComponent<TopDownBullet>();

        if (bulletScript != null)
        {
            bulletScript.speed = speed > 0f ? speed : bulletMoveSpeed;
            bulletScript.SetDamage(damage);
            bulletScript.SetDirection(shootDirection);
        }
    }

    void ShootGrenade()
    {
        if (grenadeInputLocked)
        {
            return;
        }

        if (grenadePrefab == null)
        {
            Debug.LogWarning("Grenade Prefab이 지정되지 않았습니다.");
            return;
        }

        Vector3 shootDirection = GetAimDirection();

        Vector3 spawnPosition = GetProjectileSpawnPosition(
            shootDirection,
            grenadeSpawnHeight,
            grenadeSpawnForwardOffset
        );

        Vector3 targetPosition = mouseWorldPosition;
        targetPosition.y = transform.position.y;

        HideHeldGrenadeTemporarily();

        GameObject grenade = Instantiate(
            grenadePrefab,
            spawnPosition,
            Quaternion.identity
        );

        TopDownGrenade grenadeScript = grenade.GetComponent<TopDownGrenade>();

        if (grenadeScript != null)
        {
            grenadeScript.Throw(
                targetPosition,
                grenadeArcHeight,
                grenadeFlightDuration,
                grenadeExplosionPrefab,
                grenadeExplosionDelay,
                grenadeDamage,
                grenadeExplosionRadius,
                grenadeDamageColliderDuration
            );
        }

        nextFireTime = Time.time + 1f / grenadeFireRate;
    }

    // 슬라임 건 선택 중: 마우스 위치에 발사 예고 표시
    void UpdateSlimeGunAimIndicator()
    {
        if (slimeGun == null)
        {
            return;
        }

        bool active =
            !isDead &&
            currentWeapon == WeaponType.SlimeGun;

        slimeGun.UpdateAimIndicator(active, transform.position, mouseWorldPosition);
    }

    void OnDisable()
    {
        if (slimeGun != null)
        {
            slimeGun.UpdateAimIndicator(false, transform.position, transform.position);
        }
    }

    void ShootSlimeGun()
    {
        if (grenadeInputLocked)
        {
            return;
        }

        if (slimeGun == null)
        {
            slimeGun = FindFirstObjectByType<SlimeGunController>();
        }

        if (slimeGun == null)
        {
            Debug.LogWarning("SlimeGunController를 찾지 못했습니다.");
            return;
        }

        // 마우스가 가리키는 바닥 위치에 장판 설치
        if (slimeGun.TryFire(transform.position, mouseWorldPosition))
        {
            TriggerTargetingGuidePulse();
            nextFireTime = Time.time + Mathf.Max(0f, slimeGunFireCooldown);
        }
    }

    public void PlayDeathAnimation()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        moveInput = Vector3.zero;

        if (dashCoroutine != null)
        {
            StopCoroutine(dashCoroutine);
            dashCoroutine = null;
        }

        isDashing = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (animator != null)
        {
            animator.SetFloat(speedParameterName, 0f);
            animator.SetBool(shootParameterName, false);
            animator.ResetTrigger(dieParameterName);
            animator.SetTrigger(dieParameterName);
        }

        if (reloadIndicatorRectTransform != null)
        {
            reloadIndicatorRectTransform.gameObject.SetActive(false);

            if (resetReloadRotationWhenDone)
            {
                reloadIndicatorRectTransform.localRotation = Quaternion.identity;
            }
        }
    }

    public void ResetAfterRespawn()
    {
        isDead = false;

        if (walkableInitialized)
        {
            // 리스폰 위치는 기본 지면 높이에서 시작합니다.
            // PlayerHealth가 rb.position으로 리스폰 지점을 지정하므로 그 값을 기준으로 합니다.
            // (transform.position은 물리 갱신 전이라 아직 사망 위치일 수 있음)
            Vector3 p = rb != null ? rb.position : transform.position;
            p.y = baseY;
            transform.position = p;

            if (rb != null)
            {
                rb.position = p;
            }
        }
        moveInput = Vector3.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        if (animator != null)
        {
            // 사망 모션 마지막 프레임에서 기본 상태로 되돌립니다.
            animator.Rebind();
            animator.Update(0f);

            animator.SetBool(shootParameterName, false);
            animator.ResetTrigger(dieParameterName);
            animator.SetFloat(speedParameterName, 0f);

            // 리스폰에서도 현재 무기 상태를 유지합니다.
            // Pistol/Rifle 애니메이션 모드만 현재 상태에 맞게 복원합니다.
            SetWeaponAnimationMode(lastFirearmWeapon);
        }

        UpdateHeldWeaponObjects();
        UpdateReloadIndicator();
        UpdateAmmoText();

        Debug.Log(
            "리스폰 완료 / 무기 상태 유지: " +
            currentWeapon +
            " / 총기 모션: " +
            lastFirearmWeapon
        );
    }

    void UpdateAmmoText()
    {
        if (ammoText == null)
        {
            return;
        }

        switch (currentWeapon)
        {
            case WeaponType.Pistol:
                ammoText.text = string.Format(
                    ammoTextFormat,
                    pistolCurrentAmmo,
                    pistolMaxShotsBeforeCooldown
                );
                break;

            case WeaponType.Rifle:
                ammoText.text = string.Format(
                    ammoTextFormat,
                    rifleCurrentAmmo,
                    rifleMaxShotsBeforeCooldown
                );
                break;

            case WeaponType.SlimeGun:
                ammoText.text = string.Format(
                    slimeGunAmmoFormat,
                    slimeGun != null
                        ? Mathf.RoundToInt(slimeGun.Gauge * 100f)
                        : 0
                );
                break;

            case WeaponType.Grenade:
            case WeaponType.Heal:
                ammoText.text = noAmmoText;
                break;
        }
    }

    Vector3 GetSafeDashTargetPosition(Vector3 startPosition, Vector3 direction, float distance)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            return startPosition;
        }

        direction.Normalize();

        if (Physics.SphereCast(
            startPosition,
            dashCollisionRadius,
            direction,
            out RaycastHit hit,
            distance,
            dashObstacleMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - dashCollisionSkin);

            return startPosition + direction * safeDistance;
        }

        return startPosition + direction * distance;
    }

    Vector3 GetProjectileSpawnPosition(Vector3 direction, float heightOffset, float forwardOffset)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        return transform.position +
               Vector3.up * heightOffset +
               direction * forwardOffset;
    }

    void HideHeldGrenadeTemporarily()
    {
        // Grenade 투척 시에도 현재 총기 외형을 유지합니다.
        // 별도의 held grenade 오브젝트는 사용하지 않습니다.
    }

    IEnumerator ShowHeldGrenadeAfterDelay()
    {
        yield break;
    }

    void HandleHealInput()
    {
        if (!Input.GetKeyDown(healKey))
        {
            return;
        }

        if (weaponIconSelector == null)
        {
            weaponIconSelector = FindObjectOfType<WeaponIconSelector>();
        }

        if (weaponIconSelector == null)
        {
            Debug.LogWarning("WeaponIconSelector를 찾지 못했습니다.");
            return;
        }

        if (!weaponIconSelector.IsHealUnlocked())
        {
            Debug.Log("Heal 아이템이 아직 활성화되지 않았습니다.");
            return;
        }

        PlayerHealth playerHealth = GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth = GetComponentInChildren<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerHealth를 찾지 못했습니다.");
            return;
        }

        playerHealth.HealPercent(healPercent);

        Debug.Log("Heal 아이템 사용: " + (healPercent * 100f) + "% 회복");

        if (consumeHealOnUse)
        {
            // Heal만 소비하고 현재 무기 / 다른 슬롯 / 선택 UI 상태는 유지합니다.
            weaponIconSelector.ConsumeHealSlot();
        }
    }
}