using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; } = null!;



    private AudioSource[] seSources = null!;
    private AudioSource bgmSource = null!;

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

    private void InitializeAudioSources()
    {
        print("InitializeAudioSources");
        seSources = new AudioSource[10];
        for (int i = 0; i < seSources.Length; i++)
        {
            seSources[i] = gameObject.AddComponent<AudioSource>();
            seSources[i].volume = seVolume / maxVolume;
        }

        bgmSource = gameObject.AddComponent<AudioSource>();
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

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }
}