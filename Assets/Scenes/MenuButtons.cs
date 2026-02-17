using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class MenuButtons : MonoBehaviour
{
    [SerializeField] private string tutorialSceneName = "Level1";

    [Header("Tutorial Instructions")]
    [SerializeField] private bool showTutorialInstructions = true;
    [SerializeField] private bool forceShowTutorialInstructions = true;
    [SerializeField] private string instructionsTitle = "Tutorial Instructions";
    [SerializeField] private string[] instructionLines =
    {
        "Move: WASD",
        "Aim: Move Mouse",
        "Shoot: Left or Right Mouse Button",
        "Run: Hold Left Shift",
        "Crouch: Press C",
        "Radial Burst: Press Q (after earning charges)",
        "Goal: Survive each wave and clear all levels"
    };

    private const string TutorialInstructionsObjectName = "TutorialInstructionsPanel";

    private void Start()
    {
        if (forceShowTutorialInstructions)
        {
            showTutorialInstructions = true;
        }

        if (!showTutorialInstructions)
        {
            return;
        }

        if (!SceneManager.GetActiveScene().name.Equals("Tutorial", System.StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        EnsureTutorialInstructionsPanel();
    }

    public void StartGame() {
        AudioManager.Instance?.PlayUiClick();
        SceneManager.LoadScene(tutorialSceneName);
    }

    public void QuitGame()
    {
        AudioManager.Instance?.PlayUiClick();
        Application.Quit();
    }

    private void EnsureTutorialInstructionsPanel()
    {
        GameObject existingPanel = GameObject.Find(TutorialInstructionsObjectName);
        if (existingPanel != null)
        {
            TextMeshProUGUI existingText = existingPanel.GetComponentInChildren<TextMeshProUGUI>();
            if (existingText != null)
            {
                existingText.text = BuildInstructionText();
            }

            return;
        }

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(
                "TutorialCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        else if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject panelObject = new GameObject(TutorialInstructionsObjectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(20f, -20f);
        panelRect.sizeDelta = new Vector2(700f, 360f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.03f, 0.07f, 0.12f, 0.78f);

        GameObject textObject = new GameObject("TutorialInstructionsText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(panelObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(18f, 14f);
        textRect.offsetMax = new Vector2(-18f, -14f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.enableWordWrapping = true;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.color = Color.white;
        text.text = BuildInstructionText();

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            text.font = defaultFont;
        }
    }

    private string BuildInstructionText()
    {
        string header = string.IsNullOrWhiteSpace(instructionsTitle)
            ? "Tutorial Instructions"
            : instructionsTitle.Trim();

        if (instructionLines == null || instructionLines.Length == 0)
        {
            return header;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine(header);
        builder.AppendLine();

        for (int i = 0; i < instructionLines.Length; i++)
        {
            string line = instructionLines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            builder.Append("- ").AppendLine(line.Trim());
        }

        return builder.ToString();
    }
}
