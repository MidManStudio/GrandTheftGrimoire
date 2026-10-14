// Checks for Crown Heat: the regions, which acts count as crimes and when a guard has seen them, the tiers, the
// fade, what a guard sends to the decision, and what a guard then does. Part of the Managed NPC harness, same Unity
// stand-ins and the same limits as the other checks. A guard sees through the stub raycast, so these checks prove
// the rules around sight and not real colliders.
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Crown;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;

internal static partial class Tests
{
    // A region centred on (x, 0, z) with a square footprint of the given side.
    static ManagedCrownRegion Region(string name, float x, float z, float side)
    {
        GameObject g = new GameObject(name);
        g.transform.position = new Vector3(x, 0f, z);
        ManagedCrownRegion r = g.AddComponent<ManagedCrownRegion>();
        Set(r, "_displayName", name);
        Set(r, "_size", new Vector3(side, 40f, side));
        return r;
    }

    // A guard that sees all around, so it never has to face the player.
    static ManagedNpcBrain GuardAt(Vector3 pos, NpcDisposition disposition = NpcDisposition.Retaliatory)
    {
        ManagedNpcBrain b = Npc("guard", NpcRole.Guard, DecisionBackend.StateMachine, true, pos, true);
        Set(b, "_disposition", disposition);
        Set(b, "_fieldOfViewDegrees", 360f);
        return b;
    }

    static ManagedNpcBrain Villager(NpcRole role, Vector3 pos)
    {
        return Npc(role.ToString().ToLower(), role, DecisionBackend.StateMachine, true, pos, true);
    }

    static float HeatOf(ManagedCrownRegion r) { return r.Heat; }

    // ---------- where the law holds ----------

    static void CrownRegionArea()
    {
        ResetWorld();
        ManagedCrownRegion a = Region("Hollow", 0f, 0f, 20f);
        Check(a.Contains(new Vector3(9.9f, 0f, -9.9f)) && a.Contains(new Vector3(10f, 0f, 10f)), "a point inside the box and one on its edge are in the region");
        Check(!a.Contains(new Vector3(10.01f, 0f, 0f)) && !a.Contains(new Vector3(0f, 0f, -10.01f)), "a point just outside is not");
        Check(a.Contains(new Vector3(0f, 20f, 0f)) && !a.Contains(new Vector3(0f, 20.01f, 0f)), "the box has height as well");
        Check(ManagedCrownRegion.Count == 1 && ManagedCrownRegion.Get(0) == a, "an enabled region registers");
        Check(ManagedCrownRegion.Find(new Vector3(1f, 0f, 1f)) == a && ManagedCrownRegion.Find(new Vector3(50f, 0f, 0f)) == null, "Find returns the region around a point, and null in the wilds");
        Check(ManagedCrownRegion.TierAt(new Vector3(50f, 0f, 0f)) == 0, "outside every region the tier is 0");

        // Where boxes overlap, the first one registered wins.
        ManagedCrownRegion b = Region("Overlap", 5f, 0f, 20f);
        a.Add(50f); b.Add(20f);
        Check(ManagedCrownRegion.Find(new Vector3(7f, 0f, 0f)) == a && ManagedCrownRegion.TierAt(new Vector3(7f, 0f, 0f)) == 2, "overlapping regions: the first registered wins");
        Check(ManagedCrownRegion.TierAt(new Vector3(14f, 0f, 0f)) == 1, "and a point only the second holds gets the second's tier");

        // A disabled region stops holding the law.
        a.enabled = false;
        Check(ManagedCrownRegion.Count == 1 && ManagedCrownRegion.Find(new Vector3(0f, 0f, 0f)) == b, "a disabled region is gone from the list");
        a.enabled = true;
        Check(ManagedCrownRegion.Count == 2, "and back when enabled again");
        a.enabled = true;
        Check(ManagedCrownRegion.Count == 2, "enabling twice registers once");

        // The Inspector values are kept in order.
        Set(a, "_wantedAt", 500f); Set(a, "_huntedAt", 1f); Set(a, "_outlawAt", 2f); Set(a, "_maxHeat", 100f);
        UnityEngine.Component.Invoke(a, "OnValidate");
        Check((float)GetF(a, "_wantedAt") == 100f && (float)GetF(a, "_huntedAt") == 100f && (float)GetF(a, "_outlawAt") == 100f, "thresholds are kept in order and under the maximum");
        Set(a, "_wantedAt", 0f);
        UnityEngine.Component.Invoke(a, "OnValidate");
        Check((float)GetF(a, "_wantedAt") >= 0.01f, "the wanted threshold is never zero, so a clear region is never wanted");
        Check(a.Describe() == "Hollow  heat 50.0/100  hunted (tier 2)", "the debugger line, got '" + a.Describe() + "'");
    }

    static object GetF(object o, string field)
    {
        System.Reflection.FieldInfo f = o.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return f.GetValue(o);
    }

    // ---------- crimes ----------

    static void CrownCrimes()
    {
        // A guard sees the player hurt a civilian. The first blow is an assault, 5 heat.
        ResetWorld();
        ManagedCrownRegion town = Region("Town", 0f, 0f, 100f);
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcBrain guard = GuardAt(new Vector3(0f, 0f, 0f));
        ManagedNpcBrain civ = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f));
        List<string> tiers = new List<string>(), crimes = new List<string>();
        town.TierChanged += (r, o, n) => tiers.Add(o + ">" + n);
        town.CrimeReported += (r, k, w) => crimes.Add(k + (w ? ":seen" : ":unseen"));
        Tick(0.1f);
        Check(guard.ThreatVisible && guard.Threat == player.GetComponent<ManagedNpcThreatSource>(), "the guard sees the player");
        civ.GetComponent<ManagedHealth>().TakeDamage(1f, player);
        Check(HeatOf(town) == 5f && town.Tier == 0, "an assault seen by a guard adds 5 heat, still tier 0");
        Check(crimes.Count == 1 && crimes[0] == "Assault:seen", "and is reported as an assault that was seen, got " + string.Join(",", crimes));

        // Hitting the same victim again at once is the same assault. After the repeat time it is a new one.
        civ.GetComponent<ManagedHealth>().TakeDamage(1f, player);
        Check(HeatOf(town) == 5f && crimes.Count == 1, "a second blow on the same victim inside three seconds adds nothing");
        Tick(3.1f);
        civ.GetComponent<ManagedHealth>().TakeDamage(1f, player);
        Check(HeatOf(town) == 10f && town.Tier == 1 && tiers.Count == 1 && tiers[0] == "0>1", "after the repeat time it counts again, and 10 heat is tier 1, announced once");

        // A kill is a murder, whatever came before.
        civ.GetComponent<ManagedHealth>().TakeDamage(1000f, player);
        Check(HeatOf(town) == 35f && town.Tier == 1 && crimes[crimes.Count - 1] == "Murder:seen", "the kill adds 25 and is a murder, got " + HeatOf(town));

        // Each victim counts for itself, so collateral damage adds up. Tiers rise at 40 and 75 and heat stops at 100.
        ManagedNpcBrain m1 = Villager(NpcRole.Merchant, new Vector3(-5f, 0f, 5f)), m2 = Villager(NpcRole.Merchant, new Vector3(-6f, 0f, 5f)), m3 = Villager(NpcRole.Merchant, new Vector3(-7f, 0f, 5f)), m4 = Villager(NpcRole.Civilian, new Vector3(-8f, 0f, 5f));
        m1.GetComponent<ManagedHealth>().TakeDamage(1000f, player);
        Check(HeatOf(town) == 60f && town.Tier == 2, "a second murder: 60 heat, hunted");
        m2.GetComponent<ManagedHealth>().TakeDamage(1000f, player);
        Check(HeatOf(town) == 85f && town.Tier == 3, "a third: 85 heat, outlaw");
        m3.GetComponent<ManagedHealth>().TakeDamage(1000f, player);
        Check(HeatOf(town) == 100f, "heat stops at the maximum, got " + HeatOf(town));
        Check(string.Join(",", tiers) == "0>1,1>2,2>3", "the tier changes were announced in order, got " + string.Join(",", tiers));

        // Nobody looking: the crime is reported as unseen and adds nothing.
        ResetWorld();
        ManagedCrownRegion t2 = Region("Town", 0f, 0f, 100f);
        GameObject p2 = Player(10f, true); Wall();
        ManagedNpcBrain g2 = GuardAt(Vector3.zero);
        ManagedNpcBrain c2 = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f));
        List<string> seen2 = new List<string>();
        t2.CrimeReported += (r, k, w) => seen2.Add(k + (w ? ":seen" : ":unseen"));
        Tick(0.1f);
        Check(!g2.ThreatVisible, "the guard has no line of sight");
        c2.GetComponent<ManagedHealth>().TakeDamage(1000f, p2);
        Check(HeatOf(t2) == 0f && seen2.Count == 1 && seen2[0] == "Murder:unseen", "a murder nobody saw adds no heat");

        // A guard that cannot witness: dead, or outside the region, or no guard at all.
        ResetWorld();
        ManagedCrownRegion t3 = Region("Town", 0f, 0f, 100f);
        GameObject p3 = Player(10f, true); Clear(p3);
        ManagedNpcBrain deadGuard = GuardAt(Vector3.zero);
        ManagedNpcBrain farGuard = GuardAt(new Vector3(60f, 0f, 10f));
        ManagedNpcBrain v3 = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f));
        Set(farGuard, "_viewRange", 100f);
        Tick(0.1f);
        Check(deadGuard.ThreatVisible && farGuard.ThreatVisible, "both guards see the player");
        deadGuard.GetComponent<ManagedHealth>().TakeDamage(1000f);
        v3.GetComponent<ManagedHealth>().TakeDamage(1000f, p3);
        Check(HeatOf(t3) == 0f, "a dead guard and a guard outside the region witness nothing, got " + HeatOf(t3));

        // Who is a crime to hurt. Enemies, bosses and companions are not, a guard is, and so is a merchant.
        ResetWorld();
        ManagedCrownRegion t4 = Region("Town", 0f, 0f, 100f);
        GameObject p4 = Player(10f, true); Clear(p4);
        ManagedNpcBrain g4 = GuardAt(Vector3.zero);
        Tick(0.1f);
        foreach (NpcRole role in new[] { NpcRole.Enemy, NpcRole.Boss, NpcRole.Companion })
        {
            ManagedNpcBrain victim = Villager(role, new Vector3(5f, 0f, 5f));
            Set(victim, "_leader", null);
            victim.GetComponent<ManagedHealth>().TakeDamage(1000f, p4);
        }
        Check(HeatOf(t4) == 0f, "killing an enemy, a boss or a companion is no crime, got " + HeatOf(t4));
        Villager(NpcRole.Merchant, new Vector3(5f, 0f, 5f)).GetComponent<ManagedHealth>().TakeDamage(1f, p4);
        Check(HeatOf(t4) == 5f, "hurting a merchant is an assault");
        ManagedNpcBrain victimGuard = GuardAt(new Vector3(8f, 0f, 8f));
        Tick(0.1f);
        victimGuard.GetComponent<ManagedHealth>().TakeDamage(1f, p4);
        Check(HeatOf(t4) == 20f, "hurting a guard adds 15, got " + HeatOf(t4));
        victimGuard.GetComponent<ManagedHealth>().TakeDamage(1000f, p4);
        Check(HeatOf(t4) == 60f, "killing a guard adds 40, got " + HeatOf(t4));

        // Only the player's side can commit a crime: a hit with no source, from a goblin, or from the NPC itself, is not one.
        ResetWorld();
        ManagedCrownRegion t5 = Region("Town", 0f, 0f, 100f);
        GameObject p5 = Player(10f, true); Clear(p5);
        GuardAt(Vector3.zero);
        ManagedNpcBrain v5 = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f));
        ManagedNpcBrain goblin = Villager(NpcRole.Enemy, new Vector3(6f, 0f, 5f));
        Tick(0.1f);
        v5.GetComponent<ManagedHealth>().TakeDamage(1f);
        v5.GetComponent<ManagedHealth>().TakeDamage(1f, goblin.gameObject);
        v5.GetComponent<ManagedHealth>().TakeDamage(1f, v5.gameObject);
        Check(HeatOf(t5) == 0f, "no source, a goblin and itself commit no crime, got " + HeatOf(t5));
        // A source that is a child of the player still counts as the player.
        GameObject spell = new GameObject("fireball"); spell.transform.parent = p5.transform;
        v5.GetComponent<ManagedHealth>().TakeDamage(1f, spell);
        Check(HeatOf(t5) == 5f, "a hit from something that belongs to the player is the player's");

        // Outside every region the law does not reach.
        ResetWorld();
        ManagedCrownRegion t6 = Region("Town", 0f, 0f, 20f);
        GameObject p6 = Player(10f, true); Clear(p6);
        GuardAt(Vector3.zero);
        ManagedNpcBrain wild = Villager(NpcRole.Civilian, new Vector3(30f, 0f, 5f));
        Tick(0.1f);
        wild.GetComponent<ManagedHealth>().TakeDamage(1000f, p6);
        Check(HeatOf(t6) == 0f, "a murder in the wilds earns no heat in a town elsewhere");

        // With no region in the scene at all nothing happens and nothing breaks.
        ResetWorld();
        GameObject p7 = Player(10f, true); Clear(p7);
        GuardAt(Vector3.zero);
        ManagedNpcBrain v7 = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f));
        Tick(0.1f);
        v7.GetComponent<ManagedHealth>().TakeDamage(1000f, p7);
        Check(ManagedCrownRegion.Count == 0 && ManagedCrownRegion.TierAt(v7.transform.position) == 0, "with no regions every tier is 0");

        // A guard that sees one player does not witness what another player does out of its sight.
        ResetWorld();
        ManagedCrownRegion t9 = Region("Town", 0f, 0f, 100f);
        GameObject pa = Player(10f, true), pb = Player(12f, true);
        ClearAll(pa);
        ManagedNpcBrain g9 = GuardAt(Vector3.zero);
        ManagedNpcBrain v9 = Villager(NpcRole.Civilian, new Vector3(5f, 0f, 5f)), w9 = Villager(NpcRole.Civilian, new Vector3(6f, 0f, 5f));
        Tick(0.1f);
        Check(g9.Threat == pa.GetComponent<ManagedNpcThreatSource>(), "the guard sees the first player and not the second");
        v9.GetComponent<ManagedHealth>().TakeDamage(1000f, pb);
        Check(HeatOf(t9) == 0f, "what the second player did out of its sight earns no heat, got " + HeatOf(t9));
        w9.GetComponent<ManagedHealth>().TakeDamage(1000f, pa);
        Check(HeatOf(t9) == 25f, "what the first did in its sight does, got " + HeatOf(t9));

        // The direct API for other systems, such as a bribe or an NPC that causes a stir.
        ResetWorld();
        ManagedCrownRegion t8 = Region("Town", 0f, 0f, 100f);
        List<string> tiers8 = new List<string>();
        t8.TierChanged += (r, o, n) => tiers8.Add(o + ">" + n);
        t8.Add(-5f); t8.Add(float.NaN); t8.Add(0f); t8.Reduce(-1f); t8.Reduce(float.NaN);
        Check(HeatOf(t8) == 0f && tiers8.Count == 0, "bad or zero amounts change nothing");
        t8.Add(45f);
        Check(t8.Tier == 2 && string.Join(",", tiers8) == "0>2", "adding heat can jump a tier, and says so once");
        t8.Reduce(10f);
        Check(HeatOf(t8) == 35f && t8.Tier == 1 && tiers8[tiers8.Count - 1] == "2>1", "reducing lowers the tier");
        t8.Clear();
        Check(HeatOf(t8) == 0f && t8.Tier == 0 && tiers8[tiers8.Count - 1] == "1>0", "clearing wipes it, as a bribe does");
        t8.Clear();
        Check(tiers8.Count == 3, "clearing a clear region announces nothing");
        Check(!t8.Report(ManagedCrimeKind.Murder, null) && HeatOf(t8) == 0f, "a crime with no offender is never seen");
    }

    // ---------- the tiers ----------

    static int TierAfter(ManagedCrownRegion r, float heat)
    {
        r.Clear();
        r.Add(heat);
        return r.Tier;
    }

    static void CrownTiers()
    {
        ResetWorld();
        ManagedCrownRegion r = Region("Town", 0f, 0f, 100f);
        Check(TierAfter(r, 9.99f) == 0 && TierAfter(r, 10f) == 1, "the wanted tier starts at exactly 10");
        Check(TierAfter(r, 39.99f) == 1 && TierAfter(r, 40f) == 2, "the hunted tier starts at exactly 40");
        Check(TierAfter(r, 74.99f) == 2 && TierAfter(r, 75f) == 3, "the outlaw tier starts at exactly 75");
        Check(TierAfter(r, 100f) == 3 && TierAfter(r, 500f) == 3 && HeatOf(r) == 100f, "heat above the maximum is held at the maximum");
        Check(ManagedCrownTier.Name(0) == "clear" && ManagedCrownTier.Name(1) == "wanted" && ManagedCrownTier.Name(2) == "hunted" && ManagedCrownTier.Name(3) == "outlaw", "the tier names");
        Check(ManagedCrownTier.Max == 3, "the highest tier is the one the native decision accepts");

        // Each region keeps its own numbers.
        Set(r, "_wantedAt", 50f); Set(r, "_huntedAt", 60f); Set(r, "_outlawAt", 70f);
        UnityEngine.Component.Invoke(r, "OnValidate");
        Check(TierAfter(r, 49f) == 0 && TierAfter(r, 50f) == 1 && TierAfter(r, 60f) == 2 && TierAfter(r, 70f) == 3, "a stricter region has its own thresholds");
        Set(r, "_murderHeat", 7f);
        ManagedNpcBrain guard = GuardAt(Vector3.zero);
        GameObject player = Player(10f, true); Clear(player);
        Tick(0.1f);
        r.Clear();
        Check(r.Report(ManagedCrimeKind.Murder, player.GetComponent<ManagedNpcThreatSource>()) && HeatOf(r) == 7f, "and its own heat for a crime");
    }

    // ---------- heat fades ----------

    static void CrownHeatFade()
    {
        // Unseen for twenty seconds, then half a point a second.
        ResetWorld();
        ManagedCrownRegion town = Region("Town", 0f, 0f, 100f);
        GameObject player = Player(10f, true); Wall();
        GuardAt(Vector3.zero);
        town.Add(30f);
        Advance(19f);
        Check(HeatOf(town) == 30f, "nothing fades during the first twenty seconds, got " + HeatOf(town));
        Advance(6f);
        Check(System.Math.Abs(HeatOf(town) - 27.5f) < 0.2f, "after five more seconds about 2.5 is gone, got " + HeatOf(town));

        // The tier falls as the heat does, once.
        ResetWorld();
        ManagedCrownRegion t2 = Region("Town", 0f, 0f, 100f);
        Player(10f, true); Wall();
        GuardAt(Vector3.zero);
        List<string> tiers = new List<string>();
        t2.TierChanged += (r, o, n) => tiers.Add(o + ">" + n);
        t2.Add(11f);
        Check(t2.Tier == 1, "11 heat is tier 1");
        Advance(21f);
        Check(t2.Tier == 1 && HeatOf(t2) >= 10f, "still wanted while 10 or more is left, got " + HeatOf(t2));
        Advance(2f);
        Check(t2.Tier == 0 && string.Join(",", tiers) == "0>1,1>0", "below 10 the tier drops to clear, got " + string.Join(",", tiers));
        Advance(60f);
        Check(HeatOf(t2) == 0f, "heat never goes below zero, got " + HeatOf(t2));
        Check(tiers.Count == 2, "and nothing more is announced");

        // A guard that keeps the player in view keeps the heat up.
        ResetWorld();
        ManagedCrownRegion t3 = Region("Town", 0f, 0f, 100f);
        GameObject p3 = Player(10f, true); Clear(p3);
        GuardAt(Vector3.zero);
        t3.Add(30f);
        Advance(60f);
        Check(HeatOf(t3) == 30f, "a guard that sees the player means the heat does not fade, got " + HeatOf(t3));
        Wall();
        Advance(0.7f);
        Advance(19f);
        Check(HeatOf(t3) == 30f, "the wait of twenty seconds starts when the guard lost sight, got " + HeatOf(t3));
        Advance(6f);
        Check(HeatOf(t3) < 29f, "and then it fades, got " + HeatOf(t3));

        // A guard outside the region, or a dead one, does not hold it up.
        ResetWorld();
        ManagedCrownRegion t4 = Region("Town", 0f, 0f, 20f);
        GameObject p4 = Player(10f, true); Clear(p4);
        ManagedNpcBrain far = GuardAt(new Vector3(30f, 0f, 10f));
        Set(far, "_viewRange", 100f);
        t4.Add(30f);
        Advance(30f);
        Check(HeatOf(t4) < 30f, "a guard outside the region does not stop the fade, got " + HeatOf(t4));

        // More heat restarts the wait.
        ResetWorld();
        ManagedCrownRegion t5 = Region("Town", 0f, 0f, 100f);
        Player(10f, true); Wall();
        GuardAt(Vector3.zero);
        t5.Add(30f);
        Advance(15f);
        t5.Add(1f);
        Advance(19f);
        Check(HeatOf(t5) == 31f, "heat added at 15 s holds the fade until 35 s, got " + HeatOf(t5));
    }

    // ---------- what a guard sends to the decision ----------

    static void CrownObservation()
    {
        ResetWorld();
        ManagedCrownRegion a = Region("A", 0f, 0f, 50f), b = Region("B", 100f, 0f, 50f);
        a.Add(20f); b.Add(80f);
        ManagedNpcBrain inA = GuardAt(new Vector3(0f, 0f, 0f)), inB = GuardAt(new Vector3(100f, 0f, 0f)), wild = GuardAt(new Vector3(500f, 0f, 0f));
        Check(inA.BuildObservation().CrownHeat == 1, "a guard in region A sends A's tier, 1");
        Check(inB.BuildObservation().CrownHeat == 3, "a guard in region B sends B's tier, 3");
        Check(wild.BuildObservation().CrownHeat == 0, "a guard outside every region sends 0");
        foreach (NpcRole role in new[] { NpcRole.Merchant, NpcRole.Civilian, NpcRole.Enemy, NpcRole.Boss, NpcRole.Companion })
        {
            ManagedNpcBrain other = Villager(role, new Vector3(100f, 0f, 0f));
            Check(other.BuildObservation().CrownHeat == 0, role + " in a hot region still sends 0");
        }

        // The tier follows the heat and the guard's position.
        b.Clear();
        Check(inB.BuildObservation().CrownHeat == 0, "after a bribe the guard sends 0");
        inB.transform.position = new Vector3(0f, 0f, 0f);
        Check(inB.BuildObservation().CrownHeat == 1, "and walking into region A it sends A's tier");

        // The observation fields that came before are untouched.
        NPCNativeObservationCheck(inA.BuildObservation());
    }

    static void NPCNativeObservationCheck(MidManStudio.Gtg.NPC.Native.NPCNativeObservation o)
    {
        Check(o.Role == (uint)NpcRole.Guard && o.ReservedA == 0 && o.ReservedB == 0 && o.ReservedC == 0 && o.Reserved == 0, "the reserved fields are still zero");
    }

    // ---------- what a guard does about it ----------

    static void CrownGuards()
    {
        // A retaliatory guard ignores the player until the region is hot, fights while it is, and stops when it is wiped.
        ResetWorld();
        ManagedCrownRegion town = Region("Town", 0f, 0f, 100f);
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcBrain guard = GuardAt(Vector3.zero);
        Tick(0.1f); Tick(0.1f);
        Check(guard.ThreatVisible && guard.Action == NpcAction.Patrol, "with no heat the guard sees the player and carries on, got " + guard.Action);
        town.Add(10f);
        Tick(0.1f);
        Check(guard.Action == NpcAction.Attack, "at tier 1 the guard attacks on sight, got " + guard.Action);
        town.Clear();
        Tick(0.1f);
        Check(guard.Action == NpcAction.Patrol, "once the heat is wiped it stops, got " + guard.Action);

        // The same rule through a real crime in view of the guard.
        ResetWorld();
        ManagedCrownRegion t2 = Region("Town", 0f, 0f, 100f);
        GameObject p2 = Player(10f, true); Clear(p2);
        ManagedNpcBrain g2 = GuardAt(Vector3.zero);
        ManagedNpcBrain victim = Villager(NpcRole.Merchant, new Vector3(4f, 0f, 4f));
        Tick(0.1f);
        Check(g2.Action == NpcAction.Patrol, "calm before the crime");
        victim.GetComponent<ManagedHealth>().TakeDamage(1000f, p2);
        Tick(0.1f);
        Check(t2.Tier == 1 && g2.Action == NpcAction.Attack, "the guard that saw the murder attacks the player, tier " + t2.Tier + ", " + g2.Action);

        // And through a crime nobody saw: the guard does not know and does not act.
        ResetWorld();
        ManagedCrownRegion t3 = Region("Town", 0f, 0f, 100f);
        GameObject p3 = Player(10f, true); Wall();
        ManagedNpcBrain g3 = GuardAt(Vector3.zero);
        ManagedNpcBrain v3 = Villager(NpcRole.Merchant, new Vector3(4f, 0f, 4f));
        Tick(0.7f);
        v3.GetComponent<ManagedHealth>().TakeDamage(1000f, p3);
        Clear(p3);
        Tick(0.1f);
        Check(t3.Tier == 0 && g3.Action == NpcAction.Patrol, "a murder out of sight earns no heat, so the guard that now sees the player does nothing, " + g3.Action);

        // Other dispositions and roles.
        ResetWorld();
        ManagedCrownRegion t4 = Region("Town", 0f, 0f, 100f);
        GameObject p4 = Player(10f, true); Clear(p4);
        ManagedNpcBrain hostile = GuardAt(new Vector3(1f, 0f, 0f), NpcDisposition.Hostile);
        ManagedNpcBrain peaceful = GuardAt(new Vector3(-1f, 0f, 0f), NpcDisposition.Peaceful);
        ManagedNpcBrain bandit = Npc("bandit", NpcRole.Enemy, DecisionBackend.StateMachine, true, new Vector3(0f, 0f, 1f), true);
        Set(bandit, "_disposition", NpcDisposition.Retaliatory); Set(bandit, "_fieldOfViewDegrees", 360f);
        t4.Add(100f);
        Tick(0.1f); Tick(0.1f);
        Check(hostile.Action == NpcAction.Attack, "a hostile guard attacks, as it did before");
        Check(peaceful.Action == NpcAction.Retreat, "a peaceful guard still runs and never fights, even at tier 3");
        Check(bandit.Action == NpcAction.Patrol, "a retaliatory bandit does not read Crown Heat");
        Check(t4.Tier == 3, "the region is at tier 3");

        // The debugger.
        ResetWorld();
        ManagedCrownRegion t5 = Region("Town", 0f, 0f, 100f);
        ManagedNpcBrain g5 = GuardAt(Vector3.zero);
        ManagedNpcBrain c5 = Villager(NpcRole.Civilian, new Vector3(3f, 0f, 3f));
        Check(!ManagedNpcDebugView.Describe(g5).Contains("crown"), "a clear region adds nothing to the guard's line");
        t5.Add(45f);
        Check(ManagedNpcDebugView.Describe(g5).Contains("crown hunted"), "a hot region shows its tier on the guard's line, got '" + ManagedNpcDebugView.Describe(g5) + "'");
        Check(!ManagedNpcDebugView.Describe(c5).Contains("crown"), "and not on a civilian's");
    }
}
