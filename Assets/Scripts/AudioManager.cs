using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; } = null!;



    private AudioSource[] seSources = null!;
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private UnityEngine.Audio.AudioMixer audioMixer;
    private Tween masterHighPassTween;

    public void SetMasterHighPassFilter(float cutoffFrequency, float duration = 0.2f)
    {
        if (audioMixer != null)
        {
            masterHighPassTween?.Kill();
            audioMixer.GetFloat("MasterHighPass", out float currentVal);

            masterHighPassTween = DOVirtual.Float(currentVal, cutoffFrequency, duration, val =>
            {
                audioMixer.SetFloat("MasterHighPass", val);
            }).SetLink(gameObject);
        }
    }

    private Tween masterLowPassTween;
    private Tween masterChorusDepthTween;

    private float _basePitch = 1.0f;
    private float _effectPitchOffset = 0.0f;
    private Tween _basePitchTween;
    private Tween _effectPitchTween;

    private void ApplyPitch()
    {
        if (audioMixer != null)
        {
            audioMixer.SetFloat("MasterPitch", _basePitch + _effectPitchOffset);
        }
    }

    public void SetMasterLowPassFilter(float cutoffFrequency, float duration = 2.0f)
    {
        if (audioMixer != null)
        {
            masterLowPassTween?.Kill();
            audioMixer.GetFloat("MasterLowPass", out float currentVal);

            masterLowPassTween = DOVirtual.Float(currentVal, cutoffFrequency, duration, val =>
            {
                audioMixer.SetFloat("MasterLowPass", val);
            }).SetLink(gameObject);
        }
    }

    public void SetMasterChorusDepth(float depth, float duration = 0.2f)
    {
        if (audioMixer != null)
        {
            masterChorusDepthTween?.Kill();
            audioMixer.GetFloat("MasterChorusDepth", out float currentVal);

            masterChorusDepthTween = DOVirtual.Float(currentVal, depth, duration, val =>
            {
                audioMixer.SetFloat("MasterChorusDepth", val);
            }).SetLink(gameObject);
        }
    }

    public void SetMasterPitch(float pitch, float duration = 2.0f)
    {
        _basePitchTween?.Kill();
        // Smoothly transition base pitch
        _basePitchTween = DOVirtual.Float(_basePitch, pitch, duration, val =>
        {
            _basePitch = val;
            ApplyPitch();
        }).SetLink(gameObject);
    }

    public void TriggerSuperpositionEffect()
    {
        // 3秒間ChorusDepthを1にし、その後0.03に戻す
        SetMasterChorusDepth(1.0f, 0.5f);
        DOVirtual.DelayedCall(3.0f, () =>
        {
            SetMasterChorusDepth(0.03f, 2.0f);
        }).SetLink(gameObject);
    }

    private Tween masterFreqGainTween;

    public void SetMasterFreqGain(float gain, float duration = 2.0f)
    {
        if (audioMixer != null)
        {
            masterFreqGainTween?.Kill();
            audioMixer.GetFloat("MasterFreqGain", out float currentVal);

            masterFreqGainTween = DOVirtual.Float(currentVal, gain, duration, val =>
            {
                audioMixer.SetFloat("MasterFreqGain", val);
            }).SetLink(gameObject);
        }
    }

    private Tween masterRoomTween;

    public void SetMasterRoom(float room, float duration = 0.0f)
    {
        if (audioMixer != null)
        {
            masterRoomTween?.Kill();
            audioMixer.GetFloat("MasterRoom", out float currentVal);

            masterRoomTween = DOVirtual.Float(currentVal, room, duration, val =>
            {
                audioMixer.SetFloat("MasterRoom", val);
            }).SetLink(gameObject);
        }
    }

    public void TriggerStageChangeEffect()
    {
        SetMasterRoom(0, 0.2f);
        DOVirtual.DelayedCall(1f, () =>
        {
            SetMasterRoom(-10000.00f, 1f);
        }).SetLink(gameObject);
    }

    public enum SEType
    {
        attack,
        move,
        select,
        victory,
        reset,
        shake,
        tap,
        enabledWarp,
        cancel,
        decide,
        warp,
        superposition,
        rush,
        wipeout,
    }

    public enum BGMType
    {
        Title,
    }

    [System.Serializable]
    public class SE
    {
        public SEType type;
        public AudioClip clip = null!;
    }

    [System.Serializable]
    public class BGM
    {
        public BGMType type;
        public AudioClip clip = null!;
    }

    public SE[] seClips = null!;
    public BGM[] bgmClips = null!;

    private Dictionary<SEType, AudioClip> seDictionary = null!;
    private Dictionary<BGMType, AudioClip> bgmDictionary = null!;
    public float seVolume = 0.5f;
    public float bgmVolume = 0.5f;
    public float maxVolume = 1.0f;

    // デバウンス用：同じSEが短時間に重複再生されるのを防ぐ
    private Dictionary<SEType, float> lastPlayedTime = new Dictionary<SEType, float>();
    [SerializeField] private float debounceTime = 0.05f; // 50ms以内の重複を防ぐ

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeAudioSources();
            InitializeDictionaries();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (bgmClips.Length > 0)
        {
            PlayBGM(bgmClips[0].type);
        }
    }

    private void InitializeAudioSources()
    {
        print("InitializeAudioSources");
        seSources = new AudioSource[10];
        for (int i = 0; i < seSources.Length; i++)
        {
            seSources[i] = gameObject.AddComponent<AudioSource>();
            seSources[i].volume = seVolume / maxVolume;
        }

        //bgmSource = gameObject.AddComponent<AudioSource>();
        //bgmSource.volume = GetBGMVolume();
        bgmSource.volume = 1;
        bgmSource.loop = true;
    }

    private void InitializeDictionaries()
    {
        seDictionary = new Dictionary<SEType, AudioClip>();
        foreach (var se in seClips)
        {
            seDictionary[se.type] = se.clip;
        }

        bgmDictionary = new Dictionary<BGMType, AudioClip>();
        foreach (var bgm in bgmClips)
        {
            bgmDictionary[bgm.type] = bgm.clip;
        }
    }

    public void SetSEVolume(float volume)
    {
        float vol = volume * volume / maxVolume;
        foreach (var source in seSources)
        {
            source.volume = vol;
        }
    }

    public void SetBGMVolume(float volume)
    {
        bgmSource.volume = volume * volume / maxVolume;
    }

    public void PlaySE(SEType seType)
    {
        // デバウンスチェック：短時間に同じSEが再生されるのを防ぐ
        float currentTime = Time.time;
        if (lastPlayedTime.TryGetValue(seType, out float lastTime))
        {
            if (currentTime - lastTime < debounceTime)
            {
                // デバウンス期間内なのでスキップ
                return;
            }
        }

        if (!seDictionary.TryGetValue(seType, out var clip))
        {
            Debug.LogWarning($"SE clip for {seType} not found.");
            return;
        }

        foreach (var source in seSources)
        {
            if (!source.isPlaying)
            {
                source.clip = clip;
                source.Play();

                // 再生時刻を記録
                lastPlayedTime[seType] = currentTime;
                return;
            }
        }
    }

    public void PlayBGM(BGMType bgmType)
    {
        if (!bgmDictionary.TryGetValue(bgmType, out var clip))
        {
            Debug.LogWarning($"BGM clip for {bgmType} not found.");
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }
}