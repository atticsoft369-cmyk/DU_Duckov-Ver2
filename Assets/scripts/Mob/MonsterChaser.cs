using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class MonsterChaser : MonoBehaviour
{
    [Header("Monster CSV Settings")]
    [Tooltip("Assets/Resources 폴더의 몬스터 정보 CSV 파일명. 확장자는 입력하지 않습니다.")]
    public string monsterInfoTableFileName = "Mob_Info";

    [Tooltip("CSV에서 값을 찾지 못했을 때 Inspector 기본값을 그대로 사용할지 여부")]
    public bool useInspectorValuesAsFallback = true;

    private class MonsterCsvData
    {
        public int id;
        public string mob;
        public int hp;
        public int atk;
        public float speed;
    }

    private static readonly Dictionary<string, MonsterCsvData>
        monsterCsvCache =
            new Dictionary<string, MonsterCsvData>();

    private static string loadedMonsterInfoTableName = string.Empty;
    private static bool monsterCsvLoaded;

    private enum TargetMode
    {
        Dam,
        Player
    }

    [Header("Target")]
    public Transform player;

    [Header("Target Settings")]
    [Tooltip("플레이어가 이 거리보다 멀어지면 다시 댐을 타겟으로 변경")]
    public float playerAggroRange = 25f;


    [Header("HP Settings")]
    [Min(1)]
    public int maxHp = 3;

    [SerializeField]
    private int currentHp;

    [Header("HP Bar Settings")]
    [Tooltip("Canvas UI 형태로 제작한 HP바 프리팹")]
    public GameObject hpBarPrefab;

    [Tooltip("HP바를 생성할 부모 Canvas")]
    public Canvas hpBarCanvas;

    [Tooltip("월드 좌표를 화면 좌표로 변환할 카메라")]
    public Camera hpBarCamera;

    [Tooltip("몬스터 위치를 기준으로 한 HP바 월드 좌표 오프셋")]
    public Vector3 hpBarWorldOffset =
        new Vector3(0f, 0.5f, 0f);

    [Tooltip("마지막 피격 후 HP바가 유지되는 시간. 0이면 계속 표시")]
    [Min(0f)]
    public float hpBarDisplayDuration = 3f;

    [Header("Move Settings")]
    [Min(0f)]
    public float moveSpeed = 2.5f;

    [Min(0f)]
    public float rotationSpeed = 10f;

    [Header("Attack Settings")]
    [Min(0)]
    public int attackDamage = 1;

    [Min(0.01f)]
    public float attackInterval = 1f;

    [Tooltip("공격 모션 시작 후 실제 피해가 들어가기까지의 시간 (0이면 즉시)")]
    public float attackHitDelay = 0f;

    [Min(0f)]
    public float attackRange = 1.5f;

    [Tooltip("콜라이더끼리 이 간격 이내로 붙어야 공격합니다 (몸 표면 사이 거리, m)")]
    [Min(0f)]
    public float attackContactGap = 0.25f;

    [Header("Hit Settings")]
    [Min(0f)]
    public float hitStopDuration = 0.3f;

    [Header("Animation Settings")]
    public string speedParameterName = "Speed";
    public string attackParameterName = "Attack";
    public string hitParameterName = "Hit";
    public string dieParameterName = "Die";

    [Tooltip("사망 애니메이션 후 몬스터 삭제까지의 시간")]
    [Min(0f)]
    public float deathAnimationDuration = 1.5f;

    [Header("Model")]
    [Tooltip("몬스터 모델의 Animator. 비워두면 자신 또는 자식의 Animator를 사용합니다.")]
    public Animator modelAnimator;

    [Header("Armor Phase (아머 슬라임)")]
    [Tooltip("켜면 첫 번째 HP 0에서 방패가 떨어지고(Drop) HP가 다시 가득 찹니다. 두 번째 HP 0에서 사망.")]
    public bool hasArmorPhase = false;

    public string dropParameterName = "Drop";

    [Tooltip("Drop 모션 시간. 이 동안 몬스터는 멈춰 있고, 끝나면 방패를 숨깁니다.")]
    [Min(0f)]
    public float dropAnimationDuration = 1.2f;

    [Tooltip("Drop 후 숨길 방패 렌더러의 오브젝트 이름(부분 일치, 대소문자 무시)")]
    public string[] armorPartKeywords = new string[] { "armor" };

    [Tooltip("이름이 이 단어를 포함하면 방패로 보지 않습니다 (몸통/눈)")]
    public string[] armorPartExcludeKeywords = new string[] { "body", "eye" };

    private bool armorBroken;

    // 방패 버리는(Drop) 모션 중에는 무적
    private bool isDroppingArmor;

    [Header("Death Effect")]
    public GameObject deathEffectPrefab;

    [Min(0f)]
    public float deathEffectDestroyDelay = 2f;

    public float deathEffectHeightOffset = 0.2f;

    private TargetMode targetMode =
        TargetMode.Dam;

    private DamPart currentDamPart;
    private Collider currentDamCollider;

    private Rigidbody rb;
    private Animator animator;
    private WaveMobTracker waveMobTracker;

    private float attackTimer;
    private float hitStopTimer;

    private bool isDead;

    private GameObject hpBarInstance;
    private MonsterHpBarUI hpBarUI;
    private float lastDamageTime = -999f;
    private bool hasBeenDamaged;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // 새 모델이 자식으로 들어간 경우 그 모델의 Animator를 사용합니다.
        animator = modelAnimator != null
            ? modelAnimator
            : GetComponent<Animator>();

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (animator == null)
        {
            Debug.LogWarning(
                name + ": Animator를 찾지 못했습니다.",
                this
            );
        }

        waveMobTracker =
            GetComponent<WaveMobTracker>();

        ApplyMonsterStatsFromCsv();
        ApplyGameControlStats();
        FitColliderToModel();

        currentHp =
            Mathf.Max(1, maxHp);

        isDead = false;

        SetupRigidbody();
        FindPlayer();
        SetTargetToDam();

        /*
         * 보스 캐스트바처럼 몬스터 생성 시
         * Canvas 아래에 HP바를 미리 생성합니다.
         *
         * 생성 직후에는 숨겨집니다.
         */
        SpawnHpBar();
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        if (hitStopTimer > 0f)
        {
            hitStopTimer -=
                Time.fixedDeltaTime;

            StopMove();
            SetAnimation(0f);

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
    private void Update()
    {
        if (isDead)
        {
            return;
        }

        UpdateHpBarVisibility();
    }

    private void UpdateHpBarVisibility()
    {
        if (!hasBeenDamaged)
        {
            return;
        }

        if (hpBarInstance == null)
        {
            return;
        }

        /*
        * 이 몬스터가 마지막으로 공격받은 뒤
        * 설정된 시간이 지나면 자신의 HP바만 숨깁니다.
        */
        if (hpBarDisplayDuration > 0f &&
            Time.time - lastDamageTime >= hpBarDisplayDuration)
        {
            hpBarInstance.SetActive(false);
            hasBeenDamaged = false;
        }
    }

    private void ApplyMonsterStatsFromCsv()
    {
        LoadMonsterInfoCsvIfNeeded();

        string monsterName =
            GetCsvLookupMonsterName();

        if (!monsterCsvCache.TryGetValue(
                monsterName,
                out MonsterCsvData data))
        {
            Debug.LogWarning(
                name +
                ": Mob_Info.csv에서 몬스터 정보를 찾지 못했습니다. " +
                "검색 이름 = " +
                monsterName,
                this
            );

            return;
        }

        maxHp =
            Mathf.Max(1, data.hp);

        attackDamage =
            Mathf.Max(0, data.atk);

        moveSpeed =
            Mathf.Max(0f, data.speed);

        Debug.Log(
            name +
            ": CSV 스탯 적용 / " +
            "HP=" +
            maxHp +
            ", ATK=" +
            attackDamage +
            ", Speed=" +
            moveSpeed,
            this
        );
    }

    private void LoadMonsterInfoCsvIfNeeded()
    {
        if (monsterCsvLoaded &&
            loadedMonsterInfoTableName ==
            monsterInfoTableFileName)
        {
            return;
        }

        monsterCsvCache.Clear();
        monsterCsvLoaded = false;
        loadedMonsterInfoTableName =
            monsterInfoTableFileName;

        TextAsset csvFile =
            Resources.Load<TextAsset>(
                monsterInfoTableFileName
            );

        if (csvFile == null)
        {
            string message =
                "몬스터 정보 CSV를 찾지 못했습니다: " +
                "Assets/Resources/" +
                monsterInfoTableFileName +
                ".csv";

            if (useInspectorValuesAsFallback)
            {
                Debug.LogWarning(
                    message +
                    " / Inspector 값을 사용합니다.",
                    this
                );

                return;
            }

            Debug.LogError(message, this);
            return;
        }

        string[] lines =
            csvFile.text.Split('\n');

        if (lines.Length <= 1)
        {
            Debug.LogWarning(
                monsterInfoTableFileName +
                ".csv에 데이터가 없습니다.",
                this
            );

            return;
        }

        string[] headers =
            SplitCsvLine(lines[0]);

        int idIndex =
            FindColumnIndex(headers, "id");

        int mobIndex =
            FindColumnIndex(headers, "mob");

        int hpIndex =
            FindColumnIndex(headers, "hp");

        int atkIndex =
            FindColumnIndex(headers, "atk");

        int speedIndex =
            FindColumnIndex(headers, "speed");

        if (mobIndex < 0 ||
            hpIndex < 0 ||
            atkIndex < 0 ||
            speedIndex < 0)
        {
            Debug.LogError(
                monsterInfoTableFileName +
                ".csv 헤더에 mob, hp, atk, speed 컬럼이 필요합니다.",
                this
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
                SplitCsvLine(line);

            string mobName =
                GetCsvString(
                    columns,
                    mobIndex
                );

            if (string.IsNullOrWhiteSpace(mobName))
            {
                continue;
            }

            MonsterCsvData data =
                new MonsterCsvData
                {
                    id =
                        GetCsvInt(
                            columns,
                            idIndex,
                            0
                        ),
                    mob =
                        mobName,
                    hp =
                        Mathf.Max(
                            1,
                            GetCsvInt(
                                columns,
                                hpIndex,
                                1
                            )
                        ),
                    atk =
                        Mathf.Max(
                            0,
                            GetCsvInt(
                                columns,
                                atkIndex,
                                0
                            )
                        ),
                    speed =
                        Mathf.Max(
                            0f,
                            GetCsvFloat(
                                columns,
                                speedIndex,
                                0f
                            )
                        )
                };

            monsterCsvCache[mobName] =
                data;
        }

        monsterCsvLoaded = true;

        Debug.Log(
            monsterInfoTableFileName +
            ".csv 로드 완료 / " +
            monsterCsvCache.Count +
            "종",
            this
        );
    }

    [Header("Collider Fit")]
    [Tooltip("실제 보이는 모델 크기에 맞춰 Sphere Collider를 자동 조정 (크기를 바꿔도 멀리서 때리지 않게)")]
    public bool fitColliderToModel = true;

    [Tooltip("모델 가로 반경 대비 콜라이더 반경 비율")]
    [Range(0.3f, 1.5f)]
    public float colliderFitRatio = 0.85f;

    // 보이는 모델(켜진 MeshRenderer/SkinnedMeshRenderer) 범위로 Sphere Collider 크기/중심을 맞춥니다.
    private void FitColliderToModel()
    {
        if (!fitColliderToModel)
        {
            return;
        }

        SphereCollider sphere = GetComponent<SphereCollider>();

        if (sphere == null)
        {
            return;
        }

        bool found = false;
        Bounds b = new Bounds(transform.position, Vector3.zero);

        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) ||
                !r.enabled ||
                r.gameObject.name.Contains("Minimap"))
            {
                continue;
            }

            if (!found)
            {
                b = r.bounds;
                found = true;
            }
            else
            {
                b.Encapsulate(r.bounds);
            }
        }

        if (!found)
        {
            return;
        }

        Vector3 lossy = transform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));

        if (scale < 0.0001f)
        {
            return;
        }

        float worldRadius = Mathf.Max(b.extents.x, b.extents.z) * colliderFitRatio;

        sphere.center = transform.InverseTransformPoint(b.center);
        sphere.radius = worldRadius / scale;

        selfColliderCache = null;
    }

    // GameControl.csv [MONSTER] 값이 있으면 Mob_Info보다 우선 적용
    private void ApplyGameControlStats()
    {
        GameControl.MonsterStat s = GameControl.GetMonster(GetCsvLookupMonsterName());

        if (s == null)
        {
            return;
        }

        if (s.hp > 0f) maxHp = Mathf.Max(1, Mathf.RoundToInt(s.hp));
        if (s.speed >= 0f) moveSpeed = s.speed;
        if (s.atk >= 0f) attackDamage = Mathf.Max(0, Mathf.RoundToInt(s.atk));

        Debug.Log($"{name}: GameControl 스탯 적용 / HP={maxHp}, ATK={attackDamage}, Speed={moveSpeed}", this);
    }

    private string GetCsvLookupMonsterName()
    {
        string monsterName =
            gameObject.name;

        const string cloneSuffix =
            "(Clone)";

        if (monsterName.EndsWith(
                cloneSuffix,
                System.StringComparison.Ordinal))
        {
            monsterName =
                monsterName.Substring(
                    0,
                    monsterName.Length -
                    cloneSuffix.Length
                );
        }

        return monsterName.Trim();
    }

    private static int FindColumnIndex(
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

    private static string[] SplitCsvLine(
        string line)
    {
        // 현재 Mob_Info.csv는 단순 값 형식이므로 쉼표 기준으로 처리합니다.
        // mob 이름에 쉼표를 사용할 경우 별도 CSV 파서가 필요합니다.
        return line.Split(',');
    }

    private static string GetCsvString(
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

    private static int GetCsvInt(
        string[] columns,
        int index,
        int fallback)
    {
        string value =
            GetCsvString(columns, index);

        return int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out int result)
                ? result
                : fallback;
    }

    private static float GetCsvFloat(
        string[] columns,
        int index,
        float fallback)
    {
        string value =
            GetCsvString(columns, index);

        return float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float result)
                ? result
                : fallback;
    }

    private void SetupRigidbody()
    {
        if (rb == null)
        {
            Debug.LogWarning(
                name + ": Rigidbody가 없습니다.",
                this
            );

            return;
        }

        rb.useGravity = false;

        rb.constraints =
            RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private void FindPlayer()
    {
        if (player != null)
        {
            return;
        }

        GameObject foundPlayer =
            GameObject.FindGameObjectWithTag(
                "Player"
            );

        if (foundPlayer != null)
        {
            player =
                foundPlayer.transform;
        }
    }

    private void UpdateTargetState()
    {
        if (targetMode == TargetMode.Player)
        {
            if (player == null)
            {
                FindPlayer();

                if (player == null)
                {
                    SetTargetToDam();
                    return;
                }
            }

            float distanceToPlayer =
                GetHorizontalDistance(
                    transform.position,
                    player.position
                );

            if (distanceToPlayer >
                playerAggroRange)
            {
                SetTargetToDam();
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

    private void ChasePlayer()
    {
        if (player == null)
        {
            StopMove();
            SetAnimation(0f);
            return;
        }

        Collider playerCollider =
            player.GetComponent<Collider>();

        if (playerCollider == null)
        {
            playerCollider =
                player.GetComponentInChildren<Collider>();
        }

        Vector3 targetPosition;

        if (playerCollider != null)
        {
            targetPosition =
                playerCollider.ClosestPoint(
                    transform.position
                );
        }
        else
        {
            targetPosition =
                player.position;
        }

        targetPosition.y =
            transform.position.y;

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        float distance =
            GetSurfaceDistanceTo(targetPosition);

        if (distance <= attackContactGap)
        {
            StopMove();

            Vector3 lookDirection =
                player.position -
                transform.position;

            lookDirection.y = 0f;

            RotateToTarget(
                lookDirection
            );

            SetAnimation(0f);
            AttackPlayer();

            return;
        }

        // 콜라이더가 닿기 직전까지만 이동해서 플레이어/댐을 밀어내지 않게 합니다.
        MoveTo(direction, distance - attackContactGap * 0.5f);
    }

    private void ChaseDam()
    {
        if (currentDamPart == null ||
            currentDamCollider == null)
        {
            FindNearestDam();

            if (currentDamPart == null ||
                currentDamCollider == null)
            {
                StopMove();
                SetAnimation(0f);
                return;
            }
        }

        Vector3 targetPosition =
            GetClosestPointOnDam();

        Vector3 direction =
            targetPosition -
            transform.position;

        direction.y = 0f;

        float distance =
            GetSurfaceDistanceTo(targetPosition);

        if (distance <= attackContactGap)
        {
            StopMove();
            RotateToTarget(direction);
            SetAnimation(0f);

            AttackDam();
            return;
        }

        // 콜라이더가 닿기 직전까지만 이동해서 플레이어/댐을 밀어내지 않게 합니다.
        MoveTo(direction, distance - attackContactGap * 0.5f);
    }

    private Vector3 GetClosestPointOnDam()
    {
        if (currentDamPart == null)
        {
            return transform.position;
        }

        if (currentDamCollider == null)
        {
            return currentDamPart
                .transform
                .position;
        }

        Vector3 closestPoint =
            currentDamCollider.ClosestPoint(
                transform.position
            );

        closestPoint.y =
            transform.position.y;

        return closestPoint;
    }

    private void FindNearestDam()
    {
        DamPart[] damParts =
            FindObjectsByType<DamPart>(
                FindObjectsSortMode.None
            );

        float nearestDistance =
            Mathf.Infinity;

        DamPart nearestPart = null;
        Collider nearestCollider = null;

        foreach (DamPart damPart in damParts)
        {
            if (damPart == null)
            {
                continue;
            }

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

        if (currentDamPart == null)
        {
            Debug.LogWarning(
                name + ": DamPart를 찾지 못했습니다.",
                this
            );
        }
    }

    private void SetTargetToDam()
    {
        targetMode =
            TargetMode.Dam;

        FindNearestDam();
    }

    private void SetTargetToPlayer()
    {
        FindPlayer();

        if (player == null)
        {
            Debug.LogWarning(
                name + ": Player를 찾지 못했습니다.",
                this
            );

            return;
        }

        targetMode =
            TargetMode.Player;
    }

    private void AttackDam()
    {
        attackTimer -=
            Time.fixedDeltaTime;

        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer =
            attackInterval;

        if (currentDamPart == null)
        {
            return;
        }

        PlayAttackAnimation();

        if (attackHitDelay > 0f)
        {
            StartCoroutine(DelayedDamDamage(currentDamPart));
            return;
        }

        currentDamPart.TakeDamage(
            attackDamage
        );
    }

    private void AttackPlayer()
    {
        attackTimer -=
            Time.fixedDeltaTime;

        if (attackTimer > 0f)
        {
            return;
        }

        attackTimer =
            attackInterval;

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
                name + ": PlayerHealth를 찾지 못했습니다.",
                this
            );

            return;
        }

        PlayAttackAnimation();

        if (attackHitDelay > 0f)
        {
            StartCoroutine(DelayedPlayerDamage(playerHealth));
            return;
        }

        playerHealth.TakeDamage(
            attackDamage
        );
    }

    // 공격 모션의 타격 순간(예: 앞으로 돌진하는 프레임)에 맞춰 피해를 줍니다.
    private IEnumerator DelayedPlayerDamage(PlayerHealth playerHealth)
    {
        yield return new WaitForSeconds(attackHitDelay);

        if (!isDead && playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private IEnumerator DelayedDamDamage(DamPart damPart)
    {
        yield return new WaitForSeconds(attackHitDelay);

        if (!isDead && damPart != null)
        {
            damPart.TakeDamage(attackDamage);
        }
    }

    [Header("Obstacle Avoidance")]
    [Tooltip("바위/나무 등 장애물에 막히면 옆으로 돌아서 이동합니다.")]
    public bool avoidObstacles = true;

    [Tooltip("앞쪽 장애물 검사 거리")]
    public float avoidLookAhead = 1.2f;

    [Tooltip("이 시간 동안 거의 못 움직이면 끼인 것으로 보고 옆으로 빠져나갑니다.")]
    public float stuckCheckTime = 0.4f;

    private int avoidSide = 1;
    private float stuckTimer;
    private float forcedDetourTimer;
    private Vector3 lastMovePosition;
    private bool hasLastMovePosition;

    private static readonly float[] AvoidAngles = { 35f, 70f, 105f, 140f };

    private void MoveTo(
        Vector3 direction,
        float maxStep = float.PositiveInfinity)
    {
        if (rb == null)
        {
            return;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <=
            0.001f)
        {
            StopMove();
            SetAnimation(0f);
            return;
        }

        float step =
            Mathf.Min(
                GetCurrentMoveSpeed() * Time.fixedDeltaTime,
                Mathf.Max(0f, maxStep)
            );

        Vector3 moveDirection =
            GetAvoidanceDirection(direction.normalized, step);

        Vector3 nextPosition =
            rb.position +
            moveDirection *
            step;

        rb.MovePosition(
            nextPosition
        );

        RotateToTarget(
            moveDirection
        );

        SetAnimation(1f);
    }

    // 목표 방향이 막혀 있으면 좌/우로 꺾은 방향 중 열린 곳을 고릅니다.
    private Vector3 GetAvoidanceDirection(Vector3 desired, float step)
    {
        if (!avoidObstacles)
        {
            return desired;
        }

        // 끼임 감지: 이동하려는데 거의 못 움직였으면 강제로 옆으로 우회
        if (hasLastMovePosition && step > 0.001f)
        {
            Vector3 moved = rb.position - lastMovePosition;
            moved.y = 0f;

            if (moved.magnitude < step * 0.25f)
            {
                stuckTimer += Time.fixedDeltaTime;

                if (stuckTimer >= stuckCheckTime)
                {
                    stuckTimer = 0f;
                    avoidSide = -avoidSide;
                    forcedDetourTimer = 0.7f;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
        }

        lastMovePosition = rb.position;
        hasLastMovePosition = true;

        if (forcedDetourTimer > 0f)
        {
            forcedDetourTimer -= Time.fixedDeltaTime;

            Vector3 side = Quaternion.Euler(0f, 90f * avoidSide, 0f) * desired;
            if (!IsDirectionBlocked(side))
            {
                return side;
            }

            Vector3 other = Quaternion.Euler(0f, -90f * avoidSide, 0f) * desired;
            if (!IsDirectionBlocked(other))
            {
                avoidSide = -avoidSide;
                return other;
            }
        }

        if (!IsDirectionBlocked(desired))
        {
            return desired;
        }

        foreach (float angle in AvoidAngles)
        {
            for (int k = 0; k < 2; k++)
            {
                int side = k == 0 ? avoidSide : -avoidSide;
                Vector3 dir = Quaternion.Euler(0f, angle * side, 0f) * desired;

                if (!IsDirectionBlocked(dir))
                {
                    avoidSide = side;
                    return dir;
                }
            }
        }

        return desired;
    }

    private bool IsDirectionBlocked(Vector3 dir)
    {
        if (selfColliderCache == null)
        {
            selfColliderCache = GetComponent<Collider>();

            if (selfColliderCache == null)
            {
                selfColliderCache = GetComponentInChildren<Collider>();
            }
        }

        if (selfColliderCache == null)
        {
            return false;
        }

        Bounds b = selfColliderCache.bounds;
        float radius = Mathf.Max(0.1f, Mathf.Min(b.extents.x, b.extents.z) * 0.8f);
        Vector3 origin = b.center;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            radius,
            dir,
            avoidLookAhead,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;

            if (c == null || hit.distance <= 0f)
            {
                continue;
            }

            // 바닥면은 장애물이 아님
            if (hit.normal.y > 0.6f)
            {
                continue;
            }

            Transform t = c.transform;

            if (t.IsChildOf(transform) ||
                c.GetComponentInParent<TopDownPlayerController>() != null ||
                c.GetComponentInParent<MonsterChaser>() != null ||
                c.GetComponentInParent<BossMonsterChaser>() != null ||
                c.GetComponentInParent<DamPart>() != null)
            {
                continue;
            }

            if (currentDamCollider != null && c == currentDamCollider)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void RotateToTarget(
        Vector3 direction)
    {
        if (rb == null)
        {
            return;
        }

        direction.y = 0f;

        if (direction.sqrMagnitude <=
            0.001f)
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
                rotationSpeed *
                Time.fixedDeltaTime
            );

        rb.MoveRotation(
            nextRotation
        );
    }

    private void StopMove()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity =
            Vector3.zero;

        rb.angularVelocity =
            Vector3.zero;
    }

    public void TakeDamage(
        int damage)
    {
        if (isDead || damage <= 0)
        {
            return;
        }

        // 아머 슬라임: 방패 버리는 모션이 끝날 때까지 피해를 받지 않습니다.
        if (isDroppingArmor)
        {
            return;
        }

        currentHp -= damage;

        currentHp =
            Mathf.Clamp(
                currentHp,
                0,
                maxHp
            );

        Debug.Log(
            name +
            ": 몬스터 피격 / HP " +
            currentHp +
            " / " +
            maxHp
        );

        ShowAndUpdateHpBar();

        SetTargetToPlayer();

        hitStopTimer =
            hitStopDuration;

        StopMove();
        SetAnimation(0f);

        if (currentHp <= 0)
        {
            // 아머 슬라임: 1차 사망 -> 방패 드롭 후 HP 회복
            if (hasArmorPhase && !armorBroken)
            {
                BreakArmor();
                return;
            }

            Die();
            return;
        }

        PlayHitAnimation();
    }

    private void BreakArmor()
    {
        armorBroken = true;
        isDroppingArmor = true;
        currentHp = maxHp;

        // Drop 모션 동안 멈춤
        hitStopTimer = Mathf.Max(hitStopTimer, dropAnimationDuration);
        StopMove();
        SetAnimation(0f);

        if (animator != null && !string.IsNullOrWhiteSpace(dropParameterName))
        {
            ResetAnimationTrigger(hitParameterName);
            ResetAnimationTrigger(attackParameterName);
            ResetAnimationTrigger(dropParameterName);
            animator.SetTrigger(dropParameterName);
        }

        ShowAndUpdateHpBar();

        Debug.Log(name + ": 아머 파괴 (1차 사망) / HP 회복 " + currentHp + " / " + maxHp);

        StartCoroutine(HideArmorAfterDrop());
    }

    private IEnumerator HideArmorAfterDrop()
    {
        yield return new WaitForSeconds(dropAnimationDuration);

        // Drop 모션 종료 → 다시 피해를 받을 수 있음
        isDroppingArmor = false;

        if (isDead)
        {
            yield break;
        }

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            string n = r.gameObject.name.ToLowerInvariant();

            bool isArmor = false;
            foreach (string k in armorPartKeywords)
            {
                if (!string.IsNullOrEmpty(k) && n.Contains(k.ToLowerInvariant()))
                {
                    isArmor = true;
                    break;
                }
            }

            if (!isArmor)
            {
                continue;
            }

            foreach (string ex in armorPartExcludeKeywords)
            {
                if (!string.IsNullOrEmpty(ex) && n.Contains(ex.ToLowerInvariant()))
                {
                    isArmor = false;
                    break;
                }
            }

            if (isArmor)
            {
                r.enabled = false;
            }
        }
    }

    private void SpawnHpBar()
    {
        if (hpBarPrefab == null)
        {
            return;
        }

        if (hpBarInstance != null)
        {
            return;
        }

        Transform parent =
            hpBarCanvas != null
                ? hpBarCanvas.transform
                : FindDefaultHpBarParent();

        if (parent == null)
        {
            Debug.LogWarning(
                name +
                ": HP바를 생성할 Canvas를 찾지 못했습니다.",
                this
            );

            return;
        }

        hpBarInstance =
            Instantiate(
                hpBarPrefab,
                parent
            );

        hpBarInstance.name =
            name + "_HpBar";

        hpBarUI =
            hpBarInstance
                .GetComponent<MonsterHpBarUI>();

        if (hpBarUI == null)
        {
            hpBarUI =
                hpBarInstance
                    .GetComponentInChildren<MonsterHpBarUI>(
                        true
                    );
        }

        if (hpBarUI == null)
        {
            Debug.LogError(
                name +
                ": HP바 프리팹에 MonsterHpBarUI가 없습니다.",
                hpBarInstance
            );

            Destroy(hpBarInstance);

            hpBarInstance = null;
            return;
        }

        Camera cameraToUse =
            hpBarCamera != null
                ? hpBarCamera
                : Camera.main;

        hpBarUI.Initialize(
            transform,
            cameraToUse,
            hpBarWorldOffset
        );

        /*
         * 몬스터가 피해를 받기 전에는 숨겨둡니다.
         */
        hpBarInstance.SetActive(false);
    }

    private Transform FindDefaultHpBarParent()
    {
        Canvas canvas =
            FindFirstObjectByType<Canvas>();

        return canvas != null
            ? canvas.transform
            : null;
    }

    private void ShowAndUpdateHpBar()
    {
        if (hpBarInstance == null)
        {
            SpawnHpBar();
        }

        if (hpBarInstance == null ||
            hpBarUI == null)
        {
            return;
        }

        /*
        * 이 몬스터 자신의 피격 시간만 갱신합니다.
        * 다른 몬스터의 HP바에는 영향을 주지 않습니다.
        */
        lastDamageTime = Time.time;
        hasBeenDamaged = true;

        hpBarInstance.SetActive(true);

        float hpRatio =
            maxHp > 0
                ? (float)currentHp / maxHp
                : 0f;

        hpBarUI.SetHp(hpRatio);
    }
    
    private void RemoveHpBar()
    {
        hasBeenDamaged = false;
        lastDamageTime = -999f;

        if (hpBarInstance != null)
        {
            Destroy(hpBarInstance);

            hpBarInstance = null;
            hpBarUI = null;
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        hitStopTimer = 0f;

        // 슬라임 건 게이지 충전 (실제로 처치된 경우만)
        SlimeGunController.NotifyMonsterKilled();
        attackTimer = 0f;

        StopMove();
        RemoveHpBar();

        /*
         * MobTableSpawner가 생성 직후 Tracker를 붙이므로
         * 사망 시 다시 검색합니다.
         */
        if (waveMobTracker == null)
        {
            waveMobTracker =
                GetComponent<WaveMobTracker>();
        }

        /*
         * 실제 HP가 0이 된 경우에만
         * 웨이브 시스템에 사망을 알립니다.
         */
        if (waveMobTracker != null)
        {
            waveMobTracker.NotifyKilled();
        }

        if (animator != null)
        {
            animator.SetFloat(
                speedParameterName,
                0f
            );

            ResetAnimationTrigger(
                attackParameterName
            );

            ResetAnimationTrigger(
                hitParameterName
            );

            ResetAnimationTrigger(
                dieParameterName
            );

            animator.SetTrigger(
                dieParameterName
            );
        }

        DisableMonsterPhysics();

        StartCoroutine(
            DeathRoutine()
        );
    }

    private void PlayAttackAnimation()
    {
        if (animator == null ||
            isDead)
        {
            return;
        }

        ResetAnimationTrigger(
            attackParameterName
        );

        animator.SetTrigger(
            attackParameterName
        );
    }

    private void PlayHitAnimation()
    {
        if (animator == null ||
            isDead)
        {
            return;
        }

        ResetAnimationTrigger(
            hitParameterName
        );

        animator.SetTrigger(
            hitParameterName
        );
    }

    private void ResetAnimationTrigger(
        string triggerName)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.ResetTrigger(
            triggerName
        );
    }

    private void DisableMonsterPhysics()
    {
        Collider[] colliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.isKinematic = true;
        }
    }

    private IEnumerator DeathRoutine()
    {
        yield return new WaitForSeconds(
            deathAnimationDuration
        );

        SpawnDeathEffect();

        Destroy(gameObject);
    }

    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            return;
        }

        Vector3 effectPosition =
            transform.position +
            Vector3.up *
            deathEffectHeightOffset;

        // 프리팹 자체 회전(BigExplosion은 X -90 = 위쪽으로 분출)을 유지해야 땅속으로 묻히지 않습니다.
        GameObject effect =
            Instantiate(
                deathEffectPrefab,
                effectPosition,
                deathEffectPrefab.transform.rotation
            );

        Destroy(
            effect,
            GetEffectLifetime(effect, deathEffectDestroyDelay)
        );
    }

    // 이펙트 프리팹의 모든 파티클이 끝날 때까지의 시간 (중간에 잘리지 않도록)
    public static float GetEffectLifetime(GameObject effect, float minimum)
    {
        float longest = minimum;

        foreach (ParticleSystem ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = ps.main;
            float total =
                main.startDelay.constantMax +
                main.duration +
                main.startLifetime.constantMax;

            if (main.loop)
            {
                continue;
            }

            longest = Mathf.Max(longest, total);
        }

        return longest + 0.2f;
    }

    private void OnCollisionEnter(
        Collision collision)
    {
        if (isDead ||
            collision == null)
        {
            return;
        }

        if (!collision.gameObject.CompareTag(
                "Bullet"))
        {
            return;
        }

        int appliedDamage = 1;

        TopDownBullet bullet =
            collision.gameObject.GetComponent<TopDownBullet>();

        if (bullet == null)
        {
            bullet =
                collision.gameObject.GetComponentInChildren<TopDownBullet>();
        }

        if (bullet != null)
        {
            // 총알이 이미 명중 처리됐다면 중복 피해를 주지 않습니다.
            if (!bullet.TryConsumeHit())
            {
                return;
            }

            appliedDamage =
                Mathf.Max(0, bullet.damage);
        }

        TakeDamage(appliedDamage);

        Destroy(
            collision.gameObject
        );
    }

    private void OnTriggerEnter(
        Collider other)
    {
        if (isDead ||
            other == null)
        {
            return;
        }

        GameObject bulletObject =
            other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.gameObject;

        bool isBullet =
            bulletObject.CompareTag("Bullet") ||
            other.CompareTag("Bullet");

        if (!isBullet)
        {
            return;
        }

        int appliedDamage = 1;

        TopDownBullet bullet =
            bulletObject.GetComponent<TopDownBullet>();

        if (bullet == null)
        {
            bullet =
                bulletObject.GetComponentInChildren<TopDownBullet>();
        }

        if (bullet != null)
        {
            // 총알이 이미 명중 처리됐다면 중복 피해를 주지 않습니다.
            if (!bullet.TryConsumeHit())
            {
                return;
            }

            appliedDamage =
                Mathf.Max(0, bullet.damage);
        }

        TakeDamage(appliedDamage);

        if (bulletObject != null)
        {
            Destroy(bulletObject);
        }
    }

    private float GetHorizontalDistance(
        Vector3 a,
        Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;

        return Vector3.Distance(
            a,
            b
        );
    }

    private void SetAnimation(
        float speedValue)
    {
        if (animator == null ||
            isDead)
        {
            return;
        }

        animator.SetFloat(
            speedParameterName,
            speedValue
        );
    }

    private void OnDestroy()
    {
        /*
         * 웨이브 사망 처리는 여기서 하지 않습니다.
         * HP바만 정리합니다.
         */
        RemoveHpBar();
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
            // 콜라이더 중심 높이에서 측정합니다.
            // (몸집이 큰 몬스터는 콜라이더 중심이 높아서, 발 높이로 재면
            //  실제로는 이미 닿아 있는데도 거리가 남아 공격을 못 하는 문제가 있었음)
            Vector3 probe = targetPoint;
            probe.y = selfColliderCache.bounds.center.y;
            from = selfColliderCache.ClosestPoint(probe);
        }

        from.y = 0f;
        targetPoint.y = 0f;

        return Vector3.Distance(from, targetPoint);
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