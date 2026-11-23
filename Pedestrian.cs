using TMPro;
using UnityEngine;

public class Pedestrian : MonoBehaviour
{
    public enum Mode { Idle, Wander, Snitching, Chase, Return }

    public string role = "Civilian";
    public bool isAnimal, isPolice, canSnitch;
    public float walkSpeed = 1.4f, runSpeed = 5.5f, wanderRadius = 40f;
    public float facingOffset = 180f;
    public Mode mode = Mode.Wander;

    Vector3 home, goal, chaseTarget;
    float pause, snitchTimer, chaseTimer, groundTimer, groundY;
    bool blocked;
    Transform player;
    TextMeshPro bubble;
    static readonly RaycastHit[] hits = new RaycastHit[16];

    void Start()
    {
        home = transform.position; groundY = home.y;
        PickGoal();
        pause = Random.Range(0f, 3f);
        var go = new GameObject("SnitchBubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0, 2.4f, 0);
        bubble = go.AddComponent<TextMeshPro>();
        bubble.text = "!"; bubble.fontSize = 12; bubble.fontStyle = FontStyles.Bold;
        bubble.color = new Color(1f, 0.1f, 0.1f); bubble.alignment = TextAlignmentOptions.Center;
        go.SetActive(false);
    }

    void Update()
    {
        if (mode == Mode.Idle) return;
        if (player == null)
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
        }
        float dt = Time.deltaTime;
        switch (mode)
        {
            case Mode.Wander: WanderStep(dt); break;
            case Mode.Snitching: SnitchStep(dt); break;
            case Mode.Chase: ChaseStep(dt); break;
            case Mode.Return: if (MoveToward(home, walkSpeed, dt)) mode = Mode.Wander; break;
        }
        GroundSnap(dt);
        if (bubble != null && bubble.gameObject.activeSelf && Camera.main != null)
            bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - Camera.main.transform.position);
    }

    // ---------------------------------------------------------------- behaviours
    void WanderStep(float dt)
    {
        if (pause > 0f) { pause -= dt; return; }
        bool arrived = MoveToward(goal, walkSpeed, dt);
        if (arrived || blocked) { pause = Random.Range(0.5f, 4f); PickGoal(); }
    }

    void SnitchStep(float dt)
    {
        if (player != null)
        {
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) Face(d.normalized, dt);
        }
        snitchTimer -= dt;
        if (snitchTimer > 0f) return;
        bubble.gameObject.SetActive(false);
        mode = Mode.Wander;
        if (PeopleManager.I != null) PeopleManager.I.Report(this);
    }

    void ChaseStep(float dt)
    {
        bool sees = player != null && PeopleManager.CanSee(transform, player, 35f);
        if (sees) { chaseTarget = player.position; chaseTimer = 25f; }
        chaseTimer -= dt;

        if (sees && Vector3.Distance(transform.position, player.position) < 2.4f)
        {
            if (PeopleManager.I != null) PeopleManager.I.Bust();
            return;
        }
        bool arrived = MoveToward(chaseTarget, runSpeed, dt);
        if (blocked) chaseTimer -= dt * 2f;
        if (chaseTimer <= 0f || (arrived && !sees)) mode = Mode.Return;
    }

    // ---------------------------------------------------------------- called from outside
    public void BeginSnitch()
    {
        mode = Mode.Snitching; snitchTimer = 2.5f;
        if (bubble != null) bubble.gameObject.SetActive(true);
    }

    public void Alert(Vector3 pos)
    {
        if (!isPolice) return;
        chaseTarget = pos; chaseTimer = 25f; mode = Mode.Chase;
    }

    public void StandDown() { if (isPolice) mode = Mode.Return; }

    // ---------------------------------------------------------------- movement helpers
    void PickGoal()
    {
        Vector2 r = Random.insideUnitCircle * wanderRadius;
        goal = home + new Vector3(r.x, 0f, r.y);
    }

    bool MoveToward(Vector3 target, float speed, float dt)
    {
        blocked = false;
        Vector3 dir = target - transform.position; dir.y = 0f;
        float d = dir.magnitude;
        if (d < 0.3f) return true;
        dir /= d;
        if (Blocked(dir)) { blocked = true; return false; }
        transform.position += dir * Mathf.Min(speed * dt, d);
        Face(dir, dt);
        return false;
    }

    bool Blocked(Vector3 dir)
    {
        Vector3 o = transform.position + Vector3.up * 0.9f;
        int n = Physics.RaycastNonAlloc(o, dir, hits, 1.0f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            if (c.transform.IsChildOf(transform)) continue;
            if (c.GetComponentInParent<Pedestrian>() != null) continue;
            if (c.CompareTag("Chappal")) continue;
            return true;
        }
        return false;
    }

    void Face(Vector3 dir, float dt)
    {
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + facingOffset;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, yaw, 0f), 10f * dt);
    }

    void GroundSnap(float dt)
    {
        groundTimer -= dt;
        if (groundTimer <= 0f)
        {
            groundTimer = 0.12f + Random.value * 0.05f;
            Vector3 o = transform.position + Vector3.up * 1.2f;
            int n = Physics.RaycastNonAlloc(o, Vector3.down, hits, 4f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue; bool found = false;
            for (int i = 0; i < n; i++)
            {
                Collider c = hits[i].collider;
                if (c.transform.IsChildOf(transform) || c.GetComponentInParent<Pedestrian>() != null) continue;
                if (c.CompareTag("Chappal") || c.CompareTag("Player")) continue;
                if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
            }
            if (found) groundY = best;
        }
        Vector3 p = transform.position;
        p.y = Mathf.Lerp(p.y, groundY, 12f * dt);
        transform.position = p;
    }
}