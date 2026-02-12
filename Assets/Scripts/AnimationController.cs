using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class AnimationController : MonoBehaviour
    {
        private Animator animator;
        public Animator muzzleAnimator;
        public SpriteRenderer muzzleFlashRenderer;

        public string currentDirection = "isEast";
        public bool isCurrentlyRunning;
        public bool isCrouching = false;
        public bool isDying = false;
        private PlayerController playerController;

        // no blood: these are just hit effects now (sparks/poofs)
        [SerializeField] private List<GameObject> hitEffectPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> radiatedPrefabs = new List<GameObject>();
        public bool isRadiated = false;

        public float rollTime = 0.5f;

        void Start()
        {
            animator = GetComponent<Animator>();
            playerController = GetComponent<PlayerController>();

            if (playerController == null)
            {
                Debug.LogError("PlayerController script not found on the same GameObject!");
            }

            animator.SetBool("isEast", true);
            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isCrouchRunning", false);
            animator.SetBool("isCrouchIdling", false);
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy) return;
            if (isDying) return;

            HandleAttackAttack();
            HandleMovement();

            // misc input tests left as-is
            if (Input.GetKeyDown(KeyCode.C))
            {
                if (!isCrouching)
                {
                    TriggerCrouchIdleAnimation();
                    isCrouching = true;
                }
                else
                {
                    isCrouching = false;
                    ResetCrouchIdleParameters();
                }
            }
            else if (Input.GetKey(KeyCode.Alpha1)) TriggerTakeDamageAnimation();
            else if (Input.GetKey(KeyCode.Alpha2)) TriggerSpecialAbility2Animation();
            else if (Input.GetKey(KeyCode.Alpha3)) TriggerCastSpellAnimation();
            else if (Input.GetKey(KeyCode.Alpha4)) TriggerKickAnimation();
            else if (Input.GetKey(KeyCode.Alpha5)) TriggerPummelAnimation();
            else if (Input.GetKey(KeyCode.Alpha6)) TriggerAttackSpinAnimation();
            else if (Input.GetKey(KeyCode.Alpha7)) TriggerDie();
            else if (Input.GetKey(KeyCode.LeftShift) && isCurrentlyRunning) TriggerFlipAnimation();
            else if (Input.GetKey(KeyCode.LeftControl) && isCurrentlyRunning) TriggerRollAnimation();
            else if (Input.GetKey(KeyCode.LeftAlt) && isCurrentlyRunning) TriggerSlideAnimation();
        }

        // ----------------------------
        // added: methods PlayerController expects
        // ----------------------------
        public void PlayRunAnimation(float snappedAngle)
        {
            if (animator == null) return;

            string dir = AngleToDir(snappedAngle);
            UpdateDirection(dir);

            animator.SetBool("isWalking", true);
            animator.SetBool("isRunning", true);
            animator.SetBool("isCrouchRunning", false);
            animator.SetBool("isCrouchIdling", false);
        }

        public void PlayIdleAnimation(float snappedAngle)
        {
            if (animator == null) return;

            string dir = AngleToDir(snappedAngle);
            UpdateDirection(dir);

            animator.SetBool("isWalking", false);
            animator.SetBool("isRunning", false);
            animator.SetBool("isCrouchRunning", false);
            animator.SetBool("isCrouchIdling", false);
        }

        private string AngleToDir(float a)
        {
            a = (a + 360f) % 360f;

            if (a >= 337.5f || a < 22.5f) return "isEast";
            if (a >= 22.5f && a < 67.5f) return "isNorthEast";
            if (a >= 67.5f && a < 112.5f) return "isNorth";
            if (a >= 112.5f && a < 157.5f) return "isNorthWest";
            if (a >= 157.5f && a < 202.5f) return "isWest";
            if (a >= 202.5f && a < 247.5f) return "isSouthWest";
            if (a >= 247.5f && a < 292.5f) return "isSouth";
            return "isSouthEast";
        }

        // ----------------------------
        // original logic below (unchanged except effect list + Random)
        // ----------------------------
        void UpdateDirection(string newDirection)
        {
            string[] directions = { "isWest", "isEast", "isSouth", "isSouthWest", "isNorthEast", "isSouthEast", "isNorth", "isNorthWest" };

            foreach (string direction in directions)
            {
                animator.SetBool(direction, direction == newDirection);
            }

            if (currentDirection != newDirection)
            {
                isAttacking = false;
                ResetAttackAttackParameters();
            }

            currentDirection = newDirection;
        }

        public bool isRunning;
        public bool isRunningBackwards;
        public bool isStrafingLeft;
        public bool isStrafingRight;
        public bool isAttacking = false;

        void HandleMovement()
        {
            // keep your original input-based movement anim logic
            Vector3 mouseScreenPosition = Input.mousePosition;
            mouseScreenPosition.z = Camera.main.transform.position.z - transform.position.z;
            Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
            Vector3 directionToMouse = mouseWorldPosition - transform.position;
            directionToMouse.Normalize();

            float angle = Mathf.Atan2(directionToMouse.y, directionToMouse.x) * Mathf.Rad2Deg;
            angle = (angle + 360) % 360;

            string newDir = "isEast";
            if (angle >= 337.5f || angle < 22.5f) newDir = "isEast";
            else if (angle >= 22.5f && angle < 67.5f) newDir = "isNorthEast";
            else if (angle >= 67.5f && angle < 112.5f) newDir = "isNorth";
            else if (angle >= 112.5f && angle < 157.5f) newDir = "isNorthWest";
            else if (angle >= 157.5f && angle < 202.5f) newDir = "isWest";
            else if (angle >= 202.5f && angle < 247.5f) newDir = "isSouthWest";
            else if (angle >= 247.5f && angle < 292.5f) newDir = "isSouth";
            else newDir = "isSouthEast";

            UpdateDirection(newDir);

            bool moving = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);

            animator.SetBool("isWalking", moving);
            animator.SetBool("isRunning", moving && Input.GetKey(KeyCode.LeftShift));
            isCurrentlyRunning = animator.GetBool("isRunning");
        }

        void HandleAttackAttack()
        {
            // keep whatever you already had here in the asset
            if (Input.GetMouseButtonDown(0))
            {
                isAttacking = true;
                animator.SetBool("isAttacking", true);
                StartCoroutine(ResetAttackAfterDelay());
            }
        }

        IEnumerator ResetAttackAfterDelay()
        {
            yield return new WaitForSeconds(0.1f);
            ResetAttackAttackParameters();
        }

        void ResetAttackAttackParameters()
        {
            if (animator == null) return;
            animator.SetBool("isAttacking", false);
            isAttacking = false;
        }

        public void TriggerTakeDamageAnimation()
        {
            if (!gameObject.activeInHierarchy) return;
            if (isDying) return;

            animator.SetTrigger("TakeDamage");
            SpawnEffect();
        }

        private void SpawnEffect()
        {
            List<GameObject> prefabsToUse = isRadiated ? radiatedPrefabs : hitEffectPrefabs;

            if (prefabsToUse == null || prefabsToUse.Count == 0) return;

            GameObject selectedPrefab = prefabsToUse[UnityEngine.Random.Range(0, prefabsToUse.Count)];
            if (selectedPrefab == null) return;

            GameObject effectInstance = Instantiate(selectedPrefab, transform.position, Quaternion.identity);
            StartCoroutine(UpdateSpriteOrder(effectInstance));
        }

        private IEnumerator UpdateSpriteOrder(GameObject effectInstance)
        {
            if (effectInstance == null) yield break;
            yield return new WaitForSeconds(0.5f);

            SpriteRenderer r = effectInstance.GetComponent<SpriteRenderer>();
            if (r != null) r.sortingOrder = 3;
        }

        public void TriggerCrouchIdleAnimation() { animator.SetBool("isCrouchIdling", true); }
        public void ResetCrouchIdleParameters() { animator.SetBool("isCrouchIdling", false); }
        public void TriggerDie() { isDying = true; animator.SetTrigger("Die"); }
        public void TriggerSpecialAbility1Animation() { animator.SetTrigger("Special1"); }
        public void TriggerSpecialAbility2Animation() { animator.SetTrigger("Special2"); }
        public void TriggerCastSpellAnimation() { animator.SetTrigger("Cast"); }
        public void TriggerKickAnimation() { animator.SetTrigger("Kick"); }
        public void TriggerFlipAnimation() { animator.SetTrigger("Flip"); }
        public void TriggerRollAnimation() { animator.SetTrigger("Roll"); StartCoroutine(ResetRoll()); }
        public void TriggerSlideAnimation() { animator.SetTrigger("Slide"); }
        public void TriggerPummelAnimation() { animator.SetTrigger("Pummel"); }
        public void TriggerAttackSpinAnimation() { animator.SetTrigger("Spin"); }

        private IEnumerator ResetRoll()
        {
            yield return new WaitForSeconds(rollTime);
        }
    }
}