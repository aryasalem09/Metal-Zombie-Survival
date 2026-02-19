using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PaperNpcInteractable : MonoBehaviour, IRaycastInteractable
{
    public static event System.Action<PaperNpcInteractable, PlayerController> Interacted;

    [Header("Interaction")]
    public string diaryTitle = "Diary Of A Lost Survivor";
    [TextArea(5, 18)] public string diaryStoryText;
    public string interactionActionText = "Read the lost diary";
    public float interactionDistance = 4f;
    public bool enforceInteractionHitbox = true;
    [Min(0.2f)] public float interactionHitboxRadius = 0.9f;
    [Min(0f)] public float interactionHitboxHeightOffset = 0.45f;

    [Header("Visuals")]
    public bool enableIllumination = true;
    public Color illuminationColor = new Color(1f, 0.9f, 0.62f, 1f);
    public float illuminationIntensity = 1.2f;
    public float illuminationRange = 2.4f;
    [Range(0f, 1f)] public float illuminationPulseAmount = 0.2f;
    public float illuminationPulseSpeed = 2f;
    public bool showExclamationMarker = true;
    public bool hideExclamationWhileDialogOpen = true;
    public string exclamationText = "!";
    public Color exclamationColor = new Color(1f, 0.95f, 0.25f, 1f);
    public float exclamationScale = 0.28f;
    public float exclamationHeightOffset = 0.48f;
    [Range(0f, 1f)] public float exclamationPulseAmount = 0.15f;
    public float exclamationPulseSpeed = 3f;
    public float exclamationFloatAmplitude = 0.06f;
    public float exclamationFloatSpeed = 2f;

    [Header("Locator")]
    public bool showWorldLocator = true;
    public Color locatorColor = new Color(1f, 0.86f, 0.2f, 0.9f);
    public Vector2 locatorSize = new Vector2(0.95f, 0.18f);
    public int locatorSortingOrder = 900;
    [Range(0f, 1f)] public float locatorPulseAmount = 0.18f;
    public float locatorPulseSpeed = 2.4f;

    [Header("Fallback Visual")]
    public bool forceVisibleFallbackCore = true;
    public Color fallbackCoreColor = new Color(1f, 0.92f, 0.36f, 0.86f);
    public Vector2 fallbackCoreSize = new Vector2(0.72f, 0.9f);
    public int fallbackCoreSortingOrder = 920;
    [Range(0f, 1f)] public float fallbackCorePulseAmount = 0.12f;
    public float fallbackCorePulseSpeed = 2f;
    public bool forceVisibleFallbackBody = true;
    public Color fallbackBodyColor = new Color(0.93f, 0.81f, 0.52f, 0.92f);
    public Vector3 fallbackBodySize = new Vector3(0.72f, 0.9f, 0.15f);

    [Header("Audio")]
    public bool autoConfigureAudio = true;
    public AudioClip ambientLoopClip;
    public AudioClip inspectSfx;
    public AudioClip panelOpenSfx;
    public AudioClip diaryReadSfx;
    public AudioClip panelCloseSfx;
    [Range(0f, 1f)] public float ambientVolume = 0.2f;
    [Range(0f, 1f)] public float sfxVolume = 0.95f;
    public float audioMinDistance = 1.2f;
    public float audioMaxDistance = 6f;

    [Header("Debug")]
    public bool autoAddCollider = true;

    [Header("Lifecycle")]
    public bool removeAfterInteraction = true;
    [Min(0f)] public float removeDelayAfterClose = 0.05f;

    private readonly List<Material> emissiveMaterials = new List<Material>();
    private readonly List<Color> emissiveBaseColors = new List<Color>();

    private Light glowLight;
    private Transform exclamationTransform;
    private TextMeshPro exclamationLabel;
    private Vector3 exclamationBaseLocalPosition;
    private Vector3 exclamationBaseLocalScale = Vector3.one;
    private AudioSource ambientSource;
    private AudioSource sfxSource;
    private bool isDialogOpen;
    private SpriteRenderer locatorRenderer;
    private Color locatorBaseColor;
    private SpriteRenderer fallbackCoreRenderer;
    private Color fallbackCoreBaseColor;
    private MeshRenderer fallbackBodyRenderer;
    private Transform fallbackBodyTransform;
    private SphereCollider interactionHitbox;
    private bool hasBeenInteracted;
    private bool removeQueued;
    private static Sprite locatorSprite;

    private static readonly string DefaultDiaryText =
        "Mara Ilyin - Field Diary, Fragment 12\n\n" +
        "The collapse started with the winter blackouts. Hospitals switched to backup grids, then the grids never came back. " +
        "When emergency broadcasts finally returned, they were already pre-recorded and two days old.\n\n" +
        "Quarantine walls split the city into sectors. By week three, command told us to hold civilians in place and wait for extraction. " +
        "No extraction came. Every sector heard the same lie.\n\n" +
        "The infected were not random. They moved toward radio towers first, then power relays, like they were following instructions. " +
        "Whatever this is, it learned our map before we did.\n\n" +
        "Last night a signal cut through the static: three tones, repeating at exactly 03:17. " +
        "Not military. Not civilian. Too clean to be noise. Someone is still broadcasting from beneath the old transit line.\n\n" +
        "If you're reading this, humanity did not end in one blast. It was dismantled, district by district. " +
        "Find the source of that signal before it finds you.\f" +
        "Mara Ilyin - Field Diary, Fragment 13\n\n" +
        "I followed the relay echoes down to Platform C, where the station map was painted over with new symbols. " +
        "Not graffiti. Route marks. Someone has been guiding groups through maintenance tunnels the infected avoid.\n\n" +
        "The signal origin is below the flood pumps, behind a locked blast door marked with the old biotech crest. " +
        "Every twelve minutes the corridor lights flicker in sequence, like a heartbeat leading inward.\n\n" +
        "We found three survivors sheltering in a service cage. They said the infected froze whenever the three-tone pulse played. " +
        "For fourteen seconds, they stopped moving and just listened.\n\n" +
        "I am leaving this diary where people can find it. If I do not return, bring power to the lower transit line " +
        "and transmit the pulse at 03:17. It may be the only command they still obey.";

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(diaryStoryText))
        {
            diaryStoryText = DefaultDiaryText;
        }
    }

    private void Start()
    {
        if (autoAddCollider)
        {
            EnsureCollider();
        }

        if (autoConfigureAudio)
        {
            AutoAssignAudioClips();
        }

        EnsureAudioSources();
        StartAmbientLoopIfAvailable();
        CacheEmissiveMaterials();
        EnsureGlowLight();
        EnsureExclamationMarker();
        EnsureFallbackVisibleBody();
        EnsureFallbackVisibleCore();
        EnsureWorldLocator();
        EnsureInteractionHitbox();
    }

    private void Update()
    {
        UpdateIllumination();
        UpdateExclamationMarker();
        UpdateFallbackVisibleBody();
        UpdateFallbackVisibleCore();
        UpdateWorldLocator();
    }

    public string GetInteractionPrompt(PlayerController player)
    {
        if (removeAfterInteraction && hasBeenInteracted)
        {
            return "Diary already collected";
        }

        if (isDialogOpen)
        {
            return "Diary in use";
        }

        return "Press [E] or Left Click to " + interactionActionText;
    }

    public bool CanInteract(PlayerController player)
    {
        if (removeAfterInteraction && hasBeenInteracted)
        {
            return false;
        }

        if (player == null)
        {
            return true;
        }

        float maxDistance = Mathf.Max(0.5f, interactionDistance);
        return Vector2.Distance(transform.position, player.transform.position) <= maxDistance;
    }

    public void Interact(PlayerController player)
    {
        if (!CanInteract(player) || isDialogOpen)
        {
            return;
        }

        hasBeenInteracted = true;
        isDialogOpen = true;
        PlaySfx(inspectSfx);
        Interacted?.Invoke(this, player);

        if (hideExclamationWhileDialogOpen && exclamationTransform != null)
        {
            exclamationTransform.gameObject.SetActive(false);
        }

        PaperStoryPanelUI.Show(
            diaryTitle,
            diaryStoryText,
            HandlePanelShown,
            HandleDiaryOpened,
            HandlePanelClosed);
    }

    private void EnsureCollider()
    {
        if (GetComponentInChildren<Collider>() != null || GetComponentInChildren<Collider2D>() != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            SphereCollider fallbackCollider = gameObject.AddComponent<SphereCollider>();
            fallbackCollider.radius = 0.6f;
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }
        }

        BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
        boxCollider.center = transform.InverseTransformPoint(worldBounds.center);
        boxCollider.size = new Vector3(
            SafeDivide(worldBounds.size.x, Mathf.Abs(transform.lossyScale.x)),
            SafeDivide(worldBounds.size.y, Mathf.Abs(transform.lossyScale.y)),
            Mathf.Max(0.15f, SafeDivide(worldBounds.size.z, Mathf.Abs(transform.lossyScale.z))));
    }

    private void AutoAssignAudioClips()
    {
        if (ambientLoopClip == null) ambientLoopClip = ImportedStuffAssetUtility.GetAudioClip("ha-waterheater");
        if (inspectSfx == null) inspectSfx = ImportedStuffAssetUtility.GetAudioClip("electronic_02");
        if (panelOpenSfx == null) panelOpenSfx = ImportedStuffAssetUtility.GetAudioClip("card");
        if (diaryReadSfx == null) diaryReadSfx = ImportedStuffAssetUtility.GetAudioClip("magic_01");
        if (panelCloseSfx == null) panelCloseSfx = ImportedStuffAssetUtility.GetAudioClip("electronic_01");
    }

    private void EnsureAudioSources()
    {
        ambientSource = EnsureAudioSource("PaperNpcAmbientAudio");
        sfxSource = EnsureAudioSource("PaperNpcSfxAudio");

        if (ambientSource != null)
        {
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 1f;
            ambientSource.volume = Mathf.Clamp01(ambientVolume);
            ambientSource.rolloffMode = AudioRolloffMode.Logarithmic;
            ambientSource.minDistance = Mathf.Max(0.1f, audioMinDistance);
            ambientSource.maxDistance = Mathf.Max(ambientSource.minDistance + 0.1f, audioMaxDistance);
            ambientSource.dopplerLevel = 0f;
        }

        if (sfxSource != null)
        {
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 1f;
            sfxSource.volume = Mathf.Clamp01(sfxVolume);
            sfxSource.rolloffMode = AudioRolloffMode.Logarithmic;
            sfxSource.minDistance = Mathf.Max(0.1f, audioMinDistance);
            sfxSource.maxDistance = Mathf.Max(sfxSource.minDistance + 0.1f, audioMaxDistance);
            sfxSource.dopplerLevel = 0f;
        }
    }

    private AudioSource EnsureAudioSource(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
            child.localPosition = Vector3.zero;
        }

        AudioSource source = child.GetComponent<AudioSource>();
        if (source == null)
        {
            source = child.gameObject.AddComponent<AudioSource>();
        }

        return source;
    }

    private void StartAmbientLoopIfAvailable()
    {
        if (ambientSource == null || ambientLoopClip == null)
        {
            return;
        }

        ambientSource.clip = ambientLoopClip;
        if (!ambientSource.isPlaying)
        {
            ambientSource.Play();
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null)
        {
            AudioManager.Instance?.PlayUiClick();
            return;
        }

        if (sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, 1f);
            return;
        }

        AudioManager.Instance?.PlayCustomSfx(clip);
    }

    private void HandlePanelShown()
    {
        PlaySfx(panelOpenSfx);
    }

    private void HandleDiaryOpened()
    {
        PlaySfx(diaryReadSfx);
    }

    private void HandlePanelClosed()
    {
        isDialogOpen = false;
        if (hideExclamationWhileDialogOpen && exclamationTransform != null)
        {
            exclamationTransform.gameObject.SetActive(showExclamationMarker);
        }

        PlaySfx(panelCloseSfx);

        if (removeAfterInteraction && hasBeenInteracted && !removeQueued)
        {
            StartCoroutine(RemoveAfterPanelClose());
        }
    }

    private IEnumerator RemoveAfterPanelClose()
    {
        removeQueued = true;

        float delay = Mathf.Max(0f, removeDelayAfterClose);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (this != null)
        {
            Destroy(gameObject);
        }
    }

    private void CacheEmissiveMaterials()
    {
        emissiveMaterials.Clear();
        emissiveBaseColors.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            if (materials == null)
            {
                continue;
            }

            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null || !material.HasProperty("_EmissionColor"))
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                Color emissionColor = material.GetColor("_EmissionColor");
                if (emissionColor.maxColorComponent < 0.05f)
                {
                    emissionColor = illuminationColor * 0.35f;
                }

                emissiveMaterials.Add(material);
                emissiveBaseColors.Add(emissionColor);
            }
        }
    }

    private void EnsureGlowLight()
    {
        if (!enableIllumination)
        {
            return;
        }

        Transform lightTransform = transform.Find("PaperNpcGlowLight");
        if (lightTransform == null)
        {
            GameObject lightObject = new GameObject("PaperNpcGlowLight");
            lightTransform = lightObject.transform;
            lightTransform.SetParent(transform, false);
        }

        glowLight = lightTransform.GetComponent<Light>();
        if (glowLight == null)
        {
            glowLight = lightTransform.gameObject.AddComponent<Light>();
        }

        glowLight.type = LightType.Point;
        glowLight.shadows = LightShadows.None;
        glowLight.renderMode = LightRenderMode.ForcePixel;
        glowLight.range = Mathf.Max(0.2f, illuminationRange);
        glowLight.color = illuminationColor;
        glowLight.intensity = Mathf.Max(0f, illuminationIntensity);
        lightTransform.localPosition = GetWorldBoundsLocalCenter() + new Vector3(0f, 0.18f, 0f);
    }

    private void EnsureExclamationMarker()
    {
        if (!showExclamationMarker)
        {
            return;
        }

        Transform markerTransform = transform.Find("PaperNpcExclamation");
        if (markerTransform == null)
        {
            GameObject markerObject = new GameObject("PaperNpcExclamation");
            markerTransform = markerObject.transform;
            markerTransform.SetParent(transform, false);
        }

        TextMeshPro markerLabel = markerTransform.GetComponent<TextMeshPro>();
        exclamationLabel = markerLabel;
        if (exclamationLabel == null)
        {
            exclamationLabel = markerTransform.gameObject.AddComponent<TextMeshPro>();
        }

        if (markerTransform == null || exclamationLabel == null)
        {
            return;
        }

        ImportedStuffAssetUtility.ApplyUsableFont(exclamationLabel);
        exclamationLabel.text = string.IsNullOrWhiteSpace(exclamationText) ? "!" : exclamationText.Trim();
        exclamationLabel.fontSize = 9f;
        exclamationLabel.color = exclamationColor;
        exclamationLabel.alignment = TextAlignmentOptions.Center;
        exclamationLabel.enableWordWrapping = false;
        exclamationLabel.outlineWidth = 0.2f;
        exclamationLabel.outlineColor = new Color(0f, 0f, 0f, 0.55f);

        Vector3 localCenter = GetWorldBoundsLocalCenter();
        float localTopY = GetWorldBoundsLocalTopY();
        exclamationBaseLocalPosition = new Vector3(
            localCenter.x,
            localTopY + Mathf.Max(0.1f, exclamationHeightOffset),
            localCenter.z);

        float worldScale = Mathf.Max(0.05f, exclamationScale);
        exclamationBaseLocalScale = ConvertWorldScaleToLocal(new Vector3(worldScale, worldScale, worldScale));
        markerTransform.localPosition = exclamationBaseLocalPosition;
        markerTransform.localScale = exclamationBaseLocalScale;
        exclamationTransform = markerTransform;
    }

    private void UpdateIllumination()
    {
        if (!enableIllumination)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0.01f, illuminationPulseSpeed)) *
                      Mathf.Clamp01(illuminationPulseAmount);

        if (glowLight != null)
        {
            glowLight.color = illuminationColor;
            glowLight.range = Mathf.Max(0.2f, illuminationRange);
            glowLight.intensity = Mathf.Max(0f, illuminationIntensity * pulse);
        }

        for (int i = 0; i < emissiveMaterials.Count; i++)
        {
            Material material = emissiveMaterials[i];
            if (material == null)
            {
                continue;
            }

            material.SetColor("_EmissionColor", emissiveBaseColors[i] * pulse);
        }
    }

    private void UpdateExclamationMarker()
    {
        if (!showExclamationMarker || exclamationTransform == null || !exclamationTransform.gameObject.activeSelf)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0.01f, exclamationPulseSpeed)) *
                      Mathf.Clamp01(exclamationPulseAmount);
        float bob = Mathf.Sin(Time.time * Mathf.Max(0.01f, exclamationFloatSpeed)) *
                    Mathf.Max(0f, exclamationFloatAmplitude);

        exclamationTransform.localPosition = exclamationBaseLocalPosition + Vector3.up * bob;
        exclamationTransform.localScale = exclamationBaseLocalScale * pulse;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            exclamationTransform.forward = -mainCamera.transform.forward;
        }

        if (exclamationLabel != null)
        {
            exclamationLabel.color = exclamationColor;
            exclamationLabel.text = string.IsNullOrWhiteSpace(exclamationText) ? "!" : exclamationText.Trim();
        }
    }

    private void EnsureWorldLocator()
    {
        if (!showWorldLocator)
        {
            return;
        }

        Transform locatorTransform = transform.Find("PaperNpcLocator");
        if (locatorTransform == null)
        {
            GameObject locatorObject = new GameObject("PaperNpcLocator");
            locatorTransform = locatorObject.transform;
            locatorTransform.SetParent(transform, false);
        }

        locatorRenderer = locatorTransform.GetComponent<SpriteRenderer>();
        if (locatorRenderer == null)
        {
            locatorRenderer = locatorTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        if (locatorSprite == null)
        {
            locatorSprite = CreateRuntimeWhiteSprite();
            locatorSprite.name = "PaperNpcLocatorSprite";
            locatorSprite.hideFlags = HideFlags.DontSave;
        }

        locatorRenderer.sprite = locatorSprite;
        locatorRenderer.sortingLayerID = GetTopSortingLayerId();
        locatorRenderer.sortingOrder = locatorSortingOrder;
        locatorBaseColor = locatorColor;
        locatorRenderer.color = locatorBaseColor;

        Vector3 localCenter = GetWorldBoundsLocalCenter();
        float localBottomY = transform.InverseTransformPoint(GetWorldBounds().min).y;
        locatorTransform.localPosition = new Vector3(localCenter.x, localBottomY + 0.05f, localCenter.z);
        locatorTransform.localScale = ConvertWorldScaleToLocal(new Vector3(
            Mathf.Max(0.1f, locatorSize.x),
            Mathf.Max(0.05f, locatorSize.y),
            0.02f));
    }

    private void UpdateWorldLocator()
    {
        if (!showWorldLocator || locatorRenderer == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0.01f, locatorPulseSpeed)) *
                      Mathf.Clamp01(locatorPulseAmount);
        Color color = locatorBaseColor;
        color.a = Mathf.Clamp01(locatorBaseColor.a * pulse);
        locatorRenderer.color = color;

        if (hideExclamationWhileDialogOpen)
        {
            locatorRenderer.enabled = !isDialogOpen;
        }
    }

    private void EnsureFallbackVisibleCore()
    {
        if (!forceVisibleFallbackCore)
        {
            return;
        }

        Transform coreTransform = transform.Find("PaperNpcVisibleCore");
        if (coreTransform == null)
        {
            GameObject coreObject = new GameObject("PaperNpcVisibleCore");
            coreTransform = coreObject.transform;
            coreTransform.SetParent(transform, false);
        }

        fallbackCoreRenderer = coreTransform.GetComponent<SpriteRenderer>();
        if (fallbackCoreRenderer == null)
        {
            fallbackCoreRenderer = coreTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        if (locatorSprite == null)
        {
            locatorSprite = CreateRuntimeWhiteSprite();
        }

        fallbackCoreRenderer.sprite = locatorSprite;
        fallbackCoreRenderer.sortingLayerID = GetTopSortingLayerId();
        fallbackCoreRenderer.sortingOrder = fallbackCoreSortingOrder;
        fallbackCoreBaseColor = fallbackCoreColor;
        fallbackCoreRenderer.color = fallbackCoreBaseColor;

        Vector3 localCenter = GetWorldBoundsLocalCenter();
        float localBottomY = transform.InverseTransformPoint(GetWorldBounds().min).y;
        coreTransform.localPosition = new Vector3(localCenter.x, localBottomY + 0.45f, localCenter.z);
        coreTransform.localScale = ConvertWorldScaleToLocal(new Vector3(
            Mathf.Max(0.15f, fallbackCoreSize.x),
            Mathf.Max(0.15f, fallbackCoreSize.y),
            0.02f));
    }

    private void UpdateFallbackVisibleCore()
    {
        if (!forceVisibleFallbackCore || fallbackCoreRenderer == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0.01f, fallbackCorePulseSpeed)) *
                      Mathf.Clamp01(fallbackCorePulseAmount);
        Color color = fallbackCoreBaseColor;
        color.a = Mathf.Clamp01(fallbackCoreBaseColor.a * pulse);
        fallbackCoreRenderer.color = color;
        fallbackCoreRenderer.enabled = !hideExclamationWhileDialogOpen || !isDialogOpen;
    }

    private void EnsureFallbackVisibleBody()
    {
        if (!forceVisibleFallbackBody)
        {
            return;
        }

        fallbackBodyTransform = transform.Find("PaperNpcFallbackBody");
        if (fallbackBodyTransform == null)
        {
            GameObject bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "PaperNpcFallbackBody";
            bodyObject.transform.SetParent(transform, false);

            Collider bodyCollider = bodyObject.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            fallbackBodyTransform = bodyObject.transform;
        }

        fallbackBodyRenderer = fallbackBodyTransform.GetComponent<MeshRenderer>();
        if (fallbackBodyRenderer == null)
        {
            return;
        }

        Material bodyMaterial = fallbackBodyRenderer.sharedMaterial;
        if (bodyMaterial == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                bodyMaterial = new Material(shader);
                fallbackBodyRenderer.sharedMaterial = bodyMaterial;
            }
        }

        if (bodyMaterial != null)
        {
            bodyMaterial.color = fallbackBodyColor;
            if (bodyMaterial.HasProperty("_EmissionColor"))
            {
                bodyMaterial.EnableKeyword("_EMISSION");
                bodyMaterial.SetColor("_EmissionColor", fallbackBodyColor * 0.6f);
            }
        }

        Vector3 localCenter = GetWorldBoundsLocalCenter();
        float localBottomY = transform.InverseTransformPoint(GetWorldBounds().min).y;
        fallbackBodyTransform.localPosition = new Vector3(localCenter.x, localBottomY + 0.52f, localCenter.z);
        fallbackBodyTransform.localScale = ConvertWorldScaleToLocal(new Vector3(
            Mathf.Max(0.1f, fallbackBodySize.x),
            Mathf.Max(0.1f, fallbackBodySize.y),
            Mathf.Max(0.02f, fallbackBodySize.z)));
    }

    private void UpdateFallbackVisibleBody()
    {
        if (!forceVisibleFallbackBody || fallbackBodyRenderer == null || fallbackBodyTransform == null)
        {
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.time * Mathf.Max(0.01f, fallbackCorePulseSpeed)) *
                      Mathf.Clamp01(fallbackCorePulseAmount);
        Color pulsedColor = fallbackBodyColor * pulse;
        pulsedColor.a = fallbackBodyColor.a;
        fallbackBodyRenderer.material.color = pulsedColor;
        fallbackBodyRenderer.enabled = !hideExclamationWhileDialogOpen || !isDialogOpen;
    }

    private Vector3 ConvertWorldScaleToLocal(Vector3 worldScale)
    {
        Vector3 parentScale = transform.lossyScale;
        return new Vector3(
            SafeDivide(worldScale.x, Mathf.Abs(parentScale.x)),
            SafeDivide(worldScale.y, Mathf.Abs(parentScale.y)),
            SafeDivide(worldScale.z, Mathf.Abs(parentScale.z)));
    }

    private static Sprite CreateRuntimeWhiteSprite()
    {
        Texture2D whiteTexture = Texture2D.whiteTexture;
        return Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            100f);
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

    private Vector3 GetWorldBoundsLocalCenter()
    {
        Bounds bounds = GetWorldBounds();
        return transform.InverseTransformPoint(bounds.center);
    }

    private float GetWorldBoundsLocalTopY()
    {
        Bounds bounds = GetWorldBounds();
        Vector3 topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        return transform.InverseTransformPoint(topWorld).y;
    }

    private Bounds GetWorldBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            return new Bounds(transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return bounds;
    }

    private static float SafeDivide(float numerator, float denominator)
    {
        if (denominator < 0.0001f)
        {
            return numerator;
        }

        return numerator / denominator;
    }

    private void EnsureInteractionHitbox()
    {
        if (!enforceInteractionHitbox)
        {
            return;
        }

        if (interactionHitbox == null)
        {
            interactionHitbox = GetComponent<SphereCollider>();
            if (interactionHitbox == null)
            {
                interactionHitbox = gameObject.AddComponent<SphereCollider>();
            }
        }

        interactionHitbox.isTrigger = true;
        interactionHitbox.radius = Mathf.Max(0.2f, interactionHitboxRadius);
        interactionHitbox.center = GetWorldBoundsLocalCenter() +
                                   new Vector3(0f, Mathf.Max(0f, interactionHitboxHeightOffset), 0f);
    }
}
