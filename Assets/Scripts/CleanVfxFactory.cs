using UnityEngine;
using System.Collections.Generic;

public static class CleanVfxFactory
{
    private const int MaxPoolPerEffect = 24;

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

    public static void SpawnImpactSpark(Vector3 position)
    {
        SpawnBurst(
            name: "ImpactSparkVFX",
            position: position,
            startColor: new Color(1f, 0.88f, 0.35f, 1f),
            startSize: 0.08f,
            startSpeed: 2.25f,
            lifetime: 0.25f,
            particleCount: 16);
    }

    public static void SpawnAbilityBurstGlow(Vector3 position)
    {
        SpawnBurst(
            name: "AbilityBurstGlowVFX",
            position: position,
            startColor: new Color(0.3f, 0.95f, 1f, 1f),
            startSize: 0.2f,
            startSpeed: 2.8f,
            lifetime: 0.45f,
            particleCount: 28);
    }

    public static void SpawnZombieDeathPoof(Vector3 position)
    {
        SpawnBurst(
            name: "ZombieDeathPoofVFX",
            position: position,
            startColor: new Color(0.7f, 0.7f, 0.75f, 1f),
            startSize: 0.14f,
            startSpeed: 1.5f,
            lifetime: 0.55f,
            particleCount: 20);
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
        shape.radius = 0.06f;

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
        renderer.sortingOrder = 15;

        particles.Clear(true);
        particles.Play();

        burst.releaseAt = Time.unscaledTime + lifetime + 0.5f;
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
