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

    private void Awake()
    {
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
}