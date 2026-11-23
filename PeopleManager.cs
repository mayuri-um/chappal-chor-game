using System.Collections.Generic;
using UnityEngine;

public class PeopleManager : MonoBehaviour
{
    public static PeopleManager I;
    public static float HumanFacingOffset = 180f;   // if people walk backwards or sideways, change this (try 0, 90 or -90)

    public readonly List<Pedestrian> all = new List<Pedestrian>();
    public readonly List<Pedestrian> police = new List<Pedestrian>();
    Transform player;
    float lastPing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("PeopleManager").AddComponent<PeopleManager>(); }

    void Awake() { I = this; }
    void OnEnable() { GameEvents.OnCrime += HandleCrime; }
    void OnDisable() { GameEvents.OnCrime -= HandleCrime; }

    void Start()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name.StartsWith("NPC_")) Setup(t);
        Debug.Log("PeopleManager: " + all.Count + " people now move, " + police.Count + " are police.");
    }

    void Setup(Transform t)
    {
        string[] p = t.name.Split('_');
        if (p.Length < 2) return;
        string role = p[1];
        float speed, radius, snitch = 0f; bool animal = false, cop = false;
        switch (role)
        {
            case "Civilian": speed = 1.4f; radius = 45f; snitch = 0.4f; break;
            case "Woman": speed = 1.3f; radius = 45f; snitch = 0.35f; break;
            case "Kid": speed = 1.9f; radius = 25f; break;
            case "Police": speed = 1.6f; radius = 35f; cop = true; break;
            case "Poor": speed = 1.0f; radius = 10f; break;
            case "Animal":
                animal = true;
                string kind = p.Length > 2 ? p[2] : "dog";
                speed = kind == "cow" ? 0.5f : (kind == "goat" ? 0.8f : 1.1f); radius = 22f; break;
            default: return;   // pandits, guards, protesters, vendors, sewadars, dealer stay put
        }

        if (Mathf.Abs(Mathf.DeltaAngle(t.eulerAngles.x, 0f)) > 5f)
            Debug.LogWarning("PeopleManager: " + t.name + " is tilted (" + t.eulerAngles + "). Tell Claude, rotation may need a fix.");

        var mf = t.GetComponent<MeshFilter>();
        foreach (var c in t.GetComponents<Collider>()) Destroy(c);
        var box = t.gameObject.AddComponent<BoxCollider>();
        if (mf != null && mf.sharedMesh != null) { box.center = mf.sharedMesh.bounds.center; box.size = mf.sharedMesh.bounds.size; }
        var rb = t.GetComponent<Rigidbody>();
        if (rb == null) rb = t.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        var ped = t.gameObject.AddComponent<Pedestrian>();
        ped.role = role; ped.isAnimal = animal; ped.isPolice = cop;
        ped.walkSpeed = speed; ped.wanderRadius = radius;
        ped.facingOffset = animal ? 0f : HumanFacingOffset;
        ped.canSnitch = Random.value < snitch;
        all.Add(ped);
        if (cop) police.Add(ped);
    }

    void Update()
    {
        if (GameManager.I != null && GameManager.I.heat >= 3 && Time.time - lastPing > 12f && FindPlayer())
        {
            lastPing = Time.time;
            AlertNearestPolice(player.position, 2);
            GameEvents.OnToast?.Invoke("Police are looking for you!");
        }
    }

    bool FindPlayer()
    {
        if (player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
        return player != null;
    }

    void HandleCrime(Vector3 pos, string what)
    {
        if (!FindPlayer()) return;

        foreach (var cop in police)
            if (CanSee(cop.transform, player, 45f))
            {
                cop.Alert(player.position);
                GameEvents.OnAlert?.Invoke("A POLICEMAN SAW YOU!");
                if (GameManager.I != null) GameManager.I.AddHeat(1);
                break;
            }

        int snitchers = 0;
        foreach (var c in all)
        {
            if (!c.canSnitch || c.mode != Pedestrian.Mode.Wander) continue;
            if (Vector3.Distance(c.transform.position, pos) > 30f) continue;
            if (!CanSee(c.transform, player, 30f)) continue;
            if (Random.value > 0.75f) continue;
            c.BeginSnitch();
            if (++snitchers >= 2) break;
        }
    }

    public void Report(Pedestrian p)
    {
        GameEvents.OnAlert?.Invoke("YOU GOT SNITCHED ON!  A " + p.role.ToLower() + " reported you to the police!");
        GameEvents.OnSnitched?.Invoke(p.role);
        if (GameManager.I != null) GameManager.I.AddHeat(2);
        AlertNearestPolice(FindPlayer() ? player.position : p.transform.position, 2);
    }

    void AlertNearestPolice(Vector3 pos, int count)
    {
        police.Sort((a, b) => Vector3.Distance(a.transform.position, pos).CompareTo(Vector3.Distance(b.transform.position, pos)));
        for (int i = 0; i < police.Count && i < count; i++)
            if (Vector3.Distance(police[i].transform.position, pos) < 350f) police[i].Alert(pos);
    }

    public void Bust()
    {
        if (GameManager.I != null) GameManager.I.Busted();
        foreach (var c in police) c.StandDown();
    }

    public static bool CanSee(Transform from, Transform target, float maxDist)
    {
        Vector3 eye = from.position + Vector3.up * 1.6f;
        Vector3 tp = target.position + Vector3.up;
        Vector3 d = tp - eye;
        if (d.magnitude > maxDist) return false;
        if (Physics.Raycast(eye, d.normalized, out RaycastHit hit, d.magnitude, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == target || hit.transform.IsChildOf(target);
        return true;
    }
}