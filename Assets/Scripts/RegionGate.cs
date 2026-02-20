using UnityEngine;

public class RegionGate : MonoBehaviour
{
    [SerializeField] private Collider2D primaryCollider;
    [SerializeField] private Collider2D[] additionalColliders;
    [SerializeField] private GameObject[] lockedVisuals;
    [SerializeField] private GameObject[] unlockedVisuals;
    [SerializeField] private bool lockedAtStart = true;
    [SerializeField] private bool playToggleVfx = true;
    [SerializeField] private Vector3 toggleVfxOffset = new Vector3(0f, 0.2f, 0f);

    public bool IsLocked { get; private set; }
    private bool hasInitializedState;

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
        bool stateChanged = !hasInitializedState || IsLocked != locked;
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

        if (playToggleVfx && hasInitializedState && stateChanged)
        {
            CleanVfxFactory.SpawnGateToggle(transform.position + toggleVfxOffset, locked);
        }

        hasInitializedState = true;
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
