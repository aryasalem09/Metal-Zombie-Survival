using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PlayerRaycastInteractor : MonoBehaviour
{
    [Header("Raycast")]
    public Camera interactionCamera;
    public float maxRayDistance = 20f;
    public LayerMask interactionMask = ~0;
    public KeyCode interactKey = KeyCode.E;
    public bool allowLeftMouseClick = true;
    public bool ignoreRaycastWhenPointerOverUi = true;

    [Header("Prompt")]
    public bool showPromptLabel = true;
    public Vector2 promptAnchor = new Vector2(0.5f, 0.08f);
    public Vector2 promptSize = new Vector2(780f, 68f);
    public Color promptTextColor = new Color(0.97f, 0.95f, 0.84f, 1f);
    public Color promptBackdropColor = new Color(0f, 0f, 0f, 0.62f);

    private PlayerController playerController;
    private TextMeshProUGUI promptLabel;
    private Image promptBackdrop;
    private IRaycastInteractable currentTarget;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
        }

        EnsurePromptUi();
        UpdatePrompt(null);
    }

    private void Update()
    {
        if (playerController != null && !playerController.HasInputAuthority)
        {
            UpdatePrompt(null);
            return;
        }

        if (interactionCamera == null)
        {
            interactionCamera = Camera.main;
            if (interactionCamera == null)
            {
                UpdatePrompt(null);
                return;
            }
        }

        bool pointerOverUi = ignoreRaycastWhenPointerOverUi && IsPointerOverUi();

        PlayerController activePlayer = playerController != null
            ? playerController
            : PlayerController.FindPrimary();

        if (activePlayer == null)
        {
            UpdatePrompt(null);
            currentTarget = null;
            return;
        }

        currentTarget = FindBestInteractable(activePlayer);
        UpdatePrompt(currentTarget, activePlayer);

        bool wantsInteract = Input.GetKeyDown(interactKey) ||
                             (allowLeftMouseClick && !pointerOverUi && Input.GetMouseButtonDown(0));
        if (!wantsInteract)
        {
            return;
        }

        if (currentTarget == null)
        {
            currentTarget = FindFallbackInteractable(activePlayer);
            if (currentTarget == null)
            {
                return;
            }
        }

        currentTarget.Interact(activePlayer);
    }

    private IRaycastInteractable FindBestInteractable(PlayerController activePlayer)
    {
        float rayDistance = GetEffectiveRayDistance(activePlayer);
        Ray ray = interactionCamera.ScreenPointToRay(Input.mousePosition);
        IRaycastInteractable best = null;
        float bestDistance = float.MaxValue;

        RaycastHit[] hits3D = Physics.RaycastAll(
            ray,
            rayDistance,
            interactionMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits3D.Length; i++)
        {
            RaycastHit hit = hits3D[i];
            IRaycastInteractable interactable = ResolveInteractable(hit.collider);
            if (interactable == null || !interactable.CanInteract(activePlayer))
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            best = interactable;
        }

        RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(
            ray,
            rayDistance,
            interactionMask);

        for (int i = 0; i < hits2D.Length; i++)
        {
            RaycastHit2D hit = hits2D[i];
            if (hit.collider == null)
            {
                continue;
            }

            IRaycastInteractable interactable = ResolveInteractable(hit.collider);
            if (interactable == null || !interactable.CanInteract(activePlayer))
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            best = interactable;
        }

        return best;
    }

    private float GetEffectiveRayDistance(PlayerController activePlayer)
    {
        float configuredDistance = Mathf.Max(0.1f, maxRayDistance);
        if (interactionCamera == null || activePlayer == null)
        {
            return configuredDistance;
        }

        float cameraToPlayerDistance = Vector3.Distance(
            interactionCamera.transform.position,
            activePlayer.transform.position);

        // In top-down scenes the camera is often far away on Z; ensure ray depth reaches world interactables.
        return Mathf.Max(configuredDistance, cameraToPlayerDistance + 4f);
    }

    private static IRaycastInteractable FindFallbackInteractable(PlayerController activePlayer)
    {
        if (activePlayer == null)
        {
            return null;
        }

        PaperNpcInteractable paperNpc = FindObjectOfType<PaperNpcInteractable>();
        if (paperNpc != null && paperNpc.CanInteract(activePlayer))
        {
            return paperNpc;
        }

        return null;
    }

    private static IRaycastInteractable ResolveInteractable(Component colliderComponent)
    {
        if (colliderComponent == null)
        {
            return null;
        }

        IRaycastInteractable directInteractable = colliderComponent.GetComponent(typeof(IRaycastInteractable))
                                                as IRaycastInteractable;
        if (directInteractable != null)
        {
            return directInteractable;
        }

        return colliderComponent.GetComponentInParent<IRaycastInteractable>();
    }

    private static bool IsPointerOverUi()
    {
        EventSystem eventSystem = EventSystem.current;
        return eventSystem != null && eventSystem.IsPointerOverGameObject();
    }

    private void EnsurePromptUi()
    {
        if (!showPromptLabel)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("InteractionPromptCanvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                "InteractionPromptCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 560;
        }

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform existingPrompt = canvasObject.transform.Find("PromptBackdrop");
        if (existingPrompt == null)
        {
            GameObject promptBackdropObject = new GameObject(
                "PromptBackdrop",
                typeof(RectTransform),
                typeof(Image));
            promptBackdropObject.transform.SetParent(canvasObject.transform, false);
            existingPrompt = promptBackdropObject.transform;
        }

        promptBackdrop = existingPrompt.GetComponent<Image>();
        if (promptBackdrop == null)
        {
            promptBackdrop = existingPrompt.gameObject.AddComponent<Image>();
        }

        RectTransform backdropRect = existingPrompt.GetComponent<RectTransform>();
        backdropRect.anchorMin = promptAnchor;
        backdropRect.anchorMax = promptAnchor;
        backdropRect.pivot = promptAnchor;
        backdropRect.anchoredPosition = Vector2.zero;
        backdropRect.sizeDelta = promptSize;
        promptBackdrop.color = promptBackdropColor;

        Transform existingLabel = existingPrompt.Find("PromptLabel");
        if (existingLabel == null)
        {
            GameObject labelObject = new GameObject(
                "PromptLabel",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(existingPrompt, false);
            existingLabel = labelObject.transform;
        }

        promptLabel = existingLabel.GetComponent<TextMeshProUGUI>();
        if (promptLabel == null)
        {
            promptLabel = existingLabel.gameObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform labelRect = existingLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(10f, 6f);
        labelRect.offsetMax = new Vector2(-10f, -6f);

        promptLabel.text = string.Empty;
        promptLabel.fontSize = 30f;
        promptLabel.alignment = TextAlignmentOptions.Center;
        promptLabel.enableWordWrapping = false;
        promptLabel.color = promptTextColor;
        ImportedStuffAssetUtility.ApplyUsableFont(promptLabel);
    }

    private void UpdatePrompt(IRaycastInteractable target, PlayerController player = null)
    {
        if (!showPromptLabel || promptLabel == null || promptBackdrop == null)
        {
            return;
        }

        if (target == null)
        {
            promptLabel.text = string.Empty;
            promptBackdrop.enabled = false;
            return;
        }

        promptBackdrop.enabled = true;
        promptBackdrop.color = promptBackdropColor;

        string promptText = target.GetInteractionPrompt(player);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            promptText = "Press [" + interactKey + "] to interact";
        }

        promptLabel.text = promptText;
        promptLabel.color = promptTextColor;
    }
}
