using UnityEngine;

[CreateAssetMenu(menuName = "Waves/Wave Config")]
public class WaveConfig : ScriptableObject
{
    public GameObject zombiePrefab;

    [Header("spawn (optional)")]
    [Tooltip("If you assign spawn points in WaveManager, it will use those. If none are assigned, zombies spawn just outside the camera view.")]
    public float offscreenSpawnMargin = 2f;
    [Tooltip("Minimum distance from the player when spawning (prevents cheap spawns right on top of them).")]
    public float minSpawnDistanceFromPlayer = 6f;

    [Header("waves")]
    public int waveCount = 0; // 0 = infinite
    public int zombiesPerWave = 8;
    public int zombiesPerWaveIncrease = 3;
    public int maxZombiesPerWave = 0; // 0 = no cap

    [Header("limits")]
    public int maxAliveAtOnce = 12; // keeps things playable + less lag

    [Header("timing")]
    public float timeBeforeFirstWave = 1.0f;
    public float timeBetweenSpawns = 0.5f;
    public float timeBetweenWaves = 2.5f;

    [Header("difficulty scaling")]
    [Range(1f, 3f)] public float healthMultiplierPerWave = 1.10f;
    [Range(1f, 3f)] public float speedMultiplierPerWave = 1.05f;

    [Header("runners")]
    [Range(0f, 1f)] public float runnerChanceStart = 0.05f;
    [Range(0f, 1f)] public float runnerChanceIncreasePerWave = 0.03f;
    [Range(1f, 3f)] public float runnerSpeedBonus = 1.35f;
    [Range(1f, 3f)] public float runnerDetectionBonus = 1.25f;

    [Header("mutations")]
    [Tooltip("Wave index where radiated zombies can start appearing (1 = first wave).")]
    public int radiatedWaveStart = 3;
    [Range(0f, 1f)] public float radiatedChanceStart = 0.10f;
    [Range(0f, 1f)] public float radiatedChanceIncreasePerWave = 0.03f;
    [Range(1f, 5f)] public float radiatedHealthBonus = 1.25f;
    [Range(0.5f, 3f)] public float radiatedSpeedBonus = 1.10f;

    [Tooltip("Wave index where tank zombies can start appearing (1 = first wave).")]
    public int tankWaveStart = 5;
    [Range(0f, 1f)] public float tankChanceStart = 0.08f;
    [Range(0f, 1f)] public float tankChanceIncreasePerWave = 0.02f;
    [Range(1f, 10f)] public float tankHealthBonus = 2.0f;
    [Range(0.1f, 1f)] public float tankSpeedMultiplier = 0.75f;
    [Range(1f, 2.5f)] public float tankScaleMultiplier = 1.25f;

    [Header("scene flow (optional)")]
    [Tooltip("If waveCount > 0 and this is set, WaveManager will load this scene after finishing all waves.")]
    public string nextSceneName = "";
}