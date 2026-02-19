using UnityEngine;

public class CollectiblePickup : MonoBehaviour
{
    [SerializeField] private int collectibleAmount = 1;
    [SerializeField] private int scorePerCollectible = 25;
    [SerializeField] private AudioClip pickupSfx;
    [SerializeField] private GameObject pickupVfxPrefab;
    [SerializeField] private bool destroyOnPickup = true;
    private bool collected;
    private Collider2D triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
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

        player.AddCollectible(collectibleAmount, scorePerCollectible);

        if (pickupVfxPrefab != null)
        {
            Instantiate(pickupVfxPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            CleanVfxFactory.SpawnImpactSpark(transform.position);
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
