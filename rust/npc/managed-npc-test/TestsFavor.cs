// Checks for favor: the arithmetic and the tiers, the gauge of one NPC, the regions and the Local Favor built from the
// gauges standing in them, what an NPC sends to the decision, what the decision then does with it, and what hurting
// somebody costs. Part of the Managed NPC harness, same Unity stand-ins and the same limits as the other checks. Sight
// goes through the stub raycast, so these checks prove the rules around sight and not real colliders.
using System.Collections.Generic;
using System.Reflection;
using MidManStudio.Gtg.Managed.Crown;
using MidManStudio.Gtg.Managed.Favor;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;

internal static partial class Tests
{
    // A region centred on (x, 0, z) with a square footprint of the given side.
    static ManagedFavorRegion FvTown(string name, float x, float z, float side)
    {
        GameObject g = new GameObject(name);
        g.transform.position = new Vector3(x, 0f, z);
        ManagedFavorRegion r = g.AddComponent<ManagedFavorRegion>();
        Set(r, "_displayName", name);
        Set(r, "_size", new Vector3(side, 40f, side));
        return r;
    }

    // A gauge on the NPC, set to the favor.
    static ManagedNpcFavor FvGauge(ManagedNpcBrain brain, float favor)
    {
        ManagedNpcFavor g = brain.gameObject.AddComponent<ManagedNpcFavor>();
        g.Set(favor);
        return g;
    }

    static ManagedNpcBrain FvNpc(NpcRole role, Vector3 pos, float fieldOfView = 360f)
    {
        ManagedNpcBrain b = Villager(role, pos);
        Set(b, "_fieldOfViewDegrees", fieldOfView);
        return b;
    }

    static void FvHit(ManagedNpcBrain victim, GameObject source, float amount = 1f)
    {
        victim.GetComponent<ManagedHealth>().TakeDamage(amount, source);
    }

    // ---------- arithmetic and tiers ----------

    static void FavorMath()
    {
        Check(ManagedFavorMath.Clamp(150f) == 100f && ManagedFavorMath.Clamp(-150f) == -100f && ManagedFavorMath.Clamp(40f) == 40f, "favor is held between -100 and 100");
        Check(ManagedFavorMath.Clamp(float.NaN) == 0f && ManagedFavorMath.Clamp(float.PositiveInfinity) == 100f && ManagedFavorMath.Clamp(float.NegativeInfinity) == -100f, "a number that is not a number is neutral, and infinity is the end of the scale");
        Check(RgNear(ManagedFavorMath.Normalize(50f), 0.5f, 1e-6f) && ManagedFavorMath.Normalize(100f) == 1f && ManagedFavorMath.Normalize(-100f) == -1f, "the native value is the favor over 100");
        Check(ManagedFavorMath.Normalize(250f) == 1f && ManagedFavorMath.Normalize(float.NaN) == 0f, "and is held between -1 and 1, with a number that is not a number sent as neutral");

        Check(ManagedFavorMath.Blend(0f, 0, 40f, 3f) == 40f, "a group with no members is the crowd");
        Check(ManagedFavorMath.Blend(100f, 1, 0f, 3f) == 25f, "one member leans on a crowd worth three: 100 and three zeros average to 25");
        Check(ManagedFavorMath.Blend(200f, 2, -100f, 2f) == 0f, "two members at 100 against a crowd worth two at -100 cancel out");
        Check(RgNear(ManagedFavorMath.Blend(5000f, 50, -100f, 3f), 88.679f, 0.01f), "fifty members at 100 are hardly moved by the crowd, got " + ManagedFavorMath.Blend(5000f, 50, -100f, 3f));
        Check(ManagedFavorMath.Blend(0f, 0, 40f, 0f) == 40f, "no members and a crowd worth nothing is still the crowd");
        Check(ManagedFavorMath.Blend(60f, 2, -100f, float.NaN) == 30f && ManagedFavorMath.Blend(60f, 2, -100f, -5f) == 30f, "a crowd weight that is not above zero, or not a number, counts the members only");
        Check(ManagedFavorMath.Blend(float.NaN, 2, 40f, 2f) == 20f, "a sum that is not a number counts as zero");
        Check(ManagedFavorMath.Blend(1e9f, 1, 0f, 0f) == 100f && ManagedFavorMath.Blend(0f, 0, 500f, 1f) == 100f, "the result is held between -100 and 100");
        Check(ManagedFavorMath.Blend(0f, 0, float.NaN, 3f) == 0f, "a crowd that is not a number is neutral");

        Check(ManagedFavorTier.For(-100f) == ManagedFavorTier.Hated && ManagedFavorTier.For(-60f) == ManagedFavorTier.Hated, "-60 and below is hated");
        Check(ManagedFavorTier.For(-59.9f) == ManagedFavorTier.Disliked && ManagedFavorTier.For(-20f) == ManagedFavorTier.Disliked, "-59.9 up to -20 is disliked");
        Check(ManagedFavorTier.For(-19.9f) == ManagedFavorTier.Neutral && ManagedFavorTier.For(0f) == ManagedFavorTier.Neutral && ManagedFavorTier.For(19.9f) == ManagedFavorTier.Neutral, "between the lines is neutral");
        Check(ManagedFavorTier.For(20f) == ManagedFavorTier.Liked && ManagedFavorTier.For(59.9f) == ManagedFavorTier.Liked, "20 up to 59.9 is liked");
        Check(ManagedFavorTier.For(60f) == ManagedFavorTier.Adored && ManagedFavorTier.For(100f) == ManagedFavorTier.Adored, "60 and above is adored");
        Check(ManagedFavorTier.For(float.NaN) == ManagedFavorTier.Neutral, "a number that is not a number is neutral");
        Check(ManagedFavorTier.Name(ManagedFavorTier.Hated) == "hated" && ManagedFavorTier.Name(ManagedFavorTier.Disliked) == "disliked" && ManagedFavorTier.Name(ManagedFavorTier.Neutral) == "neutral"
            && ManagedFavorTier.Name(ManagedFavorTier.Liked) == "liked" && ManagedFavorTier.Name(ManagedFavorTier.Adored) == "adored" && ManagedFavorTier.Name(99) == "neutral", "every tier has its name, and an unknown one reads as neutral");
        Check(ManagedFavorTier.LikedAt / ManagedFavorMath.Max == NPCManagedFallback.FriendlyFavor, "the liked line is the line where the native decision starts to treat a shopkeeper as friendly");

        Check(ManagedFavorSeverity.Of(ManagedCrimeKind.Assault) == 1f && ManagedFavorSeverity.Of(ManagedCrimeKind.AssaultOnGuard) == 1f, "hurting somebody weighs one");
        Check(ManagedFavorSeverity.Of(ManagedCrimeKind.Murder) == 3f && ManagedFavorSeverity.Of(ManagedCrimeKind.MurderOfGuard) == 3f, "killing somebody weighs three");
    }

    // ---------- the gauge of one NPC ----------

    static void FavorGauge()
    {
        ResetWorld();
        ManagedNpcBrain baker = Villager(NpcRole.Merchant, Vector3.zero);
        ManagedNpcFavor g = baker.gameObject.AddComponent<ManagedNpcFavor>();
        Check(ManagedNpcFavor.Count == 1 && ManagedNpcFavor.Get(0) == g, "an enabled gauge registers");
        Check(g.Value == 0f && g.Normalized == 0f && g.Tier == ManagedFavorTier.Neutral && g.IsAlive, "it starts neutral and alive");
        Check(g.Brain == baker && baker.FavorGauge == g, "it finds the brain on its object and hands itself to it");
        Check(g.HurtPenalty == 20f && g.WitnessPenalty == 5f, "the penalties start at 20 and 5");

        List<string> seen = new List<string>();
        g.Changed += (x, o, n) => seen.Add(o + ">" + n);
        int version = ManagedNpcFavor.Version;
        g.Set(50f);
        Check(g.Value == 50f && g.Tier == ManagedFavorTier.Liked && RgNear(g.Normalized, 0.5f, 1e-6f), "Set moves the favor");
        Check(ManagedNpcFavor.Version == version + 1, "and every change counts toward the version the regions watch");
        g.Set(50f);
        Check(ManagedNpcFavor.Version == version + 1 && seen.Count == 1, "setting the same value is no change and raises nothing");
        g.Add(-20f);
        g.Add(float.NaN);
        g.Set(float.NaN);
        g.Add(-500f);
        Check(g.Value == -100f && g.Tier == ManagedFavorTier.Hated, "Add takes away, and the favor stops at -100");
        Check(string.Join(",", seen) == "0>50,50>30,30>-100", "Changed fires once per real change, with the old and the new value, got " + string.Join(",", seen));
        g.Set(float.PositiveInfinity);
        Check(g.Value == 100f, "infinity is held at 100");
        g.Set(float.NegativeInfinity);
        Check(g.Value == -100f, "and minus infinity at -100");

        // A gauge made in code is read before its Awake would have set the start value.
        GameObject lazy = new GameObject("lazy");
        ManagedNpcFavor lz = lazy.AddComponent<ManagedNpcFavor>();
        Set(lz, "_favor", 35f);
        Set(lz, "_started", false);
        Check(lz.Value == 35f && lz.Describe() == "lazy  favor 35.0  liked", "the start value is read when first needed, got '" + lz.Describe() + "'");
        Set(lz, "_favor", 500f);
        Set(lz, "_started", false);
        Check(lz.Value == 100f, "and held between -100 and 100");

        // The Inspector values are kept in order.
        Set(lz, "_favor", 500f); Set(lz, "_hurtPenalty", -3f); Set(lz, "_witnessPenalty", -1f);
        UnityEngine.Component.Invoke(lz, "OnValidate");
        Check((float)GetF(lz, "_favor") == 100f && lz.HurtPenalty == 0f && lz.WitnessPenalty == 0f, "the Inspector value is clamped and the penalties are never negative");

        // A gauge on an object with no brain is a plain meter.
        GameObject bare = new GameObject("bare");
        ManagedNpcFavor bg = bare.AddComponent<ManagedNpcFavor>();
        Check(bg.Brain == null && bg.IsAlive, "a gauge with no brain is alive and has no brain");

        // Disabling the gauge takes it out of the list and away from the brain.
        int count = ManagedNpcFavor.Count;
        version = ManagedNpcFavor.Version;
        g.enabled = false;
        Check(ManagedNpcFavor.Count == count - 1 && baker.FavorGauge == null && ManagedNpcFavor.Version != version, "a disabled gauge leaves the list and the brain, and the regions are told");
        g.enabled = true;
        Check(ManagedNpcFavor.Count == count && baker.FavorGauge == g, "and comes back when enabled again");
        g.enabled = true;
        Check(ManagedNpcFavor.Count == count, "enabling twice registers once");

        // A gauge put on the object before the brain still gets found.
        GameObject early = new GameObject("early");
        ManagedNpcFavor eg = early.AddComponent<ManagedNpcFavor>();
        ManagedNpcBrain eb = early.AddComponent<ManagedNpcBrain>();
        Check(eb.FavorGauge == eg, "a brain added after its gauge picks the gauge up");

        // The dead have no opinion.
        ResetWorld();
        ManagedNpcBrain victim = Villager(NpcRole.Civilian, Vector3.zero);
        ManagedNpcFavor vg = FvGauge(victim, 40f);
        Check(vg.IsAlive, "a living NPC's gauge is alive");
        victim.GetComponent<ManagedHealth>().TakeDamage(1000f);
        Check(!vg.IsAlive && vg.Value == 40f, "a dead NPC's gauge is not alive, and keeps what it held");
    }

    // ---------- where a region holds ----------

    static void FavorRegionArea()
    {
        ResetWorld();
        ManagedFavorRegion a = FvTown("Hollow", 0f, 0f, 20f);
        Check(a.Contains(new Vector3(9.9f, 0f, -9.9f)) && a.Contains(new Vector3(10f, 0f, 10f)), "a point inside the box and one on its edge are in the region");
        Check(!a.Contains(new Vector3(10.01f, 0f, 0f)) && !a.Contains(new Vector3(0f, 0f, -10.01f)), "a point just outside is not");
        Check(a.Contains(new Vector3(0f, 20f, 0f)) && !a.Contains(new Vector3(0f, 20.01f, 0f)), "the box has height as well");
        Check(ManagedFavorRegion.Count == 1 && ManagedFavorRegion.Get(0) == a, "an enabled region registers");
        Check(ManagedFavorRegion.Find(new Vector3(1f, 0f, 1f)) == a && ManagedFavorRegion.Find(new Vector3(50f, 0f, 0f)) == null, "Find returns the region around a point, and null in the wilds");
        Check(ManagedFavorRegion.CrowdAt(new Vector3(50f, 0f, 0f)) == 0f, "in the wilds the crowd is neutral");

        // The crowd is held between -100 and 100.
        Check(a.Crowd == 0f && a.Local == 0f && a.Tier == ManagedFavorTier.Neutral && a.MemberCount == 0, "a new region is neutral");
        a.SetCrowd(30f);
        Check(a.Crowd == 30f, "SetCrowd sets the crowd");
        a.AddCrowd(-200f);
        Check(a.Crowd == -100f, "AddCrowd takes away and stops at -100");
        a.AddCrowd(float.NaN);
        a.SetCrowd(float.NaN);
        Check(a.Crowd == -100f, "a number that is not a number changes nothing");
        a.SetCrowd(500f);
        Check(a.Crowd == 100f, "and the crowd stops at 100");

        // What an NPC with no gauge feels is the crowd of the region it stands in, from -1 to 1.
        a.SetCrowd(50f);
        Check(RgNear(ManagedFavorRegion.CrowdAt(new Vector3(1f, 0f, 1f)), 0.5f, 1e-6f), "CrowdAt is the crowd over 100");

        // Where boxes overlap, the first one registered wins.
        ManagedFavorRegion b = FvTown("Overlap", 5f, 0f, 20f);
        b.SetCrowd(-80f);
        Check(ManagedFavorRegion.Find(new Vector3(7f, 0f, 0f)) == a && RgNear(ManagedFavorRegion.CrowdAt(new Vector3(7f, 0f, 0f)), 0.5f, 1e-6f), "overlapping regions: the first registered wins");
        Check(ManagedFavorRegion.Find(new Vector3(14f, 0f, 0f)) == b && RgNear(ManagedFavorRegion.CrowdAt(new Vector3(14f, 0f, 0f)), -0.8f, 1e-6f), "and a point only the second holds gets the second's crowd");

        // A disabled region stops counting.
        a.enabled = false;
        Check(ManagedFavorRegion.Count == 1 && ManagedFavorRegion.Find(new Vector3(0f, 0f, 0f)) == b, "a disabled region is gone from the list");
        a.enabled = true;
        Check(ManagedFavorRegion.Count == 2, "and back when enabled again");
        a.enabled = true;
        Check(ManagedFavorRegion.Count == 2, "enabling twice registers once");

        // The start value is read when first needed, and the Inspector values are kept in order.
        GameObject g = new GameObject("Fresh");
        ManagedFavorRegion fresh = g.AddComponent<ManagedFavorRegion>();
        Set(fresh, "_startCrowd", 25f);
        Set(fresh, "_started", false);
        Check(fresh.Crowd == 25f, "the start crowd is read when first needed");
        Set(fresh, "_size", new Vector3(-5f, 0f, 0.01f)); Set(fresh, "_startCrowd", 500f); Set(fresh, "_crowdWeight", -2f); Set(fresh, "_crowdPenalty", -1f); Set(fresh, "_assaultRepeatSeconds", -3f);
        UnityEngine.Component.Invoke(fresh, "OnValidate");
        Vector3 size = (Vector3)GetF(fresh, "_size");
        Check(size.x == 0.1f && size.y == 0.1f && size.z == 0.1f, "the box is never smaller than 10 cm");
        Check((float)GetF(fresh, "_startCrowd") == 100f && fresh.CrowdWeight == 0f && fresh.CrowdPenalty == 0f && fresh.AssaultRepeatSeconds == 0f, "the crowd, the weight, the penalty and the repeat time are kept in range");

        Check(a.Describe() == "Hollow  local favor 50.0  liked  crowd 50.0  members 0", "the debugger line, got '" + a.Describe() + "'");

        // The box is drawn green where the people like the player, red where they do not, yellow in between.
        UnityEngine.Component.Invoke(a, "OnDrawGizmosSelected");
        Check(UnityEngine.Gizmos.color.g == 1f && UnityEngine.Gizmos.color.r == 0f, "a region that likes the player is drawn green");
        a.SetCrowd(-50f); a.Describe();
        UnityEngine.Component.Invoke(a, "OnDrawGizmosSelected");
        Check(UnityEngine.Gizmos.color.r == 1f && UnityEngine.Gizmos.color.g == 0f, "one that does not is drawn red");
        a.SetCrowd(0f); a.Describe();
        UnityEngine.Component.Invoke(a, "OnDrawGizmosSelected");
        Check(UnityEngine.Gizmos.color.r == 1f && UnityEngine.Gizmos.color.g == 1f, "and one in between is drawn yellow");
    }

    // ---------- the favor of the whole area ----------

    static void FavorLocal()
    {
        ResetWorld();
        ManagedFavorRegion town = FvTown("Town", 0f, 0f, 100f);
        List<string> tiers = new List<string>();
        town.TierChanged += (r, o, n) => tiers.Add(o + ">" + n);
        Check(town.Local == 0f && town.Tier == ManagedFavorTier.Neutral && town.MemberCount == 0 && tiers.Count == 0, "a region nobody has touched is neutral and silent");

        // With no members the Local Favor is the crowd.
        town.SetCrowd(-40f);
        Check(town.Local == -40f && town.Tier == ManagedFavorTier.Disliked && tiers.Count == 1 && tiers[0] == "0>-1", "with no gauges the Local Favor is the crowd, and the tier change is announced once, got " + string.Join(",", tiers));
        Check(town.Local == -40f && tiers.Count == 1, "asking again does not announce it again");

        // Each gauge counts one. The crowd counts as three more.
        ManagedNpcBrain n1 = Villager(NpcRole.Merchant, new Vector3(5f, 0f, 5f));
        ManagedNpcFavor g1 = FvGauge(n1, 60f);
        Check(town.MemberCount == 1 && town.Local == -15f && town.Tier == ManagedFavorTier.Neutral, "one baker at 60 against a crowd of -40 worth three: -15, got " + town.Local);
        Check(string.Join(",", tiers) == "0>-1,-1>0", "and the tier rose to neutral, got " + string.Join(",", tiers));
        ManagedNpcFavor g2 = FvGauge(Villager(NpcRole.Civilian, new Vector3(6f, 0f, 5f)), 100f);
        Check(town.MemberCount == 2 && town.Local == 8f, "a second at 100: 8, got " + town.Local);
        ManagedNpcFavor g3 = FvGauge(Villager(NpcRole.Civilian, new Vector3(7f, 0f, 5f)), 100f);
        Check(town.MemberCount == 3 && RgNear(town.Local, 23.333f, 0.01f) && town.Tier == ManagedFavorTier.Liked, "a third at 100 lifts the town to liked, got " + town.Local);
        Check(string.Join(",", tiers) == "0>-1,-1>0,0>1", "and that was announced, got " + string.Join(",", tiers));

        // A gauge that changes moves the town at once, with no wait for the next refresh.
        g3.Set(-100f);
        Check(RgNear(town.Local, -10f, 0.001f) && town.Tier == ManagedFavorTier.Neutral, "a gauge that falls drags the town down at once, got " + town.Local);
        g3.Set(100f);
        Check(RgNear(town.Local, 23.333f, 0.01f) && town.Tier == ManagedFavorTier.Liked, "and back up when it recovers, got " + town.Local);

        // A gauge outside every region counts toward none of them, until it is given a home.
        ManagedNpcBrain far = Villager(NpcRole.Merchant, new Vector3(500f, 0f, 0f));
        ManagedNpcFavor gf = FvGauge(far, 100f);
        Check(gf.Region == null && town.MemberCount == 3, "a gauge outside every region counts toward none");
        Set(gf, "_homeRegion", town);
        town.Refresh();
        Check(gf.Region == town && town.MemberCount == 4, "a home region makes it count wherever it stands");

        // The dead stop counting, once the region looks again.
        g1.Brain.GetComponent<ManagedHealth>().TakeDamage(1000f);
        Advance(0.6f);
        Check(town.MemberCount == 3, "a member who died is dropped by the next refresh, got " + town.MemberCount);

        // A crowd worth nothing leaves the average of the members.
        Set(town, "_crowdWeight", 0f);
        town.Refresh();
        Check(town.Local == 100f && town.Tier == ManagedFavorTier.Adored, "with the crowd worth nothing, the Local Favor is the members' average, got " + town.Local);

        // The people walk from town to town, and the next refresh follows them.
        ResetWorld();
        ManagedFavorRegion west = FvTown("West", -100f, 0f, 50f);
        ManagedFavorRegion east = FvTown("East", 100f, 0f, 50f);
        ManagedNpcBrain walker = Villager(NpcRole.Merchant, new Vector3(-100f, 0f, 0f));
        FvGauge(walker, 80f);
        Advance(0.1f);
        Check(west.MemberCount == 1 && east.MemberCount == 0, "the walker counts toward the town it stands in");
        walker.transform.position = new Vector3(100f, 0f, 0f);
        Advance(0.6f);
        Check(west.MemberCount == 0 && east.MemberCount == 1, "and toward the other after it walks there, once the region has refreshed");

        // A home region holds the member in place however far it walks.
        ManagedFavorRegion home = west;
        Set(walker.GetComponent<ManagedNpcFavor>(), "_homeRegion", home);
        Advance(0.7f);
        Check(west.MemberCount == 1 && east.MemberCount == 0, "a walker with a home counts toward its home, not the town it is in");
    }

    // ---------- what the NPC sends to the decision ----------

    static void FavorObservation()
    {
        ResetWorld();
        ManagedFavorRegion town = FvTown("Town", 0f, 0f, 100f);
        town.SetCrowd(-60f);
        ManagedNpcBrain plain = Villager(NpcRole.Civilian, new Vector3(3f, 0f, 3f));
        Check(RgNear(plain.BuildObservation().Favor, -0.6f, 1e-6f), "an NPC with no gauge sends the crowd of its region, got " + plain.BuildObservation().Favor);
        ManagedNpcBrain proud = Villager(NpcRole.Merchant, new Vector3(-3f, 0f, 3f));
        ManagedNpcFavor pg = FvGauge(proud, 50f);
        Check(RgNear(proud.BuildObservation().Favor, 0.5f, 1e-6f), "an NPC with a gauge sends its own, whatever the crowd feels, got " + proud.BuildObservation().Favor);
        pg.Set(-100f);
        Check(proud.BuildObservation().Favor == -1f, "and it follows the gauge");
        pg.Set(100f);
        Check(proud.BuildObservation().Favor == 1f, "up to 1");
        ManagedNpcBrain wild = Villager(NpcRole.Civilian, new Vector3(500f, 0f, 0f));
        Check(wild.BuildObservation().Favor == 0f, "an NPC outside every region with no gauge sends neutral");
        ManagedNpcBrain wildGauge = Villager(NpcRole.Civilian, new Vector3(500f, 0f, 5f));
        FvGauge(wildGauge, 70f);
        Check(RgNear(wildGauge.BuildObservation().Favor, 0.7f, 1e-6f), "but an NPC outside every region with a gauge sends it");
        pg.enabled = false;
        Check(RgNear(proud.BuildObservation().Favor, -0.6f, 1e-6f), "an NPC whose gauge is switched off falls back to the crowd");
    }

    // ---------- what the decision does with it ----------

    static void FavorDecisions()
    {
        // A shopkeeper that likes the player keeps trading under the player's eye. One that does not stands still.
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter shop = RgShop(Vector3.zero, NpcDisposition.Hostile, false);
        ManagedNpcFavor sg = shop.Go.AddComponent<ManagedNpcFavor>();
        Tick(0.1f);
        Check(shop.Brain.ThreatVisible && shop.Brain.Action == NpcAction.Idle, "a neutral shopkeeper stands still under the player's eye, " + shop.Brain.Action);
        sg.Set(50f); Tick(0.1f);
        Check(shop.Brain.Action == NpcAction.Trade, "a shopkeeper at 50 keeps trading, " + shop.Brain.Action);
        sg.Set(20f); Tick(0.1f);
        Check(shop.Brain.Action == NpcAction.Trade, "20 is the line, and it is liked, " + shop.Brain.Action);
        sg.Set(19.9f); Tick(0.1f);
        Check(shop.Brain.Action == NpcAction.Idle, "just under it is not, " + shop.Brain.Action);
        sg.Set(-80f); Tick(0.1f);
        Check(shop.Brain.Action == NpcAction.Idle, "and a shopkeeper who hates the player stands still, " + shop.Brain.Action);

        // With no gauge it is the crowd of the town.
        ResetWorld();
        GameObject p2 = Player(10f, true); Clear(p2);
        ManagedFavorRegion town = FvTown("Town", 0f, 0f, 100f);
        Fighter stall = RgShop(Vector3.zero, NpcDisposition.Hostile, false);
        town.SetCrowd(50f); Tick(0.1f);
        Check(stall.Brain.Action == NpcAction.Trade, "a shopkeeper with no gauge in a town that likes the player trades, " + stall.Brain.Action);
        town.SetCrowd(-50f); Tick(0.1f);
        Check(stall.Brain.Action == NpcAction.Idle, "in a town that does not it stands still, " + stall.Brain.Action);
        stall.Go.transform.position = new Vector3(500f, 0f, 0f); p2.transform.position = new Vector3(500f, 0f, 10f); town.SetCrowd(90f); Tick(0.1f);
        Check(stall.Brain.Action == NpcAction.Idle, "outside every region the crowd has no say, " + stall.Brain.Action);

        // Once the player has attacked it, a shopkeeper is no longer a friend, however much it liked the player.
        foreach (NpcDisposition d in new[] { NpcDisposition.Retaliatory, NpcDisposition.Peaceful })
        {
            ResetWorld();
            GameObject p3 = Player(1.2f, true); Clear(p3);
            Fighter friend = RgShop(Vector3.zero, d, false);
            ManagedNpcFavor fg = friend.Go.AddComponent<ManagedNpcFavor>();
            fg.Set(90f);
            Tick(0.1f);
            Check(friend.Brain.Action == NpcAction.Trade, d + " shopkeeper that adores the player trades, " + friend.Brain.Action);
            friend.Health.TakeDamage(1f, p3);
            Check(fg.Value == 70f && fg.Tier == ManagedFavorTier.Adored, "the blow cost it 20 and it still adores the player, " + fg.Value);
            Tick(0.1f);
            NpcAction expected = d == NpcDisposition.Retaliatory ? NpcAction.Attack : NpcAction.Idle;
            Check(friend.Brain.Action == expected, d + " shopkeeper that was just attacked is no friend, it decides " + expected + ", got " + friend.Brain.Action);
        }

        // A civilian runs from the player, unless it likes the player.
        ResetWorld();
        GameObject p4 = Player(10f, true); Clear(p4);
        ManagedNpcBrain civ = FvNpc(NpcRole.Civilian, Vector3.zero);
        ManagedNpcFavor cg = civ.gameObject.AddComponent<ManagedNpcFavor>();
        Tick(0.1f);
        Check(civ.ThreatVisible && civ.Action == NpcAction.Retreat, "a neutral civilian runs from the player, " + civ.Action);
        cg.Set(50f); Tick(0.1f);
        Check(civ.Action == NpcAction.Patrol, "a civilian at 50 goes about its day, " + civ.Action);
        cg.Set(-50f); Tick(0.1f);
        Check(civ.Action == NpcAction.Retreat, "one at -50 runs, " + civ.Action);
        cg.Set(50f); Tick(0.1f);
        FvHit(civ, p4);
        Tick(0.1f);
        Check(civ.Provoked && cg.Value == 30f && civ.Action == NpcAction.Retreat, "a civilian the player has just hit runs even though it still likes the player, " + civ.Action + " at " + cg.Value);
        Tick(21f);
        Check(!civ.Provoked && civ.ThreatVisible && civ.Action == NpcAction.Patrol, "and once the memory of the blow fades it goes about its day again, " + civ.Action);

        // A civilian with no gauge feels the town.
        ResetWorld();
        GameObject p5 = Player(10f, true); Clear(p5);
        ManagedFavorRegion t5 = FvTown("Town", 0f, 0f, 100f);
        ManagedNpcBrain crowdCiv = FvNpc(NpcRole.Civilian, Vector3.zero);
        t5.SetCrowd(60f); Tick(0.1f);
        Check(crowdCiv.Action == NpcAction.Patrol, "a civilian in a town that adores the player does not run, " + crowdCiv.Action);
        t5.SetCrowd(-60f); Tick(0.1f);
        Check(crowdCiv.Action == NpcAction.Retreat, "in a town that hates the player it does, " + crowdCiv.Action);

        // Fighters do not care, whatever they feel.
        ResetWorld();
        GameObject p6 = Player(10f, true); Clear(p6);
        ManagedNpcBrain guard = GuardAt(Vector3.zero, NpcDisposition.Hostile);
        ManagedNpcFavor gg = guard.gameObject.AddComponent<ManagedNpcFavor>();
        gg.Set(100f);
        Fighter goblin = Goblin(new Vector3(4f, 0f, 0f));
        ManagedNpcFavor og = goblin.Go.AddComponent<ManagedNpcFavor>();
        og.Set(100f);
        Tick(0.1f);
        Check(guard.Action == NpcAction.Attack && goblin.Brain.Action == NpcAction.Attack, "a hostile guard and a goblin that adore the player still attack it, " + guard.Action + " and " + goblin.Brain.Action);
    }

    // ---------- what hurting somebody costs ----------

    static void FavorHurt()
    {
        // The victim loses its own favor, twenty for a blow and sixty for a kill, and a second blow at once is the same blow.
        ResetWorld();
        ManagedFavorRegion town = FvTown("Town", 0f, 0f, 100f);
        GameObject player = Player(10f, true); Clear(player);
        ManagedNpcBrain baker = Villager(NpcRole.Merchant, Vector3.zero);
        ManagedNpcFavor bg = FvGauge(baker, 50f);
        Tick(0.1f);
        FvHit(baker, player);
        Check(bg.Value == 30f, "a blow costs the victim 20, got " + bg.Value);
        FvHit(baker, player);
        Check(bg.Value == 30f, "a second blow inside three seconds is the same assault");
        Tick(3.1f);
        FvHit(baker, player);
        Check(bg.Value == 10f, "after the repeat time it counts again, got " + bg.Value);
        FvHit(baker, player, 1000f);
        Check(bg.Value == -50f && !bg.IsAlive, "the kill costs 60 and counts at once, got " + bg.Value);
        Check(town.MemberCount == 0, "and the dead baker no longer counts toward the town");

        // The relevant NPCs who watched lose a little, the ones who did not see it lose nothing.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true); Clear(player);
        baker = Villager(NpcRole.Merchant, Vector3.zero);
        ManagedNpcFavor victimGauge = FvGauge(baker, 0f);
        ManagedNpcBrain watcher = FvNpc(NpcRole.Civilian, new Vector3(3f, 0f, 0f));
        ManagedNpcFavor wg = FvGauge(watcher, 40f);
        ManagedNpcBrain blind = FvNpc(NpcRole.Civilian, new Vector3(0f, 0f, -20f));
        Set(blind, "_viewRange", 3f);
        ManagedNpcFavor ng = FvGauge(blind, 40f);
        FvNpc(NpcRole.Civilian, new Vector3(-3f, 0f, 0f));
        Tick(0.1f);
        Check(watcher.ThreatVisible && !blind.ThreatVisible, "one of them sees the player and the other does not");
        FvHit(baker, player);
        Check(wg.Value == 35f && ng.Value == 40f, "a blow costs the watcher 5 and the one who did not see it nothing, got " + wg.Value + " and " + ng.Value);
        Check(victimGauge.Value == -20f, "while the victim lost its own 20");
        FvHit(baker, player, 1000f);
        Check(wg.Value == 20f && ng.Value == 40f, "the kill costs the watcher 15, got " + wg.Value);

        // Somebody who is watching somebody else did not see it. An open field, and the nearest threat is a raider.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true);
        Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) => { h = default(RaycastHit); return false; };
        ManagedNpcBrain shop = Villager(NpcRole.Merchant, new Vector3(-5f, 0f, 0f));
        ManagedNpcBrain thug = Npc("thug", NpcRole.Enemy, DecisionBackend.StateMachine, true, new Vector3(0f, 0f, 2f), true);
        thug.gameObject.AddComponent<ManagedNpcThreatSource>();
        ManagedNpcBrain distracted = FvNpc(NpcRole.Civilian, Vector3.zero);
        ManagedNpcFavor dg = FvGauge(distracted, 40f);
        Tick(0.1f);
        Check(distracted.ThreatVisible && distracted.Threat != player.GetComponent<ManagedNpcThreatSource>(), "the nearest threat is the thug, not the player");
        FvHit(shop, player);
        Check(dg.Value == 40f && town.Crowd == -4f, "somebody watching the thug did not see the player's blow, but the town still pays, got " + dg.Value + " and " + town.Crowd);

        // The crowd of the town loses 4 for a blow and 12 for a kill, for a merchant, a civilian or a guard, and for nobody else.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true); Clear(player);
        ManagedNpcBrain m1 = Villager(NpcRole.Merchant, new Vector3(5f, 0f, 0f));
        ManagedNpcBrain m2 = Villager(NpcRole.Merchant, new Vector3(6f, 0f, 0f));
        ManagedNpcBrain c1 = Villager(NpcRole.Civilian, new Vector3(7f, 0f, 0f));
        ManagedNpcBrain g1 = GuardAt(new Vector3(8f, 0f, 0f));
        Tick(0.1f);
        FvHit(m1, player);
        Check(town.Crowd == -4f, "a blow on a merchant costs the crowd 4, got " + town.Crowd);
        FvHit(m2, player, 1000f);
        Check(town.Crowd == -16f, "a kill costs it 12, got " + town.Crowd);
        FvHit(c1, player);
        Check(town.Crowd == -20f, "a blow on a civilian costs 4, got " + town.Crowd);
        FvHit(g1, player);
        Check(town.Crowd == -24f, "and a blow on a guard 4, got " + town.Crowd);
        Check(town.Local == -24f, "and with no gauges the Local Favor is the crowd");
        ManagedNpcBrain enemy = Npc("bandit", NpcRole.Enemy, DecisionBackend.StateMachine, true, new Vector3(9f, 0f, 0f), true);
        ManagedNpcFavor eg = FvGauge(enemy, 10f);
        ManagedNpcBrain mate = Npc("mate", NpcRole.Companion, DecisionBackend.StateMachine, true, new Vector3(10f, 0f, 0f), true);
        ManagedNpcFavor mg = FvGauge(mate, 10f);
        ManagedNpcBrain onlooker = FvNpc(NpcRole.Civilian, new Vector3(0f, 0f, 3f));
        ManagedNpcFavor og = FvGauge(onlooker, 30f);
        Tick(0.1f);
        FvHit(enemy, player);
        FvHit(mate, player);
        Check(town.Crowd == -24f && og.Value == 30f, "hurting an enemy or a companion costs the town and the onlookers nothing, got crowd " + town.Crowd + " and onlooker " + og.Value);
        Check(eg.Value == -10f && mg.Value == -10f, "but each of them lost its own 20");
        town.SetCrowd(-98f);
        ManagedNpcBrain m3 = Villager(NpcRole.Merchant, new Vector3(11f, 0f, 0f));
        FvHit(m3, player);
        Check(town.Crowd == -100f, "the crowd stops at -100, got " + town.Crowd);

        // A victim outside every region still loses its own favor, and a blow is a new one after the default three seconds.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true); Clear(player);
        ManagedNpcBrain hermit = Villager(NpcRole.Merchant, new Vector3(500f, 0f, 0f));
        ManagedNpcFavor hg = FvGauge(hermit, 50f);
        FvHit(hermit, player);
        FvHit(hermit, player);
        Check(hg.Value == 30f && town.Crowd == 0f, "outside every region the victim still loses its own favor, and no crowd loses any, got " + hg.Value);
        Tick(3.1f);
        FvHit(hermit, player);
        Check(hg.Value == 10f, "and a blow after three seconds is a new one, got " + hg.Value);

        // The region sets how long a repeat blow waits.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        Set(town, "_assaultRepeatSeconds", 10f);
        player = Player(10f, true); Clear(player);
        ManagedNpcBrain slow = Villager(NpcRole.Merchant, Vector3.zero);
        ManagedNpcFavor sg = FvGauge(slow, 50f);
        FvHit(slow, player);
        Tick(5f);
        FvHit(slow, player);
        Check(sg.Value == 30f && town.Crowd == -4f, "inside a region that waits ten seconds, a blow after five is the same assault, got " + sg.Value);
        Tick(5.1f);
        FvHit(slow, player);
        Check(sg.Value == 10f && town.Crowd == -8f, "and one after ten is a new one, got " + sg.Value);

        // Only the player's blows count: not an NPC's, not a thing with no threat source, not a hit with no source.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true); Clear(player);
        ManagedNpcBrain target = Villager(NpcRole.Merchant, Vector3.zero);
        ManagedNpcFavor tg = FvGauge(target, 50f);
        ManagedNpcBrain watching = FvNpc(NpcRole.Civilian, new Vector3(3f, 0f, 0f));
        ManagedNpcFavor wag = FvGauge(watching, 40f);
        ManagedNpcBrain raider = Npc("raider", NpcRole.Enemy, DecisionBackend.StateMachine, true, new Vector3(0f, 0f, 5f), true);
        raider.gameObject.AddComponent<ManagedNpcThreatSource>();
        Tick(0.1f);
        FvHit(target, raider.gameObject);
        FvHit(target, new GameObject("someone"));
        target.GetComponent<ManagedHealth>().TakeDamage(1f);
        Check(tg.Value == 50f && wag.Value == 40f && town.Crowd == 0f, "a blow from an NPC, from a thing with no threat source and from nothing costs nobody anything, got " + tg.Value + ", " + wag.Value + ", " + town.Crowd);
        GameObject trap = new GameObject("trap");
        trap.AddComponent<ManagedNpcThreatSource>();
        FvHit(target, trap);
        Check(tg.Value == 30f && town.Crowd == -4f, "a thing with a threat source and no brain counts as the player, got " + tg.Value);

        // A victim with no gauge of its own still costs the town.
        ResetWorld();
        town = FvTown("Town", 0f, 0f, 100f);
        player = Player(10f, true); Clear(player);
        ManagedNpcBrain nobody = Villager(NpcRole.Civilian, Vector3.zero);
        FvHit(nobody, player);
        Check(town.Crowd == -4f && nobody.FavorGauge == null, "a victim with no gauge costs the town, and gets none");
    }

    // ---------- the debugger ----------

    static void FavorDebug()
    {
        ResetWorld();
        ManagedFavorRegion town = FvTown("Town", 0f, 0f, 100f);
        town.SetCrowd(10f);
        ManagedNpcBrain with = Villager(NpcRole.Merchant, Vector3.zero);
        FvGauge(with, 50f);
        ManagedNpcBrain without = Villager(NpcRole.Civilian, new Vector3(2f, 0f, 0f));
        string line = ManagedNpcDebugView.Describe(with);
        Check(line.Contains("favor 50 liked"), "an NPC with a gauge shows its favor in the list: " + line);
        Check(!ManagedNpcDebugView.Describe(without).Contains("favor"), "an NPC with none does not: " + ManagedNpcDebugView.Describe(without));

        UnityEngine.Application.isPlaying = true;
        ManagedNpcDebuggerWindow window = ManagedNpcDebuggerWindow.GetWindow<ManagedNpcDebuggerWindow>("GTG NPC Debugger");
        MethodInfo gui = typeof(ManagedNpcDebuggerWindow).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEngine.GUILayout.Labels.Clear();
        gui.Invoke(window, null);
        bool listed = false;
        foreach (string label in UnityEngine.GUILayout.Labels) if (label == "Favor: " + town.Describe()) listed = true;
        Check(listed, "the window lists the Local Favor of every region");
        UnityEngine.Application.isPlaying = false;
    }
}
