using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class GameStartManager : MonoBehaviour
{
    [Header("Title UI")]
    public GameObject titlePanel;
    public Button startButton;

    [Header("Gameplay UI")]
    public GameObject gameplayUIRoot;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public Button restartButton;

    [Header("Player")]
    public TopDownPlayerController playerController;

    [Header("Spawner")]
    public MobTableSpawner mobTableSpawner;

    [Header("Cameras")]
    public Camera titleCamera;
    public Camera gameCamera;

    [Header("Camera Scripts")]
    public TitleOrbitCamera titleOrbitCamera;
    public MonoBehaviour gameCameraFollow;

    [Header("Options")]
    public bool pauseBeforeStart = true;

    [Header("Pause")]
    [Tooltip("ESC로 여는 일시정지 창. 비워두면 이름(Pause Popup)으로 자동 검색합니다.")]
    public GameObject pausePopup;
    public string pausePopupObjectName = "Pause Popup";
    public KeyCode pauseKey = KeyCode.Escape;

    [Tooltip("일시정지 창 버튼 (비워두면 Pause Popup 아래에서 이름으로 찾습니다)")]
    public Button resumeButton;
    public Button homeButton;
    public Button exitButton;

    private bool isPaused;
    private float timeScaleBeforePause = 1f;
    private bool combatActiveBeforePause;

    public bool IsPaused => isPaused;

    private bool gameStarted;
    private bool gameOver;

    private AudioListener titleAudioListener;
    private AudioListener gameAudioListener;

    void Awake()
    {
        SetupPausePopup();

        gameStarted = false;
        gameOver = false;

        // Inspector에서 비어 있으면 씬에서 자동으로 찾습니다.
        if (playerController == null)
        {
            playerController =
                FindFirstObjectByType<TopDownPlayerController>();
        }

        if (mobTableSpawner == null)
        {
            mobTableSpawner =
                FindFirstObjectByType<MobTableSpawner>();
        }

        if (titleCamera != null)
        {
            titleAudioListener = titleCamera.GetComponent<AudioListener>();
        }

        if (gameCamera != null)
        {
            gameAudioListener = gameCamera.GetComponent<AudioListener>();
        }

        SetTitleMode();

        if (pauseBeforeStart)
        {
            Time.timeScale = 0f;
        }

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartToTitle);
            restartButton.onClick.AddListener(RestartToTitle);
        }
    }

    private void SetTitleMode()
    {
        if (titlePanel != null)
        {
            titlePanel.SetActive(true);
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
        }

        if (gameplayUIRoot != null)
        {
            gameplayUIRoot.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (titleCamera != null)
        {
            titleCamera.gameObject.SetActive(true);
        }

        if (gameCamera != null)
        {
            gameCamera.gameObject.SetActive(false);
        }

        if (titleOrbitCamera != null)
        {
            titleOrbitCamera.enabled = true;
            titleOrbitCamera.SetOrbit(true);
        }

        if (gameCameraFollow != null)
        {
            gameCameraFollow.enabled = false;
        }

        if (titleAudioListener != null)
        {
            titleAudioListener.enabled = true;
        }

        if (gameAudioListener != null)
        {
            gameAudioListener.enabled = false;
        }
    }

    private void SetGameMode()
    {
        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(false);
        }

        if (gameplayUIRoot != null)
        {
            gameplayUIRoot.SetActive(true);
        }

        if (titleOrbitCamera != null)
        {
            titleOrbitCamera.SetOrbit(false);
            titleOrbitCamera.enabled = false;
        }

        if (titleCamera != null)
        {
            titleCamera.gameObject.SetActive(false);
        }

        if (gameCamera != null)
        {
            gameCamera.gameObject.SetActive(true);
        }

        if (gameCameraFollow != null)
        {
            gameCameraFollow.enabled = true;
        }

        if (titleAudioListener != null)
        {
            titleAudioListener.enabled = false;
        }

        if (gameAudioListener != null)
        {
            gameAudioListener.enabled = true;
        }
    }

    public void StartGame()
    {
        if (gameStarted || gameOver)
        {
            return;
        }

        gameStarted = true;

        SetGameMode();

        Time.timeScale = 1f;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        if (mobTableSpawner != null)
        {
            mobTableSpawner.StartWaveSpawn();
        }

        Debug.Log("게임 시작");
    }

    /*
     * 댐 파괴 시 DamHealth가 호출합니다. (기존 연결 유지용)
     */
    public void GameOverByDamDestroyed()
    {
        TriggerGameOver("댐 HP 0");
    }

    /*
     * 모든 게임오버는 이 함수 한 곳에서 처리합니다.
     * - 댐 파괴: DamHealth -> GameOverByDamDestroyed()
     * - 웨이브 제한시간 초과: MobTableSpawner -> TriggerGameOver()
     */
    public void TriggerGameOver(string reason)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        gameStarted = false;

        // 1. 웨이브 진행(소환, 타이머, 다음 웨이브 카운트다운) 정지
        if (mobTableSpawner != null)
        {
            mobTableSpawner.StopWavesForGameOver();
        }

        // 2. 플레이어 / 카메라 정지
        if (playerController != null)
        {
            playerController.SetWaveCombatActive(false);
            playerController.enabled = false;
        }

        if (gameCameraFollow != null)
        {
            gameCameraFollow.enabled = false;
        }

        // 3. UI 전환
        if (gameplayUIRoot != null)
        {
            gameplayUIRoot.SetActive(false);
        }

        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(false);
        }

        // GameOver Canvas 루트가 꺼져 있으면 그 아래 패널을 켜도 보이지 않으므로
        // 루트(와 자식)부터 켭니다.
        if (mobTableSpawner != null)
        {
            mobTableSpawner.ShowGameOverCanvas();
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 4. 게임 정지
        Time.timeScale = 0f;

        Debug.Log("게임 오버: " + reason);
    }

    void Update()
    {
        if (!Input.GetKeyDown(pauseKey))
        {
            return;
        }

        // 게임 진행 중에만 일시정지 가능 (타이틀/게임오버 제외)
        if (!gameStarted || gameOver)
        {
            return;
        }

        if (isPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    // ---------------- Pause ----------------

    void SetupPausePopup()
    {
        if (pausePopup == null && !string.IsNullOrWhiteSpace(pausePopupObjectName))
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj != null && obj.scene.IsValid() && obj.name == pausePopupObjectName)
                {
                    pausePopup = obj;
                    break;
                }
            }
        }

        if (pausePopup == null)
        {
            Debug.LogWarning("Pause Popup을 찾지 못했습니다.", this);
            return;
        }

        if (resumeButton == null) resumeButton = FindPopupButton("Resumt_Btn", "Resume_Btn");
        if (homeButton == null) homeButton = FindPopupButton("Home_Btn");
        if (exitButton == null) exitButton = FindPopupButton("Exit Btn", "Exit_Btn");

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(ResumeGame);
            resumeButton.onClick.AddListener(ResumeGame);
        }

        if (homeButton != null)
        {
            homeButton.onClick.RemoveListener(GoHomeFromPause);
            homeButton.onClick.AddListener(GoHomeFromPause);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGameFromPause);
            exitButton.onClick.AddListener(ExitGameFromPause);
        }

        pausePopup.SetActive(false);
    }

    Button FindPopupButton(params string[] names)
    {
        foreach (Transform t in pausePopup.GetComponentsInChildren<Transform>(true))
        {
            foreach (string n in names)
            {
                if (t.name != n)
                {
                    continue;
                }

                // 이미지로만 만들어진 경우 Button을 붙여서 클릭되게 합니다.
                Button button = t.GetComponent<Button>();
                if (button == null)
                {
                    button = t.gameObject.AddComponent<Button>();
                }

                Image image = t.GetComponent<Image>();
                if (image != null)
                {
                    image.raycastTarget = true;
                    button.targetGraphic = image;
                }

                return button;
            }
        }

        Debug.LogWarning("Pause Popup에서 버튼을 찾지 못했습니다: " + string.Join("/", names), this);
        return null;
    }

    public void PauseGame()
    {
        if (isPaused || pausePopup == null)
        {
            return;
        }

        isPaused = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        if (playerController != null)
        {
            combatActiveBeforePause = playerController.IsWaveCombatActive;
            // 일시정지 중엔 조준 가이드 대신 마우스 커서를 표시하고 입력을 막습니다.
            playerController.SetWaveCombatActive(false);
            playerController.enabled = false;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        pausePopup.SetActive(true);
        pausePopup.transform.SetAsLastSibling();
    }

    public void ResumeGame()
    {
        if (!isPaused)
        {
            return;
        }

        isPaused = false;
        pausePopup.SetActive(false);

        Time.timeScale = timeScaleBeforePause > 0f ? timeScaleBeforePause : 1f;

        if (playerController != null && !gameOver)
        {
            playerController.enabled = true;
            playerController.SetWaveCombatActive(combatActiveBeforePause);
        }
    }

    public void GoHomeFromPause()
    {
        isPaused = false;
        RestartToTitle();
    }

    public void ExitGameFromPause()
    {
        // 게임 종료는 아직 구현하지 않습니다.
        Debug.Log("EXIT GAME 버튼 (아직 미구현)");
    }

    public void RestartToTitle()
    {
        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }
}