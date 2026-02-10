using UnityEngine;
using SmallScaleInc.TopDownPixelCharactersPack1;

namespace SmallScaleInc.TopDownPixelCharactersPack1
{
    public class ChestInteraction : MonoBehaviour
    {
        private Animator animator;
        private bool isOpened = false;
        private PlayerController playerController;

        void Start()
        {
            playerController = FindObjectOfType<PlayerController>();
            animator = GetComponent<Animator>();
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
}