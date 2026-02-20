using UnityEngine;
public class ChestInteraction : MonoBehaviour
{
    [SerializeField] private GameObject openVfxPrefab;
    [SerializeField] private bool addIdleLoopVfx = true;
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0.32f, 0f);
    [SerializeField] private float idleLoopScale = 0.9f;

    private Animator animator;
    private bool isOpened = false;
    private PlayerController playerController;
    private GameObject idleLoopVfxInstance;

    void Start()
    {
        playerController = FindObjectOfType<PlayerController>();
        animator = GetComponent<Animator>();

        if (openVfxPrefab == null && addIdleLoopVfx)
        {
            idleLoopVfxInstance = CleanVfxFactory.AttachPickupIdleLoop(
                transform,
                vfxOffset,
                idleLoopScale);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isOpened)
        {
            OpenChest();
        }
    }

    private void OpenChest()
    {
        animator.SetTrigger("Open");
        isOpened = true;

        if (idleLoopVfxInstance != null)
        {
            Destroy(idleLoopVfxInstance);
            idleLoopVfxInstance = null;
        }

        if (openVfxPrefab != null)
        {
            Instantiate(openVfxPrefab, transform.position + vfxOffset, Quaternion.identity);
        }
        else
        {
            CleanVfxFactory.SpawnChestOpenBurst(transform.position + vfxOffset);
        }

        if (playerController != null)
        {
            playerController.currentHealth = playerController.maxHealth;
            if (playerController.healthSlider != null)
                playerController.healthSlider.value = playerController.currentHealth;

            playerController.FlashGreen();
        }
        else
        {
            Debug.LogWarning("PlayerController not found in the scene!");
        }
    }
}
