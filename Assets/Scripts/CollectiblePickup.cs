using UnityEngine;

public class CollectiblePickup : MonoBehaviour
{
    [SerializeField] private int collectibleAmount = 1;
    [SerializeField] private int scorePerCollectible = 25;
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField] private GameObject pickupVfxPrefab;
    [SerializeField] private bool addIdleLoopVfx = true;
    [SerializeField] private Vector3 idleLoopOffset = new Vector3(0f, 0.18f, 0f);
    [SerializeField] private float idleLoopScale = 0.8f;
    [SerializeField] private bool destroyOnPickup = true;
    private bool collected;
    private Collider2D triggerCollider;
    private GameObject idleLoopVfxInstance;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        if (pickupVfxPrefab != null || !addIdleLoopVfx)
        {
            return;
        }

        idleLoopVfxInstance = CleanVfxFactory.AttachPickupIdleLoop(
            transform,
            idleLoopOffset,
            idleLoopScale);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (collected)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerController>();
        }

        if (player == null)
        {
            return;
        }

        collected = true;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if (idleLoopVfxInstance != null)
        {
            Destroy(idleLoopVfxInstance);
            idleLoopVfxInstance = null;
        }

        player.AddCollectible(collectibleAmount, scorePerCollectible);

        if (pickupVfxPrefab != null)
        {
            Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            CleanVfxFactory.SpawnPickupBurst(transform.position + idleLoopOffset * 0.5f);
        }

        if (pickupSfx != null)
        {
            AudioManager.Instance?.PlayCustomSfx(pickupSfx);
        }
        else
        {
            AudioManager.Instance?.PlayPickupSfx();
        }

        if (destroyOnPickup)
        {
            Destroy(gameObject);
        }
    }
}
