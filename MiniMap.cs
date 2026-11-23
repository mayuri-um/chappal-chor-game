using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Minimap : MonoBehaviour
{
    const float Size = 280f;
    float zoom = 140f;   // metres from centre to edge; press M to switch
    Camera mapCam;
    RenderTexture rt;
    RectTransform mask, northLabel;
    Transform player;
    float renderTimer;

    class Poi { public Transform t; public RectTransform rt; public Image img; }
    readonly List<Poi> pois = new List<Poi>();
    readonly List<Image> copDots = new List<Image>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("Minimap").AddComponent<Minimap>(); }

    void Start()
    {
        rt = new RenderTexture(512, 512, 16);
        var camGo = new GameObject("MinimapCamera");
        camGo.transform.SetParent(transform, false);
        mapCam = camGo.AddComponent<Camera>();
        mapCam.orthographic = true; mapCam.targetTexture = rt;
        mapCam.clearFlags = CameraClearFlags.SolidColor; mapCam.backgroundColor = new Color(0.12f, 0.2f, 0.12f);
        mapCam.nearClipPlane = 1f; mapCam.farClipPlane = 900f; mapCam.enabled = false;

        var canvasGo = new GameObject("MinimapCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);

        Sprite circle = MakeCircle(128);
        var frame = MakeImage(canvasGo.transform, "Frame", circle, new Color(0, 0, 0, 0.7f), Size + 14f);
        var fr = frame.rectTransform;
        fr.anchorMin = fr.anchorMax = new Vector2(1, 1); fr.pivot = new Vector2(1, 1); fr.anchoredPosition = new Vector2(-24, -24);

        var maskImg = MakeImage(frame.transform, "Mask", circle, Color.white, Size);
        mask = maskImg.rectTransform; mask.anchorMin = mask.anchorMax = new Vector2(0.5f, 0.5f); mask.anchoredPosition = Vector2.zero;
        maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        var rawGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(mask, false);
        var raw = rawGo.GetComponent<RawImage>(); raw.texture = rt; raw.raycastTarget = false;
        var rr = (RectTransform)rawGo.transform; rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one; rr.offsetMin = rr.offsetMax = Vector2.zero;

        AddPoi("DealerZone", "$", new Color(1f, 0.8f, 0.1f), circle);
        AddPoi("SafeZone_Langar", "G", new Color(1f, 0.55f, 0.1f), circle);
        AddPoi("SafeZone_Sleep", "M", new Color(0.2f, 0.85f, 0.4f), circle);
        AddPoi("Landmark_MahaSwarnaMandir", "T", new Color(0.95f, 0.25f, 0.25f), circle);
        AddPoi("Landmark_MainMandir", "T", new Color(0.95f, 0.25f, 0.25f), circle);
        AddPoi("Landmark_SmallTemple_A", "t", new Color(0.95f, 0.45f, 0.45f), circle);
        AddPoi("Landmark_SmallTemple_B", "t", new Color(0.95f, 0.45f, 0.45f), circle);
        AddPoi("Landmark_SmallTemple_C", "t", new Color(0.95f, 0.45f, 0.45f), circle);
        AddPoi("Landmark_PoliceChowki", "P", new Color(0.3f, 0.5f, 1f), circle);
        AddPoi("BastiDrop", "B", new Color(0.7f, 0.4f, 0.9f), circle);
        AddPoi("Goal_DaaruTheka", "W", Color.white, circle);

        var north = MakeText(mask, "N", "N", 20, Color.white);
        northLabel = north.rectTransform;

        var me = MakeImage(mask, "You", circle, new Color(0.2f, 1f, 0.3f), 16f);
        me.rectTransform.anchoredPosition = Vector2.zero;
    }

    void AddPoi(string objName, string letter, Color col, Sprite circle)
    {
        GameObject go = GameObject.Find(objName);
        if (go == null) return;
        var img = MakeImage(mask, "POI_" + objName, circle, col, 24f);
        MakeText(img.transform, "L", letter, 15, Color.black);
        pois.Add(new Poi { t = go.transform, rt = img.rectTransform, img = img });
    }

    void Update()
    {
        if (mapCam == null) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            player = p.transform;
        }
        if (Input.GetKeyDown(KeyCode.M)) zoom = zoom < 200f ? 450f : 140f;

        float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
        mapCam.orthographicSize = zoom;
        mapCam.transform.position = player.position + Vector3.up * 400f;
        mapCam.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
        renderTimer -= Time.deltaTime;
        if (renderTimer <= 0f) { renderTimer = 0.08f; mapCam.Render(); }

        float k = (Size / 2f) / zoom, limit = Size / 2f - 12f;
        Quaternion inv = Quaternion.Euler(0f, -yaw, 0f);

        foreach (var poi in pois)
        {
            Vector3 l = inv * (poi.t.position - player.position);
            Place(poi.rt, poi.img, new Vector2(l.x, l.z) * k, limit);
        }

        Vector3 n = inv * Vector3.forward;
        northLabel.anchoredPosition = new Vector2(n.x, n.z).normalized * (Size / 2f - 12f);

        int idx = 0;
        if (PeopleManager.I != null)
            foreach (var cop in PeopleManager.I.police)
            {
                Vector3 l = inv * (cop.transform.position - player.position);
                Vector2 ui = new Vector2(l.x, l.z) * k;
                if (ui.magnitude > limit) continue;
                if (idx >= 14) break;
                while (copDots.Count <= idx)
                {
                    var d = MakeImage(mask, "Cop", mask.GetComponent<Image>().sprite, Color.blue, 10f);
                    copDots.Add(d);
                }
                var dot = copDots[idx++];
                dot.gameObject.SetActive(true);
                dot.rectTransform.anchoredPosition = ui;
                dot.color = cop.mode == Pedestrian.Mode.Chase ? Color.red : new Color(0.3f, 0.5f, 1f);
            }
        for (int i = idx; i < copDots.Count; i++) copDots[i].gameObject.SetActive(false);
    }

    static void Place(RectTransform r, Image img, Vector2 ui, float limit)
    {
        float m = ui.magnitude;
        bool edge = m > limit;
        if (edge) ui = ui / m * limit;
        r.anchoredPosition = ui;
        var c = img.color; c.a = edge ? 0.55f : 1f; img.color = c;
    }

    void OnDestroy() { if (rt != null) rt.Release(); }

    static Image MakeImage(Transform parent, string name, Sprite s, Color c, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>(); img.sprite = s; img.color = c; img.raycastTarget = false;
        var r = (RectTransform)go.transform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(size, size);
        return img;
    }

    static TMP_Text MakeText(Transform parent, string name, string text, int size, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = c;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(40, 30);
        return t;
    }

    static Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float c = (size - 1) / 2f, r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
            }
        tex.SetPixels32(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}