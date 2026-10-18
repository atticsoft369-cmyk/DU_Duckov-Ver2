using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public class BossMonsterChaser : MonoBehaviour
{
    private enum TargetMode
    {
        Dam,
        Player
    }

    private enum BossAction
    {
        None,
        Attack,
        Dash,
        Jump,
        Poison,
        FloorAttack,
        SphereShot,
        Hit,
        Dead
    }

    [Header("Boss CSV Settings")]
    [Tooltip("Assets/Resources 폴더의 몬스터 정보 CSV 파일명. 확장자는 입력하지 않습니다.")]
    public string monsterInfoTableFileName = "Mob_Info";

    [Tooltip("보스는 Mob_Info.csv의 이 mob 행을 고정으로 사용합니다.")]
    public string bossCsvMobName = "Slime_King";

    [Tooltip("CSV 로드 실패 시 기존 Inspector 값을 사용합니다.")]
    public bool useInspectorValuesAsFallback = true;

    [Header("Boss Damage Multipliers")]
    [Tooltip("기본 공격 = CSV atk x 1")]
    public float normalAttackMultiplier = 1f;

    [Tooltip("점프 공격 = CSV atk x 5")]
    public float jumpAttackMultiplier = 5f;

    [Tooltip("포이즌 공격 = CSV atk x 1.5")]
    public float poisonAttackMultiplier = 1.5f;

    [Tooltip("대쉬 공격 = CSV atk x 2.5")]
    public float dashAttackMultiplier = 2.5f;

    [Header("Target")]
    public Transform player;

    [Header("King Slime Mode")]
    [Tooltip("켜면 대쉬/점프/포이즌 대신 킹 슬라임 스킬(FLOOR / 구체 발사)만 사용합니다.")]
    public bool useKingSkills = true;

    [Tooltip("생성 직후 플레이어를 기본 타겟으로 삼습니다. (플레이어 사망 시 댐 공격 → 플레이어가 공격하면 다시 플레이어)")]
    public bool startTargetPlayer = true;

    [Tooltip("새 모델(자식)의 Animator. 비워두면 기존 방식으로 찾습니다.")]
    public Animator modelAnimator;

    [Range(0f, 100f)] public float kingFloorChance = 50f;
    [Range(0f, 100f)] public float kingShotChance = 50f;

    public string floorParameterName = "Floor";
    public string shotParameterName = "Shot";

    [Header("King Skill 1 : FLOOR (구체 장판)")]
    [Tooltip("떨어지는 구체 프리팹 (King_FloorSphere). 비우면 기본 구체로 대체")]
    public GameObject kingSpherePrefab;

    [Tooltip("장판 이미지 (Boss Floor)")]
    public Sprite floorZoneSprite;

    [Tooltip("공격 모션 시작 후 구체가 떨어지기 시작할 때까지 시간")]
    public float floorCastTime = 0.6f;

    public int floorCount = 1;

    [Tooltip("장판 위치를 플레이어 주변으로 흩뿌리는 반경 (floorCount가 2 이상일 때)")]
    public float floorTargetSpread = 3f;

    public float floorDropHeight = 12f;
    public float floorFallDuration = 0.7f;
    public float floorRadius = 3f;
    public float floorLifetime = 5f;
    public float floorTickInterval = 0.5f;

    [Tooltip("장판 위에 있을 때 틱당 피해 = CSV atk x 배율")]
    public float floorDamageMultiplier = 1f;

    public float floorSphereScale = 1f;
    public float floorRecoveryTime = 0.5f;

    [Header("King Skill 2 : 세로형 구체 발사")]
    [Tooltip("경고 이펙트 이미지 (Attack effect)")]
    public Sprite shotWarningSprite;

    public int shotCount = 2;

    [Tooltip("구체 사이 발사 간격(초)")]
    public float shotInterval = 0.35f;

    [Tooltip("0이면 같은 라인으로 연속 발사, 0보다 크면 부채꼴로 라인을 나눔")]
    public float shotSpreadAngle = 0f;

    public float shotWarningDuration = 1f;
    public float shotLength = 18f;
    public float shotWidth = 1.6f;
    public float shotSpeed = 16f;
    public float shotSphereScale = 0.8f;
    public float shotHitRadius = 1.1f;
    public float shotSphereHeight = 0.8f;

    [Tooltip("구체 적중 피해 = CSV atk x 배율")]
    public float shotDamageMultiplier = 2f;

    public float shotRecoveryTime = 0.5f;

    [Tooltip("0보다 크면 FLOOR 틱당 피해를 이 값으로 고정 (GameControl.csv atk1)")]
    public int floorDamageOverride = 0;

    [Tooltip("0보다 크면 구체 발사 피해를 이 값으로 고정 (GameControl.csv atk2)")]
    public int shotDamageOverride = 0;

    [Header("Target Settings")]
    public float playerAggroRange = 30f;


    [Tooltip("플레이어에게 맞은 뒤 계속 플레이어를 추적할지 여부")]
    public bool keepTargetingPlayerAfterHit = true;

    [Header("Bullet Hit Settings")]
    [Tooltip("총알 스크립트에서 TakeDamage를 호출하지 못하는 경우 보스가 충돌을 직접 처리")]
    public bool receiveDamageFromBulletCollision = true;

    [Tooltip("플레이어 총알에 설정된 태그")]
    public string playerBulletTag = "Bullet";

    [Tooltip("총알에서 피해량을 읽지 못했을 때 적용할 기본 피해량")]
    public int fallbackBulletDamage = 1;

    [Tooltip("보스가 직접 피격 처리한 총알을 제거할지 여부")]
    public bool destroyBulletOnHit = true;

    [Header("HP Settings")]
    public int maxHp = 30;

    [SerializeField]
    private int currentHp;

    [Header("Boss HP Bar Settings")]
    [Tooltip("씬 Canvas에 고정 배치된 보스 HP바 전체 루트 오브젝트 이름")]
    public string fixedBossHpRootName = "Boss HPbar";

    [Tooltip("고정 보스 HP바의 Fill Image 오브젝트 이름")]
    public string fixedBossHpFillName = "Bosshp_hp";

    [Tooltip("보스가 생성되면 고정 HP바를 자동으로 표시")]
    public bool showBossHpOnSpawn = true;

    [Header("Move Settings")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 10f;

    [Header("Normal Attack Settings")]
    public int attackDamage = 2;
    public float attackRange = 2.5f;

    [Tooltip("콜라이더끼리 이 간격 이내로 붙어야 일반 공격을 시작합니다 (몸 표면 사이 거리, m)")]
    public float attackContactGap = 0.3f;
    public float attackInterval = 1.5f;

    [Tooltip("공격 모션 시작 후 실제 피해가 들어가기까지의 시간")]
    public float attackHitDelay = 0.35f;

    [Tooltip("공격 모션 전체 시간")]
    public float attackAnimationDuration = 0.8f;

    [Header("Dash Settings")]
    public float dashTriggerDistance = 6f;
    public float dashCooldown = 5f;

    [Tooltip("가이드가 표시되는 최대 거리이자, 캐스팅 종료 시 실제로 돌진하는 최대 거리")]
    public float dashDistance = 5f;

    [Tooltip("돌진 이동 속도 (초당 이동 거리). 값이 클수록 더 빠르게 돌진한다.")]
    public float dashMoveSpeed = 30f;

    [Tooltip("돌진 이동에 걸리는 최소 시간(너무 짧은 거리에서도 순간이동처럼 보이지 않도록 하는 안전장치)")]
    public float dashMinDuration = 0.05f;

    [Tooltip("대시로 플레이어를 맞췄을 때 줄 큰 피해량")]
    public int dashDamage = 2;

    [Tooltip("대시 이동 중 플레이어와의 충돌 판정 범위")]
    public float dashHitRange = 2.5f;

    [Tooltip("대시 준비(조준) 캐스팅 시간. 이 시간 동안 가이드가 표시되고 플레이어를 따라간다.")]
    public float dashCastDuration = 2f;

    [Tooltip("가이드 세그먼트 프리팹 A (dashguide1)")]
    public GameObject dashGuideSegmentPrefabA;

    [Tooltip("가이드 세그먼트 프리팹 B (dashguide2), A와 번갈아 배치된다")]
    public GameObject dashGuideSegmentPrefabB;

    [Tooltip("가이드 세그먼트 사이의 간격")]
    public float dashGuideSegmentSpacing = 1f;

    [Tooltip("가이드 세그먼트가 바닥에 파묻히지 않도록 올리는 높이")]
    public float dashGuideHeightOffset = 0.03f;

    [Tooltip("대시 중 충돌할 장애물 레이어")]
    public LayerMask dashObstacleMask;

    public float dashCollisionRadius = 0.5f;
    public float dashCollisionSkin = 0.05f;

    [Header("Boss Skill Decision")]
    [Tooltip("몇 초마다 대쉬/점프/포이즌 중 하나를 판정할지 설정")]
    public float skillDecisionInterval = 5f;

    [Range(0f, 100f)]
    public float dashSelectionChance = 0f;

    [Range(0f, 100f)]
    public float jumpSelectionChance = 100f;

    [Range(0f, 100f)]
    public float poisonSelectionChance = 0f;

    [Header("Jump Settings")]
    [Tooltip("보스가 위로 올라가는 높이")]
    public float jumpHeight = 12f;

    [Tooltip("보스가 위로 빠르게 올라가는 시간")]
    public float jumpRiseDuration = 0.35f;

    [Tooltip("점프 시작 후 타겟 마크가 생길 때까지의 총 시간")]
    public float jumpTargetMarkDelay = 1f;

    [Tooltip("타겟 마크 생성 후 착지까지 기다리는 시간")]
    public float jumpWarningDuration = 2f;

    [Tooltip("공중에서 타겟 위치로 떨어지는 시간")]
    public float jumpFallDuration = 0.25f;

    [Tooltip("착지 후 다시 행동하기까지의 시간")]
    public float jumpRecoveryDuration = 1f;

    public int jumpDamage = 3;
    public float jumpDamageRadius = 3f;

    [Tooltip("바닥에 표시할 Target 스프라이트 프리팹")]
    public GameObject jumpTargetPrefab;

    [Tooltip("타겟 마크가 바닥에 묻히지 않도록 올리는 높이")]
    public float targetMarkHeightOffset = 0.03f;

    [Tooltip("플레이어 바닥 위치를 찾을 레이어. 0이면 모든 레이어를 검사")]
    public LayerMask groundMask;

    [Tooltip("바닥 탐색을 시작할 높이")]
    public float groundRayStartHeight = 10f;

    [Tooltip("바닥 탐색 거리")]
    public float groundRayDistance = 30f;

    [Tooltip("착지 시 강제로 적용할 바닥 Y좌표. 이 값이 항상 우선 적용된다.")]
    public float groundLevelY = 0.2f;

    [Header("Jump Cast Bar Settings")]
    [Tooltip("보스 소환 시 자동으로 생성되어 붙는 캐스트바 UI 프리팹")]
    public GameObject jumpCastBarPrefab;

    [Tooltip("캐스트바를 생성할 부모 Canvas. 비워두면 씬에서 자동으로 Canvas를 찾는다.")]
    public Canvas jumpCastBarCanvas;

    [Tooltip("캐스트바 프리팹 안에서 진행도 Image를 찾을 자식 오브젝트 이름")]
    public string jumpCastBarFillImageName = "hp_hp";

    [Tooltip("캐스트바가 0%에서 100%까지 차오르는데 걸리는 시간")]
    public float jumpCastDuration = 1f;

    [Tooltip("캐스트바를 보스 머리 위 몇 유닛 높이에 표시할지")]
    public float jumpCastBarHeightOffset = 3f;

    [Tooltip("캐스트바를 화면 좌표로 변환할 카메라. 비워두면 Camera.main 사용")]
    public Camera jumpCastBarCamera;

    [Tooltip("착지 시 실행할 카메라 흔들림 컴포넌트")]
    public SimpleCameraShake cameraShake;

    public float landingShakeDuration = 0.25f;
    public float landingShakeStrength = 0.25f;

    [Header("Poison Settings")]
    public float poisonCooldown = 10f;

    [Tooltip("포이즌 캐스팅 시간")]
    public float poisonCastDuration = 1f;

    [Tooltip("포이즌 표적이 생성되는 범위 (보스 기준 반경, 미터)")]
    public float poisonTargetRadius = 30f;

    [Tooltip("생성할 포이즌 표적 개수")]
    public int poisonTargetCount = 5;

    [Tooltip("바닥에 표시할 포이즌 표적 마커 프리팹")]
    public GameObject poisonTargetMarkerPrefab;

    [Tooltip("표적 마커가 바닥에 파묻히지 않도록 올리는 높이")]
    public float poisonTargetMarkerHeightOffset = 0.03f;

    [Tooltip("포이즌 오브젝트(장판)가 떨어질 때 생성할 프리팹")]
    public GameObject poisonImpactPrefab;

    [Tooltip("포이즌 오브젝트가 보스 좌표에서 솟아올라 도달하는 높이")]
    public float poisonLaunchHeight = 15f;

    [Tooltip("포이즌 오브젝트가 목표 상공까지 솟아오르는데 걸리는 시간")]
    public float poisonRiseDuration = 0.2f;

    [Tooltip("목표 상공에서 머무는 시간. 상승 시간과 합쳐서 대략 1초가 되도록 기본값이 설정됨")]
    public float poisonHoverDuration = 0.8f;

    [Tooltip("목표 상공에서 지면으로 빠르게 떨어지는데 걸리는 시간")]
    public float poisonFallDuration = 0.2f;

    [Tooltip("첫 포이즌이 떨어지고 나서 마지막 포이즌이 떨어지기까지 걸리는 총 시간 (이 시간 안에 순차적으로 떨어짐)")]
    public float poisonImpactSpreadDuration = 2f;

    [Tooltip("포이즌 오브젝트가 땅에 떨어진 뒤 유지되는 시간 (이 시간 동안 닿으면 피해)")]
    public float poisonImpactLifetime = 1f;

    [Tooltip("포이즌 오브젝트에 닿았을 때 줄 피해량")]
    public int poisonImpactDamage = 3;

    [Tooltip("포이즌 오브젝트의 피해 판정 반경")]
    public float poisonImpactRadius = 1.5f;

    [Header("Hit Settings")]
    public float hitStopDuration = 0.3f;

    [Header("Animation Parameters")]
    public string speedParameterName = "Speed";
    public string attackParameterName = "Attack";
    public string hitParameterName = "Hit";
    public string dieParameterName = "Die";
    public string dashParameterName = "Dash";
    public string jumpStartParameterName = "JumpStart";
    public string jumpEndParameterName = "JumpEnd";
    public string poisonParameterName = "Poison";

    [Header("Death Settings")]
    public float deathAnimationDuration = 2f;
    public GameObject deathEffectPrefab;
    public float deathEffectDestroyDelay = 2f;
    public float deathEffectHeightOffset = 0.5f;

    private TargetMode targetMode = TargetMode.Dam;
    private BossAction currentAction = BossAction.None;

    private DamPart currentDamPart;
    private Collider currentDamCollider;
    private Collider playerCollider;
    private Collider bossCollider;

    private Rigidbody rb;
    private Animator animator;
    private WaveMobTracker waveMobTracker;

    private int csvBaseAttackDamage = 1;

    private float attackTimer;
    private float dashTimer;
    private float jumpTimer;
    private float poisonTimer;
    private float skillDecisionTimer;

    private bool isDead;
    private Coroutine currentActionCoroutine;
    private float bossPivotToFootOffset;
    private GameObject jumpCastBarInstance;
    private RectTransform jumpCastBarRectTransform;
    private Image jumpCastBarFillImage;

    private GameObject fixedBossHpRoot;
    private Image fixedBossHpFillImage;

    private readonly List<GameObject> dashGuideSegmentPool = new List<GameObject>();
    private readonly List<Coroutine> poisonImpactCoroutines = new List<Coroutine>();
    private readonly List<GameObject> poisonSpawnedObjects = new List<GameObject>();
    private readonly HashSet<int> processedBulletIds = new HashSet<int>();
    private readonly List<GameObject> kingSpawnedObjects = new List<GameObject>();
    private readonly List<GameObject> kingWarningObjects = new List<GameObject>();
    private PlayerHealth cachedPlayerHealth;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        waveMobTracker = GetComponent<WaveMobTracker>();

        animator = modelAnimator != null
            ? modelAnimator
            : GetComponent<Animator>();

        if (animator == null || !animator.enabled)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning(name + ": Animator를 찾지 못했습니다.");
        }

        ApplyBossStatsFromCsv();
        ApplyGameControlStats();

        currentHp = maxHp;
        skillDecisionTimer = skillDecisionInterval;

        SetupRigidbody();
        FindPlayer();
        CachePlayerCollider();
        CacheBossCollider();
        CacheBossFootOffset();
        FindCameraShake();
        SpawnJumpCastBar();
        SetupFixedBossHpBar();
        SetTargetToDam();

        if (startTargetPlayer && !IsPlayerDead())
        {
            SetTargetToPlayer();
        }
    }

    void OnDestroy()
    {
        HideFixedBossHpBar();

        if (jumpCastBarInstance != null)
        {
            Destroy(jumpCastBarInstance);
            jumpCastBarInstance = null;
        }

        for (int i = 0; i < dashGuideSegmentPool.Count; i++)
        {
            if (dashGuideSegmentPool[i] != null)
            {
                Destroy(dashGuideSegmentPool[i]);
            }
        }

        dashGuideSegmentPool.Clear();

        for (int i = 0; i < poisonSpawnedObjects.Count; i++)
        {
            if (poisonSpawnedObjects[i] != null)
            {
                Destroy(poisonSpawnedObjects[i]);
            }
        }

        poisonSpawnedObjects.Clear();
        poisonImpactCoroutines.Clear();

        ClearKingWarnings();

        for (int i = 0; i < kingSpawnedObjects.Count; i++)
        {
            if (kingSpawnedObjects[i] != null)
            {
                Destroy(kingSpawnedObjects[i]);
            }
        }

        kingSpawnedObjects.Clear();
    }

    void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateCooldowns();
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        if (currentAction != BossAction.None)
        {
            StopMove();
            return;
        }

        UpdateTargetState();

        if (targetMode == TargetMode.Player)
        {
            ChasePlayer();
        }
        else
        {
            ChaseDam();
        }
    }

    void ApplyBossStatsFromCsv()
    {
        TextAsset csvFile =
            Resources.Load<TextAsset>(
                monsterInfoTableFileName
            );

        if (csvFile == null)
        {
            HandleBossCsvLoadFailure(
                "보스 정보 CSV를 찾지 못했습니다: Assets/Resources/" +
                monsterInfoTableFileName +
                ".csv"
            );
            return;
        }

        string[] lines =
            csvFile.text.Split('\n');

        if (lines.Length <= 1)
        {
            HandleBossCsvLoadFailure(
                monsterInfoTableFileName +
                ".csv에 데이터가 없습니다."
            );
            return;
        }

        string[] headers =
            lines[0].Split(',');

        int mobIndex =
            FindCsvColumnIndex(headers, "mob");
        int hpIndex =
            FindCsvColumnIndex(headers, "hp");
        int atkIndex =
            FindCsvColumnIndex(headers, "atk");
        int speedIndex =
            FindCsvColumnIndex(headers, "speed");

        if (mobIndex < 0 ||
            hpIndex < 0 ||
            atkIndex < 0 ||
            speedIndex < 0)
        {
            HandleBossCsvLoadFailure(
                monsterInfoTableFileName +
                ".csv 헤더에 mob, hp, atk, speed 컬럼이 필요합니다."
            );
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            string line =
                lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] columns =
                line.Split(',');

            string mobName =
                GetCsvString(
                    columns,
                    mobIndex
                );

            if (!string.Equals(
                    mobName,
                    bossCsvMobName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int csvHp =
                GetCsvInt(
                    columns,
                    hpIndex,
                    maxHp
                );

            int csvAtk =
                GetCsvInt(
                    columns,
                    atkIndex,
                    attackDamage
                );

            float csvSpeed =
                GetCsvFloat(
                    columns,
                    speedIndex,
                    moveSpeed
                );

            maxHp =
                Mathf.Max(1, csvHp);

            csvBaseAttackDamage =
                Mathf.Max(0, csvAtk);

            moveSpeed =
                Mathf.Max(0f, csvSpeed);

            ApplyBossDamageMultipliers();

            Debug.Log(
                name +
                ": CSV 보스 스탯 적용 [" +
                bossCsvMobName +
                "] / HP=" +
                maxHp +
                ", Base ATK=" +
                csvBaseAttackDamage +
                ", Speed=" +
                moveSpeed +
                ", Normal=" +
                attackDamage +
                ", Jump=" +
                jumpDamage +
                ", Poison=" +
                poisonImpactDamage +
                ", Dash=" +
                dashDamage,
                this
            );

            return;
        }

        HandleBossCsvLoadFailure(
            monsterInfoTableFileName +
            ".csv에서 보스 mob '" +
            bossCsvMobName +
            "'을 찾지 못했습니다."
        );
    }

    void ApplyBossDamageMultipliers()
    {
        attackDamage =
            CalculateBossDamage(
                normalAttackMultiplier
            );

        jumpDamage =
            CalculateBossDamage(
                jumpAttackMultiplier
            );

        poisonImpactDamage =
            CalculateBossDamage(
                poisonAttackMultiplier
            );

        dashDamage =
            CalculateBossDamage(
                dashAttackMultiplier
            );
    }

    int CalculateBossDamage(float multiplier)
    {
        if (csvBaseAttackDamage <= 0 ||
            multiplier <= 0f)
        {
            return 0;
        }

        // PlayerHealth가 int 피해를 사용하므로 소수 결과는 가장 가까운 정수로 반올림합니다.
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                csvBaseAttackDamage *
                multiplier
            )
        );
    }

    void HandleBossCsvLoadFailure(string message)
    {
        if (useInspectorValuesAsFallback)
        {
            csvBaseAttackDamage =
                Mathf.Max(0, attackDamage);

            Debug.LogWarning(
                message +
                " / Inspector 값을 유지합니다.",
                this
            );

            return;
        }

        Debug.LogError(
            message,
            this
        );
    }

    static int FindCsvColumnIndex(
        string[] headers,
        string columnName)
    {
        if (headers == null)
        {
            return -1;
        }

        for (int i = 0; i < headers.Length; i++)
        {
            string header =
                headers[i]
                    .Trim()
                    .Trim('\uFEFF');

            if (string.Equals(
                    header,
                    columnName,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    static string GetCsvString(
        string[] columns,
        int index)
    {
        if (columns == null ||
            index < 0 ||
            index >= columns.Length)
        {
            return string.Empty;
        }

        return columns[index].Trim();
    }

    static int GetCsvInt(
        string[] columns,
        int index,
        int fallback)
    {
        string value =
            GetCsvString(
                columns,
                index
            );

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int result)
                ? result
                : fallback;
    }

    static float GetCsvFloat(
        string[] columns,
        int index,
        float fallback)
    {
        string value =
            GetCsvString(
                columns,
                index
            );

        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float result)
                ? result
                : fallback;
    }

    void SetupRigidbody()
    {
        if (rb == null)
        {
            Debug.LogWarning(name + ": Rigidbody가 없습니다.");
            return;
        }

        rb.useGravity = false;

        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    void FindPlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject foundPlayer =
            GameObject.FindGameObjectWithTag("Player");

        if (foundPlayer != null)
        {
            player = foundPlayer.transform;
        }
    }

    void CachePlayerCollider()
    {
        if (player == null)
        {
            playerCollider = null;
            return;
        }

        playerCollider = player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            playerCollider = player.GetComponentInChildren<Collider>();
        }
    }

    void CacheBossCollider()
    {
        bossCollider = GetComponent<Collider>();

        if (bossCollider == null)
        {
            bossCollider = GetComponentInChildren<Collider>();
        }
    }

    void UpdateCooldowns()
    {
        if (attackTimer > 0f)
        {
            attackTimer -= Time.deltaTime;
        }

        if (dashTimer > 0f)
        {
            dashTimer -= Time.deltaTime;
        }

        if (jumpTimer > 0f)
        {
            jumpTimer -= Time.deltaTime;
        }

        if (poisonTimer > 0f)
        {
            poisonTimer -= Time.deltaTime;
        }

        if (skillDecisionTimer > 0f)
        {
            skillDecisionTimer -= Time.deltaTime;
        }
    }

    void UpdateTargetState()
    {
        if (targetMode == TargetMode.Player)
        {
            if (player == null)
            {
                FindPlayer();
                CachePlayerCollider();

                if (player == null)
                {
                    SetTargetToDam();
                    return;
                }
            }

            // 플레이어가 죽으면 댐을 공격합니다. (플레이어가 다시 때리면 TakeDamage에서 플레이어로 전환)
            if (IsPlayerDead())
            {
                Debug.Log(name + ": 플레이어 사망 → 보스 타겟 변경 → Dam");
                SetTargetToDam();
                return;
            }

            if (!keepTargetingPlayerAfterHit)
            {
                float distanceToPlayer =
                    GetHorizontalDistance(
                        transform.position,
                        player.position
                    );

                if (distanceToPlayer > playerAggroRange)
                {
                    SetTargetToDam();
                }
            }
        }
        else
        {

            if (currentDamPart == null ||
                currentDamCollider == null)
            {
                FindNearestDam();
            }
        }
    }

    void ChasePlayer()
    {
        if (player == null)
        {
            StopMove();
            SetMoveAnimation(0f);
            return;
        }

        Vector3 playerCenter = GetPlayerCenter();
        Vector3 closestPoint = GetClosestPointOnPlayer();

        Vector3 lookDirection =
            playerCenter - transform.position;

        lookDirection.y = 0f;

        Vector3 moveDirection =
            closestPoint - transform.position;

        moveDirection.y = 0f;

        float surfaceDistance = GetSurfaceDistanceTo(closestPoint);
        float centerDistance =
            GetHorizontalDistance(
                transform.position,
                playerCenter
            );

        RotateToTarget(lookDirection);

        if (TryUseBossSkill(centerDistance))
        {
            return;
        }

        if (surfaceDistance <= attackContactGap)
        {
            StopMove();
            SetMoveAnimation(0f);

            TryStartNormalAttack();
            return;
        }

        MoveTo(moveDirection, surfaceDistance - attackContactGap * 0.5f);
    }

    bool TryUseBossSkill(float distanceToPlayer)
    {
        if (currentAction != BossAction.None)
        {
            return true;
        }

        if (skillDecisionTimer > 0f)
        {
            return false;
        }

        skillDecisionTimer = Mathf.Max(0.1f, skillDecisionInterval);

        if (useKingSkills)
        {
            return TryUseKingSkill();
        }

        float dashWeight = dashTimer <= 0f
            ? Mathf.Max(0f, dashSelectionChance)
            : 0f;

        float jumpWeight = jumpTimer <= 0f
            ? Mathf.Max(0f, jumpSelectionChance)
            : 0f;

        float poisonWeight = poisonTimer <= 0f
            ? Mathf.Max(0f, poisonSelectionChance)
            : 0f;

        float totalWeight =
            dashWeight +
            jumpWeight +
            poisonWeight;

        if (totalWeight <= 0f)
        {
            return false;
        }

        float selectedValue =
            Random.Range(0f, totalWeight);

        if (selectedValue < dashWeight)
        {
            StartBossAction(DashRoutine());
            return true;
        }

        selectedValue -= dashWeight;

        if (selectedValue < jumpWeight)
        {
            StartBossAction(JumpRoutine());
            return true;
        }

        StartBossAction(PoisonRoutine());
        return true;
    }

    void ChaseDam()
    {
        if (currentDamPart == null ||
            currentDamCollider == null)
        {
            FindNearestDam();

            if (currentDamPart == null ||
                currentDamCollider == null)
            {
                StopMove();
                SetMoveAnimation(0f);
                return;
            }
        }

        if (TryUseBossSkill(
                player != null
                    ? GetHorizontalDistance(
                        transform.position,
                        player.position
                    )
                    : 0f
            ))
        {
            return;
        }

        Vector3 targetPosition = GetClosestPointOnDam();

        Vector3 direction =
            targetPosition - transform.position;

        direction.y = 0f;

        float distance = GetSurfaceDistanceTo(targetPosition);

        if (distance <= attackContactGap)
        {
            StopMove();
            RotateToTarget(direction);
            SetMoveAnimation(0f);

            TryStartDamAttack();
            return;
        }

        MoveTo(direction, distance - attackContactGap * 0.5f);
    }

    void TryStartNormalAttack()
    {
        if (attackTimer > 0f ||
            currentAction != BossAction.None)
        {
            return;
        }

        attackTimer = attackInterval;
        StartBossAction(AttackPlayerRoutine());
    }

    void TryStartDamAttack()
    {
        if (attackTimer > 0f ||
            currentAction != BossAction.None)
        {
            return;
        }

        attackTimer = attackInterval;
        StartBossAction(AttackDamRoutine());
    }

    void StartBossAction(IEnumerator routine)
    {
        if (isDead)
        {
            return;
        }

        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);

            // 어떤 이유로든 진행 중이던 액션이 중단되는 경우,
            // 대시 캐스팅/포이즌 표적이 화면에 남지 않도록 정리한다.
            ClearDashGuide();
            ClearPoisonEffects();
            ClearKingWarnings();
        }

        currentActionCoroutine = StartCoroutine(routine);
    }

    IEnumerator AttackPlayerRoutine()
    {
        currentAction = BossAction.Attack;

        StopMove();
        SetMoveAnimation(0f);
        RotateTowardPlayerImmediately();
        PlayTrigger(attackParameterName);

        yield return new WaitForSeconds(attackHitDelay);

        if (!isDead && IsPlayerWithinRange(attackContactGap + 0.5f))
        {
            DamagePlayer(attackDamage);
        }

        float remainingTime =
            Mathf.Max(
                0f,
                attackAnimationDuration - attackHitDelay
            );

        yield return new WaitForSeconds(remainingTime);

        FinishCurrentAction();
    }

    IEnumerator AttackDamRoutine()
    {
        currentAction = BossAction.Attack;

        StopMove();
        SetMoveAnimation(0f);
        PlayTrigger(attackParameterName);

        yield return new WaitForSeconds(attackHitDelay);

        if (!isDead && currentDamPart != null)
        {
            Vector3 closestPoint = GetClosestPointOnDam();

            float distance = GetSurfaceDistanceTo(closestPoint);

            if (distance <= attackContactGap + 0.5f)
            {
                currentDamPart.TakeDamage(attackDamage);
            }
        }

        float remainingTime =
            Mathf.Max(
                0f,
                attackAnimationDuration - attackHitDelay
            );

        yield return new WaitForSeconds(remainingTime);

        FinishCurrentAction();
    }

    IEnumerator DashRoutine()
    {
        currentAction = BossAction.Dash;
        dashTimer = dashCooldown;

        StopMove();
        SetMoveAnimation(0f);
        RotateTowardPlayerImmediately();
        PlayTrigger(dashParameterName);

        if (player == null || rb == null)
        {
            FinishCurrentAction();
            yield break;
        }

        Vector3 lastEndPoint = transform.position;
        bool hasGuidePoint = false;

        float castDuration = Mathf.Max(0.01f, dashCastDuration);
        float castElapsed = 0f;

        // 캐스팅 동안 매 프레임 플레이어 위치를 다시 계산해서
        // 가이드가 실시간으로 플레이어를 따라가도록 한다.
        while (castElapsed < castDuration && !isDead)
        {
            RotateTowardPlayerImmediately();

            Vector3 direction = GetDirectionToPlayer();

            float distanceToPlayer =
                GetHorizontalDistance(
                    transform.position,
                    player.position
                );

            float guideDistance =
                Mathf.Min(
                    distanceToPlayer,
                    Mathf.Max(0.1f, dashDistance)
                );

            Vector3 endPoint =
                transform.position +
                direction * guideDistance;

            UpdateDashGuide(
                transform.position,
                endPoint,
                direction
            );

            lastEndPoint = endPoint;
            hasGuidePoint = true;

            castElapsed += Time.deltaTime;

            yield return null;
        }

        // 캐스팅이 끝나면 가이드는 즉시 사라진다.
        ClearDashGuide();

        if (isDead)
        {
            yield break;
        }

        if (!hasGuidePoint)
        {
            FinishCurrentAction();
            yield break;
        }

        Vector3 startPosition = rb.position;

        Vector3 dashDirection = lastEndPoint - startPosition;
        dashDirection.y = 0f;

        if (dashDirection.sqrMagnitude <= 0.001f)
        {
            FinishCurrentAction();
            yield break;
        }

        Vector3 targetPosition =
            GetSafeDashTargetPosition(
                startPosition,
                dashDirection.normalized,
                dashDirection.magnitude
            );

        float travelDistance =
            Vector3.Distance(startPosition, targetPosition);

        float moveDuration =
            Mathf.Max(
                dashMinDuration,
                travelDistance / Mathf.Max(0.01f, dashMoveSpeed)
            );

        float elapsed = 0f;
        bool damagedPlayer = false;

        while (elapsed < moveDuration && !isDead)
        {
            float ratio =
                Mathf.Clamp01(elapsed / moveDuration);

            Vector3 nextPosition =
                Vector3.Lerp(
                    startPosition,
                    targetPosition,
                    ratio
                );

            rb.MovePosition(nextPosition);

            if (!damagedPlayer &&
                IsPlayerWithinRange(dashHitRange))
            {
                DamagePlayer(dashDamage);
                damagedPlayer = true;

                if (cameraShake != null)
                {
                    cameraShake.Shake(
                        landingShakeDuration,
                        landingShakeStrength
                    );
                }
            }

            elapsed += Time.fixedDeltaTime;

            yield return new WaitForFixedUpdate();
        }

        if (!isDead)
        {
            rb.MovePosition(targetPosition);
        }

        FinishCurrentAction();
    }

    void EnsureDashGuidePool()
    {
        if (dashGuideSegmentPool.Count > 0)
        {
            return;
        }

        if (dashGuideSegmentPrefabA == null &&
            dashGuideSegmentPrefabB == null)
        {
            return;
        }

        float spacing = Mathf.Max(0.1f, dashGuideSegmentSpacing);

        int maxSegmentCount =
            Mathf.Max(
                1,
                Mathf.CeilToInt(
                    Mathf.Max(0.1f, dashDistance) / spacing
                )
            );

        for (int i = 0; i < maxSegmentCount; i++)
        {
            GameObject prefab =
                (i % 2 == 0)
                    ? (dashGuideSegmentPrefabA != null
                        ? dashGuideSegmentPrefabA
                        : dashGuideSegmentPrefabB)
                    : (dashGuideSegmentPrefabB != null
                        ? dashGuideSegmentPrefabB
                        : dashGuideSegmentPrefabA);

            if (prefab == null)
            {
                continue;
            }

            GameObject segment = Instantiate(prefab);
            segment.SetActive(false);
            dashGuideSegmentPool.Add(segment);
        }
    }

    void UpdateDashGuide(
        Vector3 start,
        Vector3 end,
        Vector3 direction
    )
    {
        EnsureDashGuidePool();

        if (dashGuideSegmentPool.Count == 0)
        {
            return;
        }

        float spacing = Mathf.Max(0.1f, dashGuideSegmentSpacing);

        float totalDistance =
            GetHorizontalDistance(start, end);

        int segmentCount =
            Mathf.Clamp(
                Mathf.FloorToInt(totalDistance / spacing),
                0,
                dashGuideSegmentPool.Count
            );

        Vector3 normalizedDirection =
            direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : transform.forward;

        Quaternion rotation =
            Quaternion.LookRotation(
                normalizedDirection,
                Vector3.up
            ) *
            Quaternion.Euler(90f, 0f, 0f);

        for (int i = 0; i < dashGuideSegmentPool.Count; i++)
        {
            GameObject segment = dashGuideSegmentPool[i];

            if (segment == null)
            {
                continue;
            }

            if (i < segmentCount)
            {
                Vector3 segmentPosition =
                    start +
                    normalizedDirection *
                    (spacing * (i + 0.5f)) +
                    Vector3.up * dashGuideHeightOffset;

                segment.transform.SetPositionAndRotation(
                    segmentPosition,
                    rotation
                );

                segment.SetActive(true);
            }
            else
            {
                segment.SetActive(false);
            }
        }
    }

    void ClearDashGuide()
    {
        for (int i = 0; i < dashGuideSegmentPool.Count; i++)
        {
            if (dashGuideSegmentPool[i] != null)
            {
                dashGuideSegmentPool[i].SetActive(false);
            }
        }
    }

    void ClearPoisonEffects()
    {
        for (int i = 0; i < poisonImpactCoroutines.Count; i++)
        {
            if (poisonImpactCoroutines[i] != null)
            {
                StopCoroutine(poisonImpactCoroutines[i]);
            }
        }

        poisonImpactCoroutines.Clear();

        for (int i = 0; i < poisonSpawnedObjects.Count; i++)
        {
            if (poisonSpawnedObjects[i] != null)
            {
                Destroy(poisonSpawnedObjects[i]);
            }
        }

        poisonSpawnedObjects.Clear();
    }

    IEnumerator JumpRoutine()
    {
        currentAction = BossAction.Jump;
        jumpTimer = skillDecisionInterval;

        StopMove();
        SetMoveAnimation(0f);
        RotateTowardPlayerImmediately();

        // 점프 전 1초간 캐스트바를 0%에서 100%까지 채운다.
        yield return RunActionCastBar(jumpCastDuration);

        if (isDead)
        {
            yield break;
        }

        PlayTrigger(jumpStartParameterName);

        if (rb == null || player == null)
        {
            FinishCurrentAction();
            yield break;
        }

        RigidbodyConstraints originalConstraints =
            rb.constraints;

        rb.constraints =
            originalConstraints &
            ~RigidbodyConstraints.FreezePositionY;

        Vector3 startPosition = rb.position;
        Vector3 airPosition =
            startPosition +
            Vector3.up * jumpHeight;

        float riseDuration =
            Mathf.Max(0.01f, jumpRiseDuration);

        float elapsed = 0f;

        while (elapsed < riseDuration && !isDead)
        {
            float ratio =
                Mathf.Clamp01(elapsed / riseDuration);

            float easedRatio =
                1f - Mathf.Pow(1f - ratio, 3f);

            rb.MovePosition(
                Vector3.Lerp(
                    startPosition,
                    airPosition,
                    easedRatio
                )
            );

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isDead)
        {
            yield break;
        }

        rb.MovePosition(airPosition);

        float remainingTargetDelay =
            Mathf.Max(
                0f,
                jumpTargetMarkDelay -
                riseDuration
            );

        if (remainingTargetDelay > 0f)
        {
            yield return new WaitForSeconds(
                remainingTargetDelay
            );
        }

        Vector3 groundPoint =
            GetPlayerGroundPosition();

        // groundPoint는 실제 바닥 표면이고, landingPosition은
        // groundLevelY 고정값을 기준으로 보정된 착지 위치다.
        Vector3 landingPosition =
            GetBossLandingPosition(groundPoint);

        GameObject targetMark = null;

        if (jumpTargetPrefab != null)
        {
            targetMark = Instantiate(
                jumpTargetPrefab,
                groundPoint +
                Vector3.up * targetMarkHeightOffset,
                Quaternion.Euler(90f, 0f, 0f)
            );
        }

        Vector3 hoverPosition =
            landingPosition +
            Vector3.up * jumpHeight;

        rb.position = hoverPosition;

        yield return new WaitForSeconds(
            Mathf.Max(0f, jumpWarningDuration)
        );

        PlayTrigger(jumpEndParameterName);

        elapsed = 0f;

        float fallDuration =
            Mathf.Max(0.01f, jumpFallDuration);

        while (elapsed < fallDuration && !isDead)
        {
            float ratio =
                Mathf.Clamp01(elapsed / fallDuration);

            float easedRatio =
                ratio * ratio;

            rb.MovePosition(
                Vector3.Lerp(
                    hoverPosition,
                    landingPosition,
                    easedRatio
                )
            );

            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        if (isDead)
        {
            if (targetMark != null)
            {
                Destroy(targetMark);
            }

            yield break;
        }

        // groundLevelY를 최종적으로 한 번 더 강제 적용해서
        // 보간 오차나 다른 경로의 y값 오염을 방지한다.
        landingPosition.y = groundLevelY;
        rb.position = landingPosition;
        rb.constraints = originalConstraints;

        if (targetMark != null)
        {
            Destroy(targetMark);
        }

        if (IsPlayerInsideJumpDamageArea(
                landingPosition,
                jumpDamageRadius
            ))
        {
            DamagePlayer(jumpDamage);
        }

        if (cameraShake != null)
        {
            cameraShake.Shake(
                landingShakeDuration,
                landingShakeStrength
            );
        }

        yield return new WaitForSeconds(
            Mathf.Max(0f, jumpRecoveryDuration)
        );

        FinishCurrentAction();
    }

    IEnumerator RunActionCastBar(float duration)
    {
        if (jumpCastBarInstance != null)
        {
            jumpCastBarInstance.SetActive(true);
        }

        if (jumpCastBarFillImage != null)
        {
            jumpCastBarFillImage.fillAmount = 0f;
        }

        UpdateJumpCastBarPosition();

        float castDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < castDuration && !isDead)
        {
            float ratio = Mathf.Clamp01(elapsed / castDuration);

            if (jumpCastBarFillImage != null)
            {
                jumpCastBarFillImage.fillAmount = ratio;
            }

            UpdateJumpCastBarPosition();

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isDead && jumpCastBarFillImage != null)
        {
            jumpCastBarFillImage.fillAmount = 1f;
        }

        if (jumpCastBarInstance != null)
        {
            jumpCastBarInstance.SetActive(false);
        }
    }

    void UpdateJumpCastBarPosition()
    {
        if (jumpCastBarRectTransform == null)
        {
            return;
        }

        Camera cam =
            jumpCastBarCamera != null
                ? jumpCastBarCamera
                : Camera.main;

        Vector3 worldPosition =
            transform.position +
            Vector3.up * jumpCastBarHeightOffset;

        Vector3 screenPosition =
            cam != null
                ? cam.WorldToScreenPoint(worldPosition)
                : worldPosition;

        jumpCastBarRectTransform.position = screenPosition;
    }

    IEnumerator PoisonRoutine()
    {
        currentAction = BossAction.Poison;
        poisonTimer = poisonCooldown;

        // 혹시 이전 실행이 정상적으로 정리되지 못했을 경우를 대비한 안전장치.
        ClearPoisonEffects();

        StopMove();
        SetMoveAnimation(0f);
        RotateTowardPlayerImmediately();
        PlayTrigger(poisonParameterName);

        // 1. 캐스팅 (캐스트바 표시)
        yield return RunActionCastBar(poisonCastDuration);

        if (isDead)
        {
            yield break;
        }

        // 2. 보스 주변 반경 내 랜덤한 위치에 표적을 생성한다.
        int targetCount = Mathf.Max(1, poisonTargetCount);
        Vector3[] targetPositions = new Vector3[targetCount];
        GameObject[] targetMarkers = new GameObject[targetCount];

        for (int i = 0; i < targetCount; i++)
        {
            Vector3 groundPosition = GetRandomPoisonTargetPosition();
            targetPositions[i] = groundPosition;

            if (poisonTargetMarkerPrefab != null)
            {
                targetMarkers[i] = Instantiate(
                    poisonTargetMarkerPrefab,
                    groundPosition +
                    Vector3.up * poisonTargetMarkerHeightOffset,
                    Quaternion.Euler(90f, 0f, 0f)
                );

                poisonSpawnedObjects.Add(targetMarkers[i]);
            }
        }

        // 3. 표적마다 시간차를 두고(총 poisonImpactSpreadDuration 이내) 포이즌을 떨어뜨린다.
        float spreadDuration =
            Mathf.Max(0.01f, poisonImpactSpreadDuration);

        Coroutine[] impactCoroutines = new Coroutine[targetCount];

        for (int i = 0; i < targetCount; i++)
        {
            float delay =
                targetCount > 1
                    ? spreadDuration * i / (targetCount - 1)
                    : 0f;

            impactCoroutines[i] = StartCoroutine(
                SpawnPoisonImpact(
                    targetPositions[i],
                    targetMarkers[i],
                    delay
                )
            );

            poisonImpactCoroutines.Add(impactCoroutines[i]);
        }

        // 4. 모든 포이즌 오브젝트가 떨어지고, 각자의 지속시간까지 끝날 때까지 기다린다.
        for (int i = 0; i < targetCount; i++)
        {
            if (impactCoroutines[i] != null)
            {
                yield return impactCoroutines[i];
            }
        }

        poisonImpactCoroutines.Clear();

        // 1~4가 모두 끝난 뒤에야 보스가 다시 움직인다.
        FinishCurrentAction();
    }

    Vector3 GetRandomPoisonTargetPosition()
    {
        Vector2 randomCircle =
            Random.insideUnitCircle *
            Mathf.Max(0.1f, poisonTargetRadius);

        Vector3 randomHorizontalPosition =
            transform.position +
            new Vector3(randomCircle.x, 0f, randomCircle.y);

        return FindGroundPosition(randomHorizontalPosition);
    }

    IEnumerator SpawnPoisonImpact(
        Vector3 position,
        GameObject targetMarker,
        float delay
    )
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (isDead)
        {
            if (targetMarker != null)
            {
                Destroy(targetMarker);
            }

            yield break;
        }

        // 보스의 현재 좌표에서 솟아오른다.
        Vector3 launchPosition = transform.position;

        GameObject impactObject = null;

        if (poisonImpactPrefab != null)
        {
            impactObject = Instantiate(
                poisonImpactPrefab,
                launchPosition,
                Quaternion.identity
            );

            poisonSpawnedObjects.Add(impactObject);
        }

        Vector3 skyAboveTarget =
            new Vector3(
                position.x,
                launchPosition.y + Mathf.Max(0.1f, poisonLaunchHeight),
                position.z
            );

        // 보스 좌표 → 목표 상공으로 솟아오른다.
        float riseDuration = Mathf.Max(0.01f, poisonRiseDuration);
        float riseElapsed = 0f;

        while (riseElapsed < riseDuration && !isDead)
        {
            float ratio = Mathf.Clamp01(riseElapsed / riseDuration);

            if (impactObject != null)
            {
                impactObject.transform.position =
                    Vector3.Lerp(
                        launchPosition,
                        skyAboveTarget,
                        ratio
                    );
            }

            riseElapsed += Time.deltaTime;
            yield return null;
        }

        if (isDead)
        {
            if (targetMarker != null)
            {
                Destroy(targetMarker);
            }

            if (impactObject != null)
            {
                Destroy(impactObject);
            }

            yield break;
        }

        if (impactObject != null)
        {
            impactObject.transform.position = skyAboveTarget;
        }

        // 목표 상공에서 잠시 머문다 (상승 시간 포함 총 약 1초 뒤 낙하 시작).
        float hoverDuration = Mathf.Max(0f, poisonHoverDuration);

        if (hoverDuration > 0f)
        {
            yield return new WaitForSeconds(hoverDuration);
        }

        if (isDead)
        {
            if (targetMarker != null)
            {
                Destroy(targetMarker);
            }

            if (impactObject != null)
            {
                Destroy(impactObject);
            }

            yield break;
        }

        // 목표 좌표로 빠르게 낙하한다.
        float fallDuration = Mathf.Max(0.01f, poisonFallDuration);
        float fallElapsed = 0f;

        while (fallElapsed < fallDuration && !isDead)
        {
            float ratio = Mathf.Clamp01(fallElapsed / fallDuration);

            if (impactObject != null)
            {
                impactObject.transform.position =
                    Vector3.Lerp(
                        skyAboveTarget,
                        position,
                        ratio
                    );
            }

            fallElapsed += Time.deltaTime;
            yield return null;
        }

        // 착지와 동시에 표적 마커는 사라진다.
        if (targetMarker != null)
        {
            Destroy(targetMarker);
        }

        if (isDead)
        {
            if (impactObject != null)
            {
                Destroy(impactObject);
            }

            yield break;
        }

        if (impactObject != null)
        {
            impactObject.transform.position = position;
        }

        float elapsed = 0f;
        bool damagedPlayer = false;

        float lifetime = Mathf.Max(0f, poisonImpactLifetime);

        while (elapsed < lifetime && !isDead)
        {
            if (!damagedPlayer &&
                IsPositionWithinRangeOfPlayer(
                    position,
                    poisonImpactRadius
                ))
            {
                DamagePlayer(poisonImpactDamage);
                damagedPlayer = true;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (impactObject != null)
        {
            Destroy(impactObject);
        }
    }

    bool IsPositionWithinRangeOfPlayer(
        Vector3 position,
        float radius
    )
    {
        if (player == null)
        {
            return false;
        }

        Vector3 playerPosition = GetPlayerCenter();

        position.y = 0f;
        playerPosition.y = 0f;

        return Vector3.Distance(position, playerPosition) <= radius;
    }

    void FinishCurrentAction()
    {
        if (isDead)
        {
            return;
        }

        currentAction = BossAction.None;
        currentActionCoroutine = null;
    }

    void MoveTo(Vector3 direction, float maxStep = float.PositiveInfinity)
    {
        if (rb == null)
        {
            return;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            StopMove();
            SetMoveAnimation(0f);
            return;
        }

        Vector3 moveDirection = direction.normalized;

        float step = Mathf.Min(
            GetCurrentMoveSpeed() * Time.fixedDeltaTime,
            Mathf.Max(0f, maxStep));

        Vector3 nextPosition =
            rb.position +
            moveDirection *
            step;

        rb.MovePosition(nextPosition);

        RotateToTarget(moveDirection);
        SetMoveAnimation(1f);
    }

    void RotateTowardPlayerImmediately()
    {
        if (player == null || rb == null)
        {
            return;
        }

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        rb.MoveRotation(
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            )
        );
    }

    void RotateToTarget(Vector3 direction)
    {
        if (rb == null)
        {
            return;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        Quaternion nextRotation =
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(nextRotation);
    }

    void StopMove()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
        {
            return;
        }

        TryReceiveBulletHit(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        TryReceiveBulletHit(other);
    }

    void TryReceiveBulletHit(Collider hitCollider)
    {
        if (!receiveDamageFromBulletCollision ||
            isDead ||
            hitCollider == null)
        {
            return;
        }

        GameObject bulletObject = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.gameObject
            : hitCollider.gameObject;

        bool isPlayerBullet =
            (!string.IsNullOrWhiteSpace(playerBulletTag) &&
             bulletObject.CompareTag(playerBulletTag)) ||
            (!string.IsNullOrWhiteSpace(playerBulletTag) &&
             hitCollider.CompareTag(playerBulletTag));

        if (!isPlayerBullet)
        {
            return;
        }

        int bulletId = bulletObject.GetInstanceID();

        if (!processedBulletIds.Add(bulletId))
        {
            return;
        }

        int appliedDamage =
            Mathf.Max(1, fallbackBulletDamage);

        TopDownBullet bullet =
            bulletObject.GetComponent<TopDownBullet>();

        if (bullet == null)
        {
            bullet =
                bulletObject.GetComponentInChildren<TopDownBullet>();
        }

        if (bullet != null)
        {
            // 총알 스크립트가 이미 명중 처리했다면 중복 피해를 주지 않습니다.
            if (!bullet.TryConsumeHit())
            {
                return;
            }

            appliedDamage =
                Mathf.Max(0, bullet.damage);
        }

        TakeDamage(appliedDamage);

        if (destroyBulletOnHit && bulletObject != null)
        {
            Destroy(bulletObject);
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        // 피해 판정보다 먼저 플레이어 어그로를 확정한다.
        SetTargetToPlayer();

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);

        UpdateFixedBossHpBar();

        Debug.Log(
            name +
            ": 보스 피격 / HP " +
            currentHp +
            " / " +
            maxHp +
            " / 현재 타겟: " +
            targetMode
        );

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        // 댐 공격이나 일반 행동 중 맞으면 즉시 취소하고 플레이어를 바라보게 한다.
        // 단, Jump/Dash/Poison 스킬은 캐스팅부터 실행까지의 전체 프로세스가
        // 피격으로 중단되지 않고 그대로 이어져야 하므로 제외한다.
        if (currentAction != BossAction.Jump &&
            currentAction != BossAction.Dash &&
            currentAction != BossAction.Poison &&
            currentAction != BossAction.FloorAttack &&
            currentAction != BossAction.SphereShot)
        {
            if (currentActionCoroutine != null)
            {
                StopCoroutine(currentActionCoroutine);
                currentActionCoroutine = null;
            }

            currentAction = BossAction.None;
            StopMove();
            RotateTowardPlayerImmediately();
            StartBossAction(HitRoutine());
        }
    }

    IEnumerator HitRoutine()
    {
        currentAction = BossAction.Hit;

        StopMove();
        SetMoveAnimation(0f);
        PlayTrigger(hitParameterName);

        yield return new WaitForSeconds(hitStopDuration);

        FinishCurrentAction();
    }

    void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentAction = BossAction.Dead;

        // 슬라임 건 게이지 충전 (실제로 처치된 경우만)
        SlimeGunController.NotifyMonsterKilled();

        /*
         * 보스 HP바를 즉시 제거합니다.
         */
        HideFixedBossHpBar();

        /*
         * MobTableSpawner가 Instantiate 이후 WaveMobTracker를
         * 추가할 수 있으므로 사망 시점에 다시 검색합니다.
         */
        if (waveMobTracker == null)
        {
            waveMobTracker =
                GetComponent<WaveMobTracker>();
        }

        /*
         * 보스의 실제 HP가 0이 된 경우에만
         * 웨이브 시스템에 사망 사실을 전달합니다.
         */
        if (waveMobTracker != null)
        {
            waveMobTracker.NotifyKilled();

            Debug.Log(
                name +
                ": 보스 사망을 웨이브 시스템에 전달"
            );
        }
        else
        {
            Debug.LogWarning(
                name +
                ": WaveMobTracker를 찾지 못했습니다. " +
                "웨이브가 종료되지 않을 수 있습니다.",
                this
            );
        }

        if (currentActionCoroutine != null)
        {
            StopCoroutine(currentActionCoroutine);
            currentActionCoroutine = null;
        }

        StopMove();
        SetMoveAnimationDirect(0f);

        ResetAllTriggers();
        PlayTrigger(dieParameterName);

        if (jumpCastBarInstance != null)
        {
            Destroy(jumpCastBarInstance);
            jumpCastBarInstance = null;
        }

        ClearDashGuide();
        ClearPoisonEffects();

        DisableBossPhysics();

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(
            deathAnimationDuration
        );

        SpawnDeathEffect();
        Destroy(gameObject);
    }

    void DisableBossPhysics()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider bossCollider in colliders)
        {
            bossCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }
    }

    void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition =
            transform.position +
            Vector3.up * deathEffectHeightOffset;

        GameObject effect = Instantiate(
            deathEffectPrefab,
            spawnPosition,
            deathEffectPrefab.transform.rotation
        );

        Destroy(
            effect,
            MonsterChaser.GetEffectLifetime(effect, deathEffectDestroyDelay)
        );
    }

    void DamagePlayer(int damage)
    {
        if (player == null)
        {
            return;
        }

        PlayerHealth playerHealth =
            player.GetComponent<PlayerHealth>();

        if (playerHealth == null)
        {
            playerHealth =
                player.GetComponentInChildren<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            playerHealth =
                player.GetComponentInParent<PlayerHealth>();
        }

        if (playerHealth == null)
        {
            Debug.LogWarning(
                name +
                ": PlayerHealth를 찾지 못했습니다."
            );

            return;
        }

        playerHealth.TakeDamage(damage);

        Debug.Log(
            name +
            ": 플레이어 공격 / 피해량 " +
            damage
        );
    }

    bool IsPlayerWithinRange(float range)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 closestPoint = GetClosestPointOnPlayer();

        float distance = GetSurfaceDistanceTo(closestPoint);

        return distance <= range;
    }

    void FindCameraShake()
    {
        if (cameraShake != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            cameraShake =
                mainCamera.GetComponent<SimpleCameraShake>();
        }
    }

    void SpawnJumpCastBar()
    {
        if (jumpCastBarPrefab == null)
        {
            return;
        }

        Transform parent =
            jumpCastBarCanvas != null
                ? jumpCastBarCanvas.transform
                : FindDefaultCastBarParent();

        if (parent == null)
        {
            Debug.LogWarning(
                name +
                ": 캐스트바를 붙일 Canvas를 찾지 못했습니다."
            );

            return;
        }

        jumpCastBarInstance = Instantiate(jumpCastBarPrefab, parent);
        jumpCastBarInstance.name = name + "_JumpCastBar";

        jumpCastBarRectTransform =
            jumpCastBarInstance.transform as RectTransform;

        jumpCastBarFillImage =
            FindChildImageByName(
                jumpCastBarInstance.transform,
                jumpCastBarFillImageName
            );

        // 소환 직후에는 캐스팅 중이 아니므로 꺼둔다.
        jumpCastBarInstance.SetActive(false);
    }

    void SetupFixedBossHpBar()
    {
        fixedBossHpRoot = FindSceneObjectIncludingInactive(
            fixedBossHpRootName
        );

        // 이전 설정값이 Bosshp_bg처럼 자식 오브젝트를 가리키고 있어도
        // 실제 HP바 전체 루트(Boss HPbar)를 사용하도록 부모를 자동 보정합니다.
        if (fixedBossHpRoot != null &&
            fixedBossHpRoot.name == "Bosshp_bg" &&
            fixedBossHpRoot.transform.parent != null)
        {
            fixedBossHpRoot =
                fixedBossHpRoot.transform.parent.gameObject;
        }

        if (fixedBossHpRoot == null)
        {
            Debug.LogWarning(
                name +
                ": 고정 보스 HP바 루트 '" +
                fixedBossHpRootName +
                "'를 찾지 못했습니다.",
                this
            );
            return;
        }

        fixedBossHpFillImage = FindImageIncludingInactive(
            fixedBossHpRoot.transform,
            fixedBossHpFillName
        );

        if (fixedBossHpFillImage == null)
        {
            Debug.LogWarning(
                name +
                ": 고정 보스 HP Fill Image '" +
                fixedBossHpFillName +
                "'를 찾지 못했습니다.",
                this
            );
        }

        if (fixedBossHpFillImage != null)
        {
            fixedBossHpFillImage.type = Image.Type.Filled;
            fixedBossHpFillImage.fillAmount = 1f;
        }

        // 보스 오브젝트가 생성된 시점부터 고정 HP바를 표시합니다.
        fixedBossHpRoot.SetActive(showBossHpOnSpawn);
        UpdateFixedBossHpBar();
    }

    void UpdateFixedBossHpBar()
    {
        if (fixedBossHpRoot == null)
        {
            SetupFixedBossHpBar();

            if (fixedBossHpRoot == null)
            {
                return;
            }
        }

        float hpRatio =
            maxHp > 0
                ? Mathf.Clamp01((float)currentHp / maxHp)
                : 0f;

        if (fixedBossHpFillImage != null)
        {
            fixedBossHpFillImage.fillAmount = hpRatio;
        }

        // 보스가 살아 있는 동안에는 피격 여부와 관계없이 계속 표시합니다.
        if (!isDead && !fixedBossHpRoot.activeSelf)
        {
            fixedBossHpRoot.SetActive(true);
        }
    }

    void HideFixedBossHpBar()
    {
        if (fixedBossHpRoot == null)
        {
            fixedBossHpRoot = FindSceneObjectIncludingInactive(
                fixedBossHpRootName
            );

            if (fixedBossHpRoot != null &&
                fixedBossHpRoot.name == "Bosshp_bg" &&
                fixedBossHpRoot.transform.parent != null)
            {
                fixedBossHpRoot =
                    fixedBossHpRoot.transform.parent.gameObject;
            }
        }

        if (fixedBossHpRoot != null)
        {
            fixedBossHpRoot.SetActive(false);
        }
    }

    GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        GameObject[] allObjects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null ||
                obj.name != objectName ||
                !obj.scene.IsValid())
            {
                continue;
            }

            return obj;
        }

        return null;
    }

    Image FindImageIncludingInactive(
        Transform root,
        string objectName
    )
    {
        if (root == null)
        {
            return null;
        }

        Image[] images =
            root.GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(objectName) ||
                image.gameObject.name == objectName)
            {
                return image;
            }
        }

        return null;
    }

    Transform FindDefaultCastBarParent()
    {
        Canvas canvas =
            FindFirstObjectByType<Canvas>();

        return canvas != null ? canvas.transform : null;
    }

    Image FindChildImageByName(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(childName))
        {
            Transform[] children =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child.name == childName)
                {
                    Image childImage = child.GetComponent<Image>();

                    if (childImage != null)
                    {
                        return childImage;
                    }
                }
            }
        }

        // 이름으로 못 찾으면 하위의 첫 Image 컴포넌트로 대체한다.
        return root.GetComponentInChildren<Image>(true);
    }

    Vector3 GetPlayerGroundPosition()
    {
        Vector3 playerPosition =
            player != null
                ? player.position
                : transform.position;

        return FindGroundPosition(playerPosition);
    }

    Vector3 FindGroundPosition(Vector3 horizontalPosition)
    {
        Vector3 rayOrigin =
            horizontalPosition +
            Vector3.up * Mathf.Max(0.1f, groundRayStartHeight);

        int layerMask =
            groundMask.value == 0
                ? Physics.DefaultRaycastLayers
                : groundMask.value;

        RaycastHit[] hits = Physics.RaycastAll(
            rayOrigin,
            Vector3.down,
            Mathf.Max(0.1f, groundRayDistance),
            layerMask,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            Transform hitTransform = hit.collider.transform;

            // 플레이어 자신의 콜라이더, 보스 콜라이더는 바닥으로 취급하지 않는다.
            if ((player != null &&
                 (hitTransform == player ||
                  hitTransform.IsChildOf(player))) ||
                hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                continue;
            }

            return hit.point;
        }

        // 바닥을 못 찾으면 X/Z는 유지하고 groundLevelY를 사용한다.
        horizontalPosition.y = groundLevelY;

        return horizontalPosition;
    }

    void CacheBossFootOffset()
    {
        // 참고용으로만 계산해 둔다. 실제 착지 위치는 groundLevelY로 강제 고정되므로
        // 이 값은 더 이상 착지 로직에 직접 사용되지 않는다.
        if (bossCollider == null)
        {
            CacheBossCollider();
        }

        if (bossCollider == null)
        {
            bossPivotToFootOffset = 0f;
            return;
        }

        bossPivotToFootOffset =
            transform.position.y - bossCollider.bounds.min.y;

        bossPivotToFootOffset =
            Mathf.Max(0f, bossPivotToFootOffset);
    }

    Vector3 GetBossLandingPosition(Vector3 groundPoint)
    {
        // 오프셋 계산 대신 groundLevelY를 항상 강제 적용해서
        // 착지 시 y값이 항상 일정하게 유지되도록 한다.
        Vector3 landingPosition = groundPoint;
        landingPosition.y = groundLevelY;

        return landingPosition;
    }

    bool IsPlayerInsideJumpDamageArea(
        Vector3 landingPosition,
        float radius
    )
    {
        if (player == null)
        {
            return false;
        }

        Vector3 playerPosition = GetPlayerCenter();
        landingPosition.y = 0f;
        playerPosition.y = 0f;

        return Vector3.Distance(
            landingPosition,
            playerPosition
        ) <= radius;
    }

    Vector3 GetPlayerCenter()
    {
        if (player == null)
        {
            return transform.position;
        }

        if (playerCollider == null)
        {
            CachePlayerCollider();
        }

        if (playerCollider != null)
        {
            return playerCollider.bounds.center;
        }

        return player.position;
    }

    Vector3 GetClosestPointOnPlayer()
    {
        if (player == null)
        {
            return transform.position;
        }

        if (playerCollider == null)
        {
            CachePlayerCollider();
        }

        Vector3 point;

        if (playerCollider != null)
        {
            point =
                playerCollider.ClosestPoint(
                    transform.position
                );
        }
        else
        {
            point = player.position;
        }

        point.y = transform.position.y;

        return point;
    }

    Vector3 GetDirectionToPlayer()
    {
        if (player == null)
        {
            return transform.forward;
        }

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return transform.forward;
        }

        return direction.normalized;
    }

    Vector3 GetSafeDashTargetPosition(
        Vector3 startPosition,
        Vector3 direction,
        float distance
    )
    {
        direction.y = 0f;
        direction.Normalize();

        if (dashObstacleMask.value != 0 &&
            Physics.SphereCast(
                startPosition,
                dashCollisionRadius,
                direction,
                out RaycastHit hit,
                distance,
                dashObstacleMask,
                QueryTriggerInteraction.Ignore
            ))
        {
            float safeDistance =
                Mathf.Max(
                    0f,
                    hit.distance - dashCollisionSkin
                );

            return
                startPosition +
                direction * safeDistance;
        }

        return
            startPosition +
            direction * distance;
    }

    Vector3 GetClosestPointOnDam()
    {
        if (currentDamPart == null)
        {
            return transform.position;
        }

        if (currentDamCollider == null)
        {
            return currentDamPart.transform.position;
        }

        Vector3 closestPoint =
            currentDamCollider.ClosestPoint(
                transform.position
            );

        closestPoint.y = transform.position.y;

        return closestPoint;
    }

    void FindNearestDam()
    {
        DamPart[] damParts =
            FindObjectsByType<DamPart>(
                FindObjectsSortMode.None
            );

        float nearestDistance = Mathf.Infinity;
        DamPart nearestPart = null;
        Collider nearestCollider = null;

        foreach (DamPart damPart in damParts)
        {
            Collider damCollider =
                damPart.targetCollider != null
                    ? damPart.targetCollider
                    : damPart.GetComponent<Collider>();

            if (damCollider == null)
            {
                damCollider =
                    damPart.GetComponentInChildren<Collider>();
            }

            if (damCollider == null)
            {
                damCollider =
                    damPart.GetComponentInParent<Collider>();
            }

            if (damCollider == null)
            {
                continue;
            }

            Vector3 closestPoint =
                damCollider.ClosestPoint(
                    transform.position
                );

            float distance =
                GetHorizontalDistance(
                    transform.position,
                    closestPoint
                );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPart = damPart;
                nearestCollider = damCollider;
            }
        }

        currentDamPart = nearestPart;
        currentDamCollider = nearestCollider;
    }

    void SetTargetToDam()
    {
        targetMode = TargetMode.Dam;
        FindNearestDam();
    }

    void SetTargetToPlayer()
    {
        FindPlayer();
        CachePlayerCollider();

        if (player == null)
        {
            Debug.LogWarning(
                name +
                ": 플레이어를 찾지 못했습니다."
            );

            return;
        }

        targetMode = TargetMode.Player;

        Debug.Log(
            name +
            ": 보스 타겟 변경 → Player"
        );
    }

    void SetMoveAnimation(float speedValue)
    {
        if (animator == null ||
            isDead ||
            currentAction != BossAction.None)
        {
            return;
        }

        animator.SetFloat(
            speedParameterName,
            speedValue
        );
    }

    void SetMoveAnimationDirect(float speedValue)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(
            speedParameterName,
            speedValue
        );
    }

    void PlayTrigger(string triggerName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    void ResetAllTriggers()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(attackParameterName);
        animator.ResetTrigger(hitParameterName);
        animator.ResetTrigger(dieParameterName);
        animator.ResetTrigger(dashParameterName);
        animator.ResetTrigger(jumpStartParameterName);
        animator.ResetTrigger(jumpEndParameterName);
        animator.ResetTrigger(poisonParameterName);
        animator.ResetTrigger(floorParameterName);
        animator.ResetTrigger(shotParameterName);
    }

    float GetHorizontalDistance(
        Vector3 a,
        Vector3 b
    )
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(a, b);
    }

    // ---------- 슬로우 (슬라임 건 장판) ----------
    private float slowMultiplierValue = 1f;
    private float slowEndTime = -1f;

    /// <summary>
    /// 이동속도에 multiplier(0~1)를 duration초 동안 적용합니다.
    /// 여러 장판이 겹치면 가장 강한 슬로우를 적용합니다.
    /// </summary>
    public void ApplySlow(float multiplier, float duration)
    {
        if (isDead)
        {
            return;
        }

        multiplier = Mathf.Clamp01(multiplier);

        if (Time.time >= slowEndTime)
        {
            slowMultiplierValue = multiplier;
        }
        else
        {
            slowMultiplierValue = Mathf.Min(slowMultiplierValue, multiplier);
        }

        slowEndTime = Mathf.Max(slowEndTime, Time.time + duration);
    }

    public bool IsSlowed()
    {
        return Time.time < slowEndTime;
    }

    private float GetCurrentMoveSpeed()
    {
        return IsSlowed()
            ? moveSpeed * slowMultiplierValue
            : moveSpeed;
    }

    // 몬스터 자기 콜라이더 표면에서 targetPoint까지의 수평 거리.
    // 콜라이더가 크거나 중심이 앞으로 치우친 경우에도 닿아 있으면 0에 가깝게 나옵니다.
    private Collider selfColliderCache;

    private float GetSurfaceDistanceTo(Vector3 targetPoint)
    {
        if (selfColliderCache == null)
        {
            selfColliderCache = GetComponent<Collider>();

            if (selfColliderCache == null)
            {
                selfColliderCache = GetComponentInChildren<Collider>();
            }
        }

        Vector3 from = transform.position;

        if (selfColliderCache != null && selfColliderCache.enabled)
        {
            from = selfColliderCache.ClosestPoint(targetPoint);
        }

        from.y = 0f;
        targetPoint.y = 0f;

        return Vector3.Distance(from, targetPoint);
    }

    // =====================================================================
    // King Slime Skills
    // =====================================================================

    // GameControl.csv [BOSS] 값 적용 (hp, speed, atk1 = FLOOR, atk2 = 구체 발사)
    void ApplyGameControlStats()
    {
        GameControl.BossStat s = GameControl.GetBoss(bossCsvMobName);

        if (s == null)
        {
            return;
        }

        if (s.hp > 0f) maxHp = Mathf.Max(1, Mathf.RoundToInt(s.hp));
        if (s.speed >= 0f) moveSpeed = s.speed;
        if (s.atk1 > 0f) floorDamageOverride = Mathf.RoundToInt(s.atk1);
        if (s.atk2 > 0f) shotDamageOverride = Mathf.RoundToInt(s.atk2);

        Debug.Log($"{name}: GameControl 보스 스탯 적용 / HP={maxHp}, Speed={moveSpeed}, FLOOR={floorDamageOverride}, SHOT={shotDamageOverride}");
    }

    bool IsPlayerDead()
    {
        if (player == null)
        {
            return false;
        }

        if (cachedPlayerHealth == null)
        {
            cachedPlayerHealth = player.GetComponent<PlayerHealth>();

            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = player.GetComponentInChildren<PlayerHealth>();
            }

            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = player.GetComponentInParent<PlayerHealth>();
            }
        }

        return cachedPlayerHealth != null && cachedPlayerHealth.IsDead;
    }

    bool TryUseKingSkill()
    {
        float floorWeight = Mathf.Max(0f, kingFloorChance);
        float shotWeight = Mathf.Max(0f, kingShotChance);
        float total = floorWeight + shotWeight;

        if (total <= 0f)
        {
            return false;
        }

        if (Random.Range(0f, total) < floorWeight)
        {
            StartBossAction(KingFloorRoutine());
        }
        else
        {
            StartBossAction(KingSphereShotRoutine());
        }

        return true;
    }

    // 스킬 목표 지점: 플레이어 타겟이면 플레이어, 아니면 댐
    Vector3 GetKingSkillTargetPosition()
    {
        if (targetMode == TargetMode.Player && player != null && !IsPlayerDead())
        {
            return GetPlayerCenter();
        }

        if (currentDamPart == null || currentDamCollider == null)
        {
            FindNearestDam();
        }

        if (currentDamCollider != null)
        {
            return GetClosestPointOnDam();
        }

        return player != null ? player.position : transform.position + transform.forward * 5f;
    }

    Vector3 FindKingGroundPoint(Vector3 position)
    {
        Vector3 origin = new Vector3(position.x, position.y + 20f, position.z);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            60f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        float best = float.MinValue;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;

            if (c == null || hit.normal.y < 0.5f)
            {
                continue;
            }

            if (c.transform.IsChildOf(transform) ||
                c.GetComponentInParent<TopDownPlayerController>() != null ||
                c.GetComponentInParent<MonsterChaser>() != null ||
                c.GetComponentInParent<BossMonsterChaser>() != null)
            {
                continue;
            }

            // 지면 근처(목표 높이 ±3) 중 가장 높은 면
            if (hit.point.y > position.y + 3f)
            {
                continue;
            }

            if (hit.point.y > best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        if (found)
        {
            return new Vector3(position.x, best, position.z);
        }

        float fallbackY = player != null && playerCollider != null
            ? playerCollider.bounds.min.y
            : transform.position.y;

        return new Vector3(position.x, fallbackY, position.z);
    }

    GameObject CreateKingSphere(Vector3 position, float scale)
    {
        GameObject sphere;

        if (kingSpherePrefab != null)
        {
            sphere = Instantiate(kingSpherePrefab, position, Quaternion.identity);
        }
        else
        {
            sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = position;

            Renderer r = sphere.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.45f, 0.95f, 0.45f);
            }
        }

        sphere.name = "King_Sphere";
        sphere.transform.localScale *= Mathf.Max(0.01f, scale);

        // 물리 충돌은 쓰지 않습니다 (피해는 거리로 판정)
        foreach (Collider c in sphere.GetComponentsInChildren<Collider>())
        {
            Destroy(c);
        }

        kingSpawnedObjects.Add(sphere);
        return sphere;
    }

    // 바닥에 눕힌 스프라이트 (장판 / 경고 라인)
    SpriteRenderer CreateGroundSprite(string objectName, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = sortingOrder;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        return sr;
    }

    static void SetGroundSpriteSize(SpriteRenderer sr, float width, float length)
    {
        if (sr == null)
        {
            return;
        }

        Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

        sr.transform.localScale = new Vector3(
            size.x > 0f ? width / size.x : width,
            size.y > 0f ? length / size.y : length,
            1f
        );
    }

    void ClearKingWarnings()
    {
        for (int i = 0; i < kingWarningObjects.Count; i++)
        {
            if (kingWarningObjects[i] != null)
            {
                Destroy(kingWarningObjects[i]);
            }
        }

        kingWarningObjects.Clear();
    }

    void FaceDirectionImmediate(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion rot = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = rot;

        if (rb != null)
        {
            rb.rotation = rot;
        }
    }

    // ---------------- Skill 1 : FLOOR ----------------

    IEnumerator KingFloorRoutine()
    {
        currentAction = BossAction.FloorAttack;

        StopMove();
        SetMoveAnimationDirect(0f);
        FaceDirectionImmediate(GetKingSkillTargetPosition() - transform.position);
        PlayTrigger(floorParameterName);

        yield return new WaitForSeconds(Mathf.Max(0f, floorCastTime));

        int count = Mathf.Max(1, floorCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 target = GetKingSkillTargetPosition();

            if (i > 0 && floorTargetSpread > 0f)
            {
                Vector2 r = Random.insideUnitCircle * floorTargetSpread;
                target += new Vector3(r.x, 0f, r.y);
            }

            Vector3 ground = FindKingGroundPoint(target);
            StartCoroutine(KingFloorDropRoutine(ground));
        }

        yield return new WaitForSeconds(Mathf.Max(0f, floorFallDuration + floorRecoveryTime));

        FinishCurrentAction();
    }

    IEnumerator KingFloorDropRoutine(Vector3 ground)
    {
        float diameter = floorRadius * 2f;

        // 낙하 지점 그림자(예고)
        SpriteRenderer shadow = null;

        if (floorZoneSprite != null)
        {
            shadow = CreateGroundSprite("King_FloorWarning", floorZoneSprite, new Color(0.2f, 0.9f, 0.2f, 0.25f), 5);
            shadow.transform.position = ground + Vector3.up * 0.04f;
            shadow.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetGroundSpriteSize(shadow, diameter * 0.3f, diameter * 0.3f);
            kingSpawnedObjects.Add(shadow.gameObject);
        }

        // 공중에서 구체 낙하
        Vector3 start = ground + Vector3.up * floorDropHeight;
        GameObject sphere = CreateKingSphere(start, floorSphereScale);
        Animator sphereAnimator = sphere.GetComponentInChildren<Animator>();

        if (sphereAnimator != null)
        {
            sphereAnimator.speed = 0f;
        }

        float duration = Mathf.Max(0.05f, floorFallDuration);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            if (sphere != null)
            {
                sphere.transform.position = Vector3.Lerp(start, ground, k * k);
            }

            if (shadow != null)
            {
                float s = Mathf.Lerp(0.3f, 1f, k);
                SetGroundSpriteSize(shadow, diameter * s, diameter * s);
            }

            yield return null;
        }

        if (shadow != null)
        {
            kingSpawnedObjects.Remove(shadow.gameObject);
            Destroy(shadow.gameObject);
        }

        if (cameraShake != null)
        {
            cameraShake.Shake(landingShakeDuration * 0.6f, landingShakeStrength * 0.6f);
        }

        // 착지 → 구체 터짐(드롭 애니메이션) + 장판 생성
        if (sphere != null)
        {
            sphere.transform.position = ground;

            if (sphereAnimator != null)
            {
                sphereAnimator.speed = 1f;
                sphereAnimator.Play(0, 0, 0f);
            }
        }

        SpriteRenderer zone = null;

        if (floorZoneSprite != null)
        {
            zone = CreateGroundSprite("King_FloorZone", floorZoneSprite, new Color(1f, 1f, 1f, 0.85f), 6);
            zone.transform.position = ground + Vector3.up * 0.05f;
            zone.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            SetGroundSpriteSize(zone, diameter, diameter);
            kingSpawnedObjects.Add(zone.gameObject);
        }

        int damage = floorDamageOverride > 0
            ? floorDamageOverride
            : CalculateBossDamage(floorDamageMultiplier);
        float life = 0f;
        float tick = 0f;
        float fade = Mathf.Min(0.5f, floorLifetime * 0.3f);

        while (life < floorLifetime)
        {
            life += Time.deltaTime;
            tick -= Time.deltaTime;

            if (tick <= 0f && player != null && !IsPlayerDead())
            {
                Vector3 p = player.position;
                float dx = p.x - ground.x;
                float dz = p.z - ground.z;

                if (dx * dx + dz * dz <= floorRadius * floorRadius)
                {
                    DamagePlayer(damage);
                    tick = Mathf.Max(0.05f, floorTickInterval);
                }
            }

            if (zone != null && life > floorLifetime - fade)
            {
                Color c = zone.color;
                c.a = Mathf.Lerp(0.85f, 0f, (life - (floorLifetime - fade)) / Mathf.Max(0.01f, fade));
                zone.color = c;
            }

            yield return null;
        }

        if (zone != null)
        {
            kingSpawnedObjects.Remove(zone.gameObject);
            Destroy(zone.gameObject);
        }

        if (sphere != null)
        {
            kingSpawnedObjects.Remove(sphere);
            Destroy(sphere);
        }
    }

    // ---------------- Skill 2 : 세로형 구체 발사 ----------------

    IEnumerator KingSphereShotRoutine()
    {
        currentAction = BossAction.SphereShot;

        StopMove();
        SetMoveAnimationDirect(0f);

        Vector3 target = GetKingSkillTargetPosition();
        Vector3 baseDir = target - transform.position;
        baseDir.y = 0f;

        if (baseDir.sqrMagnitude < 0.01f)
        {
            baseDir = transform.forward;
            baseDir.y = 0f;
        }

        baseDir.Normalize();
        FaceDirectionImmediate(baseDir);

        int count = Mathf.Max(1, shotCount);
        Vector3[] dirs = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float angle = count > 1 && shotSpreadAngle > 0f
                ? (i - (count - 1) * 0.5f) * shotSpreadAngle
                : 0f;

            dirs[i] = Quaternion.Euler(0f, angle, 0f) * baseDir;
        }

        Vector3 ground = FindKingGroundPoint(transform.position);
        Vector3 origin = new Vector3(transform.position.x, ground.y, transform.position.z);

        // 붉은 경고 라인 (같은 방향은 한 줄만)
        List<SpriteRenderer> warnings = new List<SpriteRenderer>();
        List<Vector3> drawnDirs = new List<Vector3>();

        if (shotWarningSprite != null)
        {
            foreach (Vector3 d in dirs)
            {
                bool dup = false;
                foreach (Vector3 dd in drawnDirs)
                {
                    if (Vector3.Dot(dd, d) > 0.999f) { dup = true; break; }
                }

                if (dup) continue;
                drawnDirs.Add(d);

                SpriteRenderer sr = CreateGroundSprite("King_ShotWarning", shotWarningSprite, new Color(1f, 1f, 1f, 0.8f), 7);
                sr.transform.rotation = Quaternion.LookRotation(d, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
                kingWarningObjects.Add(sr.gameObject);
                warnings.Add(sr);
            }
        }

        PlayTrigger(shotParameterName);

        // 경고: 보스 앞에서부터 라인이 쭉 뻗어나가며 깜빡임
        float wt = 0f;
        float warnDuration = Mathf.Max(0.05f, shotWarningDuration);

        while (wt < warnDuration)
        {
            wt += Time.deltaTime;
            float k = Mathf.Clamp01(wt / (warnDuration * 0.5f));
            float len = Mathf.Lerp(0.5f, shotLength, k);
            float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(wt * 14f));

            for (int i = 0; i < warnings.Count; i++)
            {
                SpriteRenderer sr = warnings[i];
                if (sr == null) continue;

                Vector3 d = drawnDirs[i];
                sr.transform.position = origin + d * (len * 0.5f) + Vector3.up * 0.06f;
                SetGroundSpriteSize(sr, shotWidth, len);

                Color c = sr.color;
                c.a = blink;
                sr.color = c;
            }

            yield return null;
        }

        ClearKingWarnings();

        // 구체 발사
        int damage = shotDamageOverride > 0
            ? shotDamageOverride
            : CalculateBossDamage(shotDamageMultiplier);

        for (int i = 0; i < count; i++)
        {
            if (isDead)
            {
                yield break;
            }

            StartCoroutine(KingShotSphereRoutine(origin, dirs[i], damage));

            if (i < count - 1)
            {
                yield return new WaitForSeconds(Mathf.Max(0f, shotInterval));
            }
        }

        yield return new WaitForSeconds(Mathf.Max(0f, shotRecoveryTime));

        FinishCurrentAction();
    }

    IEnumerator KingShotSphereRoutine(Vector3 origin, Vector3 dir, int damage)
    {
        Vector3 start = origin + Vector3.up * shotSphereHeight + dir * 1f;
        GameObject sphere = CreateKingSphere(start, shotSphereScale);

        float travelled = 0f;
        bool hitPlayer = false;
        bool hitDam = false;

        while (travelled < shotLength && sphere != null)
        {
            float step = shotSpeed * Time.deltaTime;
            travelled += step;
            sphere.transform.position += dir * step;
            sphere.transform.Rotate(Vector3.right, 720f * Time.deltaTime, Space.Self);

            Vector3 pos = sphere.transform.position;

            if (!hitPlayer && player != null && !IsPlayerDead())
            {
                Vector3 p = player.position;
                float dx = p.x - pos.x;
                float dz = p.z - pos.z;

                if (dx * dx + dz * dz <= shotHitRadius * shotHitRadius)
                {
                    hitPlayer = true;
                    DamagePlayer(damage);
                }
            }

            if (!hitDam && targetMode == TargetMode.Dam && currentDamPart != null && currentDamCollider != null)
            {
                Vector3 cp = currentDamCollider.ClosestPoint(pos);
                cp.y = pos.y;

                if ((cp - pos).sqrMagnitude <= shotHitRadius * shotHitRadius)
                {
                    hitDam = true;
                    currentDamPart.TakeDamage(damage);
                }
            }

            yield return null;
        }

        if (sphere != null)
        {
            kingSpawnedObjects.Remove(sphere);
            Destroy(sphere);
        }
    }

    public int GetCurrentHp()
    {
        return currentHp;
    }

    public int GetMaxHp()
    {
        return maxHp;
    }

    public bool IsDead()
    {
        return isDead;
    }
}