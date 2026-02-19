using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TutorialFlowController : MonoBehaviour
{
    private static readonly string[] TutorialSceneNames =
    {
        "Tutorial",
        "TutorialGame"
    };

    public enum TutorialObjective
    {
        None,
        Move,
        Attack,
        InteractPaperNpc,
        RadialBurst
    }

    [Serializable]
    public class TutorialStep
    {
        [TextArea(2, 5)] public string text;
        public TutorialObjective objective = TutorialObjective.None;
    }

    [Header("References")]
    public DialoguePanelUI dialoguePanel;
    public PlayerController player;
    public GameFlowManager gameFlowManager;

    [Header("Scene Flow")]
    public string gameplaySceneName = "Level1";
    public bool autoLoadGameplayOnComplete = true;

    [Header("Tutorial Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>();
    public bool grantBurstChargeForTutorial = true;
    public bool pauseEnemiesUntilTutorialComplete = true;
    [Min(0.05f)] public float enemyPauseRefreshInterval = 0.25f;

    [Header("Paper NPC Objective")]
    public Vector3 tutorialPaperNpcOffset = new Vector3(1.4f, 0.9f, 0f);
    [Min(1f)] public float tutorialPaperNpcMaxDistance = 8f;
    [Min(0.1f)] public float tutorialPaperNpcRepositionInterval = 0.5f;

    private int currentStepIndex;
    private bool attackObjectiveCompleted;
    private bool paperNpcObjectiveCompleted;
    private bool burstObjectiveCompleted;
    private PlayerController subscribedPlayer;
    private bool tutorialCompleted;
    private float nextEnemyPauseRefreshTime;
    private float nextPaperNpcRepositionTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapTutorialFlow()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !IsTutorialScene(activeScene.name))
        {
            return;
        }

        if (FindObjectOfType<TutorialFlowController>() != null)
        {
            return;
        }

        PlayerController primaryPlayer = PlayerController.FindPrimary();
        if (primaryPlayer == null)
        {
            return;
        }

        GameObject runtimeObject = new GameObject("TutorialFlowRuntime");
        TutorialFlowController flow = runtimeObject.AddComponent<TutorialFlowController>();
        flow.player = primaryPlayer;
        flow.gameFlowManager = FindObjectOfType<GameFlowManager>();
        flow.dialoguePanel = FindObjectOfType<DialoguePanelUI>() ?? CreateRuntimeDialoguePanel();
    }

    private void Start()
    {
        if (dialoguePanel == null)
        {
            dialoguePanel = FindObjectOfType<DialoguePanelUI>();
            if (dialoguePanel == null)
            {
                dialoguePanel = CreateRuntimeDialoguePanel();
            }
        }

        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (gameFlowManager == null)
        {
            gameFlowManager = FindObjectOfType<GameFlowManager>();
        }

        DisableTutorialWaveManager();

        if (steps == null || steps.Count == 0)
        {
            BuildDefaultSteps();
        }
        else
        {
            EnsurePaperNpcObjectiveStepExists();
        }

        EnsurePaperNpcBootstrapInScene();
        ApplyDialogueStyling(dialoguePanel);

        if (dialoguePanel != null)
        {
            dialoguePanel.NextPressed += HandleNextPressed;
        }

        PaperNpcInteractable.Interacted += HandlePaperNpcInteracted;
        BindPlayerEvents(player);
        SetEnemyState(false);
        tutorialCompleted = false;
        nextEnemyPauseRefreshTime = Time.time;

        currentStepIndex = 0;
        ShowCurrentStep();
    }

    private void OnDestroy()
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.NextPressed -= HandleNextPressed;
        }

        PaperNpcInteractable.Interacted -= HandlePaperNpcInteracted;
        BindPlayerEvents(null);
    }

    private void Update()
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (player != subscribedPlayer)
        {
            BindPlayerEvents(player);
        }

        if (!tutorialCompleted &&
            pauseEnemiesUntilTutorialComplete &&
            Time.time >= nextEnemyPauseRefreshTime)
        {
            SetEnemyState(false);
            nextEnemyPauseRefreshTime = Time.time + Mathf.Max(0.05f, enemyPauseRefreshInterval);
        }

        if (dialoguePanel == null || player == null || currentStepIndex >= steps.Count)
        {
            return;
        }

        TutorialObjective objective = steps[currentStepIndex].objective;
        switch (objective)
        {
            case TutorialObjective.Move:
                if (player.MovementDirection.sqrMagnitude > 0.01f)
                {
                    dialoguePanel.SetNextInteractable(true);
                }
                break;
            case TutorialObjective.Attack:
                if (attackObjectiveCompleted)
                {
                    dialoguePanel.SetNextInteractable(true);
                }
                break;
            case TutorialObjective.InteractPaperNpc:
                EnsurePaperNpcNearPlayer();
                if (paperNpcObjectiveCompleted)
                {
                    dialoguePanel.SetNextInteractable(true);
                }
                break;
            case TutorialObjective.RadialBurst:
                if (burstObjectiveCompleted)
                {
                    dialoguePanel.SetNextInteractable(true);
                }
                break;
        }
    }

    private void HandlePlayerAttack()
    {
        attackObjectiveCompleted = true;
    }

    private void HandleRadialBurst()
    {
        burstObjectiveCompleted = true;
    }

    private void HandlePaperNpcInteracted(PaperNpcInteractable npc, PlayerController actor)
    {
        if (npc == null)
        {
            return;
        }

        if (actor != null && player != null && actor != player)
        {
            return;
        }

        paperNpcObjectiveCompleted = true;
    }

    private void ShowCurrentStep()
    {
        if (dialoguePanel == null)
        {
            return;
        }

        if (currentStepIndex >= steps.Count)
        {
            CompleteTutorial();
            return;
        }

        TutorialStep step = steps[currentStepIndex];
        dialoguePanel.ShowMessage(step.text);
        dialoguePanel.SetNextButtonLabel(currentStepIndex == steps.Count - 1 ? "Start Mission" : "Next");
        dialoguePanel.SetNextInteractable(step.objective == TutorialObjective.None);

        if (step.objective == TutorialObjective.InteractPaperNpc)
        {
            EnsurePaperNpcNearPlayer(force: true);
        }

        if (step.objective == TutorialObjective.RadialBurst &&
            grantBurstChargeForTutorial &&
            player != null &&
            player.BurstCharges <= 0)
        {
            player.GrantBurstCharges(1);
        }
    }

    private void HandleNextPressed()
    {
        currentStepIndex++;
        ShowCurrentStep();
    }

    private void CompleteTutorial()
    {
        tutorialCompleted = true;
        SetEnemyState(true);

        if (dialoguePanel != null)
        {
            dialoguePanel.Hide();
        }

        if (!autoLoadGameplayOnComplete)
        {
            return;
        }

        if (gameFlowManager != null)
        {
            gameFlowManager.CompleteTutorial();
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    private void BuildDefaultSteps()
    {
        steps = new List<TutorialStep>
        {
            new TutorialStep
            {
                text = "Welcome to Metal Zombie Survival. Use WASD to move through the arena.",
                objective = TutorialObjective.Move
            },
            new TutorialStep
            {
                text = "Hold Right Mouse Button to release energy pulses.",
                objective = TutorialObjective.Attack
            },
            new TutorialStep
            {
                text =
                    "Find the glowing diary and press [E] (or Left Click) while aiming at it to interact.",
                objective = TutorialObjective.InteractPaperNpc
            },
            new TutorialStep
            {
                text = "Every 10 kills grants a Radial Burst charge. Press Q to fire 10 pulses around you.",
                objective = TutorialObjective.RadialBurst
            },
            new TutorialStep
            {
                text = "Region gates lock during waves and open after objectives complete. Stay alive.",
                objective = TutorialObjective.None
            },
            new TutorialStep
            {
                text = "Good luck.",
                objective = TutorialObjective.None
            }
        };
    }

    private void EnsurePaperNpcObjectiveStepExists()
    {
        if (steps == null)
        {
            steps = new List<TutorialStep>();
        }

        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].objective == TutorialObjective.InteractPaperNpc)
            {
                return;
            }
        }

        int insertIndex = steps.Count;
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] != null && steps[i].objective == TutorialObjective.Attack)
            {
                insertIndex = i + 1;
                break;
            }
        }

        steps.Insert(insertIndex, new TutorialStep
        {
            text = "Find the glowing diary and press [E] (or Left Click) while aiming at it to interact.",
            objective = TutorialObjective.InteractPaperNpc
        });
    }

    private void BindPlayerEvents(PlayerController targetPlayer)
    {
        if (subscribedPlayer != null)
        {
            subscribedPlayer.ProjectileAttackPerformed -= HandlePlayerAttack;
            subscribedPlayer.RadialBurstUsed -= HandleRadialBurst;
        }

        subscribedPlayer = targetPlayer;
        if (subscribedPlayer == null)
        {
            return;
        }

        subscribedPlayer.ProjectileAttackPerformed += HandlePlayerAttack;
        subscribedPlayer.RadialBurstUsed += HandleRadialBurst;
    }

    private void SetEnemyState(bool enabled)
    {
        if (!pauseEnemiesUntilTutorialComplete)
        {
            return;
        }

        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieAI zombie = zombies[i];
            if (zombie != null)
            {
                zombie.enabled = enabled;
            }
        }
    }

    private static bool IsTutorialScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        for (int i = 0; i < TutorialSceneNames.Length; i++)
        {
            if (string.Equals(sceneName, TutorialSceneNames[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void DisableTutorialWaveManager()
    {
        if (!IsTutorialScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        WaveManager waveManager = FindObjectOfType<WaveManager>();
        if (waveManager != null)
        {
            waveManager.StopAllCoroutines();
            waveManager.enabled = false;
        }

        GameObject waveStatusCanvas = GameObject.Find("WaveStatusCanvas");
        if (waveStatusCanvas != null)
        {
            waveStatusCanvas.SetActive(false);
        }
    }

    private void EnsurePaperNpcBootstrapInScene()
    {
        if (FindObjectOfType<Level1PaperNpcBootstrap>() != null)
        {
            return;
        }

        if (!IsTutorialScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        GameObject bootstrapObject = new GameObject("TutorialPaperNpcBootstrap");
        Level1PaperNpcBootstrap bootstrap = bootstrapObject.AddComponent<Level1PaperNpcBootstrap>();
        bootstrap.levelSceneName = "Tutorial";
        bootstrap.includeTutorialScenes = true;
    }

    private void ApplyDialogueStyling(DialoguePanelUI panel)
    {
        if (panel == null)
        {
            return;
        }

        TMP_FontAsset preferredFont = ImportedStuffAssetUtility.GetGameplayFont();
        TextMeshProUGUI[] texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null)
            {
                continue;
            }

            ImportedStuffAssetUtility.ApplyUsableFont(text, preferredFont);

            text.outlineColor = new Color(0f, 0f, 0f, 0.75f);
            text.outlineWidth = Mathf.Max(text.outlineWidth, 0.16f);
        }

        Image[] images = panel.GetComponentsInChildren<Image>(true);
        Sprite tutorialBackdropSprite = ImportedStuffAssetUtility.GetTutorialBackgroundSprite();
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            string objectName = image.gameObject.name;
            if (string.Equals(objectName, "DialoguePanel", StringComparison.OrdinalIgnoreCase))
            {
                image.color = new Color(0.2f, 0.15f, 0.1f, 0.92f);
                if (tutorialBackdropSprite != null)
                {
                    image.sprite = tutorialBackdropSprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = false;
                }
            }
            else if (string.Equals(objectName, "NextButton", StringComparison.OrdinalIgnoreCase))
            {
                image.color = new Color(0.74f, 0.34f, 0.16f, 1f);
            }
        }
    }

    private static DialoguePanelUI CreateRuntimeDialoguePanel()
    {
        EnsureEventSystemExists();

        GameObject canvasObject = new GameObject("TutorialDialogueCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panelObject = CreateUiElement("DialoguePanel", canvasObject.transform);
        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0.2f, 0.15f, 0.1f, 0.92f);
        Sprite backdropSprite = ImportedStuffAssetUtility.GetTutorialBackgroundSprite();
        if (backdropSprite != null)
        {
            panelImage.sprite = backdropSprite;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = false;
        }

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.03f);
        panelRect.anchorMax = new Vector2(0.92f, 0.34f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TMP_FontAsset defaultFont = ImportedStuffAssetUtility.GetGameplayFont() ?? TMP_Settings.defaultFontAsset;

        GameObject messageObject = CreateUiElement("Message", panelObject.transform);
        TextMeshProUGUI messageText = messageObject.AddComponent<TextMeshProUGUI>();
        messageText.text = string.Empty;
        messageText.fontSize = 32f;
        messageText.enableWordWrapping = true;
        messageText.color = new Color(0.99f, 0.95f, 0.85f, 1f);
        messageText.alignment = TextAlignmentOptions.MidlineLeft;
        messageText.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        messageText.outlineWidth = 0.18f;
        if (defaultFont != null)
        {
            ImportedStuffAssetUtility.ApplyUsableFont(messageText, defaultFont);
        }

        RectTransform messageRect = messageObject.GetComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.03f, 0.22f);
        messageRect.anchorMax = new Vector2(0.77f, 0.95f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        GameObject buttonObject = CreateUiElement("NextButton", panelObject.transform);
        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = new Color(0.74f, 0.34f, 0.16f, 1f);
        Button nextButton = buttonObject.AddComponent<Button>();

        ColorBlock buttonColors = nextButton.colors;
        buttonColors.normalColor = new Color(0.74f, 0.34f, 0.16f, 1f);
        buttonColors.highlightedColor = new Color(0.86f, 0.42f, 0.2f, 1f);
        buttonColors.pressedColor = new Color(0.55f, 0.24f, 0.11f, 1f);
        buttonColors.selectedColor = buttonColors.highlightedColor;
        nextButton.colors = buttonColors;

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.77f, 0.14f);
        buttonRect.anchorMax = new Vector2(0.97f, 0.54f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        GameObject buttonTextObject = CreateUiElement("Label", buttonObject.transform);
        TextMeshProUGUI nextButtonLabel = buttonTextObject.AddComponent<TextMeshProUGUI>();
        nextButtonLabel.text = "Next";
        nextButtonLabel.fontSize = 30f;
        nextButtonLabel.alignment = TextAlignmentOptions.Center;
        nextButtonLabel.color = Color.white;
        nextButtonLabel.outlineColor = new Color(0f, 0f, 0f, 0.75f);
        nextButtonLabel.outlineWidth = 0.16f;
        if (defaultFont != null)
        {
            ImportedStuffAssetUtility.ApplyUsableFont(nextButtonLabel, defaultFont);
        }

        RectTransform labelRect = buttonTextObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        DialoguePanelUI panelUi = canvasObject.AddComponent<DialoguePanelUI>();
        panelUi.Configure(panelObject, messageText, nextButton, nextButtonLabel);
        panelUi.Hide();
        return panelUi;
    }

    private static GameObject CreateUiElement(string name, Transform parent)
    {
        GameObject element = new GameObject(name, typeof(RectTransform));
        element.transform.SetParent(parent, false);
        return element;
    }

    private static void EnsureEventSystemExists()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));

        eventSystemObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    }

    private void EnsurePaperNpcNearPlayer(bool force = false)
    {
        if (paperNpcObjectiveCompleted || player == null)
        {
            return;
        }

        if (!force && Time.time < nextPaperNpcRepositionTime)
        {
            return;
        }

        nextPaperNpcRepositionTime = Time.time + Mathf.Max(0.1f, tutorialPaperNpcRepositionInterval);

        PaperNpcInteractable npc = FindObjectOfType<PaperNpcInteractable>();
        if (npc == null)
        {
            return;
        }

        Vector3 playerPosition = player.transform.position;
        Vector3 targetPosition = playerPosition + tutorialPaperNpcOffset;
        targetPosition.z = npc.transform.position.z;

        if (!force &&
            Vector2.Distance(new Vector2(npc.transform.position.x, npc.transform.position.y),
                             new Vector2(playerPosition.x, playerPosition.y)) <= tutorialPaperNpcMaxDistance)
        {
            return;
        }

        npc.transform.position = targetPosition;
    }
}
