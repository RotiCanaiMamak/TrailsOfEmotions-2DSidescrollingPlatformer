using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string BackgroundVolumeKey = "Audio.BackgroundVolume";
    private const string StartSceneName = "StartScene";

    [Header("Lifetime")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Mix")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float ambientVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float backgroundVolume = 1f;

    [Header("Biome Fade")]
    [Min(0f)] [SerializeField] private float biomeFadeDuration = 1f;

    [Header("Start Scene Audio")]
    [SerializeField] private AudioClip startSceneMusic;
    [Range(0f, 1f)] [SerializeField] private float startSceneMusicVolume = 1f;
    [Min(0f)] [SerializeField] private float startSceneFadeDuration = 1f;

    [Header("Game Over Music")]
    [SerializeField] private AudioClip gameOverMusic;
    [Range(0f, 1f)] [SerializeField] private float gameOverMusicVolume = 1f;
    [Min(0f)] [SerializeField] private float gameOverFadeDuration = 1f;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip transitionClip;
    [Range(0f, 1f)] [SerializeField] private float transitionVolume = 1f;
    [SerializeField] private AudioClip orbCollectionClip;
    [Range(0f, 1f)] [SerializeField] private float orbCollectionVolume = 1f;
    [SerializeField] private AudioClip playerJumpClip;
    [Range(0f, 1f)] [SerializeField] private float playerJumpVolume = 1f;
    [SerializeField] private AudioClip groundPoundClip;
    [Range(0f, 1f)] [SerializeField] private float groundPoundVolume = 1f;
    [SerializeField] private AudioClip sootLoopClip;
    [Range(0f, 1f)] [SerializeField] private float sootLoopVolume = 1f;
    [Min(0f)] [SerializeField] private float sootLoopFadeOutDuration = 0.6f;

    private readonly AudioSource[] musicSources = new AudioSource[2];
    private readonly AudioSource[] ambientSources = new AudioSource[2];
    private AudioSource sfxSource;
    private AudioSource sootLoopSource;

    private int activeMusicSourceIndex;
    private int activeAmbientSourceIndex;
    private Coroutine musicFadeRoutine;
    private Coroutine ambientFadeRoutine;
    private Coroutine musicLoopRoutine;
    private Coroutine ambientLoopRoutine;
    private Coroutine sootLoopFadeRoutine;
    private BiomeManager subscribedBiomeManager;
    private GameManager subscribedGameManager;
    private BiomeData activeBiome;
    private bool startSceneMusicActive;
    private bool gameOverAudioActive;

    public float MasterVolume => masterVolume;
    public float SfxVolume => sfxVolume;
    public float BackgroundVolume => backgroundVolume;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        LoadVolumeSettings();
        EnsureAudioSources();
    }

    private void Start()
    {
        HandleLoadedScene(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        TrySubscribeToBiomeManager();
        TrySubscribeToGameManager();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribeToBiomeManager();
        TrySubscribeToGameManager();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromBiomeManager();
        UnsubscribeFromGameManager();
        StopAudioRoutines();
        StopSourceGroup(musicSources);
        StopSourceGroup(ambientSources);
        activeBiome = null;
        startSceneMusicActive = false;
        gameOverAudioActive = false;
    }

    private void OnDestroy()
    {
        UnsubscribeFromBiomeManager();
        UnsubscribeFromGameManager();
        StopAudioRoutines();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayTransitionSound()
    {
        PlaySfx(transitionClip, transitionVolume);
    }

    public void PlayOrbCollectionSound()
    {
        PlaySfx(orbCollectionClip, orbCollectionVolume);
    }

    public void PlayPlayerJumpSound()
    {
        PlaySfx(playerJumpClip, playerJumpVolume);
    }

    public void PlayGroundPoundSound()
    {
        PlaySfx(groundPoundClip, groundPoundVolume);
    }

    public void PlaySfxOneShot(AudioClip clip, float volumeScale = 1f)
    {
        PlaySfx(clip, volumeScale);
    }

    public void PlayGameOverMusic()
    {
        EnsureAudioSources();

        if (gameOverAudioActive)
        {
            return;
        }

        gameOverAudioActive = true;
        startSceneMusicActive = false;
        activeBiome = null;

        CrossfadeMusic(gameOverMusic, GetGameOverMusicTargetVolume(), gameOverFadeDuration);
        CrossfadeAmbient(null, 0f, gameOverFadeDuration);
    }

    public void PlayStartSceneMusic(bool immediate = false)
    {
        EnsureAudioSources();

        if (startSceneMusic == null)
        {
            CrossfadeMusic(null, 0f, immediate ? 0f : startSceneFadeDuration);
            CrossfadeAmbient(null, 0f, immediate ? 0f : startSceneFadeDuration);
            return;
        }

        gameOverAudioActive = false;
        startSceneMusicActive = true;
        activeBiome = null;

        CrossfadeMusic(startSceneMusic, GetStartSceneMusicTargetVolume(), immediate ? 0f : startSceneFadeDuration);
        CrossfadeAmbient(null, 0f, immediate ? 0f : startSceneFadeDuration);
    }

    public void FadeOutStartSceneMusic()
    {
        EnsureAudioSources();

        if (!startSceneMusicActive)
        {
            return;
        }

        startSceneMusicActive = false;
        CrossfadeMusic(null, 0f, startSceneFadeDuration);
    }

    public void PlaySootEffectLoop()
    {
        EnsureAudioSources();
        PlayLoopingSfx(sootLoopClip, sootLoopSource, sootLoopVolume, ref sootLoopFadeRoutine);
    }

    public void StopSootEffectLoop(bool fadeOut = true)
    {
        EnsureAudioSources();
        StopLoopingSfx(
            sootLoopSource,
            fadeOut ? sootLoopFadeOutDuration : 0f,
            ref sootLoopFadeRoutine);
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
        ApplyCurrentMix();
    }

    public void SetSfxVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
        ApplySfxLoopVolumes();
    }

    public void SetBackgroundVolume(float volume)
    {
        backgroundVolume = Mathf.Clamp01(volume);
        SaveVolumeSettings();
        ApplyBackgroundVolumes();
    }

    private void OnBiomeChanged(BiomeData biome)
    {
        if (gameOverAudioActive)
        {
            return;
        }

        PlayBiomeAudio(biome, false);
    }

    private void PlayBiomeAudio(BiomeData biome, bool immediate)
    {
        gameOverAudioActive = false;
        startSceneMusicActive = false;
        activeBiome = biome;

        AudioClip musicClip = biome != null ? biome.backgroundMusic : null;
        AudioClip ambientClip = biome != null ? biome.ambientSound : null;

        if (biome != null && musicClip == null)
        {
            Debug.LogWarning($"[AudioManager] Biome '{biome.biomeName}' has no background music assigned.", this);
        }

        if (biome != null && ambientClip == null)
        {
            Debug.LogWarning($"[AudioManager] Biome '{biome.biomeName}' has no ambient sound assigned.", this);
        }

        CrossfadeMusic(musicClip, GetMusicTargetVolume(biome), immediate ? 0f : biomeFadeDuration);
        CrossfadeAmbient(ambientClip, GetAmbientTargetVolume(biome), immediate ? 0f : biomeFadeDuration);
    }

    private void CrossfadeMusic(AudioClip nextClip, float targetVolume, float duration)
    {
        StopRoutine(ref musicFadeRoutine);
        StopRoutine(ref musicLoopRoutine);

        musicFadeRoutine = StartCoroutine(CrossfadeToClipRoutine(
            musicSources,
            activeMusicSourceIndex,
            nextClip,
            targetVolume,
            duration,
            nextActiveIndex => activeMusicSourceIndex = nextActiveIndex,
            nextActiveIndex => StartMusicLoop(nextActiveIndex, nextClip, targetVolume),
            () => musicFadeRoutine = null));
    }

    private void CrossfadeAmbient(AudioClip nextClip, float targetVolume, float duration)
    {
        StopRoutine(ref ambientFadeRoutine);
        StopRoutine(ref ambientLoopRoutine);

        ambientFadeRoutine = StartCoroutine(CrossfadeToClipRoutine(
            ambientSources,
            activeAmbientSourceIndex,
            nextClip,
            targetVolume,
            duration,
            nextActiveIndex => activeAmbientSourceIndex = nextActiveIndex,
            nextActiveIndex => StartAmbientLoop(nextActiveIndex, nextClip, targetVolume),
            () => ambientFadeRoutine = null));
    }

    private IEnumerator CrossfadeToClipRoutine(
        AudioSource[] sources,
        int activeIndex,
        AudioClip nextClip,
        float targetVolume,
        float duration,
        System.Action<int> setActiveIndex,
        System.Action<int> onClipReady,
        System.Action onComplete)
    {
        AudioSource currentSource = sources[activeIndex];
        AudioSource nextSource = sources[1 - activeIndex];
        bool hasNextClip = nextClip != null;
        float effectiveDuration = hasNextClip ? GetFadeDurationForClip(nextClip, duration) : duration;

        if (currentSource.clip == nextClip && currentSource.isPlaying)
        {
            yield return FadeSourceRoutine(currentSource, currentSource.volume, targetVolume, effectiveDuration);
            onClipReady?.Invoke(activeIndex);
            onComplete?.Invoke();
            yield break;
        }

        float currentStartVolume = currentSource.volume;

        nextSource.Stop();
        nextSource.clip = nextClip;
        nextSource.volume = 0f;

        if (hasNextClip)
        {
            nextSource.Play();
        }

        if (effectiveDuration <= 0f)
        {
            currentSource.Stop();
            currentSource.clip = null;
            currentSource.volume = 0f;
            nextSource.volume = hasNextClip ? targetVolume : 0f;
            setActiveIndex(hasNextClip ? 1 - activeIndex : activeIndex);
            if (hasNextClip)
            {
                onClipReady?.Invoke(1 - activeIndex);
            }

            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < effectiveDuration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / effectiveDuration);
            currentSource.volume = Mathf.Lerp(currentStartVolume, 0f, t);
            nextSource.volume = hasNextClip ? Mathf.Lerp(0f, targetVolume, t) : 0f;
            yield return null;
        }

        currentSource.Stop();
        currentSource.clip = null;
        currentSource.volume = 0f;
        nextSource.volume = hasNextClip ? targetVolume : 0f;
        setActiveIndex(hasNextClip ? 1 - activeIndex : activeIndex);
        if (hasNextClip)
        {
            onClipReady?.Invoke(1 - activeIndex);
        }

        onComplete?.Invoke();
    }

    private void StartMusicLoop(int activeIndex, AudioClip clip, float targetVolume)
    {
        if (clip == null)
        {
            return;
        }

        StopRoutine(ref musicLoopRoutine);
        musicLoopRoutine = StartCoroutine(ManagedLoopRoutine(
            musicSources,
            activeIndex,
            clip,
            targetVolume,
            nextActiveIndex => activeMusicSourceIndex = nextActiveIndex));
    }

    private void StartAmbientLoop(int activeIndex, AudioClip clip, float targetVolume)
    {
        if (clip == null)
        {
            return;
        }

        StopRoutine(ref ambientLoopRoutine);
        ambientLoopRoutine = StartCoroutine(ManagedLoopRoutine(
            ambientSources,
            activeIndex,
            clip,
            targetVolume,
            nextActiveIndex => activeAmbientSourceIndex = nextActiveIndex));
    }

    private IEnumerator ManagedLoopRoutine(
        AudioSource[] sources,
        int activeIndex,
        AudioClip clip,
        float targetVolume,
        System.Action<int> setActiveIndex)
    {
        int currentIndex = activeIndex;

        while (clip != null && clip.length > 0f)
        {
            AudioSource currentSource = sources[currentIndex];
            AudioSource nextSource = sources[1 - currentIndex];

            if (currentSource == null || nextSource == null || currentSource.clip != clip || !currentSource.isPlaying)
            {
                yield break;
            }

            float fadeDuration = GetFadeDurationForClip(clip, biomeFadeDuration);
            float fadeStartTime = Mathf.Max(0f, clip.length - fadeDuration);

            while (currentSource.clip == clip && currentSource.isPlaying && currentSource.time < fadeStartTime)
            {
                yield return null;
            }

            if (currentSource.clip != clip)
            {
                yield break;
            }

            nextSource.Stop();
            nextSource.clip = clip;
            nextSource.time = 0f;
            nextSource.volume = 0f;
            nextSource.Play();

            if (fadeDuration <= 0f)
            {
                currentSource.Stop();
                currentSource.clip = null;
                currentSource.volume = 0f;
                nextSource.volume = targetVolume;
                currentIndex = 1 - currentIndex;
                setActiveIndex?.Invoke(currentIndex);
                yield return null;
                continue;
            }

            float currentStartVolume = currentSource.volume;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                if (currentSource.clip != clip || nextSource.clip != clip)
                {
                    yield break;
                }

                elapsed += GetDeltaTime();
                float t = Mathf.Clamp01(elapsed / fadeDuration);
                currentSource.volume = Mathf.Lerp(currentStartVolume, 0f, t);
                nextSource.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            currentSource.Stop();
            currentSource.clip = null;
            currentSource.volume = 0f;
            nextSource.volume = targetVolume;
            currentIndex = 1 - currentIndex;
            setActiveIndex?.Invoke(currentIndex);
        }
    }

    private IEnumerator FadeSourceRoutine(AudioSource source, float from, float to, float duration)
    {
        if (source == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        source.volume = to;
    }

    private void PlaySfx(AudioClip clip, float volumeScale = 1f)
    {
        PlaySfx(clip, 1f, volumeScale);
    }

    private void PlaySfx(AudioClip clip, float clipVolume, float volumeScale)
    {
        EnsureAudioSources();

        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * Mathf.Clamp01(clipVolume) * Mathf.Clamp01(volumeScale));
    }

    private void PlayLoopingSfx(
        AudioClip clip,
        AudioSource source,
        float clipVolume,
        ref Coroutine fadeRoutine)
    {
        StopRoutine(ref fadeRoutine);

        if (clip == null || source == null)
        {
            StopLoopingSfx(source, 0f, ref fadeRoutine);
            return;
        }

        source.clip = clip;
        source.volume = GetSfxTargetVolume(clipVolume);

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void StopLoopingSfx(AudioSource source, float fadeDuration, ref Coroutine fadeRoutine)
    {
        StopRoutine(ref fadeRoutine);

        if (source == null || !source.isPlaying || fadeDuration <= 0f)
        {
            StopLoopingSfxImmediately(source);
            return;
        }

        fadeRoutine = StartCoroutine(FadeOutLoopingSfxRoutine(source, fadeDuration));
    }

    private IEnumerator FadeOutLoopingSfxRoutine(AudioSource source, float fadeDuration)
    {
        float startVolume = source != null ? source.volume : 0f;
        float elapsed = 0f;

        while (source != null && elapsed < fadeDuration)
        {
            elapsed += GetDeltaTime();
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }

        StopLoopingSfxImmediately(source);
    }

    private void StopLoopingSfxImmediately(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    private void TrySubscribeToBiomeManager()
    {
        if (subscribedBiomeManager != null || BiomeManager.Instance == null)
        {
            return;
        }

        subscribedBiomeManager = BiomeManager.Instance;
        subscribedBiomeManager.onBiomeChanged.AddListener(OnBiomeChanged);
        subscribedBiomeManager.EnsureInitialized();

        if (activeBiome != subscribedBiomeManager.CurrentBiome)
        {
            PlayBiomeAudio(subscribedBiomeManager.CurrentBiome, false);
        }
    }

    private void UnsubscribeFromBiomeManager()
    {
        if (subscribedBiomeManager == null)
        {
            return;
        }

        subscribedBiomeManager.onBiomeChanged.RemoveListener(OnBiomeChanged);
        subscribedBiomeManager = null;
    }

    private void TrySubscribeToGameManager()
    {
        if (subscribedGameManager != null || GameManager.Instance == null)
        {
            return;
        }

        subscribedGameManager = GameManager.Instance;
        subscribedGameManager.onGameOver.AddListener(PlayGameOverMusic);
    }

    private void UnsubscribeFromGameManager()
    {
        if (subscribedGameManager == null)
        {
            return;
        }

        subscribedGameManager.onGameOver.RemoveListener(PlayGameOverMusic);
        subscribedGameManager = null;
    }

    private void EnsureAudioSources()
    {
        musicSources[0] = EnsureSource("Music Source A", false);
        musicSources[1] = EnsureSource("Music Source B", false);
        ambientSources[0] = EnsureSource("Ambient Source A", false);
        ambientSources[1] = EnsureSource("Ambient Source B", false);
        sfxSource = EnsureSource("SFX Source", false);
        sootLoopSource = EnsureSource("Soot Loop Source", true);
    }

    private AudioSource EnsureSource(string sourceName, bool loop)
    {
        Transform child = transform.Find(sourceName);
        GameObject sourceObject = child != null ? child.gameObject : new GameObject(sourceName);
        sourceObject.transform.SetParent(transform);
        sourceObject.transform.localPosition = Vector3.zero;
        sourceObject.transform.localRotation = Quaternion.identity;
        sourceObject.transform.localScale = Vector3.one;

        AudioSource source = sourceObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = sourceObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private float GetMusicTargetVolume(BiomeData biome)
    {
        return masterVolume * backgroundVolume * musicVolume * (biome != null ? biome.backgroundMusicVolume : 1f);
    }

    private float GetAmbientTargetVolume(BiomeData biome)
    {
        return masterVolume * backgroundVolume * ambientVolume * (biome != null ? biome.ambientSoundVolume : 1f);
    }

    private float GetSfxTargetVolume(float clipVolume)
    {
        return masterVolume * sfxVolume * Mathf.Clamp01(clipVolume);
    }

    private float GetGameOverMusicTargetVolume()
    {
        return masterVolume * backgroundVolume * musicVolume * gameOverMusicVolume;
    }

    private float GetStartSceneMusicTargetVolume()
    {
        return masterVolume * backgroundVolume * musicVolume * startSceneMusicVolume;
    }

    private float GetFadeDurationForClip(AudioClip clip, float requestedDuration)
    {
        if (clip == null || clip.length <= 0f)
        {
            return 0f;
        }

        return Mathf.Min(Mathf.Max(0f, requestedDuration), clip.length * 0.5f);
    }

    private float GetDeltaTime()
    {
        return Time.deltaTime;
    }

    private void StopAudioRoutines()
    {
        StopRoutine(ref musicFadeRoutine);
        StopRoutine(ref ambientFadeRoutine);
        StopRoutine(ref musicLoopRoutine);
        StopRoutine(ref ambientLoopRoutine);
        StopRoutine(ref sootLoopFadeRoutine);
        StopLoopingSfxImmediately(sootLoopSource);
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    private void StopSourceGroup(AudioSource[] sources)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
            {
                continue;
            }

            source.Stop();
            source.clip = null;
            source.volume = 0f;
        }
    }

    private void ApplyCurrentMix()
    {
        ApplyBackgroundVolumes();
        ApplySfxLoopVolumes();
    }

    private void ApplyBackgroundVolumes()
    {
        if (gameOverAudioActive)
        {
            SetSourceGroupVolume(musicSources, GetGameOverMusicTargetVolume());
            SetSourceGroupVolume(ambientSources, 0f);
            return;
        }

        if (startSceneMusicActive)
        {
            SetSourceGroupVolume(musicSources, GetStartSceneMusicTargetVolume());
            SetSourceGroupVolume(ambientSources, 0f);
            return;
        }

        if (activeBiome != null)
        {
            PlayBiomeAudio(activeBiome, true);
            return;
        }

        SetSourceGroupVolume(musicSources, masterVolume * backgroundVolume * musicVolume);
        SetSourceGroupVolume(ambientSources, masterVolume * backgroundVolume * ambientVolume);
    }

    private void ApplySfxLoopVolumes()
    {
        if (sootLoopSource != null && sootLoopSource.isPlaying)
        {
            sootLoopSource.volume = GetSfxTargetVolume(sootLoopVolume);
        }
    }

    private static void SetSourceGroupVolume(AudioSource[] sources, float volume)
    {
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source != null && source.isPlaying)
            {
                source.volume = Mathf.Clamp01(volume);
            }
        }
    }

    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, masterVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, sfxVolume);
        backgroundVolume = PlayerPrefs.GetFloat(BackgroundVolumeKey, backgroundVolume);
        ClampMixVolumes();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleLoadedScene(scene);
    }

    private void HandleLoadedScene(Scene scene)
    {
        gameOverAudioActive = false;
        startSceneMusicActive = false;
        activeBiome = null;
        UnsubscribeFromBiomeManager();
        UnsubscribeFromGameManager();

        if (scene.IsValid() && scene.name == StartSceneName)
        {
            PlayStartSceneMusic(true);
            return;
        }

        TrySubscribeToBiomeManager();
        TrySubscribeToGameManager();
    }

    private void SaveVolumeSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
        PlayerPrefs.SetFloat(BackgroundVolumeKey, backgroundVolume);
        PlayerPrefs.Save();
    }

    private void ClampMixVolumes()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
        ambientVolume = Mathf.Clamp01(ambientVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        backgroundVolume = Mathf.Clamp01(backgroundVolume);
        startSceneMusicVolume = Mathf.Clamp01(startSceneMusicVolume);
    }

    private void OnValidate()
    {
        ClampMixVolumes();
        transitionVolume = Mathf.Clamp01(transitionVolume);
        startSceneFadeDuration = Mathf.Max(0f, startSceneFadeDuration);
        gameOverMusicVolume = Mathf.Clamp01(gameOverMusicVolume);
        orbCollectionVolume = Mathf.Clamp01(orbCollectionVolume);
        playerJumpVolume = Mathf.Clamp01(playerJumpVolume);
        groundPoundVolume = Mathf.Clamp01(groundPoundVolume);
        sootLoopVolume = Mathf.Clamp01(sootLoopVolume);
        sootLoopFadeOutDuration = Mathf.Max(0f, sootLoopFadeOutDuration);
        biomeFadeDuration = Mathf.Max(0f, biomeFadeDuration);
        gameOverFadeDuration = Mathf.Max(0f, gameOverFadeDuration);
    }
}
