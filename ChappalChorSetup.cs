using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Put this file in Assets/Editor/ChappalChorSetup.cs.
// Then use the menu:  Chappal Chor > Setup Everything (run once)
public static class ChappalChorSetup
{
    [System.Serializable] class ChappalEntry { public string name; public string tier; public int value_rs; }
    [System.Serializable] class LayoutData { public List<ChappalEntry> chappals; }

    const string JsonPath = "Assets/Data/ChappalChor_layout_v3.json";

    [MenuItem("Chappal Chor/Setup Everything (run once)")]
    public static void SetupEverything()
    {
        EnsureTag("Chappal");

        GameObject mapRoot = FindMapRoot();
        if (mapRoot == null)
        {
            Debug.LogError("Chappal Chor: could not find the map in the scene. Drag ChappalChor_Map_v3.fbx into the Hierarchy first.");
            return;
        }

        Dictionary<string, int> values = LoadChappalValues();
        int zones = 0, safe = 0, targets = 0, witnesses = 0, fixedColliders = 0;

        foreach (Transform t in mapRoot.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;

            if (n.StartsWith("StealZone_"))
            {
                AddTrigger(t);
                var pick = GetOrAdd<ChappalPickup>(t.gameObject);
                pick.value = RackValue(values, n.Substring("StealZone_".Length));
                zones++;
            }
            else if (n == "SafeZone_Langar") { AddTrigger(t); GetOrAdd<SafeZone>(t.gameObject).type = SafeZone.Type.Langar; safe++; }
            else if (n == "SafeZone_Sleep") { AddTrigger(t); GetOrAdd<SafeZone>(t.gameObject).type = SafeZone.Type.Sleep; safe++; }
            else if (n == "DealerZone") { AddTrigger(t); GetOrAdd<SafeZone>(t.gameObject).type = SafeZone.Type.Dealer; safe++; }
            else if (n.StartsWith("ProtestZone") || n.StartsWith("BastiDrop") || n.StartsWith("PanditStash") ||
                     n.StartsWith("VendorMission_") || n.StartsWith("Goal_"))
            {
                AddTrigger(t);
            }
            else if (n.StartsWith("NPC_"))
            {
                string[] parts = n.Split('_');
                string role = parts.Length > 1 ? parts[1] : "";
                int pts; bool police = false, witness = false;
                switch (role)
                {
                    case "Police": pts = 30; police = true; witness = true; break;
                    case "Pandit": pts = 15; witness = true; break;
                    case "Guard": pts = 20; witness = true; break;
                    case "Civilian": pts = 10; break;
                    case "Kid":
                    case "Woman":
                    case "Animal": pts = -100; break;
                    default: pts = 0; break;   // protesters, vendors, sewadars, poor, dealer: no score
                }
                if (pts != 0)
                {
                    fixedColliders += EnsureCollider(t);
                    var tg = GetOrAdd<Target>(t.gameObject);
                    tg.points = pts; tg.label = role; tg.isPoliceTarget = police;
                    targets++;
                }
                if (witness) { GetOrAdd<Witness>(t.gameObject); witnesses++; }
            }
            else if (n.StartsWith("Target_Poster_") || n.StartsWith("Target_Hoarding_"))
            {
                fixedColliders += EnsureCollider(t);
                var tg = GetOrAdd<Target>(t.gameObject);
                tg.points = n.StartsWith("Target_Hoarding_") ? 100 : 50;
                tg.label = n.StartsWith("Target_Hoarding_") ? "Hoarding" : "Poster";
                targets++;
            }
        }

        // Player: tag, move to masjid start, hook up thrower
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null) player = GameObject.Find("PlayerCapsule");
        if (player != null)
        {
            player.tag = "Player";
            Transform start = FindByName(mapRoot.transform, "PlayerStart");
            if (start != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = start.position + Vector3.up * 2f;
                if (cc != null) cc.enabled = true;
            }
            SetupThrower(player);
        }
        else Debug.LogWarning("Chappal Chor: no player found. Add PlayerCapsule to the scene, then run this again.");

        if (Object.FindFirstObjectByType<GameManager>() == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        CreateHud();

        EditorSceneManager.MarkSceneDirty(mapRoot.scene);
        Debug.Log("Chappal Chor setup done: " + zones + " steal zones, " + safe + " safe/dealer zones, " + targets +
                  " score targets, " + witnesses + " witnesses, " + fixedColliders + " colliders added. Save the scene with Ctrl+S.");
    }

    [MenuItem("Chappal Chor/Give 5 Test Chappals (while playing)")]
    public static void GiveTestChappals()
    {
        if (!Application.isPlaying || GameManager.I == null) { Debug.LogWarning("Press Play first."); return; }
        for (int i = 0; i < 5; i++) GameManager.I.AddChappal(500);
        Debug.Log("Added 5 test chappals.");
    }

    // ------------------------------------------------------------------ helpers
    static GameObject FindMapRoot()
    {
        foreach (GameObject go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name.StartsWith("ChappalChor_Map")) return go;
        return null;
    }

    static Transform FindByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static void AddTrigger(Transform t)
    {
        var b = GetOrAdd<BoxCollider>(t.gameObject);
        b.isTrigger = true; b.center = Vector3.zero; b.size = Vector3.one * 2f;  // marker scale is already half the zone size
    }

    static int EnsureCollider(Transform t)
    {
        if (t.GetComponent<Collider>() != null) return 0;
        if (t.GetComponent<MeshFilter>() == null) return 0;
        t.gameObject.AddComponent<MeshCollider>();
        return 1;
    }

    static Dictionary<string, int> LoadChappalValues()
    {
        var d = new Dictionary<string, int>();
        if (!File.Exists(JsonPath))
        {
            Debug.LogWarning("Chappal Chor: " + JsonPath + " not found; steal zones will use the default value.");
            return d;
        }
        LayoutData data = JsonUtility.FromJson<LayoutData>(File.ReadAllText(JsonPath));
        if (data != null && data.chappals != null)
            foreach (var c in data.chappals) d[c.name] = c.value_rs;
        return d;
    }

    static int RackValue(Dictionary<string, int> values, string rackKey)
    {
        string prefix = "Chappal_" + rackKey + "_";
        int best = 0;
        foreach (var kv in values)
            if (kv.Key.StartsWith(prefix) && kv.Value > best) best = kv.Value;
        return best > 0 ? best : 200;   // most expensive pair on that rack
    }

    static void EnsureTag(string tag)
    {
        foreach (string t in UnityEditorInternal.InternalEditorUtility.tags)
            if (t == tag) return;
        UnityEditorInternal.InternalEditorUtility.AddTag(tag);
    }

    static void SetupThrower(GameObject player)
    {
        string path = "Assets/Prefabs/Chappal.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Directory.CreateDirectory("Assets/Prefabs");
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Chappal"; go.transform.localScale = new Vector3(0.3f, 0.05f, 0.12f);
            go.tag = "Chappal";
            go.AddComponent<Rigidbody>();
            prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        var thrower = GetOrAdd<ChappalThrower>(player);
        thrower.chappalPrefab = prefab.GetComponent<Rigidbody>();
        Camera cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        if (cam != null) thrower.cam = cam.transform;
        EditorUtility.SetDirty(thrower);
    }

    static void CreateHud()
    {
        if (Object.FindFirstObjectByType<HUD>() != null) return;
        if (TMP_Settings.instance == null)
        {
            Debug.LogWarning("Chappal Chor: TextMeshPro is not set up. Use Window > TextMeshPro > Import TMP Essential Resources, then run Setup Everything again.");
            return;
        }
        var canvasGo = new GameObject("HUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        TextMeshProUGUI stats = MakeText(canvasGo.transform, "StatsText", new Vector2(0, 1), new Vector2(24, -24), TextAlignmentOptions.TopLeft, 30);
        TextMeshProUGUI toast = MakeText(canvasGo.transform, "ToastText", new Vector2(0.5f, 1), new Vector2(0, -110), TextAlignmentOptions.Top, 40);
        var hud = canvasGo.AddComponent<HUD>();
        hud.stats = stats; hud.toast = toast;
    }

    static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, TextAlignmentOptions align, int size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size; tmp.alignment = align; tmp.color = Color.white; tmp.text = "";
        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = anchor; rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(800, 220);
        return tmp;
    }
}