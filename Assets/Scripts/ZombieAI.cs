using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class ZombieAI : MonoBehaviour
    {
        public event Action<ZombieAI> Died;

        [Header("References")]
        public Transform player;
        public PlayerController playerController;
        public SpriteRenderer spriteRenderer;
        public Animator animator;

        private CapsuleCollider2D capsuleCollider;
        private Collider2D[] allColliders;
        private Color originalColor;

        [Header("Settings")]
        public float detectionRadius = 5f;
        public float moveSpeed = 2f;
        public bool isRunner;

        [Header("Alert Settings")]
        public float alertedDetectionRadius = 15f;
        public float alertDuration = 3f;
        private float baseDetectionRadius;

        [Header("Attack")]
        public float attackRange = 1f;
        public float attackCooldown = 1f;
        public int zombieDamage = 1;
        private float nextAttackTime;

        [Header("Health")]
        public int maxHealth = 10;
        public int currentHealth;
        public bool isDead;

        [Header("VFX (no blood)")]
        [SerializeField] private List<GameObject> hitEffectPrefabs = new List<GameObject>();
        [SerializeField] private List<GameObject> radiatedPrefabs = new List<GameObject>();
        public bool isRadiated = false;

        [Header("cleanup")]
        public float destroyAfterDeathSeconds = 3f;

        private Coroutine resetRadiusCo;

        private void Start()
        {
            capsuleCollider = GetComponent<CapsuleCollider2D>();
            allColliders = GetComponents<Collider2D>();

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<Animator>();

            originalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

            currentHealth = maxHealth;

            // store base radius, but don't allow it to be tiny (your prefab had 2 before)
            baseDetectionRadius = detectionRadius;
            if (baseDetectionRadius < 5f) baseDetectionRadius = 12f;

            AcquirePlayer();
        }

        private void AcquirePlayer()
        {
            if (player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }

            if (player == null)
            {
                PlayerController pc = FindObjectOfType<PlayerController>();
                if (pc != null) player = pc.transform;
            }

            if (playerController == null && player != null)
            {
                playerController = player.GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            if (isDead) return;

            // if we ever lose player reference, reacquire it
            if (player == null) AcquirePlayer();
            if (player == null) return;

            float d = Vector2.Distance(transform.position, player.position);

            if (d <= attackRange) TryAttack();
            else if (d <= detectionRadius) MoveTowardsPlayer();
        }

        private void MoveTowardsPlayer()
        {
            float spd = isRunner ? moveSpeed * 1.35f : moveSpeed;
            transform.position = Vector2.MoveTowards(transform.position, player.position, spd * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime) return;
            nextAttackTime = Time.time + attackCooldown;

            if (animator != null) animator.SetTrigger("Attack");

            if (playerController != null)
            {
                playerController.TakeDamage(zombieDamage);
            }
        }

        public void TakeDamage(int dmg)
        {
            if (isDead) return;

            currentHealth -= dmg;

            if (currentHealth <= 0)
            {
                Die();
                return;
            }

            // alert: temporarily increase detection, but DON'T let it snap back to tiny range
            detectionRadius = alertedDetectionRadius;

            if (resetRadiusCo != null) StopCoroutine(resetRadiusCo);
            resetRadiusCo = StartCoroutine(ResetDetectionRadius());

            StartCoroutine(FlashRed());
            if (animator != null) animator.SetTrigger("TakeDamage");

            SpawnEffect();
        }

        private IEnumerator ResetDetectionRadius()
        {
            yield return new WaitForSeconds(alertDuration);

            // back to base (clamped), so they keep chasing normally
            detectionRadius = baseDetectionRadius;
            resetRadiusCo = null;
        }

        private IEnumerator FlashRed()
        {
            if (spriteRenderer == null) yield break;
            spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.2f);
            spriteRenderer.color = originalColor;
        }

        private void Die()
        {
            isDead = true;

            if (allColliders != null)
            {
                for (int i = 0; i < allColliders.Length; i++)
                    allColliders[i].enabled = false;
            }

            if (animator != null) animator.SetTrigger("Die");

            Died?.Invoke(this);

            if (playerController != null)
                playerController.IncrementZombieKillCount();

            Destroy(gameObject, destroyAfterDeathSeconds);
        }

        private void SpawnEffect()
        {
            List<GameObject> prefabsToUse = isRadiated ? radiatedPrefabs : hitEffectPrefabs;
            if (prefabsToUse == null || prefabsToUse.Count == 0) return;

            GameObject selectedPrefab = prefabsToUse[UnityEngine.Random.Range(0, prefabsToUse.Count)];
            if (selectedPrefab == null) return;

            Instantiate(selectedPrefab, transform.position, Quaternion.identity);
        }
    }
}