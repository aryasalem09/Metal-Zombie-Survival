using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    [Serializable]
    private class SceneMusicEntry
    {
        public string sceneName;
        public AudioClip musicClip;
    }

    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool autoSwitchMusicByScene = true;
    [SerializeField] private bool autoAssignImportedStuffAudio = true;
    [SerializeField] private List<SceneMusicEntry> sceneMusicOverrides = new List<SceneMusicEntry>();

    [Header("SFX")]
    [SerializeField] private AudioClip attackSfx;
    [SerializeField] private AudioClip hitSparkSfx;
    [SerializeField] private AudioClip playerDamageSfx;
    [SerializeField] private AudioClip uiClickSfx;
    [SerializeField] private AudioClip zombieDeathSfx;
    [SerializeField] private AudioClip abilityBurstSfx;
    [SerializeField] private AudioClip pickupSfx;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            Instance.EnsureAudioListenerPresent();
            return;
        }

        AudioManager existing = FindObjectOfType<AudioManager>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureSources();
            existing.EnsureAudioListenerPresent();
            return;
        }

        GameObject runtimeObject = new GameObject("AudioManager");
        runtimeObject.AddComponent<AudioManager>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureEarlyAudioListener()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
            {
                return;
            }
        }

        GameObject earlyListenerObject = new GameObject("EarlyRuntimeAudioListener");
        DontDestroyOnLoad(earlyListenerObject);
        earlyListenerObject.AddComponent<AudioListener>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSources();
        AutoAssignMissingAudioReferences();
        EnsureAudioListenerPresent();

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        if (!playMusicOnStart)
        {
            return;
        }

        if (autoSwitchMusicByScene)
        {
            ApplySceneMusic(SceneManager.GetActiveScene().name);
        }
        else if (backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        EnsureAudioListenerPresent();

        if (!autoSwitchMusicByScene)
        {
            return;
        }

        ApplySceneMusic(scene.name);
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (musicSource == null || clip == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void PlayAttackSfx()
    {
        PlayOneShot(attackSfx);
    }

    public void PlayHitSparkSfx()
    {
        PlayOneShot(hitSparkSfx);
    }

    public void PlayPlayerDamageSfx()
    {
        PlayOneShot(playerDamageSfx != null ? playerDamageSfx : hitSparkSfx);
    }

    public void PlayUiClick()
    {
        PlayOneShot(uiClickSfx);
    }

    public void PlayZombieDeathSfx()
    {
        PlayOneShot(zombieDeathSfx);
    }

    public void PlayAbilityBurstSfx()
    {
        PlayOneShot(abilityBurstSfx);
    }

    public void PlayPickupSfx()
    {
        PlayOneShot(pickupSfx);
    }

    public void PlayCustomSfx(AudioClip clip)
    {
        PlayOneShot(clip);
    }

    private void ApplySceneMusic(string sceneName)
    {
        AudioClip clip = ResolveMusicOverride(sceneName);
        if (clip == null)
        {
            clip = backgroundMusic;
        }

        if (clip != null)
        {
            PlayMusic(clip);
        }
    }

    private AudioClip ResolveMusicOverride(string sceneName)
    {
        if (sceneMusicOverrides != null)
        {
            for (int i = 0; i < sceneMusicOverrides.Count; i++)
            {
                SceneMusicEntry entry = sceneMusicOverrides[i];
                if (entry == null || entry.musicClip == null || string.IsNullOrWhiteSpace(entry.sceneName))
                {
                    continue;
                }

                if (string.Equals(entry.sceneName, sceneName, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.musicClip;
                }
            }
        }

        if (!autoAssignImportedStuffAudio)
        {
            return null;
        }

        string normalizedName = (sceneName ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        if (normalizedName.Contains("tutorial"))
        {
            return ImportedStuffAssetUtility.GetAudioClip("ha-hesitate");
        }

        if (normalizedName.Contains("level1"))
        {
            return ImportedStuffAssetUtility.GetAudioClip("ha-abomination");
        }

        if (normalizedName.Contains("level2"))
        {
            return ImportedStuffAssetUtility.GetAudioClip("ha-undercurrent");
        }

        if (normalizedName.Contains("level3"))
        {
            return ImportedStuffAssetUtility.GetAudioClip("ha-suffocate");
        }

        return ImportedStuffAssetUtility.GetAudioClip("ha-undercurrent2");
    }

    private void AutoAssignMissingAudioReferences()
    {
        if (!autoAssignImportedStuffAudio)
        {
            return;
        }

        if (backgroundMusic == null) backgroundMusic = ImportedStuffAssetUtility.GetAudioClip("ha-undercurrent2");
        if (attackSfx == null) attackSfx = ImportedStuffAssetUtility.GetAudioClip("machine_gun");
        if (hitSparkSfx == null) hitSparkSfx = ImportedStuffAssetUtility.GetAudioClip("laser_02");
        if (playerDamageSfx == null) playerDamageSfx = ImportedStuffAssetUtility.GetAudioClip("cannon_01");
        if (uiClickSfx == null) uiClickSfx = ImportedStuffAssetUtility.GetAudioClip("card");
        if (zombieDeathSfx == null) zombieDeathSfx = ImportedStuffAssetUtility.GetAudioClip("laser_01");
        if (abilityBurstSfx == null) abilityBurstSfx = ImportedStuffAssetUtility.GetAudioClip("magic_03");
        if (pickupSfx == null) pickupSfx = ImportedStuffAssetUtility.GetAudioClip("heal");

        if (sceneMusicOverrides == null)
        {
            sceneMusicOverrides = new List<SceneMusicEntry>();
        }

        if (sceneMusicOverrides.Count == 0)
        {
            sceneMusicOverrides.Add(new SceneMusicEntry
            {
                sceneName = "Tutorial",
                musicClip = ImportedStuffAssetUtility.GetAudioClip("ha-hesitate")
            });
            sceneMusicOverrides.Add(new SceneMusicEntry
            {
                sceneName = "Level1",
                musicClip = ImportedStuffAssetUtility.GetAudioClip("ha-abomination")
            });
            sceneMusicOverrides.Add(new SceneMusicEntry
            {
                sceneName = "Level 2",
                musicClip = ImportedStuffAssetUtility.GetAudioClip("ha-undercurrent")
            });
            sceneMusicOverrides.Add(new SceneMusicEntry
            {
                sceneName = "Level 3",
                musicClip = ImportedStuffAssetUtility.GetAudioClip("ha-suffocate")
            });
        }
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip);
    }

    private void EnsureSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
        sfxSource.spatialBlend = 0f;
    }

    private void EnsureAudioListenerPresent()
    {
        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        AudioListener activeListener = null;
        int activeCount = 0;

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || !listener.enabled || !listener.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeCount++;
            if (activeListener == null)
            {
                activeListener = listener;
            }
        }

        if (activeCount == 0)
        {
            Camera preferredCamera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (preferredCamera != null)
            {
                AudioListener cameraListener = preferredCamera.GetComponent<AudioListener>();
                if (cameraListener == null)
                {
                    cameraListener = preferredCamera.gameObject.AddComponent<AudioListener>();
                }

                cameraListener.enabled = true;
                return;
            }

            if (listeners.Length > 0 && listeners[0] != null)
            {
                listeners[0].enabled = true;
                return;
            }

            GameObject fallbackObject = new GameObject("RuntimeAudioListener");
            fallbackObject.AddComponent<AudioListener>();
            return;
        }

        if (activeCount <= 1)
        {
            return;
        }

        AudioListener keep = null;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            AudioListener mainCameraListener = mainCamera.GetComponent<AudioListener>();
            if (mainCameraListener != null &&
                mainCameraListener.enabled &&
                mainCameraListener.gameObject.activeInHierarchy)
            {
                keep = mainCameraListener;
            }
        }

        if (keep == null)
        {
            keep = activeListener;
        }

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null || listener == keep)
            {
                continue;
            }

            if (listener.enabled && listener.gameObject.activeInHierarchy)
            {
                listener.enabled = false;
            }
        }
    }
}
