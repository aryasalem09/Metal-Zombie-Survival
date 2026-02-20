using System.Collections.Generic;
using UnityEngine;

public static class CleanVfxFactory
{
    private const int MaxPoolPerEffect = 24;
    private const string ImportedResourcesRoot = "ImportedParticles/";
    private const string ImportedAbilityBurst = "AbilityBurst";
    private const string ImportedProjectileMuzzle = "ProjectileMuzzle";
    private const string ImportedPickupBurst = "PickupBurst";
    private const string ImportedPickupIdleLoop = "PickupIdleLoop";
    private const string ImportedChestOpen = "ChestOpen";
    private const string ImportedGateLock = "GateLock";
    private const string ImportedGateUnlock = "GateUnlock";
    private const string ImportedEnemySpawnPoof = "EnemySpawnPoof";
    private const float ImportedOneShotScaleMultiplier = 0.78f;
    private const float ImportedOneShotLifetimeMultiplier = 0.8f;
    private const float ImportedIdleLoopScaleMultiplier = 0.72f;
    private const int BurstSortingOrder = 13;

    private sealed class FactoryHost : MonoBehaviour
    {
        private void Update()
        {
            Tick(Time.unscaledTime);
        }
    }

    private sealed class PooledBurst
    {
        public string key;
        public GameObject gameObject;
        public ParticleSystem particleSystem;
        public float releaseAt;
    }

    private static FactoryHost host;
    private static readonly Dictionary<string, Queue<PooledBurst>> Pool = new Dictionary<string, Queue<PooledBurst>>();
    private static readonly List<PooledBurst> Active = new List<PooledBurst>();
    private static readonly Dictionary<string, GameObject> ImportedPrefabCache = new Dictionary<string, GameObject>();

    public static void SpawnImpactSpark(Vector3 position)
    {
        SpawnBurst(
            name: "ImpactSparkVFX",
            position: position,
            startColor: new Color(0.58f, 0.92f, 1f, 0.92f),
            startSize: 0.055f,
            startSpeed: 1.7f,
            lifetime: 0.18f,
            particleCount: 9);
    }

    public static void SpawnAbilityBurstGlow(Vector3 position)
    {
        if (SpawnImportedOneShot(ImportedAbilityBurst, position, Quaternion.identity, 0.76f, 1.35f))
        {
            return;
        }

        SpawnBurst(
            name: "AbilityBurstGlowVFX",
            position: position,
            startColor: new Color(0.3f, 0.95f, 1f, 0.9f),
            startSize: 0.12f,
            startSpeed: 2.05f,
            lifetime: 0.28f,
            particleCount: 12);
    }

    public static void SpawnZombieDeathPoof(Vector3 position)
    {
        SpawnBurst(
            name: "ZombieDeathPoofVFX",
            position: position,
            startColor: new Color(0.68f, 0.84f, 0.95f, 0.86f),
            startSize: 0.09f,
            startSpeed: 1.05f,
            lifetime: 0.32f,
            particleCount: 12);
    }

    public static void SpawnProjectileMuzzleFlash(Vector3 position, Vector2 direction)
    {
        float zAngle = direction.sqrMagnitude > 0.0001f
            ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg
            : 0f;
        Quaternion rotation = Quaternion.Euler(0f, 0f, zAngle);

        if (SpawnImportedOneShot(ImportedProjectileMuzzle, position, rotation, 0.55f, 0.9f))
        {
            return;
        }

        SpawnBurst(
            name: "ProjectileMuzzleFlashVFX",
            position: position,
            startColor: new Color(0.45f, 0.98f, 1f, 0.9f),
            startSize: 0.045f,
            startSpeed: 0.95f,
            lifetime: 0.1f,
            particleCount: 6);
    }

    public static void SpawnPickupBurst(Vector3 position)
    {
        if (SpawnImportedOneShot(ImportedPickupBurst, position, Quaternion.identity, 0.68f, 1.25f))
        {
            return;
        }

        SpawnBurst(
            name: "PickupBurstVFX",
            position: position,
            startColor: new Color(1f, 0.95f, 0.5f, 0.9f),
            startSize: 0.07f,
            startSpeed: 1.2f,
            lifetime: 0.22f,
            particleCount: 8);
    }

    public static GameObject AttachPickupIdleLoop(Transform parent, Vector3 localOffset, float scale = 1f)
    {
        if (parent == null)
        {
            return null;
        }

        GameObject prefab = LoadImportedPrefab(ImportedPickupIdleLoop);
        if (prefab == null)
        {
            return null;
        }

        GameObject instance = Object.Instantiate(prefab, parent);
        instance.name = "PickupIdleLoopVFX";
        instance.transform.localPosition = localOffset;
        instance.transform.localRotation = Quaternion.identity;
        float adjustedScale = Mathf.Max(0.01f, scale) * ImportedIdleLoopScaleMultiplier;
        instance.transform.localScale *= adjustedScale;

        return instance;
    }

    public static void SpawnChestOpenBurst(Vector3 position)
    {
        if (SpawnImportedOneShot(ImportedChestOpen, position, Quaternion.identity, 0.75f, 1.2f))
        {
            return;
        }

        SpawnBurst(
            name: "ChestOpenBurstVFX",
            position: position,
            startColor: new Color(0.5f, 1f, 0.65f, 0.9f),
            startSize: 0.085f,
            startSpeed: 1.25f,
            lifetime: 0.24f,
            particleCount: 12);
    }

    public static void SpawnGateToggle(Vector3 position, bool locked)
    {
        string key = locked ? ImportedGateLock : ImportedGateUnlock;
        if (SpawnImportedOneShot(key, position, Quaternion.identity, 0.72f, 1.15f))
        {
            return;
        }

        SpawnBurst(
            name: locked ? "GateLockVFX" : "GateUnlockVFX",
            position: position,
            startColor: locked ? new Color(0.35f, 0.8f, 1f, 0.9f) : new Color(1f, 0.94f, 0.4f, 0.9f),
            startSize: 0.075f,
            startSpeed: 1.05f,
            lifetime: 0.2f,
            particleCount: 10);
    }

    public static void SpawnEnemySpawnPoof(Vector3 position)
    {
        if (SpawnImportedOneShot(ImportedEnemySpawnPoof, position, Quaternion.identity, 0.75f, 1.2f))
        {
            return;
        }

        SpawnBurst(
            name: "EnemySpawnPoofVFX",
            position: position,
            startColor: new Color(0.75f, 0.75f, 0.82f, 0.86f),
            startSize: 0.08f,
            startSpeed: 0.9f,
            lifetime: 0.24f,
            particleCount: 10);
    }

    private static bool SpawnImportedOneShot(
        string resourceName,
        Vector3 position,
        Quaternion rotation,
        float scaleMultiplier,
        float fallbackLifetime)
    {
        GameObject prefab = LoadImportedPrefab(resourceName);
        if (prefab == null)
        {
            return false;
        }

        GameObject instance = Object.Instantiate(prefab, position, rotation);
        float adjustedScale = Mathf.Max(0.01f, scaleMultiplier) * ImportedOneShotScaleMultiplier;
        instance.transform.localScale *= adjustedScale;

        float lifetime = Mathf.Max(0.35f, fallbackLifetime * ImportedOneShotLifetimeMultiplier);
        Object.Destroy(instance, lifetime);
        return true;
    }

    private static GameObject LoadImportedPrefab(string resourceName)
    {
        if (string.IsNullOrWhiteSpace(resourceName))
        {
            return null;
        }

        if (ImportedPrefabCache.TryGetValue(resourceName, out GameObject cached))
        {
            return cached;
        }

        GameObject prefab = Resources.Load<GameObject>(ImportedResourcesRoot + resourceName);
        ImportedPrefabCache[resourceName] = prefab;
        return prefab;
    }

    private static void SpawnBurst(
        string name,
        Vector3 position,
        Color startColor,
        float startSize,
        float startSpeed,
        float lifetime,
        int particleCount)
    {
        PooledBurst burst = Acquire(name);
        burst.gameObject.name = name;
        burst.gameObject.transform.position = position;
        burst.gameObject.SetActive(true);

        ParticleSystem particles = burst.particleSystem;
        ParticleSystem.MainModule main = particles.main;
        main.startColor = startColor;
        main.startSize = startSize;
        main.startSpeed = startSpeed;
        main.startLifetime = lifetime;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Max(1, particleCount))
        });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.045f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(startColor * 0.7f, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = BurstSortingOrder;

        particles.Clear(true);
        particles.Play();

        burst.releaseAt = Time.unscaledTime + lifetime + 0.35f;
        Active.Add(burst);
    }

    private static void EnsureHost()
    {
        if (host != null)
        {
            return;
        }

        GameObject existing = GameObject.Find("CleanVfxFactoryHost");
        GameObject hostObject = existing != null ? existing : new GameObject("CleanVfxFactoryHost");
        host = hostObject.GetComponent<FactoryHost>();
        if (host == null)
        {
            host = hostObject.AddComponent<FactoryHost>();
        }

        Object.DontDestroyOnLoad(hostObject);
    }

    private static PooledBurst Acquire(string key)
    {
        EnsureHost();

        if (Pool.TryGetValue(key, out Queue<PooledBurst> queue) && queue.Count > 0)
        {
            return queue.Dequeue();
        }

        GameObject burstObject = new GameObject(key);
        burstObject.transform.SetParent(host.transform, false);
        ParticleSystem particles = burstObject.AddComponent<ParticleSystem>();

        return new PooledBurst
        {
            key = key,
            gameObject = burstObject,
            particleSystem = particles
        };
    }

    private static void Tick(float now)
    {
        for (int i = Active.Count - 1; i >= 0; i--)
        {
            PooledBurst burst = Active[i];
            if (burst == null || burst.gameObject == null || burst.particleSystem == null)
            {
                Active.RemoveAt(i);
                continue;
            }

            if (now < burst.releaseAt && burst.particleSystem.IsAlive(true))
            {
                continue;
            }

            ReturnToPool(burst);
            Active.RemoveAt(i);
        }
    }

    private static void ReturnToPool(PooledBurst burst)
    {
        if (burst == null || burst.gameObject == null)
        {
            return;
        }

        burst.particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        burst.gameObject.SetActive(false);

        if (!Pool.TryGetValue(burst.key, out Queue<PooledBurst> queue))
        {
            queue = new Queue<PooledBurst>();
            Pool[burst.key] = queue;
        }

        if (queue.Count >= MaxPoolPerEffect)
        {
            Object.Destroy(burst.gameObject);
            return;
        }

        queue.Enqueue(burst);
    }
}
