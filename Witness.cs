using System.Collections.Generic;
using UnityEngine;

public class Witness : MonoBehaviour
{
    public static List<Witness> All = new List<Witness>();
    public float range = 14f, angle = 80f;
    public bool frontIsMinusZ = true;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    public bool CanSee(Transform p)
    {
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = p.position + Vector3.up;
        Vector3 d = target - eye;
        if (d.magnitude > range) return false;
        Vector3 front = frontIsMinusZ ? -transform.forward : transform.forward;
        if (Vector3.Angle(front, d) > angle) return false;
        if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == p || hit.transform.IsChildOf(p);
        return true;
    }
}
