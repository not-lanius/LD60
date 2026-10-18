using UnityEngine;

[DisallowMultipleComponent]
public class AudioDirector : MonoBehaviour
{
    public static AudioDirector Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private AudioClip dodgeClip;
    [SerializeField] private AudioClip meleeClip;
    [SerializeField] private AudioClip writingClip;
    [SerializeField] private AudioClip fireWhooshClip;
    [SerializeField] private AudioClip fireImpactClip;
    [SerializeField] private AudioClip rockRiseClip;
    [SerializeField] private AudioClip rockExplosionClip;
    [SerializeField] private AudioClip lightningClip;
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField] private AudioClip enemyWindupClip;
    [SerializeField] private AudioClip[] enemyHurtClips;
    [SerializeField] private AudioClip playerHurtClip;
    [SerializeField] private AudioClip playerDeathClip;
    [SerializeField] private AudioClip galleryRoom1Clip;
    [SerializeField] private AudioClip galleryRoom2Clip;

    [Header("Volumes")]
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.32f;
    [SerializeField] [Range(0f, 1f)] private float dodgeVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float meleeVolume = 0.9f;
    [SerializeField] [Range(0f, 1f)] private float writingVolume = 0.32f;
    [SerializeField] [Range(0f, 1f)] private float fireWhooshVolume = 0.72f;
    [SerializeField] [Range(0f, 1f)] private float fireImpactVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float rockRiseVolume = 0.75f;
    [SerializeField] [Range(0f, 1f)] private float rockExplosionVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] private float lightningVolume = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float doorOpenVolume = 0.75f;
    [SerializeField] [Range(0f, 1f)] private float enemyWindupVolume = 0.42f;
    [SerializeField] [Range(0f, 1f)] private float enemyHurtVolume = 0.62f;
    [SerializeField] [Range(0f, 1f)] private float playerHurtVolume = 0.7f;
    [SerializeField] [Range(0f, 1f)] private float playerDeathVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float galleryAmbienceVolume = 0.26f;

    [Header("Bullet Time")]
    [SerializeField] [Range(0.1f, 1f)] private float bulletTimePitch = 0.55f;
    [SerializeField] private float pitchBlendSpeed = 9f;

    [SerializeField] private int sfxVoiceCount = 8;

    private AudioSource[] sfxVoices;
    private float[] voiceBasePitches;
    private AudioSource footstepSource;
    private AudioSource ambienceSource;
    private int voiceIndex;
    private bool footstepsRequested;
    private float targetPitch = 1f;
    private float currentPitch = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateSources();
        targetPitch = 1f;
        currentPitch = 1f;
        ApplyPitch();
    }

    private void Update()
    {
        currentPitch = Mathf.MoveTowards(
            currentPitch,
            targetPitch,
            pitchBlendSpeed * Time.unscaledDeltaTime);
        ApplyPitch();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void CreateSources()
    {
        sfxVoices = new AudioSource[Mathf.Max(1, sfxVoiceCount)];
        voiceBasePitches = new float[sfxVoices.Length];
        for (int i = 0; i < sfxVoices.Length; i++)
        {
            sfxVoices[i] = CreateSource("SfxVoice" + i, false);
            voiceBasePitches[i] = 1f;
        }

        footstepSource = CreateSource("Footsteps", true);
        ambienceSource = CreateSource("GalleryAmbience", true);
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    public void SetBulletTime(bool active)
    {
        targetPitch = active ? bulletTimePitch : 1f;
    }

    private void ApplyPitch()
    {
        if (sfxVoices != null)
        {
            for (int i = 0; i < sfxVoices.Length; i++)
            {
                if (sfxVoices[i] != null)
                {
                    sfxVoices[i].pitch = currentPitch * voiceBasePitches[i];
                }
            }
        }

        if (footstepSource != null)
        {
            footstepSource.pitch = currentPitch;
        }

        if (ambienceSource != null)
        {
            ambienceSource.pitch = currentPitch;
        }
    }

    public void PlayFootsteps(bool active)
    {
        if (footstepSource == null || active == footstepsRequested)
        {
            return;
        }

        footstepsRequested = active;
        if (active && footstepClip != null)
        {
            footstepSource.clip = footstepClip;
            footstepSource.volume = footstepVolume;
            footstepSource.pitch = currentPitch;
            footstepSource.Play();
        }
        else
        {
            footstepSource.Stop();
        }
    }

    public void PlayDodge()
    {
        PlaySfx(dodgeClip, dodgeVolume);
    }

    public void PlayMelee()
    {
        PlaySfx(meleeClip, meleeVolume);
    }

    public void PlayWriting()
    {
        PlaySfx(writingClip, writingVolume, Random.Range(0.96f, 1.04f));
    }

    public void PlayFireWhoosh()
    {
        PlaySfx(fireWhooshClip, fireWhooshVolume, Random.Range(0.97f, 1.03f));
    }

    public void PlayFireImpact()
    {
        PlaySfx(fireImpactClip, fireImpactVolume, Random.Range(0.96f, 1.04f));
    }

    public void PlayRockRise()
    {
        PlaySfx(rockRiseClip, rockRiseVolume, Random.Range(0.97f, 1.03f));
    }

    public void PlayRockExplosion()
    {
        PlaySfx(rockExplosionClip, rockExplosionVolume, Random.Range(0.96f, 1.04f));
    }

    public void PlayLightning()
    {
        PlaySfx(lightningClip, lightningVolume);
    }

    public void PlayDoorOpen()
    {
        PlaySfx(doorOpenClip, doorOpenVolume);
    }

    public void PlayEnemyWindup()
    {
        PlaySfx(enemyWindupClip, enemyWindupVolume);
    }

    public void PlayEnemyHurt()
    {
        if (enemyHurtClips == null || enemyHurtClips.Length == 0)
        {
            return;
        }

        AudioClip clip = enemyHurtClips[Random.Range(0, enemyHurtClips.Length)];
        PlaySfx(clip, enemyHurtVolume, Random.Range(0.94f, 1.06f));
    }

    public void PlayPlayerDeath()
    {
        PlaySfx(playerDeathClip, playerDeathVolume);
    }

    public void PlayPlayerHurt()
    {
        PlaySfx(playerHurtClip, playerHurtVolume);
    }

    public void PlayGalleryRoom1()
    {
        PlayAmbience(galleryRoom1Clip);
    }

    public void PlayGalleryRoom2()
    {
        PlayAmbience(galleryRoom2Clip);
    }

    public void StopGalleryAmbience()
    {
        if (ambienceSource != null)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
        }
    }

    private void PlayAmbience(AudioClip clip)
    {
        if (ambienceSource == null || clip == null)
        {
            return;
        }

        if (ambienceSource.clip == clip && ambienceSource.isPlaying)
        {
            return;
        }

        ambienceSource.Stop();
        ambienceSource.clip = clip;
        ambienceSource.volume = galleryAmbienceVolume;
        ambienceSource.pitch = currentPitch;
        ambienceSource.Play();
    }

    private void PlaySfx(AudioClip clip, float volume, float pitchMultiplier = 1f)
    {
        if (clip == null || sfxVoices == null || sfxVoices.Length == 0)
        {
            return;
        }

        int selectedVoice = voiceIndex;
        AudioSource voice = sfxVoices[selectedVoice];
        voiceIndex = (voiceIndex + 1) % sfxVoices.Length;
        voiceBasePitches[selectedVoice] = pitchMultiplier;
        voice.Stop();
        voice.clip = clip;
        voice.volume = volume;
        voice.pitch = currentPitch * pitchMultiplier;
        voice.Play();
    }
}
