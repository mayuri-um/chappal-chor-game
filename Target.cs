using UnityEngine;

public class Target : MonoBehaviour
{
    public int points = 10;
    public string label = "Target";
    public bool isPoliceTarget;
    float cooldown;

    void OnCollisionEnter(Collision c)
    {
        if (Time.time < cooldown) return;
        if (!c.gameObject.CompareTag("Chappal")) return;
        cooldown = Time.time + 1f;

        Debug.Log("HIT " + name + " (" + label + ") -> " + points);
        GameEvents.OnScore?.Invoke(points, label);
        GameEvents.OnToast?.Invoke(points >= 0 ? label + " pe chappal! +" + points : "Arre arre arre! " + label + " " + points);
        if (isPoliceTarget && GameManager.I != null) GameManager.I.AddHeat(2);
        GameEvents.OnCrime?.Invoke(transform.position, "hit " + label);
    }
}