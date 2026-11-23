using TMPro;
using UnityEngine;

public class HUD : MonoBehaviour
{
    public TMP_Text stats, toast;
    TMP_Text alert;
    float toastTimer, alertTimer;

    void OnEnable() { GameEvents.OnToast += ShowToast; GameEvents.OnAlert += ShowAlert; }
    void OnDisable() { GameEvents.OnToast -= ShowToast; GameEvents.OnAlert -= ShowAlert; }

    void Start()
    {
        var go = new GameObject("AlertText", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.fontSize = 46; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = new Color(1f, 0.15f, 0.15f); t.raycastTarget = false; t.text = "";
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.72f);
        rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; rt.sizeDelta = new Vector2(1800, 160);
        alert = t;
    }

    void ShowToast(string m) { if (toast != null) { toast.text = m; toastTimer = 2.5f; } }
    void ShowAlert(string m) { if (alert != null) { alert.text = m; alertTimer = 4.5f; } }

    void Update()
    {
        var g = GameManager.I;
        if (g != null && stats != null)
        {
            string en = g.energy < 25f ? "<color=#ff5050>" + Mathf.RoundToInt(g.energy) + "%</color>" : Mathf.RoundToInt(g.energy) + "%";
            string heat = g.heat > 0 ? "<color=#ff5050>" + new string('*', Mathf.Min(g.heat, 10)) + "</color>" : "0";
            string langar = g.ateLangarToday ? "<color=#60ff60>DONE</color>" : "<color=#ffb040>NOT YET - go to the gurudwara</color>";
            stats.text = "Day " + g.day + "   Score " + g.score + "\nPaisa Rs " + g.money + " / " + g.goalMoney +
                         "\nChappals " + g.ChappalCount + "   Energy " + en + "   Heat " + heat + "\nLangar today: " + langar;
        }
        if (toastTimer > 0f) { toastTimer -= Time.deltaTime; if (toastTimer <= 0f && toast != null) toast.text = ""; }
        if (alert != null)
        {
            if (alertTimer > 0f)
            {
                alertTimer -= Time.deltaTime;
                alert.alpha = 0.65f + 0.35f * Mathf.Sin(Time.time * 12f);
                if (alertTimer <= 0f) alert.text = "";
            }
        }
    }
}