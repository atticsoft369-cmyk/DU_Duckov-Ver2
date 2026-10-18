using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MobTableSpawner : MonoBehaviour
{
    [Header("Table Settings")]
    [Tooltip("Assets/StreamingAssets 폴더 안 CSV 파일명. 확장자는 입력하지 않습니다.")]
    public string tableFileName = "MobTable";

    [Header("Prefab Settings")]
    [Tooltip("Assets/Resources 아래 몬스터 프리팹 폴더")]
    public string prefabResourceFolder = "prefabs";

    [Header("Ground Snap")]
    [Tooltip("소환 시 몬스터 발밑을 지형 표면에 맞춥니다.")]
    public bool snapToGround = true;

    [Tooltip("지형으로 인식할 레이어 (기본: 전체)")]
    public LayerMask groundMask = ~0;

    [Tooltip("spawnY 기준 위/아래로 지형을 찾는 범위. 범위 안에서 가장 낮은 바닥 면을 지형으로 사용합니다.")]
    [Min(0.1f)]
    public float groundSearchRange = 5f;

    [Tooltip("지형 위로 살짝 띄우는 높이")]
    public float groundOffset = 0.02f;

    [Header("Wave Settings")]
    [Min(1)]
    public int maxWave = 5;

    [Tooltip("게임 시작 요청 후 첫 웨이브가 시작될 때까지 기다리는 시간")]
    [Min(0f)]
    public float gameStartDelay = 3f;

    [Tooltip("WAVE START 메시지를 표시하는 시간")]
    [Min(0f)]
    public float waveStartMessageDuration = 2f;

    [Header("Spawn Height")]
    public float spawnY = 0f;

    [Tooltip("소환 영역 가장자리에서 안쪽으로 띄우는 거리 (몬스터가 영역 밖으로 걸치지 않게)")]
    public float spawnAreaPadding = 1f;

    [Header("Boss Spawn Settings")]
    [Tooltip("보스가 등장할 웨이브 레벨")]
    public int bossWaveLevel = 5;

    [Tooltip("5웨이브 보스 전용 스폰 위치")]
    public Transform bossSpawnPoint;

    [Header("Wave UI")]
    public TMP_Text waveStartMessage;
    public TMP_Text monsterCountText;
    public Button nextWaveButton;
    public TMP_Text gameClearMessage;

    [Tooltip("현재 웨이브의 CSV wavetimer 값을 표시할 TMP Text")]
    public TMP_Text waveCounterText;

    [Header("Wave Timer Settings")]
    [Tooltip("남은 시간이 이 값 이하가 되면 Wave Counter가 붉은색으로 점멸합니다.")]
    [Min(0f)]
    public float waveTimerWarningSeconds = 5f;

    [Tooltip("경고 상태의 점멸 속도. 초당 점멸 횟수")]
    [Min(0.1f)]
    public float waveTimerBlinkSpeed = 4f;

    [Tooltip("시간 초과 시 활성화할 GameOver Canvas. 비워두면 이름으로 자동 검색합니다.")]
    public GameObject gameOverCanvas;

    [Tooltip("자동 검색할 GameOver Canvas 오브젝트 이름")]
    public string gameOverCanvasObjectName = "GameOver Canvas";

    [Tooltip("게임 클리어 시 표시할 Canvas 오브젝트 이름")]
    public string gameClearCanvasObjectName = "GameClear Canvas";

    [Tooltip("게임 클리어 Canvas 안의 HOME 버튼 오브젝트 이름 (누르면 타이틀로 이동)")]
    public string gameClearHomeButtonName = "RestartButton";

    [Tooltip("게임오버 시 GameOver Canvas 아래의 모든 자식 오브젝트도 강제로 활성화")]
    public bool activateAllGameOverChildren = true;

    [Tooltip("게임오버 시 Time.timeScale을 0으로 만들어 게임을 정지")]
    public bool pauseGameOnGameOver = true;

    [Header("Wave Clear UI")]
    [Tooltip("웨이브 클리어 시 잠깐 표시할 WAVE CLEAR 이미지. 비워두면 이름으로 자동 검색합니다.")]
    public GameObject waveClearMessage;

    [Tooltip("자동 검색할 WAVE CLEAR 오브젝트 이름")]
    public string waveClearMessageObjectName = "Wave Clear Msg";

    [Tooltip("WAVE CLEAR를 보여주는 시간. 이후 준비시간 카운트다운과 Next Wave 버튼이 나옵니다.")]
    [Min(0f)]
    public float waveClearMessageDuration = 2f;

    [Header("Wave Rest Settings")]
    [Tooltip("웨이브 종료 후 다음 웨이브가 자동 시작되기까지의 대기 시간")]
    [Min(0f)]
    public float nextWaveWaitDuration = 30f;

    [Tooltip("웨이브 사이 남은 시간을 표시할 TMP Text")]
    public TMP_Text restTimeCounter;

    [Header("Special Wave Start Image")]
    [Tooltip("텍스트 대신 이미지를 출력할 웨이브 레벨")]
    public int imageWaveLevel = 5;

    [Tooltip("5웨이브 시작 시 표시할 UI Image 오브젝트")]
    public Image specialWaveStartImage;

    [Header("Cursor / Targeting Guide")]
    [Tooltip("웨이브 중에만 타겟 가이드를 표시할 플레이어 컨트롤러")]
    public TopDownPlayerController playerController;

    [Header("Game Over")]
    [Tooltip("게임오버를 한 곳에서 처리할 GameStartManager. 비워두면 자동으로 찾습니다.")]
    public GameStartManager gameStartManager;

    [System.Serializable]
    public class StageReward
    {
        public string name;

        [Tooltip("이 스테이지가 시작되기 전 정비시간에 지급")]
        public int beforeStage;

        [Tooltip("지급할 슬롯 번호 (1 Pistol, 2 Rifle, 3 Slime Gun, 4 Grenade, 5 Heal)")]
        public int slotNumber;
    }

    [Header("Stage Rewards (정비시간에 NPC에게서 Space로 수령)")]
    public List<StageReward> stageRewards = new List<StageReward>
    {
        new StageReward { name = "Rifle", beforeStage = 2, slotNumber = 2 },
        new StageReward { name = "Heal", beforeStage = 2, slotNumber = 5 },
        new StageReward { name = "Slime Gun", beforeStage = 3, slotNumber = 3 },
        new StageReward { name = "Grenade", beforeStage = 3, slotNumber = 4 },
    };

    [Tooltip("같은 정비시간에 여러 개 지급할 때 연출 간격(초)")]
    public float rewardGrantInterval = 1.2f;

    [Header("Wave End NPC Reward Settings")]
    public NPCWaveReward rewardNpc;

    [Tooltip("웨이브별 보상 아이템 프리팹. 필드에 생성하지 않고 보상 종류/아이콘 판정용으로만 사용")]
    public List<GameObject> waveEndItemPrefabs = new List<GameObject>();

    [HideInInspector] public Transform itemSpot;
    [HideInInspector] public bool removePreviousItem = false;

    [Header("Item Placement Settings")]
    [Min(1)]
    public int maxItemPlaceAttempts = 10;

    [Min(0f)]
    public float itemPlacePadding = 0.3f;

    private readonly List<MobTurnData> turnDataList =
        new List<MobTurnData>();

    private Collider spawnAreaCollider;
    private GameObject currentWaveItem;
    private Coroutine waveCoroutine;
    private Coroutine nextWaveCountdownCoroutine;
    private Coroutine waveTimerCoroutine;
    private Coroutine waveClearCoroutine;

    private float currentWaveRemainingTime;
    private Color waveCounterOriginalColor = Color.white;

    private bool waveSystemStarted;
    private bool spawnFinished;
    private bool waveCompleted;
    private bool gameCleared;

    private int currentWaveIndex;
    private int currentSpawnedCount;
    private int currentWaveMaxCount;
    private int aliveMonsterCount;

    private void Awake()
    {
        spawnAreaCollider = GetComponent<Collider>();

        if (spawnAreaCollider == null)
        {
            Debug.LogError(
                "MobTableSpawner가 붙은 오브젝트에 Collider가 없습니다.",
                this
            );
        }
        else
        {
            // 소환 영역 콜라이더는 영역 표시용입니다.
            // 실제 충돌체로 두면 그 안에서 생성된 몬스터가 밖으로 밀려나므로 Trigger로 바꿉니다.
            spawnAreaCollider.isTrigger = true;
        }

        LoadMobTable();
        InitializeUI();

        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<TopDownPlayerController>();
        }

        // 타이틀 및 게임 시작 전에는 일반 마우스 커서를 표시합니다.
        SetWaveCombatCursor(false);
    }

    private void Start()
    {
        if (nextWaveButton != null)
        {
            nextWaveButton.onClick.RemoveListener(StartNextWave);
            nextWaveButton.onClick.AddListener(StartNextWave);
        }

        /*
         * 자동으로 웨이브를 시작하지 않습니다.
         * GameStartManager에서 StartWaveSpawn()을 호출하면 시작됩니다.
         */
    }

    private void OnDestroy()
    {
        SetWaveCombatCursor(false);

        if (nextWaveCountdownCoroutine != null)
        {
            StopCoroutine(nextWaveCountdownCoroutine);
            nextWaveCountdownCoroutine = null;
        }

        StopWaveTimer();

        if (nextWaveButton != null)
        {
            nextWaveButton.onClick.RemoveListener(StartNextWave);
        }
    }

    /*
     * GameStartManager에서 호출하는 웨이브 시스템 시작 함수입니다.
     */
    public void StartWaveSpawn()
    {
        if (waveSystemStarted)
        {
            Debug.LogWarning(
                "웨이브 시스템이 이미 시작되었습니다.",
                this
            );

            return;
        }

        if (turnDataList.Count <= 0)
        {
            Debug.LogError(
                "시작할 웨이브 데이터가 없습니다.",
                this
            );

            return;
        }

        waveSystemStarted = true;
        gameCleared = false;

        // 이전 Game Over에서 timeScale이 0으로 남아 있을 경우 복구
        Time.timeScale = 1f;

        FindGameOverCanvasIfNeeded();

        if (gameOverCanvas != null)
        {
            SetGameOverCanvasVisible(false);
        }

        if (waveCoroutine != null)
        {
            StopCoroutine(waveCoroutine);
        }

        waveCoroutine = StartCoroutine(GameStartRoutine());

        Debug.Log("웨이브 시스템 시작");
    }

    private void SetWaveCombatCursor(bool active)
    {
        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<TopDownPlayerController>();
        }

        if (playerController != null)
        {
            playerController.SetWaveCombatActive(active);
            return;
        }

        Cursor.visible = !active;
        Cursor.lockState = CursorLockMode.None;
    }

    private void InitializeUI()
    {
        if (waveStartMessage != null)
        {
            waveStartMessage.gameObject.SetActive(false);
        }

        if (monsterCountText != null)
        {
            monsterCountText.gameObject.SetActive(false);
        }

        if (nextWaveButton != null)
        {
            nextWaveButton.gameObject.SetActive(false);
        }

        if (gameClearMessage != null)
        {
            gameClearMessage.gameObject.SetActive(false);
        }

        if (waveCounterText != null)
        {
            waveCounterOriginalColor = waveCounterText.color;
            waveCounterText.text = "00:00";
            waveCounterText.gameObject.SetActive(false);
        }

        FindGameOverCanvasIfNeeded();

        if (gameOverCanvas != null)
        {
            SetGameOverCanvasVisible(false);
        }

        if (restTimeCounter != null)
        {
            restTimeCounter.text = FormatRestTime(nextWaveWaitDuration);
            restTimeCounter.gameObject.SetActive(false);
        }

        if (specialWaveStartImage != null)
        {
            specialWaveStartImage.gameObject.SetActive(false);
        }

        // WAVE CLEAR 이미지는 웨이브를 클리어했을 때만 보이도록 처음엔 숨깁니다.
        FindWaveClearMessageIfNeeded();
        SetWaveClearMessageVisible(false);
    }

    private IEnumerator GameStartRoutine()
    {
        if (gameStartDelay > 0f)
        {
            yield return new WaitForSeconds(gameStartDelay);
        }

        currentWaveIndex = 0;
        StartCurrentWave();
    }

    private void StartCurrentWave()
    {
        if (gameCleared)
        {
            return;
        }

        int playableWaveCount =
            Mathf.Min(maxWave, turnDataList.Count);

        if (playableWaveCount <= 0)
        {
            Debug.LogError("플레이 가능한 웨이브가 없습니다.", this);
            return;
        }

        if (currentWaveIndex >= playableWaveCount)
        {
            ShowGameClear();
            return;
        }

        if (waveCoroutine != null)
        {
            StopCoroutine(waveCoroutine);
        }

        waveCoroutine = StartCoroutine(
            RunWave(currentWaveIndex)
        );
    }

    private IEnumerator RunWave(int waveIndex)
    {
        if (spawnAreaCollider == null)
        {
            Debug.LogError(
                "몬스터 소환 영역 Collider가 없습니다.",
                this
            );

            yield break;
        }

        if (waveIndex < 0 ||
            waveIndex >= turnDataList.Count)
        {
            Debug.LogError(
                "잘못된 웨이브 인덱스입니다: " + waveIndex,
                this
            );

            yield break;
        }

        waveCompleted = false;
        spawnFinished = false;

        // 실제 웨이브가 시작되었으므로 타겟 가이드를 표시합니다.
        SetWaveCombatCursor(true);

        // 웨이브 전환은 무기 해금 상태를 절대 초기화하지 않습니다.
        // 현재까지 획득한 무기 UI를 그대로 복구/유지합니다.
        if (WeaponIconSelector.Instance != null)
        {
            WeaponIconSelector.Instance.RefreshPersistentWeaponUI();
        }

        currentSpawnedCount = 0;
        aliveMonsterCount = 0;

        StopWaveClearSequence();

        MobTurnData waveData =
            turnDataList[waveIndex];

        currentWaveMaxCount =
            GetTotalMonsterCount(waveData);

        HideNextWaveButton();

        UpdateMonsterCountUI();
        ShowMonsterCountUI();

        // 제한시간은 첫 몬스터가 소환되는 순간부터 흐릅니다.
        // 그 전(WAVE START 메시지, 첫 Turn 대기시간)에는 전체 시간만 표시해 둡니다.
        bool waveTimerStarted = false;
        StopWaveTimer();
        ShowWaveTimerPreview(waveData.waveTimer);

        yield return StartCoroutine(
            ShowWaveStartMessage(waveData.level)
        );

        Debug.Log(
            $"===== WAVE {waveData.level} 시작 ====="
        );

        /*
         * 한 웨이브에는 최대 5개의 Turn이 존재합니다.
         *
         * 각 Turn:
         * delayN 만큼 1회 대기
         * -> mobN_1 / valueN_1 소환
         * -> mobN_2 / valueN_2 소환
         *
         * 같은 Turn에 등록된 두 몬스터 종류는
         * 동일한 Turn 타이밍에 함께 소환됩니다.
         */
        for (int turnIndex = 0;
             turnIndex < waveData.turnList.Count;
             turnIndex++)
        {
            MobTurnSpawnData turnData =
                waveData.turnList[turnIndex];

            if (turnData.delay > 0f)
            {
                yield return new WaitForSeconds(
                    turnData.delay
                );
            }

            if (!waveTimerStarted)
            {
                waveTimerStarted = true;
                StartWaveTimer(waveData.waveTimer);
            }

            for (int mobIndex = 0;
                 mobIndex < turnData.mobList.Count;
                 mobIndex++)
            {
                MobSpawnData spawnData =
                    turnData.mobList[mobIndex];

                SpawnMob(
                    spawnData.mobName,
                    spawnData.count,
                    waveData.level
                );
            }
        }

        spawnFinished = true;

        // 소환할 Turn이 하나도 없던 웨이브라면 여기서 타이머를 시작합니다.
        if (!waveTimerStarted)
        {
            waveTimerStarted = true;
            StartWaveTimer(waveData.waveTimer);
        }

        Debug.Log(
            $"WAVE {waveData.level}의 모든 몬스터 소환 완료"
        );

        /*
         * 모든 몬스터 소환이 끝난 시점에
         * 살아 있는 몬스터가 없으면 웨이브를 종료합니다.
         */
        CheckWaveCompletion();

        waveCoroutine = null;
    }

    private IEnumerator ShowWaveStartMessage(
        int waveLevel)
    {
        bool useSpecialImage =
            waveLevel == imageWaveLevel &&
            specialWaveStartImage != null;

        /*
         * 5웨이브는 일반 텍스트 대신 지정한 이미지를 표시합니다.
         */
        if (useSpecialImage)
        {
            if (waveStartMessage != null)
            {
                waveStartMessage.gameObject.SetActive(false);
            }

            specialWaveStartImage.gameObject.SetActive(true);

            if (waveStartMessageDuration > 0f)
            {
                yield return new WaitForSeconds(
                    waveStartMessageDuration
                );
            }

            specialWaveStartImage.gameObject.SetActive(false);
            yield break;
        }

        /*
         * 1~4웨이브는 기존 WAVE N START 텍스트를 표시합니다.
         */
        if (specialWaveStartImage != null)
        {
            specialWaveStartImage.gameObject.SetActive(false);
        }

        if (waveStartMessage == null)
        {
            yield break;
        }

        waveStartMessage.text =
            $"WAVE {waveLevel} START";

        waveStartMessage.gameObject.SetActive(true);

        if (waveStartMessageDuration > 0f)
        {
            yield return new WaitForSeconds(
                waveStartMessageDuration
            );
        }

        waveStartMessage.gameObject.SetActive(false);
    }

    private void SpawnMob(
        string mobName,
        int count,
        int waveLevel)
    {
        if (string.IsNullOrWhiteSpace(mobName) ||
            count <= 0)
        {
            return;
        }

        string resourcePath =
            prefabResourceFolder + "/" + mobName;

        GameObject prefab =
            Resources.Load<GameObject>(resourcePath);

        if (prefab == null)
        {
            Debug.LogWarning(
                "몬스터 프리팹을 찾을 수 없습니다: " +
                "Assets/Resources/" +
                resourcePath +
                ".prefab",
                this
            );

            return;
        }

        // 보스(BossMonsterChaser가 붙은 프리팹)는 웨이브 번호와 상관없이 항상 보스 스폰 위치에서 생성
        bool isBoss =
            bossSpawnPoint != null &&
            prefab.GetComponent<BossMonsterChaser>() != null;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition;

            if (isBoss)
            {
                spawnPosition =
                    bossSpawnPoint.position;
            }
            else
            {
                spawnPosition =
                    GetRandomPointInCollider();
            }

            Quaternion spawnRotation =
                isBoss
                    ? bossSpawnPoint.rotation
                    : Quaternion.identity;

            GameObject spawnedMonster =
                Instantiate(
                    prefab,
                    spawnPosition,
                    spawnRotation
                );

            if (snapToGround)
            {
                SnapMonsterToGround(spawnedMonster);
            }

            aliveMonsterCount++;
            currentSpawnedCount++;

            RegisterMonster(spawnedMonster);
            UpdateMonsterCountUI();
        }

        Debug.Log(
            $"{mobName} {count}마리 소환 " +
            $"({currentSpawnedCount} / " +
            $"{currentWaveMaxCount})"
        );
    }

    private void RegisterMonster(
        GameObject spawnedMonster)
    {
        if (spawnedMonster == null)
        {
            return;
        }

        WaveMobTracker tracker =
            spawnedMonster.GetComponent<WaveMobTracker>();

        if (tracker == null)
        {
            tracker =
                spawnedMonster.AddComponent<WaveMobTracker>();
        }

        tracker.Initialize(
            this,
            currentWaveIndex
        );
    }

    /*
     * WaveMobTracker가 몬스터 제거를 감지하면 호출합니다.
     */
    public void NotifyMonsterDestroyed(
        int monsterWaveIndex)
    {
        /*
         * 이전 웨이브 몬스터가 늦게 제거되었거나
         * 다른 웨이브 몬스터라면 현재 웨이브 카운트에 반영하지 않습니다.
         */
        if (monsterWaveIndex != currentWaveIndex)
        {
            return;
        }

        if (waveCompleted || gameCleared)
        {
            return;
        }

        aliveMonsterCount =
            Mathf.Max(0, aliveMonsterCount - 1);

        Debug.Log(
            $"WAVE {currentWaveIndex + 1} 남은 몬스터: " +
            aliveMonsterCount
        );

        CheckWaveCompletion();
    }

    private void CheckWaveCompletion()
    {
        if (waveCompleted || gameCleared)
        {
            return;
        }

        // 아직 테이블에 등록된 몬스터 소환이 끝나지 않음
        if (!spawnFinished)
        {
            return;
        }

        // 살아 있는 몬스터가 남아 있음
        if (aliveMonsterCount > 0)
        {
            return;
        }

        CompleteCurrentWave();
    }

    private void CompleteCurrentWave()
    {
        if (waveCompleted || gameCleared)
        {
            return;
        }

        waveCompleted = true;
        StopWaveTimer();

        int clearedWaveIndex =
            currentWaveIndex;

        int waveNumber =
            turnDataList[clearedWaveIndex].level;

        Debug.Log(
            $"===== WAVE {waveNumber} CLEAR ====="
        );

        // 스테이지 보상은 정비시간에 NPC에게 등록되고, NPC 앞에서 Space로 받습니다.

        int playableWaveCount =
            Mathf.Min(maxWave, turnDataList.Count);

        bool isLastWave =
            currentWaveIndex >= playableWaveCount - 1;

        // 웨이브 클리어 -> WAVE CLEAR 표시 -> (준비시간 + Next Wave 버튼) 또는 GAME CLEAR
        if (waveClearCoroutine != null)
        {
            StopCoroutine(waveClearCoroutine);
        }

        waveClearCoroutine =
            StartCoroutine(WaveClearSequence(isLastWave));
    }

    private IEnumerator WaveClearSequence(bool isLastWave)
    {
        // 전투가 끝났으므로 조준 가이드 대신 일반 커서를 표시합니다.
        SetWaveCombatCursor(false);

        if (monsterCountText != null)
        {
            monsterCountText.gameObject.SetActive(false);
        }

        if (waveCounterText != null)
        {
            waveCounterText.gameObject.SetActive(false);
        }

        SetWaveClearMessageVisible(true);

        if (waveClearMessageDuration > 0f)
        {
            yield return new WaitForSeconds(
                waveClearMessageDuration
            );
        }

        SetWaveClearMessageVisible(false);
        waveClearCoroutine = null;

        if (gameCleared)
        {
            yield break;
        }

        if (isLastWave)
        {
            ShowGameClear();
        }
        else
        {
            ShowNextWaveButton();
            StartNextWaveCountdown();

            // 다음 스테이지 시작 전 정비시간: NPC에게 지원품 등록 (NPC 앞에서 Space로 수령)
            GrantRewardsBeforeStage(GetNextStageLevel());
        }
    }

    private int GetNextStageLevel()
    {
        int nextIndex = currentWaveIndex + 1;

        if (nextIndex >= 0 && nextIndex < turnDataList.Count)
        {
            return turnDataList[nextIndex].level;
        }

        return nextIndex + 1;
    }

    private void GrantRewardsBeforeStage(int stageLevel)
    {
        if (stageRewards == null)
        {
            return;
        }

        if (rewardNpc == null)
        {
            rewardNpc = FindFirstObjectByType<NPCWaveReward>();
        }

        // NPC가 있으면 NPC에게 등록 -> 플레이어가 NPC 앞에서 Space로 수령
        if (rewardNpc != null)
        {
            foreach (StageReward reward in stageRewards)
            {
                if (reward == null || reward.beforeStage != stageLevel)
                {
                    continue;
                }

                rewardNpc.AddPendingReward(reward.slotNumber, reward.name);
            }

            return;
        }

        // NPC가 없으면 기존처럼 자동 지급
        if (WeaponIconSelector.Instance == null)
        {
            return;
        }

        float delay = 0f;

        foreach (StageReward reward in stageRewards)
        {
            if (reward == null || reward.beforeStage != stageLevel)
            {
                continue;
            }

            Debug.Log($"[Reward] STAGE {stageLevel} 시작 전 지급: {reward.name} (슬롯 {reward.slotNumber})");

            WeaponIconSelector.Instance.GrantSlotWithEffect(reward.slotNumber, delay);
            delay += Mathf.Max(0f, rewardGrantInterval);
        }
    }

    private void FindWaveClearMessageIfNeeded()
    {
        if (waveClearMessage != null ||
            string.IsNullOrWhiteSpace(waveClearMessageObjectName))
        {
            return;
        }

        GameObject[] allObjects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null ||
                !obj.scene.IsValid() ||
                obj.name != waveClearMessageObjectName)
            {
                continue;
            }

            waveClearMessage = obj;
            return;
        }

        Debug.LogWarning(
            "WAVE CLEAR 오브젝트 '" +
            waveClearMessageObjectName +
            "'를 찾지 못했습니다.",
            this
        );
    }

    private void SetWaveClearMessageVisible(bool visible)
    {
        if (waveClearMessage != null)
        {
            waveClearMessage.SetActive(visible);
        }
    }

    private void StopWaveClearSequence()
    {
        if (waveClearCoroutine != null)
        {
            StopCoroutine(waveClearCoroutine);
            waveClearCoroutine = null;
        }

        SetWaveClearMessageVisible(false);
    }

    public void StartNextWave()
    {
        if (!waveSystemStarted)
        {
            Debug.LogWarning(
                "웨이브 시스템이 아직 시작되지 않았습니다.",
                this
            );

            return;
        }

        if (!waveCompleted || gameCleared)
        {
            return;
        }

        // 버튼으로 먼저 누르면 자동 카운트다운을 취소합니다.
        if (nextWaveCountdownCoroutine != null)
        {
            StopCoroutine(nextWaveCountdownCoroutine);
            nextWaveCountdownCoroutine = null;
        }

        HideNextWaveButton();

        currentWaveIndex++;
        StartCurrentWave();
    }

    private void StartNextWaveCountdown()
    {
        if (nextWaveCountdownCoroutine != null)
        {
            StopCoroutine(nextWaveCountdownCoroutine);
        }

        nextWaveCountdownCoroutine =
            StartCoroutine(NextWaveCountdownRoutine());
    }

    private IEnumerator NextWaveCountdownRoutine()
    {
        float remainingTime =
            Mathf.Max(0f, nextWaveWaitDuration);

        if (restTimeCounter != null)
        {
            restTimeCounter.gameObject.SetActive(true);
            restTimeCounter.text =
                FormatRestTime(remainingTime);
        }

        while (remainingTime > 0f)
        {
            yield return null;

            remainingTime -= Time.deltaTime;

            if (restTimeCounter != null)
            {
                restTimeCounter.text =
                    FormatRestTime(
                        Mathf.Max(0f, remainingTime)
                    );
            }
        }

        if (restTimeCounter != null)
        {
            restTimeCounter.text = "00:00";
        }

        nextWaveCountdownCoroutine = null;

        if (waveCompleted && !gameCleared)
        {
            StartNextWave();
        }
    }

    private string FormatRestTime(float seconds)
    {
        int totalSeconds =
            Mathf.Max(0, Mathf.CeilToInt(seconds));

        int minutes =
            totalSeconds / 60;

        int remainSeconds =
            totalSeconds % 60;

        return $"{minutes:00}:{remainSeconds:00}";
    }

    private void ShowNextWaveButton()
    {
        // 웨이브 종료 후 Next Wave 버튼을 누를 수 있도록 기본 커서를 표시합니다.
        SetWaveCombatCursor(false);

        if (nextWaveButton == null)
        {
            Debug.LogWarning(
                "Next Wave Button이 연결되지 않았습니다.",
                this
            );

            return;
        }

        nextWaveButton.gameObject.SetActive(true);
        nextWaveButton.interactable = true;
    }

    private void HideNextWaveButton()
    {
        if (nextWaveButton != null)
        {
            nextWaveButton.gameObject.SetActive(false);
        }

        if (restTimeCounter != null)
        {
            restTimeCounter.gameObject.SetActive(false);
        }
    }

    private void ShowGameClear()
    {
        if (gameCleared)
        {
            return;
        }

        gameCleared = true;
        waveCompleted = true;

        StopWaveTimer();

        if (nextWaveCountdownCoroutine != null)
        {
            StopCoroutine(nextWaveCountdownCoroutine);
            nextWaveCountdownCoroutine = null;
        }

        if (restTimeCounter != null)
        {
            restTimeCounter.gameObject.SetActive(false);
        }

        // 게임 클리어 상태에서는 일반 마우스 커서를 표시합니다.
        SetWaveCombatCursor(false);

        HideNextWaveButton();

        if (monsterCountText != null)
        {
            monsterCountText.gameObject.SetActive(false);
        }

        if (waveCounterText != null)
        {
            waveCounterText.gameObject.SetActive(false);
        }

        if (waveStartMessage != null)
        {
            waveStartMessage.gameObject.SetActive(false);
        }

        if (specialWaveStartImage != null)
        {
            specialWaveStartImage.gameObject.SetActive(false);
        }

        // 텍스트 메시지 대신 GameClear Canvas(이미지 + HOME 버튼)를 표시합니다.
        if (!ShowGameClearCanvas() && gameClearMessage != null)
        {
            gameClearMessage.text = "GAME CLEAR";
            gameClearMessage.gameObject.SetActive(true);
        }

        Debug.Log("모든 웨이브 클리어!");
    }

    private bool ShowGameClearCanvas()
    {
        GameObject canvas = null;

        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj != null && obj.scene.IsValid() && obj.name == gameClearCanvasObjectName)
            {
                canvas = obj;
                break;
            }
        }

        if (canvas == null)
        {
            Debug.LogWarning(gameClearCanvasObjectName + "를 찾지 못했습니다.", this);
            return false;
        }

        canvas.SetActive(true);

        // 클리어 창 외의 모든 UI(다른 루트 Canvas) 숨김
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (c == null || !c.isRootCanvas)
            {
                continue;
            }

            if (c.transform == canvas.transform || c.transform.IsChildOf(canvas.transform))
            {
                continue;
            }

            c.gameObject.SetActive(false);
        }

        // 게임 플레이 정지: 시간 정지 + 플레이어 조작/일시정지 입력 차단
        Time.timeScale = 0f;

        TopDownPlayerController pc = FindFirstObjectByType<TopDownPlayerController>();
        if (pc != null)
        {
            pc.enabled = false;
        }

        FindGameStartManagerIfNeeded();
        if (gameStartManager != null)
        {
            // ESC 일시정지/재개로 시간이 다시 흐르지 않도록 (HOME 버튼의 RestartToTitle은 그대로 호출 가능)
            gameStartManager.enabled = false;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // HOME 버튼: 기존 연결(무기 리셋)을 지우고 타이틀로 이동만 하게 합니다.
        foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name != gameClearHomeButtonName)
            {
                continue;
            }

            button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(GoToTitleFromGameClear);
        }

        return true;
    }

    private void GoToTitleFromGameClear()
    {
        FindGameStartManagerIfNeeded();

        if (gameStartManager != null)
        {
            gameStartManager.RestartToTitle();
        }
        else
        {
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
    }

    private void ShowMonsterCountUI()
    {
        if (monsterCountText != null)
        {
            monsterCountText.gameObject.SetActive(true);
        }
    }

    /*
     * 타이머를 돌리지 않고 이번 웨이브의 전체 제한시간만 표시합니다.
     */
    private void ShowWaveTimerPreview(float waveTimer)
    {
        if (waveCounterText == null)
        {
            return;
        }

        waveCounterText.gameObject.SetActive(true);
        waveCounterText.color = waveCounterOriginalColor;
        waveCounterText.text =
            FormatWaveTimer(Mathf.Max(0f, waveTimer));
    }

    private void StartWaveTimer(float waveTimer)
    {
        StopWaveTimer();

        currentWaveRemainingTime =
            Mathf.Max(0f, waveTimer);

        if (waveCounterText != null)
        {
            waveCounterText.gameObject.SetActive(true);
            waveCounterText.color = waveCounterOriginalColor;
            waveCounterText.text =
                FormatWaveTimer(currentWaveRemainingTime);
        }

        // CSV 값이 0 이하라면 제한시간 없음으로 처리합니다.
        if (currentWaveRemainingTime <= 0f)
        {
            return;
        }

        waveTimerCoroutine =
            StartCoroutine(WaveTimerRoutine());
    }

    private IEnumerator WaveTimerRoutine()
    {
        while (currentWaveRemainingTime > 0f &&
               !waveCompleted &&
               !gameCleared)
        {
            currentWaveRemainingTime =
                Mathf.Max(
                    0f,
                    currentWaveRemainingTime - Time.deltaTime
                );

            UpdateWaveTimerUI();

            yield return null;
        }

        waveTimerCoroutine = null;

        if (waveCompleted || gameCleared)
        {
            yield break;
        }

        currentWaveRemainingTime = 0f;
        UpdateWaveTimerUI();

        // 시간이 0이 되었는데 몬스터가 남아 있거나
        // 아직 소환할 몬스터가 남아 있으면 Game Over.
        if (aliveMonsterCount > 0 || !spawnFinished)
        {
            TriggerWaveTimeGameOver();
            yield break;
        }

        // 같은 프레임에 마지막 몬스터가 제거된 경우 정상 클리어 판정.
        CheckWaveCompletion();
    }

    private void UpdateWaveTimerUI()
    {
        if (waveCounterText == null)
        {
            return;
        }

        waveCounterText.text =
            FormatWaveTimer(currentWaveRemainingTime);

        bool warning =
            currentWaveRemainingTime > 0f &&
            currentWaveRemainingTime <= waveTimerWarningSeconds;

        if (!warning)
        {
            waveCounterText.color = waveCounterOriginalColor;
            return;
        }

        // 5초 이하: 빨강 <-> 원래색 점멸
        float blink =
            Mathf.PingPong(
                Time.unscaledTime * waveTimerBlinkSpeed,
                1f
            );

        waveCounterText.color =
            Color.Lerp(
                Color.red,
                waveCounterOriginalColor,
                blink
            );
    }

    private void StopWaveTimer()
    {
        if (waveTimerCoroutine != null)
        {
            StopCoroutine(waveTimerCoroutine);
            waveTimerCoroutine = null;
        }

        if (waveCounterText != null)
        {
            waveCounterText.color = waveCounterOriginalColor;
        }
    }

    private void TriggerWaveTimeGameOver()
    {
        if (gameCleared)
        {
            return;
        }

        Debug.Log(
            "웨이브 제한시간 종료 / 남은 몬스터: " +
            aliveMonsterCount +
            " / GAME OVER"
        );

        FindGameStartManagerIfNeeded();

        if (gameStartManager != null)
        {
            // 게임오버 처리는 GameStartManager 한 곳에서만 합니다.
            // (웨이브 정지, GameOver Canvas 표시, 시간 정지, 재시작 버튼)
            gameStartManager.TriggerGameOver("웨이브 제한시간 초과");
        }
        else
        {
            // GameStartManager가 없는 테스트 씬용 대체 처리
            StopWavesForGameOver();
            ShowGameOverCanvas();

            if (pauseGameOnGameOver)
            {
                Time.timeScale = 0f;
            }
        }

        if (waveCounterText != null)
        {
            waveCounterText.text = "00:00";
            waveCounterText.color = Color.red;
        }
    }

    /*
     * 게임오버 시 웨이브 진행을 모두 멈춥니다.
     * 댐 파괴 / 시간 초과 어느 쪽 게임오버든 GameStartManager가 호출합니다.
     */
    public void StopWavesForGameOver()
    {
        gameCleared = true;
        waveCompleted = true;
        spawnFinished = true;

        if (waveCoroutine != null)
        {
            StopCoroutine(waveCoroutine);
            waveCoroutine = null;
        }

        if (nextWaveCountdownCoroutine != null)
        {
            StopCoroutine(nextWaveCountdownCoroutine);
            nextWaveCountdownCoroutine = null;
        }

        StopWaveTimer();
        StopWaveClearSequence();

        SetWaveCombatCursor(false);
        HideNextWaveButton();

        if (waveStartMessage != null)
        {
            waveStartMessage.gameObject.SetActive(false);
        }

        if (specialWaveStartImage != null)
        {
            specialWaveStartImage.gameObject.SetActive(false);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /*
     * GameOver Canvas(루트 + 자식)를 표시합니다.
     */
    public void ShowGameOverCanvas()
    {
        FindGameOverCanvasIfNeeded();

        if (gameOverCanvas != null)
        {
            SetGameOverCanvasVisible(true);
        }
        else
        {
            Debug.LogWarning(
                "GameOver Canvas를 찾지 못했습니다.",
                this
            );
        }
    }

    private void FindGameStartManagerIfNeeded()
    {
        if (gameStartManager == null)
        {
            gameStartManager =
                FindFirstObjectByType<GameStartManager>();
        }
    }

    private void SetGameOverCanvasVisible(bool visible)
    {
        if (gameOverCanvas == null)
        {
            return;
        }

        if (visible)
        {
            // 루트를 먼저 켠 뒤 자식들의 activeSelf도 복구합니다.
            gameOverCanvas.SetActive(true);

            if (activateAllGameOverChildren)
            {
                Transform[] children =
                    gameOverCanvas.GetComponentsInChildren<Transform>(true);

                foreach (Transform child in children)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    child.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            // 숨길 때는 루트만 끄면 자식의 구성은 유지됩니다.
            gameOverCanvas.SetActive(false);
        }
    }

    public void ResumeGameFromGameOver()
    {
        Time.timeScale = 1f;
    }

    private void FindGameOverCanvasIfNeeded()
    {
        if (gameOverCanvas != null ||
            string.IsNullOrWhiteSpace(gameOverCanvasObjectName))
        {
            return;
        }

        GameObject[] allObjects =
            Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject obj in allObjects)
        {
            if (obj == null ||
                !obj.scene.IsValid() ||
                obj.name != gameOverCanvasObjectName)
            {
                continue;
            }

            gameOverCanvas = obj;
            return;
        }
    }

    private string FormatWaveTimer(float seconds)
    {
        int totalSeconds =
            Mathf.Max(
                0,
                Mathf.CeilToInt(seconds)
            );

        int minutes =
            totalSeconds / 60;

        int remainSeconds =
            totalSeconds % 60;

        return $"{minutes:00}:{remainSeconds:00}";
    }

    private void UpdateMonsterCountUI()
    {
        if (monsterCountText == null)
        {
            return;
        }

        int displayWave =
            currentWaveIndex + 1;

        monsterCountText.text =            
            $"{currentSpawnedCount} / " +
            $"{currentWaveMaxCount}";
    }

    private int GetTotalMonsterCount(
        MobTurnData waveData)
    {
        if (waveData == null)
        {
            return 0;
        }

        int totalCount = 0;

        for (int turnIndex = 0;
             turnIndex < waveData.turnList.Count;
             turnIndex++)
        {
            MobTurnSpawnData turnData =
                waveData.turnList[turnIndex];

            for (int mobIndex = 0;
                 mobIndex < turnData.mobList.Count;
                 mobIndex++)
            {
                totalCount += Mathf.Max(
                    0,
                    turnData.mobList[mobIndex].count
                );
            }
        }

        return totalCount;
    }

    /*
     * 소환된 몬스터의 가장 낮은 지점(모델 기준)이 지형 표면에 닿도록 높이를 보정합니다.
     * 몬스터는 Y축이 고정되어 있으므로 소환 시 한 번만 맞추면 됩니다.
     */
    private void SnapMonsterToGround(GameObject monster)
    {
        if (monster == null)
        {
            return;
        }

        Vector3 pos = monster.transform.position;

        if (!TryFindGroundY(pos, monster, out float groundY))
        {
            return;
        }

        if (!TryGetBottomY(monster, out float bottomY))
        {
            bottomY = pos.y;
        }

        float delta = (groundY + groundOffset) - bottomY;
        pos.y += delta;
        monster.transform.position = pos;

        Rigidbody body = monster.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = pos;
        }
    }

    private bool TryFindGroundY(Vector3 position, GameObject self, out float groundY)
    {
        groundY = 0f;

        float referenceY = position.y;
        Vector3 origin = new Vector3(position.x, referenceY + groundSearchRange, position.z);

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundSearchRange * 2f,
            groundMask.value == 0 ? Physics.DefaultRaycastLayers : groundMask.value,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float best = 0f;

        foreach (RaycastHit hit in hits)
        {
            Collider c = hit.collider;

            if (c == null || hit.normal.y < 0.5f)
            {
                continue;
            }

            // 스포너 영역 콜라이더(박스) 윗면을 땅으로 착각하지 않도록 제외
            if (c == spawnAreaCollider || c.transform.IsChildOf(transform))
            {
                continue;
            }

            if (c.transform.IsChildOf(self.transform) ||
                c.GetComponentInParent<MonsterChaser>() != null ||
                c.GetComponentInParent<BossMonsterChaser>() != null ||
                c.GetComponentInParent<TopDownPlayerController>() != null)
            {
                continue;
            }

            // 나무/덤불/지붕 윗면을 피하기 위해 범위 안에서 가장 낮은 바닥 면을 사용합니다.
            if (!found || hit.point.y < best)
            {
                best = hit.point.y;
                found = true;
            }
        }

        if (found)
        {
            groundY = best;
        }

        return found;
    }

    private static bool TryGetBottomY(GameObject monster, out float bottomY)
    {
        bottomY = float.MaxValue;
        bool found = false;

        foreach (Renderer r in monster.GetComponentsInChildren<Renderer>())
        {
            // 꺼진 렌더러(숨긴 기존 모델 등)는 bounds가 0이라 높이 계산에서 제외합니다.
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer) ||
                !r.enabled || !r.gameObject.activeInHierarchy)
            {
                continue;
            }

            bottomY = Mathf.Min(bottomY, r.bounds.min.y);
            found = true;
        }

        if (!found)
        {
            foreach (Collider c in monster.GetComponentsInChildren<Collider>())
            {
                if (c.isTrigger)
                {
                    continue;
                }

                bottomY = Mathf.Min(bottomY, c.bounds.min.y);
                found = true;
            }
        }

        return found;
    }

    private Vector3 GetRandomPointInCollider()
    {
        // BoxCollider는 회전/스케일을 반영해 박스 내부에서만 뽑습니다. (가장자리 여유 spawnAreaPadding)
        BoxCollider box = spawnAreaCollider as BoxCollider;

        if (box != null)
        {
            Vector3 lossy = box.transform.lossyScale;
            Vector3 half = box.size * 0.5f;

            float padX = Mathf.Abs(lossy.x) > 0.0001f ? spawnAreaPadding / Mathf.Abs(lossy.x) : 0f;
            float padZ = Mathf.Abs(lossy.z) > 0.0001f ? spawnAreaPadding / Mathf.Abs(lossy.z) : 0f;

            float hx = Mathf.Max(0f, half.x - padX);
            float hz = Mathf.Max(0f, half.z - padZ);

            Vector3 local = box.center + new Vector3(
                Random.Range(-hx, hx),
                0f,
                Random.Range(-hz, hz)
            );

            Vector3 world = box.transform.TransformPoint(local);
            world.y = spawnY;
            return world;
        }

        Bounds bounds =
            spawnAreaCollider.bounds;

        float randomX =
            Random.Range(
                bounds.min.x,
                bounds.max.x
            );

        float randomZ =
            Random.Range(
                bounds.min.z,
                bounds.max.z
            );

        return new Vector3(
            randomX,
            spawnY,
            randomZ
        );
    }

    private void LoadMobTable()
    {
        turnDataList.Clear();

        string csvText;

        // GameControl.csv 의 [WAVE] 섹션이 있으면 그것을 우선 사용 (빌드 후 수정 가능)
        string controlWave = GameControl.GetWaveCsv();

        if (!string.IsNullOrEmpty(controlWave))
        {
            csvText = controlWave;
            Debug.Log("[GameControl] WAVE 정보를 GameControl.csv에서 읽었습니다.");
        }
        else if (!TryReadMobTableFile(out csvText))
        {
            return;
        }

        ParseMobTableText(csvText);
    }

    private bool TryReadMobTableFile(out string csvText)
    {
        csvText = null;

        string csvPath = Path.Combine(
            Application.streamingAssetsPath,
            tableFileName + ".csv"
        );

        if (!File.Exists(csvPath))
        {
            Debug.LogError(
                "CSV 파일을 찾을 수 없습니다: " +
                csvPath,
                this
            );

            return false;
        }

        try
        {
            // 엑셀이 열어 둔 상태에서도 읽을 수 있도록 공유 모드로 엽니다.
            using (FileStream fs = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader sr = new StreamReader(fs, System.Text.Encoding.UTF8, true))
            {
                csvText = sr.ReadToEnd();
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                "CSV 파일을 읽는 중 오류가 발생했습니다: " +
                csvPath +
                "\n" +
                exception.Message,
                this
            );

            return false;
        }

        return true;
    }

    private void ParseMobTableText(string csvText)
    {
        string[] lines =
            csvText.Split('\n');

        if (lines.Length <= 1)
        {
            Debug.LogWarning(
                "MobTable에 데이터가 없습니다.",
                this
            );

            return;
        }

        for (int i = 1;
             i < lines.Length;
             i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            string[] columns =
                line.Split(',');

            MobTurnData turnData =
                new MobTurnData
                {
                    id = GetInt(columns, 0),
                    level = GetInt(columns, 1),
                    waveTimer = GetFloat(columns, 27)
                };

            /*
             * CSV 구조:
             * id, level,
             *
             * delay1, mob1_1, value1_1, mob1_2, value1_2,
             * delay2, mob2_1, value2_1, mob2_2, value2_2,
             * delay3, mob3_1, value3_1, mob3_2, value3_2,
             * delay4, mob4_1, value4_1, mob4_2, value4_2,
             * delay5, mob5_1, value5_1, mob5_2, value5_2,
             * wavetimer
             */
            for (int turnIndex = 0;
                 turnIndex < 5;
                 turnIndex++)
            {
                int baseIndex =
                    2 + turnIndex * 5;

                float delay =
                    GetFloat(
                        columns,
                        baseIndex
                    );

                MobTurnSpawnData turnSpawnData =
                    new MobTurnSpawnData
                    {
                        delay = delay
                    };

                // 한 Turn에 최대 2종류
                for (int mobSlot = 0;
                     mobSlot < 2;
                     mobSlot++)
                {
                    int mobIndex =
                        baseIndex + 1 + mobSlot * 2;

                    int valueIndex =
                        mobIndex + 1;

                    string mobName =
                        GetString(
                            columns,
                            mobIndex
                        );

                    int count =
                        GetInt(
                            columns,
                            valueIndex
                        );

                    if (string.IsNullOrEmpty(mobName) ||
                        count <= 0)
                    {
                        continue;
                    }

                    turnSpawnData.mobList.Add(
                        new MobSpawnData
                        {
                            mobName = mobName,
                            count = count
                        }
                    );
                }

                if (turnSpawnData.mobList.Count > 0)
                {
                    turnData.turnList.Add(
                        turnSpawnData
                    );
                }
            }

            turnDataList.Add(turnData);
        }

        Debug.Log(
            "MobTable 로드 완료. 총 웨이브 수: " +
            turnDataList.Count
        );
    }

    private void PrepareNpcWaveReward(
        int waveIndex)
    {
        if (rewardNpc == null)
        {
            rewardNpc = FindFirstObjectByType<NPCWaveReward>();
        }

        if (rewardNpc == null)
        {
            Debug.LogWarning("NPCWaveReward가 연결되지 않았습니다.", this);
            return;
        }

        if (waveIndex < 0 ||
            waveIndex >= waveEndItemPrefabs.Count)
        {
            rewardNpc.ClearReward();
            return;
        }

        GameObject rewardPrefab = waveEndItemPrefabs[waveIndex];

        if (rewardPrefab == null)
        {
            rewardNpc.ClearReward();
            return;
        }

        int waveNumber = turnDataList[waveIndex].level;

        Debug.Log(
            "[WaveReward] NPC 보상 등록 시도 / Wave=" +
            waveNumber +
            " / Prefab=" +
            rewardPrefab.name,
            this
        );

        rewardNpc.SetWaveReward(
            waveNumber,
            rewardPrefab
        );

        Debug.Log(
            "[WaveReward] NPC 보상 등록 호출 완료 / Wave=" +
            waveNumber,
            this
        );
    }

    // Legacy: 현재 호출되지 않으므로 필드 아이템은 생성되지 않습니다.
    private void SpawnWaveEndItem(
        int waveIndex)
    {
        if (itemSpot == null)
        {
            Debug.LogWarning(
                "ItemSpot이 연결되지 않았습니다.",
                this
            );

            return;
        }

        if (waveIndex < 0 ||
            waveIndex >= waveEndItemPrefabs.Count)
        {
            Debug.Log(
                $"{waveIndex + 1}웨이브 종료 아이템이 등록되지 않았습니다."
            );

            return;
        }

        GameObject itemPrefab =
            waveEndItemPrefabs[waveIndex];

        if (itemPrefab == null)
        {
            return;
        }

        if (removePreviousItem &&
            currentWaveItem != null)
        {
            Destroy(currentWaveItem);
        }

        GameObject item =
            Instantiate(
                itemPrefab,
                itemSpot.position,
                itemSpot.rotation
            );

        Vector3 finalPosition =
            FindAvailableItemPosition(
                item,
                itemSpot.position
            );

        item.transform.position = finalPosition;
        currentWaveItem = item;

        Debug.Log(
            $"{waveIndex + 1}웨이브 종료 아이템 등장: " +
            itemPrefab.name
        );
    }

    private Vector3 FindAvailableItemPosition(
        GameObject item,
        Vector3 basePosition)
    {
        Collider itemCollider =
            item.GetComponent<Collider>();

        if (itemCollider == null)
        {
            itemCollider =
                item.GetComponentInChildren<Collider>();
        }

        if (itemCollider == null)
        {
            Debug.LogWarning(
                "아이템에 Collider가 없습니다: " +
                item.name
            );

            return basePosition;
        }

        Physics.SyncTransforms();

        Bounds itemBounds =
            itemCollider.bounds;

        float moveDistance =
            Mathf.Max(
                itemBounds.size.x,
                itemBounds.size.z
            ) + itemPlacePadding;

        for (int i = 0;
             i < maxItemPlaceAttempts;
             i++)
        {
            Vector3 checkPosition =
                basePosition;

            if (i > 0)
            {
                int side =
                    i % 2 == 1 ? -1 : 1;

                int step =
                    (i + 1) / 2;

                checkPosition +=
                    Vector3.right *
                    side *
                    moveDistance *
                    step;
            }

            item.transform.position =
                checkPosition;

            Physics.SyncTransforms();

            if (!IsOverlappingOtherItem(
                    item,
                    itemCollider))
            {
                return checkPosition;
            }
        }

        Debug.LogWarning(
            "빈 아이템 위치를 찾지 못했습니다."
        );

        return item.transform.position;
    }

    private bool IsOverlappingOtherItem(
        GameObject item,
        Collider itemCollider)
    {
        Bounds bounds =
            itemCollider.bounds;

        Collider[] hits =
            Physics.OverlapBox(
                bounds.center,
                bounds.extents,
                item.transform.rotation,
                ~0,
                QueryTriggerInteraction.Collide
            );

        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            if (hit.transform.IsChildOf(
                    item.transform))
            {
                continue;
            }

            if (IsItemCollider(hit))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsItemCollider(
        Collider targetCollider)
    {
        if (targetCollider.CompareTag("item_rifle") ||
            targetCollider.CompareTag("item_grenade") ||
            targetCollider.CompareTag("item_heal"))
        {
            return true;
        }

        return targetCollider
            .GetComponentInParent<ItemPickup>() != null;
    }

    private int GetInt(
        string[] columns,
        int index)
    {
        if (index < 0 ||
            index >= columns.Length)
        {
            return 0;
        }

        return int.TryParse(
            columns[index].Trim(),
            out int result
        )
            ? result
            : 0;
    }

    private float GetFloat(
        string[] columns,
        int index)
    {
        if (index < 0 ||
            index >= columns.Length)
        {
            return 0f;
        }

        return float.TryParse(
            columns[index].Trim(),
            out float result
        )
            ? result
            : 0f;
    }

    private string GetString(
        string[] columns,
        int index)
    {
        if (index < 0 ||
            index >= columns.Length)
        {
            return string.Empty;
        }

        return columns[index].Trim();
    }
}

[System.Serializable]
public class MobTurnData
{
    public int id;
    public int level;
    public float waveTimer;

    // 한 웨이브당 최대 5 Turn
    public List<MobTurnSpawnData> turnList =
        new List<MobTurnSpawnData>();
}

[System.Serializable]
public class MobTurnSpawnData
{
    // 해당 Turn 시작 전 대기시간
    public float delay;

    // 한 Turn당 최대 2종류의 몬스터
    public List<MobSpawnData> mobList =
        new List<MobSpawnData>();
}

[System.Serializable]
public class MobSpawnData
{
    public string mobName;
    public int count;
}
