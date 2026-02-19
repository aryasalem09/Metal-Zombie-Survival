using System.Collections.Generic;
using UnityEngine;

public class CollisionLayerBootstrap : MonoBehaviour
{
    [Header("Layer Names")]
    public string playerLayer = "Player";
    public string enemyLayer = "Enemy";
    public string projectileLayer = "Projectile";
    public string obstacleLayer = "Obstacles";
    public string gateLayer = "Gate";
    public string interactableLayer = "Interactable";
    public string regionBoundsLayer = "RegionBounds";
    public bool autoAssignRegionBoundsLayer = true;

    private static readonly HashSet<string> MissingLayerWarnings = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeAtStartup()
    {
        ConfigureCollisionMatrix(
            playerLayer: "Player",
            enemyLayer: "Enemy",
            projectileLayer: "Projectile",
            obstacleLayer: "Obstacles",
            gateLayer: "Gate",
            interactableLayer: "Interactable",
            regionBoundsLayer: "RegionBounds");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AssignRegionBoundsLayerAtStartup()
    {
        AssignLikelyBoundaryObjectsToRegionBounds("RegionBounds");
    }

    private void Awake()
    {
        if (autoAssignRegionBoundsLayer)
        {
            AssignLikelyBoundaryObjectsToRegionBounds(regionBoundsLayer);
        }

        ConfigureCollisionMatrix(
            playerLayer,
            enemyLayer,
            projectileLayer,
            obstacleLayer,
            gateLayer,
            interactableLayer,
            regionBoundsLayer);
    }

    public static void ConfigureCollisionMatrix(
        string playerLayer,
        string enemyLayer,
        string projectileLayer,
        string obstacleLayer,
        string gateLayer,
        string interactableLayer,
        string regionBoundsLayer)
    {
        SetLayerCollision(playerLayer, projectileLayer, false);
        SetLayerCollision(projectileLayer, projectileLayer, false);
        SetLayerCollision(enemyLayer, projectileLayer, true);
        SetLayerCollision(projectileLayer, obstacleLayer, true);
        SetLayerCollision(projectileLayer, gateLayer, true);
        SetLayerCollision(projectileLayer, interactableLayer, false);
        SetLayerCollision(projectileLayer, regionBoundsLayer, false);

        SetLayerCollision(playerLayer, enemyLayer, true);
        SetLayerCollision(playerLayer, obstacleLayer, true);
        SetLayerCollision(enemyLayer, obstacleLayer, true);
        SetLayerCollision(playerLayer, gateLayer, true);
        SetLayerCollision(enemyLayer, gateLayer, true);
        SetLayerCollision(playerLayer, regionBoundsLayer, true);
        SetLayerCollision(enemyLayer, regionBoundsLayer, false);
        SetLayerCollision(playerLayer, interactableLayer, true);
        SetLayerCollision(enemyLayer, interactableLayer, false);
    }

    private static void SetLayerCollision(string layerAName, string layerBName, bool shouldCollide)
    {
        int layerA = LayerMask.NameToLayer(layerAName);
        int layerB = LayerMask.NameToLayer(layerBName);
        if (layerA < 0 || layerB < 0)
        {
            WarnLayerMissing(layerAName);
            WarnLayerMissing(layerBName);
            return;
        }

        Physics2D.IgnoreLayerCollision(layerA, layerB, !shouldCollide);
    }

    private static void WarnLayerMissing(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName) || !MissingLayerWarnings.Add(layerName))
        {
            return;
        }

        Debug.LogWarning(
            $"CollisionLayerBootstrap: Layer '{layerName}' is missing. Add it in Project Settings > Tags and Layers.");
    }

    private static void AssignLikelyBoundaryObjectsToRegionBounds(string regionBoundsLayerName)
    {
        if (string.IsNullOrWhiteSpace(regionBoundsLayerName))
        {
            return;
        }

        int regionBoundsLayer = LayerMask.NameToLayer(regionBoundsLayerName);
        if (regionBoundsLayer < 0)
        {
            WarnLayerMissing(regionBoundsLayerName);
            return;
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !IsLikelyBoundaryObject(candidate))
            {
                continue;
            }

            SetLayerRecursively(candidate, regionBoundsLayer);
        }
    }

    private static bool IsLikelyBoundaryObject(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (string.Equals(candidate.name, "ColliderGrid", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(candidate.name, "Collider", System.StringComparison.OrdinalIgnoreCase) &&
               candidate.parent != null &&
               string.Equals(candidate.parent.name, "ColliderGrid", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        if (root == null)
        {
            return;
        }

        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }
}
