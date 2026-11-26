using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs by itself (no need to attach it to anything).
// Reach enough karma (default 10) and the people make you a NETA: the police look away, chanda keeps coming in,
// three chamchas follow you around and new neta-only missions unlock. But SCANDALS (hitting kids, women or animals,
// or getting caught stealing chappals) cost karma, and if it drops too low you lose the election.
// Needs: MissionManager (new version), GameManager, GameEvents, SoundManager, PeopleManager, Pedestrian.
public class NetaRise : MonoBehaviour
{
    public static bool IsNeta;
    public static float BribeUntil;   // set by the chai-paani mission
    public static bool PoliceIgnore { get { return IsNeta || Time.time < BribeUntil; } }

    public int karmaToBecomeNeta = 10;
    public int karmaToLose = 7;
    public int entourageSize = 3;
    public float chandaInterval = 15f;
    public bool devKeys = true;   // F8 = +10 karma for testing. Turn off when you finish testing.

    static readonly int[] RankKarma = { 10, 16, 24, 35 };
    static readonly string[] RankName = { "SARPANCH", "MLA", "MINISTER", "CHIEF MINISTER" };
    static readonly string[] Shouts = { "Neta Ji Zindabad!", "Neta Ji, aap mahaan ho!", "Hamare Neta, hamari shaan!", "Neta Ji ki jai ho!", "Neta Ji ko lal salaam!" };

    class Chamcha { public Transform t; public int idx; }
    readonly List<Chamcha> entourage = new List<Chamcha>();

    Transform player;
    Vector3 lastPos, moveDir = Vector3.forward;
    float nextChanda, nextShout, nextScandal, bannerTimer;
    TMP_Text badge, banner;
    static readonly RaycastHit[] buf = new RaycastHit[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("NetaRise").AddComponent<NetaRise>(); }

    void OnEnable() { GameEvents.OnScore += OnScore; GameEvents.OnCrime += OnCrime; }
    void OnDisable() { GameEvents.OnScore -= OnScore; GameEvents.OnCrime -= OnCrime; }

    void Start()
    {
        IsNeta = PlayerPrefs.GetInt("isNeta", 0) == 1;
        BribeUntil = 0f;
        nextChanda = Time.time + chandaInterval; nextShout = Time.time + 8f;
        BuildUI();
    }

    // ------------------------------------------------------------------ UI
    void BuildUI()
    {
        var canvasGo = new GameObject("NetaCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        var bgo = new GameObject("Badge", typeof(RectTransform));
        bgo.transform.SetParent(canvasGo.transform, false);
        badge = bgo.AddComponent<TextMeshProUGUI>();
        badge.fontSize = 30; badge.fontStyle = FontStyles.Bold; badge.alignment = TextAlignmentOptions.Top; badge.raycastTarget = false; badge.text = "";
        badge.outlineWidth = 0.2f; badge.outlineColor = new Color32(0, 0, 0, 255);
        var brt = badge.rectTransform; brt.anchorMin = new Vector2(0.25f, 1f); brt.anchorMax = new Vector2(0.75f, 1f); brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0, -8); brt.sizeDelta = new Vector2(0, 60);

        var ngo = new GameObject("Banner", typeof(RectTransform));
        ngo.transform.SetParent(canvasGo.transform, false);
        banner = ngo.AddComponent<TextMeshProUGUI>();
        banner.fontSize = 84; banner.fontStyle = FontStyles.Bold; banner.alignment = TextAlignmentOptions.Center; banner.raycastTarget = false; banner.text = "";
        banner.color = new Color(1f, 0.8f, 0.2f); banner.outlineWidth = 0.25f; banner.outlineColor = new Color32(0, 0, 0, 255);
        var nrt = banner.rectTransform; nrt.anchorMin = new Vector2(0.05f, 0.55f); nrt.anchorMax = new Vector2(0.95f, 0.85f); nrt.offsetMin = nrt.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------ events
    void OnScore(int pts, string who)
    {
        if (!IsNeta || pts >= 0) return;
        if (who == "Kid" || who == "Woman" || who == "Animal") Scandal(2, "Neta Ji hit a " + who.ToLower() + "!");
    }

    void OnCrime(Vector3 pos, string what)
    {
        if (!IsNeta || what != "theft" || Time.time < nextScandal) return;
        nextScandal = Time.time + 10f;
        Scandal(1, "Neta Ji caught stealing chappals on camera!");
    }

    void Scandal(int loss, string text)
    {
        MissionManager.AddKarma(-loss);
        GameEvents.OnAlert?.Invoke("SCANDAL! " + text + "  (-" + loss + " karma)");
    }

    // ------------------------------------------------------------------ main loop
    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform; lastPos = player.position;
            if (IsNeta) SpawnEntourage();
        }
        GameManager gm = GameManager.I;
        if (gm == null) return;

        if (devKeys && Input.GetKeyDown(KeyCode.F8)) { MissionManager.AddKarma(10); GameEvents.OnToast?.Invoke("DEV: +10 karma"); }

        int karma = MissionManager.Karma;
        if (!IsNeta && karma >= karmaToBecomeNeta) Ascend();
        else if (IsNeta && karma < karmaToLose) Fall();

        Vector3 d = player.position - lastPos; d.y = 0f;
        if (d.magnitude > 0.05f && d.magnitude < 5f)
        {
            moveDir = Vector3.Lerp(moveDir, d.normalized, 0.15f);
            if (moveDir.sqrMagnitude < 0.01f) moveDir = d.normalized;
            moveDir.Normalize();
        }
        lastPos = player.position;

        if (PoliceIgnore)
        {
            gm.heat = 0;
            if (PeopleManager.I != null)
                foreach (var cop in PeopleManager.I.police)
                    if (cop != null && cop.mode == Pedestrian.Mode.Chase) cop.StandDown();
        }

        if (IsNeta)
        {
            if (Time.time > nextChanda)
            {
                nextChanda = Time.time + chandaInterval;
                int amt = 30 + (RankIndex(karma) + 1) * 25 + Random.Range(0, 20);
                gm.money += amt;
                GameEvents.OnToast?.Invoke("Chanda from your supporters: Rs " + amt);
            }
            UpdateEntourage();
            if (Time.time > nextShout)
            {
                nextShout = Time.time + Random.Range(11f, 18f);
                GameEvents.OnToast?.Invoke("Chamcha: " + Shouts[Random.Range(0, Shouts.Length)]);
                SoundManager.Play("cheer", 0.4f, 1.1f);
            }
        }
        UpdateUI(karma);
    }

    static int RankIndex(int karma)
    {
        int r = -1;
        for (int i = 0; i < RankKarma.Length; i++) if (karma >= RankKarma[i]) r = i;
        return r;
    }

    void UpdateUI(int karma)
    {
        if (IsNeta)
        {
            int r = Mathf.Max(0, RankIndex(karma));
            string next = r + 1 < RankKarma.Length ? "   <size=70%><color=#cccccc>next rank at karma " + RankKarma[r + 1] + "</color></size>" : "";
            badge.text = "<color=#ffcc33>NETA JI</color>  <size=80%>" + RankName[r] + "   Karma " + karma + "</size>" + next;
        }
        else if (karma > 0)
            badge.text = "<size=70%><color=#bbbbbb>Karma " + karma + " / " + karmaToBecomeNeta + " to become Neta Ji</color></size>";
        else badge.text = "";

        if (bannerTimer > 0f)
        {
            bannerTimer -= Time.deltaTime;
            banner.alpha = Mathf.Clamp01(bannerTimer / 1.5f);
            if (bannerTimer <= 0f) banner.text = "";
        }
    }

    // ------------------------------------------------------------------ becoming (and losing) neta status
    void Ascend()
    {
        IsNeta = true; PlayerPrefs.SetInt("isNeta", 1);
        nextChanda = Time.time + chandaInterval; nextShout = Time.time + 6f;
        SpawnEntourage();
        banner.text = "THE PEOPLE HAVE SPOKEN!\n<size=60><color=#ffffff>You are now</color> NETA JI</size>";
        banner.alpha = 1f; bannerTimer = 7f;
        SoundManager.Play("fanfare"); SoundManager.Play("cheer", 1f);
        GameEvents.OnToast?.Invoke("Police look away, chanda flows in, new NETA missions unlocked. Avoid SCANDALS!");
    }

    void Fall()
    {
        IsNeta = false; PlayerPrefs.SetInt("isNeta", 0);
        DestroyEntourage();
        GameEvents.OnAlert?.Invoke("YOU LOST THE ELECTION! The opposition exposed your scandals.");
        SoundManager.Play("sad");
    }

    // ------------------------------------------------------------------ the chamchas (followers)
    static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var mat = new Material(s); mat.color = c;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        return g;
    }

    void SpawnEntourage()
    {
        DestroyEntourage();
        if (player == null) return;
        Color white = new Color(0.96f, 0.95f, 0.9f), skin = new Color(0.58f, 0.38f, 0.26f);
        for (int i = 0; i < entourageSize; i++)
        {
            var root = new GameObject("Chamcha_" + (i + 1));
            Vector3 start = player.position - moveDir * (4f + i);
            root.transform.position = new Vector3(start.x, GroundY(start, root.transform, player.position.y), start.z);
            Prim(root.transform, PrimitiveType.Capsule, new Vector3(0, 1.0f, 0), new Vector3(0.46f, 0.4f, 0.36f), white);
            Prim(root.transform, PrimitiveType.Capsule, new Vector3(0, 0.4f, 0), new Vector3(0.4f, 0.42f, 0.34f), white);
            Prim(root.transform, PrimitiveType.Sphere, new Vector3(0, 1.7f, 0), new Vector3(0.26f, 0.29f, 0.27f), skin);
            Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.88f, 0), new Vector3(0.3f, 0.05f, 0.3f), white);
            Prim(root.transform, PrimitiveType.Cube, new Vector3(-0.05f, 1.25f, 0.08f), new Vector3(0.5f, 0.07f, 0.22f), new Color(1f, 0.55f, 0.1f), new Vector3(0, 0, 35));
            entourage.Add(new Chamcha { t = root.transform, idx = i });
        }
    }

    void DestroyEntourage()
    {
        foreach (var c in entourage) if (c != null && c.t != null) Destroy(c.t.gameObject);
        entourage.Clear();
    }

    void UpdateEntourage()
    {
        Vector3 right = Vector3.Cross(Vector3.up, moveDir);
        foreach (var c in entourage)
        {
            if (c.t == null) continue;
            Vector3 target = player.position - moveDir * (3.3f + c.idx * 0.7f) + right * ((c.idx - (entourageSize - 1) * 0.5f) * 1.7f);
            Vector3 p = c.t.position, dv = target - p; dv.y = 0f;
            float dist = dv.magnitude;
            if (dist > 45f) p = new Vector3(target.x, p.y, target.z);
            else if (dist > 0.5f)
            {
                float sp = Mathf.Clamp(dist * 2.4f, 0f, 9f);
                p += dv.normalized * Mathf.Min(sp * Time.deltaTime, dist);
                c.t.rotation = Quaternion.Slerp(c.t.rotation, Quaternion.LookRotation(dv.normalized), 8f * Time.deltaTime);
            }
            else c.t.rotation = Quaternion.Slerp(c.t.rotation, Quaternion.LookRotation(moveDir), 4f * Time.deltaTime);
            p.y = Mathf.Lerp(p.y, GroundY(p, c.t, p.y), 12f * Time.deltaTime);
            c.t.position = p;
        }
    }

    float GroundY(Vector3 p, Transform ignore, float fallback)
    {
        Vector3 o = new Vector3(p.x, fallback + 25f, p.z);
        int n = Physics.RaycastNonAlloc(o, Vector3.down, buf, 60f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MinValue; bool found = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = buf[i].collider;
            if (ignore != null && c.transform.IsChildOf(ignore)) continue;
            if (c.CompareTag("Chappal") || c.CompareTag("Player")) continue;
            if (buf[i].point.y > best && buf[i].point.y < fallback + 6f) { best = buf[i].point.y; found = true; }
        }
        return found ? best : fallback;
    }
}