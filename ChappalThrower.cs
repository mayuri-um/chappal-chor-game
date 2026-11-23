using UnityEngine;

public class ChappalThrower : MonoBehaviour
{
    public Rigidbody chappalPrefab;
    public float force = 18f;
    public Transform cam;

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (GameManager.I == null || !GameManager.I.UseChappal())
        {
            GameEvents.OnToast?.Invoke("No chappals left! Steal some first.");
            return;
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;

        Rigidbody c = Instantiate(chappalPrefab, cam.position + cam.forward * 0.8f, Random.rotation);
        c.gameObject.tag = "Chappal";
        c.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        c.interpolation = RigidbodyInterpolation.Interpolate;

        foreach (var mine in GetComponentsInChildren<Collider>())
            foreach (var theirs in c.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(mine, theirs);

        c.AddForce(cam.forward * force, ForceMode.Impulse);
        c.AddTorque(Random.insideUnitSphere * 5f, ForceMode.Impulse);
        Destroy(c.gameObject, 8f);
    }
}