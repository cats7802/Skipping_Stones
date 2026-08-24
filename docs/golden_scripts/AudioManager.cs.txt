using System.Collections.Generic;
using UnityEngine;

public enum SoundType
{
    ThrowWhoosh,
    BounceWater,
    BounceGood,
    BouncePerfect,
    SkimSlide,
    BoostPad,
    CoinJingle,
    StoneSink,
    ButtonClick
}

public class AudioManager : MonoBehaviour
{
    private static AudioManager _instance;
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Object.FindAnyObjectByType<AudioManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("AudioManager");
                    _instance = go.AddComponent<AudioManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance == null)
        {
            var inst = Instance;
            Debug.Log("🎵 [AudioManager] 게임 시작 전 오디오 시스템 자동 초기화 완료!");
        }
    }

    [Header("Audio Settings")]
    [Range(0f, 1f)] public float masterVolume = 1.0f;
    [Range(0f, 1f)] public float sfxVolume = 1.0f;

    private Dictionary<SoundType, AudioClip> _clipCache = new Dictionary<SoundType, AudioClip>();
    private List<AudioSource> _sourcePool = new List<AudioSource>();
    private const int PoolSize = 10;
    private int _poolIndex = 0;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSystem();
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 모바일/PC 오디오 리스너 볼륨 보장 및 단일 리스너 체크
        EnsureSingleAudioListener();
    }

    private void EnsureSingleAudioListener()
    {
        AudioListener.volume = 1.0f;
        AudioListener.pause = false;

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
        if (listeners == null || listeners.Length == 0)
        {
            if (Camera.main != null)
            {
                Camera.main.gameObject.AddComponent<AudioListener>();
            }
            else
            {
                gameObject.AddComponent<AudioListener>();
            }
        }
        else if (listeners.Length > 1)
        {
            // 메인 카메라 리스너만 살리고 나머지는 비활성화
            bool mainKept = false;
            foreach (var l in listeners)
            {
                if (!mainKept && (l.CompareTag("MainCamera") || l.gameObject.name.Contains("Main")))
                {
                    l.enabled = true;
                    mainKept = true;
                }
                else if (mainKept)
                {
                    l.enabled = false;
                }
            }
        }
    }

    private void InitializeAudioSystem()
    {
        // 1. AudioSource 풀링 인스턴스 생성 (2D Direct Output)
        for (int i = 0; i < PoolSize; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D 직접 출력
            source.volume = 1f;
            _sourcePool.Add(source);
        }

        // 2. Resources/Audio/ 폴더에서 AudioClip 로드 (없을 시 프로시저럴 생성 폴백)
        LoadOrSynthesizeClips();
    }

    private void LoadOrSynthesizeClips()
    {
        LoadClip(SoundType.ThrowWhoosh, "Throw_Whoosh");
        LoadClip(SoundType.BounceWater, "Bounce_Water");
        LoadClip(SoundType.BounceGood, "Bounce_Good");
        LoadClip(SoundType.BouncePerfect, "Bounce_Perfect");
        LoadClip(SoundType.SkimSlide, "Skim_Slide");
        LoadClip(SoundType.BoostPad, "Boost_Pad");
        LoadClip(SoundType.CoinJingle, "Coin_Jingle");
        LoadClip(SoundType.StoneSink, "Stone_Sink");
        LoadClip(SoundType.ButtonClick, "Button_Click");
    }

    private void LoadClip(SoundType type, string resourceName)
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/" + resourceName);
        if (clip != null)
        {
            _clipCache[type] = clip;
        }
        else
        {
            // Fallback: 런타임 메모리 프로시저럴 합성
            _clipCache[type] = CreateProceduralFallback(type);
        }
    }

    private AudioClip CreateProceduralFallback(SoundType type)
    {
        float[] samples;
        string clipName;

        switch (type)
        {
            case SoundType.ThrowWhoosh:
                samples = SoundSynthesizer.GenerateWhoosh(0.35f);
                clipName = "Procedural_ThrowWhoosh";
                break;
            case SoundType.BounceGood:
                samples = SoundSynthesizer.GenerateGoodBounce(0.35f);
                clipName = "Procedural_BounceGood";
                break;
            case SoundType.BouncePerfect:
                samples = SoundSynthesizer.GeneratePerfectChime(0.6f);
                clipName = "Procedural_BouncePerfect";
                break;
            case SoundType.SkimSlide:
                samples = SoundSynthesizer.GenerateSkimSlide(0.4f);
                clipName = "Procedural_SkimSlide";
                break;
            case SoundType.BoostPad:
                samples = SoundSynthesizer.GenerateBoostPad(0.5f);
                clipName = "Procedural_BoostPad";
                break;
            case SoundType.CoinJingle:
                samples = SoundSynthesizer.GenerateCoinJingle(0.45f);
                clipName = "Procedural_CoinJingle";
                break;
            case SoundType.StoneSink:
                samples = SoundSynthesizer.GenerateStoneSink(0.5f);
                clipName = "Procedural_StoneSink";
                break;
            case SoundType.ButtonClick:
                samples = SoundSynthesizer.GenerateButtonClick(0.08f);
                clipName = "Procedural_ButtonClick";
                break;
            case SoundType.BounceWater:
            default:
                samples = SoundSynthesizer.GenerateWaterBounce(0.3f);
                clipName = "Procedural_BounceWater";
                break;
        }

        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, 44100, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// 모바일 100% 호환 PlayOneShot 사운드 재생
    /// </summary>
    public void PlaySound(SoundType type, float volumeScale = 1f, float pitchVariation = 0.05f)
    {
        if (!_clipCache.TryGetValue(type, out AudioClip clip) || clip == null)
        {
            return;
        }

        AudioSource source = GetNextAudioSource();
        float finalVolume = Mathf.Clamp01(masterVolume * sfxVolume * volumeScale);
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        source.PlayOneShot(clip, finalVolume);
    }

    private AudioSource GetNextAudioSource()
    {
        AudioSource source = _sourcePool[_poolIndex];
        _poolIndex = (_poolIndex + 1) % _sourcePool.Count;
        return source;
    }
}
