using UnityEngine;

public class ChappalPickup : MonoBehaviour
{
    public int value = 200;
    public float stealTime = 1.5f;
    float held;
    bool done;

    void OnTriggerStay(Collider other)
    {
        if (done || !other.CompareTag("Player")) return;

        bool watched = false;
        foreach (var w in Witness.All)
            if (w.CanSee(other.transform)) { watched = true; break; }

        held = Input.GetKey(KeyCode.E) ? held + Time.fixedDeltaTime : 0f;
        InteractionPrompt.Show("E", watched ? "Someone is watching!" : "Hold to steal chappals (Rs " + value + ")", held / stealTime, watched);
        if (held < stealTime) return;

        if (watched)
        {
            GameEvents.OnAlert?.Invoke("CAUGHT RED-HANDED! Someone saw you stealing!");
            GameManager.I.AddHeat(1);
            GameEvents.OnCrime?.Invoke(other.transform.position, "theft");
            held = 0f;
            return;
        }

        GameManager.I.AddChappal(value);
        GameEvents.OnToast?.Invoke("Chappal chori! Worth Rs " + value);
        GameEvents.OnCrime?.Invoke(other.transform.position, "theft");
        done = true;
        gameObject.SetActive(false);
    }

    void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) held = 0f; }
}