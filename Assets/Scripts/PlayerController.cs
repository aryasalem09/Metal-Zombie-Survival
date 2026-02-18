using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    private static readonly List<PlayerController> Instances = new List<PlayerController>();
    private static PlayerController primaryInstance;

    [Header("References")]
    public AnimationController animationController;

    [Header("Movement")]
    [FormerlySerializedAs("speed")]
    public float moveSpeed = 1.0f;
    [Range(0.3f, 1f)] public float crouchSpeedMultiplier = 0.6f;
    [Range(1f, 2f)] public float runSpeedMultiplier = 1.25f;

    [Header("Mouse Look")]
    public bool snapLookToEightDirections;
    [Tooltip("When enabled, the character continuously tracks the exact mouse direction.")]
    public bool forceContinuousMouseLook = true;
    [Tooltip("Keeps mouse look continuous even if old scene data still has snapping enabled.")]
    public bool enforceContinuousMouseLook = true;

    [Header("Class Flags")]
    public bool isActive = true;
    public bool isRanged = true;
    public bool isStealth;
    public bool isShapeShifter;
    public bool isSummoner;
    public bool isMelee;

    [Header("Projectile Attack")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 10.0f;
    [FormerlySerializedAs("shootDelay")]
    public float projectileCooldown = 0.1f;
    [Min(0.01f)] public float minProjectileCooldown = 0.02f;
    [Min(0.01f)] public float maxRecommendedProjectileCooldown = 0.1f;
    public bool clampLegacyProjectileCooldown = true;
    public float projectileLifetime = 1.5f;
    [Range(0.02f, 2f)] public float projectileScaleMultiplier = 0.25f;
    [Range(0.05f, 1f)] public float projectileShotScaleMultiplier = 0.35f;
    [Range(0.05f, 1f)] public float globalProjectileSizeMultiplier = 0.12f;
    public float projectileSpawnOffset = 0.35f;
    public int projectileDamage = 1;
    public bool allowRuntimeProjectileFallback = true;
    public Sprite runtimeFallbackProjectileSprite;
    public Color runtimeFallbackProjectileColor = new Color(0.35f, 0.95f, 1f, 0.95f);
    [Range(0.03f, 0.3f)] public float runtimeFallbackProjectileRadius = 0.09f;
    [Min(0.005f)] public float minimumVisibleProjectileScale = 0.02f;
    public int minimumProjectileSortingOrder = 450;
    [Range(0f, 1f)] public float minimumProjectileAlpha = 0.95f;

    [Header("Special Energy Effects")]
    [FormerlySerializedAs("AoEPrefab")] public GameObject areaPulsePrefab;
    [FormerlySerializedAs("Special1Prefab")] public GameObject specialPulsePrefab;
    [FormerlySerializedAs("HookPrefab")] public GameObject summonPulsePrefab;
    [FormerlySerializedAs("ShapeShiftPrefab")] public GameObject shapeShiftPulsePrefab;
    public float castDelaySeconds = 0.25f;
    public GameObject meleePrefab;

    [Header("Radial Burst")]
    public KeyCode radialBurstKey = KeyCode.Q;
    public int burstProjectileCount = 10;
    public int killsPerBurstUnlock = 10;
    [SerializeField] private int burstCharges;
    [SerializeField] private int nextBurstUnlockAt = 10;
    public GameObject burstEffectPrefab;

    [Header("Health & UI")]
    public int maxHealth = 100;
    public int currentHealth;
    public bool isDead;
    public Slider healthSlider;
    public GameObject gameOver;
    public bool autoRestartOnDeath;
    public float restartDelaySeconds = 3f;

    [Header("Scoring UI")]
    public int zombieKillCount;
    public TextMeshProUGUI killCountText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI burstCounterText;
    [SerializeField] private int collectibleCount;
    [SerializeField] private int score;

    [Header("Runtime HUD Fallback")]
    public bool forceRuntimeHudPanel = true;
    public bool showRuntimeHudPanel = true;
    public Vector2 runtimeHudPanelSize = new Vector2(300f, 120f);
    public Vector2 runtimeHudPanelOffset = new Vector2(18f, -18f);
    public Color runtimeHudPanelColor = new Color(0f, 0f, 0f, 0.58f);
    public Color runtimeHudTextColor = Color.white;
    public Color runtimeHudHealthBarBackgroundColor = new Color(1f, 1f, 1f, 0.2f);
    public Color runtimeHudHealthBarFillColor = new Color(0.21f, 0.92f, 0.4f, 0.95f);

    [Header("Layer Names")]
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private string projectileLayerName = "Projectile";

    private Rigidbody2D rb;
    private CircleCollider2D circleCollider;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;

    private bool isOnStairs;
    private bool isRunning;
    private float nextProjectileTime;
    private Vector3 originalKillTextScale;
    private Coroutine killPulseCoroutine;
    private Vector2 movementDirection;
    private Vector2 lookDirection = Vector2.right;
    private float lookAngle;
    private bool hasInputAuthority = true;
    private static Sprite generatedFallbackProjectileSprite;
    private static TMP_FontAsset runtimeHudFontAsset;
    private TextMeshProUGUI runtimeHudHealthText;
    private TextMeshProUGUI runtimeHudKillText;
    private Image runtimeHudHealthBarFillImage;

    public bool isCrouching;

    public event Action<int, int> HealthChanged;
    public event Action<int> KillCountChanged;
    public event Action<int> BurstChargeChanged;
    public event Action<int> CollectibleChanged;
    public event Action<int> ScoreChanged;
    public event Action ProjectileAttackPerformed;
    public event Action RadialBurstUsed;
    public event Action PlayerMoved;
    public event Action PlayerDied;

    public Vector2 MovementDirection => movementDirection;
    public Vector2 LookDirection => lookDirection;
    public bool IsRunning => isRunning;
    public int BurstCharges => burstCharges;
    public int KillsUntilNextBurst => Mathf.Max(0, nextBurstUnlockAt - zombieKillCount);
    public bool HasInputAuthority => hasInputAuthority;
    public static PlayerController Primary => primaryInstance != null ? primaryInstance : FindPrimary();

    private void OnEnable()
    {
        RegisterInstance(this);
    }

    private void OnDisable()
    {
        UnregisterInstance(this);
    }

    private void OnDestroy()
    {
        UnregisterInstance(this);
    }

    private void Start()
    {
        CacheComponents();
        if (animationController == null) animationController = GetComponent<AnimationController>();

        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        if (hasInputAuthority)
        {
            TryAssignLayer(gameObject, playerLayerName);
        }

        NormalizeLegacyCombatValues();

        if (enforceContinuousMouseLook)
        {
            forceContinuousMouseLook = true;
            snapLookToEightDirections = false;
        }

        currentHealth = Mathf.Max(1, maxHealth);
        UpdateHealthUi();

        if (killCountText != null)
        {
            originalKillTextScale = killCountText.transform.localScale;
            killCountText.text = FormatKillCount(zombieKillCount);
        }

        nextBurstUnlockAt = Mathf.Max(1, nextBurstUnlockAt);
        if (nextBurstUnlockAt < killsPerBurstUnlock)
        {
            nextBurstUnlockAt = killsPerBurstUnlock;
        }

        UpdateScoreUi();
        UpdateBurstUi();

        if (forceRuntimeHudPanel)
        {
            showRuntimeHudPanel = true;
        }

        EnsureRuntimeHudPanel();
        RefreshRuntimeHudPanel();
    }

    private void Update()
    {
        if (isDead || !hasInputAuthority)
        {
            return;
        }

        if (Camera.main == null)
        {
            return;
        }

        UpdateLookDirectionFromMouse();
        HandleMovementInput();
        HandleAttackInput();
        HandleSpecialInput();
        HandleCrouchingToggle();
        HandleRadialBurstInput();
        UpdateAnimation();
        animationController?.UpdateFacingDirection(lookDirection);
    }

    private void FixedUpdate()
    {
        if (isDead || !hasInputAuthority || rb == null)
        {
            return;
        }

        float speed = moveSpeed;
        if (isCrouching)
        {
            speed *= crouchSpeedMultiplier;
        }
        else if (isRunning)
        {
            speed *= runSpeedMultiplier;
        }

        rb.MovePosition(rb.position + movementDirection * speed * Time.fixedDeltaTime);
    }

    private void UpdateLookDirectionFromMouse()
    {
        Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 toMouse = mousePosition - (Vector2)transform.position;
        if (toMouse.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float targetAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        bool shouldSnapLook = !forceContinuousMouseLook &&
                              (snapLookToEightDirections || isOnStairs);

        lookAngle = shouldSnapLook
            ? SnapAngleToEightDirections(targetAngle)
            : targetAngle;

        lookDirection = new Vector2(
            Mathf.Cos(lookAngle * Mathf.Deg2Rad),
            Mathf.Sin(lookAngle * Mathf.Deg2Rad));
    }

    private void HandleMovementInput()
    {
        movementDirection = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
        {
            movementDirection += lookDirection;
        }

        if (Input.GetKey(KeyCode.S))
        {
            movementDirection -= lookDirection;
        }

        if (Input.GetKey(KeyCode.A))
        {
            movementDirection += new Vector2(-lookDirection.y, lookDirection.x);
        }

        if (Input.GetKey(KeyCode.D))
        {
            movementDirection += new Vector2(lookDirection.y, -lookDirection.x);
        }

        if (movementDirection.sqrMagnitude > 1f)
        {
            movementDirection.Normalize();
        }

        isRunning = !isCrouching && movementDirection.sqrMagnitude > 0.0001f && Input.GetKey(KeyCode.LeftShift);

        if (movementDirection.sqrMagnitude > 0.0001f)
        {
            PlayerMoved?.Invoke();
        }
    }

    private void HandleAttackInput()
    {
        if (!HasProjectileSource())
        {
            return;
        }

        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
        {
            return;
        }

        if (Time.time < nextProjectileTime)
        {
            return;
        }

        nextProjectileTime = Time.time + GetEffectiveProjectileCooldown();
        FireProjectile(lookDirection, projectileDamage);
        ProjectileAttackPerformed?.Invoke();
        animationController?.TriggerAttackAnimation();
        AudioManager.Instance?.PlayAttackSfx();
    }

    private void HandleSpecialInput()
    {
        if (!isActive)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartCoroutine(SpawnPulseAfterDelay(specialPulsePrefab, castDelaySeconds));
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            StartCoroutine(SpawnPulseAfterDelay(areaPulsePrefab, castDelaySeconds));
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            StartCoroutine(SpawnPulseAfterDelay(summonPulsePrefab, castDelaySeconds));
        }

        if (Input.GetKeyDown(KeyCode.C) && isShapeShifter)
        {
            StartCoroutine(SpawnPulseAfterDelay(shapeShiftPulsePrefab, 0.05f));
        }
    }

    private void HandleCrouchingToggle()
    {
        if (!Input.GetKeyDown(KeyCode.C))
        {
            return;
        }

        isCrouching = !isCrouching;
        if (isStealth && spriteRenderer != null)
        {
            spriteRenderer.color = isCrouching
                ? new Color(0.55f, 0.55f, 0.55f, 0.55f)
                : originalColor;
        }
    }

    private void HandleRadialBurstInput()
    {
        if (!Input.GetKeyDown(radialBurstKey))
        {
            return;
        }

        TryUseRadialBurst();
    }

    private void UpdateAnimation()
    {
        if (animationController == null)
        {
            return;
        }

        animationController.UpdateMovementAnimation(
            movementDirection,
            lookDirection,
            isRunning,
            isCrouching);
    }

    private void FireProjectile(Vector2 direction, int damageValue)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = lookDirection.sqrMagnitude > 0.0001f ? lookDirection : Vector2.right;
        }

        Vector3 spawnPosition = transform.position + (Vector3)(direction.normalized * projectileSpawnOffset);
        float rotation = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GameObject projectile;
        if (projectilePrefab != null)
        {
            projectile = Instantiate(projectilePrefab, spawnPosition, Quaternion.Euler(0f, 0f, rotation));
        }
        else
        {
            if (!allowRuntimeProjectileFallback)
            {
                return;
            }

            projectile = CreateRuntimeProjectile(spawnPosition, rotation);
        }

        ApplyProjectileScale(projectile);
        EnsureProjectileVisibility(projectile);
        TryAssignLayerRecursively(projectile, projectileLayerName);
        IgnoreProjectileCollisionWithPlayer(projectile);

        EnergyPulseProjectile pulse = projectile.GetComponent<EnergyPulseProjectile>();
        if (pulse != null)
        {
            pulse.Configure(
                owner: gameObject,
                speedValue: projectileSpeed,
                damageValue: damageValue,
                lifetimeValue: projectileLifetime);
            pulse.Launch(direction);
            return;
        }

        Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
        if (projectileRb != null)
        {
            projectileRb.velocity = direction.normalized * projectileSpeed;
        }

        Destroy(projectile, projectileLifetime);
    }

    private bool HasProjectileSource()
    {
        return projectilePrefab != null || allowRuntimeProjectileFallback;
    }

    private float GetEffectiveProjectileCooldown()
    {
        float minimum = Mathf.Max(0.01f, minProjectileCooldown);
        return Mathf.Max(minimum, projectileCooldown);
    }

    private void NormalizeLegacyCombatValues()
    {
        minProjectileCooldown = Mathf.Max(0.01f, minProjectileCooldown);
        maxRecommendedProjectileCooldown = Mathf.Max(minProjectileCooldown, maxRecommendedProjectileCooldown);

        projectileCooldown = Mathf.Max(minProjectileCooldown, projectileCooldown);
        if (clampLegacyProjectileCooldown && projectileCooldown > maxRecommendedProjectileCooldown)
        {
            projectileCooldown = maxRecommendedProjectileCooldown;
        }

        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
        projectileLifetime = Mathf.Max(0.05f, projectileLifetime);
        projectileScaleMultiplier = Mathf.Max(0.02f, projectileScaleMultiplier);
        projectileShotScaleMultiplier = Mathf.Clamp(projectileShotScaleMultiplier, 0.05f, 1f);
        globalProjectileSizeMultiplier = Mathf.Clamp(globalProjectileSizeMultiplier, 0.05f, 1f);
        minimumVisibleProjectileScale = Mathf.Clamp(minimumVisibleProjectileScale, 0.005f, 0.03f);
        minimumProjectileSortingOrder = Mathf.Max(0, minimumProjectileSortingOrder);
        minimumProjectileAlpha = Mathf.Clamp01(minimumProjectileAlpha);
    }

    private GameObject CreateRuntimeProjectile(Vector3 spawnPosition, float rotation)
    {
        GameObject projectile = new GameObject("RuntimeEnergyPulse");
        projectile.transform.position = spawnPosition;
        projectile.transform.rotation = Quaternion.Euler(0f, 0f, rotation);
        projectile.transform.localScale = Vector3.one;

        SpriteRenderer renderer = projectile.AddComponent<SpriteRenderer>();
        renderer.sprite = runtimeFallbackProjectileSprite != null
            ? runtimeFallbackProjectileSprite
            : GetGeneratedFallbackProjectileSprite();
        renderer.color = runtimeFallbackProjectileColor;
        renderer.sortingOrder = 50;

        Rigidbody2D body = projectile.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        CircleCollider2D circle = projectile.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.radius = runtimeFallbackProjectileRadius;

        EnergyPulseProjectile energyProjectile = projectile.AddComponent<EnergyPulseProjectile>();
        energyProjectile.visualScale = 1f;

        return projectile;
    }

    private void ApplyProjectileScale(GameObject projectile)
    {
        if (projectile == null)
        {
            return;
        }

        float configuredScale = Mathf.Max(
            0.01f,
            projectileScaleMultiplier * projectileShotScaleMultiplier * globalProjectileSizeMultiplier);
        projectile.transform.localScale *= configuredScale;

        Vector3 currentScale = projectile.transform.localScale;
        float currentMagnitude = Mathf.Max(Mathf.Abs(currentScale.x), Mathf.Abs(currentScale.y));
        float minimumScale = Mathf.Max(0.01f, minimumVisibleProjectileScale);

        if (currentMagnitude > 0.0001f && currentMagnitude < minimumScale)
        {
            float compensation = minimumScale / currentMagnitude;
            projectile.transform.localScale *= compensation;
        }
    }

    private void EnsureProjectileVisibility(GameObject projectile)
    {
        if (projectile == null)
        {
            return;
        }

        SpriteRenderer projectileRenderer = projectile.GetComponentInChildren<SpriteRenderer>();
        if (projectileRenderer == null)
        {
            return;
        }

        projectileRenderer.enabled = true;
        projectileRenderer.sortingOrder =
            Mathf.Max(projectileRenderer.sortingOrder, minimumProjectileSortingOrder);

        if (projectileRenderer.sprite == null)
        {
            projectileRenderer.sprite = runtimeFallbackProjectileSprite != null
                ? runtimeFallbackProjectileSprite
                : GetGeneratedFallbackProjectileSprite();
        }

        Color color = projectileRenderer.color;
        if (IsNearlyWhite(color))
        {
            color.r = runtimeFallbackProjectileColor.r;
            color.g = runtimeFallbackProjectileColor.g;
            color.b = runtimeFallbackProjectileColor.b;
        }

        color.a = Mathf.Max(color.a, minimumProjectileAlpha);
        projectileRenderer.color = color;
    }

    private static bool IsNearlyWhite(Color color)
    {
        const float channelFloor = 0.84f;
        const float spreadThreshold = 0.08f;

        return color.r >= channelFloor &&
               color.g >= channelFloor &&
               color.b >= channelFloor &&
               Mathf.Abs(color.r - color.g) <= spreadThreshold &&
               Mathf.Abs(color.g - color.b) <= spreadThreshold &&
               Mathf.Abs(color.r - color.b) <= spreadThreshold;
    }

    private static Sprite GetGeneratedFallbackProjectileSprite()
    {
        if (generatedFallbackProjectileSprite != null)
        {
            return generatedFallbackProjectileSprite;
        }

        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = center.magnitude;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (distance / maxDistance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, true);

        generatedFallbackProjectileSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);

        generatedFallbackProjectileSprite.name = "GeneratedEnergyPulseSprite";
        return generatedFallbackProjectileSprite;
    }

    private void IgnoreProjectileCollisionWithPlayer(GameObject projectile)
    {
        Collider2D projectileCollider = projectile.GetComponent<Collider2D>();
        if (projectileCollider == null)
        {
            return;
        }

        Collider2D[] playerColliders = GetComponentsInChildren<Collider2D>();
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider2D playerCollider = playerColliders[i];
            if (playerCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, playerCollider, true);
            }
        }
    }

    private IEnumerator SpawnPulseAfterDelay(GameObject pulsePrefab, float delaySeconds)
    {
        if (pulsePrefab == null)
        {
            yield break;
        }

        yield return new WaitForSeconds(delaySeconds);
        Instantiate(pulsePrefab, transform.position, Quaternion.identity);
    }

    public bool TryUseRadialBurst()
    {
        if (!hasInputAuthority)
        {
            return false;
        }

        if (burstCharges <= 0 || !HasProjectileSource())
        {
            return false;
        }

        burstCharges--;
        BurstChargeChanged?.Invoke(burstCharges);
        UpdateBurstUi();

        float step = 360f / Mathf.Max(1, burstProjectileCount);
        for (int i = 0; i < burstProjectileCount; i++)
        {
            float angle = (step * i) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            FireProjectile(direction, projectileDamage);
        }

        if (burstEffectPrefab != null)
        {
            Instantiate(burstEffectPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            CleanVfxFactory.SpawnAbilityBurstGlow(transform.position);
        }

        AudioManager.Instance?.PlayAbilityBurstSfx();
        RadialBurstUsed?.Invoke();
        return true;
    }

    public void GrantBurstCharges(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        burstCharges += amount;
        BurstChargeChanged?.Invoke(burstCharges);
        UpdateBurstUi();
    }

    public void TakeDamage(int damageAmount)
    {
        if (!hasInputAuthority && Primary != null && Primary != this)
        {
            Primary.TakeDamage(damageAmount);
            return;
        }

        if (isDead)
        {
            return;
        }

        currentHealth -= Mathf.Max(0, damageAmount);
        currentHealth = Mathf.Max(0, currentHealth);
        UpdateHealthUi();

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            animationController?.TriggerTakeDamageAnimation();
            CleanVfxFactory.SpawnImpactSpark(transform.position);
            AudioManager.Instance?.PlayHitSparkSfx();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;

        if (circleCollider != null)
        {
            circleCollider.enabled = false;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        animationController?.TriggerDie();
        if (gameOver != null) gameOver.SetActive(true);
        PlayerDied?.Invoke();

        GameFlowManager flow = FindObjectOfType<GameFlowManager>();
        if (flow != null)
        {
            flow.HandlePlayerDefeated();
        }

        if (autoRestartOnDeath)
        {
            StartCoroutine(RestartSceneAfterDelay(restartDelaySeconds));
        }
    }

    private IEnumerator RestartSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void IncrementZombieKillCount()
    {
        if (!hasInputAuthority && Primary != null && Primary != this)
        {
            Primary.IncrementZombieKillCount();
            return;
        }

        zombieKillCount++;
        AddScore(100);

        if (killCountText != null)
        {
            killCountText.text = FormatKillCount(zombieKillCount);
            if (killPulseCoroutine != null)
            {
                StopCoroutine(killPulseCoroutine);
            }

            killPulseCoroutine = StartCoroutine(PulseTextEffect(killCountText));
        }

        while (zombieKillCount >= nextBurstUnlockAt)
        {
            burstCharges++;
            nextBurstUnlockAt += Mathf.Max(1, killsPerBurstUnlock);
        }

        KillCountChanged?.Invoke(zombieKillCount);
        BurstChargeChanged?.Invoke(burstCharges);
        UpdateBurstUi();
        RefreshRuntimeHudPanel();
    }

    public void AddCollectible(int amount, int scorePerCollectible = 25)
    {
        if (!hasInputAuthority && Primary != null && Primary != this)
        {
            Primary.AddCollectible(amount, scorePerCollectible);
            return;
        }

        collectibleCount += Mathf.Max(1, amount);
        CollectibleChanged?.Invoke(collectibleCount);
        AddScore(Mathf.Max(0, scorePerCollectible) * amount);
    }

    private void AddScore(int delta)
    {
        score += Mathf.Max(0, delta);
        ScoreChanged?.Invoke(score);
        UpdateScoreUi();
    }

    private IEnumerator PulseTextEffect(TextMeshProUGUI text)
    {
        float duration = 0.2f;
        float maxScaleFactor = 1.5f;
        float time = 0f;
        Vector3 maxScale = originalKillTextScale * maxScaleFactor;

        while (time < duration * 0.5f)
        {
            text.transform.localScale = Vector3.Lerp(text.transform.localScale, maxScale, time / (duration * 0.5f));
            time += Time.deltaTime;
            yield return null;
        }

        text.transform.localScale = maxScale;
        time = 0f;

        while (time < duration * 0.5f)
        {
            text.transform.localScale = Vector3.Lerp(text.transform.localScale, originalKillTextScale, time / (duration * 0.5f));
            time += Time.deltaTime;
            yield return null;
        }

        text.transform.localScale = originalKillTextScale;
        killPulseCoroutine = null;
    }

    private void UpdateHealthUi()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        HealthChanged?.Invoke(currentHealth, maxHealth);
        RefreshRuntimeHudPanel();
    }

    private void UpdateScoreUi()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }

    private void UpdateBurstUi()
    {
        if (burstCounterText != null)
        {
            burstCounterText.text = "Burst: " + burstCharges;
        }
    }

    private static string FormatKillCount(int kills)
    {
        return "Kills: " + kills;
    }

    public void FlashGreen()
    {
        StartCoroutine(FlashEffect());
    }

    private IEnumerator FlashEffect()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        spriteRenderer.color = Color.green;
        yield return new WaitForSeconds(0.35f);
        spriteRenderer.color = originalColor;
    }

    public void SetArcherStatus(bool status)
    {
        isRanged = status;
    }

    public void SetActiveStatus(bool status)
    {
        isActive = status;
    }
/*
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Stairs"))
        {
            isOnStairs = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Stairs"))
        {
            isOnStairs = false;
        }
    }
*/
    private float SnapAngleToEightDirections(float angle)
    {
        angle = (angle + 360f) % 360f;

        if (isOnStairs)
        {
            if (angle < 30f || angle >= 330f) return 0f;
            if (angle < 75f) return 60f;
            if (angle < 105f) return 90f;
            if (angle < 150f) return 120f;
            if (angle < 210f) return 180f;
            if (angle < 255f) return 240f;
            if (angle < 285f) return 270f;
            return 300f;
        }

        if (angle < 15f || angle >= 345f) return 0f;
        if (angle < 75f) return 30f;
        if (angle < 105f) return 90f;
        if (angle < 165f) return 150f;
        if (angle < 195f) return 180f;
        if (angle < 255f) return 210f;
        if (angle < 285f) return 270f;
        return 330f;
    }

    public static PlayerController FindPrimary()
    {
        if (primaryInstance != null)
        {
            return primaryInstance;
        }

        RefreshPrimaryInstance();
        if (primaryInstance != null)
        {
            return primaryInstance;
        }

        return FindObjectOfType<PlayerController>();
    }

    private static void RegisterInstance(PlayerController controller)
    {
        if (controller == null || Instances.Contains(controller))
        {
            return;
        }

        Instances.Add(controller);
        RefreshPrimaryInstance();
    }

    private static void UnregisterInstance(PlayerController controller)
    {
        if (controller == null)
        {
            return;
        }

        Instances.Remove(controller);
        if (primaryInstance == controller)
        {
            primaryInstance = null;
        }

        RefreshPrimaryInstance();
    }

    private static void RefreshPrimaryInstance()
    {
        for (int i = Instances.Count - 1; i >= 0; i--)
        {
            if (Instances[i] == null)
            {
                Instances.RemoveAt(i);
            }
        }

        PlayerController best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < Instances.Count; i++)
        {
            PlayerController candidate = Instances[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            int score = EvaluatePrimaryScore(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        primaryInstance = best;

        for (int i = 0; i < Instances.Count; i++)
        {
            PlayerController controller = Instances[i];
            if (controller != null)
            {
                controller.ApplyInputAuthority(controller == primaryInstance);
            }
        }
    }

    private static int EvaluatePrimaryScore(PlayerController candidate)
    {
        int score = 0;
        if (candidate.projectilePrefab != null) score += 100;
        if (candidate.animationController != null) score += 20;
        if (!candidate.isDead) score += 20;
        if (candidate.enabled) score += 10;
        if (candidate.gameObject.CompareTag("Player")) score += 5;
        return score;
    }

    private void ApplyInputAuthority(bool authority)
    {
        hasInputAuthority = authority;
        CacheComponents();

        if (authority)
        {
            if (rb != null)
            {
                rb.simulated = true;
                rb.freezeRotation = true;
            }

            if (circleCollider != null)
            {
                circleCollider.enabled = true;
            }

            if (!CompareTag("Player"))
            {
                gameObject.tag = "Player";
            }

            SetVisualState(true);
            EnsureRuntimeHudPanel();
            RefreshRuntimeHudPanel();
        }
        else
        {
            movementDirection = Vector2.zero;
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.simulated = false;
            }

            if (circleCollider != null)
            {
                circleCollider.enabled = false;
            }

            if (CompareTag("Player"))
            {
                gameObject.tag = "Untagged";
            }

            SetVisualState(false);
        }
    }

    private void CacheComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (circleCollider == null)
        {
            circleCollider = GetComponent<CircleCollider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private static void TryAssignLayer(GameObject target, string layerName)
    {
        if (target == null || string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
        {
            target.layer = layer;
        }
    }

    private static void TryAssignLayerRecursively(GameObject target, string layerName)
    {
        if (target == null || string.IsNullOrWhiteSpace(layerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0)
        {
            return;
        }

        AssignLayerRecursively(target.transform, layer);
    }

    private static void AssignLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            AssignLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void EnsureRuntimeHudPanel()
    {
        if (!showRuntimeHudPanel || !hasInputAuthority)
        {
            return;
        }

        if (runtimeHudHealthText != null &&
            runtimeHudKillText != null &&
            runtimeHudHealthBarFillImage != null)
        {
            return;
        }

        GameObject existingCanvas = GameObject.Find("RuntimeHudCanvas");
        if (existingCanvas != null)
        {
            CacheRuntimeHudReferences(existingCanvas.transform);
            if (runtimeHudHealthText != null &&
                runtimeHudKillText != null &&
                runtimeHudHealthBarFillImage != null)
            {
                return;
            }
        }

        GameObject canvasObject = existingCanvas != null
            ? existingCanvas
            : new GameObject(
                "RuntimeHudCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 450;
        }

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Transform panelTransform = canvasObject.transform.Find("HudPanel");
        GameObject panelObject;
        if (panelTransform == null)
        {
            panelObject = new GameObject("HudPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = runtimeHudPanelOffset;
            panelRect.sizeDelta = runtimeHudPanelSize;

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = runtimeHudPanelColor;
        }
        else
        {
            panelObject = panelTransform.gameObject;
            Image panelImage = panelObject.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelObject.AddComponent<Image>();
            }

            panelImage.color = runtimeHudPanelColor;

            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0f, 1f);
                panelRect.anchorMax = new Vector2(0f, 1f);
                panelRect.pivot = new Vector2(0f, 1f);
                panelRect.anchoredPosition = runtimeHudPanelOffset;
                panelRect.sizeDelta = runtimeHudPanelSize;
            }
        }

        CacheRuntimeHudReferences(canvasObject.transform);

        if (runtimeHudHealthText == null)
        {
            runtimeHudHealthText = CreateRuntimeHudText(
                panelObject.transform,
                "HealthText",
                new Vector2(12f, -14f),
                "Health: --/--");
        }

        if (runtimeHudHealthBarFillImage == null)
        {
            runtimeHudHealthBarFillImage = CreateRuntimeHudHealthBar(panelObject.transform);
        }

        if (runtimeHudKillText == null)
        {
            runtimeHudKillText = CreateRuntimeHudText(
                panelObject.transform,
                "KillsText",
                new Vector2(12f, -74f),
                "Kills: 0");
        }
    }

    private void CacheRuntimeHudReferences(Transform canvasTransform)
    {
        if (canvasTransform == null)
        {
            return;
        }

        Transform panel = canvasTransform.Find("HudPanel");
        if (panel == null)
        {
            return;
        }

        if (runtimeHudHealthText == null)
        {
            Transform healthTextTransform = panel.Find("HealthText");
            if (healthTextTransform != null)
            {
                runtimeHudHealthText = healthTextTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (runtimeHudKillText == null)
        {
            Transform killTextTransform = panel.Find("KillsText");
            if (killTextTransform != null)
            {
                runtimeHudKillText = killTextTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (runtimeHudHealthBarFillImage == null)
        {
            Transform healthFillTransform = panel.Find("HealthBarBackground/Fill");
            if (healthFillTransform != null)
            {
                runtimeHudHealthBarFillImage = healthFillTransform.GetComponent<Image>();
            }
        }
    }

    private TextMeshProUGUI CreateRuntimeHudText(
        Transform parent,
        string objectName,
        Vector2 anchoredPosition,
        string initialText)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(runtimeHudPanelSize.x - 24f, 30f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = initialText;
        text.fontSize = 24f;
        text.color = runtimeHudTextColor;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        TMP_FontAsset hudFont = GetRuntimeHudFont();
        if (hudFont != null)
        {
            text.font = hudFont;
        }

        return text;
    }

    private Image CreateRuntimeHudHealthBar(Transform parent)
    {
        GameObject backgroundObject = new GameObject("HealthBarBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(parent, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0f, 1f);
        backgroundRect.anchorMax = new Vector2(0f, 1f);
        backgroundRect.pivot = new Vector2(0f, 1f);
        backgroundRect.anchoredPosition = new Vector2(12f, -44f);
        backgroundRect.sizeDelta = new Vector2(runtimeHudPanelSize.x - 24f, 18f);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = runtimeHudHealthBarBackgroundColor;

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = runtimeHudHealthBarFillColor;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        return fillImage;
    }

    private static TMP_FontAsset GetRuntimeHudFont()
    {
        if (runtimeHudFontAsset != null)
        {
            return runtimeHudFontAsset;
        }

        runtimeHudFontAsset = TMP_Settings.defaultFontAsset;
        if (runtimeHudFontAsset == null)
        {
            runtimeHudFontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        }

        return runtimeHudFontAsset;
    }

    private void RefreshRuntimeHudPanel()
    {
        if (!showRuntimeHudPanel || !hasInputAuthority)
        {
            return;
        }

        if (runtimeHudHealthText == null ||
            runtimeHudKillText == null ||
            runtimeHudHealthBarFillImage == null)
        {
            EnsureRuntimeHudPanel();
        }

        if (runtimeHudHealthText != null)
        {
            runtimeHudHealthText.text = "Health: " + currentHealth + "/" + maxHealth;
            runtimeHudHealthText.color = runtimeHudTextColor;
        }

        if (runtimeHudHealthBarFillImage != null)
        {
            float normalizedHealth = maxHealth > 0
                ? Mathf.Clamp01((float)currentHealth / maxHealth)
                : 0f;
            runtimeHudHealthBarFillImage.fillAmount = normalizedHealth;
            runtimeHudHealthBarFillImage.color = Color.Lerp(
                new Color(1f, 0.25f, 0.2f, 0.95f),
                runtimeHudHealthBarFillColor,
                normalizedHealth);
        }

        if (runtimeHudKillText != null)
        {
            runtimeHudKillText.text = FormatKillCount(zombieKillCount);
            runtimeHudKillText.color = runtimeHudTextColor;
        }
    }

    private void SetVisualState(bool enabled)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = enabled;
            }
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = enabled;
            }
        }
    }
}
