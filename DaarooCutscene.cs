using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Night-time cutscene at the wine shop: you and your wife sit at the table, pour, clink glasses and drink.
// Started by WifeEnding (see the edits). Press SPACE during it to skip to the end.
public class DarooCutscene : MonoBehaviour
{
    public static DarooCutscene Instance;

    public static void Play(Transform theka, Transform player, System.Action onDone)
    {
        if (Instance != null) Instance.Restore();
        var go = new GameObject("DarooCutscene");
        Instance = go.AddComponent<DarooCutscene>();
        Instance.Begin(theka, player, onDone);
    }

    public static void Cleanup()
    {
        if (Instance != null) Instance.Restore();
    }

    const float EndT = 58f, FadeOutStart = 56f;

    class Figure
    {
        public Transform root, armR, glass, liquid;
        public float baseFill;
    }

    // ------------------------------------------------------------------ state
    Transform theka, player;
    System.Action onDone;
    Vector3 T, C, perp, along;
    float groundY, t;
    bool finished, doneCalled;

    Camera cam;
    Light warm, flash, sun;
    float sunInt0, ambient0; Color sunCol0;
    Figure husband, wife;
    Transform bottle;
    GameObject canvasGo;
    Image fade;
    TMP_Text subtitle, popup, dayText;

    readonly List<GameObject> spawned = new List<GameObject>();
    readonly List<Behaviour> disabled = new List<Behaviour>();
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    CharacterController ccOff;
    int bottlesDropped;

    static readonly (float a, float b, string who, string text)[] Lines =
    {
        (2.0f, 5.8f,  "W", "Itni mehnat ke baad, aaj poora mahina sirf hum aur daaru!"),
        (6.0f, 9.0f,  "Y", "Aur ek bhi chappal nahi bachi poore shehar mein."),
        (10.0f, 12.0f, "W", "Chal, pehla peg tere naam ka, chor sahab."),
        (12.3f, 14.5f, "Y", "Aur doosra tere naam ka, rani."),
        (19.4f, 21.6f, "B", "CHEERS!"),
        (28.0f, 30.0f, "Y", "Aaah... maza aa gaya."),
        (31.0f, 33.5f, "W", "Ek aur? Mahina lamba hai!"),
        (35.0f, 37.6f, "Y", "Teri kasam, itna sukoon kabhi nahi mila."),
        (39.0f, 40.4f, "N", "Mahina beet raha hai..."),
        (50.0f, 53.0f, "W", "Ab agle mahine ka paisa kahan se aayega, chor sahab?"),
        (53.4f, 56.0f, "Y", "Mandir ke bahar ki chappal hai na!"),
    };

    // ------------------------------------------------------------------ setup
    void Begin(Transform theka_, Transform player_, System.Action done)
    {
        theka = theka_; player = player_; onDone = done;
        Time.timeScale = 0f;

        FreezePlayer();
        HideCanvases();

        groundY = theka.position.y;
        Vector3 tp = theka.position + new Vector3(6.5f, 0f, -1f);
        groundY = GroundAt(tp.x, tp.z, theka.position.y);
        T = new Vector3(tp.x, groundY, tp.z);
        C = T + Vector3.up * 1.05f;
        perp = new Vector3(1f, 0f, -1f).normalized;
        along = new Vector3(1f, 0f, 1f).normalized;

        BuildScene();
        BuildCamera();
        BuildUI();
        BuildLights();
    }

    float GroundAt(float x, float z, float fallback)
    {
        if (Physics.Raycast(new Vector3(x, fallback + 6f, z), Vector3.down, out RaycastHit h, 14f, ~0, QueryTriggerInteraction.Ignore))
            return h.point.y;
        return fallback;
    }

    void FreezePlayer()
    {
        if (player == null) return;
        foreach (var mb in player.GetComponentsInChildren<MonoBehaviour>())
        {
            string n = mb.GetType().Name;
            if (mb.enabled && (n == "FirstPersonController" || n == "ThirdPersonController" || n == "ChappalThrower" ||
                               n == "StarterAssetsInputs" || n == "PlayerInput" || n == "BasicRigidBodyPush"))
            { mb.enabled = false; disabled.Add(mb); }
        }
        var cc = player.GetComponent<CharacterController>();
        if (cc != null && cc.enabled) { cc.enabled = false; ccOff = cc; }
        foreach (var r in player.GetComponentsInChildren<Renderer>())
            if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
    }

    void HideCanvases()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.enabled) { c.enabled = false; hiddenCanvases.Add(c); }
    }

    static Material LitMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s); m.color = c; return m;
    }

    GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c, Vector3? euler = null)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        if (euler.HasValue) g.transform.localRotation = Quaternion.Euler(euler.Value);
        g.GetComponent<Renderer>().sharedMaterial = LitMat(c);
        return g;
    }

    Transform MakeRoot(string name, Vector3 pos)
    {
        var g = new GameObject(name);
        g.transform.position = pos;
        spawned.Add(g);
        return g.transform;
    }

    Figure BuildFigure(string name, Vector3 pos, Color shirt, Color pants, Color skin, Color hair, bool female)
    {
        var f = new Figure();
        f.root = MakeRoot(name, pos);
        Vector3 flat = T - pos; flat.y = 0f;
        f.root.rotation = Quaternion.LookRotation(flat);
        Transform r = f.root;

        Prim(r, PrimitiveType.Capsule, new Vector3(0, 0.95f, 0), new Vector3(0.46f, 0.38f, 0.34f), shirt);
        Prim(r, PrimitiveType.Sphere, new Vector3(0, 1.62f, 0), new Vector3(0.26f, 0.29f, 0.27f), skin);
        Prim(r, PrimitiveType.Sphere, new Vector3(0, 1.67f, -0.04f), new Vector3(0.29f, 0.3f, 0.3f), hair);
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(r, PrimitiveType.Cylinder, new Vector3(0.13f * sx, 0.56f, 0.3f), new Vector3(0.15f, 0.3f, 0.15f), pants, new Vector3(90, 0, 0));
            Prim(r, PrimitiveType.Capsule, new Vector3(0.13f * sx, 0.28f, 0.58f), new Vector3(0.13f, 0.28f, 0.13f), pants);
            Prim(r, PrimitiveType.Sphere, new Vector3(0.13f * sx, 0.04f, 0.66f), new Vector3(0.15f, 0.08f, 0.22f), new Color(0.12f, 0.1f, 0.1f));
        }
        if (female)
        {
            Prim(r, PrimitiveType.Sphere, new Vector3(0, 1.76f, -0.17f), new Vector3(0.14f, 0.14f, 0.14f), hair);
            Prim(r, PrimitiveType.Sphere, new Vector3(0, 1.64f, 0.13f), new Vector3(0.035f, 0.035f, 0.035f), new Color(0.8f, 0.05f, 0.05f));
            Prim(r, PrimitiveType.Capsule, new Vector3(0, 0.95f, 0.01f), new Vector3(0.48f, 0.15f, 0.36f), new Color(1f, 0.75f, 0.2f));
        }
        else
        {
            Prim(r, PrimitiveType.Cube, new Vector3(0, 1.57f, 0.125f), new Vector3(0.11f, 0.02f, 0.03f), new Color(0.05f, 0.04f, 0.04f));
            Prim(r, PrimitiveType.Cube, new Vector3(-0.02f, 1.3f, 0.06f), new Vector3(0.6f, 0.07f, 0.24f), new Color(0.75f, 0.12f, 0.1f), new Vector3(0, 0, 38));
        }

        // left arm (resting)
        var armL = new GameObject("ArmL").transform;
        armL.SetParent(r, false); armL.localPosition = new Vector3(-0.27f, 1.2f, 0.05f); armL.localRotation = Quaternion.Euler(-55, 0, 0);
        Prim(armL, PrimitiveType.Capsule, new Vector3(0, -0.25f, 0), new Vector3(0.12f, 0.25f, 0.12f), shirt);
        Prim(armL, PrimitiveType.Sphere, new Vector3(0, -0.52f, 0), new Vector3(0.1f, 0.1f, 0.1f), skin);

        // right arm (holds the glass)
        f.armR = new GameObject("ArmR").transform;
        f.armR.SetParent(r, false); f.armR.localPosition = new Vector3(0.27f, 1.2f, 0.05f); f.armR.localRotation = Quaternion.Euler(-70, 0, 0);
        Prim(f.armR, PrimitiveType.Capsule, new Vector3(0, -0.25f, 0), new Vector3(0.12f, 0.25f, 0.12f), shirt);
        Prim(f.armR, PrimitiveType.Sphere, new Vector3(0, -0.52f, 0), new Vector3(0.1f, 0.1f, 0.1f), skin);

        // glass with amber liquid (world space, driven by code)
        f.glass = MakeRoot(name + "_Glass", pos);
        Prim(f.glass, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.07f, 0.06f, 0.07f), new Color(0.85f, 0.92f, 0.97f));
        f.liquid = Prim(f.glass, PrimitiveType.Cylinder, new Vector3(0, -0.058f, 0), new Vector3(0.058f, 0.001f, 0.058f), new Color(0.9f, 0.55f, 0.1f)).transform;
        return f;
    }

    void BuildScene()
    {
        husband = BuildFigure("You", T + new Vector3(-0.9f, 0f, -0.8f), new Color(0.93f, 0.9f, 0.82f), new Color(0.2f, 0.22f, 0.3f),
                              new Color(0.55f, 0.36f, 0.24f), new Color(0.05f, 0.04f, 0.04f), false);
        wife = BuildFigure("Wife", T + new Vector3(0.9f, 0f, 0.8f), new Color(0.85f, 0.1f, 0.45f), new Color(0.75f, 0.08f, 0.38f),
                           new Color(0.62f, 0.42f, 0.3f), new Color(0.06f, 0.04f, 0.04f), true);

        // bottle
        bottle = MakeRoot("Bottle", T + Vector3.up * 0.8f);
        Prim(bottle, PrimitiveType.Cylinder, new Vector3(0, 0.13f, 0), new Vector3(0.055f, 0.13f, 0.055f), new Color(0.18f, 0.45f, 0.2f));
        Prim(bottle, PrimitiveType.Cylinder, new Vector3(0, 0.33f, 0), new Vector3(0.022f, 0.08f, 0.022f), new Color(0.18f, 0.45f, 0.2f));
        Prim(bottle, PrimitiveType.Cube, new Vector3(0, 0.14f, 0.056f), new Vector3(0.07f, 0.1f, 0.005f), new Color(0.95f, 0.9f, 0.8f));

        // snacks on a plate
        var plate = MakeRoot("Snacks", T + new Vector3(-0.15f, 0.8f, 0.22f));
        Prim(plate, PrimitiveType.Cylinder, new Vector3(0, 0.01f, 0), new Vector3(0.3f, 0.01f, 0.3f), Color.white);
        for (int i = 0; i < 7; i++)
            Prim(plate, PrimitiveType.Sphere, new Vector3(Random.Range(-0.08f, 0.08f), 0.045f, Random.Range(-0.08f, 0.08f)),
                 new Vector3(0.06f, 0.045f, 0.06f), i % 3 == 0 ? new Color(0.9f, 0.7f, 0.2f) : new Color(0.55f, 0.32f, 0.12f));

        // a small lamp on the table
        var lamp = MakeRoot("TableLamp", T + new Vector3(0.12f, 0.8f, -0.1f));
        Prim(lamp, PrimitiveType.Cylinder, new Vector3(0, 0.05f, 0), new Vector3(0.1f, 0.05f, 0.1f), new Color(0.2f, 0.2f, 0.22f));
        Prim(lamp, PrimitiveType.Sphere, new Vector3(0, 0.2f, 0), new Vector3(0.13f, 0.15f, 0.13f), new Color(1f, 0.85f, 0.5f));
    }

    void BuildCamera()
    {
        var go = new GameObject("CutsceneCamera");
        spawned.Add(go);
        cam = go.AddComponent<Camera>();
        cam.depth = 100f; cam.fieldOfView = 52f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1500f;
        cam.clearFlags = CameraClearFlags.Skybox;
        Vector3 p, look; CamPose(0f, out p, out look);
        go.transform.position = p; go.transform.rotation = Quaternion.LookRotation(look - p);
    }

    void BuildLights()
    {
        sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        if (sun != null) { sunInt0 = sun.intensity; sunCol0 = sun.color; }
        ambient0 = RenderSettings.ambientIntensity;

        var wg = new GameObject("WarmLight"); spawned.Add(wg);
        wg.transform.position = T + new Vector3(0, 2.1f, 0);
        warm = wg.AddComponent<Light>(); warm.type = LightType.Point; warm.range = 14f; warm.color = new Color(1f, 0.7f, 0.4f); warm.intensity = 0f;

        var fg = new GameObject("ToastFlash"); spawned.Add(fg);
        fg.transform.position = C;
        flash = fg.AddComponent<Light>(); flash.type = LightType.Point; flash.range = 6f; flash.color = new Color(1f, 0.95f, 0.7f); flash.intensity = 0f;
    }

    void BuildUI()
    {
        canvasGo = new GameObject("CutsceneCanvas", typeof(Canvas), typeof(CanvasScaler));
        spawned.Add(canvasGo);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        var bg = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        fade = bg.GetComponent<Image>(); fade.color = Color.black; fade.raycastTarget = false;
        var fr = (RectTransform)bg.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;

        subtitle = MakeText("Subtitle", 42, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.2f), TextAlignmentOptions.Center, false);
        popup = MakeText("Popup", 110, new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.72f), TextAlignmentOptions.Center, true);
        dayText = MakeText("Day", 130, new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.9f), TextAlignmentOptions.Center, true);
        var hint = MakeText("Hint", 26, new Vector2(0.7f, 0.01f), new Vector2(0.99f, 0.06f), TextAlignmentOptions.BottomRight, false);
        hint.text = "<alpha=#99>Press SPACE to skip";
    }

    TMP_Text MakeText(string name, int size, Vector2 amin, Vector2 amax, TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(canvasGo.transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false; t.text = "";
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal; t.outlineWidth = 0.2f; t.outlineColor = new Color32(0, 0, 0, 255);
        var r = t.rectTransform; r.anchorMin = amin; r.anchorMax = amax; r.offsetMin = r.offsetMax = Vector2.zero;
        return t;
    }

    // ------------------------------------------------------------------ timeline
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    void CamPose(float time, out Vector3 pos, out Vector3 look)
    {
        Vector3 up = Vector3.up; look = C;
        if (time < 9f) pos = Vector3.Lerp(T + perp * 7.2f + up * 2.8f, T + perp * 4.4f + up * 1.9f, Smooth(time / 9f));
        else if (time < 19f) pos = Vector3.Lerp(T + perp * 3.6f + up * 1.5f, T + perp * 2.7f + up * 1.4f, Smooth((time - 9f) / 10f));
        else if (time < 24f) pos = Vector3.Lerp(T + perp * 1.7f + up * 1.25f, T + perp * 1.3f + up * 1.2f, Smooth((time - 19f) / 5f));
        else if (time < 38f)
        {
            float ang = Mathf.Lerp(0f, 110f, Smooth((time - 24f) / 14f));
            pos = T + Quaternion.AngleAxis(ang, up) * (perp * 3.2f) + up * 1.6f;
        }
        else
        {
            float u = Smooth((time - 38f) / 14f);
            Vector3 start = T + Quaternion.AngleAxis(110f, up) * (perp * 3.2f) + up * 1.6f;
            Vector3 end = T + new Vector3(6f, 5.5f, -8f);
            pos = Vector3.Lerp(start, end, u);
            look = Vector3.Lerp(C, T + up * 0.8f, u);
        }
    }

    // how raised the arm is (0 = resting, 1 = glass at mouth)
    static float Sip(float time, float start)
    {
        float x = time - start;
        if (x < 0f || x > 2f) return 0f;
        if (x < 0.6f) return Smooth(x / 0.6f);
        if (x < 1.4f) return 1f;
        return 1f - Smooth((x - 1.4f) / 0.6f);
    }

    float ClinkAmount(float time)
    {
        if (time < 19.3f || time > 22f) return 0f;
        if (time < 20.1f) return Smooth((time - 19.3f) / 0.8f);
        if (time < 21.2f) return 1f;
        return 1f - Smooth((time - 21.2f) / 0.8f);
    }

    float FillOf(Figure f, float time)
    {
        bool isYou = f == husband;
        float poured = isYou ? Smooth((time - 10.5f) / 1.2f) : Smooth((time - 12.2f) / 1.2f);
        float drank = 0f;
        float[] sips = isYou ? new[] { 28.9f, 31.9f, 34.9f } : new[] { 29.4f, 32.4f, 35.4f };
        foreach (float s in sips) if (time > s) drank += 0.18f;
        return Mathf.Max(0f, poured * 0.7f - drank);
    }

    void Update()
    {
        if (warm != null)
            warm.intensity = (3.2f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) + 0.3f * Mathf.Sin(Time.unscaledTime * 23f)) * Mathf.Clamp01(t / 2f);

        if (finished) return;
        t += Time.unscaledDeltaTime;
        if (Input.GetKeyDown(KeyCode.Space) && t < FadeOutStart) t = FadeOutStart;

        float night = Smooth(t / 2.5f);
        if (sun != null) { sun.intensity = Mathf.Lerp(sunInt0, 0.12f, night); sun.color = Color.Lerp(sunCol0, new Color(0.5f, 0.6f, 1f), night); }
        RenderSettings.ambientIntensity = Mathf.Lerp(ambient0, 0.25f, night);

        Vector3 p, look; CamPose(t, out p, out look);
        cam.transform.position = p;
        cam.transform.rotation = Quaternion.LookRotation(look - p);

        float a = 1f - Smooth(t / 1.6f);
        if (t > FadeOutStart) a = Mathf.Max(a, Smooth((t - FadeOutStart) / (EndT - FadeOutStart)) * 0.6f);
        fade.color = new Color(0, 0, 0, a);

        UpdateSubtitles();
        UpdateFigures();
        UpdateBottle();
        UpdateMontage();

        if (t >= EndT)
        {
            finished = true;
            if (!doneCalled) { doneCalled = true; subtitle.text = ""; popup.text = ""; dayText.text = ""; onDone?.Invoke(); }
        }
    }

    void UpdateSubtitles()
    {
        string s = "";
        foreach (var l in Lines)
            if (t >= l.a && t <= l.b)
            {
                string col = l.who == "W" ? "#ff77bb" : (l.who == "Y" ? "#ffd24d" : "#ffffff");
                string name = l.who == "W" ? "Biwi: " : (l.who == "Y" ? "Tu: " : "");
                s = "<color=" + col + ">" + name + "</color>" + l.text;
            }
        subtitle.text = s;
        popup.text = (t > 20.05f && t < 21.2f) ? "<color=#ffe27a>*CLINK*</color>" : "";
        if (flash != null) flash.intensity = Mathf.Clamp01(1f - Mathf.Abs(t - 20.1f) * 5f) * 8f;
    }

    void UpdateFigures()
    {
        float clink = ClinkAmount(t);
        UpdateFigure(husband, Sip(t, 28f) + Sip(t, 31f) + Sip(t, 34f), clink, -along);
        UpdateFigure(wife, Sip(t, 28.5f) + Sip(t, 31.5f) + Sip(t, 34.5f), clink, along);
    }

    void UpdateFigure(Figure f, float sip, float clink, Vector3 side)
    {
        sip = Mathf.Clamp01(sip);
        f.armR.localRotation = Quaternion.Euler(Mathf.Lerp(-70f, -150f, sip), 0f, 0f);
        Vector3 hand = f.armR.TransformPoint(new Vector3(0, -0.58f, 0.04f));
        Vector3 target = C + side * 0.04f;
        f.glass.position = Vector3.Lerp(hand, target, clink);
        f.glass.rotation = f.root.rotation * Quaternion.Euler(-75f * sip, 0f, 0f);

        float fill = FillOf(f, t);
        float h = Mathf.Max(0.001f, 0.05f * fill);
        f.liquid.localScale = new Vector3(0.058f, h, 0.058f);
        f.liquid.localPosition = new Vector3(0, -0.058f + h, 0);
        f.liquid.gameObject.SetActive(fill > 0.01f);
    }

    void UpdateBottle()
    {
        Vector3 rest = T + Vector3.up * 0.8f;
        Vector3 pos = rest; Quaternion rot = Quaternion.identity;
        if (t > 10f && t < 13.9f)
        {
            Transform g; float blend, pour;
            if (t < 11.9f) { g = husband.glass; blend = Smooth((t - 10f) / 0.6f); pour = Smooth((t - 10.5f) / 0.4f) * (1f - Smooth((t - 11.5f) / 0.4f)); }
            else if (t < 13.4f) { g = wife.glass; blend = Smooth((t - 11.9f) / 0.6f); pour = Smooth((t - 12.2f) / 0.4f) * (1f - Smooth((t - 13.1f) / 0.3f)); }
            else { g = wife.glass; blend = 1f - Smooth((t - 13.4f) / 0.5f); pour = 0f; }

            Vector3 dir = g.position - rest; dir.y = 0f; dir.Normalize();
            Vector3 pourPos = g.position + Vector3.up * 0.226f - dir * 0.348f;
            pos = Vector3.Lerp(rest, pourPos, blend);
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            rot = Quaternion.AngleAxis(110f * pour, axis);
        }
        bottle.position = pos; bottle.rotation = rot;
    }

    void UpdateMontage()
    {
        if (t >= 40f && t < 50f)
        {
            int day = Mathf.Clamp(Mathf.FloorToInt((t - 40f) / 0.3f) + 1, 1, 30);
            dayText.text = "<color=#ffd24d>Day " + day + "</color>";
            int want = day / 2;
            while (bottlesDropped < want) DropBottle();
        }
        else if (t >= 36.5f && t < 40f) dayText.text = "";
        else if (t >= 50f) dayText.text = "";
    }

    void DropBottle()
    {
        bottlesDropped++;
        float ang = Random.Range(0f, 360f), r = Random.Range(1.6f, 3.6f);
        Vector3 off = Quaternion.Euler(0, ang, 0) * Vector3.forward * r;
        if (off.z > 1.2f) off.z = -off.z;   // keep them away from the shop wall
        float x = T.x + off.x, z = T.z + off.z;
        var b = new GameObject("EmptyBottle"); spawned.Add(b);
        b.transform.position = new Vector3(x, GroundAt(x, z, groundY) + 0.05f, z);
        b.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
        Prim(b.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.13f, 0.1f), new Color(0.18f, 0.45f, 0.2f), new Vector3(0, 0, 90));
        Prim(b.transform, PrimitiveType.Cylinder, new Vector3(0.2f, 0, 0), new Vector3(0.04f, 0.08f, 0.04f), new Color(0.18f, 0.45f, 0.2f), new Vector3(0, 0, 90));
    }

    // ------------------------------------------------------------------ undo everything (called when you press R on the ending screen)
    void Restore()
    {
        if (sun != null) { sun.intensity = sunInt0; sun.color = sunCol0; }
        RenderSettings.ambientIntensity = ambient0;
        foreach (var b in disabled) if (b != null) b.enabled = true;
        foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
        foreach (var c in hiddenCanvases) if (c != null) c.enabled = true;
        if (ccOff != null) ccOff.enabled = true;
        foreach (var g in spawned) if (g != null) Destroy(g);
        Time.timeScale = 1f;
        if (Instance == this) Instance = null;
        Destroy(gameObject);
    }
}