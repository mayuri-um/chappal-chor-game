using UnityEngine;
using UnityEngine.UI;

public class CrosshairDot : MonoBehaviour
{
    Image dot, outline;
    Color current = Color.white;
    float currentScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        new GameObject("CrosshairDot").AddComponent<CrosshairDot>();
    }

    void Start()
    {
        var canvasGo = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        Sprite circle = MakeCircle(64);
        outline = MakeImage(canvasGo.transform, "Outline", circle, new Color(0, 0, 0, 0.55f), 16);
        dot = MakeImage(canvasGo.transform, "Dot", circle, Color.white, 9);
    }

    void Update()
    {
        if (dot == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Color target = Color.white;
        float scale = 1f;
        Vector3 origin = cam.transform.position + cam.transform.forward * 0.6f;
        if (Physics.Raycast(origin, cam.transform.forward, out RaycastHit hit, 80f, ~0, QueryTriggerInteraction.Ignore))
        {
            Target t = hit.collider.GetComponentInParent<Target>();
            if (t != null)
            {
                if (t.points < 0) target = new Color(1f, 0.2f, 0.2f);
                else if (t.points > 0) target = new Color(0.3f, 1f, 0.35f);
                scale = 1.7f;
            }
        }

        current = Color.Lerp(current, target, Time.deltaTime * 14f);
        currentScale = Mathf.Lerp(currentScale, scale, Time.deltaTime * 14f);
        dot.color = current;
        dot.rectTransform.localScale = Vector3.one * currentScale;
        outline.rectTransform.localScale = Vector3.one * currentScale;
    }

    static Image MakeImage(Transform parent, string name, Sprite s, Color c, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = s; img.color = c; img.raycastTarget = false;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        return img;
    }

    static Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        var px = new Color32[size * size];
        float c = (size - 1) / 2f, r = size / 2f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}