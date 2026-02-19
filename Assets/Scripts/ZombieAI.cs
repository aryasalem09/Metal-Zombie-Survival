using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ZombieAI : MonoBehaviour
{
    public event Action<ZombieAI> Died;

    [Header("References")]
    public Transform player;
    public PlayerController playerController;
    public SpriteRenderer spriteRenderer;
    public Animator animator;

    [Header("Movement")]
    public float detectionRadius = 12f;
    public float moveSpeed = 1.8f;
    public float movementSmoothing = 14f;
    public bool isRunner;

    [Header("Line Of Sight")]
    public LayerMask obstacleMask;
    public float lineOfSightCheckInterval = 0.15f;
    public float blockedPauseSeconds = 0.3f;
    [SerializeField] private string regionBoundsLayerName = "RegionBounds";

    [Header("Attack")]
    public float attackRange = 0.8f;
    public float attackCooldown = 1.0f;
    public int zombieDamage = 1;
    [Min(0.01f)] public float minimumAttackRange = 0.6f;
    [Min(0f)] public float attackRangePadding = 0.15f;

    [Header("Low Health Rage")]
    [Min(1)] public int lowHealthThreshold = 3;
    [Range(1f, 4f)] public float lowHealthSpeedMultiplier = 1.6f;
    [Range(1f, 4f)] public float lowHealthDamageMultiplier = 1.7f;
    [SerializeField] private bool enragedAtLowHealth;

    [Header("Damage Reaction")]
    public float hurtDuration = 0.15f;
    public float knockbackImpulse = 1.15f;
    public float alertedDetectionRadius = 16f;
    public float alertDuration = 2.5f;
    public Color hitFlashColor = new Color(1f, 0.28f, 0.28f, 1f);
    [Range(0.03f, 0.3f)] public float hitFlashDuration = 0.12f;
    [Min(0.1f)] public float knockbackVelocityDamping = 22f;
    [Min(0.1f)] public float idleVelocityDamping = 16f;
    [Range(0f, 1f)] public float knockbackRetentionAfterHurt = 0.1f;
    [Min(0.2f)] public float maxKnockbackSpeed = 4f;

    [Header("Health")]
    public int maxHealth = 9;
    public int currentHealth;
    public bool isDead;

    [Header("Visual Effects")]
    [SerializeField] private List<GameObject> hitEffectPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> radiatedPrefabs = new List<GameObject>();
    public bool isRadiated;

    [Header("Cleanup")]
    public float destroyAfterDeathSeconds = 3f;

    private static readonly string[] DirectionParameters =
    {
        "isWest",
        "isEast",
        "isSouth",
        "isSouthWest",
        "isNorthEast",
        "isSouthEast",
        "isNorth",
        "isNorthWest"
    };

    private Rigidbody2D rb;
    private Collider2D[] colliders;
    private Coroutine detectionResetCoroutine;
    private Coroutine hurtRecoveryCoroutine;

    private Vector2 desiredVelocity;
    private Vector2 lastKnownPlayerPosition;
    private Vector2 facingDirection = Vector2.right;

    private float nextLineOfSightCheckTime;
    private float nextAttackTime;
    private float hurtUntilTime;
    private float pauseMovementUntilTime;
    private float baseDetectionRadius;
    private bool temporarilyHurt;
    private bool hasLineOfSight = true;
    private bool lineOfSightBlocked;
    private bool hasLastKnownPlayerPosition;
    private bool regionBoundaryBypassApplied;
    private string currentDirection = "isEast";
    private Color baseSpriteColor = Color.white;
    private Coroutine hitFlashCoroutine;

    // Animation safety flags – prevent movement bools from overwriting
    // attack / take-damage states, same pattern as the player controller.
    private bool isPlayingAttackAnim;
    private bool isPlayingHurtAnim;
    private float attackAnimSetTime;
    private float hurtAnimSetTime;
    private const float MaxZombieAttackAnimDuration = 0.5f;
    private const float MaxZombieHurtAnimDuration = 0.4f;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (animator == null) animator = GetComponent<Animator>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);

        if (spriteRenderer != null)
        {
            baseSpriteColor = spriteRenderer.color;
        }

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0)
        {
            gameObject.layer = enemyLayer;
        }

        if (obstacleMask.value == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacles");
            if (obstacleLayer >= 0)
            {
                obstacleMask = 1 << obstacleLayer;
            }
        }

        regionBoundaryBypassApplied = TryIgnoreRegionBoundaryCollisions();

        currentHealth = Mathf.Max(1, maxHealth);
        baseDetectionRadius = Mathf.Max(3f, detectionRadius);
        attackRange = Mathf.Max(minimumAttackRange, attackRange);
        zombieDamage = Mathf.Max(1, zombieDamage);

        AcquirePlayer();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        if (!EnsurePlayerReference())
        {
            desiredVelocity = Vector2.zero;
            UpdateMovementAnimation(Vector2.zero);
            return;
        }

        if (!regionBoundaryBypassApplied)
        {
            regionBoundaryBypassApplied = TryIgnoreRegionBoundaryCollisions();
        }

        Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer <= detectionRadius && Time.time >= nextLineOfSightCheckTime)
        {
            bool clearLineOfSight = HasClearLineOfSightToPlayer();
            nextLineOfSightCheckTime = Time.time + Mathf.Max(0.05f, lineOfSightCheckInterval);

            if (clearLineOfSight)
            {
                hasLineOfSight = true;
                lineOfSightBlocked = false;
                lastKnownPlayerPosition = player.position;
                hasLastKnownPlayerPosition = true;
            }
            else
            {
                hasLineOfSight = false;
                if (!hasLastKnownPlayerPosition)
                {
                    lastKnownPlayerPosition = player.position;
                    hasLastKnownPlayerPosition = true;
                }

                if (!lineOfSightBlocked)
                {
                    pauseMovementUntilTime = Time.time + Mathf.Max(0f, blockedPauseSeconds);
                }

                lineOfSightBlocked = true;
            }
        }

        if (Time.time < hurtUntilTime)
        {
            desiredVelocity = Vector2.zero;
            UpdateMovementAnimation(Vector2.zero);
            return;
        }

        if (temporarilyHurt)
        {
            desiredVelocity = Vector2.zero;
            UpdateMovementAnimation(Vector2.zero);
            return;
        }

        if (CanAttackPlayer(distanceToPlayer))
        {
            desiredVelocity = Vector2.zero;
            TryAttack();
            UpdateMovementAnimation(Vector2.zero);
            return;
        }

        bool shouldChase =
            distanceToPlayer <= detectionRadius ||
            (hasLastKnownPlayerPosition &&
             Vector2.Distance(transform.position, lastKnownPlayerPosition) > 0.2f);

        if (shouldChase)
        {
            if (Time.time < pauseMovementUntilTime)
            {
                desiredVelocity = Vector2.zero;
            }
            else
            {
                Vector2 target = hasLineOfSight
                    ? (Vector2)player.position
                    : (hasLastKnownPlayerPosition ? lastKnownPlayerPosition : (Vector2)player.position);

                MoveTowards(target);
            }
        }
        else
        {
            desiredVelocity = Vector2.zero;
        }

        UpdateMovementAnimation(desiredVelocity);
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        EnsureMovableBody();

        if (rb != null)
        {
            float followSmoothing = Mathf.Max(1f, movementSmoothing);
            rb.velocity = Vector2.Lerp(
                rb.velocity,
                desiredVelocity,
                Time.fixedDeltaTime * followSmoothing);

            if (temporarilyHurt || Time.time < hurtUntilTime)
            {
                float damp = Mathf.Clamp01(Time.fixedDeltaTime * Mathf.Max(0.1f, knockbackVelocityDamping));
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, damp);
            }
            else if (desiredVelocity.sqrMagnitude < 0.0001f)
            {
                float damp = Mathf.Clamp01(Time.fixedDeltaTime * Mathf.Max(0.1f, idleVelocityDamping));
                rb.velocity = Vector2.Lerp(rb.velocity, Vector2.zero, damp);
            }

            if (desiredVelocity.sqrMagnitude < 0.0001f && rb.velocity.sqrMagnitude < 0.0004f)
            {
                rb.velocity = Vector2.zero;
            }
        }
        else
        {
            transform.position += (Vector3)(desiredVelocity * Time.fixedDeltaTime);
        }
    }

    private void MoveTowards(Vector2 targetPosition)
    {
        Vector2 delta = targetPosition - (Vector2)transform.position;
        if (delta.sqrMagnitude < 0.0001f)
        {
            desiredVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = delta.normalized;
        facingDirection = direction;

        float effectiveSpeed = isRunner ? moveSpeed * 1.35f : moveSpeed;
        desiredVelocity = direction * Mathf.Max(0f, effectiveSpeed);
    }

    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        if (!EnsurePlayerReference() || player == null || playerController == null)
        {
            return;
        }

        float distanceToTarget = Vector2.Distance(transform.position, player.position);
        if (distanceToTarget > GetEffectiveAttackRange())
        {
            return;
        }

        nextAttackTime = Time.time + Mathf.Max(0.05f, attackCooldown);
        facingDirection = ((Vector2)player.position - (Vector2)transform.position).normalized;

        isPlayingAttackAnim = true;
        attackAnimSetTime = Time.time;

        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", true);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", false);
        SetDirectionalBool("AttackAttack", true);
        SetDirectionalBool("Attack2", true);

        StartCoroutine(ResetAttackAnimationAfterDelay(0.16f));
        playerController.TakeDamage(zombieDamage);
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead)
        {
            return;
        }

        currentHealth -= Mathf.Max(0, damageAmount);
        currentHealth = Mathf.Max(0, currentHealth);

        TriggerHitFlash();

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        TryActivateLowHealthRage();

        if (detectionResetCoroutine != null)
        {
            StopCoroutine(detectionResetCoroutine);
        }

        detectionRadius = Mathf.Max(baseDetectionRadius, alertedDetectionRadius);
        detectionResetCoroutine = StartCoroutine(ResetDetectionRadiusAfterDelay(alertDuration));

        hurtUntilTime = Time.time + Mathf.Max(0.05f, hurtDuration);
        pauseMovementUntilTime = Time.time + Mathf.Min(0.08f, blockedPauseSeconds);
        temporarilyHurt = true;
        if (hurtRecoveryCoroutine != null)
        {
            StopCoroutine(hurtRecoveryCoroutine);
        }

        hurtRecoveryCoroutine = StartCoroutine(RecoverFromHitAfterDelay(hurtDuration));

        EnsureMovableBody();
        ApplyKnockbackFromPlayer();
        TriggerTakeDamageAnimation();
        SpawnHitEffect();
    }

    public void ApplyMutantModifiers(
        float healthMultiplier,
        float speedMultiplier,
        float damageMultiplier,
        float scaleMultiplier,
        Color tint,
        bool runner)
    {
        maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * healthMultiplier));
        currentHealth = maxHealth;
        moveSpeed *= speedMultiplier;
        zombieDamage = Mathf.Max(1, Mathf.RoundToInt(zombieDamage * damageMultiplier));
        transform.localScale *= Mathf.Max(0.2f, scaleMultiplier);
        isRunner = runner || isRunner;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = tint;
            baseSpriteColor = tint;
        }
    }

    private void ApplyKnockbackFromPlayer()
    {
        if (rb == null)
        {
            return;
        }

        rb.WakeUp();

        Transform playerTransform = player != null
            ? player
            : playerController != null ? playerController.transform : null;

        if (playerTransform == null)
        {
            return;
        }

        Vector2 away = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
        if (away.sqrMagnitude < 0.0001f)
        {
            away = UnityEngine.Random.insideUnitCircle.normalized;
        }

        rb.AddForce(away * Mathf.Max(0f, knockbackImpulse), ForceMode2D.Impulse);

        float maxSpeed = Mathf.Max(0.2f, maxKnockbackSpeed);
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }

    private void TriggerTakeDamageAnimation()
    {
        isPlayingHurtAnim = true;
        hurtAnimSetTime = Time.time;

        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", true);
        SetDirectionalBool("TakeDamage", true);
        SetDirectionalBool("takeDamage", true);
        AnimatorParamAdapter.SetTrigger(animator, "TakeDamage");
        StartCoroutine(ResetTakeDamageAnimationAfterDelay(0.18f));
    }

    private void UpdateMovementAnimation(Vector2 velocity)
    {
        if (animator == null)
        {
            return;
        }

        // Safety timeout: force-clear stale anim flags if coroutines were killed
        if (isPlayingAttackAnim && Time.time - attackAnimSetTime > MaxZombieAttackAnimDuration)
        {
            isPlayingAttackAnim = false;
        }
        if (isPlayingHurtAnim && Time.time - hurtAnimSetTime > MaxZombieHurtAnimDuration)
        {
            isPlayingHurtAnim = false;
        }

        // Always update facing so the sprite looks correct
        bool isMoving = velocity.sqrMagnitude > 0.01f;
        Vector2 directionForAnimation = isMoving ? velocity.normalized : facingDirection;
        if (directionForAnimation.sqrMagnitude > 0.0001f)
        {
            SetDirection(VectorToDirection(directionForAnimation));
        }

        // Priority guard – don't overwrite attack/hurt bools
        if (isPlayingAttackAnim || isPlayingHurtAnim)
        {
            return;
        }

        AnimatorParamAdapter.SetBool(animator, "isWalking", isMoving);
        AnimatorParamAdapter.SetBool(animator, "isRunning", isMoving && isRunner);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TryContactAttack(collision.collider);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryContactAttack(other);
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        desiredVelocity = Vector2.zero;
        temporarilyHurt = false;
        hurtUntilTime = 0f;
        isPlayingAttackAnim = false;
        isPlayingHurtAnim = false;

        if (hurtRecoveryCoroutine != null)
        {
            StopCoroutine(hurtRecoveryCoroutine);
            hurtRecoveryCoroutine = null;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            hitFlashCoroutine = null;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = false;
                }
            }
        }

        AnimatorParamAdapter.SetTrigger(animator, "isDie");
        AnimatorParamAdapter.SetTrigger(animator, "dieTrigger");
        AnimatorParamAdapter.SetTrigger(animator, "Die");
        SetDirectionalTrigger("die");

        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<PlayerController>();
        }

        if (playerController != null)
        {
            playerController.IncrementZombieKillCount();
        }

        AudioManager.Instance?.PlayZombieDeathSfx();
        CleanVfxFactory.SpawnZombieDeathPoof(transform.position);

        Died?.Invoke(this);
        Destroy(gameObject, destroyAfterDeathSeconds);
    }

    private void TryActivateLowHealthRage()
    {
        if (enragedAtLowHealth || isDead)
        {
            return;
        }

        int threshold = Mathf.Max(1, lowHealthThreshold);
        if (currentHealth >= threshold)
        {
            return;
        }

        enragedAtLowHealth = true;
        moveSpeed *= Mathf.Max(1f, lowHealthSpeedMultiplier);
        zombieDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(zombieDamage * Mathf.Max(1f, lowHealthDamageMultiplier)));
        detectionRadius = Mathf.Max(detectionRadius, alertedDetectionRadius);
    }

    private void TriggerHitFlash()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
        }

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        spriteRenderer.color = hitFlashColor;
        yield return new WaitForSeconds(Mathf.Max(0.03f, hitFlashDuration));

        if (spriteRenderer != null && !isDead)
        {
            spriteRenderer.color = baseSpriteColor;
        }

        hitFlashCoroutine = null;
    }

    private bool HasClearLineOfSightToPlayer()
    {
        if (player == null)
        {
            return false;
        }

        Vector2 origin = transform.position;
        Vector2 destination = player.position;
        Vector2 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            distance,
            obstacleMask);

        if (hit.collider == null)
        {
            return true;
        }

        if (player != null && hit.collider.transform.root == player.root)
        {
            return true;
        }

        PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
        return hitPlayer != null && hitPlayer == playerController;
    }

    private bool CanAttackPlayer(float distanceToPlayer)
    {
        if (!EnsurePlayerReference())
        {
            return false;
        }

        float effectiveRange = GetEffectiveAttackRange();
        if (distanceToPlayer > effectiveRange)
        {
            return false;
        }

        if (hasLineOfSight)
        {
            return true;
        }

        // If we are already touching or almost touching the player,
        // allow the hit even if the line-of-sight ray momentarily clips geometry.
        return distanceToPlayer <= Mathf.Max(0.2f, effectiveRange * 0.65f);
    }

    private float GetEffectiveAttackRange()
    {
        float configuredRange = Mathf.Max(minimumAttackRange, attackRange);
        float ownRadius = EstimateRadiusFromColliders(gameObject);
        float playerRadius = EstimatePlayerRadius();
        float contactRange = ownRadius + playerRadius + Mathf.Max(0f, attackRangePadding);
        return Mathf.Max(configuredRange, contactRange);
    }

    private float EstimatePlayerRadius()
    {
        if (!EnsurePlayerReference() || playerController == null)
        {
            return 0.12f;
        }

        return EstimateRadiusFromColliders(playerController.gameObject);
    }

    private static float EstimateRadiusFromColliders(GameObject target)
    {
        if (target == null)
        {
            return 0.12f;
        }

        Collider2D[] targetColliders = target.GetComponents<Collider2D>();
        float maxRadius = 0.12f;
        for (int i = 0; i < targetColliders.Length; i++)
        {
            Collider2D targetCollider = targetColliders[i];
            if (targetCollider == null || !targetCollider.enabled)
            {
                continue;
            }

            Bounds bounds = targetCollider.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y);
            maxRadius = Mathf.Max(maxRadius, radius);
        }

        return maxRadius;
    }

    private void TryContactAttack(Collider2D other)
    {
        if (isDead || other == null || Time.time < nextAttackTime)
        {
            return;
        }

        if (!other.CompareTag("Player"))
        {
            return;
        }

        playerController = other.GetComponent<PlayerController>() ??
                           other.GetComponentInParent<PlayerController>() ??
                           playerController;

        if (playerController == null)
        {
            playerController = PlayerController.FindPrimary();
        }

        if (playerController == null)
        {
            return;
        }

        player = playerController.transform;
        desiredVelocity = Vector2.zero;
        hasLineOfSight = true;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);
        if (distanceToPlayer <= GetEffectiveAttackRange() + 0.05f)
        {
            TryAttack();
        }
    }

    private void AcquirePlayer()
    {
        if (playerController == null)
        {
            playerController = PlayerController.FindPrimary();
        }

        if (playerController != null)
        {
            player = playerController.transform;
            lastKnownPlayerPosition = player.position;
            hasLastKnownPlayerPosition = true;
            return;
        }

        if (player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                player = taggedPlayer.transform;
            }
        }

        if (playerController == null)
        {
            if (player != null)
            {
                playerController = player.GetComponent<PlayerController>();
                if (playerController == null)
                {
                    playerController = player.GetComponentInParent<PlayerController>();
                }
            }

            if (playerController == null)
            {
                playerController = PlayerController.FindPrimary();
                if (playerController != null)
                {
                    player = playerController.transform;
                }
            }
        }

        if (player != null)
        {
            lastKnownPlayerPosition = player.position;
            hasLastKnownPlayerPosition = true;
        }
    }

    private bool EnsurePlayerReference()
    {
        if (playerController != null)
        {
            player = playerController.transform;
            return true;
        }

        AcquirePlayer();

        if (playerController != null)
        {
            player = playerController.transform;
            return true;
        }

        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = player.GetComponentInParent<PlayerController>();
            }

            if (playerController != null)
            {
                player = playerController.transform;
                return true;
            }
        }

        return player != null;
    }

    private void EnsureMovableBody()
    {
        if (rb == null || isDead)
        {
            return;
        }

        if (rb.bodyType != RigidbodyType2D.Dynamic)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        if (!rb.simulated)
        {
            rb.simulated = true;
        }

        if ((rb.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
        {
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private IEnumerator RecoverFromHitAfterDelay(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.05f, delay));
        temporarilyHurt = false;
        hurtUntilTime = 0f;

        if (rb != null && !isDead)
        {
            rb.velocity *= Mathf.Clamp01(knockbackRetentionAfterHurt);
            if (rb.velocity.sqrMagnitude < 0.001f)
            {
                rb.velocity = Vector2.zero;
            }
        }

        hurtRecoveryCoroutine = null;
    }

    private IEnumerator ResetDetectionRadiusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, delay));
        detectionRadius = baseDetectionRadius;
        detectionResetCoroutine = null;
    }

    private IEnumerator ResetTakeDamageAnimationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", false);
        SetDirectionalBool("TakeDamage", false);
        SetDirectionalBool("takeDamage", false);
        isPlayingHurtAnim = false;
    }

    private IEnumerator ResetAttackAnimationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", false);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", false);
        SetDirectionalBool("AttackAttack", false);
        SetDirectionalBool("Attack2", false);
        isPlayingAttackAnim = false;
    }

    private void SpawnHitEffect()
    {
        List<GameObject> effectPool = isRadiated ? radiatedPrefabs : hitEffectPrefabs;
        if (effectPool != null && effectPool.Count > 0)
        {
            GameObject prefab = effectPool[UnityEngine.Random.Range(0, effectPool.Count)];
            if (prefab != null)
            {
                Instantiate(prefab, transform.position, Quaternion.identity);
                return;
            }
        }

        CleanVfxFactory.SpawnImpactSpark(transform.position);
    }

    private void SetDirection(string direction)
    {
        if (direction == currentDirection)
        {
            return;
        }

        for (int i = 0; i < DirectionParameters.Length; i++)
        {
            string parameter = DirectionParameters[i];
            bool isCurrentDirection = parameter == direction;
            AnimatorParamAdapter.SetBool(animator, parameter, isCurrentDirection);
            AnimatorParamAdapter.SetBool(animator, "Move" + DirectionSuffix(parameter), isCurrentDirection);
        }

        currentDirection = direction;
    }

    private void SetDirectionalBool(string prefix, bool value)
    {
        string suffix = DirectionSuffix(currentDirection);
        string parameterName = prefix + suffix;
        AnimatorParamAdapter.SetBool(animator, parameterName, value);
    }

    private void SetDirectionalTrigger(string prefix)
    {
        string suffix = DirectionSuffix(currentDirection);
        string parameterName = prefix + suffix;
        AnimatorParamAdapter.SetTrigger(animator, parameterName);
    }

    private static string VectorToDirection(Vector2 vector)
    {
        float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        angle = (angle + 360f) % 360f;

        if (angle >= 337.5f || angle < 22.5f) return "isEast";
        if (angle < 67.5f) return "isNorthEast";
        if (angle < 112.5f) return "isNorth";
        if (angle < 157.5f) return "isNorthWest";
        if (angle < 202.5f) return "isWest";
        if (angle < 247.5f) return "isSouthWest";
        if (angle < 292.5f) return "isSouth";
        return "isSouthEast";
    }

    private static string DirectionSuffix(string directionBoolName)
    {
        if (string.IsNullOrEmpty(directionBoolName))
        {
            return "East";
        }

        return directionBoolName.StartsWith("is")
            ? directionBoolName.Substring(2)
            : directionBoolName;
    }

    private bool TryIgnoreRegionBoundaryCollisions()
    {
        if (colliders == null || colliders.Length == 0)
        {
            colliders = GetComponents<Collider2D>();
            if (colliders == null || colliders.Length == 0)
            {
                return false;
            }
        }

        Collider2D[] sceneColliders = FindObjectsOfType<Collider2D>(true);
        if (sceneColliders == null || sceneColliders.Length == 0)
        {
            return false;
        }

        int regionBoundsLayer = LayerMask.NameToLayer(regionBoundsLayerName);
        int ignoredCount = 0;

        for (int i = 0; i < sceneColliders.Length; i++)
        {
            Collider2D boundary = sceneColliders[i];
            if (boundary == null || !boundary.enabled || boundary.isTrigger)
            {
                continue;
            }

            if (boundary.transform.root == transform.root)
            {
                continue;
            }

            bool matchesLayer = regionBoundsLayer >= 0 &&
                                boundary.gameObject.layer == regionBoundsLayer;
            bool matchesFallbackName = IsLikelyBoundaryByName(boundary);
            if (!matchesLayer && !matchesFallbackName)
            {
                continue;
            }

            for (int j = 0; j < colliders.Length; j++)
            {
                Collider2D ownCollider = colliders[j];
                if (ownCollider == null || !ownCollider.enabled)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(ownCollider, boundary, true);
            }

            ignoredCount++;
        }

        return ignoredCount > 0;
    }

    private static bool IsLikelyBoundaryByName(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        Transform target = collider.transform;
        if (string.Equals(target.name, "ColliderGrid", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (target.parent != null &&
            string.Equals(target.parent.name, "ColliderGrid", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return collider is EdgeCollider2D &&
               target.parent != null &&
               target.parent.GetComponent<Grid>() != null;
    }
}
