using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Level1PaperNpcBootstrap : MonoBehaviour
{
    [Header("Scene Target")]
    public string levelSceneName = "Level1";
    public bool includeTutorialScenes = true;
    public string[] tutorialSceneNames = { "Tutorial", "TutorialGame" };

    [Header("NPC Placement")]
    public Vector3 fallbackWorldPosition = new Vector3(-15f, 4.8f, 0f);
    public Vector3 playerRelativeOffset = new Vector3(2.1f, 1.2f, 0f);
    public Vector3 cameraRelativeOffset = new Vector3(1.8f, 1.1f, 0f);
    public bool spawnNearCameraWhenPlayerMissing = true;
    public float desiredMinNpcSize = 0.24f;
    public float desiredMaxNpcSize = 0.32f;
    public float maxRelocationDistance = 18f;
    [Min(0.05f)] public float spawnClearanceRadius = 0.45f;
    public bool avoidBlockedSpawnPositions = true;
    public bool logNpcSpawnPosition = true;
    public float npcWorldDepth = 0f;

    [Header("Guaranteed Visibility")]
    public bool forceRuntimeBillboardMarker = true;
    public Vector2 runtimeBillboardWorldSize = new Vector2(1.35f, 1.35f);
    public float runtimeBillboardHeightOffset = 0.56f;
    public int runtimeBillboardSortingOrder = 980;
    public Color runtimeBillboardColor = new Color(1f, 0.93f, 0.2f, 0.92f);

    [Header("Interaction")]
    public float interactionDistance = 1f;

    [Header("Runtime Recovery")]
    public bool keepNpcNearPlayer = true;
    [Min(0.1f)] public float npcRecoveryInterval = 0.75f;
    [Min(1f)] public float maxNpcDistanceFromPlayer = 12f;
    public bool repositionWhenOffScreen = true;

    private float nextRecoveryTime;
    private PaperNpcInteractable runtimePaperNpc;
    private bool hasCollectedDiaryThisScene;
    private static Sprite runtimeBillboardSprite;

    private static readonly Vector3[] TutorialCandidateOffsets =
    {
        new Vector3(1.35f, 0.95f, 0f),
        new Vector3(-1.1f, 1.2f, 0f),
        new Vector3(1.25f, -1f, 0f),
        new Vector3(-1.35f, -0.8f, 0f),
        new Vector3(0f, 1.6f, 0f),
        new Vector3(1.8f, 0f, 0f)
    };

    private static readonly Vector3[] Level1CandidateOffsets =
    {
        new Vector3(2.1f, 1.2f, 0f),
        new Vector3(1.8f, 0f, 0f),
        new Vector3(-1.8f, 0f, 0f),
        new Vector3(0f, 1.6f, 0f),
        new Vector3(0f, -1.6f, 0f),
        new Vector3(1.35f, 1.1f, 0f),
        new Vector3(-1.35f, 1.1f, 0f),
        new Vector3(1.35f, -1.1f, 0f),
        new Vector3(-1.35f, -1.1f, 0f)
    };

    private static readonly Vector3[] Level2CandidateOffsets =
    {
        new Vector3(-2.35f, 1.45f, 0f),
        new Vector3(-2f, 0.2f, 0f),
        new Vector3(-1.5f, -1.3f, 0f),
        new Vector3(0f, 1.9f, 0f),
        new Vector3(1.6f, 1.2f, 0f),
        new Vector3(-0.2f, -1.9f, 0f)
    };

    private static readonly Vector3[] Level3CandidateOffsets =
    {
        new Vector3(1.25f, -2.2f, 0f),
        new Vector3(2.1f, -1.1f, 0f),
        new Vector3(-1.4f, -1.8f, 0f),
        new Vector3(0f, -2.4f, 0f),
        new Vector3(1.6f, 0.7f, 0f),
        new Vector3(-1.8f, 1f, 0f)
    };

    private const string TutorialDiaryStory =
        "Mara Ilyin - Field Diary, Prologue\n\n" +
        "The sirens did not mark an evacuation. They marked a lock. District gates sealed first, then the broadcasts changed to looped instructions with no signatures.\n\n" +
        "At 03:17, a clean three-tone signal slipped under the noise. Every infected inside the clinic corridor stopped and faced the same direction. They were listening.\n\n" +
        "If you find this page, stay above ground at dawn and below ground after dark. The city is not empty. It is waiting.";

    private const string Level1DiaryStory =
        "Mara Ilyin - Field Diary, Fragment 12\n\n" +
        "We traced the pulse through dead substations until it converged on the old transit line. The infected avoid the lower tunnels unless the signal changes.\n\n" +
        "Someone painted route marks over the station maps. Not warnings. Instructions. They lead to a sealed blast door behind flood pumps.\n\n" +
        "I left this fragment here in case I do not come back. Restore power to Platform C and listen at 03:17.\f" +
        "Mara Ilyin - Field Diary, Fragment 13\n\n" +
        "We heard movement behind the blast door, but no footsteps. Metal on metal, like tools being arranged.\n\n" +
        "For fourteen seconds after each pulse, the infected freeze and turn toward the tunnel mouth. Whatever is transmitting, they still obey it.\n\n" +
        "Next marker: Sector Vault B. If I am gone, follow the red cable with the cracked insulation.";

    private const string Level2DiaryStory =
        "Mara Ilyin - Field Diary, Fragment 14\n\n" +
        "Sector Vault B was not a shelter. It was a relay room with every wall covered in hand-written call signs. Most were crossed out.\n\n" +
        "The pulse now carries a whisper under the tones. We slowed it on a recorder and found coordinates embedded in the static.\n\n" +
        "They point toward the drowned service quarter where the streetlights still flicker in sequence, even with no grid power.\f" +
        "Mara Ilyin - Field Diary, Fragment 15\n\n" +
        "Tonight we found a maintenance chapel below the pumps. Candles were still warm, but there were no people, only maps pinned with watchtower photos.\n\n" +
        "Every tower photo had the same symbol scratched into the corner: a split circle with three teeth. The same shape is stamped on the blast-door hinges.\n\n" +
        "If you hear boots behind you with no shadows in front of you, do not run to the nearest light. Run to the nearest water tower.";

    private const string Level3DiaryStory =
        "Mara Ilyin - Field Diary, Fragment 16\n\n" +
        "The tower reservoir was drained from the inside. At the bottom we found a hatch labeled HEARTLINE ACCESS, still powered, still unlocked.\n\n" +
        "Inside were operator consoles, all set to transmit at 03:17. One screen listed district names, then replaced them with one word: COMPLIANT.\n\n" +
        "Someone has been steering both panic routes and infection routes at the same time.\f" +
        "Mara Ilyin - Field Diary, Fragment 17\n\n" +
        "We followed the final cable to a room with no doors and one active speaker. It played the three tones once, then my own voice repeated back from a log that had never been recorded.\n\n" +
        "If this reaches you, the signal is no longer just a command. It is learning who answers.\n\n" +
        "The next fragment is not in this district. Look for the station map where every exit is painted black except one.";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapOnSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !IsDefaultTargetScene(activeScene.name))
        {
            return;
        }

        if (FindObjectOfType<Level1PaperNpcBootstrap>() != null)
        {
            return;
        }

        GameObject bootstrap = new GameObject("Level1PaperNpcBootstrap");
        Level1PaperNpcBootstrap bootstrapComponent = bootstrap.AddComponent<Level1PaperNpcBootstrap>();
        bootstrapComponent.levelSceneName = activeScene.name;
    }

    private void Start()
    {
        if (!IsSceneTarget(SceneManager.GetActiveScene().name))
        {
            Destroy(gameObject);
            return;
        }

        hasCollectedDiaryThisScene = false;
        PlayerController player = EnsurePlayerInteractor();
        runtimePaperNpc = EnsurePaperNpcExists(player);
    }

    private void OnEnable()
    {
        PaperNpcInteractable.Interacted += HandlePaperNpcInteracted;
    }

    private void OnDisable()
    {
        PaperNpcInteractable.Interacted -= HandlePaperNpcInteracted;
    }

    private void Update()
    {
        if (!keepNpcNearPlayer || !IsSceneTarget(SceneManager.GetActiveScene().name) || hasCollectedDiaryThisScene)
        {
            return;
        }

        if (Time.time < nextRecoveryTime)
        {
            return;
        }

        nextRecoveryTime = Time.time + Mathf.Max(0.1f, npcRecoveryInterval);
        PlayerController player = EnsurePlayerInteractor();
        runtimePaperNpc = EnsurePaperNpcExists(player);
        RecoverNpcPositionIfNeeded(runtimePaperNpc, player);
    }

    private static PlayerController EnsurePlayerInteractor()
    {
        PlayerController player = PlayerController.FindPrimary();
        if (player == null)
        {
            return null;
        }

        PlayerRaycastInteractor interactor = player.GetComponent<PlayerRaycastInteractor>();
        if (interactor == null)
        {
            interactor = player.gameObject.AddComponent<PlayerRaycastInteractor>();
        }

        interactor.maxRayDistance = Mathf.Max(interactor.maxRayDistance, 20f);
        interactor.interactKey = KeyCode.E;
        interactor.showPromptLabel = true;
        interactor.promptAnchor = new Vector2(0.5f, 0.08f);
        interactor.promptSize = new Vector2(900f, 70f);
        interactor.promptTextColor = new Color(0.99f, 0.96f, 0.85f, 1f);
        interactor.promptBackdropColor = new Color(0f, 0f, 0f, 0.58f);
        return player;
    }

    private PaperNpcInteractable EnsurePaperNpcExists(PlayerController player)
    {
        if (hasCollectedDiaryThisScene)
        {
            return null;
        }

        PaperNpcInteractable existingNpc = FindObjectOfType<PaperNpcInteractable>();
        if (existingNpc != null)
        {
            ConfigureNpc(existingNpc);
            ForceNpcVisibility(existingNpc.gameObject, player);
            FitNpcSize(existingNpc.gameObject, desiredMinNpcSize, desiredMaxNpcSize);

            Vector3 existingPosition = existingNpc.transform.position;
            existingPosition.z = npcWorldDepth;
            existingNpc.transform.position = existingPosition;

            Vector3 nearbyPosition = ResolvePreferredSpawnPosition(player);
            if (Vector2.Distance(existingNpc.transform.position, nearbyPosition) > maxRelocationDistance ||
                !IsSpawnPositionClear(existingNpc.transform.position, existingNpc.transform, player != null ? player.transform : null))
            {
                existingNpc.transform.position = nearbyPosition;
            }

            return existingNpc;
        }

        GameObject npcObject = SpawnPaperNpc(player);
        if (npcObject == null)
        {
            return null;
        }

        PaperNpcInteractable paperNpc = npcObject.GetComponent<PaperNpcInteractable>();
        if (paperNpc == null)
        {
            paperNpc = npcObject.AddComponent<PaperNpcInteractable>();
        }

        ConfigureNpc(paperNpc);
        return paperNpc;
    }

    private void HandlePaperNpcInteracted(PaperNpcInteractable npc, PlayerController actor)
    {
        if (npc == null || !IsSceneTarget(SceneManager.GetActiveScene().name))
        {
            return;
        }

        hasCollectedDiaryThisScene = true;
        runtimePaperNpc = null;
    }

    private GameObject SpawnPaperNpc(PlayerController player)
    {
        GameObject paperNpcPrefab = ImportedStuffAssetUtility.GetPaperNpcPrefab();
        GameObject npcObject;

        if (paperNpcPrefab != null)
        {
            npcObject = Instantiate(paperNpcPrefab);
            npcObject.name = "Paper NPC - Grimoire";
        }
        else
        {
            npcObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            npcObject.name = "Paper NPC Placeholder";
            npcObject.transform.localScale = new Vector3(0.8f, 0.8f, 0.25f);

            Renderer renderer = npcObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.9f, 0.83f, 0.66f, 1f);
            }
        }

        Vector3 basePosition = ResolvePreferredSpawnPosition(player);

        npcObject.transform.position = basePosition;
        npcObject.transform.rotation = Quaternion.identity;
        ForceNpcVisibility(npcObject, player);

        FitNpcSize(npcObject, desiredMinNpcSize, desiredMaxNpcSize);

        if (logNpcSpawnPosition)
        {
            Debug.Log("Level1PaperNpcBootstrap: Paper NPC spawned at " + basePosition);
        }

        return npcObject;
    }

    private void ConfigureNpc(PaperNpcInteractable paperNpc)
    {
        if (paperNpc == null)
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        paperNpc.interactionDistance = interactionDistance;
        paperNpc.diaryTitle = GetDiaryTitleForScene(sceneName);
        paperNpc.diaryStoryText = GetDiaryStoryForScene(sceneName);
        paperNpc.interactionActionText = "open the survivor diary";
        paperNpc.removeAfterInteraction = true;
        paperNpc.removeDelayAfterClose = 0.05f;
        paperNpc.enableIllumination = true;
        paperNpc.showExclamationMarker = true;
        paperNpc.exclamationText = "!";
        paperNpc.exclamationScale = 0.58f;
        paperNpc.exclamationHeightOffset = 0.62f;
        paperNpc.exclamationColor = new Color(1f, 0.96f, 0.18f, 1f);
        paperNpc.showWorldLocator = true;
        paperNpc.locatorSize = new Vector2(1.2f, 0.24f);
        paperNpc.locatorColor = new Color(1f, 0.93f, 0.08f, 0.96f);
        paperNpc.forceVisibleFallbackCore = true;
        paperNpc.fallbackCoreSize = new Vector2(0.42f, 0.52f);
        paperNpc.fallbackCoreColor = new Color(1f, 0.94f, 0.2f, 0.45f);
        paperNpc.forceVisibleFallbackBody = true;
        paperNpc.fallbackBodySize = new Vector3(0.18f, 0.28f, 0.06f);
        paperNpc.fallbackBodyColor = new Color(1f, 0.88f, 0.26f, 0.35f);
        paperNpc.interactionHitboxRadius = 0.35f;
        paperNpc.autoConfigureAudio = true;
    }

    private static void FitNpcSize(GameObject npcObject, float minSize, float maxSize)
    {
        if (npcObject == null || maxSize <= 0.01f)
        {
            return;
        }

        Renderer[] renderers = npcObject.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        float largestAxis = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largestAxis <= 0.001f)
        {
            return;
        }

        float clampedTargetSize = Mathf.Clamp(largestAxis, Mathf.Max(0.05f, minSize), maxSize);
        if (Mathf.Abs(clampedTargetSize - largestAxis) <= 0.001f)
        {
            return;
        }

        float scaleFactor = clampedTargetSize / Mathf.Max(0.001f, largestAxis);
        npcObject.transform.localScale *= scaleFactor;
    }

    private void ForceNpcVisibility(GameObject npcObject, PlayerController player)
    {
        if (npcObject == null)
        {
            return;
        }

        int targetLayer = -1;
        if (player != null)
        {
            targetLayer = player.gameObject.layer;
        }

        if (targetLayer < 0)
        {
            targetLayer = LayerMask.NameToLayer("Player");
        }

        if (targetLayer < 0)
        {
            targetLayer = LayerMask.NameToLayer("Default");
        }

        if (targetLayer >= 0)
        {
            SetLayerRecursively(npcObject.transform, targetLayer);
        }

        Renderer[] renderers = npcObject.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.enabled = true;

            if (renderer is SpriteRenderer spriteRenderer)
            {
                if (string.IsNullOrWhiteSpace(spriteRenderer.sortingLayerName))
                {
                    spriteRenderer.sortingLayerName = "Default";
                }

                spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 50);
            }
        }

        EnsureRuntimeBillboardMarker(npcObject);
    }

    private void EnsureRuntimeBillboardMarker(GameObject npcObject)
    {
        if (!forceRuntimeBillboardMarker || npcObject == null)
        {
            return;
        }

        Transform markerTransform = npcObject.transform.Find("PaperNpcRuntimeBillboard");
        if (markerTransform == null)
        {
            GameObject markerObject = new GameObject("PaperNpcRuntimeBillboard");
            markerTransform = markerObject.transform;
            markerTransform.SetParent(npcObject.transform, false);
        }

        SpriteRenderer markerRenderer = markerTransform.GetComponent<SpriteRenderer>();
        if (markerRenderer == null)
        {
            markerRenderer = markerTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        if (runtimeBillboardSprite == null)
        {
            runtimeBillboardSprite = CreateRuntimeRingSprite();
        }

        markerRenderer.enabled = true;
        markerRenderer.sprite = runtimeBillboardSprite;
        markerRenderer.color = runtimeBillboardColor;
        markerRenderer.sortingLayerID = GetTopSortingLayerId();
        markerRenderer.sortingOrder = Mathf.Max(50, runtimeBillboardSortingOrder);

        markerTransform.localPosition = new Vector3(0f, Mathf.Max(0f, runtimeBillboardHeightOffset), 0f);
        markerTransform.localRotation = Quaternion.identity;
        markerTransform.localScale = ConvertWorldScaleToLocal(
            npcObject.transform,
            new Vector3(
                Mathf.Max(0.2f, runtimeBillboardWorldSize.x),
                Mathf.Max(0.2f, runtimeBillboardWorldSize.y),
                1f));
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private static bool MatchesSceneName(string candidate, string expected)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        if (string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedCandidate = NormalizeSceneName(candidate);
        string normalizedExpected = NormalizeSceneName(expected);
        return string.Equals(normalizedCandidate, normalizedExpected, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSceneName(string sceneName)
    {
        return sceneName
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    private bool IsSceneTarget(string sceneName)
    {
        if (MatchesSceneName(sceneName, levelSceneName))
        {
            return true;
        }

        if (!includeTutorialScenes || tutorialSceneNames == null)
        {
            return false;
        }

        for (int i = 0; i < tutorialSceneNames.Length; i++)
        {
            if (MatchesSceneName(sceneName, tutorialSceneNames[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDefaultTargetScene(string sceneName)
    {
        if (MatchesSceneName(sceneName, "Level1") ||
            MatchesSceneName(sceneName, "Level2") ||
            MatchesSceneName(sceneName, "Level3"))
        {
            return true;
        }

        return MatchesSceneName(sceneName, "Tutorial") ||
               MatchesSceneName(sceneName, "TutorialGame");
    }

    private void RecoverNpcPositionIfNeeded(PaperNpcInteractable npc, PlayerController player)
    {
        if (npc == null)
        {
            return;
        }

        if (!npc.gameObject.activeInHierarchy)
        {
            npc.gameObject.SetActive(true);
        }

        ForceNpcVisibility(npc.gameObject, player);

        if (player == null)
        {
            return;
        }

        Vector2 npcPosition2D = npc.transform.position;
        Vector2 playerPosition2D = player.transform.position;
        bool tooFar = Vector2.Distance(npcPosition2D, playerPosition2D) > Mathf.Max(1f, maxNpcDistanceFromPlayer);
        bool blocked = !IsSpawnPositionClear(npc.transform.position, npc.transform, player.transform);
        bool offScreen = repositionWhenOffScreen && !IsInCameraView(npc.transform.position);

        if (!tooFar && !blocked && !offScreen)
        {
            return;
        }

        Vector3 replacementPosition = ResolvePreferredSpawnPosition(player);
        npc.transform.position = replacementPosition;

        if (logNpcSpawnPosition)
        {
            Debug.Log("Level1PaperNpcBootstrap: Repositioned Paper NPC to " + replacementPosition);
        }
    }

    private static bool IsInCameraView(Vector3 worldPosition)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return true;
        }

        Vector3 viewport = mainCamera.WorldToViewportPoint(worldPosition);
        if (viewport.z < 0f)
        {
            return false;
        }

        return viewport.x > 0.04f &&
               viewport.x < 0.96f &&
               viewport.y > 0.04f &&
               viewport.y < 0.96f;
    }

    private Vector3 ResolvePreferredSpawnPosition(PlayerController player)
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Vector3[] candidateOffsets = GetCandidateOffsetsForScene(sceneName);
        Vector3 scenePrimaryOffset = candidateOffsets.Length > 0
            ? candidateOffsets[0]
            : playerRelativeOffset;

        Vector3 position;
        if (player != null)
        {
            Vector3 playerPosition = player.transform.position;

            for (int i = 0; i < candidateOffsets.Length; i++)
            {
                position = playerPosition + candidateOffsets[i];
                position.z = npcWorldDepth;
                if (IsSpawnPositionClear(position, player.transform))
                {
                    return position;
                }
            }

            position = playerPosition + scenePrimaryOffset;
            position.z = npcWorldDepth;
            return position;
        }

        if (spawnNearCameraWhenPlayerMissing && Camera.main != null)
        {
            position = Camera.main.transform.position + cameraRelativeOffset;
            position.z = npcWorldDepth;
            return position;
        }

        position = GetFallbackWorldPositionForScene(sceneName);
        position.z = npcWorldDepth;
        return position;
    }

    private static Vector3[] GetCandidateOffsetsForScene(string sceneName)
    {
        if (MatchesSceneName(sceneName, "Tutorial") || MatchesSceneName(sceneName, "TutorialGame"))
        {
            return TutorialCandidateOffsets;
        }

        if (MatchesSceneName(sceneName, "Level 2"))
        {
            return Level2CandidateOffsets;
        }

        if (MatchesSceneName(sceneName, "Level 3"))
        {
            return Level3CandidateOffsets;
        }

        return Level1CandidateOffsets;
    }

    private Vector3 GetFallbackWorldPositionForScene(string sceneName)
    {
        if (MatchesSceneName(sceneName, "Tutorial") || MatchesSceneName(sceneName, "TutorialGame"))
        {
            return fallbackWorldPosition + new Vector3(0f, 1.8f, 0f);
        }

        if (MatchesSceneName(sceneName, "Level 2"))
        {
            return fallbackWorldPosition + new Vector3(-4.5f, -0.5f, 0f);
        }

        if (MatchesSceneName(sceneName, "Level 3"))
        {
            return fallbackWorldPosition + new Vector3(4f, -3f, 0f);
        }

        return fallbackWorldPosition;
    }

    private static string GetDiaryTitleForScene(string sceneName)
    {
        if (MatchesSceneName(sceneName, "Level 2"))
        {
            return "Diary Of Mara Ilyin - Fragments 14-15";
        }

        if (MatchesSceneName(sceneName, "Level 3"))
        {
            return "Diary Of Mara Ilyin - Fragments 16-17";
        }

        if (MatchesSceneName(sceneName, "Tutorial") || MatchesSceneName(sceneName, "TutorialGame"))
        {
            return "Diary Of Mara Ilyin - Prologue";
        }

        return "Diary Of Mara Ilyin - Fragments 12-13";
    }

    private static string GetDiaryStoryForScene(string sceneName)
    {
        if (MatchesSceneName(sceneName, "Level 2"))
        {
            return Level2DiaryStory;
        }

        if (MatchesSceneName(sceneName, "Level 3"))
        {
            return Level3DiaryStory;
        }

        if (MatchesSceneName(sceneName, "Tutorial") || MatchesSceneName(sceneName, "TutorialGame"))
        {
            return TutorialDiaryStory;
        }

        return Level1DiaryStory;
    }

    private static Vector3 ConvertWorldScaleToLocal(Transform parent, Vector3 worldScale)
    {
        if (parent == null)
        {
            return worldScale;
        }

        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            SafeDivide(worldScale.x, Mathf.Abs(parentScale.x)),
            SafeDivide(worldScale.y, Mathf.Abs(parentScale.y)),
            SafeDivide(worldScale.z, Mathf.Abs(parentScale.z)));
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        if (denominator < 0.0001f)
        {
            return numerator;
        }

        return numerator / denominator;
    }

    private static int GetTopSortingLayerId()
    {
        SortingLayer[] layers = SortingLayer.layers;
        if (layers == null || layers.Length == 0)
        {
            return 0;
        }

        return layers[layers.Length - 1].id;
    }

    private static Sprite CreateRuntimeRingSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.46f;
        float innerRadius = size * 0.28f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float edgeFalloff = Mathf.Clamp01((outerRadius - distance) / (size * 0.05f));
                float innerFade = Mathf.Clamp01((distance - innerRadius) / (size * 0.06f));
                float alpha = edgeFalloff * innerFade;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);

        Sprite ringSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        ringSprite.name = "PaperNpcRuntimeRingSprite";
        ringSprite.hideFlags = HideFlags.DontSave;
        return ringSprite;
    }

    private bool IsSpawnPositionClear(Vector3 worldPosition, params Transform[] ignoreRoots)
    {
        if (!avoidBlockedSpawnPositions)
        {
            return true;
        }

        float radius = Mathf.Max(0.05f, spawnClearanceRadius);

        Collider2D[] overlaps2D = Physics2D.OverlapCircleAll(worldPosition, radius);
        for (int i = 0; i < overlaps2D.Length; i++)
        {
            Collider2D overlap = overlaps2D[i];
            if (overlap == null || overlap.isTrigger)
            {
                continue;
            }

            if (IsIgnoredTransform(overlap.transform, ignoreRoots))
            {
                continue;
            }

            return false;
        }

        Collider[] overlaps3D = Physics.OverlapSphere(worldPosition, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps3D.Length; i++)
        {
            Collider overlap = overlaps3D[i];
            if (overlap == null)
            {
                continue;
            }

            if (IsIgnoredTransform(overlap.transform, ignoreRoots))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static bool IsIgnoredTransform(Transform candidate, Transform[] ignoreRoots)
    {
        if (candidate == null || ignoreRoots == null)
        {
            return false;
        }

        for (int i = 0; i < ignoreRoots.Length; i++)
        {
            Transform root = ignoreRoots[i];
            if (root == null)
            {
                continue;
            }

            if (candidate.IsChildOf(root))
            {
                return true;
            }
        }

        return false;
    }
}
