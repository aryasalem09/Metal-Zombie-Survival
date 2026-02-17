using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Scene Flow")]
    public string tutorialSceneName = "Tutorial";
    public string gameplaySceneName = "Level1";
    public string mainMenuSceneName = "Tutorial";

    [Header("Panels")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public Button retryButton;
    public Button quitButton;
    public Button victoryContinueButton;
    public bool pauseGameplayOnPanelOpen = true;

    [Header("Runtime")]
    public PlayerController player;
    public RegionWaveManager regionWaveManager;

    private bool gameEnded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnhookRuntimeReferences();
    }

    private void Start()
    {
        WireButtons();
        HookRuntimeReferences();
        HidePanels();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        gameEnded = false;
        Time.timeScale = 1f;
        HookRuntimeReferences();
        HidePanels();
    }

    private void HookRuntimeReferences()
    {
        UnhookRuntimeReferences();

        if (player == null || !player.HasInputAuthority)
        {
            player = PlayerController.FindPrimary();
        }

        if (regionWaveManager == null)
        {
            regionWaveManager = FindObjectOfType<RegionWaveManager>();
        }

        if (player != null)
        {
            player.PlayerDied += HandlePlayerDefeated;
        }

        if (regionWaveManager != null)
        {
            regionWaveManager.AllRegionsCompleted += HandleGameplayVictory;
        }
    }

    private void UnhookRuntimeReferences()
    {
        if (player != null)
        {
            player.PlayerDied -= HandlePlayerDefeated;
        }

        if (regionWaveManager != null)
        {
            regionWaveManager.AllRegionsCompleted -= HandleGameplayVictory;
        }
    }

    private void WireButtons()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryCurrentScene);
            retryButton.onClick.AddListener(RetryCurrentScene);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitToMainMenu);
            quitButton.onClick.AddListener(QuitToMainMenu);
        }

        if (victoryContinueButton != null)
        {
            victoryContinueButton.onClick.RemoveListener(QuitToMainMenu);
            victoryContinueButton.onClick.AddListener(QuitToMainMenu);
        }
    }

    private void HidePanels()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    public void CompleteTutorial()
    {
        LoadScene(gameplaySceneName);
    }

    public void LoadTutorial()
    {
        LoadScene(tutorialSceneName);
    }

    public void LoadGameplay()
    {
        LoadScene(gameplaySceneName);
    }

    public void RetryCurrentScene()
    {
        Time.timeScale = 1f;
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

    public void QuitToMainMenu()
    {
        Time.timeScale = 1f;
        LoadScene(mainMenuSceneName);
    }

    public void HandlePlayerDefeated()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        if (pauseGameplayOnPanelOpen)
        {
            Time.timeScale = 0f;
        }
    }

    private void HandleGameplayVictory()
    {
        if (gameEnded)
        {
            return;
        }

        gameEnded = true;
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (pauseGameplayOnPanelOpen)
        {
            Time.timeScale = 0f;
        }
    }

    private static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("GameFlowManager tried to load an empty scene name.");
            return;
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
