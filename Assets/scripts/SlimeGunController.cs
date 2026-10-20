using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 슬라임 건
/// - 슬라임(일반 몬스터/보스) 처치 시 게이지 충전 (기본 1마리당 25%)
/// - 게이지가 가득 차면 마우스로 지정한 바닥 위치에 슬라임 장판 생성
/// - 장판 위를 지나는 몬스터는 슬로우 (SlimeFloorZone)
/// </summary>
public class SlimeGunController : MonoBehaviour
{
    public static SlimeGunController Instance { get; private set; }

    [Header("Gauge")]
    [Tooltip("슬라임 1마리 처치 시 충전량 (0.25 = 25%)")]
    [Range(0f, 1f)]
    public float gaugePerKill = 0.25f;

    [Tooltip("1회 발사에 필요한 게이지 (1 = 100%)")]
    [Range(0.01f, 1f)]
    public float gaugeCostPerShot = 1f;

    [SerializeField]
    [Range(0f, 1f)]
    private float gauge;

    [Header("Gauge UI")]
    [Tooltip("게이지 Fill Image (Image Type = Filled 권장)")]
    public Image gaugeFillImage;

    [Tooltip("게이지 % 표시용 텍스트 (선택)")]
    public TMP_Text gaugeText;

    [Tooltip("게이지가 가득 찼을 때 Fill Image 깜빡임")]
    public bool blinkWhenFull = true;

    public float fullBlinkSpeed = 3f;

    [Header("Fire Settings")]
    [Tooltip("발사 가능한 최대 거리. 0이면 제한 없음")]
    [Min(0f)]
    public float maxRange = 0f;

    [Tooltip("장판 위치(바닥)를 찾을 레이어. Nothing이면 모든 레이어 검사")]
    public LayerMask groundMask;

    [Tooltip("바닥 탐색을 시작할 높이")]
    public float groundRayStartHeight = 20f;

    [Header("Floor")]
    [Tooltip("장판 프리팹. 비워두면 Floor Sprite로 장판을 자동 생성합니다.")]
    public GameObject floorPrefab;

    [Tooltip("장판 이미지 (Slime gun Floor)")]
    public Sprite floorSprite;

    [Tooltip("장판 크기 (가로 X, 세로 Z, 미터)")]
    public Vector2 floorSize = new Vector2(5f, 2.6f);

    [Tooltip("바닥에 묻히지 않도록 올리는 높이")]
    public float floorHeightOffset = 0.05f;

    [Tooltip("장판 유지 시간 (초)")]
    [Min(0.1f)]
    public float floorLifetime = 5f;

    [Tooltip("사라지기 전 서서히 투명해지는 시간 (초)")]
    [Min(0f)]
    public float floorFadeDuration = 1f;

    [Tooltip("장판 위 몬스터 이동속도 배율 (0.4 = 60% 감속)")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0.4f;

    [Tooltip("장판을 벗어난 뒤에도 슬로우가 유지되는 시간")]
    [Min(0f)]
    public float slowLingerDuration = 0.3f;

    [Header("Aim Indicator (발사 예고)")]
    [Tooltip("슬라임 건 선택 중 마우스 위치에 표시할 예고 이미지 (Slime gun Floor)")]
    public Sprite aimIndicatorSprite;
    [Range(0f, 1f)] public float aimReadyAlpha = 0.7f;
    [Range(0f, 1f)] public float aimNotReadyAlpha = 0.25f;

    [Header("Projectile (파란 구)")]
    [Tooltip("발사체/장판 머티리얼 (Slime gun_floor 텍스처)")]
    public Material slimeMaterial;
    public float projectileSize = 0.7f;
    public float projectileSpeed = 14f;
    [Tooltip("포물선 최고 높이 = 거리 x 이 값 (최소 2m)")]
    public float projectileArcRatio = 0.35f;
    public float projectileLaunchHeight = 1.2f;

    [Header("Floor Model")]
    [Tooltip("착지 시 생성할 장판 3D 모델 (Slime gun_floor.fbx). 비우면 Floor Sprite 사용")]
    public GameObject floorModelPrefab;

    private SpriteRenderer aimIndicator;

    [Header("Debug (임시 테스트용)")]
    [Tooltip("Full Weapon / Reset 버튼 아래에 '게이지 100%' 테스트 버튼을 자동 생성")]
    public bool createDebugFillButton = true;

    private Color gaugeOriginalColor = Color.white;

    public float Gauge => gauge;
    public bool IsReady => gauge + 0.0001f >= gaugeCostPerShot;

    void Awake()
    {
        Instance = this;
        ApplyGameControl();

        if (gaugeFillImage != null)
        {
            gaugeOriginalColor = gaugeFillImage.color;
        }

        UpdateGaugeUI();
    }

    // GameControl.csv [PLAYER] 슬라임 건 값 적용
    void ApplyGameControl()
    {
        float currentKills = gaugePerKill > 0f ? 1f / gaugePerKill : 4f;
        float kills = GameControl.GetPlayer("slime_gun_kills", currentKills);
        if (kills > 0f)
        {
            gaugePerKill = Mathf.Clamp01(1f / kills);
        }

        // 범위 = 장판 가로 길이(m). 세로는 기존 비율 유지
        float range = GameControl.GetPlayer("slime_gun_range", floorSize.x);
        if (range > 0f && floorSize.x > 0f)
        {
            floorSize = new Vector2(range, range * (floorSize.y / floorSize.x));
        }

        // 느려지는 정도(%) : 60 = 이동속도 60% 감소
        float slowPercent = GameControl.GetPlayer("slime_gun_slow_percent", (1f - slowMultiplier) * 100f);
        slowMultiplier = Mathf.Clamp01(1f - slowPercent / 100f);
    }

    void Start()
    {
        if (createDebugFillButton)
        {
            CreateDebugFillButton();
        }
    }

    /// <summary>
    /// 테스트용: 게이지를 즉시 100%로 채웁니다.
    /// </summary>
    public void FillGaugeForTest()
    {
        gauge = 1f;
        UpdateGaugeUI();
        Debug.Log("[SlimeGun] 테스트: 게이지 100%");
    }

    void CreateDebugFillButton()
    {
        // 기존 디버그 버튼(Full Weapon / Reset)을 복제해서 같은 모양으로 만듭니다.
        GameObject template = FindSceneObject("AllweaponButton");
        GameObject resetButton = FindSceneObject("ResetButton");

        if (template == null)
        {
            Debug.LogWarning("[SlimeGun] AllweaponButton을 찾지 못해 테스트 버튼을 만들지 않았습니다.");
            return;
        }

        GameObject copy = Instantiate(template, template.transform.parent);
        copy.name = "SlimeGaugeFullButton";

        RectTransform copyRect = copy.GetComponent<RectTransform>();
        RectTransform templateRect = template.GetComponent<RectTransform>();
        RectTransform resetRect = resetButton != null ? resetButton.GetComponent<RectTransform>() : null;

        if (copyRect != null && templateRect != null)
        {
            Vector2 step = resetRect != null
                ? resetRect.anchoredPosition - templateRect.anchoredPosition
                : new Vector2(0f, -40f);

            Vector2 basePos = resetRect != null ? resetRect.anchoredPosition : templateRect.anchoredPosition;
            copyRect.anchoredPosition = basePos + step;
        }

        Button button = copy.GetComponent<Button>();
        if (button == null)
        {
            button = copy.AddComponent<Button>();
        }

        // 복제된 기존 클릭 이벤트(Full Weapon 기능)를 제거하고 게이지 채우기만 연결
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(FillGaugeForTest);

        TMP_Text tmp = copy.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = "Full Slime";
        }
        else
        {
            Text legacy = copy.GetComponentInChildren<Text>(true);
            if (legacy != null)
            {
                legacy.text = "Full Slime";
            }
        }

        copy.SetActive(true);
    }

    static GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj != null && obj.scene.IsValid() && obj.name == objectName)
            {
                return obj;
            }
        }

        return null;
    }

    void OnDestroy()
    {
        if (aimIndicator != null)
        {
            Destroy(aimIndicator.gameObject);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Tooltip("WeaponIconSelector에서 슬라임 건 슬롯 인덱스 (0부터)")]
    public int slimeGunSlotIndex = 2;

    void Update()
    {
        UpdateGaugeVisibility();
        UpdateFullBlink();
    }

    // 슬라임 건을 획득한 뒤부터 게이지 UI 표시
    void UpdateGaugeVisibility()
    {
        if (gaugeFillImage == null)
        {
            return;
        }

        Transform gaugeRoot = gaugeFillImage.transform.parent != null
            ? gaugeFillImage.transform.parent
            : gaugeFillImage.transform;

        bool unlocked = WeaponIconSelector.Instance != null &&
                        WeaponIconSelector.Instance.IsUnlocked(slimeGunSlotIndex);

        if (gaugeRoot.gameObject.activeSelf != unlocked)
        {
            gaugeRoot.gameObject.SetActive(unlocked);
        }
    }

    /// <summary>
    /// 몬스터(슬라임/보스)가 실제로 처치됐을 때 호출됩니다.
    /// </summary>
    public static void NotifyMonsterKilled()
    {
        if (Instance != null)
        {
            Instance.AddGauge(Instance.gaugePerKill);
        }
    }

    public void AddGauge(float amount)
    {
        gauge = Mathf.Clamp01(gauge + amount);
        UpdateGaugeUI();
    }

    public void ResetGauge()
    {
        gauge = 0f;
        UpdateGaugeUI();
    }

    /// <summary>
    /// 마우스로 지정한 위치에 장판 발사. 게이지가 부족하면 false.
    /// </summary>
    public bool TryFire(Vector3 shooterPosition, Vector3 targetPosition)
    {
        if (!IsReady)
        {
            Debug.Log(
                "슬라임 건 게이지 부족: " +
                Mathf.RoundToInt(gauge * 100f) + "%"
            );
            return false;
        }

        Vector3 target = targetPosition;

        if (maxRange > 0f)
        {
            Vector3 offset = target - shooterPosition;
            offset.y = 0f;

            if (offset.magnitude > maxRange)
            {
                target = shooterPosition + offset.normalized * maxRange;
            }
        }

        Vector3 groundPoint = FindGroundPoint(target, shooterPosition.y);

        // 플레이어에게서 파란 구가 포물선으로 날아가 착지 지점에 장판 생성
        StartCoroutine(ProjectileRoutine(shooterPosition + Vector3.up * projectileLaunchHeight, groundPoint));

        gauge = Mathf.Clamp01(gauge - gaugeCostPerShot);
        UpdateGaugeUI();

        return true;
    }

    Vector3 FindGroundPoint(Vector3 horizontalPosition, float fallbackY)
    {
        Vector3 origin = horizontalPosition;
        origin.y = fallbackY + Mathf.Max(0.5f, groundRayStartHeight);

        int mask = groundMask.value == 0
            ? Physics.DefaultRaycastLayers
            : groundMask.value;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRayStartHeight * 2f + 10f,
            mask,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            // 플레이어/몬스터 몸체는 바닥으로 보지 않습니다.
            if (hit.collider.GetComponentInParent<TopDownPlayerController>() != null ||
                hit.collider.GetComponentInParent<MonsterChaser>() != null ||
                hit.collider.GetComponentInParent<BossMonsterChaser>() != null)
            {
                continue;
            }

            return hit.point;
        }

        horizontalPosition.y = fallbackY;
        return horizontalPosition;
    }

    // ---------------- 발사 예고 ----------------

    public void UpdateAimIndicator(bool active, Vector3 shooterPosition, Vector3 mousePosition)
    {
        if (!active || aimIndicatorSprite == null)
        {
            if (aimIndicator != null && aimIndicator.gameObject.activeSelf)
            {
                aimIndicator.gameObject.SetActive(false);
            }
            return;
        }

        if (aimIndicator == null)
        {
            GameObject go = new GameObject("SlimeGun AimIndicator");
            aimIndicator = go.AddComponent<SpriteRenderer>();
            aimIndicator.sprite = aimIndicatorSprite;
            aimIndicator.sortingOrder = 10;
            aimIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            aimIndicator.receiveShadows = false;
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Vector2 s = aimIndicatorSprite.bounds.size;
            go.transform.localScale = new Vector3(
                s.x > 0f ? floorSize.x / s.x : 1f,
                s.y > 0f ? floorSize.y / s.y : 1f,
                1f);
        }

        if (!aimIndicator.gameObject.activeSelf)
        {
            aimIndicator.gameObject.SetActive(true);
        }

        Vector3 target = ClampToRange(shooterPosition, mousePosition);
        Vector3 ground = FindGroundPoint(target, shooterPosition.y);
        aimIndicator.transform.position = ground + Vector3.up * (floorHeightOffset + 0.02f);

        float a = IsReady
            ? aimReadyAlpha * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f))
            : aimNotReadyAlpha;

        Color c = Color.white;
        c.a = a;
        aimIndicator.color = c;
    }

    Vector3 ClampToRange(Vector3 shooterPosition, Vector3 target)
    {
        if (maxRange > 0f)
        {
            Vector3 offset = target - shooterPosition;
            offset.y = 0f;

            if (offset.magnitude > maxRange)
            {
                return shooterPosition + offset.normalized * maxRange;
            }
        }

        return target;
    }

    // ---------------- 발사체 ----------------

    System.Collections.IEnumerator ProjectileRoutine(Vector3 start, Vector3 ground)
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "SlimeGun Projectile";
        Destroy(ball.GetComponent<Collider>());
        ball.transform.localScale = Vector3.one * projectileSize;

        Renderer r = ball.GetComponent<Renderer>();
        if (slimeMaterial != null)
        {
            r.sharedMaterial = slimeMaterial;
        }
        else
        {
            r.material.color = new Color(0.2f, 0.5f, 1f);
        }

        Vector3 end = ground + Vector3.up * (projectileSize * 0.5f);
        Vector3 flat = end - start;
        flat.y = 0f;

        float distance = flat.magnitude;
        float duration = Mathf.Clamp(distance / Mathf.Max(0.1f, projectileSpeed), 0.25f, 2f);
        float arc = Mathf.Max(2f, distance * projectileArcRatio);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            Vector3 p = Vector3.Lerp(start, end, k);
            p.y += arc * 4f * k * (1f - k);
            ball.transform.position = p;

            // 날아가며 살짝 늘어나는 젤리 느낌
            float stretch = 1f + 0.2f * Mathf.Sin(k * Mathf.PI);
            ball.transform.localScale = new Vector3(projectileSize / stretch, projectileSize * stretch, projectileSize / stretch);

            yield return null;
        }

        Destroy(ball);
        SpawnFloor(ground);
    }

    void SpawnFloor(Vector3 groundPoint)
    {
        Vector3 position = groundPoint + Vector3.up * floorHeightOffset;
        GameObject floor;

        if (floorPrefab != null)
        {
            floor = Instantiate(floorPrefab, position, Quaternion.identity);
        }
        else if (floorModelPrefab != null)
        {
            floor = new GameObject("SlimeGun Floor");
            floor.transform.position = position;

            GameObject model = Instantiate(floorModelPrefab, floor.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            foreach (Collider col in model.GetComponentsInChildren<Collider>())
            {
                Destroy(col);
            }

            bool has = false;
            Bounds b = new Bounds(position, Vector3.zero);

            foreach (Renderer mr in model.GetComponentsInChildren<Renderer>())
            {
                if (slimeMaterial != null)
                {
                    Material[] mats = mr.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = slimeMaterial;
                    mr.sharedMaterials = mats;
                }

                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                if (!has) { b = mr.bounds; has = true; }
                else b.Encapsulate(mr.bounds);
            }

            // 모델 가로/세로를 장판 크기(floorSize)에 맞춤, 높이는 가로 비율 따라감
            if (has && b.size.x > 0.001f && b.size.z > 0.001f)
            {
                Vector3 ls = model.transform.localScale;
                float sx = floorSize.x / b.size.x;
                float sz = floorSize.y / b.size.z;
                model.transform.localScale = new Vector3(ls.x * sx, ls.y * Mathf.Min(sx, sz), ls.z * sz);

                // 바닥에 붙도록 모델 아래면을 지면에 맞춤
                Bounds nb = new Bounds();
                bool first = true;
                foreach (Renderer mr in model.GetComponentsInChildren<Renderer>())
                {
                    if (first) { nb = mr.bounds; first = false; } else nb.Encapsulate(mr.bounds);
                }
                model.transform.position += Vector3.up * (position.y - nb.min.y);
            }
        }
        else
        {
            floor = new GameObject("SlimeGun Floor");
            floor.transform.position = position;

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(floor.transform, false);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = floorSprite;

            if (floorSprite != null)
            {
                Vector2 spriteSize = floorSprite.bounds.size;

                visual.transform.localScale = new Vector3(
                    spriteSize.x > 0f ? floorSize.x / spriteSize.x : 1f,
                    spriteSize.y > 0f ? floorSize.y / spriteSize.y : 1f,
                    1f
                );
            }
            else
            {
                Debug.LogWarning("슬라임 건 Floor Sprite가 지정되지 않았습니다.", this);
            }
        }

        SlimeFloorZone zone = floor.GetComponent<SlimeFloorZone>();

        if (zone == null)
        {
            zone = floor.AddComponent<SlimeFloorZone>();
        }

        zone.Initialize(
            floorSize,
            slowMultiplier,
            slowLingerDuration,
            floorLifetime,
            floorFadeDuration
        );
    }

    void UpdateGaugeUI()
    {
        if (gaugeFillImage != null)
        {
            gaugeFillImage.fillAmount = gauge;

            if (!IsReady)
            {
                gaugeFillImage.color = gaugeOriginalColor;
            }
        }

        if (gaugeText != null)
        {
            gaugeText.text = Mathf.RoundToInt(gauge * 100f) + "%";
        }
    }

    void UpdateFullBlink()
    {
        if (!blinkWhenFull || gaugeFillImage == null || !IsReady)
        {
            return;
        }

        float t = Mathf.PingPong(Time.unscaledTime * fullBlinkSpeed, 1f);

        // 가득 찼을 때 밝기를 깜빡여 발사 가능함을 알려줍니다.
        Color c = gaugeOriginalColor;
        c.a = Mathf.Lerp(0.45f, 1f, t);
        gaugeFillImage.color = c;
    }
}
