using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class AnimationController : MonoBehaviour
{
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

    private Animator animator;
    private PlayerController playerController;
    private Rigidbody2D playerBody;
    private Coroutine attackResetCoroutine;
    private Coroutine takeDamageResetCoroutine;

    [FormerlySerializedAs("muzzleAnimator")]
    public Animator pulseEmitterAnimator;
    [FormerlySerializedAs("muzzleFlashRenderer")]
    public SpriteRenderer pulseFlashRenderer;

    public string currentDirection = "isEast";
    public bool isCurrentlyRunning;
    public bool isCrouching;
    public bool isDying;

    [FormerlySerializedAs("bloodPrefabs")]
    [SerializeField] private List<GameObject> hitEffectPrefabs = new List<GameObject>();
    [SerializeField] private List<GameObject> radiatedPrefabs = new List<GameObject>();
    public bool isRadiated;
    public float rollTime = 0.5f;

    public bool isRunning;
    public bool isRunningBackwards;
    public bool isStrafingLeft;
    public bool isStrafingRight;
    public bool isAttacking;
    public bool isTakingDamage;

    // Safety timeout — if a coroutine is killed (death, disable, scene change)
    // these timestamps let Update detect stale flags and force-reset them.
    private float attackFlagSetTime;
    private float takeDamageFlagSetTime;
    private const float MaxAttackFlagDuration = 0.6f;
    private const float MaxTakeDamageFlagDuration = 0.5f;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        playerController = GetComponent<PlayerController>();
        playerBody = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        SetDirection("isEast");
        AnimatorParamAdapter.SetBool(animator, "isWalking", false);
        AnimatorParamAdapter.SetBool(animator, "isRunning", false);
        AnimatorParamAdapter.SetBool(animator, "isCrouchRunning", false);
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", false);
    }

    // Animation is driven externally by PlayerController.Update() calling
    // UpdateMovementAnimation / UpdateFacingDirection.  A self-driven Update
    // was here before but caused a double-update every frame, making the
    // animator thrash between states and producing visible flicker.
    //
    // If this component is ever used standalone (no PlayerController) you can
    // uncomment the block below, but with a PlayerController present it MUST
    // be disabled.
    /*
    private void Update()
    {
        if (!gameObject.activeInHierarchy || isDying) return;
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
            if (playerController == null) return;
        }
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (animator == null) return;
        }
        Vector2 movement = playerController.MovementDirection;
        if (movement.sqrMagnitude <= 0.0001f && playerBody != null && playerBody.velocity.sqrMagnitude > 0.0004f)
            movement = playerBody.velocity;
        UpdateMovementAnimation(movement, playerController.LookDirection, playerController.IsRunning, playerController.isCrouching);
    }
    */

    /// <summary>
    /// Safety net: if isAttacking or isTakingDamage flags are stuck (e.g. coroutine
    /// killed by death or scene change), force-clear them after a timeout so the
    /// animator is never permanently locked out of movement states.
    /// </summary>
    private void LateUpdate()
    {
        if (isDying) return;

        if (isAttacking && Time.time - attackFlagSetTime > MaxAttackFlagDuration)
        {
            ForceResetAttack();
        }

        if (isTakingDamage && Time.time - takeDamageFlagSetTime > MaxTakeDamageFlagDuration)
        {
            ForceResetTakeDamage();
        }
    }

    private void OnDisable()
    {
        // Reset all transient flags so re-enabling the object starts clean.
        if (isAttacking) ForceResetAttack();
        if (isTakingDamage) ForceResetTakeDamage();
    }

    private void ForceResetAttack()
    {
        isAttacking = false;
        if (animator != null)
        {
            AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", false);
            AnimatorParamAdapter.SetBool(animator, "isAttackRunning", false);
            SetDirectionalBoolExclusive("AttackAttack", false);
            SetDirectionalBoolExclusive("Attack2", false);
        }
        if (attackResetCoroutine != null)
        {
            StopCoroutine(attackResetCoroutine);
            attackResetCoroutine = null;
        }
    }

    private void ForceResetTakeDamage()
    {
        isTakingDamage = false;
        if (animator != null)
        {
            AnimatorParamAdapter.SetBool(animator, "isTakeDamage", false);
            SetDirectionalBoolExclusive("takeDamage", false);
            SetDirectionalBoolExclusive("TakeDamage", false);
        }
        if (takeDamageResetCoroutine != null)
        {
            StopCoroutine(takeDamageResetCoroutine);
            takeDamageResetCoroutine = null;
        }
    }

    public void UpdateMovementAnimation(Vector2 movement, Vector2 facing, bool running, bool crouching)
    {
        if (animator == null || isDying)
        {
            return;
        }

        // Always update facing direction (so the character looks at the mouse)
        Vector2 facingDirection = facing.sqrMagnitude > 0.0001f
            ? facing.normalized
            : Vector2.right;
        SetDirection(VectorToDirection(facingDirection));

        // --- PRIORITY GUARD ---
        // During attack, damage, or special-ability windows, do NOT overwrite
        // the movement booleans.  The animator transitions for those states
        // need their bools to stay stable until the coroutine resets them;
        // otherwise movement bools fight them and the animation flickers.
        if (isAttacking || isTakingDamage)
        {
            return;
        }

        bool forwardInput = false;
        bool backwardInput = false;
        bool leftInput = false;
        bool rightInput = false;

        if (playerController != null && playerController.HasInputAuthority)
        {
            forwardInput = playerController.ForwardInputHeld;
            backwardInput = playerController.BackwardInputHeld;
            leftInput = playerController.LeftInputHeld;
            rightInput = playerController.RightInputHeld;
        }

        bool movingFromInput = forwardInput || backwardInput || leftInput || rightInput;
        bool moving = movement.sqrMagnitude > 0.0001f || movingFromInput;

        if (!movingFromInput && moving)
        {
            Vector2 movementDirection = movement.normalized;
            Vector2 rightDirection = new Vector2(facingDirection.y, -facingDirection.x);

            float forwardAmount = Vector2.Dot(movementDirection, facingDirection);
            float rightAmount = Vector2.Dot(movementDirection, rightDirection);
            const float directionalThreshold = 0.28f;

            forwardInput = forwardAmount > directionalThreshold;
            backwardInput = forwardAmount < -directionalThreshold;
            leftInput = rightAmount < -directionalThreshold;
            rightInput = rightAmount > directionalThreshold;

            if (!forwardInput && !backwardInput && !leftInput && !rightInput)
            {
                if (Mathf.Abs(forwardAmount) >= Mathf.Abs(rightAmount))
                {
                    forwardInput = forwardAmount >= 0f;
                }
                else
                {
                    rightInput = rightAmount >= 0f;
                }
            }
        }

        if (moving && !forwardInput && !backwardInput && !leftInput && !rightInput)
        {
            forwardInput = true;
        }

        if (crouching)
        {
            forwardInput = false;
            backwardInput = false;
            leftInput = false;
            rightInput = false;
        }

        isCrouching = crouching;
        isCurrentlyRunning = moving && !crouching;
        isRunning = (forwardInput || (running && moving)) && !crouching;
        isRunningBackwards = backwardInput && !crouching;
        isStrafingLeft = leftInput && !crouching;
        isStrafingRight = rightInput && !crouching;

        bool isWalking = moving && !crouching;
        bool isCrouchRunning = moving && crouching;
        bool isCrouchIdling = !moving && crouching;

        AnimatorParamAdapter.SetBool(animator, "isWalking", isWalking);
        AnimatorParamAdapter.SetBool(animator, "isRunning", isRunning);
        AnimatorParamAdapter.SetBool(animator, "isCrouchRunning", isCrouchRunning);
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", isCrouchIdling);
        AnimatorParamAdapter.SetBool(animator, "isRunningBackwards", isRunningBackwards);
        AnimatorParamAdapter.SetBool(animator, "isStrafingLeft", isStrafingLeft);
        AnimatorParamAdapter.SetBool(animator, "isStrafingRight", isStrafingRight);

        // Feed directional variants used by this specific controller.
        // These are exclusive families: only one direction stays true.
        SetDirectionalBoolExclusive("Move", isWalking);
        SetDirectionalBoolExclusive("CrouchRun", isCrouchRunning);
        SetDirectionalBoolExclusive("CrouchIdle", isCrouchIdling);
        SetDirectionalBoolExclusive("RunBackwards", isRunningBackwards);
        SetDirectionalBoolExclusive("StrafeLeft", isStrafingLeft);
        SetDirectionalBoolExclusive("StrafeRight", isStrafingRight);
    }

    public void UpdateFacingDirection(Vector2 facing)
    {
        if (isDying || facing.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        if (animator == null)
        {
            return;
        }

        SetDirection(VectorToDirection(facing.normalized));
    }

    public void PlayRunAnimation(float snappedAngle)
    {
        if (isDying)
        {
            return;
        }

        SetDirection(AngleToDirection(snappedAngle));
        AnimatorParamAdapter.SetBool(animator, "isWalking", true);
        AnimatorParamAdapter.SetBool(animator, "isRunning", true);
        AnimatorParamAdapter.SetBool(animator, "isCrouchRunning", false);
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", false);
    }

    public void PlayIdleAnimation(float snappedAngle)
    {
        if (isDying)
        {
            return;
        }

        SetDirection(AngleToDirection(snappedAngle));
        AnimatorParamAdapter.SetBool(animator, "isWalking", false);
        AnimatorParamAdapter.SetBool(animator, "isRunning", false);
        AnimatorParamAdapter.SetBool(animator, "isCrouchRunning", false);
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", false);
    }

    public void TriggerAttackAnimation()
    {
        if (isDying || animator == null)
        {
            return;
        }

        isAttacking = true;
        attackFlagSetTime = Time.time;

        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", true);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", isCurrentlyRunning);
        SetDirectionalBoolExclusive("AttackAttack", true);
        SetDirectionalBoolExclusive("Attack2", true);

        if (attackResetCoroutine != null)
        {
            StopCoroutine(attackResetCoroutine);
        }

        attackResetCoroutine = StartCoroutine(ResetAttackAfterDelay(0.2f));
    }

    public void TriggerTakeDamageAnimation()
    {
        if (isDying || animator == null)
        {
            return;
        }

        isTakingDamage = true;
        takeDamageFlagSetTime = Time.time;
        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", true);
        AnimatorParamAdapter.SetTrigger(animator, "TakeDamage");
        SetDirectionalBoolExclusive("takeDamage", true);
        SetDirectionalBoolExclusive("TakeDamage", true);
        SpawnEffect();

        if (takeDamageResetCoroutine != null)
        {
            StopCoroutine(takeDamageResetCoroutine);
        }

        takeDamageResetCoroutine = StartCoroutine(ResetTakeDamageAfterDelay(0.18f));
    }

    public void TriggerCrouchIdleAnimation()
    {
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", true);
    }

    public void ResetCrouchIdleParameters()
    {
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", false);
    }

    public void TriggerDie()
    {
        if (animator == null || isDying)
        {
            return;
        }

        isDying = true;

        AnimatorParamAdapter.SetTrigger(animator, "isDie");
        AnimatorParamAdapter.SetTrigger(animator, "dieTrigger");
        AnimatorParamAdapter.SetTrigger(animator, "Die");
        SetDirectionalTrigger("die");
    }

    public void TriggerSpecialAbility1Animation()
    {
        TriggerSpecial("Special1", "isSpecialAbility1", "specialAbility1");
    }

    public void TriggerSpecialAbility2Animation()
    {
        TriggerSpecial("Special2", "isSpecialAbility2", "specialAbility2");
    }

    public void TriggerCastSpellAnimation()
    {
        TriggerSpecial("Cast", "isCastingSpell", "CastSpell");
    }

    public void TriggerKickAnimation()
    {
        TriggerSpecial("Kick", "isKicking", "Kick");
    }

    public void TriggerFlipAnimation()
    {
        TriggerSpecial("Flip", "isFlipping", "Flip");
    }

    public void TriggerRollAnimation()
    {
        TriggerSpecial("Roll", "isRolling", "Rolling");
        StartCoroutine(ResetRoll());
    }

    public void TriggerSlideAnimation()
    {
        TriggerSpecial("Slide", "isSliding", "Sliding");
    }

    public void TriggerPummelAnimation()
    {
        TriggerSpecial("Pummel", "isPummeling", "Pummel");
    }

    public void TriggerAttackSpinAnimation()
    {
        TriggerSpecial("Spin", "isAttackSpinning", "AttackSpin");
    }

    private void TriggerSpecial(string triggerName, string stateBoolName, string directionalPrefix)
    {
        if (animator == null || isDying)
        {
            return;
        }

        AnimatorParamAdapter.SetTrigger(animator, triggerName);
        AnimatorParamAdapter.SetBool(animator, stateBoolName, true);
        SetDirectionalBoolExclusive(directionalPrefix, true);
        StartCoroutine(ResetTemporaryBool(stateBoolName, directionalPrefix, 0.2f));
    }

    private IEnumerator ResetTemporaryBool(string stateBoolName, string directionalPrefix, float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, stateBoolName, false);
        SetDirectionalBoolExclusive(directionalPrefix, false);
    }

    private IEnumerator ResetAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", false);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", false);
        SetDirectionalBoolExclusive("AttackAttack", false);
        SetDirectionalBoolExclusive("Attack2", false);
        isAttacking = false;
        attackResetCoroutine = null;
    }

    private IEnumerator ResetTakeDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", false);
        SetDirectionalBoolExclusive("takeDamage", false);
        SetDirectionalBoolExclusive("TakeDamage", false);
        isTakingDamage = false;
        takeDamageResetCoroutine = null;
    }

    private IEnumerator ResetRoll()
    {
        yield return new WaitForSeconds(rollTime);
        AnimatorParamAdapter.SetBool(animator, "isRolling", false);
        SetDirectionalBoolExclusive("Rolling", false);
    }

    private void SpawnEffect()
    {
        List<GameObject> prefabsToUse = isRadiated ? radiatedPrefabs : hitEffectPrefabs;
        if (prefabsToUse != null && prefabsToUse.Count > 0)
        {
            GameObject selectedPrefab = prefabsToUse[Random.Range(0, prefabsToUse.Count)];
            if (selectedPrefab != null)
            {
                Instantiate(selectedPrefab, transform.position, Quaternion.identity);
                return;
            }
        }

        CleanVfxFactory.SpawnImpactSpark(transform.position);
    }

    private void SetDirection(string newDirection)
    {
        if (animator == null)
        {
            return;
        }

        if (newDirection == currentDirection)
        {
            return;
        }

        for (int i = 0; i < DirectionParameters.Length; i++)
        {
            string direction = DirectionParameters[i];
            bool isCurrentDirection = direction == newDirection;
            AnimatorParamAdapter.SetBool(animator, direction, isCurrentDirection);
        }

        currentDirection = newDirection;
    }

    private string VectorToDirection(Vector2 vector)
    {
        if (vector.sqrMagnitude <= 0.0001f)
        {
            return currentDirection;
        }

        float angle = Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;
        return AngleToDirection(angle);
    }

    private static string AngleToDirection(float angle)
    {
        float normalizedAngle = (angle + 360f) % 360f;

        if (normalizedAngle >= 337.5f || normalizedAngle < 22.5f) return "isEast";
        if (normalizedAngle < 67.5f) return "isNorthEast";
        if (normalizedAngle < 112.5f) return "isNorth";
        if (normalizedAngle < 157.5f) return "isNorthWest";
        if (normalizedAngle < 202.5f) return "isWest";
        if (normalizedAngle < 247.5f) return "isSouthWest";
        if (normalizedAngle < 292.5f) return "isSouth";
        return "isSouthEast";
    }

    private void SetDirectionalBoolExclusive(string prefix, bool value)
    {
        if (animator == null || string.IsNullOrEmpty(prefix))
        {
            return;
        }

        for (int i = 0; i < DirectionParameters.Length; i++)
        {
            string direction = DirectionParameters[i];
            bool isCurrent = value && direction == currentDirection;
            string parameterName = prefix + DirectionSuffix(direction);
            AnimatorParamAdapter.SetBool(animator, parameterName, isCurrent);
        }
    }

    private void SetDirectionalTrigger(string prefix)
    {
        string suffix = DirectionSuffix(currentDirection);
        string parameterName = prefix + suffix;
        AnimatorParamAdapter.SetTrigger(animator, parameterName);
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
}
