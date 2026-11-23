using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WifeEnding : MonoBehaviour
{
    public bool devKeys = true;          // F9 = give the goal money, F10 = teleport to the wife. Turn off when you finish testing.
    public float triggerRadius = 14f;
    public float holdTime = 3f;

    Transform theka, player, wife;
    GameObject beamGroup, endScreen;
    Material beamMat, ringMat;
    Transform ringT;
    TextMeshPro label;
    float held;
    bool ended;
    bool playing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("WifeEnding").AddComponent<WifeEnding>(); }

    void Start()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == "Goal_DaaruTheka") { theka = t; break; }
        if (theka == null)
        {
            Debug.LogWarning("WifeEnding: Goal_DaaruTheka was not found in the scene.");
            enabled = false; return;
        }
        BuildWife();
        BuildBeam();
    }

    // ------------------------------------------------------------------ building the wife and the beacon
    Vector3 GroundAt(Vector3 p)
    {
        if (Physics.Raycast(new Vector3(p.x, theka.position.y + 0.5f, p.z), Vector3.down, out RaycastHit h, 8f, ~0, QueryTriggerInteraction.Ignore))
            return h.point;
        return new Vector3(p.x, theka.position.y - 1.5f, p.z);
    }

    static Material LitMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var m = new Material(s); m.color = c; return m;
    }

    static void Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = LitMat(c);
    }

    void StartCutscene(GameManager gm)
    {
        playing = true; held = 0f;
        if (wife != null) wife.gameObject.SetActive(false);
        if (beamGroup != null) beamGroup.SetActive(false);
        DarooCutscene.Play(theka, player, () => { playing = false; ShowEnding(gm); });
    }

    void BuildWife()
    {
        var root = new GameObject("Wife");
        root.transform.position = GroundAt(theka.position + new Vector3(5.2f, 0f, 0.5f));
        wife = root.transform;
        Prim(wife, PrimitiveType.Capsule, new Vector3(0, 0.78f, 0), new Vector3(0.5f, 0.72f, 0.5f), new Color(0.85f, 0.1f, 0.45f)); // sari
        Prim(wife, PrimitiveType.Sphere, new Vector3(0, 1.64f, 0), new Vector3(0.27f, 0.3f, 0.27f), new Color(0.62f, 0.42f, 0.3f));   // head
        Prim(wife, PrimitiveType.Sphere, new Vector3(0, 1.68f, -0.05f), new Vector3(0.3f, 0.32f, 0.3f), new Color(0.08f, 0.06f, 0.05f)); // hair
        Prim(wife, PrimitiveType.Capsule, new Vector3(0, 1.15f, 0.02f), new Vector3(0.56f, 0.3f, 0.52f), new Color(1f, 0.75f, 0.2f)); // gold border
    }

    void BuildBeam()
    {
        beamGroup = new GameObject("WifeBeam");
        beamGroup.transform.position = GroundAt(theka.position);
        Shader sh = Shader.Find("Sprites/Default");

        beamMat = new Material(sh);
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(beamGroup.transform, false);
        beam.transform.localPosition = new Vector3(0, 60, 0);
        beam.transform.localScale = new Vector3(1.2f, 60, 1.2f);
        beam.GetComponent<Renderer>().sharedMaterial = beamMat;

        ringMat = new Material(sh);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(beamGroup.transform, false);
        ring.transform.localPosition = new Vector3(0, 0.1f, 0);
        ring.transform.localScale = new Vector3(9, 0.02f, 9);
        ring.GetComponent<Renderer>().sharedMaterial = ringMat;
        ringT = ring.transform;

        var lab = new GameObject("Label");
        lab.transform.SetParent(beamGroup.transform, false);
        lab.transform.localPosition = new Vector3(0, 9f, 0);
        label = lab.AddComponent<TextMeshPro>();
        label.alignment = TextAlignmentOptions.Center; label.fontSize = 6; label.fontStyle = FontStyles.Bold;
        label.color = new Color(1f, 0.4f, 0.75f); label.rectTransform.sizeDelta = new Vector2(30, 8);

        beamGroup.SetActive(false);
    }

    // ------------------------------------------------------------------ every frame
    void Update()
    {
        if (theka == null) return;
        var gm = GameManager.I;

        if (ended)
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            return;
        }

        if (playing) return;

        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        if (devKeys) DevKeys(gm);
        if (gm == null) return;

        bool ready = gm.money >= gm.goalMoney;
        float d = Vector3.Distance(player.position, theka.position);

        Vector3 look = player.position - wife.position; look.y = 0f;
        if (look.sqrMagnitude > 0.01f && d < 40f) wife.rotation = Quaternion.Slerp(wife.rotation, Quaternion.LookRotation(look), 5f * Time.deltaTime);

        if (!beamGroup.activeSelf) beamGroup.SetActive(true);
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            Color c = ready ? new Color(1f, 0.35f, 0.7f) : new Color(0.7f, 0.45f, 1f);
            beamMat.color = new Color(c.r, c.g, c.b, (ready ? 0.18f : 0.12f) + (ready ? 0.22f : 0.12f) * pulse);
            ringMat.color = new Color(c.r, c.g, c.b, ready ? 0.6f : 0.4f);
            float s = 9f * (1f + 0.1f * pulse);
            ringT.localScale = new Vector3(s, 0.02f, s);
            label.color = c;
            label.text = ready
                ? "YOUR WIFE\nWine shop - go to her!\n" + Mathf.RoundToInt(d) + " m"
                : "WINE SHOP\nNeed Rs " + (gm.goalMoney - gm.money) + " more\n" + Mathf.RoundToInt(d) + " m";
            if (Camera.main != null)
                label.transform.rotation = Quaternion.LookRotation(label.transform.position - Camera.main.transform.position);
            label.transform.localScale = Vector3.one * Mathf.Clamp(d * 0.03f, 1f, 16f);
        }

        if (d < triggerRadius)
        {
            if (ready)
            {
                held = Input.GetKey(KeyCode.F) ? held + Time.deltaTime : 0f;
                InteractionPrompt.Show("F", "Hold to meet your wife and party!", held / holdTime);
                if (held >= holdTime) StartCutscene(gm);
            }
            else
            {
                held = 0f;
                InteractionPrompt.Show("F", "Need Rs " + (gm.goalMoney - gm.money) + " more for the party", 0f, true);
            }
        }
        else held = 0f;
    }

    void DevKeys(GameManager gm)
    {
        if (Input.GetKeyDown(KeyCode.F9) && gm != null)
        {
            gm.money = gm.goalMoney;
            GameEvents.OnToast?.Invoke("DEV: money set to the goal.");
        }
        if (Input.GetKeyDown(KeyCode.F10)) Teleport(theka.position + new Vector3(-2f, 2.5f, -1.5f));
    }

    void Teleport(Vector3 pos)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = pos;
        if (cc != null) cc.enabled = true;
    }

    // ------------------------------------------------------------------ the ending
    void ShowEnding(GameManager gm)
    {
        ended = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;

        endScreen = new GameObject("EndingCanvas", typeof(Canvas), typeof(CanvasScaler));
        var canvas = endScreen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var scaler = endScreen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(endScreen.transform, false);
        bg.GetComponent<Image>().color = new Color(0, 0, 0, 0.9f);
        var r = (RectTransform)bg.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;

        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(endScreen.transform, false);
        var t = tgo.AddComponent<TextMeshProUGUI>();
        t.alignment = TextAlignmentOptions.Center; t.fontSize = 44; t.color = Color.white;
        t.text = "<size=96><color=#ffcc33>THE END</color></size>\n\n" +
                 "<color=#ff77bb>Biwi: \"Itni der kar di! Chal, party karte hain!\"</color>\n\n" +
                 "A whole month of drinking with your wife begins...\n\n" +
                 "Days survived: " + gm.day + "     Score: " + gm.score + "     Paisa: Rs " + gm.money + "\n\n" +
                 "<size=32>Press R to start a new game</size>";
        var tr = t.rectTransform;
        tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(120, 60); tr.offsetMax = new Vector2(-120, -60);
    }

    void Restart()
    {
        foreach (string k in new[] { "score", "money", "day", "chappals" }) PlayerPrefs.DeleteKey(k);
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.money = 0; gm.score = 0; gm.day = 1; gm.heat = 0; gm.energy = 100f;
            gm.ateLangarToday = false; gm.chappalValues.Clear();
        }
        GameObject start = GameObject.Find("PlayerStart");
        if (player != null && start != null) Teleport(start.transform.position + Vector3.up * 2f);
        if (wife != null) wife.gameObject.SetActive(true);
        if (endScreen != null) Destroy(endScreen);
        ended = false; held = 0f;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }
}