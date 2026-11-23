using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MerchantHighlight : MonoBehaviour
{
    class Marker
    {
        public Transform target, root, ring, label;
        public Material beamMat, ringMat;
        public TextMeshPro text;
        public string baseText;
        public float maxDist;
        public Color color;
        public bool big;
        public Vector3 ringBase;
    }

    readonly List<Marker> markers = new List<Marker>();
    Transform player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("MerchantHighlight").AddComponent<MerchantHighlight>();
    }

    void Start()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "NPC_Dealer_01")
                Add(t, "KABADI DEALER\nSELL CHAPPALS HERE", new Color(1f, 0.8f, 0.1f), 90f, 4f, float.MaxValue, true);
            else if (t.name.StartsWith("NPC_Vendor_"))
                Add(t, "VENDOR", new Color(0.2f, 0.9f, 1f), 14f, 1.5f, 80f, false);
        }
    }

    void Add(Transform t, string text, Color color, float beamHeight, float ringRadius, float maxDist, bool big)
    {
        var m = new Marker { target = t, baseText = text, maxDist = maxDist, color = color, big = big };
        var rootGo = new GameObject("Marker_" + t.name);
        m.root = rootGo.transform;
        m.root.position = t.position;
        Shader shader = Shader.Find("Sprites/Default");

        m.beamMat = new Material(shader);
        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(beam.GetComponent<Collider>());
        beam.transform.SetParent(m.root, false);
        beam.transform.localPosition = new Vector3(0, beamHeight / 2f, 0);
        float w = big ? 0.7f : 0.35f;
        beam.transform.localScale = new Vector3(w, beamHeight / 2f, w);
        beam.GetComponent<Renderer>().sharedMaterial = m.beamMat;

        m.ringMat = new Material(shader);
        var c = color; c.a = 0.7f; m.ringMat.color = c;
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(m.root, false);
        ring.transform.localPosition = new Vector3(0, 0.08f, 0);
        m.ringBase = new Vector3(ringRadius * 2f, 0.02f, ringRadius * 2f);
        ring.transform.localScale = m.ringBase;
        ring.GetComponent<Renderer>().sharedMaterial = m.ringMat;
        m.ring = ring.transform;

        var lab = new GameObject("Label");
        lab.transform.SetParent(m.root, false);
        lab.transform.localPosition = new Vector3(0, big ? 7f : 3.2f, 0);
        m.text = lab.AddComponent<TextMeshPro>();
        m.text.alignment = TextAlignmentOptions.Center;
        m.text.fontSize = 6;
        m.text.fontStyle = FontStyles.Bold;
        m.text.color = color;
        m.text.rectTransform.sizeDelta = new Vector2(30, 6);
        m.label = lab.transform;

        markers.Add(m);
    }

    void Update()
    {
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        Camera cam = Camera.main;
        if (cam == null) return;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        foreach (var m in markers)
        {
            if (m.target == null) continue;
            m.root.position = m.target.position;
            float d = Vector3.Distance(player.position, m.target.position);
            bool show = d <= m.maxDist;
            if (m.root.gameObject.activeSelf != show) m.root.gameObject.SetActive(show);
            if (!show) continue;

            Color c = m.color; c.a = 0.18f + 0.22f * pulse;
            m.beamMat.color = c;
            m.ring.localScale = new Vector3(m.ringBase.x * (1f + 0.12f * pulse), m.ringBase.y, m.ringBase.z * (1f + 0.12f * pulse));
            m.text.text = m.baseText + "\n" + Mathf.RoundToInt(d) + " m";
            m.label.rotation = Quaternion.LookRotation(m.label.position - cam.transform.position);
            float s = m.big ? Mathf.Clamp(d * 0.025f, 1f, 14f) : Mathf.Clamp(d * 0.02f, 0.6f, 3f);
            m.label.localScale = Vector3.one * s;
        }
    }
}