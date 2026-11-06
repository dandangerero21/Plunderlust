using System;
using UnityEngine;

namespace Plunderlust.Audio
{
    public enum SFXType
    {
        UIClick,
        CannonFire,
        ShipHit,
        ShipBell,
        WoodCreak,
        WaveSplash,
        TreasureDig,
        LootCollect
    }

    /// <summary>
    /// Central 17th-century Nautical Audio Manager.
    /// Exposes Inspector slots for custom audio clips with seamless procedural acoustic fallbacks
    /// so the game sounds atmospheric, rich, and alive immediately even before custom clips are assigned.
    /// Manages Master, Music, and SFX channels with persistent PlayerPrefs volume settings.
    /// Operates with zero per-frame heap allocations.
    /// </summary>
    [DefaultExecutionOrder(-180)]
    public class NauticalAudioManager : MonoBehaviour
    {
        public static NauticalAudioManager Instance { get; private set; }

        private const string PrefMasterVol = "Plunderlust_MasterVol";
        private const string PrefMusicVol = "Plunderlust_MusicVol";
        private const string PrefSfxVol = "Plunderlust_SfxVol";

        [Header("Inspector Audio Slots (Assign custom clips here)")]
        [Tooltip("Background sea shanty voyage theme.")]
        [SerializeField] private AudioClip _bgmVoyage;
        [Tooltip("Atmospheric harbor main menu theme.")]
        [SerializeField] private AudioClip _bgmMainMenu;
        [Tooltip("Naval cannon broadside explosion.")]
        [SerializeField] private AudioClip _sfxCannonFire;
        [Tooltip("Splintering wood hull impact.")]
        [SerializeField] private AudioClip _sfxShipHit;
        [Tooltip("Resonant ship's bell ring.")]
        [SerializeField] private AudioClip _sfxBell;
        [Tooltip("Aged ship timbers creaking under wave stress.")]
        [SerializeField] private AudioClip _sfxWoodCreak;
        [Tooltip("Ocean waves lapping and surf.")]
        [SerializeField] private AudioClip _sfxWaveLapping;
        [Tooltip("Aged brass/wood UI touch click.")]
        [SerializeField] private AudioClip _sfxUIClick;
        [Tooltip("Shovel striking sand / digging.")]
        [SerializeField] private AudioClip _sfxTreasureDig;
        [Tooltip("Doubloons / loot salvage jingle.")]
        [SerializeField] private AudioClip _sfxLootCollect;

        [Header("Volume Balances")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float _musicVolume = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float _sfxVolume = 0.85f;

        // Dedicated Runtime AudioSources
        private AudioSource _musicSource;
        private AudioSource _ambientSource;
        private AudioSource _sfxSource;
        private AudioSource _uiSource;

        // Procedural Audio Fallback Cache (Generated once on demand)
        private AudioClip _procBGMMainMenu;
        private AudioClip _procBGMVoyage;
        private AudioClip _procCannon;
        private AudioClip _procHit;
        private AudioClip _procBell;
        private AudioClip _procCreak;
        private AudioClip _procWaves;
        private AudioClip _procClick;
        private AudioClip _procDig;
        private AudioClip _procLoot;

        public float MasterVolume => _masterVolume;
        public float MusicVolume => _musicVolume;
        public float SFXVolume => _sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumePreferences();
            SetupAudioSources();
            EnsureAudioClips();
        }

        private void Start()
        {
            ApplyVolumes();
        }

        private void SetupAudioSources()
        {
            // 1. Music AudioSource (Looping)
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;
            _musicSource.spatialBlend = 0f; // 2D Stereo

            // 2. Ambient Ocean Waves / Creaks AudioSource (Looping)
            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.spatialBlend = 0f;

            // 3. One-Shot SFX AudioSource
            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
            _sfxSource.spatialBlend = 0f;

            // 4. UI AudioSource
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.loop = false;
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

        }

        private float _nextCreakTimer = 8f;

        private void Update()
        {
            _nextCreakTimer -= Time.deltaTime;
            if (_nextCreakTimer <= 0f)
            {
                _nextCreakTimer = UnityEngine.Random.Range(10f, 22f);
                if ((_musicSource != null && _musicSource.isPlaying) || (_ambientSource != null && _ambientSource.isPlaying))
                {
                    PlaySFX(SFXType.WoodCreak, 0.12f);
                }
            }
        }

        private void EnsureAudioClips()
        {
            if (_sfxCannonFire == null) _sfxCannonFire = Resources.Load<AudioClip>("sfx/cannon-soundbible_5xh1Wvy");
            if (_sfxShipHit == null) _sfxShipHit = Resources.Load<AudioClip>("sfx/wood-breaking");
            if (_sfxWoodCreak == null) _sfxWoodCreak = Resources.Load<AudioClip>("sfx/wooden-floor-creaks");
            if (_sfxBell == null) _sfxBell = Resources.Load<AudioClip>("sfx/yeat-bell");
            if (_sfxWaveLapping == null) _sfxWaveLapping = Resources.Load<AudioClip>("sfx/copy-of-ocean-wave-1-online-audio-converter");
            if (_bgmVoyage == null) _bgmVoyage = Resources.Load<AudioClip>("sfx/Golden Echo");
            if (_bgmMainMenu == null) _bgmMainMenu = Resources.Load<AudioClip>("sfx/Golden Echo");
            if (_sfxUIClick == null) _sfxUIClick = Resources.Load<AudioClip>("sfx/click-a");

#if UNITY_EDITOR
            if (_sfxCannonFire == null) _sfxCannonFire = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/cannon-soundbible_5xh1Wvy.mp3");
            if (_sfxShipHit == null) _sfxShipHit = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/wood-breaking.mp3");
            if (_sfxWoodCreak == null) _sfxWoodCreak = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/wooden-floor-creaks.mp3");
            if (_sfxBell == null) _sfxBell = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/yeat-bell.mp3");
            if (_sfxWaveLapping == null) _sfxWaveLapping = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/copy-of-ocean-wave-1-online-audio-converter.mp3");
            if (_bgmVoyage == null) _bgmVoyage = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/Golden Echo.mp3");
            if (_bgmMainMenu == null) _bgmMainMenu = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/sfx/Golden Echo.mp3");
            if (_sfxUIClick == null) _sfxUIClick = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/kenney_ui-pack/Sounds/click-a.ogg");
#endif

            PreloadClip(_sfxCannonFire);
            PreloadClip(_sfxShipHit);
            PreloadClip(_sfxWoodCreak);
            PreloadClip(_sfxBell);
            PreloadClip(_sfxWaveLapping);
            PreloadClip(_bgmVoyage);
            PreloadClip(_bgmMainMenu);
            PreloadClip(_sfxUIClick);
        }

        private void PreloadClip(AudioClip clip)
        {
            if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }
        }

        private void LoadVolumePreferences()
        {
            _masterVolume = PlayerPrefs.GetFloat(PrefMasterVol, 1.0f);
            _musicVolume = PlayerPrefs.GetFloat(PrefMusicVol, 0.75f);
            _sfxVolume = PlayerPrefs.GetFloat(PrefSfxVol, 0.85f);
        }

        public void SetMasterVolume(float vol)
        {
            _masterVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat(PrefMasterVol, _masterVolume);
            ApplyVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            _musicVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat(PrefMusicVol, _musicVolume);
            ApplyVolumes();
        }

        public void SetSFXVolume(float vol)
        {
            _sfxVolume = Mathf.Clamp01(vol);
            PlayerPrefs.SetFloat(PrefSfxVol, _sfxVolume);
            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            if (_musicSource != null) _musicSource.volume = _masterVolume * _musicVolume;
            if (_ambientSource != null) _ambientSource.volume = _masterVolume * _sfxVolume * 0.45f;
            if (_sfxSource != null) _sfxSource.volume = _masterVolume * _sfxVolume;
            if (_uiSource != null) _uiSource.volume = _masterVolume * _sfxVolume * 0.9f;
        }

        // =====================================================================
        // PUBLIC PLAYBACK API
        // =====================================================================

        public void PlayMainMenuMusic()
        {
            AudioClip clip = _bgmMainMenu != null ? _bgmMainMenu : GetOrCreateProcBGMMainMenu();
            if (_musicSource.clip != clip || !_musicSource.isPlaying)
            {
                _musicSource.clip = clip;
                _musicSource.Play();
            }

            // Start soft ambient harbor surf
            AudioClip ambClip = _sfxWaveLapping != null ? _sfxWaveLapping : GetOrCreateProcWaves();
            if (_ambientSource.clip != ambClip || !_ambientSource.isPlaying)
            {
                _ambientSource.clip = ambClip;
                _ambientSource.Play();
            }
        }

        public void PlayVoyageMusic()
        {
            AudioClip clip = _bgmVoyage != null ? _bgmVoyage : GetOrCreateProcBGMVoyage();
            if (_musicSource.clip != clip || !_musicSource.isPlaying)
            {
                _musicSource.clip = clip;
                _musicSource.Play();
            }

            AudioClip ambClip = _sfxWaveLapping != null ? _sfxWaveLapping : GetOrCreateProcWaves();
            if (_ambientSource.clip != ambClip || !_ambientSource.isPlaying)
            {
                _ambientSource.clip = ambClip;
                _ambientSource.Play();
            }
        }

        public void StopMusic()
        {
            if (_musicSource != null) _musicSource.Stop();
        }

        public void PlaySFX(SFXType type, float pitchVariance = 0.08f)
        {
            AudioClip clip = GetAudioClipForType(type);
            if (clip == null) return;

            if (clip.loadState != AudioDataLoadState.Loaded)
            {
                clip.LoadAudioData();
            }

            if (_sfxSource != null)
            {
                _sfxSource.pitch = 1.0f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
                _sfxSource.PlayOneShot(clip, 1.0f);
            }
        }

        public void PlayUIClick()
        {
            AudioClip clip = _sfxUIClick != null ? _sfxUIClick : GetOrCreateProcClick();
            if (_uiSource != null && clip != null)
            {
                _uiSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
                _uiSource.PlayOneShot(clip, _masterVolume * _sfxVolume);
            }
        }

        private AudioClip GetAudioClipForType(SFXType type)
        {
            switch (type)
            {
                case SFXType.UIClick:
                    return _sfxUIClick != null ? _sfxUIClick : GetOrCreateProcClick();
                case SFXType.CannonFire:
                    return _sfxCannonFire != null ? _sfxCannonFire : GetOrCreateProcCannon();
                case SFXType.ShipHit:
                    return _sfxShipHit != null ? _sfxShipHit : GetOrCreateProcHit();
                case SFXType.ShipBell:
                    return _sfxBell != null ? _sfxBell : GetOrCreateProcBell();
                case SFXType.WoodCreak:
                    return _sfxWoodCreak != null ? _sfxWoodCreak : GetOrCreateProcCreak();
                case SFXType.WaveSplash:
                    return _sfxWaveLapping != null ? _sfxWaveLapping : GetOrCreateProcWaves();
                case SFXType.TreasureDig:
                    return _sfxTreasureDig != null ? _sfxTreasureDig : GetOrCreateProcDig();
                case SFXType.LootCollect:
                    return _sfxLootCollect != null ? _sfxLootCollect : GetOrCreateProcLoot();
                default:
                    return null;
            }
        }

        // =====================================================================
        // PROCEDURAL SYNTHETIC NAUTICAL AUDIO GENERATORS (FALLBACKS)
        // High-quality PCM synthesis with zero external dependencies
        // =====================================================================

        private AudioClip GetOrCreateProcClick()
        {
            if (_procClick != null) return _procClick;
            const int sampleRate = 44100;
            const float duration = 0.06f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 90f);
                float tone = Mathf.Sin(2f * Mathf.PI * 720f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 1440f * t) * 0.3f;
                data[i] = tone * decay * 0.45f;
            }

            _procClick = AudioClip.Create("Proc_UIClick", numSamples, 1, sampleRate, false);
            _procClick.SetData(data, 0);
            return _procClick;
        }

        private AudioClip GetOrCreateProcCannon()
        {
            if (_procCannon != null) return _procCannon;
            const int sampleRate = 44100;
            const float duration = 1.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            float phase = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Explosive pitch drop from 160Hz down to 35Hz
                float freq = Mathf.Lerp(160f, 35f, Mathf.Pow(t / duration, 0.4f));
                phase += 2f * Mathf.PI * freq / sampleRate;

                float envelope = Mathf.Exp(-t * 3.8f);
                float subBass = Mathf.Sin(phase);
                // Heavy clipping distortion for cannon punch
                float punch = Mathf.Clamp(subBass * 3.2f, -1f, 1f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 6.5f) * 0.4f;

                data[i] = (punch * 0.75f + noise) * envelope * 0.85f;
            }

            _procCannon = AudioClip.Create("Proc_CannonFire", numSamples, 1, sampleRate, false);
            _procCannon.SetData(data, 0);
            return _procCannon;
        }

        private AudioClip GetOrCreateProcHit()
        {
            if (_procHit != null) return _procHit;
            const int sampleRate = 44100;
            const float duration = 0.8f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 5.2f);
                float crack = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 18f);
                float woodResonance = Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 260f * t) * 0.3f;
                data[i] = (crack * 0.6f + woodResonance * 0.4f) * decay * 0.7f;
            }

            _procHit = AudioClip.Create("Proc_ShipHit", numSamples, 1, sampleRate, false);
            _procHit.SetData(data, 0);
            return _procHit;
        }

        private AudioClip GetOrCreateProcBell()
        {
            if (_procBell != null) return _procBell;
            const int sampleRate = 44100;
            const float duration = 1.9f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 2.4f);
                float bell = Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.5f
                           + Mathf.Sin(2f * Mathf.PI * 2093.0f * t) * 0.3f
                           + Mathf.Sin(2f * Mathf.PI * 3135.0f * t) * 0.15f
                           + Mathf.Sin(2f * Mathf.PI * 520.0f * t) * 0.2f;
                data[i] = bell * decay * 0.55f;
            }

            _procBell = AudioClip.Create("Proc_ShipBell", numSamples, 1, sampleRate, false);
            _procBell.SetData(data, 0);
            return _procBell;
        }

        private AudioClip GetOrCreateProcCreak()
        {
            if (_procCreak != null) return _procCreak;
            const int sampleRate = 44100;
            const float duration = 1.6f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            float phase = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
                // Pitch modulated groaning timber
                float freq = 95f + Mathf.Sin(2f * Mathf.PI * 4.5f * t) * 22f;
                phase += 2f * Mathf.PI * freq / sampleRate;
                float tone = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.4f;
                float grit = (UnityEngine.Random.value * 2f - 1f) * 0.12f;
                data[i] = (tone * 0.7f + grit) * env * 0.4f;
            }

            _procCreak = AudioClip.Create("Proc_WoodCreak", numSamples, 1, sampleRate, false);
            _procCreak.SetData(data, 0);
            return _procCreak;
        }

        private AudioClip GetOrCreateProcWaves()
        {
            if (_procWaves != null) return _procWaves;
            const int sampleRate = 44100;
            const float duration = 4.5f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            float lastNoise = 0f;
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Periodic wave swell envelope (smooth swell peak around t = 1.8s)
                float swell = Mathf.Sin((t / duration) * Mathf.PI);
                swell = Mathf.Pow(Mathf.Max(0f, swell), 1.5f);

                // Low-pass filtered pink/brown noise
                float raw = UnityEngine.Random.value * 2f - 1f;
                lastNoise = Mathf.Lerp(lastNoise, raw, 0.08f);

                data[i] = lastNoise * swell * 0.45f;
            }

            _procWaves = AudioClip.Create("Proc_WaveLapping", numSamples, 1, sampleRate, false);
            _procWaves.SetData(data, 0);
            return _procWaves;
        }

        private AudioClip GetOrCreateProcDig()
        {
            if (_procDig != null) return _procDig;
            const int sampleRate = 44100;
            const float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 12f);
                float sandGrit = (UnityEngine.Random.value * 2f - 1f) * 0.65f;
                float shovelImpact = Mathf.Sin(2f * Mathf.PI * 140f * t) * 0.4f;
                data[i] = (sandGrit + shovelImpact) * decay * 0.5f;
            }

            _procDig = AudioClip.Create("Proc_TreasureDig", numSamples, 1, sampleRate, false);
            _procDig.SetData(data, 0);
            return _procDig;
        }

        private AudioClip GetOrCreateProcLoot()
        {
            if (_procLoot != null) return _procLoot;
            const int sampleRate = 44100;
            const float duration = 0.45f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                // Double coin jingle chimes (1320Hz then 1760Hz)
                float chime = 0f;
                if (t < 0.2f)
                {
                    chime = Mathf.Sin(2f * Mathf.PI * 1320f * t) * Mathf.Exp(-t * 14f);
                }
                else
                {
                    float t2 = t - 0.16f;
                    chime = Mathf.Sin(2f * Mathf.PI * 1760f * t2) * Mathf.Exp(-t2 * 12f);
                }
                data[i] = chime * 0.5f;
            }

            _procLoot = AudioClip.Create("Proc_LootCollect", numSamples, 1, sampleRate, false);
            _procLoot.SetData(data, 0);
            return _procLoot;
        }

        private AudioClip GetOrCreateProcBGMMainMenu()
        {
            if (_procBGMMainMenu != null) return _procBGMMainMenu;
            const int sampleRate = 22050; // Optimized sample rate for memory
            const float duration = 16.0f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            // 17th-Century Nautical Progression in A-Minor:
            // Measures (2.0s each): Am -> Dm -> F -> Em -> Am -> Dm -> G -> Am
            float[] rootFreqs = { 220.0f, 146.83f, 174.61f, 164.81f, 220.0f, 146.83f, 196.0f, 220.0f };
            float[] melodyNotes = {
                // Measure 1 (Am)
                220f, 261.63f, 329.63f, 440f,
                // Measure 2 (Dm)
                293.66f, 349.23f, 440f, 349.23f,
                // Measure 3 (F)
                349.23f, 440f, 523.25f, 440f,
                // Measure 4 (Em)
                329.63f, 392f, 329.63f, 261.63f,
                // Measure 5 (Am)
                220f, 329.63f, 440f, 523.25f,
                // Measure 6 (Dm)
                587.33f, 523.25f, 440f, 349.23f,
                // Measure 7 (G)
                392f, 440f, 493.88f, 392f,
                // Measure 8 (Am)
                440f, 329.63f, 261.63f, 220f
            };

            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;

                // Bass Drone
                int measure = Mathf.Clamp((int)(t / 2.0f), 0, 7);
                float bassFreq = rootFreqs[measure] * 0.5f;
                float bass = Mathf.Sin(2f * Mathf.PI * bassFreq * t) * 0.22f;

                // Acoustic Flute/Whistle Melody
                int noteIdx = Mathf.Clamp((int)(t / 0.5f), 0, 31);
                float noteFreq = melodyNotes[noteIdx];
                float noteTime = t % 0.5f;
                float noteEnv = Mathf.Sin(Mathf.Clamp01(noteTime / 0.5f) * Mathf.PI);

                // Flute harmonics (warm sine + gentle 2nd harmonic + vibrato)
                float vibrato = Mathf.Sin(2f * Mathf.PI * 5.0f * t) * 2.5f;
                float flute = Mathf.Sin(2f * Mathf.PI * (noteFreq + vibrato) * t) * 0.28f
                            + Mathf.Sin(2f * Mathf.PI * (noteFreq * 2f) * t) * 0.08f;

                data[i] = (bass + flute * noteEnv) * 0.55f;
            }

            _procBGMMainMenu = AudioClip.Create("Proc_BGM_MainMenu", numSamples, 1, sampleRate, false);
            _procBGMMainMenu.SetData(data, 0);
            return _procBGMMainMenu;
        }

        private AudioClip GetOrCreateProcBGMVoyage()
        {
            if (_procBGMVoyage != null) return _procBGMVoyage;
            // Shorter 8.0s heroic sea shanty motif for voyage
            const int sampleRate = 22050;
            const float duration = 8.0f;
            int numSamples = (int)(sampleRate * duration);
            float[] data = new float[numSamples];

            float[] melody = { 220f, 277.18f, 329.63f, 440f, 493.88f, 440f, 329.63f, 277.18f };
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                int noteIdx = Mathf.Clamp((int)(t / 1.0f), 0, 7);
                float freq = melody[noteIdx];
                float noteTime = t % 1.0f;
                float env = Mathf.Sin(Mathf.Clamp01(noteTime / 1.0f) * Mathf.PI);
                float horn = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.25f + Mathf.Sin(2f * Mathf.PI * freq * 2f * t) * 0.1f;
                data[i] = horn * env * 0.45f;
            }

            _procBGMVoyage = AudioClip.Create("Proc_BGM_Voyage", numSamples, 1, sampleRate, false);
            _procBGMVoyage.SetData(data, 0);
            return _procBGMVoyage;
        }
    }
}
