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

    [Header("인스펙터 정품 오디오 클립 등록 (Audio Clips)")]
    [SerializeField] private AudioClip throwWhooshClip;
    [SerializeField] private AudioClip bounceWaterClip;
    [SerializeField] private AudioClip bounceGoodClip;
    [SerializeField] private AudioClip bouncePerfectClip;
    [SerializeField] private AudioClip skimSlideClip;
    [SerializeField] private AudioClip boostPadClip;
    [SerializeField] private AudioClip coinJingleClip;
    [SerializeField] private AudioClip stoneSinkClip;
    [SerializeField] private AudioClip buttonClickClip;
    [SerializeField] private AudioClip bgmMusicClip;

    // BGM 오디오 소스는 런타임에 자동 생성/바인딩
    [HideInInspector]
    [SerializeField] private AudioSource bgmSource;

    private Dictionary<SoundType, AudioClip> _clipCache = new Dictionary<SoundType, AudioClip>();
    private List<AudioSource> _sourcePool = new List<AudioSource>();
    private const int PoolSize = 10;
    private int _poolIndex = 0;

    private void Reset()
    {
        AutoFillClipsFromResources();
    }

    private void OnValidate()
    {
        AutoFillClipsFromResources();
    }

    public void AutoFillClipsFromResources()
    {
#if UNITY_EDITOR
        if (throwWhooshClip == null) throwWhooshClip = Resources.Load<AudioClip>("Audio/Throw_Whoosh");
        if (bounceWaterClip == null) bounceWaterClip = Resources.Load<AudioClip>("Audio/Bounce_Water");
        if (bounceGoodClip == null) bounceGoodClip = Resources.Load<AudioClip>("Audio/Bounce_Good");
        if (bouncePerfectClip == null) bouncePerfectClip = Resources.Load<AudioClip>("Audio/Bounce_Perfect");
        if (skimSlideClip == null) skimSlideClip = Resources.Load<AudioClip>("Audio/Skim_Slide");
        if (boostPadClip == null) boostPadClip = Resources.Load<AudioClip>("Audio/Boost_Pad");
        if (coinJingleClip == null) coinJingleClip = Resources.Load<AudioClip>("Audio/Coin_Jingle");
        if (stoneSinkClip == null) stoneSinkClip = Resources.Load<AudioClip>("Audio/Stone_Sink");
        if (buttonClickClip == null) buttonClickClip = Resources.Load<AudioClip>("Audio/Button_Click");
        if (bgmMusicClip == null) bgmMusicClip = Resources.Load<AudioClip>("Audio/alex-morgan-acoustic-guitar-sunrise-travel");
#endif
    }

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
        // 1. 인스펙터에 직접 할당된 클립 우선 등록
        RegisterInspectorClip(SoundType.ThrowWhoosh, throwWhooshClip, "Throw_Whoosh");
        RegisterInspectorClip(SoundType.BounceWater, bounceWaterClip, "Bounce_Water");
        RegisterInspectorClip(SoundType.BounceGood, bounceGoodClip, "Bounce_Good");
        RegisterInspectorClip(SoundType.BouncePerfect, bouncePerfectClip, "Bounce_Perfect");
        RegisterInspectorClip(SoundType.SkimSlide, skimSlideClip, "Skim_Slide");
        RegisterInspectorClip(SoundType.BoostPad, boostPadClip, "Boost_Pad");
        RegisterInspectorClip(SoundType.CoinJingle, coinJingleClip, "Coin_Jingle");
        RegisterInspectorClip(SoundType.StoneSink, stoneSinkClip, "Stone_Sink");
        RegisterInspectorClip(SoundType.ButtonClick, buttonClickClip, "Button_Click");

        // 2. BGM 소스 초기화
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
        }

        if (bgmMusicClip != null)
        {
            bgmSource.clip = bgmMusicClip;
        }
        else
        {
            AudioClip resBgm = Resources.Load<AudioClip>("Audio/BGM_Main");
            if (resBgm != null) bgmSource.clip = resBgm;
        }
    }

    private void RegisterInspectorClip(SoundType type, AudioClip inspectorClip, string resourceName)
    {
        if (inspectorClip != null)
        {
            _clipCache[type] = inspectorClip;
            return;
        }

        AudioClip resClip = Resources.Load<AudioClip>("Audio/" + resourceName);
        if (resClip != null)
        {
            _clipCache[type] = resClip;
            return;
        }

        // 마지막 폴백: 프로시저럴 합성
        _clipCache[type] = CreateProceduralFallback(type);
    }

    public void PlayBGM(AudioClip clip = null, float bpm = 60f)
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
            bgmSource.spatialBlend = 0f;
        }

        if (clip != null)
        {
            bgmSource.clip = clip;
        }
        else if (bgmSource.clip == null)
        {
            if (bgmMusicClip != null) bgmSource.clip = bgmMusicClip;
            else bgmSource.clip = Resources.Load<AudioClip>("Audio/alex-morgan-acoustic-guitar-sunrise-travel") ?? Resources.Load<AudioClip>("Audio/BGM_Main");
        }

        if (bgmSource.clip != null)
        {
            if (_bgmFadeCoroutine != null)
            {
                StopCoroutine(_bgmFadeCoroutine);
                _bgmFadeCoroutine = null;
            }
            bgmSource.volume = masterVolume * 0.7f;
            bgmSource.pitch = 1.0f; // 🎵 디렉터 확정: BGM 원곡 100% 정상 속도로 재생
            bgmSource.Play();
        }
    }

    public bool IsBGMPlaying()
    {
        return bgmSource != null && bgmSource.isPlaying;
    }

    private Coroutine _bgmFadeCoroutine;

    public void StopBGMFadeOut(float duration = 3.0f)
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            if (_bgmFadeCoroutine != null) StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = StartCoroutine(CoFadeOutBGM(duration));
        }
    }

    private System.Collections.IEnumerator CoFadeOutBGM(float duration)
    {
        if (bgmSource == null) yield break;

        float startVolume = bgmSource.volume;
        float elapsed = 0f;

        while (elapsed < duration && bgmSource != null && bgmSource.isPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.volume = startVolume;
        }
        _bgmFadeCoroutine = null;
    }

    public void SetBGMPitchByBPM(float targetBPM, float baseBPM = 60f)
    {
        if (bgmSource != null && bgmSource.isPlaying)
        {
            // 디렉터 요청: 콤보 가속 잠금, 원곡 1.0x 피치 항시 유지
            bgmSource.pitch = 1.0f;
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
    /// 모바일 100% 호환 Play 사운드 재생 (간편 호출)
    /// </summary>
    public void Play(SoundType type, float volumeScale = 1f)
    {
        PlaySound(type, volumeScale);
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
