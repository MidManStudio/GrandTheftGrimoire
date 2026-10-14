// Checks for the combat engagement: how attackers space out around a target, who gets to strike, how they circle,
// reposition and give way, how a target is chosen, and when a swing misses. Part of the Managed NPC harness, same
// Unity stand-ins and the same limits (a straight-line agent, a callback raycast). The agent in the stub does not
// collide with other agents, so these checks prove where attackers are sent and when they strike, and not how a real
// NavMesh crowd squeezes past each other.
using System.Collections.Generic;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using UnityEngine;
using UnityEngine.AI;

internal static partial class Tests
{
    const float Tau = 6.28318531f;

    static float WrapRad(float d)
    {
        while (d > 3.14159265f) d -= Tau;
        while (d < -3.14159265f) d += Tau;
        return d;
    }
    static float AngGap(float a, float b) { return System.Math.Abs(WrapRad(a - b)); }
    static float Ang(Vector3 p, Vector3 around) { return (float)System.Math.Atan2(p.z - around.z, p.x - around.x); }

    // Every ray ends at the threat source whose visible point is nearest to the end of the ray, as in a clear room.
    static void ClearAll(params GameObject[] players)
    {
        Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) =>
        {
            Vector3 aim = o + d * (m - 0.05f);
            GameObject best = null; float bestSq = float.MaxValue;
            foreach (GameObject p in players)
            {
                ManagedNpcThreatSource s = p.GetComponent<ManagedNpcThreatSource>();
                for (int i = 0; i < s.PointCount; i++) { float sq = (s.GetPoint(i) - aim).sqrMagnitude; if (sq < bestSq) { bestSq = sq; best = p; } }
            }
            h = new RaycastHit { collider = best.GetComponent<Collider>() };
            return true;
        };
    }

    static Fighter AtBearing(float degrees, float radius)
    {
        float r = degrees * Mathf.Deg2Rad;
        return Goblin(new Vector3(Mathf.Cos(r) * radius, 0f, Mathf.Sin(r) * radius));
    }

    // 1.26 is 70 percent of the default attack range, 2.5 the default waiting distance, 1.2 the spacing, 1.5 the circling speed.
    static ManagedCombatEngagement.Member JoinAt(ManagedNpcThreatSource src, Fighter f, float now)
    {
        return src.Engagement.Join(f.Actor, f.Brain, now, 1.26f, 2.5f, 1.2f, 1.5f);
    }

    static List<float> SortedAngles(IList<ManagedCombatEngagement.Member> members)
    {
        List<float> angles = new List<float>();
        foreach (ManagedCombatEngagement.Member m in members)
        {
            float a = m.Angle % Tau; if (a < 0f) a += Tau;
            angles.Add(a);
        }
        angles.Sort();
        return angles;
    }

    static bool EvenlySpaced(List<float> sorted, float tolerance)
    {
        float step = Tau / sorted.Count;
        for (int i = 0; i < sorted.Count; i++)
        {
            float gap = i + 1 < sorted.Count ? sorted[i + 1] - sorted[i] : sorted[0] + Tau - sorted[i];
            if (System.Math.Abs(gap - step) > tolerance) return false;
        }
        return true;
    }

    // ---------- the engagement on its own, no actors moving ----------

    static void EngagementGeometry()
    {
        // Three attackers bunched on one side of the target are spread a third of a circle apart on the melee ring.
        ResetWorld();
        GameObject player = Player(0f, true);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Fighter[] f = { AtBearing(0f, 3f), AtBearing(10f, 3f), AtBearing(20f, 3f) };
        ManagedCombatEngagement.Member[] m = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) m[i] = JoinAt(src, f[i], 0f);
        Check(src.AttackerCount == 3 && src.StrikingCount == 0, "joining adds attackers and gives no turn until the first pass");
        src.Engagement.Refresh(1f);
        Check(src.StrikingCount == 3, "three attackers and a limit of three: all three strike");
        Check(EvenlySpaced(SortedAngles(m), 0.02f), "three bunched attackers end up a third of a circle apart");
        bool onRing = true;
        foreach (ManagedCombatEngagement.Member x in m) if (System.Math.Abs((src.Engagement.SlotPosition(x) - src.Engagement.Center).magnitude - 1.26f) > 0.001f) onRing = false;
        Check(onRing, "every striking place lies on the melee ring");

        // With a limit of two, the two holders stand opposite each other and the third waits on the wider ring.
        ResetWorld();
        GameObject p2 = Player(0f, true);
        ManagedNpcThreatSource s2 = p2.GetComponent<ManagedNpcThreatSource>();
        s2.MaxMeleeAttackers = 2;
        Fighter[] g = { AtBearing(0f, 3f), AtBearing(10f, 3.1f), AtBearing(20f, 3.2f) };
        ManagedCombatEngagement.Member[] gm = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) gm[i] = JoinAt(s2, g[i], 0f);
        s2.Engagement.Refresh(1f);
        List<ManagedCombatEngagement.Member> holders = new List<ManagedCombatEngagement.Member>();
        ManagedCombatEngagement.Member waiter = null;
        foreach (ManagedCombatEngagement.Member x in gm) { if (x.HasToken) holders.Add(x); else waiter = x; }
        Check(holders.Count == 2 && waiter != null, "a limit of two gives two turns and one waiter");
        Check(holders.Count == 2 && System.Math.Abs(AngGap(holders[0].Angle, holders[1].Angle) - 3.14159265f) < 0.02f, "two holders stand opposite each other");
        Check(waiter != null && System.Math.Abs(waiter.Radius - 3.76f) < 0.001f, "the waiter stands on the waiting ring, 2.5 m outside the melee ring");
        Check(holders.Count == 2 && holders[0].Distance <= waiter.Distance + 0.001f && holders[1].Distance <= waiter.Distance + 0.001f, "the nearest attackers get the turns when nobody has waited longer");

        // A lowered limit takes the extra turns back, farthest first.
        ResetWorld();
        GameObject p3 = Player(0f, true);
        ManagedNpcThreatSource s3 = p3.GetComponent<ManagedNpcThreatSource>();
        Fighter[] h = { AtBearing(0f, 3f), AtBearing(120f, 4f), AtBearing(240f, 5f) };
        ManagedCombatEngagement.Member[] hm = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) hm[i] = JoinAt(s3, h[i], 0f);
        s3.Engagement.Refresh(1f);
        Check(s3.StrikingCount == 3, "all three strike under the limit of three");
        s3.MaxMeleeAttackers = 1;
        s3.Engagement.Refresh(1.3f);
        Check(s3.StrikingCount == 1 && hm[0].HasToken, "lowering the limit to one leaves the nearest attacker with the turn");

        // Rotation: with one turn and a two second rotation, every attacker gets it in turn, longest wait first.
        ResetWorld();
        GameObject p4 = Player(0f, true);
        ManagedNpcThreatSource s4 = p4.GetComponent<ManagedNpcThreatSource>();
        s4.MaxMeleeAttackers = 1; s4.AttackerRotationSeconds = 2f;
        Fighter[] r = { AtBearing(0f, 3f), AtBearing(90f, 3.5f), AtBearing(180f, 4f) };
        ManagedCombatEngagement.Member[] rm = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) rm[i] = JoinAt(s4, r[i], 0f);
        HashSet<int> held = new HashSet<int>();
        int Holder() { for (int i = 0; i < 3; i++) if (rm[i].HasToken) return i; return -1; }
        s4.Engagement.Refresh(1f);
        int first = Holder(); held.Add(first);
        s4.Engagement.Refresh(2.5f);
        Check(Holder() == first, "a holder keeps the turn until the rotation time has passed");
        s4.Engagement.Refresh(3.1f);
        int second = Holder(); held.Add(second);
        Check(second != first && s4.StrikingCount == 1, "after the rotation time the turn goes to someone who was waiting");
        s4.Engagement.Refresh(5.3f);
        held.Add(Holder());
        Check(held.Count == 3, "after two rotations all three attackers have had the turn");
        s4.AttackerRotationSeconds = 0f;
        int stay = Holder();
        s4.Engagement.Refresh(30f);
        Check(Holder() == stay, "a rotation time of zero lets the holder keep the turn");

        // Waiters get an even spread on their ring, a step ahead along the circle that flips direction every eight seconds.
        ResetWorld();
        GameObject p5 = Player(0f, true);
        ManagedNpcThreatSource s5 = p5.GetComponent<ManagedNpcThreatSource>();
        s5.MaxMeleeAttackers = 1; s5.AttackerRotationSeconds = 0f;
        Fighter w0 = AtBearing(0f, 3f), w1 = AtBearing(200f, 4f);
        ManagedCombatEngagement.Member wm0 = JoinAt(s5, w0, 0f), wm1 = JoinAt(s5, w1, 0f);
        s5.Engagement.Refresh(1f);
        ManagedCombatEngagement.Member wait = wm0.HasToken ? wm1 : wm0;
        float ahead = 1.5f * 1f / 3.76f;
        Check(System.Math.Abs(WrapRad(wait.Angle - wait.Bearing) - ahead) < 0.005f, "a waiter's place is a step ahead of it along the circle");
        s5.Engagement.Refresh(9f);
        wait = wm0.HasToken ? wm1 : wm0;
        Check(System.Math.Abs(WrapRad(wait.Angle - wait.Bearing) + ahead) < 0.005f, "and the direction flips after eight seconds");

        // A crowd of waiters widens the ring so each keeps its arc, and they are spread evenly around it.
        ResetWorld();
        GameObject p6 = Player(0f, true);
        ManagedNpcThreatSource s6 = p6.GetComponent<ManagedNpcThreatSource>();
        s6.MaxMeleeAttackers = 1; s6.AttackerRotationSeconds = 0f;
        List<ManagedCombatEngagement.Member> crowd = new List<ManagedCombatEngagement.Member>();
        for (int i = 0; i < 40; i++) crowd.Add(JoinAt(s6, AtBearing(i * 9f + (i % 3), 6f + (i % 5)), 0f));
        s6.Engagement.Refresh(1f);
        List<ManagedCombatEngagement.Member> waiting = crowd.FindAll(x => !x.HasToken);
        Check(waiting.Count == 39 && s6.StrikingCount == 1, "forty attackers with one turn leave thirty nine waiting");
        float expected = 39f * 1.2f / Tau;
        Check(System.Math.Abs(waiting[0].Radius - expected) < 0.001f && expected > 3.76f, "a crowd widens the waiting ring to " + expected.ToString("F2"));
        Check(EvenlySpaced(SortedAngles(waiting), 0.01f), "the waiters are spread evenly around it");
    }

    // ---------- attackers walking in ----------

    static void EngagementSpacing()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = { Goblin(new Vector3(-0.6f, 0, 0)), Goblin(new Vector3(0.6f, 0, 0)), Goblin(new Vector3(0, 0, 1f)), Goblin(new Vector3(0, 0, -1f)) };
        Advance(4f);
        Check(src.AttackerCount == 4, "all four attackers join the engagement, got " + src.AttackerCount);
        Check(src.StrikingCount == 3, "only three hold a turn by default, got " + src.StrikingCount);
        List<Fighter> holders = new List<Fighter>(), waiting = new List<Fighter>();
        foreach (Fighter f in g) { if (f.Actor.HasAttackToken) holders.Add(f); else waiting.Add(f); }
        Check(holders.Count == 3 && waiting.Count == 1, "three holders and one waiter");
        bool inReach = true, facing = true, hit = true;
        foreach (Fighter f in holders)
        {
            if (Dist(f.Go.transform.position, player.transform.position) >= 1.8f) inReach = false;
            if (System.Math.Abs(Mathf.DeltaAngle(f.Go.transform.rotation.yaw, Quaternion.LookRotation(player.transform.position - f.Go.transform.position).yaw)) > 15f) facing = false;
            if (f.Hits < 1) hit = false;
        }
        Check(inReach, "every holder stands within melee reach");
        Check(facing, "every holder faces the target");
        Check(hit, "every holder has struck at least once by now");
        float minSeparation = float.MaxValue, minAngle = float.MaxValue;
        for (int i = 0; i < holders.Count; i++)
            for (int j = i + 1; j < holders.Count; j++)
            {
                minSeparation = System.Math.Min(minSeparation, Dist(holders[i].Go.transform.position, holders[j].Go.transform.position));
                minAngle = System.Math.Min(minAngle, AngGap(Ang(holders[i].Go.transform.position, player.transform.position), Ang(holders[j].Go.transform.position, player.transform.position)));
            }
        Check(minSeparation >= 1.5f, "holders stand apart, nearest pair " + minSeparation.ToString("F2") + " m");
        Check(minAngle >= 1.5f, "holders are spread around the target, narrowest gap " + minAngle.ToString("F2") + " rad");
        Fighter w = waiting[0];
        float wd = Dist(w.Go.transform.position, player.transform.position);
        Check(wd > 3f && wd < 5f, "the waiter keeps to the waiting ring, " + wd.ToString("F2") + " m out");
        Check(w.Hits == 0, "the waiter has not struck");
        Check(!w.Agent.updateRotation, "a close attacker turns itself to face the target and not by its path");
    }

    static void EngagementUnlimited()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        src.MaxMeleeAttackers = 0;
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = {
            Goblin(new Vector3(-2, 0, 0)), Goblin(new Vector3(-1, 0, 0)), Goblin(new Vector3(0, 0, 0)),
            Goblin(new Vector3(1, 0, 0)), Goblin(new Vector3(2, 0, 0)), Goblin(new Vector3(0, 0, 1)) };
        Advance(5f);
        Check(src.StrikingCount == 6 && src.AttackerCount == 6, "with no limit all six strike, got " + src.StrikingCount);
        bool allHit = true, close = true; float sep = float.MaxValue;
        for (int i = 0; i < g.Length; i++)
        {
            if (g[i].Hits < 1) allHit = false;
            if (Dist(g[i].Go.transform.position, player.transform.position) >= 1.8f) close = false;
            for (int j = i + 1; j < g.Length; j++) sep = System.Math.Min(sep, Dist(g[i].Go.transform.position, g[j].Go.transform.position));
        }
        Check(allHit && close, "all six reach the target and strike");
        Check(sep >= 1.0f, "six attackers still stand apart, nearest pair " + sep.ToString("F2") + " m, an agent is about 1 m wide");
    }

    static void EngagementRotation()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        src.AttackerRotationSeconds = 2f;
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = { Goblin(new Vector3(-0.6f, 0, 0)), Goblin(new Vector3(0.6f, 0, 0)), Goblin(new Vector3(0, 0, 1f)), Goblin(new Vector3(0, 0, -1f)) };
        int most = 0;
        for (int i = 0; i < 280; i++) { Advance(0.05f); most = System.Math.Max(most, src.StrikingCount); }
        Check(most == 3, "never more than three strike at once, saw " + most);
        bool everyone = true; string hits = "";
        foreach (Fighter f in g) { if (f.Hits < 1) everyone = false; hits += f.Hits + " "; }
        Check(everyone, "with rotation every attacker gets to strike within fourteen seconds, hits " + hits);
    }

    static void EngagementLone()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Fighter f = Goblin(Vector3.zero);
        Advance(4f);
        Check(src.AttackerCount == 1 && src.StrikingCount == 1 && f.Actor.HasAttackToken && f.Actor.IsEngaged, "a lone attacker holds a turn");
        float d = Dist(f.Go.transform.position, player.transform.position);
        Check(d < 1.8f && d > 1.0f, "it stops in melee reach on the straight line from where it came, " + d.ToString("F2"));
        Check(System.Math.Abs(f.Go.transform.position.x) < 0.01f, "on the side it came from");
        Check(f.Hits >= 2, "and strikes on the cooldown, hits " + f.Hits);
        Check(f.Agent.avoidancePriority == 40, "a holder pushes through other agents, priority " + f.Agent.avoidancePriority);
    }

    static void EngagementCircling()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = { Goblin(new Vector3(-0.6f, 0, 0)), Goblin(new Vector3(0.6f, 0, 0)), Goblin(new Vector3(0, 0, 1f)), Goblin(new Vector3(0, 0, -1f)) };
        Advance(3f);
        Fighter w = null; foreach (Fighter f in g) if (!f.Actor.HasAttackToken) w = f;
        Check(w != null, "one attacker waits");
        Vector3 here = player.transform.position;
        float b1 = Ang(w.Go.transform.position, here), r1 = Dist(w.Go.transform.position, here);
        Check(w.Agent.avoidancePriority == 60, "a waiter gives way to the holders, priority " + w.Agent.avoidancePriority);
        Check(System.Math.Abs(w.Agent.speed - 1.5f) < 0.001f, "a waiter on its ring moves at the circling speed, " + w.Agent.speed);
        Advance(1.5f);
        float b2 = Ang(w.Go.transform.position, here), r2 = Dist(w.Go.transform.position, here);
        Check(AngGap(b1, b2) > 0.5f, "a waiter circles the target, moved " + AngGap(b1, b2).ToString("F2") + " rad");
        Check(System.Math.Abs(r1 - 3.76f) < 1f && System.Math.Abs(r2 - 3.76f) < 1f, "and stays near its ring, " + r1.ToString("F2") + " then " + r2.ToString("F2"));
        float yaw = System.Math.Abs(Mathf.DeltaAngle(w.Go.transform.rotation.yaw, Quaternion.LookRotation(here - w.Go.transform.position).yaw));
        Check(yaw < 20f, "and keeps facing the target while it moves sideways, off by " + yaw.ToString("F0") + " degrees");
        Check(w.Brain.ThreatVisible && w.Brain.Action == NpcAction.Attack, "so it keeps seeing the target and stays in the fight");
        Check(src.StrikingCount == 3, "while three keep their turns");

        // A circling speed of zero makes a waiter stand on the ring.
        ResetWorld();
        GameObject p2 = Player(10f, true); Clear(p2);
        p2.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] h = { Goblin(new Vector3(-0.6f, 0, 0)), Goblin(new Vector3(0.6f, 0, 0)), Goblin(new Vector3(0, 0, 1f)), Goblin(new Vector3(0, 0, -1f)) };
        foreach (Fighter f in h) Set(f.Actor, "_circleSpeed", 0f);
        Advance(3.5f);
        Fighter still = null; foreach (Fighter f in h) if (!f.Actor.HasAttackToken) still = f;
        Vector3 at = still.Go.transform.position;
        Advance(1f);
        Check(Dist(at, still.Go.transform.position) < 0.05f, "a waiter with circling speed zero stands still");
    }

    static void EngagementBlocked()
    {
        // An attacker that cannot make progress asks for another place on the ring.
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        Advance(0.3f);
        Vector3 slot0 = f.Actor.EngagementSlot;
        f.Agent.Frozen = true;
        float most = 0f;
        for (int i = 0; i < 60; i++) { Advance(0.05f); most = System.Math.Max(most, Dist(f.Actor.EngagementSlot, slot0)); }
        Check(most > 0.5f, "a stuck attacker is given another place along the ring, moved " + most.ToString("F2") + " m");
        Check(f.Hits == 0, "and has not struck, it is not in reach");
        f.Agent.Frozen = false;
        Advance(6f);
        Check(Dist(f.Go.transform.position, player.transform.position) < 1.8f && f.Hits >= 1, "freed, it reaches the target and strikes");

        // A place that is off the NavMesh is replaced without sending the agent anywhere.
        ResetWorld();
        GameObject p2 = Player(10f, true); Clear(p2);
        NavMesh.IsWalkable = p => !(System.Math.Abs(p.x) < 0.5f && p.z > 8f && p.z < 9.5f);
        Fighter g = Goblin(Vector3.zero);
        Advance(0.3f);
        Vector3 inWall = new Vector3(0f, 0f, 8.74f);
        Check(Dist(g.Actor.EngagementSlot, inWall) < 0.05f, "the engagement first offers a place inside the wall");
        Advance(1.2f);
        Check(System.Math.Abs(g.Actor.EngagementSlot.x) > 0.5f, "the attacker is given another place beside it");
        Check(NavMesh.IsWalkable(g.Agent.destination), "no destination inside the wall was sent to the agent");
        Advance(6f);
        Check(g.Hits >= 1, "and still gets to strike");

        // A holder in reach that cannot settle at its place strikes from where it stands after a moment.
        ResetWorld();
        GameObject p3 = Player(10f, true); Clear(p3);
        p3.GetComponent<ManagedNpcThreatSource>().MaxMeleeAttackers = 2;
        p3.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter a = Goblin(new Vector3(-0.3f, 0, 7f)), b = Goblin(new Vector3(0.3f, 0, 7f));
        a.Agent.Frozen = true; b.Agent.Frozen = true;
        Advance(0.6f);
        a.Go.transform.position = new Vector3(-0.3f, 0, 9f); b.Go.transform.position = new Vector3(0.3f, 0, 9f);
        Advance(1.0f);
        Check(a.Hits == 0 && b.Hits == 0, "holders far from their places hold their blows at first");
        Advance(1.5f);
        Check(a.Hits >= 1 && b.Hits >= 1, "and strike after the settling time rather than stand idle in reach");
    }

    static void EngagementLeave()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Fighter f = Goblin(Vector3.zero);
        Advance(3f);
        Check(src.AttackerCount == 1 && !f.Agent.updateRotation && f.Agent.avoidancePriority == 40, "engaged: counted, turning itself, holder priority");
        player.GetComponent<ManagedNpcThreatSource>().enabled = false;
        Advance(1.5f);
        Check(f.Brain.Action == NpcAction.Patrol && src.AttackerCount == 0, "when the threat is gone it leaves the engagement");
        Check(f.Agent.updateRotation && f.Agent.avoidancePriority == 50, "and the agent turns by its path again with its own priority");

        // A new fight starts clean.
        player.GetComponent<ManagedNpcThreatSource>().enabled = true;
        Advance(2f);
        Check(src.AttackerCount == 1 && f.Actor.HasAttackToken, "a new fight joins again");

        // Switching to retreat gives up the place.
        f.Health.TakeDamage(90f);
        Advance(1f);
        Check(f.Brain.Action == NpcAction.Retreat && src.AttackerCount == 0 && f.Agent.updateRotation, "retreating leaves the engagement");
    }

    static void EngagementHandover()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = { Goblin(new Vector3(-0.6f, 0, 0)), Goblin(new Vector3(0.6f, 0, 0)), Goblin(new Vector3(0, 0, 1f)), Goblin(new Vector3(0, 0, -1f)) };
        Advance(3f);
        Fighter holder = null, waiter = null;
        foreach (Fighter f in g) { if (f.Actor.HasAttackToken) holder = holder ?? f; else waiter = f; }
        holder.Health.TakeDamage(1000f);
        Advance(0.6f);
        Check(src.AttackerCount == 3, "a dead attacker leaves the engagement");
        Check(waiter.Actor.HasAttackToken && src.StrikingCount == 3, "and its turn goes to the waiter");

        // Destroying the whole group empties it.
        foreach (Fighter f in g) f.Health.TakeDamage(1000f);
        Advance(0.5f);
        Check(src.AttackerCount == 0 && src.StrikingCount == 0, "with everyone dead nobody is left in the engagement");
    }

    // ---------- choosing a target ----------

    static void TargetChoice()
    {
        ResetWorld();
        GameObject far = Player(10f, true), near = Player(6f, true);
        ClearAll(far, near);
        ManagedNpcThreatSource sFar = far.GetComponent<ManagedNpcThreatSource>(), sNear = near.GetComponent<ManagedNpcThreatSource>();
        ManagedNpcBrain n = Npc("n", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Tick(0.1f);
        Check(n.Threat == sNear, "of two visible sources the nearer is the target");
        far.transform.position = new Vector3(0, 0, 5.5f);
        Tick(0.1f);
        Check(n.Threat == sNear, "a source only slightly nearer does not take the target over");
        far.transform.position = new Vector3(0, 0, 3f);
        Tick(0.1f);
        Check(n.Threat == sFar, "a source clearly nearer does");
        Check(Dist(n.LastKnownThreatPosition, far.transform.position) < 0.001f, "and its position is the one remembered");

        // The one that hurt the NPC is preferred over a nearer stranger while the NPC is provoked.
        ResetWorld();
        GameObject attacker = Player(8f, true), stranger = Player(5f, true);
        ClearAll(attacker, stranger);
        Fighter hurt = Goblin(Vector3.zero), calm = Goblin(Vector3.zero);
        hurt.Health.TakeDamage(1f, attacker);
        Tick(0.1f);
        Check(hurt.Brain.Provoked && hurt.Brain.Threat == attacker.GetComponent<ManagedNpcThreatSource>(), "a provoked NPC targets what hurt it even though a stranger is nearer");
        Check(calm.Brain.Threat == stranger.GetComponent<ManagedNpcThreatSource>(), "an NPC nobody hurt takes the nearer one");

        // More sources than the first scratch array holds, with the nearest registered last.
        ResetWorld();
        List<GameObject> crowd = new List<GameObject>();
        for (int i = 0; i < 12; i++) crowd.Add(Player(14f - i, true));
        ClearAll(crowd.ToArray());
        ManagedNpcBrain m = Npc("m", NpcRole.Enemy, DecisionBackend.Utility, true, Vector3.zero);
        Tick(0.1f);
        Check(m.Threat == crowd[11].GetComponent<ManagedNpcThreatSource>(), "twelve sources, the nearest one is chosen");
        Check(Physics.RayCount <= 2, "and the rays stop at the first source seen, " + Physics.RayCount);

        // Ranking leaves no references behind.
        Check(typeof(ManagedNpcBrain).GetField("RankedSources", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic) != null, "the ranking scratch exists");
        System.Array scratch = (System.Array)typeof(ManagedNpcBrain).GetField("RankedSources", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
        bool clean = true; foreach (object o in scratch) if (o != null) clean = false;
        Check(clean, "the shared ranking array is emptied after every sight pass");
    }

    // ---------- a swing at a target that moved ----------

    static void HitReach()
    {
        ResetWorld();
        GameObject player = Player(1.5f, true); Clear(player);
        Fighter f = Goblin(Vector3.zero);
        Advance(0.5f);
        Check(f.Hits >= 1, "a target in reach is hit");
        int landed = f.Hits;
        float health = player.GetComponent<ManagedHealth>().Current;
        // The target steps back after the NPC last saw it. The NPC still aims at the old place.
        player.transform.position = new Vector3(0, 0, 4.5f);
        Set(f.Actor, "_nextAttackTime", 0f);
        UnityEngine.Component.Invoke(f.Actor, "Update");
        Check(f.Hits == landed && System.Math.Abs(player.GetComponent<ManagedHealth>().Current - health) < 0.001f, "a swing misses a target that has really moved out of reach");
        // A small step back is within the lunge.
        player.transform.position = new Vector3(0, 0, 2.4f);
        Set(f.Actor, "_nextAttackTime", 0f);
        UnityEngine.Component.Invoke(f.Actor, "Update");
        Check(f.Hits == landed + 1, "a step back within the leeway still gets hit");
    }

    // ---------- debugger ----------

    static void EngagementDebug()
    {
        ResetWorld(); UnityEditor.Handles.Reset();
        GameObject player = Player(10f, true); Clear(player);
        player.GetComponent<ManagedNpcThreatSource>().MaxMeleeAttackers = 1;
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter[] g = { Goblin(new Vector3(0, 0, 0)), Goblin(new Vector3(0.5f, 0, -3f)) };
        Advance(2f);
        Fighter holder = g[0].Actor.HasAttackToken ? g[0] : g[1], waiter = holder == g[0] ? g[1] : g[0];
        Check(ManagedNpcDebugView.Describe(holder.Brain).Contains("striking") && !ManagedNpcDebugView.Describe(holder.Brain).Contains("waiting"), "the debugger says a holder is striking");
        Check(ManagedNpcDebugView.Describe(waiter.Brain).Contains("waiting") && !ManagedNpcDebugView.Describe(waiter.Brain).Contains("striking"), "and a waiter is waiting");
        ManagedNpcDrawOptions all = new ManagedNpcDrawOptions { Mode = ManagedNpcDrawMode.All };
        UnityEditor.Handles.Reset();
        ManagedNpcDebugView.Draw(waiter.Brain, all, true, new List<Vector3>());
        Check(Count("WireDisc:0.35") == 1 && Count("Dotted") >= 1, "the Scene view marks the waiter's place and draws a line to it");
        UnityEditor.Handles.Reset();
        all.Ranges = false;
        ManagedNpcDebugView.Draw(waiter.Brain, all, true, new List<Vector3>());
        Check(Count("WireDisc:0.35") == 0, "the place is drawn with the ranges and not without them");

        ResetWorld();
        Fighter calm = Goblin(Vector3.zero);
        Advance(0.3f);
        Check(!ManagedNpcDebugView.Describe(calm.Brain).Contains("striking") && !ManagedNpcDebugView.Describe(calm.Brain).Contains("waiting"), "an NPC that is not attacking is described without a place");

        ResetWorld();
        GameObject lone = Player(10f, true);
        ManagedNpcThreatSource s = lone.GetComponent<ManagedNpcThreatSource>();
        s.MaxMeleeAttackers = -4; s.AttackerRotationSeconds = -1f;
        Check(s.MaxMeleeAttackers == 0 && s.AttackerRotationSeconds == 0f, "negative engagement settings clamp to zero");
        Check(s.AttackerCount == 0 && s.StrikingCount == 0, "a source nobody attacks reports no attackers");
    }

    // ---------- rules that the walking checks do not reach on their own ----------

    static void EngagementRules()
    {
        // Attackers bunched across the point where the bearings wrap around are spread evenly, and walk no more than needed.
        ResetWorld();
        GameObject player = Player(0f, true);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Fighter[] f = { AtBearing(170f, 3f), AtBearing(180f, 3f), AtBearing(-170f, 3f) };
        ManagedCombatEngagement.Member[] m = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) m[i] = JoinAt(src, f[i], 0f);
        src.Engagement.Refresh(1f);
        float walked = 0f;
        for (int i = 0; i < 3; i++) walked += AngGap(m[i].Bearing, m[i].Angle);
        Check(EvenlySpaced(SortedAngles(m), 0.02f), "three attackers bunched across the wrap of the bearings are still spread a third of a circle apart");
        Check(System.Math.Abs(walked - 3.840f) < 0.02f, "and between them walk the least there is, " + walked.ToString("F3") + " rad");

        // The place comes from where the freshest sighting put the target, whichever attacker joined first.
        for (int order = 0; order < 2; order++)
        {
            ResetWorld();
            GameObject p2 = Player(0f, true);
            ManagedNpcThreatSource s2 = p2.GetComponent<ManagedNpcThreatSource>();
            Fighter stale = AtBearing(0f, 3f), fresh = AtBearing(90f, 3f);
            Set(stale.Brain, "_lastSeenTime", 1f); Set(stale.Brain, "_lastKnownThreatPosition", new Vector3(0f, 0f, 0f));
            Set(fresh.Brain, "_lastSeenTime", 2f); Set(fresh.Brain, "_lastKnownThreatPosition", new Vector3(5f, 0f, 0f));
            if (order == 0) { JoinAt(s2, fresh, 2f); JoinAt(s2, stale, 2f); } else { JoinAt(s2, stale, 2f); JoinAt(s2, fresh, 2f); }
            s2.Engagement.Refresh(3f);
            Check(Dist(s2.Engagement.Center, new Vector3(5f, 0f, 0f)) < 0.001f, "the rings are drawn around the freshest sighting, joined " + (order == 0 ? "first" : "last"));
        }

        // Two neighbours that block each other step opposite ways, and a repeated block turns the step around and widens it.
        ResetWorld();
        GameObject p3 = Player(0f, true);
        ManagedNpcThreatSource s3 = p3.GetComponent<ManagedNpcThreatSource>();
        Fighter even = AtBearing(0f, 3f), odd = AtBearing(10f, 3f);
        even.Brain.Id = 2UL; odd.Brain.Id = 3UL;
        ManagedCombatEngagement.Member me = JoinAt(s3, even, 0f), mo = JoinAt(s3, odd, 0f);
        me.Blocked = true; mo.Blocked = true;
        s3.Engagement.Refresh(1f);
        Check(me.Bias > 0f && mo.Bias < 0f, "two neighbours that report themselves stuck are moved opposite ways, " + me.Bias.ToString("F2") + " and " + mo.Bias.ToString("F2"));
        Check(System.Math.Abs(me.Bias - 0.6f) < 0.001f, "the first step is 0.6 rad");
        me.Blocked = true;
        s3.Engagement.Refresh(1.3f);
        Check(System.Math.Abs(me.Bias + 0.9f) < 0.001f, "a second block inside the window turns the step around and grows it by half, " + me.Bias.ToString("F2"));
        me.Blocked = true; s3.Engagement.Refresh(1.6f);
        me.Blocked = true; s3.Engagement.Refresh(1.9f);
        Check(System.Math.Abs(me.Bias) <= 1.8001f, "the step never grows past 1.8 rad, " + me.Bias.ToString("F2"));
        s3.Engagement.Refresh(6f);
        Check(me.Bias == 0f && mo.Bias == 0f, "and it ends when its time is up");

        // Only a holder strikes. A waiter standing in reach waits.
        ResetWorld();
        GameObject p4 = Player(1.2f, true); Clear(p4);
        ManagedNpcThreatSource s4 = p4.GetComponent<ManagedNpcThreatSource>();
        s4.MaxMeleeAttackers = 1; s4.AttackerRotationSeconds = 0f;
        p4.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter a = Goblin(new Vector3(-0.3f, 0, 0)), b = Goblin(new Vector3(0.3f, 0, 0));
        a.Agent.Frozen = true; b.Agent.Frozen = true;
        Advance(4f);
        Fighter holder = a.Actor.HasAttackToken ? a : b, waiter = holder == a ? b : a;
        Check(s4.AttackerCount == 2 && s4.StrikingCount == 1, "two attackers, one turn");
        Check(Dist(waiter.Go.transform.position, p4.transform.position) < 1.8f, "the waiter stands within reach of the target");
        Check(holder.Hits >= 1 && waiter.Hits == 0, "and still does not strike, holder hits " + holder.Hits + ", waiter hits " + waiter.Hits);
    }

    // ---------- cost ----------

    static void EngagementCost()
    {
        ResetWorld();
        GameObject player = Player(0f, true);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        src.MaxMeleeAttackers = 3;
        for (int i = 0; i < 200; i++) JoinAt(src, AtBearing(i * 1.8f, 6f + (i % 7)), 0f);
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        const int passes = 50;
        for (int i = 0; i < passes; i++) src.Engagement.Refresh(1f + i * 0.25f);
        sw.Stop();
        double perPass = sw.Elapsed.TotalMilliseconds / passes;
        System.Console.WriteLine("engagement pass with 200 attackers: " + perPass.ToString("F3") + " ms");
        Check(src.StrikingCount == 3 && src.AttackerCount == 200, "two hundred attackers, three turns");
        Check(perPass < 20.0, "one pass over two hundred attackers stays cheap, " + perPass.ToString("F3") + " ms");

        // A group of twenty five waiters gets its places in one pass as well.
        ResetWorld();
        GameObject p2 = Player(0f, true);
        ManagedNpcThreatSource s2 = p2.GetComponent<ManagedNpcThreatSource>();
        s2.MaxMeleeAttackers = 1;
        for (int i = 0; i < 25; i++) JoinAt(s2, AtBearing(i * 14f, 5f), 0f);
        sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < passes; i++) s2.Engagement.Refresh(1f + i * 0.25f);
        sw.Stop();
        Check(sw.Elapsed.TotalMilliseconds / passes < 20.0, "one pass over twenty five waiters stays cheap, " + (sw.Elapsed.TotalMilliseconds / passes).ToString("F3") + " ms");
    }
}
