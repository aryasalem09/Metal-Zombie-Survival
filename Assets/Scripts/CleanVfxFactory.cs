using UnityEngine;

public static class CleanVfxFactory
{
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
        GameObject vfxObject = new GameObject(name);
        vfxObject.transform.position = position;

        ParticleSystem particles = vfxObject.AddComponent<ParticleSystem>();
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

        particles.Play();
        Object.Destroy(vfxObject, lifetime + 0.5f);
    }
}