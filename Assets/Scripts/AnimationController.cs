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

    private void Update()
    {
        if (!gameObject.activeInHierarchy || isDying)
        {
            return;
        }

        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
            if (playerController == null)
            {
                playerController = GetComponentInParent<PlayerController>();
            }

            if (playerController == null)
            {
                return;
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (animator == null)
            {
                return;
            }
        }

        Vector2 movement = playerController.MovementDirection;
        if (movement.sqrMagnitude <= 0.0001f && playerBody != null && playerBody.velocity.sqrMagnitude > 0.0004f)
        {
            movement = playerBody.velocity;
        }

        Vector2 facing = playerController.LookDirection;
        bool isMoving = movement.sqrMagnitude > 0.0001f;
        bool running = playerController.IsRunning;

        UpdateMovementAnimation(movement, facing, running, playerController.isCrouching);
    }

    public void UpdateMovementAnimation(Vector2 movement, Vector2 facing, bool running, bool crouching)
    {
        if (animator == null || isDying)
        {
            return;
        }

        bool moving = movement.sqrMagnitude > 0.0001f;

        // Use movement direction for locomotion states so walk/run/crouch
        // always match world motion, then fall back to facing while idle.
        Vector2 directionSource = moving
            ? movement.normalized
            : (facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.right);

        SetDirection(VectorToDirection(directionSource));

        isCurrentlyRunning = moving && running;
        isCrouching = crouching;

        // Keep locomotion deterministic for this controller; avoid toggling
        // complex strafe/backward branches that can block base movement states.
        isRunningBackwards = false;
        isStrafingLeft = false;
        isStrafingRight = false;

        bool isCrouchRunning = moving && crouching;
        bool isRunMoving = moving && running && !crouching;

        // The controller expects "isWalking" for many movement transitions,
        // including paths that then branch to run/crouch directional states.
        AnimatorParamAdapter.SetBool(animator, "isWalking", moving);
        AnimatorParamAdapter.SetBool(animator, "isRunning", isRunMoving);
        AnimatorParamAdapter.SetBool(animator, "isCrouchRunning", isCrouchRunning);
        AnimatorParamAdapter.SetBool(animator, "isCrouchIdling", !moving && crouching);
        AnimatorParamAdapter.SetBool(animator, "isRunningBackwards", isRunningBackwards);
        AnimatorParamAdapter.SetBool(animator, "isStrafingLeft", isStrafingLeft);
        AnimatorParamAdapter.SetBool(animator, "isStrafingRight", isStrafingRight);

        // Feed directional variants used by this specific controller.
        SetDirectionalBool("Run", isRunMoving && !isRunningBackwards && !isStrafingLeft && !isStrafingRight);
        SetDirectionalBool("CrouchRun", isCrouchRunning);
        SetDirectionalBool("CrouchIdle", !moving && crouching);
        SetDirectionalBool("RunBackwards", isRunningBackwards);
        SetDirectionalBool("StrafeLeft", isStrafingLeft);
        SetDirectionalBool("StrafeRight", isStrafingRight);
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

        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", true);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", isCurrentlyRunning);
        SetDirectionalBool("AttackAttack", true);
        SetDirectionalBool("Attack2", true);

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

        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", true);
        AnimatorParamAdapter.SetTrigger(animator, "TakeDamage");
        SetDirectionalBool("takeDamage", true);
        SetDirectionalBool("TakeDamage", true);
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
        SetDirectionalBool(directionalPrefix, true);
        StartCoroutine(ResetTemporaryBool(stateBoolName, directionalPrefix, 0.2f));
    }

    private IEnumerator ResetTemporaryBool(string stateBoolName, string directionalPrefix, float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, stateBoolName, false);
        SetDirectionalBool(directionalPrefix, false);
    }

    private IEnumerator ResetAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isAttackAttacking", false);
        AnimatorParamAdapter.SetBool(animator, "isAttackRunning", false);
        SetDirectionalBool("AttackAttack", false);
        SetDirectionalBool("Attack2", false);
        isAttacking = false;
        attackResetCoroutine = null;
    }

    private IEnumerator ResetTakeDamageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        AnimatorParamAdapter.SetBool(animator, "isTakeDamage", false);
        SetDirectionalBool("takeDamage", false);
        SetDirectionalBool("TakeDamage", false);
        takeDamageResetCoroutine = null;
    }

    private IEnumerator ResetRoll()
    {
        yield return new WaitForSeconds(rollTime);
        AnimatorParamAdapter.SetBool(animator, "isRolling", false);
        SetDirectionalBool("Rolling", false);
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

        for (int i = 0; i < DirectionParameters.Length; i++)
        {
            string direction = DirectionParameters[i];
            bool isCurrentDirection = direction == newDirection;
            AnimatorParamAdapter.SetBool(animator, direction, isCurrentDirection);
            AnimatorParamAdapter.SetBool(animator, "Move" + DirectionSuffix(direction), isCurrentDirection);
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
