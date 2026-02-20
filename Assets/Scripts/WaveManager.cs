using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class WaveManager : MonoBehaviour
{
    [Header("config")]
    public WaveConfig config;

    [Header("spawn points (optional)")]
    [Tooltip("Optional. If empty, zombies will spawn just outside the camera view instead.")]
    public Transform[] spawnPoints;

    [Header("runtime (read-only)")]
    [SerializeField] private int currentWave = 0;
    [SerializeField] private int aliveZombies = 0;
    [SerializeField] private int zombiesRemainingInWave = 0;
    [SerializeField] private bool isSpawning = false;
    [SerializeField] private bool currentWaveIsBoss = false;

    [Header("Wave Indicator UI")]
    public TextMeshProUGUI waveStatusText;
    public bool autoCreateWaveStatusUi = true;
    public bool forceWaveStatusUi = true;
    public Vector2 waveStatusOffset = new Vector2(-24f, -24f);
    public Color waveStatusColor = new Color(1f, 0.96f, 0.72f, 1f);
    public Color waveStatusPanelColor = new Color(0f, 0f, 0f, 0.62f);
    public Color waveStatusOutlineColor = new Color(0f, 0f, 0f, 0.85f);
    public Vector2 waveStatusPanelSize = new Vector2(560f, 128f);
    [Range(16, 72)] public int waveStatusFontSize = 32;

    private PlayerController player;
    private readonly HashSet<ZombieAI> activeZombies = new HashSet<ZombieAI>();
    private string cachedWaveStatusText = string.Empty;
    private static TMP_FontAsset runtimeWaveStatusFont;
    private Image waveStatusPanelImage;
    private int resolvedWaveLimit;

    public int CurrentWave => currentWave;
    public int AliveZombies => aliveZombies;
    public int ZombiesRemainingInWave => zombiesRemainingInWave;
    public bool IsSpawningWave => isSpawning;
    public event System.Action AllWavesCompleted;

    private void Start()
    {
        RegionWaveManager regionWaveManager = FindObjectOfType<RegionWaveManager>();
        if (regionWaveManager != null &&
            regionWaveManager.isActiveAndEnabled &&
            regionWaveManager.regions != null &&
            regionWaveManager.regions.Count > 0)
        {
            enabled = false;
            return;
        }

        player = PlayerController.FindPrimary();

        if (!EnsureConfig())
        {
            enabled = false;
            return;
        }

        ApplyTutorialOverridesIfNeeded();

        if (forceWaveStatusUi)
        {
            autoCreateWaveStatusUi = true;
        }

        EnsureWaveStatusUi();
        resolvedWaveLimit = ResolveWaveLimit();
        RefreshWaveStatusUi();

        StartCoroutine(RunWaves());
    }

    private IEnumerator RunWaves()
    {
        yield return new WaitForSeconds(config.timeBeforeFirstWave);

        while (true)
        {
            currentWave++;
            currentWaveIsBoss = false;

            // if config is finite and we finished, move to next scene or notify completion
            if (resolvedWaveLimit > 0 && currentWave > resolvedWaveLimit)
            {
                HandleWaveSequenceCompleted();
                yield break;
            }

            currentWaveIsBoss = IsBossWave(currentWave);
            int zombiesThisWave = GetZombieCountForWave(currentWave, currentWaveIsBoss);
            zombiesRemainingInWave = Mathf.Max(0, zombiesThisWave);
            int waveStartHealth = GetPlayerHealthSnapshot();

            isSpawning = true;
            RefreshWaveStatusUi();

            for (int i = 0; i < zombiesThisWave; i++)
            {
                // don't flood the scene: cap how many can exist at once
                while (config.maxAliveAtOnce > 0 && GetAliveZombieCount() >= config.maxAliveAtOnce)
                    yield return null;

                SpawnOneZombie(currentWave, currentWaveIsBoss);
                yield return new WaitForSeconds(config.timeBetweenSpawns);
            }

            isSpawning = false;
            RefreshWaveStatusUi();

            // wait until everything from this wave is dead
            while (GetAliveZombieCount() > 0)
                yield return null;

            HealPlayerForWaveLoss(waveStartHealth);

            if (player != null)
            {
                CleanVfxFactory.SpawnPickupBurst(player.transform.position + Vector3.up * 0.22f);
            }

            yield return new WaitForSeconds(config.timeBetweenWaves);
        }
    }

    private bool IsBossWave(int waveNumber)
    {
        if (config == null || !config.useBossWave || waveNumber <= 0)
        {
            return false;
        }

        int configuredWave = config.bossWaveNumber;
        if (configuredWave <= 0)
        {
            configuredWave = resolvedWaveLimit > 0
                ? resolvedWaveLimit
                : 0;
        }

        if (configuredWave <= 0)
        {
            return false;
        }

        return waveNumber == configuredWave;
    }

    private int GetPlayerHealthSnapshot()
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (player == null)
        {
            return 0;
        }

        return Mathf.Clamp(player.currentHealth, 0, player.maxHealth);
    }

    private void HealPlayerForWaveLoss(int waveStartHealth)
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (player == null || player.isDead)
        {
            return;
        }

        int waveEndHealth = Mathf.Clamp(player.currentHealth, 0, player.maxHealth);
        int healthLostThisWave = Mathf.Max(0, waveStartHealth - waveEndHealth);
        if (healthLostThisWave <= 0)
        {
            return;
        }

        int healAmount = Mathf.CeilToInt(healthLostThisWave * 0.5f);
        player.Heal(healAmount);
    }

    private int GetZombieCountForWave(int waveNumber, bool isBossWave)
    {
        if (config == null)
        {
            return 0;
        }

        if (isBossWave)
        {
            return Mathf.Max(1, config.bossZombieCount);
        }

        int zombiesThisWave = config.zombiesPerWave + (waveNumber - 1) * config.zombiesPerWaveIncrease;
        if (config.maxZombiesPerWave > 0)
        {
            zombiesThisWave = Mathf.Min(zombiesThisWave, config.maxZombiesPerWave);
        }

        return Mathf.Max(0, zombiesThisWave);
    }

    private int ResolveWaveLimit()
    {
        if (config == null)
        {
            return 0;
        }

        if (config.waveCount > 0)
        {
            return config.waveCount;
        }

        if (config.allowInfiniteWaves)
        {
            return 0;
        }

        return Mathf.Max(1, config.defaultFiniteWaveCount);
    }

    private void HandleWaveSequenceCompleted()
    {
        isSpawning = false;
        zombiesRemainingInWave = 0;
        ShowCompletionStatus();

        string nextSceneName = config != null ? config.nextSceneName : string.Empty;
        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneTransitionTo(nextSceneName);
            return;
        }

        AllWavesCompleted?.Invoke();
    }

    private void ShowCompletionStatus()
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (player != null)
        {
            Vector3 completionVfxPosition = player.transform.position + Vector3.up * 0.22f;
            CleanVfxFactory.SpawnAbilityBurstGlow(completionVfxPosition);
            CleanVfxFactory.SpawnPickupBurst(completionVfxPosition);
        }

        if (waveStatusText == null)
        {
            return;
        }

        cachedWaveStatusText = "MISSION COMPLETE\nALL WAVES CLEARED";
        waveStatusText.text = cachedWaveStatusText;
        waveStatusText.color = waveStatusColor;
        waveStatusText.outlineColor = waveStatusOutlineColor;
    }

    private void SceneTransitionTo(string sceneName)
    {
        string normalizedSceneName = sceneName != null ? sceneName.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedSceneName))
        {
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(normalizedSceneName);
    }

    private bool EnsureConfig()
    {
        if (config == null)
        {
            config = FindFallbackWaveConfig();
            if (config == null)
            {
                Debug.LogError("WaveManager: no WaveConfig assigned and no fallback WaveConfig was found.");
                return false;
            }

            Debug.LogWarning("WaveManager: missing config reference; using fallback WaveConfig '" + config.name + "'.");
        }

        if (config.zombiePrefab != null)
        {
            return true;
        }

        GameObject fallbackZombiePrefab = FindFallbackZombiePrefab();
        if (fallbackZombiePrefab == null)
        {
            Debug.LogError(
                "WaveManager: the active WaveConfig has no zombie prefab assigned and no fallback zombie prefab was found.");
            return false;
        }

        WaveConfig runtimeConfig = Instantiate(config);
        runtimeConfig.zombiePrefab = fallbackZombiePrefab;
        config = runtimeConfig;

        Debug.LogWarning(
            "WaveManager: using fallback zombie prefab '" + fallbackZombiePrefab.name + "' for WaveConfig '" + config.name + "'.");
        return true;
    }

    private void ApplyTutorialOverridesIfNeeded()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (!IsTutorialSceneName(sceneName) || config == null)
        {
            return;
        }

        WaveConfig runtimeConfig = Instantiate(config);
        runtimeConfig.waveCount = 0;
        runtimeConfig.allowInfiniteWaves = true;
        runtimeConfig.nextSceneName = string.Empty;
        config = runtimeConfig;
    }

    private static bool IsTutorialSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return string.Equals(sceneName, "Tutorial", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(sceneName, "TutorialGame", StringComparison.OrdinalIgnoreCase);
    }

    private static WaveConfig FindFallbackWaveConfig()
    {
        WaveConfig[] configs = Resources.FindObjectsOfTypeAll<WaveConfig>();
        WaveConfig firstValid = null;

        for (int i = 0; i < configs.Length; i++)
        {
            WaveConfig candidate = configs[i];
            if (candidate == null || candidate.zombiePrefab == null)
            {
                continue;
            }

            if (firstValid == null)
            {
                firstValid = candidate;
            }

            if (string.Equals(candidate.name, "DefaultWaveConfig", System.StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return firstValid;
    }

    private static GameObject FindFallbackZombiePrefab()
    {
        ZombieAI[] zombies = Resources.FindObjectsOfTypeAll<ZombieAI>();
        GameObject firstPrefab = null;

        for (int i = 0; i < zombies.Length; i++)
        {
            ZombieAI zombie = zombies[i];
            if (zombie == null || zombie.gameObject == null)
            {
                continue;
            }

            if (zombie.gameObject.scene.IsValid())
            {
                continue;
            }

            if (firstPrefab == null)
            {
                firstPrefab = zombie.gameObject;
            }

            if (string.Equals(zombie.gameObject.name, "Zombie_Basic", System.StringComparison.OrdinalIgnoreCase))
            {
                return zombie.gameObject;
            }
        }

        return firstPrefab;
    }

    private void SpawnOneZombie(int waveIndex, bool forceBoss)
    {
        if (config.zombiePrefab == null) return;

        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        Vector3 pos = GetSpawnPosition();
        GameObject zObj = Instantiate(config.zombiePrefab, pos, Quaternion.identity);
        CleanVfxFactory.SpawnEnemySpawnPoof(pos);

        var z = zObj.GetComponent<ZombieAI>();
        if (z == null)
        {
            Debug.LogWarning("WaveManager: spawned prefab has no ZombieAI.");
            return;
        }

        z.Died -= OnZombieDied;
        z.Died += OnZombieDied;
        activeZombies.Add(z);
        aliveZombies = activeZombies.Count;
        RefreshWaveStatusUi();

        if (player != null)
        {
            z.player = player.transform;
            z.playerController = player;
        }

        // scaling: speed ramp only – health scaling removed so regular
        // zombies always die in exactly 3 shots (base HP 9, damage per shot 3).
        float spdMult = Mathf.Pow(config.speedMultiplierPerWave, Mathf.Max(0, waveIndex - 1));

        // z.maxHealth stays at the prefab value (9) – no per-wave HP inflation.
        z.currentHealth = z.maxHealth;
        z.moveSpeed = z.moveSpeed * spdMult;

        if (forceBoss)
        {
            ApplyBossModifiers(z);
        }
        else
        {
            // runners become more common later
            float runnerChance = config.runnerChanceStart + (waveIndex - 1) * config.runnerChanceIncreasePerWave;
            runnerChance = Mathf.Clamp01(runnerChance);

            z.isRunner = Random.value < runnerChance;
            if (z.isRunner)
            {
                z.moveSpeed *= config.runnerSpeedBonus;
                z.detectionRadius *= config.runnerDetectionBonus;
            }

            // mutations
            ApplyMutations(z, waveIndex);
        }
    }

    private Vector3 GetSpawnPosition()
    {
        // if designer provided spawn points, use them
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (sp != null) return sp.position;
        }

        // otherwise: spawn just outside camera view
        Camera cam = Camera.main;
        if (cam == null)
            return transform.position + (Vector3)(Random.insideUnitCircle.normalized * 10f);

        float margin = Mathf.Max(0.1f, config != null ? config.offscreenSpawnMargin : 2f);

        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        float left = bl.x - margin;
        float right = tr.x + margin;
        float bottom = bl.y - margin;
        float top = tr.y + margin;

        int side = Random.Range(0, 4);
        Vector3 p;
        if (side == 0) p = new Vector3(left, Random.Range(bottom, top), 0f);
        else if (side == 1) p = new Vector3(right, Random.Range(bottom, top), 0f);
        else if (side == 2) p = new Vector3(Random.Range(left, right), bottom, 0f);
        else p = new Vector3(Random.Range(left, right), top, 0f);

        // avoid spawning right on top of player
        if (player != null && config != null && config.minSpawnDistanceFromPlayer > 0f)
        {
            int guard = 0;
            while (Vector2.Distance(p, player.transform.position) < config.minSpawnDistanceFromPlayer && guard < 20)
            {
                side = Random.Range(0, 4);
                if (side == 0) p = new Vector3(left, Random.Range(bottom, top), 0f);
                else if (side == 1) p = new Vector3(right, Random.Range(bottom, top), 0f);
                else if (side == 2) p = new Vector3(Random.Range(left, right), bottom, 0f);
                else p = new Vector3(Random.Range(left, right), top, 0f);
                guard++;
            }
        }

        return p;
    }

    private void ApplyMutations(ZombieAI z, int waveIndex)
    {
        if (z == null || config == null) return;

        // radiated
        if (waveIndex >= config.radiatedWaveStart)
        {
            float c = config.radiatedChanceStart + (waveIndex - config.radiatedWaveStart) * config.radiatedChanceIncreasePerWave;
            c = Mathf.Clamp01(c);
            if (Random.value < c)
            {
                z.isRadiated = true;
                z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * config.radiatedHealthBonus));
                z.currentHealth = z.maxHealth;
                z.moveSpeed = z.moveSpeed * config.radiatedSpeedBonus;

                if (z.spriteRenderer != null)
                    z.spriteRenderer.color = new Color(0.7f, 1f, 0.7f, 1f);
            }
        }

        // tank
        if (waveIndex >= config.tankWaveStart)
        {
            float c = config.tankChanceStart + (waveIndex - config.tankWaveStart) * config.tankChanceIncreasePerWave;
            c = Mathf.Clamp01(c);
            if (Random.value < c)
            {
                z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * config.tankHealthBonus));
                z.currentHealth = z.maxHealth;
                z.moveSpeed = z.moveSpeed * config.tankSpeedMultiplier;
                z.transform.localScale = z.transform.localScale * config.tankScaleMultiplier;

                if (z.spriteRenderer != null)
                    z.spriteRenderer.color = new Color(0.85f, 0.85f, 1f, 1f);
            }
        }
    }

    private void ApplyBossModifiers(ZombieAI z)
    {
        if (z == null || config == null)
        {
            return;
        }

        z.maxHealth = Mathf.Max(1, Mathf.RoundToInt(z.maxHealth * Mathf.Max(1f, config.bossHealthMultiplier)));
        z.currentHealth = z.maxHealth;
        z.moveSpeed *= Mathf.Max(0.4f, config.bossSpeedMultiplier);
        z.zombieDamage = Mathf.Max(1, Mathf.RoundToInt(z.zombieDamage * Mathf.Max(1f, config.bossDamageMultiplier)));
        z.transform.localScale *= Mathf.Max(1f, config.bossScaleMultiplier);
        z.detectionRadius *= 1.35f;
        z.attackRange *= 1.2f;

        if (z.spriteRenderer != null)
        {
            z.spriteRenderer.color = config.bossTint;
        }
    }

    private void OnZombieDied(ZombieAI z)
    {
        if (z != null)
        {
            z.Died -= OnZombieDied;
            activeZombies.Remove(z);
        }

        aliveZombies = activeZombies.Count;
        zombiesRemainingInWave = Mathf.Max(0, zombiesRemainingInWave - 1);
        RefreshWaveStatusUi();
    }

    private int GetAliveZombieCount()
    {
        if (activeZombies.Count > 0)
        {
            activeZombies.RemoveWhere(z => z == null || z.isDead);
        }

        aliveZombies = activeZombies.Count;
        return aliveZombies;
    }

    private void EnsureWaveStatusUi()
    {
        if (waveStatusText != null || !autoCreateWaveStatusUi)
        {
            return;
        }

        GameObject existingText = GameObject.Find("WaveStatusText");
        if (existingText != null)
        {
            waveStatusText = existingText.GetComponent<TextMeshProUGUI>();
            waveStatusPanelImage = existingText.GetComponentInParent<Image>();
            if (waveStatusText != null)
            {
                return;
            }
        }

        GameObject canvasObject = GameObject.Find("WaveStatusCanvas");
        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                "WaveStatusCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 470;
        }

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject panelObject = GameObject.Find("WaveStatusPanel");
        if (panelObject == null)
        {
            panelObject = new GameObject("WaveStatusPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
        }

        waveStatusPanelImage = panelObject.GetComponent<Image>();
        if (waveStatusPanelImage == null)
        {
            waveStatusPanelImage = panelObject.AddComponent<Image>();
        }

        waveStatusPanelImage.color = waveStatusPanelColor;

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = waveStatusOffset;
        panelRect.sizeDelta = waveStatusPanelSize;

        GameObject textObject = panelObject.transform.Find("WaveStatusText")?.gameObject;
        if (textObject == null)
        {
            textObject = new GameObject("WaveStatusText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
        }

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 0f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(16f, 12f);
        rectTransform.offsetMax = new Vector2(-16f, -12f);

        waveStatusText = textObject.GetComponent<TextMeshProUGUI>();
        waveStatusText.fontSize = waveStatusFontSize;
        waveStatusText.alignment = TextAlignmentOptions.TopRight;
        waveStatusText.color = waveStatusColor;
        waveStatusText.outlineColor = waveStatusOutlineColor;
        waveStatusText.outlineWidth = 0.18f;
        waveStatusText.enableWordWrapping = true;
        waveStatusText.text = string.Empty;

        TMP_FontAsset defaultFont = GetRuntimeWaveStatusFont();
        ImportedStuffAssetUtility.ApplyUsableFont(waveStatusText, defaultFont);
    }

    private static TMP_FontAsset GetRuntimeWaveStatusFont()
    {
        if (IsUsableRuntimeFont(runtimeWaveStatusFont))
        {
            return runtimeWaveStatusFont;
        }

        runtimeWaveStatusFont = ImportedStuffAssetUtility.GetGameplayFont();
        if (!IsUsableRuntimeFont(runtimeWaveStatusFont))
        {
            runtimeWaveStatusFont = TMP_Settings.defaultFontAsset;
        }

        if (!IsUsableRuntimeFont(runtimeWaveStatusFont))
        {
            runtimeWaveStatusFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        return runtimeWaveStatusFont;
    }

    private static bool IsUsableRuntimeFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null || fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            if (fontAsset.atlasTextures[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshWaveStatusUi()
    {
        if (waveStatusText == null)
        {
            if (!autoCreateWaveStatusUi)
            {
                return;
            }

            EnsureWaveStatusUi();
        }

        if (waveStatusText == null)
        {
            return;
        }

        TMP_FontAsset preferredFont = GetRuntimeWaveStatusFont();
        ImportedStuffAssetUtility.ApplyUsableFont(waveStatusText, preferredFont);

        waveStatusText.fontSize = waveStatusFontSize;
        waveStatusText.alignment = TextAlignmentOptions.TopRight;
        waveStatusText.enableWordWrapping = true;

        string nextValue = BuildWaveStatusText();
        if (nextValue == cachedWaveStatusText)
        {
            return;
        }

        cachedWaveStatusText = nextValue;
        waveStatusText.text = nextValue;
        waveStatusText.color = waveStatusColor;
        waveStatusText.outlineColor = waveStatusOutlineColor;

        if (waveStatusPanelImage != null)
        {
            waveStatusPanelImage.color = waveStatusPanelColor;
        }
    }

    private string BuildWaveStatusText()
    {
        string waveLabel = resolvedWaveLimit > 0
            ? "1/" + resolvedWaveLimit
            : "1";

        if (currentWave <= 0)
        {
            return "WAVE " + waveLabel + "\nZOMBIES LEFT: 0";
        }

        waveLabel = resolvedWaveLimit > 0
            ? currentWave + "/" + resolvedWaveLimit
            : currentWave.ToString();

        string statusText;
        if (currentWaveIsBoss)
        {
            statusText = isSpawning ? "BOSS ARRIVING" : "BOSS FIGHT";
        }
        else
        {
            statusText = isSpawning ? "SPAWNING" : "CLEARING";
        }

        return "WAVE " + waveLabel +
               "\nZOMBIES LEFT: " + Mathf.Max(0, zombiesRemainingInWave) +
               "\nSTATUS: " + statusText;
    }
}
