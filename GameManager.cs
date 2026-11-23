using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    public int score, money, day = 1, heat;
    public float energy = 100f;
    public int goalMoney = 50000;

    [Header("Energy (fast drain)")]
    public float energyDrainPerSec = 0.1f;
    public float sprintExtraDrain = 0.1f;
    public float exhaustedSpeedFactor = 0.4f;

    [Header("Daily rule")]
    public bool ateLangarToday;

    public List<int> chappalValues = new List<int>();

    public int ChappalCount => chappalValues.Count;
    public bool IsExhausted => energy <= 0f;

    float lastCrimeTime = -999f, lastHeatDecay, nextFind;
    bool lowWarned, goalAnnounced, exhaustedApplied;
    MonoBehaviour controller;
    FieldInfo fMove, fSprint;
    float moveBase, sprintBase;

    void Awake() { I = this; Load(); }
    void OnEnable() { GameEvents.OnScore += HandleScore; }
    void OnDisable() { GameEvents.OnScore -= HandleScore; }
    void HandleScore(int pts, string who) { score += pts; }

    void Update()
    {
        bool moving = Mathf.Abs(Input.GetAxisRaw("Horizontal")) + Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0.1f;
        bool sprinting = Input.GetKey(KeyCode.LeftShift) && moving;
        energy = Mathf.Max(0f, energy - (energyDrainPerSec + (sprinting ? sprintExtraDrain : 0f)) * Time.deltaTime);

        if (energy < 25f && !lowWarned) { lowWarned = true; GameEvents.OnToast?.Invoke("Energy low! Eat langar at the gurudwara."); }
        if (energy >= 25f) lowWarned = false;

        UpdateExhaustion();

        if (heat > 0 && Time.time - lastCrimeTime > 30f && Time.time - lastHeatDecay > 20f)
        {
            heat--; lastHeatDecay = Time.time;
        }
        if (!goalAnnounced && money >= goalMoney)
        {
            goalAnnounced = true;
            GameEvents.OnToast?.Invoke("Paisa poora! Go to the wine shop and party with your wife!");
        }
    }

    void UpdateExhaustion()
    {
        if (controller == null && Time.time > nextFind)
        {
            nextFind = Time.time + 1f;
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
                foreach (var mb in p.GetComponents<MonoBehaviour>())
                {
                    string n = mb.GetType().Name;
                    if (n == "FirstPersonController" || n == "ThirdPersonController")
                    {
                        controller = mb;
                        fMove = mb.GetType().GetField("MoveSpeed");
                        fSprint = mb.GetType().GetField("SprintSpeed");
                        if (fMove != null) moveBase = (float)fMove.GetValue(mb);
                        if (fSprint != null) sprintBase = (float)fSprint.GetValue(mb);
                        break;
                    }
                }
        }
        if (controller == null || exhaustedApplied == IsExhausted) return;
        exhaustedApplied = IsExhausted;
        float f = IsExhausted ? exhaustedSpeedFactor : 1f;
        if (fMove != null) fMove.SetValue(controller, moveBase * f);
        if (fSprint != null) fSprint.SetValue(controller, sprintBase * f);
        if (IsExhausted) GameEvents.OnAlert?.Invoke("YOU ARE EXHAUSTED! Get to the gurudwara!");
    }

    public void AddChappal(int value) { chappalValues.Add(value); }

    public bool UseChappal()
    {
        if (chappalValues.Count == 0) return false;
        chappalValues.RemoveAt(0);
        return true;
    }

    public void AddHeat(int h) { heat = Mathf.Min(10, heat + h); lastCrimeTime = Time.time; }

    public void SellAll()
    {
        int total = 0;
        foreach (int v in chappalValues) total += v;
        chappalValues.Clear();
        money += total;
        GameEvents.OnToast?.Invoke("Becha! +Rs " + total);
    }

    public void EatLangar()
    {
        energy = 100f;
        ateLangarToday = true;
        GameEvents.OnToast?.Invoke("Langar khaya. Energy full! Today's langar is done.");
    }

    public void SleepAndSave()
    {
        if (!ateLangarToday)
        {
            GameEvents.OnAlert?.Invoke("You must eat at the gurudwara before you can sleep!");
            return;
        }
        day++; energy = 100f; heat = 0; ateLangarToday = false;
        Save();
        GameEvents.OnToast?.Invoke("Subah ho gayi. Day " + day + " (saved)");
    }

    public void Busted()
    {
        int lost = chappalValues.Count;
        int fine = Mathf.Min(money, 500);
        chappalValues.Clear(); money -= fine; heat = 0;
        GameObject player = GameObject.FindWithTag("Player");
        GameObject start = GameObject.Find("PlayerStart");
        if (player != null && start != null)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.position = start.transform.position + Vector3.up * 2f;
            if (cc != null) cc.enabled = true;
        }
        GameEvents.OnAlert?.Invoke("BUSTED! Police took " + lost + " chappals and fined you Rs " + fine);
    }

    void Save()
    {
        PlayerPrefs.SetInt("score", score); PlayerPrefs.SetInt("money", money); PlayerPrefs.SetInt("day", day);
        PlayerPrefs.SetString("chappals", string.Join(",", chappalValues));
        PlayerPrefs.Save();
    }

    void Load()
    {
        score = PlayerPrefs.GetInt("score", 0); money = PlayerPrefs.GetInt("money", 0); day = PlayerPrefs.GetInt("day", 1);
        chappalValues.Clear();
        string s = PlayerPrefs.GetString("chappals", "");
        if (s.Length > 0) foreach (string p in s.Split(',')) chappalValues.Add(int.Parse(p));
    }
}