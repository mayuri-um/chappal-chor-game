using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs by itself (no need to attach it to anything).
// A day runs from 06:00 to midnight in about 9 real minutes (30 real seconds per game hour).
// Sleeping at the masjid starts a new day at 06:00. If you stay up until 02:00 you pass out.
// At night: the sun fades to moonlight, street lamps and landmarks glow, fewer people are out,
// witnesses see less far (easier to steal), but police patrol faster. The kabadi closes at 21:00.
// Needs: GameManager, GameEvents, InteractionPrompt, Witness, PeopleManager, Pedestrian.
public class DayNightCycle : MonoBehaviour
{
    public static float Hour = 8f;   // 6 to 26 (24 = midnight, 26 = 2 AM)
    public static bool IsNight { get { float h = Hour % 24f; return h >= 20f || h < 5.5f; } }

    public float secondsPerHour = 30f;
    public float faintHour = 26f;
    public float dealerOpen = 6f, dealerClose = 21f;
    public bool devKeys = true;   // hold T to fast-forward time. Turn off when you finish testing.

    Light sun; float sunInt0, ambient0, fogDen0; Color sunCol0, fogCol0; bool fog0; FogMode fogMode0;
    Material skyMat; bool hasExposure; float exposure0;
    Transform player; Light lantern;

    class LightSpot { public Light l; public float maxI; }
    readonly List<LightSpot> spots = new List<LightSpot>();
    readonly List<Material> bulbs = new List<Material>();
    Behaviour dealerZone; Vector3 dealerPos; bool dealerFound;
    readonly Dictionary<Witness, float> witnessBase = new Dictionary<Witness, float>();
    readonly Dictionary<Pedestrian, Vector2> copBase = new Dictionary<Pedestrian, Vector2>();
    readonly HashSet<int> fired = new HashSet<int>();
    int lastDay = -1;
    float nextRules, nextCutCheck;
    bool cutsceneOn, lastLate;
    TMP_Text clock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("DayNightCycle").AddComponent<DayNightCycle>(); }

    // ------------------------------------------------------------------ setup
    void Start()
    {
        sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { sunInt0 = Mathf.Max(0.5f, sun.intensity); sunCol0 = sun.color; }
        ambient0 = Mathf.Max(0.8f, RenderSettings.ambientIntensity);
        fog0 = RenderSettings.fog; fogCol0 = RenderSettings.fogColor; fogDen0 = RenderSettings.fogDensity; fogMode0 = RenderSettings.fogMode;

        if (RenderSettings.skybox != null)
        {
            skyMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMat;
            hasExposure = skyMat.HasProperty("_Exposure");
            if (hasExposure) exposure0 = skyMat.GetFloat("_Exposure");
        }

        Spot("MainChowk_Center", new Vector3(0, 7, 0), 45f, 3.5f);
        Spot("Landmark_MahaSwarnaMandir", new Vector3(0, 8, -30), 55f, 3.5f);
        Spot("Landmark_MainMandir", new Vector3(0, 6, -10), 35f, 3f);
        Spot("Landmark_Gurudwara", new Vector3(0, 7, 0), 50f, 3f);
        Spot("Landmark_Masjid", new Vector3(0, 7, -10), 45f, 3f);
        Spot("Landmark_DaaruTheka", new Vector3(4, 5, -4), 28f, 3.5f);
        Spot("Landmark_PoliceChowki", new Vector3(0, 5, -3), 22f, 3f);
        Spot("Landmark_NetaChowk_Statue", new Vector3(0, 7, 0), 28f, 3f);
        Spot("Landmark_KabadiDealer", new Vector3(0, 5, 2), 26f, 2.5f);
        Spot("Landmark_ProtestMaidan", new Vector3(0, 9, 0), 70f, 3f);

        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            bool has = false;
            foreach (var sm in r.sharedMaterials) if (sm != null && sm.name.ToLower().Contains("bulb")) { has = true; break; }
            if (!has) continue;
            foreach (var im in r.materials) if (im != null && im.name.ToLower().Contains("bulb")) bulbs.Add(im);
        }

        GameObject dz = GameObject.Find("DealerZone");
        if (dz != null) { dealerPos = dz.transform.position; dealerFound = true; dealerZone = dz.GetComponent("SafeZone") as Behaviour; }

        BuildClock();
    }

    void Spot(string name, Vector3 offset, float range, float intensity)
    {
        GameObject g = GameObject.Find(name);
        if (g == null) return;
        var go = new GameObject("NightLight_" + name);
        go.transform.SetParent(transform, false);
        go.transform.position = g.transform.position + offset;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.range = range; l.intensity = 0f; l.color = new Color(1f, 0.78f, 0.5f); l.shadows = LightShadows.None;
        spots.Add(new LightSpot { l = l, maxI = intensity });
    }

    void BuildClock()
    {
        var canvasGo = new GameObject("ClockCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("Clock", typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        clock = go.AddComponent<TextMeshProUGUI>();
        clock.fontSize = 34; clock.fontStyle = FontStyles.Bold; clock.alignment = TextAlignmentOptions.TopRight; clock.raycastTarget = false;
        clock.outlineWidth = 0.2f; clock.outlineColor = new Color32(0, 0, 0, 255);
        var rt = clock.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-24, -345); rt.sizeDelta = new Vector2(420, 80);
    }

    // ------------------------------------------------------------------ main loop
    void Update()
    {
        if (Time.timeScale == 0f) return;
        GameManager gm = GameManager.I;
        if (gm == null) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                var lg = new GameObject("NightLantern");
                lg.transform.SetParent(player, false); lg.transform.localPosition = new Vector3(0, 2f, 0);
                lantern = lg.AddComponent<Light>();
                lantern.type = LightType.Point; lantern.range = 14f; lantern.intensity = 0f; lantern.color = new Color(1f, 0.85f, 0.6f); lantern.shadows = LightShadows.None;
            }
        }

        if (lastDay < 0) lastDay = gm.day;
        if (gm.day != lastDay)
        {
            lastDay = gm.day; Hour = 6f; fired.Clear(); lastLate = !lastLate;
            GameEvents.OnToast?.Invoke("Good morning! Day " + gm.day + " begins.");
        }

        float speed = devKeys && Input.GetKey(KeyCode.T) ? 25f : 1f;
        Hour += Time.deltaTime * speed / secondsPerHour;
        FireEvents();
        if (Hour >= faintHour) { Faint(gm); return; }

        if (Time.unscaledTime > nextCutCheck)
        {
            nextCutCheck = Time.unscaledTime + 0.5f;
            cutsceneOn = GameObject.Find("DarooCutscene") != null;
        }
        if (!cutsceneOn) ApplyLighting();

        if (Time.time > nextRules) { nextRules = Time.time + 1f; Rules(); }
        DealerClosedPrompt();
        UpdateClock(gm);
    }

    // ------------------------------------------------------------------ light, sky and fog
    void ApplyLighting()
    {
        float h = Hour % 24f;
        float sinDay = (h >= 6f && h <= 18f) ? Mathf.Sin(Mathf.PI * (h - 6f) / 12f) : 0f;
        float dayK = Mathf.Clamp01(sinDay * 3f);        // fast ramp at dawn and dusk
        float nightK = 1f - dayK;
        float elev = (h >= 6f && h <= 18f) ? Mathf.Lerp(8f, 172f, (h - 6f) / 12f) : 60f;

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(elev, 35f, 0f);
            float low = 1f - Mathf.Clamp01(sinDay * 1.6f);   // 1 near the horizon = orange
            Color dayCol = Color.Lerp(sunCol0, new Color(1f, 0.62f, 0.35f), low * 0.85f);
            sun.color = Color.Lerp(new Color(0.45f, 0.55f, 0.95f), dayCol, dayK);
            sun.intensity = Mathf.Lerp(0.1f, sunInt0, dayK);
        }
        RenderSettings.ambientIntensity = Mathf.Lerp(0.3f, ambient0, dayK);
        if (hasExposure) skyMat.SetFloat("_Exposure", Mathf.Lerp(0.12f, exposure0, dayK));

        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(new Color(0.03f, 0.04f, 0.09f), new Color(0.72f, 0.8f, 0.9f), dayK);
        RenderSettings.fogDensity = Mathf.Lerp(0.0022f, 0.0006f, dayK);

        foreach (var s in spots) if (s.l != null) s.l.intensity = s.maxI * nightK;
        if (lantern != null) lantern.intensity = 2.4f * nightK;
        Color glow = new Color(1f, 0.8f, 0.4f) * (nightK * 3f);
        foreach (var m in bulbs)
            if (m != null) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", glow); }
    }

    // ------------------------------------------------------------------ messages
    void Say(int hour, string msg, bool alert)
    {
        if (Hour < hour || fired.Contains(hour)) return;
        fired.Add(hour);
        if (alert) GameEvents.OnAlert?.Invoke(msg); else GameEvents.OnToast?.Invoke(msg);
    }

    void FireEvents()
    {
        Say(12, "Noon. The market is at its busiest.", false);
        Say(18, "Sunset. The streets are getting dark.", false);
        Say(20, "Night falls. Fewer eyes on you, but the police patrol faster.", false);
        Say(21, "The kabadi has closed for the night.", false);
        Say(24, "MIDNIGHT! Go to the masjid and sleep.", true);
        Say(25, "You are exhausted. You will pass out at 2:00 AM!", true);
    }

    void Faint(GameManager gm)
    {
        int lost = Mathf.Min(gm.money, 300);
        gm.money -= lost; gm.day++; gm.energy = 50f; gm.heat = 0; gm.ateLangarToday = false;
        GameObject start = GameObject.Find("PlayerStart");
        if (player != null && start != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.position = start.transform.position + Vector3.up * 2f;
            if (cc != null) cc.enabled = true;
        }
        lastDay = gm.day; Hour = 6f; fired.Clear();
        GameEvents.OnAlert?.Invoke("YOU PASSED OUT! You wake up at the masjid. Rs " + lost + " is missing from your pockets.");
    }

    // ------------------------------------------------------------------ night rules
    void Rules()
    {
        bool night = IsNight;
        float h = Hour % 24f;

        foreach (var w in new List<Witness>(Witness.All))
        {
            if (w == null) continue;
            if (!witnessBase.ContainsKey(w)) witnessBase[w] = w.range;
            w.range = witnessBase[w] * (night ? 0.55f : 1f);
        }

        if (PeopleManager.I != null)
        {
            foreach (var cop in PeopleManager.I.police)
            {
                if (cop == null) continue;
                if (!copBase.ContainsKey(cop)) copBase[cop] = new Vector2(cop.walkSpeed, cop.runSpeed);
                Vector2 b = copBase[cop];
                cop.walkSpeed = b.x * (night ? 1.3f : 1f);
                cop.runSpeed = b.y * (night ? 1.15f : 1f);
            }

            bool late = h >= 22f || h < 5f;
            if (late != lastLate)
            {
                lastLate = late;
                int i = 0;
                foreach (var p in PeopleManager.I.all)
                {
                    if (p == null) continue;
                    if (p.role == "Civilian" || p.role == "Woman" || p.role == "Kid")
                    {
                        p.gameObject.SetActive(!(late && (i % 10) < 7));   // 70% go home late at night
                        i++;
                    }
                }
                if (late) GameEvents.OnToast?.Invoke("Most people have gone home. The streets are quiet.");
            }
        }

        if (dealerZone != null) dealerZone.enabled = h >= dealerOpen && h < dealerClose;
    }

    void DealerClosedPrompt()
    {
        if (!dealerFound || player == null) return;
        float h = Hour % 24f;
        if (h >= dealerOpen && h < dealerClose) return;
        Vector3 a = player.position, b = dealerPos; a.y = 0; b.y = 0;
        if (Vector3.Distance(a, b) < 8f) InteractionPrompt.Show("F", "The kabadi is closed (opens at " + Mathf.RoundToInt(dealerOpen) + ":00)", 0f, true);
    }

    // ------------------------------------------------------------------ clock
    void UpdateClock(GameManager gm)
    {
        float h = Hour % 24f;
        int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f);
        string period, col;
        if (h < 5.5f) { period = "Late night"; col = "#8fb0ff"; }
        else if (h < 8f) { period = "Early morning"; col = "#ffb36b"; }
        else if (h < 12f) { period = "Morning"; col = "#ffffff"; }
        else if (h < 16f) { period = "Afternoon"; col = "#ffffff"; }
        else if (h < 20f) { period = "Evening"; col = "#ffb36b"; }
        else { period = "Night"; col = "#8fb0ff"; }
        clock.text = "<color=" + col + ">Day " + gm.day + "   " + hh.ToString("00") + ":" + mm.ToString("00") + "</color>\n<size=60%>" + period + "</size>";
    }
}