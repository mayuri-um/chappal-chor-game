using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Runs by itself (no need to attach it to anything).
// Title screen on launch (with a slow camera orbit around the clock tower), pause menu on Esc,
// controls page, settings (volume, music, effects, mouse sensitivity, fullscreen), save game and new game.
// The menu works with the mouse (hover + click + drag sliders) and the keyboard (W/S or arrows, Enter, A/D or arrows).
// Needs: GameManager, GameEvents, MissionManager, NetaRise, DayNightCycle, SoundManager.
public class GameMenus : MonoBehaviour
{
    enum Layout { Left, Center }

    class Item
    {
        public string label, key;
        public System.Action click;
        public System.Func<string> dynamic;
        public System.Action<float> change;
        public bool slider, disabled;
        public float value;
        public GameObject go;
        public RectTransform rect, barRect;
        public Image bg, fill;
        public TMP_Text text;
    }

    public string gameTitle = "CHAPPAL CHOR";
    public string subtitle = "Steal the chappals. Hit the posters. Find your wife.";

    bool menuOpen, onTitle, inProgress;
    Canvas canvas;
    GameObject root;
    RectTransform column;
    TMP_Text titleText, subText, bodyText, statusText;
    Image dim, leftPanel;
    List<Item> page = new List<Item>();
    int sel;
    Item dragging;
    System.Action onBack;
    Vector2 lastMouse;
    float statusTimer, hideTimer;

    // title camera
    Camera titleCam; Vector3 chowkPos; bool chowkFound; float orbit;

    // freezing the player while a menu is open
    readonly List<Behaviour> frozen = new List<Behaviour>();
    readonly List<Canvas> hidden = new List<Canvas>();
    CharacterController ccOff;

    // mouse sensitivity (found by name, so it also works if you swap controllers)
    MonoBehaviour fpc; FieldInfo rotField; float baseRot = -1f;

    static readonly string[] SettingKeys = { "set_master", "set_music", "set_sfx", "set_sens" };
    static readonly float[] SettingDefaults = { 1f, 0.5f, 0.85f, 0.41f };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("GameMenus").AddComponent<GameMenus>(); }

    // ------------------------------------------------------------------ setup
    void Awake()
    {
        Time.timeScale = 0f;
        BuildUI();
    }

    void Start()
    {
        GameObject c = GameObject.Find("MainChowk_Center");
        if (c != null) { chowkPos = c.transform.position; chowkFound = true; }
        ApplyAllSettings();
        OpenTitle();
    }

    void BuildUI()
    {
        var cgo = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler));
        cgo.transform.SetParent(transform, false);
        canvas = cgo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 400;
        var scaler = cgo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        root = cgo;

        dim = Img(canvas.transform, "Dim", new Color(0, 0, 0, 0.5f));
        Stretch(dim.rectTransform, Vector2.zero, Vector2.one);

        leftPanel = Img(canvas.transform, "LeftPanel", new Color(0, 0, 0, 0.55f));
        Stretch(leftPanel.rectTransform, new Vector2(0, 0), new Vector2(0.46f, 1));

        titleText = Txt(canvas.transform, "Title", 120, TextAlignmentOptions.TopLeft, true);
        titleText.color = new Color(1f, 0.8f, 0.2f);
        subText = Txt(canvas.transform, "Subtitle", 34, TextAlignmentOptions.TopLeft, false);
        subText.color = new Color(0.9f, 0.9f, 0.9f);

        bodyText = Txt(canvas.transform, "Body", 34, TextAlignmentOptions.Top, false);
        statusText = Txt(canvas.transform, "Status", 32, TextAlignmentOptions.Bottom, true);
        statusText.color = new Color(0.5f, 1f, 0.5f);
        var sr = statusText.rectTransform; sr.anchorMin = new Vector2(0.2f, 0f); sr.anchorMax = new Vector2(0.8f, 0f); sr.pivot = new Vector2(0.5f, 0f);
        sr.anchoredPosition = new Vector2(0, 40); sr.sizeDelta = new Vector2(0, 60);

        var colGo = new GameObject("Column", typeof(RectTransform));
        colGo.transform.SetParent(canvas.transform, false);
        column = (RectTransform)colGo.transform;
    }

    static Image Img(Transform parent, string name, Color c)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var i = go.GetComponent<Image>(); i.color = c; i.raycastTarget = false;
        return i;
    }

    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static TMP_Text Txt(Transform parent, string name, int size, TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = size; t.alignment = align; t.color = Color.white; t.raycastTarget = false; t.richText = true;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.outlineWidth = 0.18f; t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }

    // ------------------------------------------------------------------ pages
    bool HasSave() { return PlayerPrefs.GetInt("day", 1) > 1 || PlayerPrefs.GetInt("money", 0) > 0 || PlayerPrefs.GetInt("score", 0) > 0 || PlayerPrefs.GetInt("karma", 0) > 0; }

    Item Btn(string label, System.Action click) { return new Item { label = label, click = click }; }

    void OpenTitle()
    {
        menuOpen = true; onTitle = true;
        Time.timeScale = 0f;
        root.SetActive(true);
        Freeze(); CreateTitleCam();
        ShowTitlePage();
    }

    void ShowTitlePage()
    {
        var items = new List<Item>();
        if (inProgress) items.Add(Btn("Resume", CloseMenu));
        else if (HasSave()) items.Add(Btn("Continue", CloseMenu));
        items.Add(Btn("New Game", () => { if (HasSave() || inProgress) ShowConfirmNew(); else DoNewGame(); }));
        items.Add(Btn("Controls", ShowControls));
        items.Add(Btn("Settings", ShowSettings));
        items.Add(Btn("Quit", Application.Quit));
        titleText.text = gameTitle;
        ShowPage(Layout.Left, gameTitle, subtitle, items, null, null, 120);
    }

    void ShowConfirmNew()
    {
        var items = new List<Item> { Btn("Yes, erase my save and start fresh", DoNewGame), Btn("Cancel", ShowTitlePage) };
        ShowPage(Layout.Left, "New Game?", "Your saved progress, karma and neta status will be erased.", items, null, ShowTitlePage, 76);
    }

    void OpenPause()
    {
        menuOpen = true; onTitle = false;
        Time.timeScale = 0f;
        root.SetActive(true);
        Freeze();
        ShowPausePage();
    }

    void ShowPausePage()
    {
        var items = new List<Item>
        {
            Btn("Resume", CloseMenu),
            Btn("Save Game", SaveGame),
            Btn("Controls", ShowControls),
            Btn("Settings", ShowSettings),
            Btn("Quit to Title", () => { inProgress = true; OpenTitle(); })
        };
        ShowPage(Layout.Center, "PAUSED", "", items, null, CloseMenu, 76);
    }

    void ShowControls()
    {
        string body =
            "<color=#ffcc33>MOVE</color>  W A S D        <color=#ffcc33>RUN</color>  Left Shift        <color=#ffcc33>JUMP</color>  Space        <color=#ffcc33>LOOK</color>  Mouse\n" +
            "<color=#ffcc33>THROW A CHAPPAL</color>  Left mouse button\n" +
            "<color=#ffcc33>STEAL FROM A RACK</color>  hold E\n" +
            "<color=#ffcc33>INTERACT</color>  hold F  (eat langar, sleep and save, sell, accept missions)\n" +
            "<color=#ffcc33>SPECIAL</color>  hold G  (seva, scrap, chanda)      hold C  (protest chant)\n" +
            "<color=#ffcc33>MAP ZOOM</color>  M        <color=#ffcc33>MUSIC</color>  N        <color=#ffcc33>PAUSE</color>  Esc\n\n" +
            "<color=#ffffff>GOAL:</color> steal chappals, sell them to the kabadi, collect Rs 50,000\nand go to your wife at the wine shop at the south end of the map.\n" +
            "Eat langar at the gurudwara every day before you sleep at the masjid.\n" +
            "Do good deeds for karma. Too much, and you become a Neta...";
        System.Action back = inProgressOrPause() ? (System.Action)ShowPausePage : ShowTitlePage;
        ShowPage(Layout.Center, "CONTROLS", "", new List<Item> { Btn("Back", back) }, body, back, 76);
    }

    bool inProgressOrPause() { return !onTitle; }

    void ShowSettings()
    {
        System.Action back = inProgressOrPause() ? (System.Action)ShowPausePage : ShowTitlePage;
        var items = new List<Item>
        {
            Slider("Master volume", "set_master", 0),
            Slider("Music", "set_music", 1),
            Slider("Effects", "set_sfx", 2),
            Slider("Mouse sensitivity", "set_sens", 3),
            new Item { label = "Fullscreen", dynamic = () => "Fullscreen:  " + (Screen.fullScreen ? "ON" : "OFF"), click = () => Screen.fullScreen = !Screen.fullScreen },
            Btn("Back", back)
        };
        ShowPage(Layout.Center, "SETTINGS", "", items, null, back, 76);
    }

    Item Slider(string label, string key, int idx)
    {
        return new Item { label = label, key = key, slider = true, value = PlayerPrefs.GetFloat(key, SettingDefaults[idx]), change = v => Apply(key, v) };
    }

    // ------------------------------------------------------------------ building a page
    void ClearItems()
    {
        foreach (var it in page) if (it.go != null) Destroy(it.go);
        page.Clear(); dragging = null;
    }

    void ShowPage(Layout layout, string title, string sub, List<Item> items, string body, System.Action back, int titleSize)
    {
        ClearItems();
        page = items; onBack = back; sel = 0;
        for (int i = 0; i < page.Count; i++) if (!page[i].disabled) { sel = i; break; }
        bool left = layout == Layout.Left;

        leftPanel.gameObject.SetActive(left);
        dim.color = left ? new Color(0, 0, 0, 0.12f) : new Color(0, 0, 0, 0.68f);

        titleText.text = title; titleText.fontSize = titleSize;
        titleText.alignment = left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
        var tr = titleText.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(left ? 0f : 0.5f, 1f); tr.pivot = new Vector2(left ? 0f : 0.5f, 1f);
        tr.anchoredPosition = left ? new Vector2(140, -110) : new Vector2(0, -80); tr.sizeDelta = new Vector2(1500, 190);

        subText.text = sub;
        subText.alignment = left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Top;
        var sr = subText.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(left ? 0f : 0.5f, 1f); sr.pivot = new Vector2(left ? 0f : 0.5f, 1f);
        sr.anchoredPosition = left ? new Vector2(146, -245) : new Vector2(0, -175); sr.sizeDelta = new Vector2(left ? 760 : 1400, 110);

        bodyText.gameObject.SetActive(body != null);
        if (body != null)
        {
            bodyText.text = body;
            var br = bodyText.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0.5f, 1f); br.pivot = new Vector2(0.5f, 1f);
            br.anchoredPosition = new Vector2(0, -190); br.sizeDelta = new Vector2(1600, 600);
        }

        float top = body != null ? -840f : (left ? -430f : (string.IsNullOrEmpty(sub) ? -230f : -300f));
        column.anchorMin = column.anchorMax = new Vector2(left ? 0f : 0.5f, 1f); column.pivot = new Vector2(0.5f, 1f);
        column.anchoredPosition = new Vector2(left ? 140f + 290f : 0f, top); column.sizeDelta = new Vector2(600, 10);

        float y = 0f;
        foreach (var it in page)
        {
            float h = it.slider ? 96f : 62f;
            var go = new GameObject("Item", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(column, false);
            it.go = go; it.bg = go.GetComponent<Image>(); it.bg.raycastTarget = false;
            it.rect = (RectTransform)go.transform;
            it.rect.anchorMin = it.rect.anchorMax = new Vector2(0.5f, 1f); it.rect.pivot = new Vector2(0.5f, 1f);
            it.rect.anchoredPosition = new Vector2(0, -y); it.rect.sizeDelta = new Vector2(580, h);

            it.text = Txt(go.transform, "Label", 36, it.slider ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Center, true);
            var lr = it.text.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(20, 0); lr.offsetMax = new Vector2(-20, it.slider ? -8 : 0);

            if (it.slider)
            {
                var track = Img(go.transform, "Track", new Color(1, 1, 1, 0.25f));
                it.barRect = track.rectTransform;
                it.barRect.anchorMin = new Vector2(0, 0); it.barRect.anchorMax = new Vector2(1, 0); it.barRect.pivot = new Vector2(0.5f, 0f);
                it.barRect.offsetMin = new Vector2(20, 14); it.barRect.offsetMax = new Vector2(-20, 34);
                it.fill = Img(it.barRect, "Fill", new Color(1f, 0.75f, 0.15f));
                it.fill.rectTransform.anchorMin = Vector2.zero; it.fill.rectTransform.anchorMax = new Vector2(it.value, 1f);
                it.fill.rectTransform.offsetMin = it.fill.rectTransform.offsetMax = Vector2.zero;
            }
            y += h + 12f;
        }
        RefreshVisuals();
    }

    void RefreshVisuals()
    {
        for (int i = 0; i < page.Count; i++)
        {
            var it = page[i];
            if (it.go == null) continue;
            bool on = i == sel && !it.disabled;
            it.bg.color = it.disabled ? new Color(1, 1, 1, 0.03f) : (on ? new Color(1f, 0.78f, 0.15f, 0.4f) : new Color(1, 1, 1, 0.09f));
            string label = it.dynamic != null ? it.dynamic() : it.label;
            if (it.slider) label += "   " + Mathf.RoundToInt(it.value * 100f) + "%";
            it.text.text = label;
            it.text.color = it.disabled ? new Color(0.6f, 0.6f, 0.6f) : (on ? Color.white : new Color(0.92f, 0.92f, 0.92f));
        }
    }

    // ------------------------------------------------------------------ input and loop
    void Update()
    {
        if (statusTimer > 0f) { statusTimer -= Time.unscaledDeltaTime; if (statusTimer <= 0f) statusText.text = ""; }

        if (!menuOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) && Time.timeScale > 0.5f && GameObject.Find("DarooCutscene") == null) OpenPause();
            return;
        }

        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        if (onTitle) { OrbitTitleCam(); HideOtherCanvases(); }
        HandleInput();
        RefreshVisuals();
    }

    void HandleInput()
    {
        Vector2 mp = Input.mousePosition;
        bool moved = (mp - lastMouse).sqrMagnitude > 1f; lastMouse = mp;
        if (moved)
            for (int i = 0; i < page.Count; i++)
                if (!page[i].disabled && page[i].rect != null && RectTransformUtility.RectangleContainsScreenPoint(page[i].rect, mp, null)) { sel = i; break; }

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);

        if (sel >= 0 && sel < page.Count)
        {
            Item cur = page[sel];
            if (cur.slider)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SetSlider(cur, cur.value - 0.05f);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SetSlider(cur, cur.value + 0.05f);
            }
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Activate(cur);
        }

        if (Input.GetMouseButtonDown(0))
            foreach (var it in page)
                if (!it.disabled && it.rect != null && RectTransformUtility.RectangleContainsScreenPoint(it.rect, mp, null))
                {
                    sel = page.IndexOf(it);
                    if (it.slider) { dragging = it; DragSlider(it); }
                    else Activate(it);
                    break;
                }
        if (dragging != null)
        {
            if (Input.GetMouseButton(0)) DragSlider(dragging); else dragging = null;
        }

        if (Input.GetKeyDown(KeyCode.Escape) && onBack != null) onBack();
    }

    void Move(int dir)
    {
        if (page.Count == 0) return;
        for (int n = 0; n < page.Count; n++)
        {
            sel = (sel + dir + page.Count) % page.Count;
            if (!page[sel].disabled) return;
        }
    }

    void Activate(Item it)
    {
        if (it.click == null) return;
        SoundManager.Play("ding", 0.35f, 1.4f);
        it.click();
    }

    void DragSlider(Item it)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(it.barRect, Input.mousePosition, null, out local))
        {
            Rect r = it.barRect.rect;
            SetSlider(it, (local.x - r.xMin) / Mathf.Max(1f, r.width));
        }
    }

    void SetSlider(Item it, float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(v, it.value)) return;
        it.value = v;
        it.fill.rectTransform.anchorMax = new Vector2(v, 1f);
        PlayerPrefs.SetFloat(it.key, v);
        if (it.change != null) it.change(v);
    }

    // ------------------------------------------------------------------ settings
    void ApplyAllSettings()
    {
        for (int i = 0; i < SettingKeys.Length; i++) Apply(SettingKeys[i], PlayerPrefs.GetFloat(SettingKeys[i], SettingDefaults[i]));
    }

    void Apply(string key, float v)
    {
        switch (key)
        {
            case "set_master": AudioListener.volume = v; break;
            case "set_music":
                GameObject m = GameObject.Find("Music");
                if (m != null) { var s = m.GetComponent<AudioSource>(); if (s != null) s.volume = v * 0.4f; }
                break;
            case "set_sfx": if (SoundManager.I != null) SoundManager.I.sfxVolume = v; break;
            case "set_sens": SetSensitivity(Mathf.Lerp(0.3f, 2f, v)); break;
        }
    }

    void SetSensitivity(float factor)
    {
        if (fpc == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p == null) return;
            foreach (var mb in p.GetComponents<MonoBehaviour>())
                if (mb.GetType().Name == "FirstPersonController")
                {
                    fpc = mb; rotField = mb.GetType().GetField("RotationSpeed");
                    if (rotField != null) baseRot = (float)rotField.GetValue(mb);
                    break;
                }
        }
        if (fpc != null && rotField != null && baseRot > 0f) rotField.SetValue(fpc, baseRot * factor);
    }

    // ------------------------------------------------------------------ game actions
    void Status(string msg) { statusText.text = msg; statusTimer = 2.5f; }

    void SaveGame()
    {
        var gm = GameManager.I;
        if (gm == null) return;
        MethodInfo save = typeof(GameManager).GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance);
        if (save != null) { save.Invoke(gm, null); PlayerPrefs.Save(); Status("Game saved"); }
        else Status("Could not save");
    }

    void DoNewGame()
    {
        foreach (string k in new[] { "score", "money", "day", "chappals", "karma", "isNeta" }) PlayerPrefs.DeleteKey(k);
        var gm = GameManager.I;
        if (gm != null)
        {
            gm.money = 0; gm.score = 0; gm.day = 1; gm.heat = 0; gm.energy = 100f; gm.ateLangarToday = false; gm.chappalValues.Clear();
        }
        MissionManager.AddKarma(-MissionManager.Karma);
        NetaRise.IsNeta = false; NetaRise.BribeUntil = 0f;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t != null && t.name.StartsWith("Chamcha_")) Destroy(t.gameObject);

        var mm = FindFirstObjectByType<MissionManager>();
        if (mm != null)
        {
            MethodInfo reset = typeof(MissionManager).GetMethod("ResetAll", BindingFlags.NonPublic | BindingFlags.Instance);
            if (reset != null) reset.Invoke(mm, null);
        }

        GameObject player = GameObject.FindWithTag("Player");
        GameObject start = GameObject.Find("PlayerStart");
        if (player != null && start != null)
        {
            var cc = player.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;
            player.transform.position = start.transform.position + Vector3.up * 2f;
            if (cc != null && was) cc.enabled = true;
        }
        DayNightCycle.Hour = 8f;
        inProgress = false;
        CloseMenu();
    }

    void CloseMenu()
    {
        menuOpen = false; onTitle = false; inProgress = true;
        root.SetActive(false);
        Unfreeze();
        foreach (var c in hidden) if (c != null) c.enabled = true;
        hidden.Clear();
        if (titleCam != null) { Destroy(titleCam.gameObject); titleCam = null; }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
    }

    // ------------------------------------------------------------------ freezing the player, hiding the HUD, title camera
    void Freeze()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p == null) return;
        foreach (var mb in p.GetComponentsInChildren<MonoBehaviour>())
        {
            string n = mb.GetType().Name;
            if (mb.enabled && (n == "FirstPersonController" || n == "ThirdPersonController" || n == "ChappalThrower" ||
                               n == "StarterAssetsInputs" || n == "PlayerInput" || n == "BasicRigidBodyPush"))
            { mb.enabled = false; frozen.Add(mb); }
        }
    }

    void Unfreeze()
    {
        foreach (var b in frozen) if (b != null) b.enabled = true;
        frozen.Clear();
    }

    void HideOtherCanvases()
    {
        hideTimer -= Time.unscaledDeltaTime;
        if (hideTimer > 0f) return;
        hideTimer = 0.25f;
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c != canvas && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; hidden.Add(c); }
    }

    void CreateTitleCam()
    {
        if (!chowkFound || titleCam != null) return;
        var go = new GameObject("TitleCamera");
        go.transform.SetParent(transform, false);
        titleCam = go.AddComponent<Camera>();
        titleCam.depth = 90f; titleCam.fieldOfView = 55f; titleCam.nearClipPlane = 0.3f; titleCam.farClipPlane = 2000f;
        titleCam.clearFlags = CameraClearFlags.Skybox;
        orbit = Random.Range(0f, 360f);
        OrbitTitleCam();
    }

    void OrbitTitleCam()
    {
        if (titleCam == null) return;
        orbit += 7f * Time.unscaledDeltaTime;
        Vector3 offset = Quaternion.Euler(0f, orbit, 0f) * new Vector3(0f, 0f, 42f);
        Vector3 pos = chowkPos + offset + Vector3.up * (20f + 3f * Mathf.Sin(Time.unscaledTime * 0.3f));
        titleCam.transform.position = pos;
        titleCam.transform.rotation = Quaternion.LookRotation(chowkPos + Vector3.up * 16f - pos);
    }
}