using UnityEngine;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class EnergyPulseProjectile : MonoBehaviour
    {
        public float speed = 10f;
        public int damage = 5;
        public float lifetime = 1.5f;

        [Header("visual")]
        public float visualScale = 1f;
        public int sortingOrder = 50;

        public GameObject hitVfx;

        private Rigidbody2D rb;
        private Collider2D myCol;
        private bool launched;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            myCol = GetComponent<Collider2D>();

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = sortingOrder;
        }

        private void Start()
        {
            // apply scale ONCE (no more double-scaling)
            if (visualScale > 0f) transform.localScale = Vector3.one * visualScale;

            Destroy(gameObject, lifetime);
        }

        public void Launch(Vector2 dir)
        {
            launched = true;
            if (rb != null) rb.velocity = dir.normalized * speed;
        }

        private void FixedUpdate()
        {
            // if PlayerController didn't call Launch, still move forward
            if (!launched && rb != null && rb.velocity.sqrMagnitude < 0.01f)
            {
                rb.velocity = transform.right * speed;
                launched = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        private void OnCollisionEnter2D(Collision2D col) => TryHit(col.collider);

        private void TryHit(Collider2D other)
        {
            if (other == null) return;

            // ignore player
            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerController>() != null)
                return;

            ZombieAI z = other.GetComponent<ZombieAI>();
            if (z == null) z = other.GetComponentInParent<ZombieAI>();

            if (z != null)
            {
                z.TakeDamage(damage);
                SpawnHit();
                Destroy(gameObject);
                return;
            }

            if (!other.isTrigger)
            {
                SpawnHit();
                Destroy(gameObject);
            }
        }

        private void SpawnHit()
        {
            if (hitVfx != null) Instantiate(hitVfx, transform.position, Quaternion.identity);
        }
    }
}