using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Runs by itself (no need to attach it to anything). Every sound is generated in code, so no audio files are needed.
// Press N in play mode to turn the music on and off.
// Other scripts can call:  SoundManager.Play("coin");  SoundManager.Play("cheer", 0.8f);
public class SoundManager : MonoBehaviour
{
    public static SoundManager I;

    public float masterVolume = 1f, sfxVolume = 0.85f, musicVolume = 0.2f, ambienceVolume = 0.3f;

    const int SR = 22050;
    const float TwoPi = 6.2831853f;
    static readonly System.Random rng = new System.Random(12345);
    static float N() { return (float)(rng.NextDouble() * 2.0 - 1.0); }
    static float R() { return (float)rng.NextDouble(); }

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    AudioSource[] pool; int poolIdx;
    AudioSource music, city, crickets;
    class Bell { public AudioSource src; public float next, every; }
    readonly List<Bell> bells = new List<Bell>();

    Transform player; CharacterController cc;
    Vector3 lastPos; float speed, stepTimer, nextBird = 4f, nextHonk = 9f, nextCheck;
    int lastCount = -1, lastMoney = -1, lastDay = -1, lastHeat = -1;
    float lastEnergy = -1f;
    bool night, musicOn = true;
    Component cutscene; FieldInfo cutsceneT; float lastCsT;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("SoundManager").AddComponent<SoundManager>(); }

    public static void Play(string name, float vol = 1f, float pitch = 1f)
    {
        if (I != null) I.PlayInternal(name, vol, pitch, 0f);
    }

    // ------------------------------------------------------------------ lifecycle
    void Awake()
    {
        I = this;
        BuildClips();
        pool = new AudioSource[10];
        for (int i = 0; i < pool.Length; i++)
        {
            pool[i] = gameObject.AddComponent<AudioSource>();
            pool[i].playOnAwake = false; pool[i].spatialBlend = 0f;
        }
    }

    void OnEnable() { GameEvents.OnScore += OnScore; GameEvents.OnAlert += OnAlert; GameEvents.OnSnitched += OnSnitched; }
    void OnDisable() { GameEvents.OnScore -= OnScore; GameEvents.OnAlert -= OnAlert; GameEvents.OnSnitched -= OnSnitched; }

    void Start()
    {
        music = Loop2D("Music", "music", musicVolume * masterVolume);
        city = Loop2D("CityAmbience", "city", ambienceVolume * 0.75f * masterVolume);
        crickets = Loop2D("Crickets", "crickets", 0f);

        Add3DLoop("ChowkCrowd", FindPos("MainChowk_Center", new Vector3(0, 0, 0)), "crowd", 0.55f, 20f, 230f);
        GameObject pz = GameObject.Find("ProtestZone");
        if (pz != null) Add3DLoop("ProtestCrowd", pz.transform.position, "crowd2", 0.8f, 30f, 200f);
        GameObject gd = GameObject.Find("Landmark_Gurudwara");
        if (gd != null) Add3DLoop("GurudwaraDrone", gd.transform.position, "drone", 0.35f, 14f, 95f);

        AddBell("Landmark_MahaSwarnaMandir", 1f, 270f, 9f);
        AddBell("Landmark_MainMandir", 0.9f, 170f, 12f);
        AddBell("Landmark_SmallTemple_A", 0.7f, 90f, 16f);
        AddBell("Landmark_SmallTemple_B", 0.7f, 90f, 17f);
        AddBell("Landmark_SmallTemple_C", 0.7f, 90f, 18f);
    }

    static Vector3 FindPos(string name, Vector3 fallback)
    {
        GameObject g = GameObject.Find(name);
        return g != null ? g.transform.position : fallback;
    }

    AudioSource Loop2D(string name, string clip, float vol)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.clip = clips[clip]; s.loop = true; s.volume = vol; s.spatialBlend = 0f; s.playOnAwake = false;
        s.Play();
        return s;
    }

    AudioSource Make3D(string name, Vector3 pos, float vol, float min, float max)
    {
        var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.position = pos;
        var s = go.AddComponent<AudioSource>();
        s.spatialBlend = 1f; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
        s.dopplerLevel = 0f; s.volume = vol; s.playOnAwake = false;
        return s;
    }

    void Add3DLoop(string name, Vector3 pos, string clip, float vol, float min, float max)
    {
        var s = Make3D(name, pos, vol * ambienceVolume * 2.2f * masterVolume, min, max);
        s.clip = clips[clip]; s.loop = true; s.time = R() * clips[clip].length * 0.9f; s.Play();
    }

    void AddBell(string objName, float vol, float max, float every)
    {
        GameObject g = GameObject.Find(objName);
        if (g == null) return;
        var s = Make3D("Bell_" + objName, g.transform.position + Vector3.up * 8f, vol * sfxVolume * masterVolume, 18f, max);
        s.clip = clips["bell"];
        bells.Add(new Bell { src = s, every = every, next = Time.time + 2f + R() * every });
    }

    // ------------------------------------------------------------------ events
    void OnScore(int pts, string who)
    {
        if (who == "Protest") return;
        if (who == "Mission") { Play("fanfare"); return; }
        Play("thwack", 1f, 0.9f + R() * 0.2f);
        if (pts > 0) Play("ding", 0.7f, 1f + Mathf.Min(pts, 100) / 400f);
        else if (pts < 0) Play("sad", 1f);
    }

    void OnAlert(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        string u = msg.ToUpper();
        if (u.Contains("SNITCH")) { Play("sting"); Play("whistle", 0.9f); }
        else if (u.Contains("BUSTED") || u.Contains("ROBBED")) Play("busted");
        else if (u.Contains("EXHAUST")) Play("busted", 0.6f, 0.8f);
        else Play("siren", 0.45f);
    }

    void OnSnitched(string who) { Play("whistle", 0.8f, 1.05f); }

    IEnumerator Later(float s, string clip, float vol) { yield return new WaitForSecondsRealtime(s); Play(clip, vol); }

    // ------------------------------------------------------------------ per-frame
    void PlayInternal(string name, float vol, float pitch, float pan)
    {
        AudioClip c;
        if (!clips.TryGetValue(name, out c) || pool == null) return;
        AudioSource s = pool[poolIdx++ % pool.Length];
        s.pitch = pitch; s.panStereo = pan;
        s.PlayOneShot(c, Mathf.Clamp01(vol * sfxVolume * masterVolume));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.N))
        {
            musicOn = !musicOn;
            music.mute = !musicOn;
            GameEvents.OnToast?.Invoke(musicOn ? "Music on" : "Music off");
        }

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) { player = p.transform; cc = p.GetComponent<CharacterController>(); lastPos = player.position; }
        }

        float dt = Time.unscaledDeltaTime;
        TrackGame();
        Footsteps(Time.deltaTime);
        Cutscene();

        // ambience cross-fade between day and night
        float amb = ambienceVolume * masterVolume;
        city.volume = Mathf.MoveTowards(city.volume, (night ? 0.04f : 0.22f) * amb / 0.3f, dt * 0.4f);
        crickets.volume = Mathf.MoveTowards(crickets.volume, night ? 0.5f * amb / 0.3f : 0f, dt * 0.4f);

        if (Time.timeScale > 0f)
        {
            foreach (Bell b in bells)
                if (Time.time > b.next && !night)
                {
                    b.next = Time.time + b.every * (0.8f + R() * 0.5f);
                    if (player != null && Vector3.Distance(player.position, b.src.transform.position) < b.src.maxDistance)
                    { b.src.pitch = 0.95f + R() * 0.1f; b.src.PlayOneShot(clips["bell"]); }
                }
            if (!night && Time.time > nextBird) { nextBird = Time.time + 5f + R() * 9f; PlayInternal(R() < 0.5f ? "bird" : "birds", 0.22f, 0.95f + R() * 0.2f, (R() * 2f - 1f) * 0.8f); }
            if (!night && Time.time > nextHonk) { nextHonk = Time.time + 9f + R() * 14f; PlayInternal("honk", 0.2f, 0.9f + R() * 0.25f, (R() * 2f - 1f) * 0.9f); }
        }
    }

    void TrackGame()
    {
        GameManager gm = GameManager.I;
        if (gm == null) return;
        int count = gm.ChappalCount;
        if (lastCount >= 0)
        {
            if (count == lastCount - 1 && gm.money == lastMoney) Play("whoosh", 0.9f, 0.95f + R() * 0.15f);
            else if (count > lastCount) Play("rustle");
            if (gm.money > lastMoney) Play("coin");
            if (gm.energy - lastEnergy > 30f) Play("eat");
            if (gm.day > lastDay) { Play("sleep"); StartCoroutine(Later(1.8f, "birds", 0.5f)); }
            if (gm.heat > lastHeat) Play("sting", 0.4f, 1.3f);
        }
        lastCount = count; lastMoney = gm.money; lastEnergy = gm.energy; lastDay = gm.day; lastHeat = gm.heat;
    }

    void Footsteps(float dt)
    {
        if (player == null || dt <= 0f) return;
        Vector3 d = player.position - lastPos; d.y = 0f;
        float v = d.magnitude / dt;
        if (v < 25f) speed = Mathf.Lerp(speed, v, 0.3f);
        lastPos = player.position;

        bool grounded = cc == null || !cc.enabled || cc.isGrounded;
        if (speed > 0.8f && grounded && !night)
        {
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                float k = Mathf.InverseLerp(2f, 9f, speed);
                stepTimer = Mathf.Lerp(0.55f, 0.29f, k);
                PlayInternal("step" + rng.Next(3), Mathf.Lerp(0.25f, 0.5f, k), 0.9f + R() * 0.2f, 0f);
            }
        }
        else stepTimer = 0.05f;
    }

    // night cutscene hooks (found by name, so DarooCutscene.cs is not required to compile)
    void Cutscene()
    {
        if (cutscene == null)
        {
            if (Time.unscaledTime < nextCheck) { night = false; return; }
            nextCheck = Time.unscaledTime + 0.5f;
            GameObject go = GameObject.Find("DarooCutscene");
            if (go != null)
            {
                cutscene = go.GetComponent("DarooCutscene");
                if (cutscene != null) cutsceneT = cutscene.GetType().GetField("t", BindingFlags.NonPublic | BindingFlags.Instance);
                lastCsT = 0f;
            }
            if (cutscene == null) { night = false; return; }
        }
        night = true;
        float ct = cutsceneT != null ? (float)cutsceneT.GetValue(cutscene) : 0f;
        if (Crossed(ct, 10.4f) || Crossed(ct, 12.1f)) Play("pour", 0.9f);
        if (Crossed(ct, 20.1f)) Play("clink", 1f);
        foreach (float s in new[] { 28.9f, 29.4f, 31.9f, 32.4f, 34.9f, 35.4f }) if (Crossed(ct, s)) Play("gulp", 0.8f, 0.9f + R() * 0.2f);
        lastCsT = ct;
    }

    bool Crossed(float now, float at) { return lastCsT < at && now >= at; }

    // ------------------------------------------------------------------ synthesis helpers
    static float[] Gen(float seconds, System.Func<float, float> f)
    {
        int n = Mathf.CeilToInt(seconds * SR); var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = f(i / (float)SR);
        return d;
    }

    static void Norm(float[] d, float peak)
    {
        float m = 0f;
        for (int i = 0; i < d.Length; i++) m = Mathf.Max(m, Mathf.Abs(d[i]));
        if (m < 1e-5f) return;
        float k = peak / m;
        for (int i = 0; i < d.Length; i++) d[i] *= k;
    }

    void Make(string name, float[] d, float peak = 0.85f)
    {
        Norm(d, peak);
        var c = AudioClip.Create(name, d.Length, 1, SR, false);
        c.SetData(d, 0);
        clips[name] = c;
    }

    static float[] LoopFade(float[] d, int fade)
    {
        int n = d.Length - fade; var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = d[i];
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            o[i] = d[i] * k + d[n + i] * (1f - k);
        }
        return o;
    }

    static float[] Babble(float seconds, int syllables, float pitchLo, float pitchHi, float gain)
    {
        int n = (int)(seconds * SR); var buf = new float[n];
        float[] F1 = { 450f, 550f, 700f, 850f }, F2 = { 1000f, 1300f, 1700f, 2100f };
        for (int s = 0; s < syllables; s++)
        {
            int start = rng.Next(n); int len = (int)((0.1f + 0.2f * R()) * SR);
            float f0 = pitchLo + (pitchHi - pitchLo) * R(), f1 = F1[rng.Next(4)], f2 = F2[rng.Next(4)], g = (0.3f + 0.7f * R()) * gain;
            int H = Mathf.Clamp((int)(3000f / f0), 3, 14); var ha = new float[H];
            for (int h = 0; h < H; h++)
            {
                float fh = f0 * (h + 1);
                ha[h] = 1f / (1f + Mathf.Pow((fh - f1) / 150f, 2f)) + 0.6f / (1f + Mathf.Pow((fh - f2) / 250f, 2f));
            }
            float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float u = i / (float)len, env = Mathf.Sin(Mathf.PI * u); env *= env;
                ph += TwoPi * f0 * (1f + 0.12f * (u - 0.5f)) / SR;
                float sample = 0f;
                for (int h = 0; h < H; h++) sample += ha[h] * Mathf.Sin(ph * (h + 1));
                buf[(start + i) % n] += sample * env * g * 0.12f;
            }
        }
        float lp = 0f;
        for (int i = 0; i < n; i++) { lp += (N() - lp) * 0.08f; buf[i] += lp * 0.06f * gain; }
        return buf;
    }

    static void Pluck(float[] buf, int start, float freq, float gain)
    {
        int len = Mathf.Max(2, (int)(SR / freq)); var d = new float[len];
        for (int i = 0; i < len; i++) d[i] = N();
        int total = (int)(3.2f * SR), p = 0;
        for (int i = 0; i < total; i++)
        {
            int nx = (p + 1) % len; float v = d[p];
            d[p] = 0.4985f * (d[p] + d[nx]); p = nx;
            float env = Mathf.Clamp01(i / (0.002f * SR)) * Mathf.Exp(-i / (float)SR * 0.5f);
            buf[(start + i) % buf.Length] += v * gain * 0.5f * env;
        }
    }

    static void Tabla(float[] buf, int start, char c)
    {
        int len = (int)(0.4f * SR); float ph = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)SR, s = 0f;
            switch (c)
            {
                case 'D':
                    ph += TwoPi * (95f + 75f * Mathf.Exp(-t * 28f)) / SR;
                    s = Mathf.Sin(ph) * Mathf.Exp(-t * 7f) * 0.9f + Mathf.Sin(TwoPi * 640f * t) * Mathf.Exp(-t * 18f) * 0.3f + N() * Mathf.Exp(-t * 90f) * 0.3f; break;
                case 'N':
                    s = (Mathf.Sin(TwoPi * 720f * t) + 0.5f * Mathf.Sin(TwoPi * 1440f * t + 1f)) * Mathf.Exp(-t * 16f) * 0.45f + N() * Mathf.Exp(-t * 120f) * 0.15f; break;
                case 'T': s = Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 30f) * 0.35f; break;
                case 'K': s = N() * Mathf.Exp(-t * 60f) * 0.5f; break;
                default: s = Mathf.Sin(TwoPi * 990f * t) * Mathf.Exp(-t * 45f) * 0.12f; break;
            }
            buf[(start + i) % buf.Length] += s;
        }
    }

    // ------------------------------------------------------------------ the sounds
    void BuildClips()
    {
        for (int v = 0; v < 3; v++)
        {
            float lp = 0f, f0 = 60f + v * 12f;
            Make("step" + v, Gen(0.18f, t => { lp += (N() - lp) * 0.2f; return lp * 1.5f * Mathf.Exp(-t * 26f) + Mathf.Sin(TwoPi * f0 * t) * Mathf.Exp(-t * 42f) * 0.8f; }), 0.8f);
        }
        {
            float lp = 0f, lp2 = 0f;
            Make("whoosh", Gen(0.45f, t => { float u = t / 0.45f, s = Mathf.Sin(u * Mathf.PI); lp += (N() - lp) * Mathf.Lerp(0.04f, 0.4f, s); lp2 += (lp - lp2) * 0.6f; return lp2 * s * s * 3f; }), 0.8f);
        }
        {
            float lp = 0f, ph = 0f;
            Make("thwack", Gen(0.28f, t => { lp += (N() - lp) * 0.35f; ph += TwoPi * (55f + 170f * Mathf.Exp(-t * 14f)) / SR; return lp * Mathf.Exp(-t * 40f) * 1.4f + Mathf.Sin(ph) * Mathf.Exp(-t * 16f); }), 0.9f);
        }
        Make("ding", Gen(0.9f, t => (Mathf.Sin(TwoPi * 880f * t) * Mathf.Exp(-t * 6f) * 0.5f + Mathf.Sin(TwoPi * 1320f * t) * Mathf.Exp(-t * 8f) * 0.35f + Mathf.Sin(TwoPi * 1760f * t) * Mathf.Exp(-t * 10f) * 0.2f) * Mathf.Clamp01(t / 0.004f)), 0.7f);
        {
            float[] fr = { 311.1f, 293.7f, 277.2f, 246.9f }, du = { 0.26f, 0.26f, 0.26f, 0.75f }; float ph = 0f;
            Make("sad", Gen(1.6f, t =>
            {
                int i = 0; float s = 0f;
                while (i < 3 && t >= s + du[i]) { s += du[i]; i++; }
                float lt = t - s, f = fr[i] * (i == 3 ? 1f + 0.015f * Mathf.Sin(TwoPi * 6f * lt) * Mathf.Clamp01(lt * 3f) : 1f);
                ph += TwoPi * f / SR;
                float env = Mathf.Clamp01(lt / 0.02f) * (i == 3 ? Mathf.Exp(-lt * 2.2f) : Mathf.Clamp01((du[i] - lt) / 0.06f));
                return (Mathf.Sin(ph) + 0.5f * Mathf.Sin(2f * ph) + 0.33f * Mathf.Sin(3f * ph) + 0.2f * Mathf.Sin(4f * ph)) * env;
            }), 0.7f);
        }
        {
            float[] a = new float[16]; for (int i = 0; i < 16; i++) a[i] = 0.2f + R(); float lp = 0f;
            Make("rustle", Gen(0.75f, t => { lp += (N() - lp) * 0.5f; return (N() - lp) * a[Mathf.Min(15, (int)(t / 0.05f))] * Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.75f - t) / 0.15f); }), 0.6f);
        }
        {
            Make("coin", Gen(1.0f, t =>
            {
                float s = 0f;
                for (int k = 0; k < 5; k++)
                {
                    float t0 = k * 0.07f; if (t < t0) continue; float lt = t - t0;
                    s += (Mathf.Sin(TwoPi * 2093f * lt) + 0.5f * Mathf.Sin(TwoPi * 3136f * lt) + 0.3f * Mathf.Sin(TwoPi * 4186f * lt)) * Mathf.Exp(-lt * 7f) * 0.3f * Mathf.Clamp01(lt / 0.002f);
                }
                return s;
            }), 0.6f);
        }
        {
            float[] nf = { 523.3f, 659.3f, 784f, 1046.5f };
            Make("fanfare", Gen(1.3f, t =>
            {
                float s = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float t0 = k * 0.11f; if (t < t0) continue; float lt = t - t0;
                    float env = Mathf.Clamp01(lt / 0.008f) * Mathf.Exp(-lt * (k == 3 ? 3f : 9f));
                    s += (Mathf.Sin(TwoPi * nf[k] * lt) + 0.35f * Mathf.Sin(TwoPi * nf[k] * 2f * lt)) * env * 0.4f;
                }
                return s;
            }), 0.8f);
        }
        {
            float ph = 0f;
            Make("busted", Gen(1.0f, t => { ph += TwoPi * Mathf.Lerp(260f, 70f, t) / SR; return (Mathf.Sign(Mathf.Sin(ph)) * 0.4f + Mathf.Sin(ph)) * Mathf.Exp(-t * 1.8f) * Mathf.Clamp01(t / 0.01f); }), 0.8f);
        }
        {
            float ph = 0f;
            Make("siren", Gen(1.4f, t => { ph += TwoPi * (650f + 260f * Mathf.Sin(TwoPi * 1.8f * t)) / SR; return (Mathf.Sin(ph) + 0.3f * Mathf.Sin(2f * ph)) * Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((1.4f - t) / 0.1f); }), 0.5f);
        }
        {
            float ph = 0f;
            Make("sting", Gen(0.55f, t =>
            {
                float f = t < 0.22f ? 146.8f : 123.5f; ph += TwoPi * f / SR; float lt = t < 0.22f ? t : t - 0.22f, saw = 0f;
                for (int h = 1; h <= 5; h++) saw += Mathf.Sin(ph * h) / h;
                return saw * Mathf.Clamp01(lt / 0.01f) * Mathf.Exp(-lt * 5f);
            }), 0.8f);
        }
        {
            float ph = 0f;
            Make("whistle", Gen(0.95f, t =>
            {
                bool first = t < 0.3f, second = t > 0.42f && t < 0.9f;
                if (!first && !second) return 0f;
                float lt = first ? t : t - 0.42f, len = first ? 0.3f : 0.48f;
                float env = Mathf.Clamp01(lt / 0.02f) * Mathf.Clamp01((len - lt) / 0.05f);
                ph += TwoPi * (2700f + 140f * Mathf.Sin(TwoPi * 8f * t)) / SR;
                return (Mathf.Sin(ph) * (0.65f + 0.35f * Mathf.Sin(TwoPi * 38f * t)) + N() * 0.08f) * env;
            }), 0.6f);
        }
        {
            float[] ts = { 0f, 0.17f, 0.31f, 0.5f };
            Make("eat", Gen(0.8f, t =>
            {
                float s = 0f;
                foreach (float t0 in ts)
                {
                    if (t < t0) continue; float lt = t - t0;
                    s += (Mathf.Sin(TwoPi * 3200f * lt) + 0.6f * Mathf.Sin(TwoPi * 4700f * lt)) * Mathf.Exp(-lt * 40f) * 0.35f;
                }
                return s;
            }), 0.6f);
        }
        {
            float[] nf = { 659.3f, 523.3f, 392f };
            Make("sleep", Gen(3.2f, t =>
            {
                float s = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float t0 = k * 0.55f; if (t < t0) continue; float lt = t - t0;
                    s += (Mathf.Sin(TwoPi * nf[k] * lt) + 0.25f * Mathf.Sin(TwoPi * nf[k] * 2f * lt)) * Mathf.Exp(-lt * 2.2f) * Mathf.Clamp01(lt / 0.01f) * 0.4f;
                }
                return s;
            }), 0.55f);
        }
        {
            var t0s = new float[8]; var f0s = new float[8]; var ks = new float[8];
            for (int i = 0; i < 8; i++) { t0s[i] = R() * 1.8f; f0s[i] = 2600f + R() * 1000f; ks[i] = (R() < 0.6f ? 1f : -1f) * (3000f + R() * 5000f); }
            Make("birds", Gen(2.2f, t =>
            {
                float s = 0f;
                for (int i = 0; i < 8; i++)
                {
                    float lt = t - t0s[i]; if (lt < 0f || lt > 0.14f) continue;
                    s += Mathf.Sin(TwoPi * (f0s[i] * lt + 0.5f * ks[i] * lt * lt)) * Mathf.Sin(Mathf.PI * lt / 0.14f);
                }
                return s * 0.5f;
            }), 0.5f);
            Make("bird", Gen(0.6f, t =>
            {
                float s = 0f;
                for (int i = 0; i < 3; i++)
                {
                    float lt = t - i * 0.17f; if (lt < 0f || lt > 0.12f) continue;
                    s += Mathf.Sin(TwoPi * (3300f * lt + 0.5f * 6000f * lt * lt)) * Mathf.Sin(Mathf.PI * lt / 0.12f);
                }
                return s;
            }), 0.5f);
        }
        {
            Make("honk", Gen(0.6f, t =>
            {
                bool a = t < 0.22f, b = t > 0.3f && t < 0.56f;
                if (!a && !b) return 0f;
                float f = a ? 392f : 523.3f, lt = a ? t : t - 0.3f, s = 0f;
                for (int h = 1; h <= 4; h++) s += Mathf.Sin(TwoPi * f * h * t) / h;
                return s * Mathf.Clamp01(lt / 0.01f) * Mathf.Clamp01(((a ? 0.22f : 0.26f) - lt) / 0.02f);
            }), 0.6f);
        }
        {
            float[] r = { 0.5f, 1f, 1.19f, 1.56f, 2f, 2.74f, 3f, 4.1f }, am = { 0.4f, 1f, 0.5f, 0.6f, 0.5f, 0.35f, 0.25f, 0.15f }, dc = { 0.8f, 1.2f, 1.5f, 1.8f, 2.2f, 2.8f, 3.3f, 4f };
            Make("bell", Gen(4.2f, t =>
            {
                float s = 0f;
                for (int i = 0; i < 8; i++) s += Mathf.Sin(TwoPi * 410f * r[i] * t) * am[i] * Mathf.Exp(-t * dc[i]);
                return s * Mathf.Clamp01(t / 0.002f);
            }), 0.7f);
        }
        Make("drone", Gen(4f, t =>
        {
            float s = 0f;
            for (int h = 1; h <= 6; h++) s += Mathf.Sin(TwoPi * 130f * h * t) / h + 0.6f * Mathf.Sin(TwoPi * 195f * h * t) / h;
            return s * (1f + 0.08f * Mathf.Sin(TwoPi * 5f * t));
        }), 0.5f);
        {
            float b = 0f, m1 = 0f, m2 = 0f;
            float[] raw = Gen(12.5f, t => { b += (N() - b) * 0.012f; m1 += (N() - m1) * 0.25f; m2 += (m1 - m2) * 0.25f; return b * 5f + (m1 - m2) * 0.9f * (0.7f + 0.3f * Mathf.Sin(TwoPi * t / 6f)) + Mathf.Sin(TwoPi * 55f * t) * 0.04f; });
            Make("city", LoopFade(raw, SR / 2), 0.5f);
        }
        Make("crowd", Babble(10f, 70, 100f, 250f, 1f), 0.6f);
        Make("crowd2", Babble(10f, 110, 120f, 280f, 1.2f), 0.7f);
        {
            float[] cheer = Babble(2.2f, 160, 180f, 330f, 1.5f);
            for (int i = 0; i < cheer.Length; i++) { float u = i / (float)cheer.Length; cheer[i] *= Mathf.Pow(Mathf.Sin(Mathf.PI * u), 0.7f); }
            Make("cheer", cheer, 0.8f);
        }
        Make("crickets", Gen(4f, t =>
        {
            float g1 = (t * 4f) % 1f < 0.4f ? 1f : 0f, p1 = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(TwoPi * 28f * t)), 2f);
            float g2 = (t * 3f) % 1f < 0.5f ? 1f : 0f, p2 = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(TwoPi * 24f * t + 1f)), 2f);
            return Mathf.Sin(TwoPi * 4400f * t) * g1 * p1 * 0.5f + Mathf.Sin(TwoPi * 4900f * t) * g2 * p2 * 0.4f;
        }), 0.5f);
        {
            float lp = 0f;
            Make("pour", Gen(1.3f, t => { lp += (N() - lp) * 0.15f; return (Mathf.Sin(TwoPi * (300f + 200f * Mathf.Sin(TwoPi * 11f * t)) * t) * 0.25f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 7f * t)) + lp * 0.3f) * Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1.3f - t) / 0.2f); }), 0.5f);
        }
        Make("clink", Gen(0.7f, t => (Mathf.Sin(TwoPi * 2800f * t) + 0.7f * Mathf.Sin(TwoPi * 4200f * t) + 0.4f * Mathf.Sin(TwoPi * 6100f * t)) * Mathf.Exp(-t * 14f) * Mathf.Clamp01(t / 0.001f)), 0.7f);
        {
            float ph = 0f;
            Make("gulp", Gen(0.3f, t => { ph += TwoPi * (190f - 70f * t / 0.3f) / SR; return Mathf.Sin(ph) * Mathf.Sin(Mathf.PI * t / 0.3f) + N() * Mathf.Exp(-t * 80f) * 0.2f; }), 0.5f);
        }
        BuildMusic();
    }

    void BuildMusic()
    {
        const float bpm = 80f;
        float beat = 60f / bpm;
        int n = (int)(32f * beat * SR);
        var buf = new float[n];

        float[] tp = { 196f, 261.6f, 261.6f, 130.8f };
        for (int k = 0; k < 24; k++) Pluck(buf, (int)(k * 1.0f * SR), tp[k % 4], 0.5f);

        string pat = "DNNDDNNDDTTKKNND";
        for (int b = 0; b < 32; b++)
        {
            int st = (int)(b * beat * SR);
            Tabla(buf, st, pat[b % 16]);
            if (b % 2 == 1) Tabla(buf, st + (int)(beat * 0.5f * SR), 't');
        }

        float[][] notes =
        {
            new[] { 329.6f, 2, 2 }, new[] { 392f, 4, 1 }, new[] { 440f, 5, 1 }, new[] { 392f, 6, 2 }, new[] { 329.6f, 8, 1 },
            new[] { 293.7f, 9, 1 }, new[] { 261.6f, 10, 3 }, new[] { 392f, 14, 2 }, new[] { 440f, 16, 2 }, new[] { 523.3f, 18, 2 },
            new[] { 440f, 20, 1 }, new[] { 392f, 21, 1 }, new[] { 329.6f, 22, 2 }, new[] { 293.7f, 24, 1 }, new[] { 329.6f, 25, 1 }, new[] { 261.6f, 26, 4 },
        };
        foreach (float[] nt in notes)
        {
            int st = (int)(nt[1] * beat * SR), len = (int)(nt[2] * beat * SR * 0.95f); float ph = 0f;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR;
                float env = Mathf.Clamp01(t / 0.09f) * Mathf.Clamp01((len / (float)SR - t) / 0.12f);
                ph += TwoPi * nt[0] * (1f + 0.006f * Mathf.Sin(TwoPi * 5.2f * t) * Mathf.Clamp01(t * 2f)) / SR;
                float v = Mathf.Sin(ph) + 0.18f * Mathf.Sin(2f * ph) + 0.05f * Mathf.Sin(3f * ph) + N() * 0.04f;
                buf[(st + i) % n] += v * env * 0.32f;
            }
        }
        Make("music", buf, 0.8f);
    }
}