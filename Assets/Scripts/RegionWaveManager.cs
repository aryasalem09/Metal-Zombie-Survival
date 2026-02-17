using System;
using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

[Serializable]
public class RegionWaveDefinition
{
    [Min(1)] public int enemyCount = 8;
    [Min(0.1f)] public float spawnRate = 2f;
    [Range(0f, 1f)] public float runnerChance = 0.15f;
    [Range(0f, 1f)] public float tankChance = 0.08f;
    [Range(0f, 1f)] public float spitterChance;
    [Min(0.1f)] public float healthMultiplier = 1f;
    [Min(0.1f)] public float speedMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
}

[Serializable]
public class RegionDefinition
{
    public string regionName = "Region";
    public Collider2D regionBounds;
    public Collider2D confinerBounds;
    public Transform playerSpawnPoint;
    public RegionGate[] gatesToLock;
    public List<RegionWaveDefinition> waves = new List<RegionWaveDefinition>();
    public bool isBossRegion;
}

public class RegionWaveManager : MonoBehaviour
{
    private enum MutantVariant
    {
        Normal,
        Runner,
        Tank,
        Spitter,
        Boss
    }

    [Header("References")]
    public PlayerController player;
    public ZombieAI zombiePrefab;
    public Transform zombieContainer;

    [Header("Region Sequence")]
    public List<RegionDefinition> regions = new List<RegionDefinition>();
    public bool autoStart = true;
    public float regionTransitionDelay = 0.75f;
    public float waveIntermissionSeconds = 1.0f;

    [Header("Spawning")]
    public int maxAliveAtOnce = 12;
    public float minSpawnDistanceFromPlayer = 5.0f;
    public float offscreenSpawnMargin = 1.5f;

    [Header("Cinemachine")]
    public CinemachineVirtualCamera virtualCamera;
    public CinemachineConfiner2D confiner2D;

    [Header("Debug")]
    [SerializeField] private int currentRegionIndex = -1;
    [SerializeField] private int aliveEnemies;
    [SerializeField] private int regionKillCount;
    [SerializeField] private int regionTargetKillCount;
    [SerializeField] private bool regionRunning;

    public event Action<int, RegionDefinition> RegionStarted;
    public event Action<int, RegionDefinition> RegionCompleted;
    public event Action<int, int> RegionProgressUpdated;
    public event Action AllRegionsCompleted;

    private readonly List<ZombieAI> activeEnemies = new List<ZombieAI>();
    private Coroutine regionRoutine;

    public int CurrentRegionIndex => currentRegionIndex;
    public bool IsRegionRunning => regionRunning;

    private void Start()
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (autoStart)
        {
            StartRegionSequence();
        }
    }

    public void StartRegionSequence()
    {
        if (regionRoutine != null)
        {
            StopCoroutine(regionRoutine);
        }

        if (regions == null || regions.Count == 0)
        {
            Debug.LogWarning("RegionWaveManager has no regions configured.");
            return;
        }

        regionRoutine = StartCoroutine(RunRegionSequence());
    }

    private IEnumerator RunRegionSequence()
    {
        CleanupActiveEnemies();

        for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
        {
            RegionDefinition region = regions[regionIndex];
            currentRegionIndex = regionIndex;
            regionRunning = true;
            aliveEnemies = 0;
            regionKillCount = 0;
            regionTargetKillCount = CountRegionTargets(region);
            RegionProgressUpdated?.Invoke(regionKillCount, regionTargetKillCount);

            PrepareRegion(region);
            RegionStarted?.Invoke(regionIndex, region);

            yield return new WaitForSeconds(regionTransitionDelay);
            yield return RunRegionWaves(region);

            UnlockRegionGates(region);
            regionRunning = false;
            RegionCompleted?.Invoke(regionIndex, region);
        }

        currentRegionIndex = -1;
        AllRegionsCompleted?.Invoke();
        regionRoutine = null;
    }

    private IEnumerator RunRegionWaves(RegionDefinition region)
    {
        if (region.waves == null || region.waves.Count == 0)
        {
            region.waves = new List<RegionWaveDefinition> { new RegionWaveDefinition() };
        }

        for (int waveIndex = 0; waveIndex < region.waves.Count; waveIndex++)
        {
            RegionWaveDefinition wave = region.waves[waveIndex];
            int totalToSpawn = Mathf.Max(1, wave.enemyCount);

            for (int i = 0; i < totalToSpawn; i++)
            {
                while (maxAliveAtOnce > 0 && aliveEnemies >= maxAliveAtOnce)
                {
                    yield return null;
                }

                SpawnZombie(region, wave, waveIndex, i == totalToSpawn - 1 && region.isBossRegion);
                yield return new WaitForSeconds(1f / Mathf.Max(0.1f, wave.spawnRate));
            }

            while (aliveEnemies > 0)
            {
                yield return null;
            }

            if (waveIndex < region.waves.Count - 1)
            {
                yield return new WaitForSeconds(waveIntermissionSeconds);
            }
        }
    }

    private void SpawnZombie(
        RegionDefinition region,
        RegionWaveDefinition wave,
        int waveIndex,
        bool forceBossVariant)
    {
        if (zombiePrefab == null)
        {
            Debug.LogWarning("RegionWaveManager cannot spawn because zombiePrefab is missing.");
            return;
        }

        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        Vector2 spawnPosition = FindSpawnPosition(region);
        ZombieAI zombie = Instantiate(
            zombiePrefab,
            spawnPosition,
            Quaternion.identity,
            zombieContainer != null ? zombieContainer : null);

        zombie.player = player != null ? player.transform : null;
        zombie.playerController = player;

        MutantVariant variant = forceBossVariant
            ? MutantVariant.Boss
            : RollMutantVariant(wave, waveIndex);

        ApplyWaveStats(zombie, wave, variant, region.isBossRegion && forceBossVariant);

        zombie.Died -= OnZombieDied;
        zombie.Died += OnZombieDied;

        aliveEnemies++;
        activeEnemies.Add(zombie);
    }

    private void ApplyWaveStats(
        ZombieAI zombie,
        RegionWaveDefinition wave,
        MutantVariant variant,
        bool isBoss)
    {
        float healthMultiplier = wave.healthMultiplier;
        float speedMultiplier = wave.speedMultiplier;
        float damageMultiplier = wave.damageMultiplier;
        float scaleMultiplier = 1f;
        Color tint = Color.white;
        bool setRunner = false;

        switch (variant)
        {
            case MutantVariant.Runner:
                healthMultiplier *= 0.75f;
                speedMultiplier *= 1.65f;
                damageMultiplier *= 1.05f;
                scaleMultiplier *= 0.9f;
                tint = new Color(1f, 0.95f, 0.65f, 1f);
                setRunner = true;
                break;
            case MutantVariant.Tank:
                healthMultiplier *= 2.6f;
                speedMultiplier *= 0.7f;
                damageMultiplier *= 1.55f;
                scaleMultiplier *= 1.35f;
                tint = new Color(0.76f, 0.88f, 1f, 1f);
                break;
            case MutantVariant.Spitter:
                healthMultiplier *= 1.2f;
                speedMultiplier *= 0.8f;
                damageMultiplier *= 1.15f;
                scaleMultiplier *= 1.05f;
                tint = new Color(0.68f, 1f, 0.8f, 1f);
                zombie.attackRange *= 1.7f;
                zombie.attackCooldown *= 1.25f;
                zombie.detectionRadius *= 1.2f;
                break;
            case MutantVariant.Boss:
                healthMultiplier *= 6f;
                speedMultiplier *= 1.05f;
                damageMultiplier *= 2.3f;
                scaleMultiplier *= 1.7f;
                tint = new Color(0.9f, 0.6f, 1f, 1f);
                break;
        }

        if (isBoss)
        {
            healthMultiplier *= 1.35f;
            damageMultiplier *= 1.15f;
        }

        zombie.ApplyMutantModifiers(
            healthMultiplier: healthMultiplier,
            speedMultiplier: speedMultiplier,
            damageMultiplier: damageMultiplier,
            scaleMultiplier: scaleMultiplier,
            tint: tint,
            runner: setRunner);
    }

    private MutantVariant RollMutantVariant(RegionWaveDefinition wave, int waveIndex)
    {
        float runnerChance = Mathf.Clamp01(wave.runnerChance + waveIndex * 0.03f);
        float tankChance = Mathf.Clamp01(wave.tankChance + waveIndex * 0.02f);
        float spitterChance = Mathf.Clamp01(wave.spitterChance + waveIndex * 0.015f);

        float roll = UnityEngine.Random.value;
        if (roll < runnerChance) return MutantVariant.Runner;
        if (roll < runnerChance + tankChance) return MutantVariant.Tank;
        if (roll < runnerChance + tankChance + spitterChance) return MutantVariant.Spitter;
        return MutantVariant.Normal;
    }

    private Vector2 FindSpawnPosition(RegionDefinition region)
    {
        Camera cameraRef = Camera.main;
        Bounds fallbackBounds = new Bounds(transform.position, new Vector3(30f, 20f, 1f));
        Collider2D boundsCollider = region.regionBounds;
        Bounds regionBounds = boundsCollider != null ? boundsCollider.bounds : fallbackBounds;

        if (TryFindEdgeSpawnPosition(boundsCollider, regionBounds, cameraRef, out Vector2 edgeSpawn))
        {
            return edgeSpawn;
        }

        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector2 candidate = new Vector2(
                UnityEngine.Random.Range(regionBounds.min.x, regionBounds.max.x),
                UnityEngine.Random.Range(regionBounds.min.y, regionBounds.max.y));

            if (boundsCollider != null && !boundsCollider.OverlapPoint(candidate))
            {
                continue;
            }

            if (player != null &&
                Vector2.Distance(candidate, player.transform.position) < minSpawnDistanceFromPlayer)
            {
                continue;
            }

            if (cameraRef != null && IsInsideCamera(cameraRef, candidate, offscreenSpawnMargin))
            {
                continue;
            }

            return candidate;
        }

        Vector2 fallback = regionBounds.center;
        if (player != null)
        {
            Vector2 awayFromPlayer =
                ((Vector2)regionBounds.center - (Vector2)player.transform.position).normalized;
            if (awayFromPlayer.sqrMagnitude < 0.0001f)
            {
                awayFromPlayer = UnityEngine.Random.insideUnitCircle.normalized;
            }

            fallback = (Vector2)regionBounds.center + awayFromPlayer * Mathf.Max(minSpawnDistanceFromPlayer, 4f);
        }

        return fallback;
    }

    private bool TryFindEdgeSpawnPosition(
        Collider2D boundsCollider,
        Bounds regionBounds,
        Camera cameraRef,
        out Vector2 spawnPoint)
    {
        for (int attempt = 0; attempt < 80; attempt++)
        {
            int side = UnityEngine.Random.Range(0, 4);
            float t = UnityEngine.Random.Range(0f, 1f);
            Vector2 candidate;

            switch (side)
            {
                case 0:
                    candidate = new Vector2(regionBounds.min.x + 0.1f, Mathf.Lerp(regionBounds.min.y, regionBounds.max.y, t));
                    break;
                case 1:
                    candidate = new Vector2(regionBounds.max.x - 0.1f, Mathf.Lerp(regionBounds.min.y, regionBounds.max.y, t));
                    break;
                case 2:
                    candidate = new Vector2(Mathf.Lerp(regionBounds.min.x, regionBounds.max.x, t), regionBounds.min.y + 0.1f);
                    break;
                default:
                    candidate = new Vector2(Mathf.Lerp(regionBounds.min.x, regionBounds.max.x, t), regionBounds.max.y - 0.1f);
                    break;
            }

            if (boundsCollider != null && !boundsCollider.OverlapPoint(candidate))
            {
                candidate = Vector2.Lerp(candidate, regionBounds.center, 0.07f);
                if (!boundsCollider.OverlapPoint(candidate))
                {
                    continue;
                }
            }

            if (player != null &&
                Vector2.Distance(candidate, player.transform.position) < minSpawnDistanceFromPlayer)
            {
                continue;
            }

            if (cameraRef != null && IsInsideCamera(cameraRef, candidate, offscreenSpawnMargin))
            {
                continue;
            }

            spawnPoint = candidate;
            return true;
        }

        spawnPoint = Vector2.zero;
        return false;
    }

    private static bool IsInsideCamera(Camera cameraRef, Vector2 worldPosition, float margin)
    {
        Vector3 bottomLeft = cameraRef.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 topRight = cameraRef.ViewportToWorldPoint(new Vector3(1f, 1f, 0f));

        return worldPosition.x > bottomLeft.x + margin &&
               worldPosition.x < topRight.x - margin &&
               worldPosition.y > bottomLeft.y + margin &&
               worldPosition.y < topRight.y - margin;
    }

    private void PrepareRegion(RegionDefinition region)
    {
        if (player == null)
        {
            player = PlayerController.FindPrimary();
        }

        if (player != null && region.playerSpawnPoint != null)
        {
            Vector3 previousPlayerPosition = player.transform.position;
            player.transform.position = region.playerSpawnPoint.position;

            if (virtualCamera != null)
            {
                virtualCamera.Follow = player.transform;
                virtualCamera.LookAt = player.transform;
                virtualCamera.OnTargetObjectWarped(
                    player.transform,
                    player.transform.position - previousPlayerPosition);
            }
        }

        if (confiner2D != null && region.confinerBounds != null)
        {
            confiner2D.m_BoundingShape2D = region.confinerBounds;
            confiner2D.InvalidateCache();
        }

        LockRegionGates(region);
    }

    private static void LockRegionGates(RegionDefinition region)
    {
        if (region.gatesToLock == null)
        {
            return;
        }

        for (int i = 0; i < region.gatesToLock.Length; i++)
        {
            RegionGate gate = region.gatesToLock[i];
            if (gate != null)
            {
                gate.SetLocked(true);
            }
        }
    }

    private static void UnlockRegionGates(RegionDefinition region)
    {
        if (region.gatesToLock == null)
        {
            return;
        }

        for (int i = 0; i < region.gatesToLock.Length; i++)
        {
            RegionGate gate = region.gatesToLock[i];
            if (gate != null)
            {
                gate.SetLocked(false);
            }
        }
    }

    private static int CountRegionTargets(RegionDefinition region)
    {
        if (region.waves == null || region.waves.Count == 0)
        {
            return 1;
        }

        int count = 0;
        for (int i = 0; i < region.waves.Count; i++)
        {
            count += Mathf.Max(1, region.waves[i].enemyCount);
        }

        return count;
    }

    private void OnZombieDied(ZombieAI zombie)
    {
        aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
        regionKillCount++;
        RegionProgressUpdated?.Invoke(regionKillCount, regionTargetKillCount);

        if (zombie != null)
        {
            zombie.Died -= OnZombieDied;
            activeEnemies.Remove(zombie);
        }
    }

    private void CleanupActiveEnemies()
    {
        for (int i = activeEnemies.Count - 1; i >= 0; i--)
        {
            ZombieAI zombie = activeEnemies[i];
            if (zombie != null)
            {
                zombie.Died -= OnZombieDied;
                Destroy(zombie.gameObject);
            }
        }

        activeEnemies.Clear();
        aliveEnemies = 0;
    }
}
