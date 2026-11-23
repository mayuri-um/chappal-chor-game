using UnityEngine;

public class SafeZone : MonoBehaviour
{
    public enum Type { Langar, Sleep, Dealer }
    public Type type;
    public float holdTime = 0f;   // 0 = use the default for each type
    float held;
    bool needRelease;

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var g = GameManager.I;
        if (g == null) return;

        bool blocked = type == Type.Sleep && !g.ateLangarToday;

        string label;
        float need;
        switch (type)
        {
            case Type.Langar:
                label = "Hold to eat langar (restore energy)";
                need = 2f;
                break;
            case Type.Sleep:
                label = g.ateLangarToday ? "Hold to sleep and save (next day)" : "Eat at the gurudwara first!";
                need = 3f;
                break;
            default:
                label = g.ChappalCount > 0 ? "Hold to sell " + g.ChappalCount + " chappals" : "No chappals to sell";
                need = 1.5f;
                break;
        }
        if (holdTime > 0f) need = holdTime;

        if (!Input.GetKey(KeyCode.F) || blocked)
        {
            held = 0f;
            if (!Input.GetKey(KeyCode.F)) needRelease = false;
        }
        else if (!needRelease)
        {
            held += Time.fixedDeltaTime;
        }

        InteractionPrompt.Show("F", label, held / need, blocked);
        if (blocked || held < need) return;

        if (type == Type.Langar) g.EatLangar();
        else if (type == Type.Sleep) g.SleepAndSave();
        else if (g.ChappalCount > 0) g.SellAll();
        else GameEvents.OnToast?.Invoke("You have no chappals to sell!");

        held = 0f;
        needRelease = true;   // release F before it can trigger again
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) { held = 0f; needRelease = false; }
    }
}