using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialoguePanelUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Button nextButton;
    [SerializeField] private TextMeshProUGUI nextButtonText;
    [SerializeField] private string defaultNextLabel = "Next";

    public event Action NextPressed;

    private void Awake()
    {
        BindButton();
    }

    public void Configure(
        GameObject panelRootObject,
        TextMeshProUGUI dialogueTextComponent,
        Button nextButtonComponent,
        TextMeshProUGUI nextButtonTextComponent)
    {
        panelRoot = panelRootObject;
        dialogueText = dialogueTextComponent;
        nextButton = nextButtonComponent;
        nextButtonText = nextButtonTextComponent;

        BindButton();
        SetNextButtonLabel(defaultNextLabel);
    }

    private void BindButton()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(HandleNextPressed);
            nextButton.onClick.AddListener(HandleNextPressed);
        }
    }

    public void ShowMessage(string message)
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        if (dialogueText != null)
        {
            dialogueText.text = message;
        }
    }

    public void Hide()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    public void SetNextInteractable(bool isInteractable)
    {
        if (nextButton != null)
        {
            nextButton.interactable = isInteractable;
        }
    }

    public void SetNextButtonLabel(string label)
    {
        if (nextButtonText != null)
        {
            nextButtonText.text = string.IsNullOrWhiteSpace(label) ? defaultNextLabel : label;
        }
    }

    private void HandleNextPressed()
    {
        AudioManager.Instance?.PlayUiClick();
        NextPressed?.Invoke();
    }
}