using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PaperStoryPanelUI : MonoBehaviour
{
    private const int MaxCharactersPerPage = 620;

    private static PaperStoryPanelUI instance;

    private GameObject overlayRoot;
    private GameObject panelRoot;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI bodyText;
    private Button openDiaryButton;
    private Button previousPageButton;
    private Button nextPageButton;
    private Button closeButton;
    private TextMeshProUGUI openDiaryLabel;
    private TextMeshProUGUI previousPageLabel;
    private TextMeshProUGUI nextPageLabel;
    private TextMeshProUGUI closeLabel;
    private TextMeshProUGUI pageIndicatorText;

    private readonly List<string> storyPages = new List<string>();
    private int currentPageIndex;
    private string pendingStoryText;
    private Action onPanelShown;
    private Action onDiaryOpened;
    private Action onPanelClosed;
    private bool hasRevealedDiaryText;

    public static void Show(string title, string storyText)
    {
        Show(title, storyText, null, null, null);
    }

    public static void Show(
        string title,
        string storyText,
        Action panelShownCallback,
        Action diaryOpenedCallback,
        Action panelClosedCallback)
    {
        PaperStoryPanelUI ui = EnsureInstance();
        if (ui == null)
        {
            return;
        }

        ui.ShowInternal(
            title,
            storyText,
            panelShownCallback,
            diaryOpenedCallback,
            panelClosedCallback);
    }

    private static PaperStoryPanelUI EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<PaperStoryPanelUI>();
        if (instance != null)
        {
            return instance;
        }

        GameObject runtimeObject = new GameObject("PaperStoryPanelUI");
        instance = runtimeObject.AddComponent<PaperStoryPanelUI>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        CreateUiIfNeeded();
        Hide();
    }

    private void ShowInternal(
        string title,
        string storyText,
        Action panelShownCallback,
        Action diaryOpenedCallback,
        Action panelClosedCallback)
    {
        CreateUiIfNeeded();

        pendingStoryText = string.IsNullOrWhiteSpace(storyText)
            ? "The pages are ruined and unreadable."
            : storyText.Trim();

        onPanelShown = panelShownCallback;
        onDiaryOpened = diaryOpenedCallback;
        onPanelClosed = panelClosedCallback;
        hasRevealedDiaryText = false;
        storyPages.Clear();
        currentPageIndex = 0;

        titleText.text = string.IsNullOrWhiteSpace(title)
            ? "Survivor Diary"
            : title.Trim();

        bodyText.text =
            "A weathered journal is tied shut with faded string.\n\nPress OPEN DIARY to read the final entries.";

        openDiaryButton.gameObject.SetActive(true);
        SetPageNavigationVisible(false);

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
        }

        onPanelShown?.Invoke();
    }

    private void HandleOpenDiaryPressed()
    {
        BuildStoryPagesIfNeeded();
        currentPageIndex = 0;
        ShowCurrentPage();

        openDiaryButton.gameObject.SetActive(false);
        SetPageNavigationVisible(storyPages.Count > 1);

        if (!hasRevealedDiaryText)
        {
            hasRevealedDiaryText = true;
            onDiaryOpened?.Invoke();
        }

        if (onDiaryOpened == null)
        {
            AudioManager.Instance?.PlayUiClick();
        }
    }

    private void HandlePreviousPagePressed()
    {
        if (currentPageIndex <= 0)
        {
            return;
        }

        currentPageIndex--;
        ShowCurrentPage();
        AudioManager.Instance?.PlayUiClick();
    }

    private void HandleNextPagePressed()
    {
        if (currentPageIndex >= storyPages.Count - 1)
        {
            return;
        }

        currentPageIndex++;
        ShowCurrentPage();
        AudioManager.Instance?.PlayUiClick();
    }

    private void HandleClosePressed()
    {
        onPanelClosed?.Invoke();
        Hide();
        if (onPanelClosed == null)
        {
            AudioManager.Instance?.PlayUiClick();
        }

        ClearCallbacks();
    }

    private void Hide()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private void ClearCallbacks()
    {
        onPanelShown = null;
        onDiaryOpened = null;
        onPanelClosed = null;
        hasRevealedDiaryText = false;
        storyPages.Clear();
        currentPageIndex = 0;
    }

    private void BuildStoryPagesIfNeeded()
    {
        if (storyPages.Count > 0)
        {
            return;
        }

        string normalizedStory = NormalizeStoryText(pendingStoryText);
        if (string.IsNullOrWhiteSpace(normalizedStory))
        {
            storyPages.Add("The pages are ruined and unreadable.");
            return;
        }

        string[] forcedPages = normalizedStory.Split(new[] { '\f' }, StringSplitOptions.RemoveEmptyEntries);
        if (forcedPages.Length > 1)
        {
            for (int i = 0; i < forcedPages.Length; i++)
            {
                AddChunkedPages(forcedPages[i]);
            }
        }
        else
        {
            AddChunkedPages(normalizedStory);
        }

        if (storyPages.Count == 0)
        {
            storyPages.Add("The pages are ruined and unreadable.");
        }
    }

    private static string NormalizeStoryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n").Trim();
    }

    private void AddChunkedPages(string sourceText)
    {
        string text = NormalizeStoryText(sourceText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        int startIndex = 0;
        while (startIndex < text.Length)
        {
            int remaining = text.Length - startIndex;
            if (remaining <= MaxCharactersPerPage)
            {
                string tail = text.Substring(startIndex).Trim();
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    storyPages.Add(tail);
                }

                break;
            }

            int searchEnd = Mathf.Min(startIndex + MaxCharactersPerPage, text.Length - 1);
            int searchCount = searchEnd - startIndex + 1;

            int paragraphBreakIndex = text.LastIndexOf(
                "\n\n",
                searchEnd,
                searchCount,
                StringComparison.Ordinal);

            int spaceBreakIndex = text.LastIndexOf(' ', searchEnd, searchCount);
            int breakIndex = paragraphBreakIndex > startIndex
                ? paragraphBreakIndex
                : spaceBreakIndex;

            if (breakIndex <= startIndex)
            {
                breakIndex = startIndex + MaxCharactersPerPage;
            }

            string pageContent = text.Substring(startIndex, breakIndex - startIndex).Trim();
            if (!string.IsNullOrWhiteSpace(pageContent))
            {
                storyPages.Add(pageContent);
            }

            startIndex = breakIndex;
            while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
            {
                startIndex++;
            }
        }
    }

    private void ShowCurrentPage()
    {
        if (storyPages.Count == 0)
        {
            bodyText.text = "The pages are ruined and unreadable.";
            SetPageNavigationVisible(false);
            return;
        }

        currentPageIndex = Mathf.Clamp(currentPageIndex, 0, storyPages.Count - 1);
        bodyText.text = storyPages[currentPageIndex];
        RefreshPageControls();
    }

    private void SetPageNavigationVisible(bool visible)
    {
        if (previousPageButton != null)
        {
            previousPageButton.gameObject.SetActive(visible);
        }

        if (nextPageButton != null)
        {
            nextPageButton.gameObject.SetActive(visible);
        }

        if (pageIndicatorText != null)
        {
            pageIndicatorText.gameObject.SetActive(visible);
        }
    }

    private void RefreshPageControls()
    {
        bool hasMultiplePages = storyPages.Count > 1;
        SetPageNavigationVisible(hasMultiplePages);

        if (previousPageButton != null)
        {
            previousPageButton.interactable = hasMultiplePages && currentPageIndex > 0;
        }

        if (nextPageButton != null)
        {
            nextPageButton.interactable = hasMultiplePages && currentPageIndex < storyPages.Count - 1;
        }

        if (pageIndicatorText != null)
        {
            pageIndicatorText.text = "PAGE " + (currentPageIndex + 1) + " / " + storyPages.Count;
        }
    }

    private void CreateUiIfNeeded()
    {
        if (panelRoot != null)
        {
            return;
        }

        EnsureEventSystemExists();

        TMP_FontAsset uiFont = ImportedStuffAssetUtility.GetGameplayFont() ?? TMP_Settings.defaultFontAsset;
        GameObject canvasObject = new GameObject(
            "PaperStoryCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 610;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        overlayRoot = backdrop;
        Image backdropImage = backdrop.GetComponent<Image>();
        backdropImage.color = new Color(0f, 0f, 0f, 0.86f);
        RectTransform backdropRect = backdrop.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        panelRoot = new GameObject("PaperPanel", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        panelRoot.transform.SetParent(backdrop.transform, false);
        Image panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = new Color(0.94f, 0.9f, 0.78f, 1f);
        panelImage.sprite = ImportedStuffAssetUtility.GetPaperPanelSprite();
        panelImage.type = panelImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.08f, 0.08f);
        panelRect.anchorMax = new Vector2(0.92f, 0.92f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(panelRoot.transform, false);
        titleText = titleObject.GetComponent<TextMeshProUGUI>();
        ImportedStuffAssetUtility.ApplyUsableFont(titleText, uiFont);
        titleText.fontSize = 44f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.15f, 0.1f, 0.06f, 1f);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.08f, 0.84f);
        titleRect.anchorMax = new Vector2(0.92f, 0.96f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject bodyBackgroundObject = new GameObject("BodyBackground", typeof(RectTransform), typeof(Image));
        bodyBackgroundObject.transform.SetParent(panelRoot.transform, false);
        Image bodyBackgroundImage = bodyBackgroundObject.GetComponent<Image>();
        bodyBackgroundImage.color = new Color(0.98f, 0.95f, 0.86f, 0.94f);

        RectTransform bodyBackgroundRect = bodyBackgroundObject.GetComponent<RectTransform>();
        bodyBackgroundRect.anchorMin = new Vector2(0.07f, 0.22f);
        bodyBackgroundRect.anchorMax = new Vector2(0.93f, 0.82f);
        bodyBackgroundRect.offsetMin = Vector2.zero;
        bodyBackgroundRect.offsetMax = Vector2.zero;

        GameObject bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyObject.transform.SetParent(bodyBackgroundObject.transform, false);
        bodyText = bodyObject.GetComponent<TextMeshProUGUI>();
        ImportedStuffAssetUtility.ApplyUsableFont(bodyText, uiFont);
        bodyText.fontSize = 30f;
        bodyText.enableAutoSizing = false;
        bodyText.fontSizeMin = 20f;
        bodyText.fontSizeMax = 32f;
        bodyText.enableWordWrapping = true;
        bodyText.lineSpacing = 6f;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.color = new Color(0.2f, 0.15f, 0.1f, 1f);

        RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(30f, 24f);
        bodyRect.offsetMax = new Vector2(-30f, -22f);

        GameObject pageIndicatorObject = new GameObject("PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
        pageIndicatorObject.transform.SetParent(panelRoot.transform, false);
        pageIndicatorText = pageIndicatorObject.GetComponent<TextMeshProUGUI>();
        ImportedStuffAssetUtility.ApplyUsableFont(pageIndicatorText, uiFont);
        pageIndicatorText.fontSize = 20f;
        pageIndicatorText.alignment = TextAlignmentOptions.Center;
        pageIndicatorText.color = new Color(0.2f, 0.15f, 0.1f, 0.95f);

        RectTransform pageIndicatorRect = pageIndicatorObject.GetComponent<RectTransform>();
        pageIndicatorRect.anchorMin = new Vector2(0.33f, 0.15f);
        pageIndicatorRect.anchorMax = new Vector2(0.67f, 0.2f);
        pageIndicatorRect.offsetMin = Vector2.zero;
        pageIndicatorRect.offsetMax = Vector2.zero;

        openDiaryButton = CreateButton(
            panelRoot.transform,
            "OpenDiaryButton",
            new Vector2(0.14f, 0.06f),
            new Vector2(0.4f, 0.15f),
            "OPEN DIARY",
            uiFont,
            new Color(0.42f, 0.25f, 0.14f, 1f),
            out openDiaryLabel);
        openDiaryButton.onClick.AddListener(HandleOpenDiaryPressed);

        previousPageButton = CreateButton(
            panelRoot.transform,
            "PreviousPageButton",
            new Vector2(0.14f, 0.06f),
            new Vector2(0.27f, 0.15f),
            "< PREV",
            uiFont,
            new Color(0.35f, 0.22f, 0.12f, 0.98f),
            out previousPageLabel);
        previousPageButton.onClick.AddListener(HandlePreviousPagePressed);

        nextPageButton = CreateButton(
            panelRoot.transform,
            "NextPageButton",
            new Vector2(0.29f, 0.06f),
            new Vector2(0.42f, 0.15f),
            "NEXT >",
            uiFont,
            new Color(0.35f, 0.22f, 0.12f, 0.98f),
            out nextPageLabel);
        nextPageButton.onClick.AddListener(HandleNextPagePressed);

        closeButton = CreateButton(
            panelRoot.transform,
            "CloseButton",
            new Vector2(0.62f, 0.06f),
            new Vector2(0.86f, 0.15f),
            "CLOSE",
            uiFont,
            new Color(0.18f, 0.18f, 0.18f, 0.95f),
            out closeLabel);
        closeButton.onClick.AddListener(HandleClosePressed);

        SetPageNavigationVisible(false);
        overlayRoot.SetActive(false);
    }

    private static Button CreateButton(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        string label,
        TMP_FontAsset font,
        Color buttonColor,
        out TextMeshProUGUI labelText)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonColor * 1.15f;
        colors.pressedColor = buttonColor * 0.85f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        ImportedStuffAssetUtility.ApplyUsableFont(labelText, font);
        labelText.fontSize = 23f;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

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
