// CI harness for the Managed NPC code (Managed/Health and Managed/NPC). It compiles those files with plain .NET
// against the hand written UnityEngine stand-ins in UnityStubs.cs and runs the checks below. The stub raycast is a
// callback, so these checks cover the logic around the ray (cone, budget, lingering, dead and self sources,
// rotation, events), not real colliders or layers. Unity itself is never involved.
// Usage: dotnet run -- [native|fallback]   (the argument states which decision path the run must have used)
using System;
using System.Collections.Generic;
using System.Reflection;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;
using UnityEngine.AI;

internal static class Tests
{
    static int failures, checks;
    static bool sawNative, sawFallback;

    // Records which decision path a director batch used, so the end of the run can state it.
    static void Note(ManagedNpcDirector d)
    {
        if (d == null || d.LastBatchCount == 0) return;
        if (d.LastUsedNative) sawNative = true; else sawFallback = true;
    }
    static void Check(bool ok, string what) { checks++; if (!ok) { failures++; Console.WriteLine("FAIL: " + what); } }

    static void Set(object o, string field, object v)
    {
        FieldInfo f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        if (f == null) throw new Exception("no field " + field);
        f.SetValue(o, v);
    }
    static void Update(ManagedNpcDirector d) { typeof(ManagedNpcDirector).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, null); }
    static void ResetWorld()
    {
        UnityEngine.Object.All.Clear();
        typeof(ManagedNpcDirector).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        typeof(ManagedNpcThreatSource).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Time.time = 0; Time.deltaTime = 0.02f; Debug.Logs.Clear(); Debug.Warnings.Clear(); Physics.RayCount = 0; Physics.OnRaycast = null;
        UnityEngine.Object.Destroyed.Clear(); NavMesh.IsWalkable = null; UnityEngine.Random.Seed(7);
        ManagedSpellDamage.Enabled = true; ManagedSpellDamage.HurtCaster = false; ManagedSpellCaster.ClearForTest();
    }
    static ManagedNpcDirector Director() { return UnityEngine.Object.FindFirstObjectByType<ManagedNpcDirector>(FindObjectsInactive.Include); }

    static GameObject Player(float z, bool withHealth = false, GameObject[] outCol = null)
    {
        GameObject g = new GameObject("player"); g.transform.position = new Vector3(0, 0, z);
        if (withHealth) g.AddComponent<ManagedHealth>();
        g.AddComponent<ManagedNpcThreatSource>();
        g.AddComponent<Collider>();
        return g;
    }
    static ManagedNpcBrain Npc(string name, NpcRole role, DecisionBackend backend, bool canMove, Vector3 pos, bool withHealth = false)
    {
        GameObject g = new GameObject(name); g.transform.position = pos;
        if (withHealth) g.AddComponent<ManagedHealth>();
        ManagedNpcBrain b = g.AddComponent<ManagedNpcBrain>();
        Set(b, "_role", role); Set(b, "_backend", backend); Set(b, "_canMove", canMove);
        return b;
    }
    // The ray hits the player's collider first, as it would in a clear room.
    static void Clear(GameObject player) { Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) => { h = new RaycastHit { collider = player.GetComponent<Collider>() }; return true; }; }
    static void Wall() { GameObject w = new GameObject("wall"); w.AddComponent<Collider>(); Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) => { h = new RaycastHit { collider = w.GetComponent<Collider>() }; return true; }; }
    static void Tick(float dt) { Time.time += dt; Update(Director()); Note(Director()); }

    static void MathTests()
    {
        Vector3 eye = Vector3.zero, fwd = new Vector3(0, 0, 1);
        float c90 = ManagedNpcSight.CosHalfFieldOfView(90f);
        Check(ManagedNpcSight.InView(eye, fwd, new Vector3(0, 0, 5), 10, c90), "straight ahead in view");
        Check(ManagedNpcSight.InView(eye, fwd, new Vector3(2, 0, 3), 10, c90), "33 degrees off, in 90 degree cone");
        Check(!ManagedNpcSight.InView(eye, fwd, new Vector3(3, 0, 2), 10, c90), "56 degrees off, out of 90 degree cone");
        Check(!ManagedNpcSight.InView(eye, fwd, new Vector3(0, 0, -5), 10, c90), "behind is out");
        Check(!ManagedNpcSight.InView(eye, fwd, new Vector3(0, 0, 11), 10, c90), "beyond range is out");
        Check(ManagedNpcSight.InView(eye, fwd, new Vector3(0, 0, 10), 10, c90), "exactly at range is in");
        Check(ManagedNpcSight.InView(eye, fwd, eye, 10, c90), "point at the eye is in");
        Check(ManagedNpcSight.InView(eye, fwd, new Vector3(0, 0, -5), 10, ManagedNpcSight.CosHalfFieldOfView(360f)), "360 degree view sees behind");
        Check(ManagedNpcSight.InView(eye, fwd, new Vector3(5, 0, 0.01f), 10, ManagedNpcSight.CosHalfFieldOfView(180f)) && !ManagedNpcSight.InView(eye, fwd, new Vector3(5, 0, -0.01f), 10, ManagedNpcSight.CosHalfFieldOfView(180f)), "180 degree view splits at the side");
    }

    static void Basic()
    {
        ResetWorld();
        GameObject player = Player(10f);
        ManagedNpcBrain goblin = Npc("goblin", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        ManagedNpcBrain merchant = Npc("merchant", NpcRole.Merchant, DecisionBackend.StateMachine, false, Vector3.zero);
        ManagedNpcDirector d = Director();
        Check(d != null, "a brain creates a director when the scene has none");
        Check(goblin.Id != 0 && merchant.Id != 0 && goblin.Id != merchant.Id, "ids are non-zero and unique");
        Check(ManagedNpcDirector.RegisteredCount == 2, "two brains registered");

        Clear(player);
        List<string> changes = new List<string>();
        goblin.ActionChanged += (b, o, n) => changes.Add(o + ">" + n);
        Tick(0.1f);
        Check(goblin.ThreatVisible && merchant.ThreatVisible, "clear line of sight sees the player");
        Check(goblin.Action == NpcAction.Attack, "goblin attacks a visible threat, got " + goblin.Action);
        Check(merchant.Action == NpcAction.Idle, "merchant stays idle under threat, got " + merchant.Action);
        Check(changes.Count == 1 && changes[0] == "Idle>Attack", "one ActionChanged for the goblin: " + string.Join(",", changes));
        Tick(0.1f);
        Check(changes.Count == 1, "no event while the action is unchanged");

        Wall();
        Tick(0.1f);
        Check(goblin.ThreatVisible && goblin.Action == NpcAction.Attack, "sight lingers for the lose sight delay");
        Tick(0.6f);
        Check(!goblin.ThreatVisible, "sight is lost after the delay");
        Check(goblin.Action == NpcAction.Patrol, "goblin patrols with no threat, got " + goblin.Action);
        Check(merchant.Action == NpcAction.Trade, "merchant trades with no threat, got " + merchant.Action);
    }

    static void CheapRejects()
    {
        ResetWorld();
        GameObject far = Player(50f); Clear(far);
        ManagedNpcBrain a = Npc("a", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Tick(0.1f);
        Check(Physics.RayCount == 0 && !a.ThreatVisible, "out of range costs no ray");

        ResetWorld();
        GameObject behind = Player(-5f); Clear(behind);
        ManagedNpcBrain b = Npc("b", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Tick(0.1f);
        Check(Physics.RayCount == 0 && !b.ThreatVisible, "behind the NPC costs no ray");
    }

    static void Budget()
    {
        ResetWorld();
        GameObject player = Player(10f);
        // One visible point so one NPC costs one ray.
        Set(player.GetComponent<ManagedNpcThreatSource>(), "_visiblePoints", new[] { new Vector3(0, 1, 0) });
        Clear(player);
        ManagedNpcBrain[] g = new ManagedNpcBrain[3];
        for (int i = 0; i < 3; i++) g[i] = Npc("g" + i, NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Set(Director(), "_maxSightRaysPerTick", 1);
        Tick(0.1f);
        Check(Physics.RayCount == 1, "budget of one allows one ray, got " + Physics.RayCount);
        int seenFirst = 0; foreach (ManagedNpcBrain b in g) if (b.ThreatVisible) seenFirst++;
        Check(seenFirst == 1, "only one NPC saw on the first tick, got " + seenFirst);
        Tick(0.1f); Tick(0.1f);
        int seen = 0; foreach (ManagedNpcBrain b in g) if (b.ThreatVisible) seen++;
        Check(seen == 3, "every NPC is served within three ticks, got " + seen + " (starvation if lower)");
        Check(Director().LastSightRays == 1, "LastSightRays reports the spend");
    }

    static void DeadAndSelf()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcBrain alive = Npc("alive", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero, true);
        ManagedNpcBrain dying = Npc("dying", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero, true);
        Tick(0.1f);
        Check(alive.Action == NpcAction.Attack && dying.Action == NpcAction.Attack, "both attack while alive");
        Check(Director().LastBatchCount == 2, "both in the batch");
        dying.GetComponent<ManagedHealth>().TakeDamage(1000f);
        Tick(0.1f);
        Check(dying.Action == NpcAction.Idle, "a dead NPC goes idle");
        Check(Director().LastBatchCount == 1, "a dead NPC is left out of the batch");

        // Health fraction reaches the decision: a badly hurt, mobile enemy retreats.
        alive.GetComponent<ManagedHealth>().TakeDamage(90f);
        Check(System.Math.Abs(alive.HealthFraction - 0.1f) < 0.001f, "health fraction is 0.1");
        Tick(0.1f);
        Check(alive.Action == NpcAction.Retreat, "a badly hurt enemy retreats, got " + alive.Action);

        // A dead player is not a threat.
        player.GetComponent<ManagedHealth>().TakeDamage(1000f);
        ManagedNpcBrain fresh = Npc("fresh", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Time.time += 1f; Tick(0.1f);
        Check(!fresh.ThreatVisible, "a dead threat source is not seen");

        // A threat source on the NPC itself is not a threat to itself.
        ResetWorld();
        ManagedNpcBrain self = Npc("self", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        self.gameObject.AddComponent<ManagedNpcThreatSource>(); self.gameObject.AddComponent<Collider>();
        Clear(self.gameObject);
        Tick(0.1f);
        Check(!self.ThreatVisible && Physics.RayCount == 0, "an NPC does not see its own threat source");
    }

    static void DirectorRules()
    {
        ResetWorld();
        Npc("lonely", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Tick(0.1f); Tick(0.2f); Tick(0.2f);
        Check(Debug.Warnings.FindAll(w => w.Contains("ManagedNpcThreatSource")).Count == 1, "missing threat source warns exactly once");

        ResetWorld();
        GameObject host = new GameObject("scene director"); host.AddComponent<ManagedNpcDirector>();
        Npc("n", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        int directors = UnityEngine.Object.All.FindAll(c => c is ManagedNpcDirector).Count;
        Check(directors == 1, "a scene director is reused, found " + directors);
        GameObject extra = new GameObject("extra"); ManagedNpcDirector second = extra.AddComponent<ManagedNpcDirector>();
        Check(!second.enabled, "a second director disables itself");
        Check(Debug.Warnings.Exists(w => w.Contains("More than one")), "a second director warns");

        ResetWorld();
        GameObject off = new GameObject("disabled director"); ManagedNpcDirector dd = off.AddComponent<ManagedNpcDirector>(); dd.enabled = false;
        Npc("n2", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Check(UnityEngine.Object.All.FindAll(c => c is ManagedNpcDirector).Count == 1, "a disabled scene director is respected, none is added");

        ResetWorld();
        GameObject p = Player(10f); Clear(p);
        for (int i = 0; i < 600; i++) Npc("m" + i, NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Set(Director(), "_maxSightRaysPerTick", 100000); // isolates batch rotation from the ray budget
        Tick(0.1f);
        Check(Director().LastBatchCount == 256, "a tick is capped at the batch size, got " + Director().LastBatchCount);
        int decided = 0; Tick(0.1f); Tick(0.1f);
        foreach (UnityEngine.Component c in UnityEngine.Object.All) { ManagedNpcBrain b = c as ManagedNpcBrain; if (b != null && b.Action == NpcAction.Attack) decided++; }
        Check(decided == 600, "three ticks serve all 600 NPCs in turns, got " + decided);
    }

    static void HealthRules()
    {
        GameObject g = new GameObject("h"); ManagedHealth h = g.AddComponent<ManagedHealth>();
        int damaged = 0, died = 0; float removed = 0;
        h.Damaged += (x, a, s) => { damaged++; removed += a; }; h.Died += (x, s) => died++;
        Check(h.IsAlive && h.Fraction == 1f && h.Current == 100f, "starts full");
        h.TakeDamage(-5f); h.TakeDamage(float.NaN); h.TakeDamage(0f);
        Check(damaged == 0 && h.Current == 100f, "non-positive and NaN damage are ignored");
        h.TakeDamage(30f);
        Check(h.Current == 70f && System.Math.Abs(h.Fraction - 0.7f) < 1e-6f && damaged == 1 && removed == 30f, "damage applies");
        h.Heal(1000f); Check(h.Current == 100f, "heal is capped at max");
        h.Invulnerable = true; h.TakeDamage(50f); Check(h.Current == 100f && damaged == 1, "invulnerable ignores damage");
        h.Invulnerable = false; h.TakeDamage(500f);
        Check(!h.IsAlive && h.Current == 0f && died == 1 && removed == 130f, "overkill removes only what was left and kills once");
        h.TakeDamage(10f); h.Heal(10f);
        Check(damaged == 2 && died == 1 && !h.IsAlive, "a dead target ignores damage and heals");
        h.ResetToFull(); Check(h.IsAlive && h.Fraction == 1f, "ResetToFull revives");
        IManagedDamageable i = h; i.TakeDamage(20f, g); Check(h.Current == 80f, "works through the interface");
    }


    // ---------- actor and spell damage ----------

    sealed class Fighter
    {
        public GameObject Go; public ManagedNpcBrain Brain; public ManagedNpcActor Actor; public NavMeshAgent Agent; public ManagedHealth Health;
        public int Hits; public List<float> HitTimes = new List<float>();
    }

    static Fighter Fight(string name, Vector3 pos, NpcRole role, DecisionBackend backend, bool canMove, Transform[] waypoints)
    {
        Fighter f = new Fighter();
        f.Go = new GameObject(name); f.Go.transform.position = pos;
        f.Health = f.Go.AddComponent<ManagedHealth>();
        f.Agent = f.Go.AddComponent<NavMeshAgent>();
        f.Brain = f.Go.AddComponent<ManagedNpcBrain>();
        Set(f.Brain, "_role", role); Set(f.Brain, "_backend", backend); Set(f.Brain, "_canMove", canMove);
        f.Actor = f.Go.AddComponent<ManagedNpcActor>();
        if (waypoints != null) Set(f.Actor, "_waypoints", waypoints);
        f.Actor.Attacked += (a, t) => { f.Hits++; f.HitTimes.Add(Time.time); };
        return f;
    }
    static Fighter Goblin(Vector3 pos) { return Fight("goblin", pos, NpcRole.Enemy, DecisionBackend.Utility, true, null); }

    static GameObject Marker(float x, float z) { GameObject g = new GameObject("wp"); g.transform.position = new Vector3(x, 0, z); return g; }

    // Runs the director and every actor and agent forward. A step is 0.05 s.
    static void Advance(float seconds)
    {
        const float dt = 0.05f;
        int steps = (int)System.Math.Round(seconds / dt);
        for (int i = 0; i < steps; i++)
        {
            Time.time += dt; Time.deltaTime = dt;
            ManagedNpcDirector d = Director();
            if (d != null) { Update(d); Note(d); }
            List<UnityEngine.Component> all = new List<UnityEngine.Component>(UnityEngine.Object.All);
            foreach (UnityEngine.Component c in all) if (c is ManagedNpcActor && ((Behaviour)c).enabled) UnityEngine.Component.Invoke(c, "Update");
            foreach (UnityEngine.Component c in all) { NavMeshAgent a = c as NavMeshAgent; if (a != null) a.Simulate(dt); }
        }
    }
    static float Dist(Vector3 a, Vector3 b) { return (a - b).magnitude; }

    static void ThreatMemory()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcBrain g = Npc("g", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Check(src.Damageable != null && ReferenceEquals(src.Damageable, player.GetComponent<ManagedHealth>()), "a threat source finds the health on its object");
        Check(g.Threat == null, "no threat before the first tick");
        Tick(0.1f);
        Check(g.Threat == src && Dist(g.LastKnownThreatPosition, player.transform.position) < 0.001f, "the brain remembers what it saw and where");
        player.transform.position = new Vector3(4, 0, 10);
        Wall();
        Tick(0.1f);
        Check(g.Threat == src && Dist(g.LastKnownThreatPosition, new Vector3(0, 0, 10)) < 0.001f, "while lingering the last known position stays where it was seen");
        Tick(0.6f);
        Check(g.Threat == null && !g.ThreatVisible, "the memory ends with the lingering");

        ResetWorld();
        GameObject bare = Player(10f, false);
        Check(bare.GetComponent<ManagedNpcThreatSource>().Damageable == null, "a threat source with no health has no damageable");
    }

    static void ActorPatrol()
    {
        ResetWorld();
        GameObject w0 = Marker(5, 0), w1 = Marker(5, 5);
        Fighter f = Fight("patroller", Vector3.zero, NpcRole.Enemy, DecisionBackend.Utility, true, new[] { w0.transform, w1.transform });
        Advance(0.3f);
        Check(f.Brain.Action == NpcAction.Patrol && f.Actor.Mode == NpcAction.Patrol, "no threat means patrol");
        Check(Dist(f.Agent.destination, w0.transform.position) < 0.01f, "the first waypoint is the first stop");
        Check(System.Math.Abs(f.Agent.speed - 1.8f) < 0.001f, "patrol uses the patrol speed");
        Advance(3.0f);
        Check(Dist(f.Go.transform.position, w0.transform.position) < 0.5f, "the NPC reaches the first waypoint");
        Check(f.Agent.isStopped, "it stands still at the stop");
        Advance(2.0f);
        Check(Dist(f.Agent.destination, w1.transform.position) < 0.01f && !f.Agent.isStopped, "after the pause it heads to the second waypoint");
        Advance(5.0f);
        Check(Dist(f.Agent.destination, w0.transform.position) < 0.01f || Dist(f.Go.transform.position, w1.transform.position) < 0.5f, "the route loops back to the start");

        ResetWorld();
        Fighter wander = Goblin(Vector3.zero);
        HashSet<string> seen = new HashSet<string>(); bool inside = true;
        for (int i = 0; i < 240; i++)
        {
            Advance(0.05f);
            if (wander.Agent.hasDestination) { seen.Add(wander.Agent.destination.x.ToString("F2") + "," + wander.Agent.destination.z.ToString("F2")); if (Dist(wander.Agent.destination, Vector3.zero) > 8.01f) inside = false; }
        }
        Check(seen.Count >= 2, "wandering picks more than one destination, got " + seen.Count);
        Check(inside, "wander destinations stay inside the wander radius of home");
    }

    static void ActorAttack()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        Advance(0.3f);
        Check(f.Brain.Action == NpcAction.Attack && f.Actor.Mode == NpcAction.Attack, "a visible threat means attack");
        Check(System.Math.Abs(f.Agent.destination.z - 10f) < 0.01f && System.Math.Abs(f.Agent.speed - 3.8f) < 0.001f, "it chases at the chase speed toward the threat");
        Check(System.Math.Abs(f.Agent.stoppingDistance - 1.44f) < 0.01f, "it stops inside the attack range");
        Advance(3.7f);
        Check(Dist(f.Go.transform.position, player.transform.position) < 1.8f, "it closes to melee range");
        Check(f.Hits >= 2 && f.Hits <= 3, "melee hits land on the cooldown, got " + f.Hits);
        bool spaced = true; for (int i = 1; i < f.HitTimes.Count; i++) if (f.HitTimes[i] - f.HitTimes[i - 1] < 1.19f) spaced = false;
        Check(spaced, "hits are at least one cooldown apart");
        ManagedHealth ph = player.GetComponent<ManagedHealth>();
        Check(System.Math.Abs(ph.Current - (100f - 10f * f.Hits)) < 0.001f, "the player lost exactly the damage of the hits, " + ph.Current);
        float facing = System.Math.Abs(Mathf.DeltaAngle(f.Go.transform.rotation.yaw, 0f));
        Check(facing < 5f, "it faces the target");

        // Killing the player ends the fight.
        ResetWorld();
        GameObject weak = Player(10f, true); Clear(weak); weak.GetComponent<ManagedHealth>().TakeDamage(75f);
        Fighter g = Goblin(Vector3.zero);
        Advance(8f);
        Check(!weak.GetComponent<ManagedHealth>().IsAlive && g.Hits == 3, "three hits kill a player with 25 health, hits " + g.Hits);
        Advance(2f);
        Check(g.Brain.Action == NpcAction.Patrol, "with the player dead the goblin goes back to patrol, got " + g.Brain.Action);
        int before = g.Hits; Advance(4f);
        Check(g.Hits == before, "no hits land on a dead player");

        // Two attackers can both be ready in the same frame. The second must not hit a target the first just killed.
        ResetWorld();
        GameObject victim = Player(2f, true); Clear(victim);
        Fighter h = Goblin(Vector3.zero);
        Advance(0.3f);
        Check(h.Hits >= 1, "the goblin hits a target in range");
        int landed = h.Hits;
        Set(h.Actor, "_nextAttackTime", 0f);
        victim.GetComponent<ManagedHealth>().TakeDamage(1000f);
        UnityEngine.Component.Invoke(h.Actor, "Update");
        Check(h.Hits == landed, "an attacker does not hit a target that is already dead");
    }

    static void ActorLostTarget()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        Advance(0.3f);
        player.transform.position = new Vector3(6, 0, 10);
        Wall();
        Advance(0.4f);
        Check(f.Agent.SetDestinationCalls >= 2, "the chase destination is refreshed while the target is remembered");
        Check(Dist(f.Agent.destination, new Vector3(0, 0, 10)) < 0.01f, "out of sight, the NPC goes to where it last saw the target, not to the live position");
        Advance(2.0f);
        Check(f.Brain.Action == NpcAction.Patrol, "after the memory runs out it patrols again, got " + f.Brain.Action);

        ResetWorld();
        GameObject far = Player(10f, true); Clear(far);
        NavMesh.IsWalkable = p => p.x < 100f;
        far.transform.position = new Vector3(500, 0, 5);
        Fighter g = Goblin(Vector3.zero);
        Set(g.Brain, "_viewRange", 1000f); Set(g.Brain, "_fieldOfViewDegrees", 360f);
        Advance(1f);
        Check(g.Brain.Action == NpcAction.Attack && g.Agent.SetDestinationCalls == 0, "a target off the NavMesh gives no destination and no exception");
    }

    static void ActorRetreat()
    {
        ResetWorld();
        GameObject player = Player(5f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        f.Health.TakeDamage(90f);
        Advance(0.5f);
        Check(f.Brain.Action == NpcAction.Retreat, "a badly hurt goblin retreats, got " + f.Brain.Action);
        Check(f.Agent.destination.z <= -11.9f && System.Math.Abs(f.Agent.speed - 4.2f) < 0.001f, "it runs directly away at the retreat speed, " + f.Agent.destination.z);
        Advance(2f);
        Check(f.Go.transform.position.z < -4f, "it actually moves away");
        Check(f.Hits == 0, "a retreating NPC does not hit");
    }

    static void ActorIdleTrade()
    {
        ResetWorld();
        GameObject player = Player(10f, false); Clear(player);
        Fighter m = Fight("merchant", Vector3.zero, NpcRole.Merchant, DecisionBackend.StateMachine, false, null);
        Advance(1f);
        Check(m.Brain.Action == NpcAction.Idle && m.Agent.isStopped, "a merchant under threat stands still");
        player.GetComponent<ManagedNpcThreatSource>().enabled = false;
        Advance(1f);
        Check(m.Brain.Action == NpcAction.Trade && m.Agent.isStopped, "with no threat it trades, standing still");
        Advance(2f);
        Check(Dist(m.Go.transform.position, Vector3.zero) < 0.001f && m.Agent.SetDestinationCalls == 0, "a merchant never moves");
    }

    static void ActorNavMeshAndDeath()
    {
        ResetWorld();
        Fighter f = Goblin(Vector3.zero);
        f.Agent.isOnNavMesh = false;
        Advance(1f);
        Check(Debug.Warnings.FindAll(w => w.Contains("not on a NavMesh")).Count == 1, "off the NavMesh warns once");
        Check(f.Agent.SetDestinationCalls == 0, "nothing is sent to an agent that is not on the NavMesh");
        f.Agent.isOnNavMesh = true;
        Advance(0.5f);
        Check(System.Math.Abs(f.Agent.speed - 1.8f) < 0.001f && f.Agent.SetDestinationCalls >= 1, "once on the NavMesh it is configured and moves");

        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter d = Goblin(Vector3.zero);
        Set(d.Actor, "_removeAfterDeathSeconds", 2f);
        Advance(0.3f);
        d.Health.TakeDamage(1000f);
        Advance(1f);
        Check(d.Actor.Mode == NpcAction.Idle && d.Agent.isStopped, "a dead NPC stops");
        Check(UnityEngine.Object.Destroyed.Count == 1 && ReferenceEquals(UnityEngine.Object.Destroyed[0].Key, d.Go) && UnityEngine.Object.Destroyed[0].Value == 2f, "its removal is scheduled");
        Advance(2f);
        Check(d.Actor.Mode == NpcAction.Idle && d.Hits == 0, "a dead NPC stays idle and does not hit");

        ResetWorld();
        Fighter keep = Goblin(Vector3.zero);
        keep.Health.TakeDamage(1000f); Advance(0.5f);
        Check(UnityEngine.Object.Destroyed.Count == 0, "with removal off the body stays");
    }

    static GameObject Target(string name, float x, float z, bool damageable)
    {
        GameObject g = new GameObject(name); g.transform.position = new Vector3(x, 0, z);
        if (damageable) g.AddComponent<ManagedHealth>();
        g.AddComponent<Collider>();
        return g;
    }
    static ManagedSpellImpact Fire(float x, float z, GameObject source = null)
    {
        return new ManagedSpellImpact { Position = new Vector3(x, 0, z), Kind = ManagedSpellKind.Fireball, Source = source };
    }
    static float HP(GameObject g) { return g.GetComponent<ManagedHealth>().Current; }

    static void SpellDamage()
    {
        ResetWorld();
        GameObject a = Target("a", 0, 0, true), b = Target("b", 2, 0, true), far = Target("far", 10, 0, true), rock = Target("rock", 1, 0, false);
        int hurt = ManagedSpellDamage.Apply(Fire(0, 0));
        Check(hurt == 2, "a fireball hurts the two damageable targets in range, got " + hurt);
        Check(System.Math.Abs(HP(a) - 60f) < 0.01f, "a direct hit does full damage, " + HP(a));
        Check(System.Math.Abs(HP(b) - 75f) < 0.05f, "damage falls off toward the edge, " + HP(b));
        Check(HP(far) == 100f, "a target outside the radius is untouched");

        ResetWorld();
        GameObject c = Target("c", 0, 0, true);
        Check(ManagedSpellDamage.Apply(new ManagedSpellImpact { Position = Vector3.zero, Kind = ManagedSpellKind.Ice }) == 0 && HP(c) == 100f, "ice does no damage");
        ManagedSpellDamage.Enabled = false;
        Check(ManagedSpellDamage.Apply(Fire(0, 0)) == 0 && HP(c) == 100f, "damage can be switched off");
        ManagedSpellDamage.Enabled = true;

        ResetWorld();
        GameObject body = Target("body", 0, 0, true);
        GameObject arm = new GameObject("arm"); arm.transform.parent = body.transform; arm.transform.position = new Vector3(0.5f, 0, 0); arm.AddComponent<Collider>();
        Check(ManagedSpellDamage.Apply(Fire(0, 0)) == 1 && System.Math.Abs(HP(body) - 60f) < 0.01f, "a body with two colliders is hurt once, " + HP(body));

        ResetWorld();
        GameObject self = Target("caster", 0, 0, true), other = Target("other", 1, 0, true);
        ManagedSpellDamage.Apply(Fire(0, 0, self));
        Check(HP(self) == 100f && HP(other) < 100f, "the caster is not hurt by its own fireball");
        ManagedSpellDamage.HurtCaster = true;
        ManagedSpellDamage.Apply(Fire(0, 0, self));
        Check(HP(self) < 100f, "the caster is hurt when that is switched on");
        ManagedSpellDamage.HurtCaster = false;

        ResetWorld();
        GameObject dead = Target("dead", 0, 0, true); dead.GetComponent<ManagedHealth>().TakeDamage(1000f);
        Check(ManagedSpellDamage.Apply(Fire(0, 0)) == 0, "a dead target is not counted as hurt");

        ResetWorld();
        List<GameObject> crowd = new List<GameObject>();
        for (int i = 0; i < 40; i++) crowd.Add(Target("t" + i, 0, i * 0.05f, true));
        int many = ManagedSpellDamage.Apply(Fire(0, 0));
        Check(many > 0 && many <= 32, "a crowd larger than the overlap buffer is capped without an error, hurt " + many);

        ResetWorld();
        GameObject viaEvent = Target("viaEvent", 0, 0, true);
        MethodInfo subscribe = typeof(ManagedSpellDamage).GetMethod("Subscribe", BindingFlags.Static | BindingFlags.NonPublic);
        subscribe.Invoke(null, null); subscribe.Invoke(null, null);
        Check(ManagedSpellCaster.SubscriberCount == 1, "subscribing twice leaves one subscription");
        ManagedSpellCaster.Raise(Fire(0, 0));
        Check(System.Math.Abs(HP(viaEvent) - 60f) < 0.01f, "an impact raised by the caster reaches the damage, once");

        ManagedSpellDefinition fire = ManagedSpellDefinition.For(ManagedSpellKind.Fireball);
        Check(fire != null && fire.ImpactDamage == 40f && fire.ImpactRadius == 3f, "the fireball definition carries the damage numbers");
        Check(ManagedSpellDefinition.For(ManagedSpellKind.Ice).ImpactDamage == 0f, "the ice definition has none");
    }


    // ---------- NPC debugger (Editor folder code, run against UnityEditor stubs) ----------

    static void DebugView()
    {
        ResetWorld(); UnityEditor.Handles.Reset();
        Check(ManagedNpcDebugView.ColorFor(NpcAction.Attack).r > 0.9f && ManagedNpcDebugView.ColorFor(NpcAction.Patrol).g > 0.8f, "attack is red and patrol is green");
        HashSet<string> colors = new HashSet<string>();
        foreach (NpcAction a in new[] { NpcAction.Idle, NpcAction.Trade, NpcAction.Patrol, NpcAction.Attack, NpcAction.Retreat })
        { Color c = ManagedNpcDebugView.ColorFor(a); colors.Add(c.r + "," + c.g + "," + c.b); }
        Check(colors.Count == 5, "every action has its own color");

        GameObject player = Player(10f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        f.Go.name = "goblin-1";
        Advance(0.3f);
        string line = ManagedNpcDebugView.Describe(f.Brain);
        Check(line.Contains("goblin-1") && line.Contains("Enemy/Utility") && line.Contains("Attack") && line.Contains("hp 100%") && line.Contains("sees threat"), "the description names the NPC, role, action, health and threat: " + line);
        f.Health.TakeDamage(1000f);
        Advance(0.3f);
        Check(ManagedNpcDebugView.Describe(f.Brain).Contains("dead") && !ManagedNpcDebugView.Describe(f.Brain).Contains("hp"), "a dead NPC is described as dead");
        Check(ManagedNpcDebugView.DescribeDirector(null, 3).Contains("no director"), "no director is reported");
        Check(ManagedNpcDebugView.DescribeDirector(Director(), 1).Contains("managed fallback") || ManagedNpcDebugView.DescribeDirector(Director(), 1).Contains("native"), "the director line names the decision path");
    }

    static void DebugFilter()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter attacker = Goblin(Vector3.zero);
        Fighter merchant = Fight("merchant", new Vector3(0, 0, 1), NpcRole.Merchant, DecisionBackend.StateMachine, false, null);
        Advance(0.3f);
        ManagedNpcDrawOptions o = new ManagedNpcDrawOptions();
        Check(ManagedNpcDebugView.Passes(o, attacker.Brain) && ManagedNpcDebugView.Passes(o, merchant.Brain), "no filter shows everything");
        o.Filter = ManagedNpcActionFilter.Attack;
        Check(ManagedNpcDebugView.Passes(o, attacker.Brain) && !ManagedNpcDebugView.Passes(o, merchant.Brain), "the attack filter shows attackers only");
        o.Filter = ManagedNpcActionFilter.Idle;
        Check(!ManagedNpcDebugView.Passes(o, attacker.Brain) && ManagedNpcDebugView.Passes(o, merchant.Brain), "the idle filter shows the idle merchant");
        o.Filter = ManagedNpcActionFilter.Any; o.OnlyWithThreat = true;
        Check(ManagedNpcDebugView.Passes(o, attacker.Brain), "only-with-threat keeps an NPC that sees one");
        player.GetComponent<ManagedNpcThreatSource>().enabled = false; Advance(1f);
        Check(!ManagedNpcDebugView.Passes(o, attacker.Brain), "only-with-threat drops it once the threat is gone");
        // The filter enum order must follow the action order after the Any entry.
        foreach (NpcAction a in new[] { NpcAction.Idle, NpcAction.Trade, NpcAction.Patrol, NpcAction.Attack, NpcAction.Retreat })
            Check((int)a == (int)(ManagedNpcActionFilter)System.Enum.Parse(typeof(ManagedNpcActionFilter), a.ToString()) - 1, "filter " + a + " lines up with the action");
    }

    static void DebugRoute()
    {
        ResetWorld();
        GameObject w0 = Marker(1, 0), w1 = Marker(2, 0), w2 = Marker(3, 0);
        Fighter f = Fight("p", Vector3.zero, NpcRole.Enemy, DecisionBackend.Utility, true, new[] { w0.transform, null, w1.transform, w2.transform });
        List<Vector3> route = new List<Vector3>();
        int n = ManagedNpcDebugView.CollectRoute(f.Actor, route);
        Check(n == 3 && route[0].x == 1 && route[2].x == 3, "the route skips empty stops and starts at the first");
        Set(f.Actor, "_nextWaypoint", 2);
        ManagedNpcDebugView.CollectRoute(f.Actor, route);
        Check(route.Count == 3 && route[0].x == 2 && route[1].x == 3 && route[2].x == 1, "the route starts at the next stop and wraps");
        ResetWorld();
        Fighter none = Goblin(Vector3.zero);
        Check(ManagedNpcDebugView.CollectRoute(none.Actor, route) == 0 && route.Count == 0, "no waypoints gives an empty route");
    }

    static int Count(string prefix) { int n = 0; foreach (string c in UnityEditor.Handles.Calls) if (c.StartsWith(prefix)) n++; return n; }

    static void DebugDraw()
    {
        ResetWorld(); UnityEditor.Handles.Reset();
        GameObject player = Player(10f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        Advance(0.4f);
        List<Vector3> scratch = new List<Vector3>();
        ManagedNpcDrawOptions all = new ManagedNpcDrawOptions { Mode = ManagedNpcDrawMode.All, SightCones = true };
        int calls0 = f.Agent.SetDestinationCalls; Vector3 pos0 = f.Go.transform.position;
        ManagedNpcDebugView.Draw(f.Brain, all, true, scratch);
        Check(Count("Label:") == 1, "one label for the NPC");
        Check(Count("WireDisc:1.80") == 1, "the attack range is drawn at the attack range radius");
        Check(Count("SolidDisc") == 1 && Count("Line") >= 1, "the destination and the path are drawn");
        Check(Count("Arc:") == 1, "the sight cone arc is drawn");
        Check(Count("Line") >= 1 + 2 + 2, "the path, two cone edges and the last-known-target cross are drawn, got " + Count("Line"));
        Check(f.Agent.SetDestinationCalls == calls0 && f.Go.transform.position.x == pos0.x && f.Go.transform.position.z == pos0.z, "drawing changes nothing in the game");

        UnityEditor.Handles.Reset();
        ManagedNpcDrawOptions off = new ManagedNpcDrawOptions { Labels = false, Paths = false, Ranges = false, SightCones = false };
        ManagedNpcDebugView.Draw(f.Brain, off, false, scratch);
        Check(UnityEditor.Handles.Calls.Count == 0, "with every switch off nothing is drawn, got " + UnityEditor.Handles.Calls.Count);

        ResetWorld(); UnityEditor.Handles.Reset();
        GameObject w0 = Marker(5, 0), w1 = Marker(5, 5), w2 = Marker(0, 5);
        Fighter p = Fight("patrol", Vector3.zero, NpcRole.Enemy, DecisionBackend.Utility, true, new[] { w0.transform, w1.transform, w2.transform });
        Advance(0.3f);
        ManagedNpcDebugView.Draw(p.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("Dotted") == 3 && Count("Label:") == 4, "a patrol route draws a dotted leg and a number per stop, plus the NPC label");
        Check(Count("WireDisc:0.30") == 3, "each stop gets a marker");
        Check(Count("WireDisc:8.00") == 0, "no wander radius when waypoints exist");

        ResetWorld(); UnityEditor.Handles.Reset();
        Fighter wander = Goblin(Vector3.zero);
        Advance(0.3f);
        ManagedNpcDebugView.Draw(wander.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("WireDisc:8.00") == 1, "a wandering NPC shows its wander radius");

        ResetWorld(); UnityEditor.Handles.Reset();
        Npc("no actor", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Advance(0.2f);
        ManagedNpcDebugView.Draw(ManagedNpcDirector.GetBrain(0), new ManagedNpcDrawOptions(), false, scratch);
        Check(Count("Label:") == 1, "an NPC with no actor still gets its label and does not break the drawer");
    }

    static void DebugWindow()
    {
        ResetWorld(); UnityEditor.Handles.Reset();
        UnityEngine.Application.isPlaying = true;
        GameObject player = Player(10f, true); Clear(player);
        List<Fighter> crowd = new List<Fighter>();
        for (int i = 0; i < 5; i++) crowd.Add(Goblin(new Vector3(i, 0, 0)));
        Advance(0.3f);

        ManagedNpcDebuggerWindow window = ManagedNpcDebuggerWindow.GetWindow<ManagedNpcDebuggerWindow>("GTG NPC Debugger");
        MethodInfo enable = typeof(ManagedNpcDebuggerWindow).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo disable = typeof(ManagedNpcDebuggerWindow).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo sceneGui = typeof(ManagedNpcDebuggerWindow).GetMethod("OnSceneGui", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo gui = typeof(ManagedNpcDebuggerWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo inspector = typeof(ManagedNpcDebuggerWindow).GetMethod("OnInspectorUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        ManagedNpcDrawOptions options = (ManagedNpcDrawOptions)typeof(ManagedNpcDebuggerWindow).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);

        int before = UnityEditor.SceneView.SubscriberCount;
        enable.Invoke(window, null);
        Check(UnityEditor.SceneView.SubscriberCount == before + 1, "opening the window subscribes to the Scene view");
        disable.Invoke(window, null);
        Check(UnityEditor.SceneView.SubscriberCount == before, "closing the window unsubscribes");

        options.Labels = true; options.Paths = false; options.Ranges = false;
        options.Mode = ManagedNpcDrawMode.Off; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(UnityEditor.Handles.Calls.Count == 0, "mode Off draws nothing");
        options.Mode = ManagedNpcDrawMode.SelectedOnly; UnityEditor.Selection.activeGameObject = null; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == 0, "selected only with no selection draws nothing");
        UnityEditor.Selection.activeGameObject = crowd[2].Go; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == 1, "selected only draws the one selected NPC");
        options.Mode = ManagedNpcDrawMode.All; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == 5, "all draws every NPC, got " + Count("Label:"));
        options.Filter = ManagedNpcActionFilter.Patrol; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == 0, "the filter applies to the Scene drawing too");
        options.Filter = ManagedNpcActionFilter.Any;
        UnityEngine.Application.isPlaying = false; UnityEditor.Handles.Reset(); sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == 0, "nothing is drawn in Edit mode");
        UnityEngine.Application.isPlaying = true;

        UnityEditor.SceneView.lastActiveSceneView = new UnityEditor.SceneView();
        UnityEngine.GUILayout.Labels.Clear();
        UnityEngine.GUILayout.NextButton = true; UnityEditor.Selection.activeGameObject = null;
        gui.Invoke(window, null);
        Check(UnityEditor.Selection.activeGameObject != null && UnityEditor.SceneView.lastActiveSceneView.FrameCalls >= 1, "clicking a row selects the NPC and frames it");
        UnityEngine.GUILayout.NextButton = false;
        int repaints = UnityEditor.SceneView.RepaintCalls; inspector.Invoke(window, null);
        Check(window.RepaintCalls >= 1 && UnityEditor.SceneView.RepaintCalls == repaints + 1, "the inspector tick repaints the window and the Scene view");
        options.Mode = ManagedNpcDrawMode.Off; repaints = UnityEditor.SceneView.RepaintCalls; inspector.Invoke(window, null);
        Check(UnityEditor.SceneView.RepaintCalls == repaints, "no Scene repaint when drawing is off");

        ResetWorld(); UnityEditor.Handles.Reset();
        UnityEngine.Application.isPlaying = true;
        GameObject p2 = Player(10f, true); Clear(p2);
        for (int i = 0; i < 350; i++) Goblin(new Vector3(i * 0.01f, 0, 0));
        Set(Director(), "_maxSightRaysPerTick", 100000);
        Advance(0.5f);
        options.Mode = ManagedNpcDrawMode.All;
        sceneGui.Invoke(window, new object[] { null });
        Check(Count("Label:") == ManagedNpcDebugView.MaxDrawn, "a crowd is capped at the draw limit, got " + Count("Label:"));
        UnityEngine.Application.isPlaying = false;
    }


    // ---------- observation version 3: disposition, provoked, level, orders, companions ----------

    static Fighter Companion(string name, Vector3 pos, Transform leader, int level)
    {
        Fighter f = Fight(name, pos, NpcRole.Companion, DecisionBackend.Utility, true, null);
        f.Brain.Leader = leader; f.Brain.Level = level;
        return f;
    }

    static void ObservationV3()
    {
        ResetWorld();
        GameObject g = new GameObject("g"); g.AddComponent<ManagedHealth>();
        ManagedNpcBrain b = g.AddComponent<ManagedNpcBrain>();
        NPCNativeObservation o = b.BuildObservation();
        Check(o.Disposition == 0 && o.Order == 0 && o.Provoked == 0 && o.Level == 1 && o.OrderLevel == 0 && o.ReservedByte == 0 && o.ReservedA == 0 && o.ReservedB == 0 && o.ReservedC == 0, "defaults are hostile, no order, not provoked, level 1");
        b.Disposition = NpcDisposition.Peaceful; b.Level = 7; b.SetOrder(NpcOrder.Attack, 9);
        o = b.BuildObservation();
        Check(o.Disposition == 2 && o.Level == 7 && o.Order == 3 && o.OrderLevel == 9, "the settings reach the observation");
        b.Level = 70000; b.SetOrder(NpcOrder.Attack, 123456);
        o = b.BuildObservation();
        Check(o.Level == 65535 && o.OrderLevel == 65535, "levels are clamped to 16 bits");
        b.Level = -4; b.SetOrder(NpcOrder.Hold, -3);
        o = b.BuildObservation();
        Check(o.Level == 0 && o.OrderLevel == 0 && b.Level == 0, "negative levels count as zero");
        b.SetOrder(NpcOrder.None, 50);
        Check(b.Order == NpcOrder.None && b.OrderLevel == 0, "no order carries no level");
        Check(b.BuildObservation().Order == 0, "the observation says no order");
    }

    static void Provocation()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter r = Goblin(Vector3.zero);
        r.Brain.Disposition = NpcDisposition.Retaliatory;
        Set(r.Brain, "_provokedMemorySeconds", 2f);
        Advance(0.4f);
        Check(r.Brain.ThreatVisible && !r.Brain.Provoked, "it sees the player and is not provoked");
        Check(r.Brain.Action == NpcAction.Patrol, "a retaliatory NPC ignores a threat that has not attacked, got " + r.Brain.Action);

        r.Health.TakeDamage(1f, null);
        Check(!r.Brain.Provoked, "damage with no source does not provoke");
        r.Health.TakeDamage(1f, r.Go);
        Check(!r.Brain.Provoked, "damage from itself does not provoke");
        r.Health.TakeDamage(1f, player);
        Check(r.Brain.Provoked && ReferenceEquals(r.Brain.LastAttacker, player), "damage from the player provokes and is remembered");
        Check(r.Brain.BuildObservation().Provoked == 1, "the observation says provoked");
        Advance(0.3f);
        Check(r.Brain.Action == NpcAction.Attack, "once attacked it fights back, got " + r.Brain.Action);
        Advance(3f);
        Check(!r.Brain.Provoked && r.Brain.BuildObservation().Provoked == 0, "provocation runs out");
        Check(r.Brain.Action != NpcAction.Attack, "and it stops fighting, got " + r.Brain.Action);
        r.Go.GetComponent<ManagedHealth>().ResetToFull();

        ResetWorld();
        GameObject p2 = Player(10f, true); Clear(p2);
        Fighter peaceful = Goblin(Vector3.zero);
        peaceful.Brain.Disposition = NpcDisposition.Peaceful;
        Advance(0.4f);
        Check(peaceful.Brain.Action == NpcAction.Retreat, "a peaceful NPC runs from a threat, got " + peaceful.Brain.Action);
        peaceful.Health.TakeDamage(1f, p2);
        Advance(0.3f);
        Check(peaceful.Brain.Action == NpcAction.Retreat && peaceful.Hits == 0, "even when attacked it does not fight back");
        Advance(2f);
        Check(peaceful.Go.transform.position.z < -2f, "it runs away");

        ResetWorld();
        GameObject p3 = Player(10f, true); Clear(p3);
        Fighter m = Fight("merchant", Vector3.zero, NpcRole.Merchant, DecisionBackend.StateMachine, false, null);
        m.Health.TakeDamage(5f, p3);
        Advance(0.4f);
        Check(m.Brain.Action == NpcAction.Idle, "a merchant that is attacked stays at its stall");

        // The damage event is unsubscribed when the NPC is disabled, so a disabled NPC is not changed.
        ResetWorld();
        GameObject p4 = Player(10f, true);
        Fighter off = Goblin(Vector3.zero);
        off.Brain.enabled = false;
        off.Health.TakeDamage(5f, p4);
        Check(!off.Brain.Provoked, "a disabled brain is not provoked");
    }

    static void CompanionFollow()
    {
        ResetWorld();
        GameObject player = Player(12f, true); Clear(player);
        Fighter c = Companion("companion", Vector3.zero, player.transform, 10);
        Advance(0.4f);
        Check(!c.Brain.ThreatVisible && ManagedNpcDirector.GetBrain(0).Threat == null, "a companion never sees its leader as a threat");
        Check(c.Brain.Action == NpcAction.Follow && c.Actor.Mode == NpcAction.Follow, "a companion with no order follows, got " + c.Brain.Action);
        Check(System.Math.Abs(c.Agent.speed - 3.4f) < 0.001f, "it walks at the follow speed");
        Advance(6f);
        float d = Dist(c.Go.transform.position, player.transform.position);
        Check(d <= 3.1f && d >= 2.0f && c.Agent.isStopped, "it stops near the leader, " + d);

        int calls = c.Agent.SetDestinationCalls;
        player.transform.position = c.Go.transform.position + new Vector3(0, 0, 3.6f);
        Advance(1f);
        Check(c.Agent.isStopped && c.Agent.SetDestinationCalls == calls, "a leader a little farther away does not restart it");
        player.transform.position = c.Go.transform.position + new Vector3(0, 0, 6f);
        Advance(0.5f);
        Check(!c.Agent.isStopped && c.Agent.SetDestinationCalls > calls, "a leader well beyond the distance does");

        ResetWorld();
        GameObject p2 = Player(12f, true);
        Fighter lost = Companion("lost", Vector3.zero, null, 10);
        Advance(0.4f);
        Check(Debug.Warnings.FindAll(w => w.Contains("no Leader")).Count == 1, "a companion without a leader warns once");
        ManagedNpcBrain brain = lost.Brain;
        brain.Leader = p2.transform;
        Advance(0.4f);
        Check(brain.Action == NpcAction.Follow, "and follows once given a leader");

        ResetWorld();
        GameObject p3 = Player(12f, true);
        Fighter noLeaderNoWalk = Fight("c", Vector3.zero, NpcRole.Companion, DecisionBackend.Utility, true, null);
        noLeaderNoWalk.Brain.Level = 1;
        Advance(0.4f);
        Check(noLeaderNoWalk.Agent.SetDestinationCalls == 0, "a follower with no leader has nowhere to go");
    }

    static void CompanionOrders()
    {
        ResetWorld();
        GameObject player = Player(12f, true);
        Fighter c = Companion("c", Vector3.zero, player.transform, 10);
        Advance(0.3f);
        c.Brain.SetOrder(NpcOrder.Hold, 0);
        Vector3 at = c.Go.transform.position;
        Advance(0.3f);
        int calls = c.Agent.SetDestinationCalls;
        Advance(2f);
        Check(c.Brain.Action == NpcAction.Hold && c.Actor.Mode == NpcAction.Hold, "hold holds, got " + c.Brain.Action);
        Check(c.Agent.isStopped && c.Agent.SetDestinationCalls == calls && Dist(c.Go.transform.position, at) < 0.5f, "a holding companion stands still");
        c.Brain.SetOrder(NpcOrder.None, 0);
        Advance(0.5f);
        Check(c.Brain.Action == NpcAction.Follow, "clearing the order goes back to following");

        // Refusal.
        ResetWorld();
        GameObject p2 = Player(12f, true);
        Fighter r = Companion("r", Vector3.zero, p2.transform, 10);
        List<string> refused = new List<string>();
        r.Brain.OrderRefused += (b, o, lvl) => refused.Add(o + ":" + lvl + ":" + b.Order);
        r.Brain.SetOrder(NpcOrder.Attack, 13);
        Advance(0.4f);
        Check(refused.Count == 0 && r.Brain.Order == NpcOrder.Attack && r.Brain.Action != NpcAction.RefuseOrder, "an attack order exactly 3 above the level is accepted");
        r.Brain.SetOrder(NpcOrder.Attack, 14);
        Advance(0.2f);
        Check(refused.Count == 1 && refused[0] == "Attack:14:None", "an attack order 4 above the level is refused, and the order is already cleared: " + string.Join(",", refused));
        Check(r.Brain.Order == NpcOrder.None && r.Brain.OrderLevel == 0, "the refused order is gone");
        Advance(0.3f);
        Check(r.Brain.Action == NpcAction.Follow, "after the refusal it follows, got " + r.Brain.Action);
        Advance(0.5f);
        Check(refused.Count == 1, "one refusal, not one per tick");

        // A refusal result for a companion that has no order any more (the game cleared it between the
        // observation and the answer) clears nothing and raises no event.
        ResetWorld();
        GameObject pn = Player(12f, true);
        Fighter none = Companion("none", Vector3.zero, pn.transform, 1);
        int noneEvents = 0;
        none.Brain.OrderRefused += (b, o, lvl) => noneEvents++;
        none.Brain.ApplyAction(NpcAction.RefuseOrder);
        Check(noneEvents == 0 && none.Brain.Order == NpcOrder.None && none.Brain.Action == NpcAction.RefuseOrder, "a refusal with no order raises no event");

        // The refusal action is carried out as standing still.
        ResetWorld();
        GameObject p3 = Player(12f, true);
        Fighter s = Companion("s", Vector3.zero, p3.transform, 1);
        Advance(0.4f);
        s.Brain.SetOrder(NpcOrder.Hold, 99);
        Check(s.Agent.SetDestinationCalls >= 1, "it was walking before");
        UnityEngine.Component.Invoke(Director(), "Update");
        Time.time += 0.2f; UnityEngine.Component.Invoke(Director(), "Update");
        Check(s.Actor.Mode == NpcAction.RefuseOrder || s.Actor.Mode == NpcAction.Follow, "the actor takes the refusal");

        // A listener that gives another too-hard order from inside the refusal event is refused again.
        ResetWorld();
        GameObject p4 = Player(12f, true);
        Fighter again = Companion("again", Vector3.zero, p4.transform, 1);
        int times = 0;
        again.Brain.OrderRefused += (b, o, lvl) => { times++; if (times < 3) b.SetOrder(NpcOrder.Attack, 40); };
        again.Brain.SetOrder(NpcOrder.Attack, 40);
        Advance(1.5f);
        Check(times == 3 && again.Brain.Order == NpcOrder.None, "every refusal clears the order, even back to back, refused " + times);

        // Orders mean nothing to other roles.
        ResetWorld();
        GameObject p5 = Player(12f, true); Clear(p5);
        Fighter goblin = Goblin(Vector3.zero);
        goblin.Brain.SetOrder(NpcOrder.Hold, 99);
        Advance(0.4f);
        Check(goblin.Brain.Action == NpcAction.Attack, "a goblin ignores a hold order, got " + goblin.Brain.Action);
    }

    static void DebugViewV3()
    {
        ResetWorld();
        GameObject player = Player(12f, true);
        Fighter c = Companion("comp", Vector3.zero, player.transform, 10);
        c.Brain.SetOrder(NpcOrder.Attack, 9);
        Advance(0.3f);
        string line = ManagedNpcDebugView.Describe(c.Brain);
        Check(line.Contains("Companion") && line.Contains("lvl 10") && line.Contains("order Attack lvl 9"), "a companion line shows its level and order: " + line);
        Check(ManagedNpcDebugView.Describe(c.Brain).Contains("Follow") || ManagedNpcDebugView.Describe(c.Brain).Contains("Attack"), "and its action");
        Fighter peaceful = Goblin(new Vector3(1, 0, 0));
        peaceful.Brain.Disposition = NpcDisposition.Peaceful;
        Check(ManagedNpcDebugView.Describe(peaceful.Brain).Contains("Peaceful"), "a non-hostile disposition is shown");
        Check(!ManagedNpcDebugView.Describe(Goblin(new Vector3(2, 0, 0)).Brain).Contains("Hostile"), "a hostile one is not noise");
        peaceful.Health.TakeDamage(1f, player);
        Check(ManagedNpcDebugView.Describe(peaceful.Brain).Contains("provoked"), "provoked is shown");

        HashSet<string> colors = new HashSet<string>();
        foreach (NpcAction a in new[] { NpcAction.Idle, NpcAction.Trade, NpcAction.Patrol, NpcAction.Attack, NpcAction.Retreat, NpcAction.Follow, NpcAction.Hold, NpcAction.RefuseOrder, NpcAction.Mission, NpcAction.Betray })
        { Color col = ManagedNpcDebugView.ColorFor(a); colors.Add(col.r + "," + col.g + "," + col.b); }
        Check(colors.Count == 10, "all ten actions have their own color, got " + colors.Count);

        ManagedNpcDrawOptions o = new ManagedNpcDrawOptions();
        foreach (NpcAction a in new[] { NpcAction.Follow, NpcAction.Hold, NpcAction.RefuseOrder, NpcAction.Mission, NpcAction.Betray })
        {
            o.Filter = (ManagedNpcActionFilter)System.Enum.Parse(typeof(ManagedNpcActionFilter), a.ToString());
            Check((int)a == (int)o.Filter - 1, "filter " + a + " lines up with the action");
        }

        UnityEditor.Handles.Reset();
        List<Vector3> scratch = new List<Vector3>();
        Advance(0.5f);
        Fighter f = Companion("follower", Vector3.zero, player.transform, 10);
        Advance(0.5f);
        UnityEditor.Handles.Reset();
        ManagedNpcDebugView.Draw(f.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("Dotted") >= 1 && Count("WireDisc:3.00") == 1, "a follower shows a line to its leader and the follow distance around the leader");
    }


    // ---------- observation version 4: order types, betrayal, missions ----------

    static void ObservationV4()
    {
        ResetWorld();
        GameObject g = new GameObject("c"); g.AddComponent<ManagedHealth>();
        ManagedNpcBrain b = g.AddComponent<ManagedNpcBrain>();
        Set(b, "_role", NpcRole.Companion);
        NPCNativeObservation o = b.BuildObservation();
        Check(o.Trustworthiness == 0.5f && o.Affinity == 0.5f && o.PaySatisfaction == 0.5f && o.BetrayalOpportunity == 0 && o.Noise != 0, "a companion sends neutral loyalty numbers, no opportunity, and a random number");
        b.Trustworthiness = 7f; b.Affinity = -2f; b.PaySatisfaction = 0.25f; b.BetrayalOpportunity = true;
        o = b.BuildObservation();
        Check(o.Trustworthiness == 1f && o.Affinity == 0f && o.PaySatisfaction == 0.25f && o.BetrayalOpportunity == 1, "the numbers are clamped to 0..1 and the opportunity reaches the observation");
        Check(System.Math.Abs(b.Loyalty - (0.4f * 1f + 0.3f * 0f + 0.3f * 0.25f)) < 1e-5f, "loyalty is the weighted mix");
        HashSet<uint> seen = new HashSet<uint>();
        for (int i = 0; i < 50; i++) seen.Add(b.BuildObservation().Noise);
        Check(seen.Count == 50 && !seen.Contains(0u), "the random number changes every decision and is never zero");

        ResetWorld();
        ManagedNpcBrain a1 = Npc("a", NpcRole.Companion, DecisionBackend.Utility, true, Vector3.zero);
        ResetWorld();
        ManagedNpcBrain a2 = Npc("a", NpcRole.Companion, DecisionBackend.Utility, true, Vector3.zero);
        Check(a1.BuildObservation().Noise == a2.BuildObservation().Noise, "the same id gives the same sequence, so a run is reproducible");

        ResetWorld();
        ManagedNpcBrain enemy = Npc("e", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        enemy.Trustworthiness = 0.9f; enemy.BetrayalOpportunity = true;
        o = enemy.BuildObservation();
        Check(o.Trustworthiness == 0f && o.Affinity == 0f && o.PaySatisfaction == 0f && o.Noise == 0 && o.BetrayalOpportunity == 0, "other roles send zeros for every companion input");
    }

    static void Betrayal()
    {
        ResetWorld();
        GameObject player = Player(12f, true);
        Fighter c = Companion("traitor", Vector3.zero, player.transform, 10);
        c.Brain.Trustworthiness = 0f; c.Brain.Affinity = 0f; c.Brain.PaySatisfaction = 0f;
        int betrayed = 0; c.Brain.Betrayed += b => betrayed++;
        Advance(0.5f);
        Check(c.Brain.Action == NpcAction.Follow && betrayed == 0, "no opportunity, no betrayal, however disloyal");
        c.Brain.BetrayalOpportunity = true;
        bool done = false;
        for (int i = 0; i < 400 && !done; i++) { Advance(0.1f); done = betrayed > 0; }
        Check(betrayed == 1, "a disloyal companion with an opportunity betrays within a few decisions, " + betrayed);
        Check(!c.Brain.BetrayalOpportunity, "the opportunity is used up");
        Check(c.Brain.Action == NpcAction.Betray && c.Actor.Mode == NpcAction.Betray && c.Agent.isStopped, "it stops and the game takes over");
        Advance(1f);
        Check(betrayed == 1 && c.Brain.Action == NpcAction.Follow, "one opportunity, one betrayal, then it follows again, " + betrayed);

        ResetWorld();
        GameObject p2 = Player(12f, true);
        Fighter loyal = Companion("loyal", Vector3.zero, p2.transform, 10);
        loyal.Brain.Trustworthiness = 1f; loyal.Brain.Affinity = 1f; loyal.Brain.PaySatisfaction = 1f; loyal.Brain.BetrayalOpportunity = true;
        int never = 0; loyal.Brain.Betrayed += b => never++;
        Advance(40f);
        Check(never == 0 && loyal.Brain.BetrayalOpportunity, "a loyal companion never betrays, and keeps the opportunity");
        Check(System.Math.Abs(loyal.Brain.Loyalty - 1f) < 1e-5f, "full loyalty is 1");

        ResetWorld();
        GameObject p3 = Player(12f, true);
        Fighter enemy = Goblin(Vector3.zero);
        enemy.Brain.BetrayalOpportunity = true; enemy.Brain.Trustworthiness = 0f;
        int e = 0; enemy.Brain.Betrayed += b => e++;
        Advance(20f);
        Check(e == 0, "only companions betray");

        // Betrayal beats a refusal and an order.
        ResetWorld();
        GameObject p4 = Player(12f, true);
        Fighter busy = Companion("busy", Vector3.zero, p4.transform, 1);
        busy.Brain.Trustworthiness = 0f; busy.Brain.Affinity = 0f; busy.Brain.PaySatisfaction = 0f;
        int refusals = 0, betrayals = 0;
        busy.Brain.OrderRefused += (b, o, l) => refusals++; busy.Brain.Betrayed += b => betrayals++;
        busy.Brain.BetrayalOpportunity = true;
        busy.Brain.SetOrder(NpcOrder.Raid, 90);
        for (int i = 0; i < 400 && betrayals == 0 && refusals == 0; i++) Advance(0.1f);
        Check(betrayals + refusals >= 1, "something happened");
        Check(betrayals == 1 || refusals == 1, "either the roll came first or the refusal did");
    }

    static void OrderTypes()
    {
        ResetWorld();
        GameObject player = Player(12f, true);
        Fighter c = Companion("c", Vector3.zero, player.transform, 10);
        c.Brain.Trustworthiness = 1f; c.Brain.Affinity = 1f; c.Brain.PaySatisfaction = 1f;
        List<string> refused = new List<string>();
        c.Brain.OrderRefused += (b, o, lvl) => refused.Add(o + ":" + lvl);

        c.Brain.SetOrder(NpcOrder.Deliver, 16);
        Advance(0.4f);
        Check(refused.Count == 0 && c.Brain.Action == NpcAction.Mission && c.Actor.Mode == NpcAction.Mission, "a delivery 6 levels above is accepted and becomes a mission, got " + c.Brain.Action);
        Check(c.Agent.isStopped, "the actor stands still while game code runs the mission");
        c.Brain.SetOrder(NpcOrder.Raid, 12);
        Advance(0.4f);
        Check(refused.Count == 1 && refused[0] == "Raid:12", "a raid 2 levels above is refused: " + string.Join(",", refused));
        c.Brain.SetOrder(NpcOrder.Raid, 11);
        Advance(0.4f);
        Check(refused.Count == 1 && c.Brain.Action == NpcAction.Mission, "a raid 1 level above is accepted");
        c.Brain.SetOrder(NpcOrder.Deliver, 17);
        Advance(0.4f);
        Check(refused.Count == 2 && refused[1] == "Deliver:17", "a delivery 7 levels above is refused");
    }

    static void Missions()
    {
        ResetWorld();
        NpcMissionOutcome outcome;
        Check(!ManagedMissionResolver.TryResolve(NpcOrder.Attack, 10, 10, 0, out outcome), "an attack order is not a mission");
        Check(!ManagedMissionResolver.TryResolve(NpcOrder.None, 10, 10, 0, out outcome), "no order is not a mission");
        Check(!ManagedMissionResolver.TryResolve(NpcOrder.Raid, -1, 10, 0, out outcome) && !ManagedMissionResolver.TryResolve(NpcOrder.Raid, 10, -1, 0, out outcome), "negative levels are rejected");
        Check(!ManagedMissionResolver.TryResolve(NpcOrder.Raid, 65536, 10, 0, out outcome) && !ManagedMissionResolver.TryResolve(NpcOrder.Raid, 10, 65536, 0, out outcome), "levels above 65535 are rejected");
        Check(ManagedMissionResolver.TryResolve(NpcOrder.Raid, 10, 10, 0, out outcome) && outcome == NpcMissionOutcome.Success, "the lowest roll succeeds");
        Check(ManagedMissionResolver.TryResolve(NpcOrder.Raid, 10, 10, uint.MaxValue, out outcome) && outcome == NpcMissionOutcome.Killed, "the highest roll on a raid kills");
        Check(ManagedMissionResolver.TryResolve(NpcOrder.Deliver, 10, 10, uint.MaxValue, out outcome) && outcome == NpcMissionOutcome.Killed, "and on a delivery too");

        // Statistics: a stronger companion does better, a raid is deadlier than a delivery.
        long[] raidWeak = new long[4], raidStrong = new long[4], delivery = new long[4], raid = new long[4];
        for (uint i = 0; i < 4096; i++)
        {
            uint noise = (i << 20) | (i & 0xFFu);
            ManagedMissionResolver.TryResolve(NpcOrder.Raid, 5, 20, noise, out outcome); raidWeak[(int)outcome]++;
            ManagedMissionResolver.TryResolve(NpcOrder.Raid, 35, 20, noise, out outcome); raidStrong[(int)outcome]++;
            ManagedMissionResolver.TryResolve(NpcOrder.Raid, 20, 20, noise, out outcome); raid[(int)outcome]++;
            ManagedMissionResolver.TryResolve(NpcOrder.Deliver, 20, 20, noise, out outcome); delivery[(int)outcome]++;
        }
        Check(raidWeak[0] < raid[0] && raid[0] < raidStrong[0], "success rises with the companion's level: " + raidWeak[0] + " " + raid[0] + " " + raidStrong[0]);
        Check(delivery[0] > raid[0], "a delivery is safer than a raid");
        Check(raid[3] > delivery[3], "and a raid kills more often");

        // A brain resolves with its own level and its own random numbers.
        ResetWorld();
        ManagedNpcBrain veteran = Npc("v", NpcRole.Companion, DecisionBackend.Utility, true, Vector3.zero);
        veteran.Level = 60;
        ManagedNpcBrain rookie = Npc("r", NpcRole.Companion, DecisionBackend.Utility, true, Vector3.zero);
        rookie.Level = 1;
        int veteranWins = 0, rookieWins = 0;
        for (int i = 0; i < 400; i++)
        {
            NpcMissionOutcome a, b;
            Check(veteran.TryResolveMission(NpcOrder.Raid, 30, out a) && rookie.TryResolveMission(NpcOrder.Raid, 30, out b), "a brain resolves a raid");
            if (veteran.TryResolveMission(NpcOrder.Raid, 30, out a) && a == NpcMissionOutcome.Success) veteranWins++;
            if (rookie.TryResolveMission(NpcOrder.Raid, 30, out b) && b == NpcMissionOutcome.Success) rookieWins++;
        }
        Check(veteranWins > rookieWins + 100, "a level 60 companion wins far more often than a level 1 one: " + veteranWins + " vs " + rookieWins);
        NpcMissionOutcome none;
        Check(!veteran.TryResolveMission(NpcOrder.Follow, 10, out none), "a brain cannot resolve an order that is not a mission");
    }

    static void DebugViewV4()
    {
        ResetWorld();
        GameObject player = Player(12f, true);
        Fighter c = Companion("comp", Vector3.zero, player.transform, 10);
        c.Brain.Trustworthiness = 1f; c.Brain.Affinity = 1f; c.Brain.PaySatisfaction = 1f;
        Advance(0.3f);
        Check(ManagedNpcDebugView.Describe(c.Brain).Contains("loyalty 1.00") && !ManagedNpcDebugView.Describe(c.Brain).Contains("betrayal possible"), "a companion line shows its loyalty");
        c.Brain.BetrayalOpportunity = true;
        Check(ManagedNpcDebugView.Describe(c.Brain).Contains("betrayal possible"), "and whether it could betray");
    }

    internal static int Run(string expectPath)
    {
        MathTests(); HealthRules(); Basic(); CheapRejects(); Budget(); DeadAndSelf(); DirectorRules();
        ThreatMemory(); ActorPatrol(); ActorAttack(); ActorLostTarget(); ActorRetreat(); ActorIdleTrade(); ActorNavMeshAndDeath(); SpellDamage();
        DebugView(); DebugFilter(); DebugRoute(); DebugDraw(); DebugWindow();
        ObservationV3(); Provocation(); CompanionFollow(); CompanionOrders(); DebugViewV3();
        ObservationV4(); Betrayal(); OrderTypes(); Missions(); DebugViewV4();
        Console.WriteLine("decision path in director batches: " + (sawNative && sawFallback ? "mixed" : sawNative ? "native" : sawFallback ? "managed fallback" : "none"));
        if (expectPath == "native") Check(sawNative && !sawFallback, "every batch used the native library");
        else if (expectPath == "fallback") Check(sawFallback && !sawNative, "every batch used the managed fallback");
        Console.WriteLine(failures == 0 ? "MANAGED NPC TESTS PASS (" + checks + " checks)" : "MANAGED NPC TESTS FAIL (" + failures + " of " + checks + ")");
        return failures == 0 ? 0 : 1;
    }
}
