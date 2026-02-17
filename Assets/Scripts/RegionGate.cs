using UnityEngine;

public class RegionGate : MonoBehaviour
{
    [SerializeField] private Collider2D primaryCollider;
    [SerializeField] private Collider2D[] additionalColliders;
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private GameObject[] unlockedVisuals;
    [SerializeField] private bool lockedAtStart = true;

    public bool IsLocked { get; private set; }

    private void Awake()
    {
        if (primaryCollider == null)
        {
            primaryCollider = GetComponent<Collider2D>();
        }

        SetLocked(lockedAtStart);
    }

    public void SetLocked(bool locked)
    {
        IsLocked = locked;

        if (primaryCollider != null)
        {
            primaryCollider.enabled = locked;
        }

        if (additionalColliders != null)
        {
            for (int i = 0; i < additionalColliders.Length; i++)
            {
                Collider2D extraCollider = additionalColliders[i];
                if (extraCollider != null)
                {
                    extraCollider.enabled = locked;
                }
            }
        }

        ToggleVisuals(lockedVisuals, locked);
        ToggleVisuals(unlockedVisuals, !locked);
    }

    private static void ToggleVisuals(GameObject[] visuals, bool state)
    {
        if (visuals == null)
        {
            return;
        }

        for (int i = 0; i < visuals.Length; i++)
        {
            if (visuals[i] != null)
            {
                visuals[i].SetActive(state);
            }
        }
    }
}