using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    public bool autoCreatePanelsIfMissing = true;

    [Header("Runtime")]
    public PlayerController player;
    public RegionWaveManager regionWaveManager;
    public WaveManager waveManager;

    private bool gameEnded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            return;
        }

        GameFlowManager existing = FindObjectOfType<GameFlowManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        GameObject runtimeObject = new GameObject("GameFlowManager");
        runtimeObject.AddComponent<GameFlowManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        InitializeForCurrentScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeForCurrentScene();
    }

    private void InitializeForCurrentScene()
    {
        gameEnded = false;
        Time.timeScale = 1f;

        HookRuntimeReferences();
        EnsurePanelReferences();
        WireButtons();
        HidePanels();
    }

    private void HookRuntimeReferences()
    {
        UnhookRuntimeReferences();

        if (player == null || !player.HasInputAuthority)
        {
            player = PlayerController.FindPrimary();
        }

        if (regionWaveManager == null || !regionWaveManager.isActiveAndEnabled)
        {
            regionWaveManager = FindObjectOfType<RegionWaveManager>();
        }

        if (waveManager == null || !waveManager.isActiveAndEnabled)
        {
            waveManager = FindObjectOfType<WaveManager>();
        }

        if (player != null)
        {
            player.PlayerDied += HandlePlayerDefeated;
        }

        if (regionWaveManager != null)
        {
            regionWaveManager.AllRegionsCompleted += HandleGameplayVictory;
        }

        if (waveManager != null)
        {
            waveManager.AllWavesCompleted += HandleGameplayVictory;
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

        if (waveManager != null)
        {
            waveManager.AllWavesCompleted -= HandleGameplayVictory;
        }
    }

    private void EnsurePanelReferences()
    {
        if (gameOverPanel == null)
        {
            gameOverPanel = FindPanelByName("GameOverScreen", "GameOverPanel");
        }

        if (victoryPanel == null)
        {
            victoryPanel = FindPanelByName("VictoryScreen", "VictoryPanel", "MissionCompletePanel");
        }

        if (gameOverPanel != null && victoryPanel != null && gameOverPanel == victoryPanel)
        {
            victoryPanel = null;
        }

        if (retryButton == null && gameOverPanel != null)
        {
            retryButton = FindButtonInPanel(gameOverPanel.transform, "Retry", "Restart");
        }

        if (quitButton == null && gameOverPanel != null)
        {
            quitButton = FindButtonInPanel(gameOverPanel.transform, "Quit", "Menu", "Main");
        }

        if (victoryContinueButton == null && victoryPanel != null)
        {
            victoryContinueButton = FindButtonInPanel(victoryPanel.transform, "Continue", "Next", "Menu");
        }

        if (!autoCreatePanelsIfMissing)
        {
            return;
        }

        if (gameOverPanel == null)
        {
            gameOverPanel = CreateRuntimePanel(
                "GameOverScreen",
                "YOU DIED",
                new Color(0.18f, 0.05f, 0.05f, 0.95f));
        }

        if (retryButton == null)
        {
            retryButton = CreateRuntimeButton(
                gameOverPanel.transform,
                "RetryButton",
                "Retry",
                new Vector2(0.18f, 0.12f),
                new Vector2(0.45f, 0.28f),
                new Color(0.72f, 0.27f, 0.2f, 1f));
        }

        if (quitButton == null)
        {
            quitButton = CreateRuntimeButton(
                gameOverPanel.transform,
                "QuitButton",
                "Main Menu",
                new Vector2(0.55f, 0.12f),
                new Vector2(0.82f, 0.28f),
                new Color(0.2f, 0.2f, 0.2f, 0.95f));
        }

        if (victoryPanel == null)
        {
            victoryPanel = CreateRuntimePanel(
                "VictoryScreen",
                "MISSION COMPLETE",
                new Color(0.06f, 0.17f, 0.09f, 0.95f));
        }

        if (victoryContinueButton == null)
        {
            victoryContinueButton = CreateRuntimeButton(
                victoryPanel.transform,
                "VictoryContinueButton",
                "Continue",
                new Vector2(0.32f, 0.12f),
                new Vector2(0.68f, 0.28f),
                new Color(0.14f, 0.45f, 0.22f, 1f));
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
            victoryContinueButton.onClick.RemoveListener(HandleVictoryContinuePressed);
            victoryContinueButton.onClick.AddListener(HandleVictoryContinuePressed);
        }
    }

    private void HidePanels()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
        }
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
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
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

    private void HandleVictoryContinuePressed()
    {
        string nextScene = waveManager != null && waveManager.config != null
            ? waveManager.config.nextSceneName
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(nextScene))
        {
            LoadScene(nextScene);
            return;
        }

        QuitToMainMenu();
    }

    private static GameObject FindPanelByName(params string[] candidates)
    {
        GameObject[] roots = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            for (int j = 0; j < roots.Length; j++)
            {
                GameObject gameObject = roots[j];
                if (gameObject == null ||
                    !gameObject.scene.IsValid() ||
                    !string.Equals(gameObject.name, candidate, System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return gameObject;
            }
        }

        return null;
    }

    private static Button FindButtonInPanel(Transform panelRoot, params string[] keywordCandidates)
    {
        if (panelRoot == null)
        {
            return null;
        }

        Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < keywordCandidates.Length; i++)
        {
            string keyword = keywordCandidates[i];
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            for (int j = 0; j < buttons.Length; j++)
            {
                Button button = buttons[j];
                if (button == null)
                {
                    continue;
                }

                if (button.gameObject.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return button;
                }
            }
        }

        return buttons.Length > 0 ? buttons[0] : null;
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

    private static Canvas EnsureRuntimeCanvas()
    {
        GameObject existing = GameObject.Find("GameFlowCanvas");
        if (existing == null)
        {
            existing = new GameObject(
                "GameFlowCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas canvas = existing.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;
        }

        CanvasScaler scaler = existing.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        EnsureEventSystemExists();
        return canvas;
    }

    private static GameObject CreateRuntimePanel(string name, string title, Color panelColor)
    {
        Canvas canvas = EnsureRuntimeCanvas();
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.27f, 0.3f);
        rect.anchorMax = new Vector2(0.73f, 0.7f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = panelColor;

        GameObject labelObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panelObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.08f, 0.62f);
        labelRect.anchorMax = new Vector2(0.92f, 0.9f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI titleText = labelObject.GetComponent<TextMeshProUGUI>();
        titleText.text = title;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = 52f;
        titleText.color = new Color(0.97f, 0.94f, 0.83f, 1f);
        titleText.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        titleText.outlineWidth = 0.18f;
        ImportedStuffAssetUtility.ApplyUsableFont(titleText, ImportedStuffAssetUtility.GetGameplayFont());

        return panelObject;
    }

    private static Button CreateRuntimeButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color buttonColor)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonColor * 1.12f;
        colors.pressedColor = buttonColor * 0.84f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.fontSize = 30f;
        labelText.color = Color.white;
        labelText.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        labelText.outlineWidth = 0.14f;
        ImportedStuffAssetUtility.ApplyUsableFont(labelText, ImportedStuffAssetUtility.GetGameplayFont());

        return button;
    }

    private static void EnsureEventSystemExists()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystemObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }
}
