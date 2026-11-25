using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs by itself (no need to attach it to anything).
// Needs the map marker "ProtestZone" (placed by the map), GameManager, GameEvents and InteractionPrompt.
// SoundManager is optional-but-expected: it plays the crowd cheer while you chant.
public class ProtestBonus : MonoBehaviour
{
    public int pointsPerSecond = 5;
    public int chantMultiplier = 3;
    public int dailyCap = 1500;
    public KeyCode chantKey = KeyCode.C;

    static readonly string[] Slogans =
    {
        "NETA JI HAI HAI!", "HAMARI MAANGE POORI KARO!", "MEHNGAI HATAO, DESH BACHAO!",
        "DADA NETA MURDABAD!", "BHAIYA JI HAI HAI!", "CHAPPAL CHOR ZINDABAD!"
    };

    Transform zone, player;
    Collider zoneCol;
    Vector3 zoneCenter;
    Vector2 halfSize = new Vector2(50f, 45f);

    int earnedToday, lastDay = -1, sloganIdx;
    float acc, sloganTimer, chantAmount;
    bool inside, capNotified;

    // crowd bounce
    readonly List<Transform> crowd = new List<Transform>();
    readonly List<float> crowdBaseY = new List<float>();

    // beacon
    GameObject beaconRoot; Material beamMat, ringMat; TextMeshPro beaconText; Transform beaconLabel;

    // UI
    GameObject panel; TMP_Text panelText, sloganText; RectTransform barFill;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("ProtestBonus").AddComponent<ProtestBonus>(); }

    void Start()
    {
        GameObject z = GameObject.Find("ProtestZone");
        if (z == null)
        {
            Debug.LogWarning("ProtestBonus: marker 'ProtestZone' not found in the scene, protest bonus is disabled.");
            enabled = false; return;
        }
        zone = z.transform; zoneCol = z.GetComponent<Collider>();
        zoneCenter = zone.position;
        if (zoneCol != null)
        {
            zoneCenter = zoneCol.bounds.center;
            halfSize = new Vector2(zoneCol.bounds.extents.x, zoneCol.bounds.extents.z);
        }

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("NPC_Protester_")) { crowd.Add(t); crowdBaseY.Add(t.position.y); }

        BuildBeacon();
        BuildUI();
    }

    // ------------------------------------------------------------------ building the visuals
    void BuildBeacon()
    {
        Shader sh = Shader.Find("Sprites/Default");
        beaconRoot = new GameObject("ProtestBeacon");
        beaconRoot.transform.position = new Vector3(zoneCenter.x, zoneCenter.y - 1.5f, zoneCenter.z);
        beamMat = new Material(sh); ringMat = new Material(sh);

        var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(beaconRoot.transform, false);
        b.transform.localPosition = new Vector3(0, 40, 0); b.transform.localScale = new Vector3(1.0f, 40, 1.0f);
        b.GetComponent<Renderer>().sharedMaterial = beamMat;

        var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(r.GetComponent<Collider>());
        r.transform.SetParent(beaconRoot.transform, false);
        r.transform.localPosition = new Vector3(0, 0.1f, 0); r.transform.localScale = new Vector3(halfSize.x * 1.6f, 0.02f, halfSize.x * 1.6f);
        r.GetComponent<Renderer>().sharedMaterial = ringMat;

        var lab = new GameObject("Label");
        lab.transform.SetParent(beaconRoot.transform, false);
        lab.transform.localPosition = new Vector3(0, 12f, 0);
        beaconText = lab.AddComponent<TextMeshPro>();
        beaconText.alignment = TextAlignmentOptions.Center; beaconText.fontSize = 6; beaconText.fontStyle = FontStyles.Bold;
        beaconText.color = new Color(1f, 0.55f, 0.1f); beaconText.rectTransform.sizeDelta = new Vector2(30, 8);
        beaconLabel = lab.transform;
    }

    void BuildUI()
    {
        var canvasGo = new GameObject("ProtestCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 36;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        var bg = panel.GetComponent<Image>(); bg.color = new Color(0, 0, 0, 0.55f); bg.raycastTarget = false;
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 1f); prt.pivot = new Vector2(0.5f, 1f);
        prt.anchoredPosition = new Vector2(0, -190); prt.sizeDelta = new Vector2(620, 88);

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(panel.transform, false);
        panelText = tgo.AddComponent<TextMeshProUGUI>();
        panelText.fontSize = 28; panelText.alignment = TextAlignmentOptions.Center; panelText.color = Color.white; panelText.raycastTarget = false;
        var trt = panelText.rectTransform; trt.anchorMin = new Vector2(0, 0.38f); trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;

        var back = new GameObject("BarBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(panel.transform, false);
        back.GetComponent<Image>().color = new Color(1, 1, 1, 0.2f); back.GetComponent<Image>().raycastTarget = false;
        var brt = (RectTransform)back.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f); brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0, 12); brt.sizeDelta = new Vector2(560, 14);

        var fill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(back.transform, false);
        fill.GetComponent<Image>().color = new Color(1f, 0.6f, 0.1f); fill.GetComponent<Image>().raycastTarget = false;
        barFill = (RectTransform)fill.transform; barFill.anchorMin = new Vector2(0, 0); barFill.anchorMax = new Vector2(0, 1);
        barFill.pivot = new Vector2(0, 0.5f); barFill.anchoredPosition = Vector2.zero; barFill.sizeDelta = new Vector2(0, 0);

        var sgo = new GameObject("Slogan", typeof(RectTransform));
        sgo.transform.SetParent(canvasGo.transform, false);
        sloganText = sgo.AddComponent<TextMeshProUGUI>();
        sloganText.fontSize = 62; sloganText.fontStyle = FontStyles.Bold; sloganText.alignment = TextAlignmentOptions.Center;
        sloganText.color = new Color(1f, 0.75f, 0.2f); sloganText.raycastTarget = false; sloganText.text = "";
        sloganText.outlineWidth = 0.25f; sloganText.outlineColor = new Color32(0, 0, 0, 255);
        var srt = sloganText.rectTransform; srt.anchorMin = new Vector2(0.1f, 0.58f); srt.anchorMax = new Vector2(0.9f, 0.7f); srt.offsetMin = srt.offsetMax = Vector2.zero;

        panel.SetActive(false);
    }

    // ------------------------------------------------------------------ logic
    bool IsInside(Vector3 p)
    {
        return Mathf.Abs(p.x - zoneCenter.x) <= halfSize.x && Mathf.Abs(p.z - zoneCenter.z) <= halfSize.y;
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        GameManager gm = GameManager.I;
        if (gm == null) return;

        if (lastDay < 0) lastDay = gm.day;
        if (gm.day != lastDay) { lastDay = gm.day; earnedToday = 0; acc = 0f; capNotified = false; }

        inside = IsInside(player.position);
        bool capped = earnedToday >= dailyCap;
        bool chanting = inside && !capped && Input.GetKey(chantKey);
        chantAmount = Mathf.MoveTowards(chantAmount, chanting ? 1f : 0f, Time.deltaTime * 4f);

        UpdateBeacon(capped);
        UpdateCrowd(chanting);

        panel.SetActive(inside);
        if (!inside) { sloganText.text = ""; return; }

        // earn points
        if (!capped)
        {
            acc += pointsPerSecond * (chanting ? chantMultiplier : 1) * Time.deltaTime;
            int whole = Mathf.FloorToInt(acc);
            if (whole >= 3)
            {
                int pts = Mathf.Min(whole, dailyCap - earnedToday);
                acc -= whole;
                earnedToday += pts;
                GameEvents.OnScore?.Invoke(pts, "Protest");
            }
        }
        else if (!capNotified)
        {
            capNotified = true;
            GameEvents.OnToast?.Invoke("The crowd is tired. Come back tomorrow for more protest points.");
        }

        // slogans while chanting
        if (chanting)
        {
            sloganTimer -= Time.deltaTime;
            if (sloganTimer <= 0f)
            {
                sloganTimer = 2.4f;
                sloganIdx = (sloganIdx + 1) % Slogans.Length;
                sloganText.text = Slogans[sloganIdx];
                SoundManager.Play("cheer", 0.8f, 0.95f + Random.value * 0.1f);
            }
        }
        else { sloganText.text = ""; sloganTimer = 0f; }

        // UI
        int rate = capped ? 0 : pointsPerSecond * (chanting ? chantMultiplier : 1);
        panelText.text = (chanting ? "<color=#ffcc33>CHANTING!</color>   " : "<color=#ffffff>PROTEST BONUS</color>   ") +
                         "<color=#7dff7d>+" + rate + "/s</color>   <size=75%>Today " + earnedToday + " / " + dailyCap + "</size>";
        barFill.sizeDelta = new Vector2(560f * Mathf.Clamp01(earnedToday / (float)dailyCap), 0f);
        InteractionPrompt.Show(chantKey.ToString(), capped ? "The crowd is tired" : (chanting ? "CHANTING!  +" + rate + " points/s" : "Hold to lead the chant (x" + chantMultiplier + " points)"), earnedToday / (float)dailyCap, capped);
    }

    void UpdateBeacon(bool capped)
    {
        float d = Vector3.Distance(player.position, zoneCenter);
        bool show = !inside && d < 380f;
        if (beaconRoot.activeSelf != show) beaconRoot.SetActive(show);
        if (!show) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        Color c = capped ? new Color(0.6f, 0.6f, 0.6f) : new Color(1f, 0.55f, 0.1f);
        beamMat.color = new Color(c.r, c.g, c.b, 0.12f + 0.18f * pulse);
        ringMat.color = new Color(c.r, c.g, c.b, 0.35f);
        beaconText.color = c;
        beaconText.text = capped ? "PROTEST (done for today)\n" + Mathf.RoundToInt(d) + " m" : "PROTEST - BONUS POINTS\n" + Mathf.RoundToInt(d) + " m";
        if (Camera.main != null) beaconLabel.rotation = Quaternion.LookRotation(beaconLabel.position - Camera.main.transform.position);
        beaconLabel.localScale = Vector3.one * Mathf.Clamp(d * 0.03f, 1f, 14f);
    }

    void UpdateCrowd(bool chanting)
    {
        for (int i = 0; i < crowd.Count; i++)
        {
            Transform t = crowd[i];
            if (t == null) continue;
            Vector3 p = t.position;
            float target = chanting ? crowdBaseY[i] + Mathf.Abs(Mathf.Sin(Time.time * 7f + i * 1.7f)) * 0.18f : crowdBaseY[i];
            p.y = Mathf.Lerp(p.y, target, 12f * Time.deltaTime);
            t.position = p;
        }
    }
}