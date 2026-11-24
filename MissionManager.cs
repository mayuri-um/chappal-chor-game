using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs by itself (no need to attach it to anything).
// Needs: GameManager, GameEvents, InteractionPrompt, Witness and the map markers
// PanditStash, BastiDrop, VendorMission_FruitCart, VendorMission_ChaiStall, VendorMission_Tailor.

public class MissionHitReceiver : MonoBehaviour
{
    public bool hit;
    void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag("Chappal")) hit = true;
    }
}

public class MissionManager : MonoBehaviour
{
    enum State { Available, Active, Done }
    enum Kind { Pandit, Rival, Milk, Pickpocket }

    // ------------------------------------------------------------------ world beacon (beam + ring + label)
    class Beacon
    {
        public GameObject root;
        public Material beam, ring;
        public TextMeshPro text;
        public Transform label;
        public Color color;

        public Beacon(string name, Color col, float height)
        {
            color = col;
            Shader sh = Shader.Find("Sprites/Default");
            root = new GameObject("Beacon_" + name);
            beam = new Material(sh); ring = new Material(sh);

            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0, height / 2f, 0);
            b.transform.localScale = new Vector3(0.5f, height / 2f, 0.5f);
            b.GetComponent<Renderer>().sharedMaterial = beam;

            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(r.GetComponent<Collider>());
            r.transform.SetParent(root.transform, false);
            r.transform.localPosition = new Vector3(0, 0.08f, 0);
            r.transform.localScale = new Vector3(3f, 0.02f, 3f);
            r.GetComponent<Renderer>().sharedMaterial = ring;

            var lab = new GameObject("Label");
            lab.transform.SetParent(root.transform, false);
            lab.transform.localPosition = new Vector3(0, 3.4f, 0);
            text = lab.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center; text.fontSize = 5; text.fontStyle = FontStyles.Bold;
            text.color = col; text.rectTransform.sizeDelta = new Vector2(30, 6);
            label = lab.transform;
            root.SetActive(false);
        }

        public void Show(Vector3 pos, string txt, float dist, float pulse)
        {
            root.SetActive(true);
            root.transform.position = pos;
            Color c = color; c.a = 0.14f + 0.2f * pulse; beam.color = c;
            c.a = 0.6f; ring.color = c;
            text.text = txt + "\n" + Mathf.RoundToInt(dist) + " m";
            if (Camera.main != null) label.rotation = Quaternion.LookRotation(label.position - Camera.main.transform.position);
            label.localScale = Vector3.one * Mathf.Clamp(dist * 0.025f, 0.8f, 12f);
        }

        public void Hide() { if (root != null && root.activeSelf) root.SetActive(false); }
        public void Kill() { if (root != null) Object.Destroy(root); }
    }

    class Mission
    {
        public Kind kind;
        public string title, objective = "";
        public State state;
        public Vector3 startPos;
        public int reward;
        public float timer, held, cooldown, fleeTimer, wanderTimer;
        public int stage;
        public bool fleeing;
        public GameObject actor, prop;
        public MissionHitReceiver receiver;
        public Vector3 pickPos, wanderGoal;
        public Beacon beacon;
    }

    readonly List<Mission> missions = new List<Mission>();
    Transform player;
    Vector3 bastiPos; bool bastiFound;
    int karma, lastDay = -1;
    float perkTime;
    bool promptShown;
    TMP_Text panel;
    GameObject panelBg;
    static readonly RaycastHit[] buf = new RaycastHit[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("MissionManager").AddComponent<MissionManager>(); }

    // ------------------------------------------------------------------ setup
    void Start()
    {
        karma = PlayerPrefs.GetInt("karma", 0);

        GameObject b = GameObject.Find("BastiDrop");
        if (b != null) { bastiPos = b.transform.position; bastiFound = true; }
        else Debug.LogWarning("MissionManager: BastiDrop not found, the pandit mission cannot finish.");

        AddMission(Kind.Pandit, "PanditStash", "Robin Hood: rob the pandit, feed the poor", 500, new Color(1f, 0.85f, 0.2f));
        AddMission(Kind.Rival, "VendorMission_FruitCart", "Fruit cart: chase off the rival hawker", 300, new Color(1f, 0.85f, 0.2f));
        AddMission(Kind.Milk, "VendorMission_ChaiStall", "Chai stall: carry the milk cans", 250, new Color(1f, 0.85f, 0.2f));
        AddMission(Kind.Pickpocket, "VendorMission_Tailor", "Tailor: guard the cart", 400, new Color(1f, 0.85f, 0.2f));
        BuildPanel();
    }

    void AddMission(Kind kind, string markerName, string title, int reward, Color color)
    {
        GameObject g = GameObject.Find(markerName);
        if (g == null) { Debug.LogWarning("MissionManager: marker '" + markerName + "' not found, skipping that mission."); return; }
        missions.Add(new Mission
        {
            kind = kind,
            title = title,
            reward = reward,
            startPos = g.transform.position,
            state = State.Available,
            beacon = new Beacon(markerName, color, 45f)
        });
    }

    void BuildPanel()
    {
        var canvasGo = new GameObject("MissionCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 35;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        panelBg = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelBg.transform.SetParent(canvasGo.transform, false);
        var img = panelBg.GetComponent<Image>(); img.color = new Color(0, 0, 0, 0.45f); img.raycastTarget = false;
        var rt = (RectTransform)panelBg.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f); rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(20, -20); rt.sizeDelta = new Vector2(620, 420);

        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(panelBg.transform, false);
        panel = go.AddComponent<TextMeshProUGUI>();
        panel.fontSize = 25; panel.color = Color.white; panel.raycastTarget = false; panel.alignment = TextAlignmentOptions.TopLeft;
        var tr = panel.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(14, 10); tr.offsetMax = new Vector2(-14, -10);
    }

    // ------------------------------------------------------------------ main loop
    void Update()
    {
        if (Time.timeScale == 0f || missions.Count == 0) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        GameManager gm = GameManager.I;
        if (gm == null) return;

        if (lastDay < 0) lastDay = gm.day;
        if (gm.day != lastDay) { lastDay = gm.day; ResetAll(); }

        promptShown = false;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        foreach (Mission m in missions)
        {
            if (m.cooldown > 0f) m.cooldown -= Time.deltaTime;
            switch (m.kind)
            {
                case Kind.Pandit: TickPandit(m, gm, pulse); break;
                case Kind.Rival: TickRival(m, gm, pulse); break;
                case Kind.Milk: TickMilk(m, gm, pulse); break;
                case Kind.Pickpocket: TickPickpocket(m, gm, pulse); break;
            }
        }

        if (karma >= 3 && gm.heat > 0 && Time.time > perkTime)
        {
            gm.heat--; perkTime = Time.time + 20f;
            GameEvents.OnToast?.Invoke("The basti hides you. Heat is cooling down.");
        }
        UpdatePanel();
    }

    // ------------------------------------------------------------------ helpers
    static float D(Vector3 a, Vector3 b) { a.y = 0; b.y = 0; return Vector3.Distance(a, b); }

    bool Watched()
    {
        foreach (var w in Witness.All) if (w != null && w.CanSee(player)) return true;
        return false;
    }

    bool Hold(Mission m, KeyCode key, string keyName, string label, float need, bool warning = false)
    {
        if (promptShown) { m.held = 0f; return false; }
        promptShown = true;
        m.held = Input.GetKey(key) ? m.held + Time.deltaTime : 0f;
        InteractionPrompt.Show(keyName, label, m.held / need, warning);
        if (m.held >= need) { m.held = 0f; return true; }
        return false;
    }

    void Complete(Mission m, GameManager gm, int money, int score, string msg)
    {
        m.state = State.Done; m.stage = 0;
        m.objective = "";
        Clear(m);
        gm.money += money;
        GameEvents.OnScore?.Invoke(score, "Mission");
        karma++; PlayerPrefs.SetInt("karma", karma);
        GameEvents.OnToast?.Invoke(msg + (money > 0 ? "  +Rs " + money : "") + "  +" + score + " points  +1 karma");
        m.beacon.Hide();
    }

    void Fail(Mission m, string msg)
    {
        Clear(m);
        m.state = State.Available; m.stage = 0; m.cooldown = 20f; m.objective = "";
        GameEvents.OnAlert?.Invoke(msg);
        m.beacon.Hide();
    }

    void Clear(Mission m)
    {
        if (m.actor != null) Destroy(m.actor);
        if (m.prop != null) Destroy(m.prop);
        m.actor = null; m.prop = null; m.receiver = null; m.fleeing = false;
    }

    void ResetAll()
    {
        foreach (var m in missions)
        {
            Clear(m); m.state = State.Available; m.stage = 0; m.cooldown = 0f; m.objective = ""; m.beacon.Hide();
        }
        GameEvents.OnToast?.Invoke("New day: the missions are available again.");
    }

    static Material LitMat(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        var mat = new Material(s); mat.color = c; return mat;
    }

    static GameObject Prim(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color c)
    {
        var g = GameObject.CreatePrimitive(type);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = LitMat(c);
        return g;
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

    GameObject MakeActor(string name, Vector3 pos, Color shirt, Color pants, bool capped)
    {
        var root = new GameObject(name);
        root.transform.position = new Vector3(pos.x, GroundY(pos, null, pos.y), pos.z);
        Prim(root.transform, PrimitiveType.Capsule, new Vector3(0, 1.1f, 0), new Vector3(0.5f, 0.38f, 0.38f), shirt);
        Prim(root.transform, PrimitiveType.Capsule, new Vector3(0, 0.42f, 0), new Vector3(0.42f, 0.42f, 0.36f), pants);
        Prim(root.transform, PrimitiveType.Sphere, new Vector3(0, 1.78f, 0), new Vector3(0.27f, 0.3f, 0.28f), new Color(0.55f, 0.36f, 0.24f));
        Prim(root.transform, PrimitiveType.Sphere, new Vector3(0, 1.84f, -0.03f), new Vector3(0.29f, 0.28f, 0.3f), new Color(0.05f, 0.04f, 0.04f));
        if (capped) Prim(root.transform, PrimitiveType.Cylinder, new Vector3(0, 1.98f, 0.05f), new Vector3(0.3f, 0.03f, 0.34f), new Color(0.1f, 0.1f, 0.1f));
        var col = root.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.95f, 0); col.radius = 0.35f; col.height = 1.9f;
        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        return root;
    }

    // move on the ground toward a point; returns true when there
    bool Walk(GameObject a, Vector3 target, float speed)
    {
        Vector3 d = target - a.transform.position; d.y = 0f;
        float dist = d.magnitude;
        Vector3 pos = a.transform.position;
        if (dist > 0.05f)
        {
            Vector3 dir = d / dist;
            pos += dir * Mathf.Min(speed * Time.deltaTime, dist);
            a.transform.rotation = Quaternion.Slerp(a.transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
        }
        pos.y = Mathf.Lerp(pos.y, GroundY(pos, a.transform, a.transform.position.y), 12f * Time.deltaTime);
        a.transform.position = pos;
        return dist < 0.4f;
    }

    // ------------------------------------------------------------------ 1. pandit: Robin Hood
    void TickPandit(Mission m, GameManager gm, float pulse)
    {
        if (m.state == State.Done) return;
        if (m.state == State.Available)
        {
            float d = D(player.position, m.startPos);
            if (d < 220f) m.beacon.Show(m.startPos, "ROB THE PANDIT", d, pulse); else m.beacon.Hide();
            m.objective = "Steal the donation box at the pandit's house";
            if (d < 7f)
            {
                bool watched = Watched();
                if (Hold(m, KeyCode.E, "E", watched ? "The pandit is watching!" : "Hold to steal the temple donation box", 2.5f, watched))
                {
                    GameEvents.OnCrime?.Invoke(player.position, "theft");
                    if (watched)
                    {
                        GameEvents.OnAlert?.Invoke("THE PANDIT CAUGHT YOU!");
                        gm.AddHeat(2);
                    }
                    else
                    {
                        m.state = State.Active;
                        GameEvents.OnToast?.Invoke("You took the donation box! Rush it to the basti.");
                    }
                }
            }
            return;
        }

        // active: carrying the donation to the basti
        if (!bastiFound) return;
        float db = D(player.position, bastiPos);
        m.objective = "Take the stolen donation to the basti";
        m.beacon.Show(bastiPos, "GIVE TO THE POOR", db, pulse);
        if (db < 10f && Hold(m, KeyCode.F, "F", "Hold to give the money to the poor", 2.5f))
        {
            gm.heat = 0;
            Complete(m, gm, 0, m.reward, "The poor bless you! Heat cleared.");
        }
    }

    // ------------------------------------------------------------------ 2. fruit cart: chase off the rival hawker
    void TickRival(Mission m, GameManager gm, float pulse)
    {
        if (m.state == State.Done) return;
        if (m.state == State.Available)
        {
            float d = D(player.position, m.startPos);
            if (d < 90f && m.cooldown <= 0f) m.beacon.Show(m.startPos, "MISSION: RIVAL HAWKER", d, pulse); else m.beacon.Hide();
            if (d < 7f && m.cooldown <= 0f && Hold(m, KeyCode.F, "F", "Hold to accept: chase off the rival hawker (+Rs " + m.reward + ")", 1f))
            {
                m.state = State.Active; m.timer = 90f; m.fleeing = false;
                Vector3 p = m.startPos + new Vector3(6f, 0f, 5f);
                m.actor = MakeActor("RivalHawker", p, new Color(0.75f, 0.1f, 0.1f), new Color(0.2f, 0.2f, 0.25f), false);
                m.receiver = m.actor.AddComponent<MissionHitReceiver>();
                m.wanderGoal = p; m.wanderTimer = 0f;
                GameEvents.OnToast?.Invoke("Mission started: hit the rival hawker with a chappal!");
            }
            return;
        }

        m.timer -= Time.deltaTime;
        if (m.actor == null) { Fail(m, "MISSION FAILED"); return; }
        float dr = D(player.position, m.actor.transform.position);
        m.objective = "Hit the rival hawker with a chappal  (" + Mathf.CeilToInt(Mathf.Max(0, m.timer)) + " s)";
        m.beacon.Show(m.actor.transform.position, "RIVAL HAWKER", dr, pulse);

        if (!m.fleeing && m.receiver != null && m.receiver.hit)
        {
            m.fleeing = true; m.fleeTimer = 2.2f;
            GameEvents.OnScore?.Invoke(30, "Rival hawker");
            GameEvents.OnToast?.Invoke("Bullseye! The hawker runs away!");
        }
        if (m.fleeing)
        {
            Vector3 away = m.actor.transform.position - player.position; away.y = 0f; away.Normalize();
            Walk(m.actor, m.actor.transform.position + away * 20f, 9f);
            m.fleeTimer -= Time.deltaTime;
            if (m.fleeTimer <= 0f) Complete(m, gm, m.reward, 150, "The hawker is gone, the fruit seller is happy!");
            return;
        }
        m.wanderTimer -= Time.deltaTime;
        if (m.wanderTimer <= 0f || Walk(m.actor, m.wanderGoal, 1.8f))
        {
            Vector2 r = Random.insideUnitCircle * 9f;
            m.wanderGoal = m.startPos + new Vector3(r.x, 0f, r.y);
            m.wanderTimer = Random.Range(2f, 5f);
        }
        if (m.timer <= 0f) Fail(m, "MISSION FAILED: the hawker stole the customers");
    }

    // ------------------------------------------------------------------ 3. chai stall: carry the milk cans
    void TickMilk(Mission m, GameManager gm, float pulse)
    {
        if (m.state == State.Done) return;
        if (m.state == State.Available)
        {
            float d = D(player.position, m.startPos);
            if (d < 90f && m.cooldown <= 0f) m.beacon.Show(m.startPos, "MISSION: MILK CANS", d, pulse); else m.beacon.Hide();
            if (d < 7f && m.cooldown <= 0f && Hold(m, KeyCode.F, "F", "Hold to accept: carry the milk cans (+Rs " + m.reward + ")", 1f))
            {
                m.state = State.Active; m.stage = 0; m.timer = 150f;
                m.pickPos = m.startPos + new Vector3(-30f, 0f, 35f);
                m.pickPos.y = GroundY(m.pickPos, null, m.startPos.y);
                var cans = new GameObject("MilkCans"); cans.transform.position = m.pickPos;
                for (int i = 0; i < 3; i++)
                {
                    Prim(cans.transform, PrimitiveType.Cylinder, new Vector3((i - 1) * 0.5f, 0.4f, 0), new Vector3(0.35f, 0.4f, 0.35f), new Color(0.8f, 0.82f, 0.85f));
                    Prim(cans.transform, PrimitiveType.Cylinder, new Vector3((i - 1) * 0.5f, 0.82f, 0), new Vector3(0.2f, 0.03f, 0.2f), new Color(0.5f, 0.5f, 0.55f));
                }
                m.prop = cans;
                GameEvents.OnToast?.Invoke("Mission started: fetch the milk cans and bring them to the stall.");
            }
            return;
        }

        m.timer -= Time.deltaTime;
        int secs = Mathf.CeilToInt(Mathf.Max(0, m.timer));
        if (m.stage == 0)
        {
            float dp = D(player.position, m.pickPos);
            m.objective = "Pick up the milk cans  (" + secs + " s)";
            m.beacon.Show(m.pickPos, "MILK CANS", dp, pulse);
            if (dp < 5f && Hold(m, KeyCode.E, "E", "Hold to pick up the milk cans", 1.5f))
            {
                m.stage = 1;
                if (m.prop != null) { Destroy(m.prop); m.prop = null; }
                GameEvents.OnToast?.Invoke("Got the cans! Take them back to the chai stall.");
            }
        }
        else
        {
            float ds = D(player.position, m.startPos);
            m.objective = "Deliver the milk to the chai stall  (" + secs + " s)";
            m.beacon.Show(m.startPos, "CHAI STALL", ds, pulse);
            if (ds < 6f && Hold(m, KeyCode.F, "F", "Hold to hand over the milk cans", 1.5f))
            {
                Complete(m, gm, m.reward, 120, "Fresh milk delivered! The chai stall owner is thrilled.");
                return;
            }
        }
        if (m.timer <= 0f) Fail(m, "MISSION FAILED: the milk went sour");
    }

    // ------------------------------------------------------------------ 4. tailor: stop the pickpocket
    void TickPickpocket(Mission m, GameManager gm, float pulse)
    {
        if (m.state == State.Done) return;
        if (m.state == State.Available)
        {
            float d = D(player.position, m.startPos);
            if (d < 90f && m.cooldown <= 0f) m.beacon.Show(m.startPos, "MISSION: GUARD THE CART", d, pulse); else m.beacon.Hide();
            if (d < 7f && m.cooldown <= 0f && Hold(m, KeyCode.F, "F", "Hold to accept: guard the tailor's cart (+Rs " + m.reward + ")", 1f))
            {
                m.state = State.Active; m.fleeing = false;
                float ang = Random.Range(0f, 360f);
                Vector3 p = m.startPos + Quaternion.Euler(0, ang, 0) * Vector3.forward * 45f;
                m.actor = MakeActor("Pickpocket", p, new Color(0.12f, 0.12f, 0.14f), new Color(0.1f, 0.1f, 0.12f), true);
                m.receiver = m.actor.AddComponent<MissionHitReceiver>();
                GameEvents.OnToast?.Invoke("A pickpocket is coming for the cart! Hit him with a chappal!");
            }
            return;
        }

        if (m.actor == null) { Fail(m, "MISSION FAILED"); return; }
        float dc = D(m.actor.transform.position, m.startPos);
        m.objective = "Stop the pickpocket!  He is " + Mathf.RoundToInt(dc) + " m from the cart";
        m.beacon.Show(m.actor.transform.position, "PICKPOCKET", D(player.position, m.actor.transform.position), pulse);

        if (!m.fleeing && m.receiver != null && m.receiver.hit)
        {
            m.fleeing = true; m.fleeTimer = 2f;
            m.actor.transform.rotation = Quaternion.Euler(80f, m.actor.transform.eulerAngles.y, 0f);
            GameEvents.OnScore?.Invoke(30, "Pickpocket");
            GameEvents.OnToast?.Invoke("Got him! The pickpocket drops flat.");
        }
        if (m.fleeing)
        {
            m.fleeTimer -= Time.deltaTime;
            if (m.fleeTimer <= 0f) Complete(m, gm, m.reward, 200, "The tailor's cart is safe!");
            return;
        }
        Walk(m.actor, m.startPos, 2.6f);
        if (dc < 2.5f) Fail(m, "THE PICKPOCKET ROBBED THE CART!");
    }

    // ------------------------------------------------------------------ tracker panel
    void UpdatePanel()
    {
        if (panel == null) return;
        var sb = new StringBuilder();
        sb.Append("<b>MISSIONS</b>   <color=#ffd24d>Karma " + karma + "</color>\n");
        foreach (Mission m in missions)
        {
            switch (m.state)
            {
                case State.Done:
                    sb.Append("<color=#66ff66>[done] " + m.title + "</color>\n");
                    break;
                case State.Active:
                    sb.Append("<color=#7fe9ff>[>] " + m.title + "</color>\n     <size=85%>" + m.objective + "</size>\n");
                    break;
                default:
                    string cd = m.cooldown > 0f ? "  <size=80%>(retry in " + Mathf.CeilToInt(m.cooldown) + " s)</size>" : "";
                    sb.Append("<color=#bbbbbb>[ ] " + m.title + "  <size=80%>" + Mathf.RoundToInt(D(player.position, m.startPos)) + " m away</size>" + cd + "</color>\n");
                    break;
            }
        }
        if (karma < 3) sb.Append("<size=75%><color=#999999>Karma 3+ : the basti hides you and your heat cools faster.</color></size>");
        panel.text = sb.ToString();
        var rt = (RectTransform)panelBg.transform;
        rt.sizeDelta = new Vector2(620, 40f + panel.textInfo.lineCount * 31f);
    }
}