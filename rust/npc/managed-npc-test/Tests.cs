// CI harness for the Managed NPC code (Managed/Health and Managed/NPC). It compiles those files with plain .NET
// against the hand written UnityEngine stand-ins in UnityStubs.cs and runs the checks below. The stub raycast is a
// callback, so these checks cover the logic around the ray (cone, budget, lingering, dead and self sources,
// rotation, events), not real colliders or layers. Unity itself is never involved.
// Usage: dotnet run -- [native|fallback]   (the argument states which decision path the run must have used)
using System;
using System.Collections.Generic;
using System.Reflection;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;

internal static class Tests
{
    static int failures, checks;
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
        Time.time = 0; Debug.Logs.Clear(); Debug.Warnings.Clear(); Physics.RayCount = 0; Physics.OnRaycast = null;
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
    static void Tick(float dt) { Time.time += dt; Update(Director()); }

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

    internal static int Run(string expectPath)
    {
        MathTests(); HealthRules(); Basic(); CheapRejects(); Budget(); DeadAndSelf(); DirectorRules();
        bool native = Director() != null && Director().LastUsedNative;
        Console.WriteLine("decision path in last batch: " + (native ? "native" : "managed fallback"));
        if (expectPath == "native") Check(native, "the native library was expected and used");
        else if (expectPath == "fallback") Check(!native, "the managed fallback was expected and used");
        Console.WriteLine(failures == 0 ? "MANAGED NPC TESTS PASS (" + checks + " checks)" : "MANAGED NPC TESTS FAIL (" + failures + " of " + checks + ")");
        return failures == 0 ? 0 : 1;
    }
}
