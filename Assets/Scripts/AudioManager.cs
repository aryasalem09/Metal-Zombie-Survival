using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Music")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("SFX")]
    [SerializeField] private AudioClip attackSfx;
    [SerializeField] private AudioClip hitSparkSfx;
    [SerializeField] private AudioClip uiClickSfx;
    [SerializeField] private AudioClip zombieDeathSfx;
    [SerializeField] private AudioClip abilityBurstSfx;
    [SerializeField] private AudioClip pickupSfx;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureSources();
    }

    private void Start()
    {
        if (playMusicOnStart && backgroundMusic != null)
        {
            PlayMusic(backgroundMusic);
        }
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

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }
}