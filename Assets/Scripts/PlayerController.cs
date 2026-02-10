using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;
using System.Collections.Generic;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class PlayerController : MonoBehaviour
    {
        public AnimationController animationController;
        private CircleCollider2D circleCollider;
        public float speed = 1.0f;
        private Rigidbody2D rb;
        private Vector2 movementDirection;
        private bool isOnStairs = false;
        public bool isCrouching = false;
        private SpriteRenderer spriteRenderer;
        private float lastAngle;
        private bool isRunning = false;
        private Color originalColor;

        private AudioSource gunfireAudioSource;

        // Archer specifics
        public bool isActive;
        public bool isRanged;
        public bool isStealth;
        public bool isShapeShifter;
        public bool isSummoner;

        public GameObject projectilePrefab;
        public GameObject AoEPrefab;
        public GameObject Special1Prefab;
        public GameObject HookPrefab;
        public GameObject ShapeShiftPrefab;

        public float projectileSpeed = 10.0f;
        public float shootDelay = 0f;

        [Header("Projectile visual size")]
        [Range(0.05f, 2f)]
        public float projectileScaleMultiplier = 0.25f;

        // Melee specifics
        public bool isMelee;
        public GameObject meleePrefab;

        [Header("Damage Settings")]
        public float bulletDamage = 1f;
        public float bulletsPerSecond = 3f;
        private float nextFireTime = 0f;

        [Header("Line Renderer / Bullet Trace")]
        public GameObject bulletLinePrefab;
        public float lineDisplayTime = 0.05f;

        [Header("Shot Origin Offsets")]
        public float muzzleForwardOffset = 0.5f;
        public float muzzleUpOffset = 0.2f;

        // -------------------- Health & UI --------------------
        public int maxHealth = 100;
        public int currentHealth;
        public bool isDead = false;
        public Slider healthSlider;
        public GameObject gameOver;

        // --- Score / Kill Count ---
        public int zombieKillCount = 0;
        public TextMeshProUGUI killCountText;

        private Coroutine pulseCoroutine;
        private Vector3 originalScale;

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            animationController = GetComponent<AnimationController>();
            circleCollider = GetComponent<CircleCollider2D>();

            if (spriteRenderer != null) originalColor = spriteRenderer.color;

            currentHealth = maxHealth;
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }

            gunfireAudioSource = GetComponent<AudioSource>();

            if (killCountText != null)
            {
                originalScale = killCountText.transform.localScale;
                killCountText.text = zombieKillCount.ToString();
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.O))
            {
                SceneManager.LoadScene("Level1");
                return;
            }
            if (zombieKillCount > 30)
            {
                SceneManager.LoadScene("Example scene 2");
            }
            if (isDead) return;
            if (Camera.main == null) return;

            Vector2 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 directionToMouse = (mousePosition - (Vector2)transform.position).normalized;

            float angle = Mathf.Atan2(directionToMouse.y, directionToMouse.x) * Mathf.Rad2Deg;
            lastAngle = SnapAngleToEightDirections(angle);

            movementDirection = new Vector2(Mathf.Cos(lastAngle * Mathf.Deg2Rad), Mathf.Sin(lastAngle * Mathf.Deg2Rad));

            HandleMovement();
            HandleZombieDamage();
            HandleShooting();

            // IMPORTANT:
            // Your raycast gun damage works regardless of isActive/isRanged,
            // but your orb projectile WAS blocked behind isActive/isRanged.
            // This makes the projectile always spawn on RMB down if assigned.
            if (projectilePrefab != null && Input.GetMouseButtonDown(1))
            {
                DelayedShoot(); // fire instantly (no delay/cooldown)
            }

            bool isMoving = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
                            Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D);

            if (isMoving && !isRunning) isRunning = true;
            else if (!isMoving && isRunning) isRunning = false;

            if (Input.GetKeyDown(KeyCode.C))
            {
                if (isShapeShifter && isActive)
                {
                    StartCoroutine(ShapeShiftDelayed());
                }
                HandleCrouching();
            }

            // keep your special ability logic gated exactly as before
            if (isActive)
            {
                if (isRanged)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1))
                    {
                        StartCoroutine(DeploySpecial1Delayed());
                    }
                    if (Input.GetKeyDown(KeyCode.Alpha3))
                    {
                        StartCoroutine(DeployAoEDelayed());
                    }
                    if (Input.GetKeyDown(KeyCode.Alpha5))
                    {
                        if (isSummoner) StartCoroutine(DeployHookDelayed());
                        else StartCoroutine(Quickshot());
                    }
                    if (Input.GetKeyDown(KeyCode.Alpha6))
                    {
                        StartCoroutine(CircleShot());
                    }
                }

                if (isMelee)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1))
                    {
                        StartCoroutine(DeployAoEDelayed());
                    }
                    if (Input.GetKeyDown(KeyCode.Alpha5))
                    {
                        StartCoroutine(DeployHookDelayed());
                    }
                    if (Input.GetKeyDown(KeyCode.Alpha6))
                    {
                        Invoke(nameof(DelayedShoot), shootDelay);
                    }
                }
                else if (Input.GetKeyDown(KeyCode.LeftControl) && isRunning)
                {
                    if (isShapeShifter && isActive)
                    {
                        StartCoroutine(ShapeShiftDelayed());
                    }
                }
            }
        }

        void FixedUpdate()
        {
            if (movementDirection != Vector2.zero)
            {
                rb.MovePosition(rb.position + movementDirection * speed * Time.fixedDeltaTime);
            }
        }

        private void HandleShooting()
        {
            if (Input.GetMouseButtonDown(1))
            {
                PlayGunfireSound();
            }
        }

        private void PlayGunfireSound()
        {
            // left as your original (commented out in your old script)
        }

        private IEnumerator StopGunfireSoundAfterDelay()
        {
            yield return new WaitForSeconds(0.25f);
            if (!Input.GetMouseButton(1))
            {
                if (gunfireAudioSource != null) gunfireAudioSource.Stop();
            }
        }

        public void TakeDamage(int damageAmount)
        {
            if (isDead) return;

            currentHealth -= damageAmount;

            if (healthSlider != null)
            {
                healthSlider.value = currentHealth;
            }

            if (currentHealth <= 0) Die();
            else if (animationController != null) animationController.TriggerTakeDamageAnimation();
        }

        private void Die()
        {
            isDead = true;

            if (circleCollider != null) circleCollider.enabled = false;
            if (animationController != null) animationController.TriggerDie();

            Rigidbody2D r = GetComponent<Rigidbody2D>();
            if (r != null) r.bodyType = RigidbodyType2D.Static;

            if (gameOver != null) gameOver.SetActive(true);
            StartCoroutine(RestartSceneAfterDelay(3f));
        }

        private IEnumerator RestartSceneAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void IncrementZombieKillCount()
        {
            zombieKillCount++;
            if (killCountText != null)
            {
                killCountText.text = zombieKillCount.ToString();
                if (pulseCoroutine != null) StopCoroutine(pulseCoroutine);
                pulseCoroutine = StartCoroutine(PulseTextEffect(killCountText));
            }
        }

        private IEnumerator PulseTextEffect(TextMeshProUGUI text)
        {
            float duration = 0.2f;
            float maxScaleFactor = 1.5f;
            float time = 0f;

            Vector3 maxScale = originalScale * maxScaleFactor;

            while (time < duration / 2)
            {
                text.transform.localScale = Vector3.Lerp(text.transform.localScale, maxScale, time / (duration / 2));
                time += Time.deltaTime;
                yield return null;
            }
            text.transform.localScale = maxScale;
            time = 0f;

            while (time < duration / 2)
            {
                text.transform.localScale = Vector3.Lerp(text.transform.localScale, originalScale, time / (duration / 2));
                time += Time.deltaTime;
                yield return null;
            }

            text.transform.localScale = originalScale;
            pulseCoroutine = null;
        }

        private void HandleZombieDamage()
        {
            if (Input.GetMouseButton(1))
            {
                speed = 0.5f;

                Vector2 playerPos = transform.position;
                Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector2 direction = (mousePos - playerPos).normalized;
                Vector2 muzzleOrigin = playerPos;
                float maxDistance = 10f;

                Vector2 rayOrigin = muzzleOrigin;
                bool shouldContinue = true;
                List<Vector2> hitPoints = new List<Vector2> { muzzleOrigin };

                bool prev = Physics2D.queriesHitTriggers;
                Physics2D.queriesHitTriggers = true;

                while (shouldContinue)
                {
                    RaycastHit2D hit = Physics2D.Raycast(rayOrigin, direction, maxDistance);

                    if (hit.collider != null)
                    {
                        hitPoints.Add(hit.point);

                        ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                        if (zombie == null) zombie = hit.collider.GetComponentInParent<ZombieAI>();

                        if (zombie != null)
                        {
                            zombie.TakeDamage((int)bulletDamage);

                            if (Random.value > 0.5f)
                                rayOrigin = hit.point + direction * 0.1f;
                            else
                                shouldContinue = false;
                        }
                        else
                        {
                            shouldContinue = false;
                        }
                    }
                    else
                    {
                        hitPoints.Add(rayOrigin + direction * maxDistance);
                        shouldContinue = false;
                    }
                }

                Physics2D.queriesHitTriggers = prev;

                if (bulletLinePrefab != null)
                    StartCoroutine(ShowShotLine(hitPoints));
            }
            else
            {
                speed = 1.0f;
            }
        }

        private IEnumerator ShowShotLine(List<Vector2> hitPoints)
        {
            GameObject lineObj = Instantiate(bulletLinePrefab, Vector3.zero, Quaternion.identity);
            LineRenderer lr = lineObj.GetComponent<LineRenderer>();

            if (lr != null)
            {
                lr.positionCount = hitPoints.Count;
                for (int i = 0; i < hitPoints.Count; i++)
                {
                    lr.SetPosition(i, hitPoints[i]);
                }
            }

            yield return new WaitForSeconds(lineDisplayTime);
            Destroy(lineObj);
        }

        float SnapAngleToEightDirections(float angle)
        {
            angle = (angle + 360) % 360;

            if (isOnStairs)
            {
                if (angle < 30 || angle >= 330) return 0;
                else if (angle >= 30 && angle < 75) return 60;
                else if (angle >= 75 && angle < 105) return 90;
                else if (angle >= 105 && angle < 150) return 120;
                else if (angle >= 150 && angle < 210) return 180;
                else if (angle >= 210 && angle < 255) return 240;
                else if (angle >= 255 && angle < 285) return 270;
                else if (angle >= 285 && angle < 330) return 300;
            }
            else
            {
                if (angle < 15 || angle >= 345) return 0;
                else if (angle >= 15 && angle < 75) return 30;
                else if (angle >= 75 && angle < 105) return 90;
                else if (angle >= 105 && angle < 165) return 150;
                else if (angle >= 165 && angle < 195) return 180;
                else if (angle >= 195 && angle < 255) return 210;
                else if (angle >= 255 && angle < 285) return 270;
                else if (angle >= 285 && angle < 345) return 330;
            }

            return 0;
        }

        float GetPerpendicularAngle(float angle, bool isLeft)
        {
            float perpendicularAngle = isLeft ? angle - 90 : angle + 90;
            perpendicularAngle = (perpendicularAngle + 360) % 360;
            return SnapAngleToEightDirections(perpendicularAngle);
        }

        void HandleMovement()
        {
            if (Input.GetKey(KeyCode.W))
            {
                return;
            }
            else if (!isCrouching)
            {
                if (Input.GetKey(KeyCode.S))
                {
                    movementDirection = -movementDirection;
                }
                else if (Input.GetKey(KeyCode.A))
                {
                    float leftAngle = GetPerpendicularAngle(Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg, true);
                    movementDirection = new Vector2(Mathf.Cos(leftAngle * Mathf.Deg2Rad), Mathf.Sin(leftAngle * Mathf.Deg2Rad));
                }
                else if (Input.GetKey(KeyCode.D))
                {
                    float rightAngle = GetPerpendicularAngle(Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg, false);
                    movementDirection = new Vector2(Mathf.Cos(rightAngle * Mathf.Deg2Rad), Mathf.Sin(rightAngle * Mathf.Deg2Rad));
                }
                else
                {
                    movementDirection = Vector2.zero;
                }
            }
            else
            {
                movementDirection = Vector2.zero;
            }
        }

        void HandleCrouching()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                isCrouching = !isCrouching;

                if (isCrouching && isStealth && spriteRenderer != null)
                {
                    spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                }
                else
                {
                    if (spriteRenderer != null) spriteRenderer.color = Color.white;
                }
            }
        }

        void DelayedShoot()
        {
            Vector2 fireDirection = new Vector2(Mathf.Cos(lastAngle * Mathf.Deg2Rad), Mathf.Sin(lastAngle * Mathf.Deg2Rad));
            ShootProjectile(fireDirection);
        }

        void ShootProjectile(Vector2 direction)
        {
            if (projectilePrefab == null) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            Vector3 spawnPos = transform.position + (Vector3)(direction.normalized * 0.35f);

            GameObject projectileInstance = Instantiate(projectilePrefab, spawnPos, Quaternion.Euler(0, 0, angle));

            // force it visible (debug-safe)
            var sr = projectileInstance.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = true;
                sr.sortingOrder = Mathf.Max(sr.sortingOrder, 5000);
                sr.color = Color.white;
            }

            // apply size here (since your prefab has scale 1)
            projectileInstance.transform.localScale = Vector3.one * projectileScaleMultiplier * 0.05f;

            // ignore collision with player
            Collider2D pc = GetComponent<Collider2D>();
            Collider2D pr = projectileInstance.GetComponent<Collider2D>();
            if (pc != null && pr != null) Physics2D.IgnoreCollision(pr, pc, true);

            Rigidbody2D rbProjectile = projectileInstance.GetComponent<Rigidbody2D>();
            if (rbProjectile != null) rbProjectile.velocity = direction.normalized * projectileSpeed;

            Destroy(projectileInstance, 1.5f);
        }

        IEnumerator Quickshot()
        {
            yield return new WaitForSeconds(0.1f);

            for (int i = 0; i < 5; i++)
            {
                Vector2 fireDirection = new Vector2(Mathf.Cos(lastAngle * Mathf.Deg2Rad), Mathf.Sin(lastAngle * Mathf.Deg2Rad));
                ShootProjectile(fireDirection);
                yield return new WaitForSeconds(0.18f);
            }
        }

        IEnumerator CircleShot()
        {
            float initialDelay = 0.1f;
            float timeBetweenShots = 0.9f / 8;

            yield return new WaitForSeconds(initialDelay);

            for (int i = 0; i < 8; i++)
            {
                float a = lastAngle + i * 45;
                float rad = Mathf.Deg2Rad * a;
                Vector2 fireDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                ShootProjectile(fireDirection);

                yield return new WaitForSeconds(timeBetweenShots);
            }
        }

        IEnumerator DeployAoEDelayed()
        {
            if (AoEPrefab != null)
            {
                GameObject aoeInstance;

                if (isSummoner)
                {
                    Vector3 mouseScreenPosition = Input.mousePosition;
                    mouseScreenPosition.z = Camera.main.nearClipPlane;
                    Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

                    yield return new WaitForSeconds(0.3f);
                    aoeInstance = Instantiate(AoEPrefab, new Vector3(mouseWorldPosition.x, mouseWorldPosition.y, 0), Quaternion.identity);
                    Destroy(aoeInstance, 8.7f);
                }
                else
                {
                    if (isMelee) yield return new WaitForSeconds(0.5f);
                    else if (isShapeShifter) yield return new WaitForSeconds(0.2f);
                    else yield return new WaitForSeconds(0.3f);

                    aoeInstance = Instantiate(AoEPrefab, transform.position, Quaternion.identity);
                    Destroy(aoeInstance, 0.9f);
                }
            }
        }

        IEnumerator ShapeShiftDelayed()
        {
            if (ShapeShiftPrefab != null)
            {
                yield return new WaitForSeconds(0.001f);
                GameObject shapeShiftInstance = Instantiate(ShapeShiftPrefab, transform.position, Quaternion.identity);
                Destroy(shapeShiftInstance, 0.9f);
            }
        }

        IEnumerator DeploySpecial1Delayed()
        {
            if (Special1Prefab != null)
            {
                GameObject Special1PrefabInstance;

                if (isSummoner)
                {
                    Vector3 mouseScreenPosition = Input.mousePosition;
                    mouseScreenPosition.z = Camera.main.nearClipPlane;
                    Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

                    yield return new WaitForSeconds(0.6f);
                    Special1PrefabInstance = Instantiate(Special1Prefab, new Vector3(mouseWorldPosition.x, mouseWorldPosition.y, 0), Quaternion.identity);
                }
                else
                {
                    if (isMelee) yield return new WaitForSeconds(0.5f);
                    else yield return new WaitForSeconds(0.6f);

                    Special1PrefabInstance = Instantiate(Special1Prefab, transform.position, Quaternion.identity);
                }

                Destroy(Special1PrefabInstance, 1.0f);
            }
        }

        IEnumerator DeployHookDelayed()
        {
            GameObject hookInstance;

            if (isSummoner)
            {
                Vector3 mouseScreenPosition = Input.mousePosition;
                mouseScreenPosition.z = Camera.main.nearClipPlane;
                Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);

                yield return new WaitForSeconds(0.6f);
                hookInstance = Instantiate(HookPrefab, new Vector3(mouseWorldPosition.x, mouseWorldPosition.y, 0), Quaternion.identity);
                Destroy(hookInstance, 5.2f);
            }
            else
            {
                if (HookPrefab != null)
                {
                    Vector2 direction = new Vector2(Mathf.Cos(lastAngle * Mathf.Deg2Rad), Mathf.Sin(lastAngle * Mathf.Deg2Rad));
                    float a = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    hookInstance = Instantiate(HookPrefab, transform.position, Quaternion.Euler(0, 0, a));
                    Destroy(hookInstance, 1.0f);
                }
                yield return null;
            }
        }

        public void FlashGreen()
        {
            StartCoroutine(FlashEffect());
        }

        private IEnumerator FlashEffect()
        {
            if (spriteRenderer == null) yield break;
            spriteRenderer.color = Color.green;
            yield return new WaitForSeconds(0.7f);
            spriteRenderer.color = originalColor;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.tag == "Stairs")
            {
                isOnStairs = true;
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.tag == "Stairs")
            {
                isOnStairs = false;
            }
        }
    }
}