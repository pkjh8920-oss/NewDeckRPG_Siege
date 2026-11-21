// ============================================================
// SoundManager.cs  (3-190 사운드)
// 배경음(BGM)과 효과음(SFX)을 내는 곳. 어디서든 SoundManager.Play("Hit") 처럼 부르면 됨
//
// 소리 파일 넣는 곳 (확장자 .wav .mp3 .ogg 아무거나, 이름만 맞으면 됨)
//   Assets/Resources/Sound/BGM/Title.ogg   ← 배경음
//   Assets/Resources/Sound/SFX/Hit.wav     ← 효과음
//   파일이 없으면 아무 소리 없이 그냥 넘어감 (에러 안 남) → 파일을 하나씩 늘려 가도 됨
//
// 같은 효과음을 여러 개 넣으면 무작위로 하나: Hit.wav, Hit_2.wav, Hit_3.wav ... (매번 조금 다르게)
//
// 볼륨: GameSettings 의 BgmVolume · SfxVolume (설정 화면 막대). 전체 볼륨은 AudioListener 가 따로 곱함
//
// 자바로 치면: static 메서드만 있는 유틸 클래스 + 안에서 쓰는 "소리 재생기" 객체 하나 (싱글턴)
//   AudioSource = 소리를 내는 스피커 하나 / AudioClip = 소리 파일 하나
//   배경음은 스피커 2개를 번갈아 써서, 곡이 바뀔 때 앞 곡은 작아지고 새 곡은 커짐 (크로스페이드)
// ============================================================

using System.Collections.Generic;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    private static SoundManager inst;

    private AudioSource bgmA, bgmB;        // 배경음 스피커 2개 (번갈아 씀)
    private AudioSource current;           // 지금 커지는(재생 중인) 쪽
    private string currentBgm = "";
    private const float FadeSpeed = 1.2f;  // 1초에 볼륨이 얼마나 변하는지 (약 0.8초 만에 바뀜)

    private const int SfxVoices = 10;      // 효과음 스피커 개수 (동시에 10개까지)
    private readonly List<AudioSource> sfx = new List<AudioSource>();
    private int nextVoice;

    // 불러온 파일 기억 (같은 파일을 매번 다시 불러오지 않게). 없는 파일은 빈 배열로 기억
    private static readonly Dictionary<string, AudioClip[]> sfxCache = new Dictionary<string, AudioClip[]>();
    private static readonly Dictionary<string, AudioClip> bgmCache = new Dictionary<string, AudioClip>();
    private static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    // 처음 부를 때 만들어짐 (씬에 미리 놓지 않아도 됨)
    static SoundManager Inst
    {
        get
        {
            if (inst == null)
            {
                GameObject go = new GameObject("SoundManager");
                DontDestroyOnLoad(go);   // 화면(씬)이 바뀌어도 안 사라짐
                inst = go.AddComponent<SoundManager>();
                inst.Init();
            }
            return inst;
        }
    }

    void Init()
    {
        bgmA = gameObject.AddComponent<AudioSource>();
        bgmB = gameObject.AddComponent<AudioSource>();
        foreach (AudioSource s in new[] { bgmA, bgmB }) { s.loop = true; s.playOnAwake = false; s.volume = 0f; }
        for (int i = 0; i < SfxVoices; i++)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            sfx.Add(s);
        }
        current = bgmA;
    }

    // ---------------- 배경음 ----------------
    // 이름이 지금 곡과 같으면 아무것도 안 함. "" 이면 서서히 꺼짐
    //   매 프레임 불러도 됨 (BattleScreen 이 지금 화면에 맞는 곡 이름을 계속 알려 줌)
    public static void Bgm(string name)
    {
        SoundManager m = Inst;
        if (name == null) name = "";
        if (name == m.currentBgm) return;
        m.currentBgm = name;
        AudioClip clip = LoadBgm(name);
        AudioSource next = m.current == m.bgmA ? m.bgmB : m.bgmA;
        if (clip == null) { m.current = next; next.Stop(); return; }   // 파일이 없으면 앞 곡만 서서히 꺼짐
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        m.current = next;
    }

    static AudioClip LoadBgm(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        AudioClip c;
        if (!bgmCache.TryGetValue(name, out c)) { c = Resources.Load<AudioClip>("Sound/BGM/" + name); bgmCache[name] = c; }
        return c;
    }

    void Update()
    {
        // 크로스페이드: 지금 곡은 목표 볼륨까지 커지고, 다른 쪽은 0 까지 작아짐
        //   unscaledDeltaTime = 히트스톱·일시정지(게임 속도 0)에도 소리는 정상으로 바뀜
        float dt = Time.unscaledDeltaTime;
        float target = GameSettings.BgmVolume;
        foreach (AudioSource s in new[] { bgmA, bgmB })
        {
            float want = (s == current && s.clip != null && s.isPlaying) ? target : 0f;
            s.volume = Mathf.MoveTowards(s.volume, want, dt * FadeSpeed);
            if (s != current && s.volume <= 0f && s.isPlaying) s.Stop();
        }
    }

    // ---------------- 효과음 ----------------
    // name 파일이 없으면 fallback 이름들을 차례로 찾음. 예) Play("HitHeavy", "Hit")
    //   같은 소리가 0.04초 안에 또 오면 무시 (여러 적을 한꺼번에 때릴 때 소리가 겹쳐 터지지 않게)
    public static void Play(string name, params string[] fallback)
    {
        SoundManager m = Inst;
        AudioClip[] clips = LoadSfx(name);
        string used = name;
        if (clips.Length == 0 && fallback != null)
            foreach (string f in fallback) { clips = LoadSfx(f); used = f; if (clips.Length > 0) break; }
        if (clips.Length == 0) return;

        float now = Time.unscaledTime;
        float last;
        if (lastPlayed.TryGetValue(used, out last) && now - last < 0.04f) return;
        lastPlayed[used] = now;

        AudioSource s = m.sfx[m.nextVoice];
        m.nextVoice = (m.nextVoice + 1) % m.sfx.Count;   // 돌아가며 씀 (가장 오래된 소리가 끊김)
        s.clip = clips[Random.Range(0, clips.Length)];
        s.pitch = Random.Range(0.95f, 1.05f);              // 높낮이를 살짝 바꿔서 같은 소리도 덜 지루하게
        s.volume = GameSettings.SfxVolume;
        s.Play();
    }

    // Hit, Hit_2, Hit_3 ... 을 모두 불러 둠
    static AudioClip[] LoadSfx(string name)
    {
        AudioClip[] arr;
        if (sfxCache.TryGetValue(name, out arr)) return arr;
        List<AudioClip> list = new List<AudioClip>();
        AudioClip first = Resources.Load<AudioClip>("Sound/SFX/" + name);
        if (first != null)
        {
            list.Add(first);
            for (int i = 2; i <= 9; i++)
            {
                AudioClip more = Resources.Load<AudioClip>("Sound/SFX/" + name + "_" + i);
                if (more == null) break;
                list.Add(more);
            }
        }
        arr = list.ToArray();
        sfxCache[name] = arr;
        return arr;
    }
}
