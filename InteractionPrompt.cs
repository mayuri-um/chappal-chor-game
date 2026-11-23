using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionPrompt : MonoBehaviour
{
    static InteractionPrompt instance;
    public static InteractionPrompt I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("InteractionPrompt");
                instance = go.AddComponent<InteractionPrompt>();
            }
            return instance;
        }
    }

    CanvasGroup group;
    Image ring;
    TMP_Text keyText, labelText;
    float lastRequest = -10f;

    public static void Show(string key, string label, float progress, bool warning = false)
    {
        I.Request(key, label, progress, warning);
    }

    void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    void Request(string key, string label, float progress, bool warning)
    {
        lastRequest = Time.unscaledTime;
        keyText.text = key;
        labelText.text = label;
        ring.fillAmount = Mathf.Clamp01(progress);
        Color c = warning ? new Color(0.95f, 0.25f, 0.2f) : new Color(1f, 0.8f, 0.2f);
        ring.color = c;
        labelText.color = warning ? c : Color.white;
    }

    void Update()
    {
        bool active = Time.unscaledTime - lastRequest < 0.15f;
        group.alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.unscaledDeltaTime * 8f);
    }

    void Build()
    {
        var canvasGo = new GameObject("PromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f; group.blocksRaycasts = false; group.interactable = false;

        Sprite ringSprite = MakeRing(256, 100f, 128f);
        Sprite discSprite = MakeRing(256, 0f, 128f);

        var root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.28f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(140, 140);

        MakeImage(root.transform, "RingBack", ringSprite, new Color(1, 1, 1, 0.25f), 140);
        ring = MakeImage(root.transform, "RingFill", ringSprite, new Color(1f, 0.8f, 0.2f), 140);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0f;
        MakeImage(root.transform, "Centre", discSprite, new Color(0, 0, 0, 0.65f), 104);

        keyText = MakeText(root.transform, "Key", 56, Vector2.zero, FontStyles.Bold);
        labelText = MakeText(root.transform, "Label", 32, new Vector2(0, -105), FontStyles.Normal);
    }

    static Image MakeImage(Transform parent, string name, Sprite s, Color c, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
        return img;
    }

    static TMP_Text MakeText(Transform parent, string name, int size, Vector2 pos, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size; tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.raycastTarget = false;
        var r = tmp.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = pos;
        r.sizeDelta = new Vector2(900, 80);
        return tmp;
    }

    static Sprite MakeRing(int size, float inner, float outer)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - d) * (inner <= 0f ? 1f : Mathf.Clamp01(d - inner));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}