using UnityEngine;
using UnityEngine.Serialization;

public class EnergyPulseProjectile : MonoBehaviour
{
    [Header("Runtime Stats")]
    public float speed = 10f;
    public int damage = 5;
    public float lifetime = 1.5f;

    [Header("Visual")]
    public float visualScale = 1f;
    public bool applyVisualScale;
    public int sortingOrder = 350;
    [FormerlySerializedAs("hitVfx")]
    public GameObject impactVfxPrefab;
    public AudioClip impactSfx;

    [Header("Collision")]
    public LayerMask obstacleLayers;
    [SerializeField] private string projectileLayerName = "Projectile";
    [SerializeField] private string obstacleLayerName = "Obstacles";
    [SerializeField] private string gateLayerName = "Gate";

    private Rigidbody2D rb;
    private Collider2D projectileCollider;
    private GameObject owner;
    private bool launched;
    private bool impacted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        projectileCollider = GetComponent<Collider2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        int projectileLayer = LayerMask.NameToLayer(projectileLayerName);
        if (projectileLayer >= 0)
        {
            gameObject.layer = projectileLayer;
        }

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, sortingOrder);
        }

        if (projectileCollider != null && !projectileCollider.isTrigger)
        {
            projectileCollider.isTrigger = true;
        }

        if (obstacleLayers.value == 0)
        {
            int obstacleLayer = LayerMask.NameToLayer(obstacleLayerName);
            int gateLayer = LayerMask.NameToLayer(gateLayerName);
            int mask = 0;
            if (obstacleLayer >= 0) mask |= 1 << obstacleLayer;
            if (gateLayer >= 0) mask |= 1 << gateLayer;
            obstacleLayers = mask;
        }
    }

    private void Start()
    {
        if (applyVisualScale && visualScale > 0f)
        {
            transform.localScale *= visualScale;
        }

        Destroy(gameObject, lifetime);
    }

    private void FixedUpdate()
    {
        if (impacted || rb == null)
        {
            return;
        }

        if (!launched && rb.velocity.sqrMagnitude < 0.01f)
        {
            rb.velocity = transform.right * speed;
            launched = true;
        }
    }

    public void Configure(GameObject owner, float speedValue, int damageValue, float lifetimeValue)
    {
        this.owner = owner;
        speed = speedValue;
        damage = damageValue;
        lifetime = lifetimeValue;
    }

    public void Launch(Vector2 direction)
    {
        if (rb == null)
        {
            return;
        }

        Vector2 normalizedDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        rb.velocity = normalizedDirection * speed;
        launched = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHit(collision.collider);
    }

    private void TryHit(Collider2D other)
    {
        if (impacted || other == null)
        {
            return;
        }

        if (owner != null && other.transform.root == owner.transform.root)
        {
            return;
        }

        ZombieAI zombie = other.GetComponent<ZombieAI>();
        if (zombie == null)
        {
            zombie = other.GetComponentInParent<ZombieAI>();
        }

        if (zombie != null && !zombie.isDead)
        {
            zombie.TakeDamage(damage);
            Impact();
            return;
        }

        bool hitObstacleLayer =
            obstacleLayers.value != 0 &&
            (obstacleLayers.value & (1 << other.gameObject.layer)) != 0;

        if (hitObstacleLayer)
        {
            Impact();
            return;
        }

        if (!other.isTrigger)
        {
            Impact();
        }
    }

    private void Impact()
    {
        impacted = true;

        if (impactVfxPrefab != null)
        {
            Instantiate(impactVfxPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            CleanVfxFactory.SpawnImpactSpark(transform.position);
        }

        if (impactSfx != null)
        {
            AudioManager.Instance?.PlayCustomSfx(impactSfx);
        }
        else
        {
            AudioManager.Instance?.PlayHitSparkSfx();
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        if (projectileCollider != null)
        {
            projectileCollider.enabled = false;
        }

        Destroy(gameObject);
    }
}
