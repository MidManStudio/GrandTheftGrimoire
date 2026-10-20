// Checks for ranged and hybrid fighters and for shopkeepers that fight back: the aiming maths, the weapon (ammo,
// cooldown, line of fire), the shots in the air (arrows, bolts and spells), what the brain tells Rust about the fight,
// the shooters' own pool and rings in the engagement, and the actor's shooting, kiting and switching. Part of the
// Managed NPC harness, same Unity stand-ins and the same limits: a straight-line agent, a callback raycast for sight
// and line of fire, and a box sweep for the shots themselves. The sight and line of fire rays go through the callback,
// the arrows and spells fly through the box sweep, so a test can block one without the other.
using System;
using System.Collections.Generic;
using System.Reflection;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Magic;
using MidManStudio.Gtg.Managed.NPC;
using MidManStudio.Gtg.Managed.Rendering;
using MidManStudio.Gtg.NPC.Components;
using MidManStudio.Gtg.NPC.Native;
using UnityEngine;
using UnityEngine.AI;

// A body that is not a ManagedHealth. It counts every hit it is handed, even when it says it is dead.
internal sealed class RgTripwire : MonoBehaviour, IManagedDamageable
{
    public bool Alive = true;
    public int Calls;
    public bool IsAlive { get { return Alive; } }
    public void TakeDamage(float amount, GameObject source = null) { Calls++; }
}

internal static partial class Tests
{
    // ---------- helpers ----------

    static bool RgNear(float a, float b, float tolerance) { return Math.Abs(a - b) <= tolerance; }
    static bool SameSpot(Vector3 a, Vector3 b) { return RgNear(a.x, b.x, 1e-6f) && RgNear(a.y, b.y, 1e-6f) && RgNear(a.z, b.z, 1e-6f); }

    // A target with a body, a box from the feet to 2 m, so a shot aimed at the chest hits it. It has health and a threat
    // source like the player.
    static GameObject RgTarget(float x, float z, bool withHealth = true)
    {
        GameObject g = Player(z, withHealth);
        g.transform.position = new Vector3(x, 0f, z);
        Collider c = g.GetComponent<Collider>();
        c.center = new Vector3(0f, 1f, 0f);
        c.extents = new Vector3(0.5f, 1f, 0.5f);
        return g;
    }

    static float RgHealth(GameObject g) { return g.GetComponent<ManagedHealth>().Current; }

    // A weapon on its own object, with no brain or actor, for checks of the weapon and the shots.
    static ManagedNpcShooter RgBare(Vector3 position, ManagedNpcRangedKind kind = ManagedNpcRangedKind.Projectile)
    {
        GameObject g = new GameObject("shooter");
        g.transform.position = position;
        ManagedNpcShooter s = g.AddComponent<ManagedNpcShooter>();
        Set(s, "_kind", kind);
        Set(s, "_spreadDegrees", 0f);
        return s;
    }

    static ManagedNpcFireResult RgFire(ManagedNpcShooter s, GameObject target)
    {
        return s.TryFire(Time.time, target.GetComponent<ManagedNpcThreatSource>().AimPoint, target.transform);
    }

    static void RgStep(float seconds)
    {
        int n = (int)Math.Round(seconds / 0.01f);
        for (int i = 0; i < n; i++) ManagedNpcShots.Step(0.01f);
    }

    // A fighter with a weapon, built in the order a scene gets them: health, agent, brain, weapon, actor.
    static Fighter RgFighter(string name, Vector3 position, NpcRole role, AttackStyle style, bool canMove, bool withWeapon, ManagedNpcRangedKind kind)
    {
        Fighter f = new Fighter();
        f.Go = new GameObject(name); f.Go.transform.position = position;
        f.Health = f.Go.AddComponent<ManagedHealth>();
        f.Agent = f.Go.AddComponent<NavMeshAgent>();
        f.Brain = f.Go.AddComponent<ManagedNpcBrain>();
        Set(f.Brain, "_role", role); Set(f.Brain, "_backend", DecisionBackend.StateMachine); Set(f.Brain, "_canMove", canMove);
        Set(f.Brain, "_attackStyle", style); Set(f.Brain, "_viewRange", 40f);
        if (withWeapon)
        {
            ManagedNpcShooter s = f.Go.AddComponent<ManagedNpcShooter>();
            Set(s, "_kind", kind); Set(s, "_spreadDegrees", 0f);
        }
        f.Actor = f.Go.AddComponent<ManagedNpcActor>();
        f.Actor.Attacked += (a, t) => { f.Hits++; f.HitTimes.Add(Time.time); };
        return f;
    }

    static Fighter RgArcher(Vector3 position, AttackStyle style = AttackStyle.Ranged, bool canMove = true, ManagedNpcRangedKind kind = ManagedNpcRangedKind.Projectile)
    {
        return RgFighter("archer", position, NpcRole.Enemy, style, canMove, true, kind);
    }

    static ManagedNpcShooter RgGun(Fighter f) { return f.Go.GetComponent<ManagedNpcShooter>(); }

    // Counts the shots a fighter looses, with the planar distance to the target at each one.
    static List<float> RgWatch(Fighter f, GameObject target)
    {
        List<float> distances = new List<float>();
        RgGun(f).Fired += s =>
        {
            Vector3 d = target.transform.position - f.Go.transform.position; d.y = 0f;
            distances.Add(d.magnitude);
        };
        return distances;
    }

    // ---------- the aiming maths ----------

    // The height of a shot above its launch point at a horizontal distance, for a unit launch direction.
    static float RgHeightAt(Vector3 direction, float speed, float gravity, float horizontal)
    {
        float flat = (float)Math.Sqrt(direction.x * direction.x + direction.z * direction.z);
        float t = horizontal / (speed * flat);
        return speed * direction.y * t - 0.5f * gravity * t * t;
    }

    static float RgAngle(Vector3 a, Vector3 b)
    {
        // atan2 of the cross and the dot, so a tiny angle is not lost to the rounding of an arc cosine
        Vector3 na = a.normalized, nb = b.normalized;
        return (float)(Math.Atan2(Vector3.Cross(na, nb).magnitude, Vector3.Dot(na, nb)) * 180.0 / Math.PI);
    }

    static void RangedBallistics()
    {
        const float Speed = 35f, G = 4.905f;
        Vector3 dir;
        Vector3 from = new Vector3(0f, 1.4f, 0f);

        bool ok = ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 20f), Speed, G, out dir);
        Check(ok && RgNear(dir.magnitude, 1f, 1e-4f), "a level target 20 m away can be hit and the direction is a unit vector");
        Check(dir.y > 0.005f && Math.Abs(dir.x) < 1e-6f && dir.z > 0.99f, "the shot points a little above the line, straight along the ground line to the target");
        Check(RgNear(RgHeightAt(dir, Speed, G, 20f), 0f, 0.01f), "and falls back onto the target height there");

        // Targets near and far, above and below, to the side and behind. The path must pass through each of them.
        float[][] cases =
        {
            new[] { 0f, 0f, 5f }, new[] { 0f, 10f, 30f }, new[] { 0f, -8f, 30f }, new[] { 12f, 3f, -20f },
            new[] { -30f, 0f, 0.5f }, new[] { 3f, 0f, 200f }, new[] { 0f, 25f, 60f }, new[] { 0.2f, 0f, 0.2f }
        };
        bool allThrough = true, allTowards = true, allUnit = true;
        string bad = "";
        foreach (float[] c in cases)
        {
            Vector3 to = from + new Vector3(c[0], c[1], c[2]);
            if (!ManagedNpcBallistics.TryLaunchDirection(from, to, Speed, G, out dir)) { allThrough = false; bad += " unreachable " + c[0] + "," + c[1] + "," + c[2]; continue; }
            float d = (float)Math.Sqrt(c[0] * c[0] + c[2] * c[2]);
            if (!RgNear(RgHeightAt(dir, Speed, G, d), c[1], 0.02f * Math.Max(1f, d / 20f))) { allThrough = false; bad += " misses " + c[0] + "," + c[1] + "," + c[2]; }
            float flat = (float)Math.Sqrt(dir.x * dir.x + dir.z * dir.z);
            if (!RgNear(dir.x / flat, c[0] / d, 1e-3f) || !RgNear(dir.z / flat, c[2] / d, 1e-3f)) allTowards = false;
            if (!RgNear(dir.magnitude, 1f, 1e-3f)) allUnit = false;
        }
        Check(allThrough, "the path passes through every target, near and far, above, below, beside and behind:" + bad);
        Check(allTowards, "the horizontal heading is always toward the target");
        Check(allUnit, "every direction is a unit vector");

        // The flat path of the two that reach the point, so a shot does not lob when it can fly level.
        ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 100f), Speed, G, out dir);
        Check(dir.y / dir.z < 1f && dir.y > 0f, "the lower of the two arcs is taken, under 45 degrees");

        // The edge of reach is v squared over g, about 249.7 m for these numbers on level ground.
        Check(ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 249f), Speed, G, out dir), "249 m is within reach at this speed");
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 250f), Speed, G, out dir), "250 m is out of reach");
        Check(RgNear(dir.z, 1f, 1e-4f) && RgNear(dir.y, 0f, 1e-4f), "and an unreachable target gives the straight line to it");
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f + 200f, 100f), Speed, G, out dir), "a target far above is out of reach");

        // Without gravity it is the straight line.
        Check(ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(3f, 5.4f, 12f), Speed, 0f, out dir)
            && RgNear(RgAngle(dir, new Vector3(3f, 4f, 12f)), 0f, 0.01f), "with no gravity the shot flies along the line");
        Check(ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(3f, 5.4f, 12f), Speed, -2f, out dir)
            && RgNear(RgAngle(dir, new Vector3(3f, 4f, 12f)), 0f, 0.01f), "gravity that pulls up is treated as none");

        // Targets that give no angle to solve for.
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 11.4f, 0f), Speed, G, out dir) && RgNear(dir.y, 1f, 1e-4f), "straight above has no arc, the answer is the line up");
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, from, Speed, G, out dir) && RgNear(dir.magnitude, 1f, 1e-4f) && !float.IsNaN(dir.x + dir.y + dir.z), "the point itself has no direction, and the answer is still a unit vector and not NaN");
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 10f), 0f, G, out dir) && RgNear(dir.z, 1f, 1e-4f), "a zero speed hits nothing");
        Check(!ManagedNpcBallistics.TryLaunchDirection(from, new Vector3(0f, 1.4f, 10f), -5f, G, out dir), "a negative speed hits nothing");

        // Spread.
        Vector3 forward = new Vector3(0.3f, 0.2f, 1f).normalized;
        Vector3 r = ManagedNpcBallistics.Spread(forward, 0f, 1f, 0f);
        Check(r.x == forward.x && r.y == forward.y && r.z == forward.z, "zero spread returns the direction as it was");
        r = ManagedNpcBallistics.Spread(forward, float.NaN, 1f, 0f);
        Check(r.x == forward.x && r.y == forward.y && r.z == forward.z, "a spread that is not a number changes nothing");
        r = ManagedNpcBallistics.Spread(forward, -3f, 1f, 0f);
        Check(r.x == forward.x && r.y == forward.y && r.z == forward.z, "a negative spread changes nothing");
        r = ManagedNpcBallistics.Spread(forward, 5f, 0f, 0f);
        Check(RgNear(RgAngle(r, forward), 0f, 0.01f), "the centre of the disc keeps the aim");
        r = ManagedNpcBallistics.Spread(forward, 5f, 1f, 0f);
        Check(RgNear(RgAngle(r, forward), 5f, 0.01f) && RgNear(r.magnitude, 1f, 1e-4f), "the edge of the disc is exactly the spread away, and the result is a unit vector");
        r = ManagedNpcBallistics.Spread(forward, 5f, 0f, -1f);
        Check(RgNear(RgAngle(r, forward), 5f, 0.01f), "so is the edge in the other axis");
        bool within = true; float most = 0f; HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < 400; i++)
        {
            Vector2 p = UnityEngine.Random.insideUnitCircle;
            Vector3 s = ManagedNpcBallistics.Spread(forward, 5f, p.x, p.y);
            float a = RgAngle(s, forward);
            if (a > 5.01f || float.IsNaN(a)) within = false;
            most = Math.Max(most, a);
            seen.Add(s.x.ToString("F3") + s.y.ToString("F3"));
        }
        Check(within && most > 3.5f && seen.Count > 300, "four hundred random shots stay inside the cone, reach most of it and differ from one another (widest " + most.ToString("F2") + ")");
        r = ManagedNpcBallistics.Spread(new Vector3(0f, 1f, 0f), 5f, 1f, 0f);
        Check(RgNear(RgAngle(r, new Vector3(0f, 1f, 0f)), 5f, 0.01f) && !float.IsNaN(r.x + r.y + r.z), "straight up still spreads, there is no sideways to be undefined");
        r = ManagedNpcBallistics.Spread(new Vector3(0f, -1f, 0f), 5f, 0f, 1f);
        Check(RgNear(RgAngle(r, new Vector3(0f, -1f, 0f)), 5f, 0.01f) && !float.IsNaN(r.x + r.y + r.z), "and so does straight down");
        r = ManagedNpcBallistics.Spread(new Vector3(0f, 0f, 10f), 5f, 1f, 0f);
        Check(RgNear(RgAngle(r, new Vector3(0f, 0f, 1f)), 5f, 0.01f), "the direction does not have to be a unit vector");
    }

    // ---------- the weapon ----------

    static void RangedShooter()
    {
        ResetWorld();
        GameObject target = RgTarget(0f, 10f);
        ManagedNpcShooter s = RgBare(Vector3.zero);
        int fired = 0; s.Fired += x => fired++;
        Check(s.Kind == ManagedNpcRangedKind.Projectile && s.Capacity == 12f && s.Ammo == 12f && s.IsReady, "a new weapon is an arrow weapon and starts full");
        Check(s.Range == 20f && s.StandOffDistance == 12f, "with a reach of 20 m and a stand-off of 12 m");
        Check(RgNear(s.ShotPoint.x, 0.3f, 1e-4f) && RgNear(s.ShotPoint.y, 1.4f, 1e-4f) && RgNear(s.ShotPoint.z, 0.5f, 1e-4f), "shots leave from the shot point in the NPC's own space");
        s.gameObject.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up);
        Check(RgNear(s.ShotPoint.x, 0.5f, 1e-3f) && RgNear(s.ShotPoint.z, -0.3f, 1e-3f), "and the shot point turns with the NPC");
        s.gameObject.transform.rotation = Quaternion.identity;

        Check(RgFire(s, target) == ManagedNpcFireResult.Fired, "a ready weapon with a clear line fires");
        Check(s.Ammo == 11f && fired == 1 && ManagedNpcShots.Count == 1 && ManagedNpcShots.IsProjectileAt(0), "one unit of ammo is spent, the event is raised once and one arrow is in the air");
        Vector3 v = ManagedNpcShots.VelocityAt(0);
        Check(RgNear(v.magnitude, 35f, 0.01f), "the arrow leaves at the projectile speed, " + v.magnitude.ToString("F2"));
        Check(RgFire(s, target) == ManagedNpcFireResult.Waiting && s.Ammo == 11f && fired == 1 && ManagedNpcShots.Count == 1, "firing again at once waits on the cooldown and spends nothing");
        Time.time = 1.49f;
        Check(RgFire(s, target) == ManagedNpcFireResult.Waiting, "still waiting just before the cooldown ends");
        Time.time = 1.5f;
        Check(RgFire(s, target) == ManagedNpcFireResult.Fired && fired == 2 && ManagedNpcShots.Count == 2, "and fires when it ends");

        // Ammo.
        s.AddAmmo(-100f);
        Check(s.Ammo == 0f && !s.IsReady, "ammo cannot go below zero");
        Time.time = 10f;
        Check(RgFire(s, target) == ManagedNpcFireResult.NoAmmo && fired == 2, "with no ammo it reports NoAmmo and fires nothing");
        s.AddAmmo(100f);
        Check(s.Ammo == 12f, "ammo cannot go above the capacity");
        s.AddAmmo(-11.5f);
        Check(RgNear(s.Ammo, 0.5f, 1e-4f) && !s.IsReady, "half a shot is not a shot");
        s.AddAmmo(0.5f);
        Check(s.IsReady, "and a whole one is");
        s.Refill();
        Check(s.Ammo == 12f, "Refill fills the pool");

        // Regeneration. A quiver does not refill, a magic user's mana does.
        ResetWorld();
        ManagedNpcShooter quiver = RgBare(Vector3.zero);
        quiver.AddAmmo(-100f);
        Time.time = 100f;
        Check(quiver.Ammo == 0f, "a weapon with no regeneration stays empty however long it waits");
        ManagedNpcShooter mana = RgBare(Vector3.zero);
        Set(mana, "_regenSecondsPerShot", 2f);
        mana.AddAmmo(-100f);
        Time.time = 101f;
        Check(RgNear(mana.Ammo, 0.5f, 1e-3f) && !mana.IsReady, "a shot comes back every two seconds, half of one after one second");
        Time.time = 102f;
        Check(mana.IsReady && RgNear(mana.Ammo, 1f, 1e-3f), "and is ready after two");
        Time.time = 1000f;
        Check(mana.Ammo == 12f, "regeneration stops at the capacity");
        mana.AddAmmo(-100f); Time.time = 1001f; mana.AddAmmo(-100f);
        Check(mana.Ammo == 0f, "taking ammo away also takes the part that had come back");

        // The line of fire.
        ResetWorld();
        GameObject t2 = RgTarget(0f, 10f);
        ManagedNpcShooter s2 = RgBare(Vector3.zero);
        int fired2 = 0; s2.Fired += x => fired2++;
        Clear(t2);
        Check(RgFire(s2, t2) == ManagedNpcFireResult.Fired, "a ray that ends at the target itself is a clear line");
        ManagedNpcShots.Clear();
        s2.Refill();
        Wall();
        Time.time = 5f;
        Check(RgFire(s2, t2) == ManagedNpcFireResult.Blocked && s2.Ammo == 12f && fired2 == 1 && ManagedNpcShots.Count == 0, "a wall in the way blocks the shot and spends nothing");
        Clear(t2);
        Check(RgFire(s2, t2) == ManagedNpcFireResult.Fired, "a blocked try starts no cooldown, so the next try fires at the same moment");
        ManagedNpcShots.Clear();
        Time.time = 10f;
        Collider own = s2.gameObject.AddComponent<Collider>();
        Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) => { h = new RaycastHit { collider = own }; return true; };
        Check(RgFire(s2, t2) == ManagedNpcFireResult.Fired, "the weapon's own body is not in the way of its own shot");
        ManagedNpcShots.Clear();
        Time.time = 20f;
        Physics.OnRaycast = null;
        Check(s2.TryFire(Time.time, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.Fired, "a point with no target aimed at is fine");
        ManagedNpcShots.Clear();
        Wall();
        Time.time = 30f;
        Check(s2.TryFire(Time.time, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.Blocked, "with no target named, anything hit is in the way");
        Physics.OnRaycast = null;

        // A shot that would be born inside something.
        ResetWorld();
        ManagedNpcShooter s3 = RgBare(Vector3.zero);
        GameObject post = new GameObject("post"); post.transform.position = s3.ShotPoint;
        post.AddComponent<Collider>();
        GameObject t3 = RgTarget(0f, 10f);
        Check(RgFire(s3, t3) == ManagedNpcFireResult.Blocked && s3.Ammo == 12f && ManagedNpcShots.Count == 0, "an arrow that would start inside a wall is blocked and costs nothing");
        ManagedNpcShooter mage = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        Check(RgFire(mage, t3) == ManagedNpcFireResult.Blocked && mage.Ammo == 12f && ManagedNpcShots.Count == 0, "and so is a spell");

        // Out of reach for the arrow's speed.
        ResetWorld();
        ManagedNpcShooter slow = RgBare(Vector3.zero);
        Set(slow, "_projectileSpeed", 10f);
        Check(slow.TryFire(0f, new Vector3(0f, 1.4f, 100f), null) == ManagedNpcFireResult.Blocked && slow.Ammo == 12f && ManagedNpcShots.Count == 0, "a point the arrow cannot reach at its speed is not fired at, and nothing is spent");
        Check(slow.TryFire(0f, new Vector3(0f, 1.4f, 8f), null) == ManagedNpcFireResult.Fired, "a nearer one is");

        // A full sky.
        ResetWorld();
        ManagedNpcShooter crowd = RgBare(Vector3.zero);
        int crowdFired = 0; crowd.Fired += x => crowdFired++;
        for (int i = 0; i < ManagedNpcShots.MaxShots; i++)
            if (!ManagedNpcShots.AddProjectile(new Vector3(100f, 50f, 100f), new Vector3(0f, 0f, 1f), 0f, 1f, 0.1f, 0.15f, Color.red, 4f, null)) Check(false, "room for shot " + i);
        Check(ManagedNpcShots.Count == ManagedNpcShots.MaxShots, "the sky holds " + ManagedNpcShots.MaxShots + " shots");
        Check(!ManagedNpcShots.AddProjectile(Vector3.zero, new Vector3(0f, 0f, 1f), 0f, 1f, 0.1f, 0.15f, Color.red, 4f, null) && ManagedNpcShots.Count == ManagedNpcShots.MaxShots, "one more is refused and nothing changes");
        Check(crowd.TryFire(0f, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.Blocked && crowd.Ammo == 12f && crowdFired == 0, "a weapon whose shot does not fit keeps its ammo and raises no event");
        Check(crowd.TryFire(0f, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.Blocked, "and has started no cooldown, the next try is not Waiting");
        ManagedNpcShots.Clear();
        Check(crowd.TryFire(0f, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.Fired, "once there is room it fires");

        // A spell weapon.
        ResetWorld();
        ManagedNpcShooter caster = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        GameObject t4 = RgTarget(0f, 10f);
        Check(RgFire(caster, t4) == ManagedNpcFireResult.Fired && caster.Ammo == 11f && ManagedNpcShots.Count == 1 && !ManagedNpcShots.IsProjectileAt(0), "a spell weapon fires a spell and spends one unit");
        Check(RgNear(ManagedNpcShots.VelocityAt(0).magnitude, ManagedSpellDefinition.Fireball.Speed, 0.01f), "at the speed of the Fireball entry in the spell table");
        Vector3 toTarget = (t4.GetComponent<ManagedNpcThreatSource>().AimPoint - caster.ShotPoint).normalized;
        Check(RgAngle(ManagedNpcShots.VelocityAt(0), toTarget) < 0.01f, "flying straight along the line to the target, a spell is not lobbed");

        // Spread: a zero spread is exact, a wide one stays inside its cone.
        ResetWorld();
        GameObject t5 = RgTarget(0f, 30f);
        ManagedNpcShooter exact = RgBare(Vector3.zero);
        Set(exact, "_cooldownSeconds", 0f);
        RgFire(exact, t5);
        Vector3 first = ManagedNpcShots.VelocityAt(0);
        ManagedNpcShots.Clear();
        RgFire(exact, t5);
        Check(RgAngle(first, ManagedNpcShots.VelocityAt(0)) < 1e-3f, "with no spread two shots at the same point leave the same way");
        ManagedNpcShots.Clear();
        ManagedNpcShooter loose = RgBare(Vector3.zero);
        Set(loose, "_spreadDegrees", 5f); Set(loose, "_cooldownSeconds", 0f);
        List<Vector3> dirs = new List<Vector3>();
        for (int i = 0; i < 12; i++) { RgFire(loose, t5); dirs.Add(ManagedNpcShots.VelocityAt(ManagedNpcShots.Count - 1)); }
        float widest = 0f; HashSet<string> distinct = new HashSet<string>();
        foreach (Vector3 d in dirs) { widest = Math.Max(widest, RgAngle(d, first)); distinct.Add(d.x.ToString("F4") + d.y.ToString("F4")); }
        Check(widest <= 5.01f + 0.01f && distinct.Count == 12, "twelve shots with a 5 degree spread all differ and none strays beyond it (widest " + widest.ToString("F2") + ")");
        ManagedNpcShots.Clear();

        // Gravity: a weapon with no gravity on its arrows aims straight, the world's gravity is read when firing.
        ResetWorld();
        ManagedNpcShooter flat = RgBare(Vector3.zero);
        Set(flat, "_projectileGravityScale", 0f);
        flat.TryFire(0f, new Vector3(0f, 1.4f, 40f), null);
        Check(RgAngle(ManagedNpcShots.VelocityAt(0), new Vector3(-0.3f, 0f, 39.5f)) < 0.01f, "an arrow with no gravity scale flies the straight line");
        ManagedNpcShots.Clear();
        ManagedNpcShooter arc = RgBare(Vector3.zero);
        arc.TryFire(0f, new Vector3(0f, 1.4f, 40f), null);
        float rise = ManagedNpcShots.VelocityAt(0).y;
        ManagedNpcShots.Clear();
        Physics.gravity = new Vector3(0f, -19.62f, 0f);
        ManagedNpcShooter heavy = RgBare(Vector3.zero);
        heavy.TryFire(0f, new Vector3(0f, 1.4f, 40f), null);
        Check(ManagedNpcShots.VelocityAt(0).y > rise * 1.8f, "in a world with twice the gravity the arrow is aimed higher, " + rise.ToString("F2") + " then " + ManagedNpcShots.VelocityAt(0).y.ToString("F2"));
        Physics.gravity = new Vector3(0f, -9.81f, 0f);
        ManagedNpcShots.Clear();

        // The clamps an author's numbers get.
        ResetWorld();
        ManagedNpcShooter c = RgBare(Vector3.zero);
        MethodInfo validate = typeof(ManagedNpcShooter).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic);
        float kite = NPCManagedFallback.KiteDistance;
        Set(c, "_range", 3f); Set(c, "_standOffDistance", 2f);
        validate.Invoke(c, null);
        Check(c.Range == kite + 1f && c.StandOffDistance == kite + 1f, "a range and a stand-off inside the kite line are lifted to one meter past it, so a shooter is not told to back away from its own place");
        Set(c, "_range", 20f); Set(c, "_standOffDistance", 50f);
        validate.Invoke(c, null);
        Check(c.StandOffDistance == 20f, "a stand-off beyond the range is brought back to the range");
        Set(c, "_capacity", -4f); Set(c, "_spreadDegrees", 400f); Set(c, "_projectileSpeed", 0f); Set(c, "_cooldownSeconds", -1f);
        validate.Invoke(c, null);
        Check(c.Capacity == 0f && c.Kind == ManagedNpcRangedKind.Projectile, "a negative capacity is zero");
        c.Refill();
        Check(!c.IsReady && c.TryFire(0f, new Vector3(0f, 1.4f, 10f), null) == ManagedNpcFireResult.NoAmmo, "and a weapon with a capacity of zero never fires");
    }

    // ---------- arrows in the air ----------

    static void RangedShotFlight()
    {
        // An arrow hits what stands in its way and hurts it, and the hurt carries the shooter as its source.
        ResetWorld();
        GameObject target = RgTarget(0f, 15f);
        ManagedNpcShooter s = RgBare(Vector3.zero);
        ManagedHealth health = target.GetComponent<ManagedHealth>();
        GameObject lastSource = null; int hurt = 0;
        health.Damaged += (h, a, src) => { hurt++; lastSource = src; };
        Check(RgFire(s, target) == ManagedNpcFireResult.Fired, "the arrow is fired");
        RgStep(0.1f);
        Check(ManagedNpcShots.Count == 1 && ManagedNpcShots.PositionAt(0).z > 3f && ManagedNpcShots.PositionAt(0).z < 5f, "it flies at about 35 m a second, " + ManagedNpcShots.PositionAt(0).z.ToString("F2") + " m out after a tenth");
        RgStep(1f);
        Check(ManagedNpcShots.Count == 0, "and is gone once it hits");
        Check(hurt == 1 && RgNear(health.Current, 88f, 1e-3f), "the target took one hit of 12");
        Check(ReferenceEquals(lastSource, s.gameObject), "from the shooter");

        // A dead target is not hurt again.
        RgFire(s, target);
        health.TakeDamage(1000f);
        int hurtBefore = hurt;
        Time.time += 3f; RgFire(s, target);
        RgStep(1f);
        Check(ManagedNpcShots.Count == 0 && hurt == hurtBefore, "arrows into a body that is already down hurt nothing more");

        // The shot itself skips a body that reports itself down, whatever that body does with the hit. A body that counts
        // every call, even on the dead, shows it.
        ResetWorld();
        GameObject tripBody = RgTarget(0f, 10f, false);
        RgTripwire trip = tripBody.AddComponent<RgTripwire>();
        ManagedNpcShooter s8 = RgBare(Vector3.zero);
        Check(RgFire(s8, tripBody) == ManagedNpcFireResult.Fired, "a shot at a body that is not a ManagedHealth is fired");
        RgStep(1f);
        Check(trip.Calls == 1, "a living body is hurt once, got " + trip.Calls);
        trip.Alive = false;
        Time.time += 3f; RgFire(s8, tripBody);
        RgStep(1f);
        Check(ManagedNpcShots.Count == 0 && trip.Calls == 1, "a body that says it is down is not hurt at all, got " + trip.Calls + " calls");

        // It arcs. Over 40 m the arrow rises above the line it started on and still reaches the chest.
        ResetWorld();
        GameObject far = RgTarget(0f, 40f);
        ManagedNpcShooter s2 = RgBare(Vector3.zero);
        float startY = s2.ShotPoint.y, peak = startY; int steps = 0;
        RgFire(s2, far);
        while (ManagedNpcShots.Count > 0 && steps < 300) { peak = Math.Max(peak, ManagedNpcShots.PositionAt(0).y); ManagedNpcShots.Step(0.01f); steps++; }
        Check(peak > startY + 0.3f, "over 40 m the arrow climbs " + (peak - startY).ToString("F2") + " m above where it left");
        Check(RgNear(RgHealth(far), 88f, 1e-3f), "and still lands in the body");

        ResetWorld();
        GameObject far2 = RgTarget(0f, 40f);
        ManagedNpcShooter s3 = RgBare(Vector3.zero);
        Set(s3, "_projectileGravityScale", 0f);
        float peak3 = -100f; steps = 0;
        RgFire(s3, far2);
        while (ManagedNpcShots.Count > 0 && steps < 300) { peak3 = Math.Max(peak3, ManagedNpcShots.PositionAt(0).y); ManagedNpcShots.Step(0.01f); steps++; }
        Check(peak3 <= s3.ShotPoint.y + 1e-3f && RgNear(RgHealth(far2), 88f, 1e-3f), "with no gravity scale it flies straight, never above where it left, and still hits");

        // A miss ends at the time cap.
        ResetWorld();
        ManagedNpcShooter s4 = RgBare(Vector3.zero);
        s4.TryFire(0f, new Vector3(0f, 1.4f, 15f), null);
        RgStep(3.5f);
        Check(ManagedNpcShots.Count == 1, "a missed arrow is still in the air after three and a half seconds");
        RgStep(1f);
        Check(ManagedNpcShots.Count == 0, "and gone after four");

        // A wall stops it, and the thing behind is not hurt.
        ResetWorld();
        GameObject behind = RgTarget(0f, 15f);
        GameObject wall = new GameObject("wall"); wall.transform.position = new Vector3(0f, 1.5f, 8f);
        Collider wc = wall.AddComponent<Collider>(); wc.extents = new Vector3(3f, 3f, 0.25f);
        ManagedNpcShooter s5 = RgBare(Vector3.zero);
        Check(s5.TryFire(0f, behind.GetComponent<ManagedNpcThreatSource>().AimPoint, behind.transform) == ManagedNpcFireResult.Fired, "the shooter's own sight ray is a callback, so it fires, and the flight is what the wall stops");
        float lastZ = 0f; steps = 0;
        while (ManagedNpcShots.Count > 0 && steps < 300) { lastZ = ManagedNpcShots.PositionAt(0).z; ManagedNpcShots.Step(0.01f); steps++; }
        Check(lastZ > 7f && lastZ < 8f, "the arrow stops at the wall, last seen " + lastZ.ToString("F2") + " m out");
        Check(RgHealth(behind) == 100f, "and what stands behind the wall is not hurt");

        // Friendly fire: whatever is hit is hurt, an ally in the line included.
        ResetWorld();
        GameObject ally = RgTarget(0f, 7f);
        GameObject enemy = RgTarget(0f, 15f);
        ManagedNpcShooter s6 = RgBare(Vector3.zero);
        RgFire(s6, enemy);
        RgStep(1f);
        Check(RgHealth(ally) < 100f && RgHealth(enemy) == 100f, "an arrow hits the first body in its way, friend or not");

        // The shooter's own body is skipped.
        ResetWorld();
        GameObject t7 = RgTarget(0f, 15f);
        ManagedNpcShooter s7 = RgBare(Vector3.zero);
        s7.gameObject.AddComponent<ManagedHealth>();
        Collider body = s7.gameObject.AddComponent<Collider>(); body.center = new Vector3(0f, 1f, 0f); body.extents = new Vector3(0.5f, 1f, 0.5f);
        Check(RgFire(s7, t7) == ManagedNpcFireResult.Fired, "a shooter with a body of its own fires");
        RgStep(1f);
        Check(RgHealth(s7.gameObject) == 100f && RgHealth(t7) == 88f, "the arrow leaves its own body and hits the target");

        // Each shot keeps its own numbers when others leave the air before it.
        ResetWorld();
        GameObject[] victims = { RgTarget(-4f, 10f), RgTarget(0f, 20f), RgTarget(4f, 30f) };
        float[] damage = { 5f, 7f, 9f }, size = { 0.11f, 0.22f, 0.33f };
        Color[] colors = { new Color(1f, 0f, 0f, 1f), new Color(0f, 1f, 0f, 1f), new Color(0f, 0f, 1f, 1f) };
        float[] sweep = { 0.05f, 0.07f, 0.09f };
        ManagedNpcShooter[] archers = new ManagedNpcShooter[3];
        GameObject[] hitBy = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            int which = i;
            victims[i].GetComponent<ManagedHealth>().Damaged += (h, a, src) => { hitBy[which] = src; };
            ManagedNpcShooter sh = RgBare(new Vector3(victims[i].transform.position.x, 0f, 0f));
            archers[i] = sh;
            Set(sh, "_projectileDamage", damage[i]); Set(sh, "_projectileDiameter", size[i]); Set(sh, "_projectileColor", colors[i]); Set(sh, "_projectileRadius", sweep[i]);
            RgFire(sh, victims[i]);
        }
        Check(ManagedNpcShots.Count == 3, "three arrows are in the air");
        ManagedSphereBatch batch = new ManagedSphereBatch(ManagedNpcShots.MaxShots, 0, false, "test");
        ManagedNpcShots.FillBatch(batch);
        Check(batch.Count == 3, "one sphere is added to the batch for each");
        RgStep(0.35f);
        Check(ManagedNpcShots.Count == 2, "the nearest hit first, two remain");
        ManagedNpcShots.FillBatch(batch);
        Check(batch.Count == 2 && RgNear(batch.LastScale.x, size[1], 1e-4f) && batch.LastColor.g == 1f && batch.LastColor.r == 0f, "the batch is cleared and refilled, and the shot that stayed put still has its own size and color");
        // The far arrow was swapped into the freed place at the front. Its size, color and sweep radius must have gone with it.
        float[] radii = (float[])typeof(ManagedNpcShots).GetField("_radius", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Check(RgNear(radii[0], sweep[2], 1e-6f) && RgNear(radii[1], sweep[1], 1e-6f), "the sweep radius of each shot moves with it, got " + radii[0] + " and " + radii[1]);
        Check(batch.Colors.Count == 2 && batch.Colors[0].b == 1f && batch.Colors[0].r == 0f && RgNear(batch.Scales[0].x, size[2], 1e-4f), "and the shot that took the freed place carries its own size and color, not the one that left");
        RgStep(2f);
        Check(ManagedNpcShots.Count == 0, "all three are spent");
        Check(ReferenceEquals(hitBy[0], archers[0].gameObject) && ReferenceEquals(hitBy[1], archers[1].gameObject) && ReferenceEquals(hitBy[2], archers[2].gameObject), "and each target was hit by the shooter that aimed at it, whichever place its arrow ended up in");
        Check(RgNear(RgHealth(victims[0]), 100f - damage[0], 1e-3f) && RgNear(RgHealth(victims[1]), 100f - damage[1], 1e-3f) && RgNear(RgHealth(victims[2]), 100f - damage[2], 1e-3f),
            "each target lost exactly the damage of the arrow aimed at it: " + RgHealth(victims[0]) + ", " + RgHealth(victims[1]) + ", " + RgHealth(victims[2]));

        // A fast shot does not pass through a thin wall.
        ResetWorld();
        GameObject thin = new GameObject("thin"); thin.transform.position = new Vector3(0f, 1f, 7.3f);
        Collider tc = thin.AddComponent<Collider>(); tc.extents = new Vector3(3f, 3f, 0.01f);
        ManagedNpcShots.AddProjectile(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 200f), 0f, 1f, 0.1f, 0.15f, Color.red, 4f, null);
        ManagedNpcShots.Step(0.05f);
        Check(ManagedNpcShots.Count == 0, "a shot that moves ten meters in one step is stopped by a wall a centimeter thick");
        UnityEngine.Object.All.Remove(tc);
        ManagedNpcShots.AddProjectile(new Vector3(0f, 1f, 0f), new Vector3(0f, 0f, 200f), 0f, 1f, 0.1f, 0.15f, Color.red, 4f, null);
        ManagedNpcShots.Step(0.05f);
        Check(ManagedNpcShots.Count == 1 && RgNear(ManagedNpcShots.PositionAt(0).z, 10f, 0.01f), "and with the wall gone the same shot flies on");

        // The hidden driver.
        ResetWorld();
        int made = ManagedSphereBatch.Created, gone = ManagedSphereBatch.Disposed;
        Check(UnityEngine.Object.All.FindAll(c => c is ManagedNpcShotDriver).Count == 0, "no driver exists before the first shot");
        ManagedNpcShooter d1 = RgBare(Vector3.zero);
        GameObject t8 = RgTarget(0f, 15f);
        RgFire(d1, t8);
        Check(UnityEngine.Object.All.FindAll(c => c is ManagedNpcShotDriver).Count == 1 && ManagedSphereBatch.Created == made + 1, "the first shot creates one driver and one batch");
        Time.time += 2f; RgFire(d1, t8);
        Check(UnityEngine.Object.All.FindAll(c => c is ManagedNpcShotDriver).Count == 1 && ManagedSphereBatch.Created == made + 1, "more shots reuse it");
        ManagedNpcShotDriver driver = (ManagedNpcShotDriver)UnityEngine.Object.All.Find(c => c is ManagedNpcShotDriver);
        Check(driver.gameObject.name == "GTG NPC Shots", "the driver object is named for what it does");
        UnityEngine.Component.Invoke(driver, "LateUpdate");
        ManagedSphereBatch drawn = (ManagedSphereBatch)typeof(ManagedNpcShotDriver).GetField("_batch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);
        Check(drawn.Count == 2 && drawn.DrawCalls == 1, "late update fills the batch with one sphere per shot and draws it once");
        Advance(1f);
        Check(RgNear(RgHealth(t8), 76f, 1e-3f) && ManagedNpcShots.Count == 0, "the driver's update flies the shots on its own, both arrows hit");
        UnityEngine.Component.Invoke(driver, "LateUpdate");
        Check(drawn.Count == 0 && drawn.DrawCalls == 2, "and the next late update draws an empty batch");
        Time.time += 2f; RgFire(d1, t8);
        UnityEngine.Component.Invoke(driver, "OnDestroy");
        Check(ManagedSphereBatch.Disposed == gone + 1 && ManagedNpcShots.Count == 0, "destroying the driver releases its batch and takes the shots in the air with it");
        Time.time += 2f; RgFire(d1, t8);
        Check(ManagedSphereBatch.Created == made + 2 && ManagedNpcShots.Count == 1, "the next shot makes a new driver");

        // A driver added by hand in a scene, as a setup mistake. The first one is adopted and does the work, so no hidden
        // one is made, and any other one removes itself with a warning, so a shot is never stepped twice a frame.
        ResetWorld();
        int before = ManagedSphereBatch.Created;
        ManagedNpcShooter d2 = RgBare(Vector3.zero);
        GameObject t9 = RgTarget(0f, 15f);
        GameObject placed = new GameObject("Npc_Manager");
        ManagedNpcShotDriver mine = placed.AddComponent<ManagedNpcShotDriver>();
        Check(ManagedSphereBatch.Created == before + 1 && UnityEngine.Object.Destroyed.Count == 0 && Debug.Warnings.Count == 0, "a driver placed in a scene before any shot is adopted without a word");
        RgFire(d2, t9);
        Check(UnityEngine.Object.All.FindAll(c => c is ManagedNpcShotDriver).Count == 1 && ManagedSphereBatch.Created == before + 1, "so the first shot makes no hidden driver");
        Advance(1f);
        Check(RgNear(RgHealth(t9), 88f, 1e-3f) && ManagedNpcShots.Count == 0, "and the placed driver flies the shots");
        GameObject extra = new GameObject("Npc_Ranged");
        ManagedNpcShotDriver second = extra.AddComponent<ManagedNpcShotDriver>();
        Check(ManagedSphereBatch.Created == before + 1, "a second driver makes no batch");
        Check(UnityEngine.Object.Destroyed.Count == 1 && UnityEngine.Object.Destroyed[0].Key == second, "and is removed");
        Check(Debug.Warnings.Count == 1 && Debug.Warnings[0].Contains("Npc_Ranged") && Debug.Warnings[0].Contains("ManagedNpcShotDriver"), "with a warning that names the object");
        Time.time += 2f; RgFire(d2, t9);
        Vector3 held = ManagedNpcShots.PositionAt(0);
        UnityEngine.Component.Invoke(second, "Update");
        Check(ManagedNpcShots.Count == 1 && SameSpot(ManagedNpcShots.PositionAt(0), held), "a removed driver that still gets an update does not step the shots");
        UnityEngine.Component.Invoke(second, "OnDestroy");
        Check(ManagedNpcShots.Count == 1, "and its removal does not take the shots in the air with it");
        UnityEngine.Component.Invoke(mine, "Update");
        Check(ManagedNpcShots.Count == 1 && !SameSpot(ManagedNpcShots.PositionAt(0), held), "the adopted driver is the one that steps them");
    }

    // ---------- spells in the air ----------

    static void RangedSpellShots()
    {
        ResetWorld();
        List<ManagedSpellImpact> impacts = new List<ManagedSpellImpact>();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };

        GameObject target = RgTarget(0f, 15f);
        GameObject neighbour = RgTarget(2.5f, 15f);
        ManagedNpcShooter mage = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        Check(RgFire(mage, target) == ManagedNpcFireResult.Fired, "the spell is cast");
        RgStep(1.5f);
        Check(ManagedNpcShots.Count == 0 && impacts.Count == 1, "it ends in exactly one impact");
        ManagedSpellImpact hit = impacts.Count > 0 ? impacts[0] : default(ManagedSpellImpact);
        Check(hit.Kind == ManagedSpellKind.Fireball && hit.PayloadId == ManagedSpellPayloads.IdFor(ManagedSpellKind.Fireball), "it is the Fireball and carries its payload");
        Check(ReferenceEquals(hit.Source, mage.gameObject), "the impact names the mage as its source, so its own spell can be told from anyone else's");
        Check(hit.Position.z > 13.5f && hit.Position.z < 15f, "the impact is on the near face of the target, " + hit.Position.z.ToString("F2") + " m out");
        float lostMain = 100f - RgHealth(target), lostSide = 100f - RgHealth(neighbour);
        ManagedSpellDefinition fire = ManagedSpellDefinition.Fireball;
        Check(lostMain >= fire.ImpactDamage * fire.ImpactEdgeFraction - 1e-3f && lostMain <= fire.ImpactDamage + 1e-3f, "the target loses between the edge and full damage of the Fireball, " + lostMain.ToString("F1"));
        Check(lostSide > 0f && lostSide < lostMain, "a body beside it is hurt by the blast, less, " + lostSide.ToString("F1"));

        // The caster is not hurt by its own spell, even with the blast on top of it.
        ResetWorld();
        impacts.Clear();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };
        GameObject close = RgTarget(0f, 3.2f);
        ManagedNpcShooter self = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        self.gameObject.AddComponent<ManagedHealth>();
        Collider selfBody = self.gameObject.AddComponent<Collider>(); selfBody.center = new Vector3(0f, 1f, 0f); selfBody.extents = new Vector3(0.5f, 1f, 0.5f);
        RgFire(self, close);
        RgStep(1f);
        Check(impacts.Count == 1 && RgHealth(close) < 100f, "a spell cast at something 3 m away explodes on it");
        Check(RgHealth(self.gameObject) == 100f, "and the mage standing inside the blast is not hurt");
        ManagedSpellDamage.HurtCaster = true;
        impacts.Clear();
        Time.time += 3f; RgFire(self, close);
        RgStep(1f);
        Check(RgHealth(self.gameObject) < 100f, "unless the game turns that on");
        ManagedSpellDamage.HurtCaster = false;

        // No hit: the spell runs out of SP, collapses and releases its payload where it stopped.
        ResetWorld();
        impacts.Clear();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };
        ManagedNpcShooter lost = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        lost.TryFire(0f, new Vector3(0f, 1.4f, 30f), null);
        Check(ManagedNpcShots.Count == 1, "one spell is flying");
        RgStep(1f);
        Check(ManagedNpcShots.Count == 1 && impacts.Count == 0, "it is still flying after a second, and has raised nothing");
        RgStep(8f);
        Check(ManagedNpcShots.Count == 0 && impacts.Count == 1, "a spell that hits nothing ends in one impact where it ran out");
        Check(impacts[0].Position.z > 20f && impacts[0].Normal.z < -0.9f, "far out, with the normal pointing back along its path, " + impacts[0].Position.z.ToString("F1") + " m");

        // A steered spell flies straight for an NPC.
        ResetWorld();
        ManagedNpcShooter seeker = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        Set(seeker, "_spell", ManagedSpellKind.Seeker);
        GameObject runner = RgTarget(0f, 20f);
        RgFire(seeker, runner);
        Vector3 v0 = ManagedNpcShots.VelocityAt(0);
        Check(RgNear(v0.magnitude, ManagedSpellDefinition.Seeker.Speed, 0.01f), "the Seeker leaves at its own speed");
        runner.transform.position = new Vector3(8f, 0f, 20f);
        RgStep(0.5f);
        Check(ManagedNpcShots.Count == 1 && RgAngle(v0, ManagedNpcShots.VelocityAt(0)) < 0.01f, "and does not turn toward a target that moves, it has no lock");

        // Ice has no damage in the table, the impact still happens.
        ResetWorld();
        impacts.Clear();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };
        ManagedNpcShooter ice = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        Set(ice, "_spell", ManagedSpellKind.Ice);
        GameObject frozen = RgTarget(0f, 12f);
        RgFire(ice, frozen);
        RgStep(1.5f);
        Check(impacts.Count == 1 && impacts[0].Kind == ManagedSpellKind.Ice && RgHealth(frozen) == 100f, "an Ice spell lands and raises its impact, and the table gives it no damage");

        // Damage can be turned off for a scene and the shot still lands.
        ResetWorld();
        impacts.Clear();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };
        ManagedSpellDamage.Enabled = false;
        GameObject calm = RgTarget(0f, 12f);
        ManagedNpcShooter quiet = RgBare(Vector3.zero, ManagedNpcRangedKind.Spell);
        RgFire(quiet, calm);
        RgStep(1.5f);
        Check(impacts.Count == 1 && RgHealth(calm) == 100f, "with spell damage off the impact still happens and nothing is hurt");
        ManagedSpellDamage.Enabled = true;

        // What is drawn: one sphere per shot, an arrow at its diameter and a spell at the size and color of its entry.
        ResetWorld();
        ManagedNpcShooter bow = RgBare(Vector3.zero);
        ManagedNpcShooter staff = RgBare(new Vector3(5f, 0f, 0f), ManagedNpcRangedKind.Spell);
        GameObject t = RgTarget(0f, 20f);
        RgFire(bow, t);
        ManagedSphereBatch batch = new ManagedSphereBatch(ManagedNpcShots.MaxShots, 0, false, "test");
        ManagedNpcShots.FillBatch(batch);
        Check(batch.Count == 1 && RgNear(batch.LastScale.x, 0.15f, 1e-4f) && RgNear(batch.LastColor.r, 0.95f, 1e-4f), "an arrow is drawn as a sphere of its diameter and color");
        staff.TryFire(0f, new Vector3(5f, 1.4f, 200f), null);
        ManagedNpcShots.FillBatch(batch);
        Check(batch.Count == 2 && RgNear(batch.LastScale.x, ManagedSpellDefinition.Fireball.VisualDiameter, 1e-4f) && RgNear(batch.LastColor.r, ManagedSpellDefinition.Fireball.Color.r, 1e-4f), "a fresh spell is drawn at its full size and brightness");
        RgStep(2.0f);
        ManagedNpcShots.FillBatch(batch);
        Check(batch.Count == 1 && batch.LastScale.x < ManagedSpellDefinition.Fireball.VisualDiameter - 0.01f && batch.LastColor.r < ManagedSpellDefinition.Fireball.Color.r - 0.01f, "as its SP drains the spell shrinks and dims, now " + batch.LastScale.x.ToString("F3") + " m");
    }

    // ---------- what the brain tells Rust ----------

    static void RangedObservation()
    {
        ResetWorld();
        GameObject player = Player(10f, true); Clear(player);
        Fighter melee = Goblin(Vector3.zero);
        Fighter archer = RgArcher(new Vector3(1f, 0f, 0f));
        Fighter knight = RgArcher(new Vector3(-1f, 0f, 0f), AttackStyle.Hybrid);
        NPCNativeObservation o = melee.Brain.BuildObservation();
        Check(o.AttackStyle == 0 && o.RangedReady == 0 && o.RangedStance == 0 && o.ThreatDistance == 0f && o.Favor == 0f, "before anything is seen a melee fighter sends zeros for the new fields");
        Tick(0.1f);
        o = melee.Brain.BuildObservation();
        Check(o.AttackStyle == 0 && o.RangedReady == 0 && o.RangedStance == 0, "a melee fighter never sends a ranged flag, whatever it is doing");
        Check(RgNear(o.ThreatDistance, 10f, 0.01f), "but is told the distance, " + o.ThreatDistance);
        o = archer.Brain.BuildObservation();
        Check(o.AttackStyle == 1 && o.RangedReady == 1 && o.Favor == 0f, "an archer with ammo sends its style and ready");
        Check(o.RangedStance == 1 && archer.Brain.Action == NpcAction.Shoot, "and is in the shooting stance once its action is Shoot");
        o = knight.Brain.BuildObservation();
        Check(o.AttackStyle == 2 && o.RangedReady == 1 && o.RangedStance == 1, "a hybrid sends style 2 and, shooting from 10 m, the shooting stance");
        RgGun(archer).AddAmmo(-100f);
        o = archer.Brain.BuildObservation();
        Check(o.RangedReady == 0, "an archer out of ammo is not ready");
        RgGun(archer).AddAmmo(1f);
        Check(archer.Brain.BuildObservation().RangedReady == 1, "and is again with one shot");

        // The stance follows the action, and Kite counts as shooting.
        player.transform.position = new Vector3(0f, 0f, 3f);
        Tick(0.1f);
        Check(RgNear(archer.Brain.BuildObservation().ThreatDistance, 3.162f, 0.01f) && archer.Brain.Action == NpcAction.Kite && archer.Brain.BuildObservation().RangedStance == 1, "an archer 3.2 m from its target kites, and kiting is the shooting stance");
        Check(knight.Brain.Action == NpcAction.Attack && knight.Brain.BuildObservation().RangedStance == 0, "a hybrid that far in fights by hand and is no longer in the shooting stance, " + knight.Brain.Action);
        Check(melee.Brain.Action == NpcAction.Attack, "while the melee fighter attacks as always");

        // The distance is along the ground and to where the target was last seen.
        player.transform.position = new Vector3(0f, 6f, 10f);
        Tick(0.1f);
        Check(RgNear(melee.Brain.BuildObservation().ThreatDistance, 10f, 0.01f), "height is not part of the distance");
        player.transform.position = new Vector3(0f, 0f, 20f);
        Wall();
        Tick(0.1f);
        Check(melee.Brain.ThreatVisible && RgNear(melee.Brain.BuildObservation().ThreatDistance, 10f, 0.01f), "while sight lingers the distance is to where the target was last seen, not to where it went");
        Tick(0.6f);
        Check(!melee.Brain.ThreatVisible && melee.Brain.BuildObservation().ThreatDistance == 0f, "and is zero, unknown, once sight is lost");

        // A target on top of the NPC is one centimeter away, never zero.
        ResetWorld();
        GameObject on = Player(0f, true); Clear(on);
        Fighter pinned = Goblin(Vector3.zero);
        Tick(0.1f);
        Check(pinned.Brain.ThreatVisible && RgNear(pinned.Brain.BuildObservation().ThreatDistance, 0.01f, 1e-6f), "a target standing on the NPC is 0.01 m away, so a known distance is never mistaken for an unknown one");

        // A style with no weapon is a setup mistake: told once, treated as nothing to shoot.
        ResetWorld();
        GameObject p2 = Player(10f, true); Clear(p2);
        Fighter bare = RgFighter("no bow", Vector3.zero, NpcRole.Enemy, AttackStyle.Ranged, true, false, ManagedNpcRangedKind.Projectile);
        Tick(0.1f); Tick(0.1f); Tick(0.1f);
        NPCNativeObservation bo = bare.Brain.BuildObservation();
        Check(bo.AttackStyle == 1 && bo.RangedReady == 0, "an archer with no weapon sends ready as zero");
        Check(Debug.Warnings.FindAll(w => w.Contains("ManagedNpcShooter") && w.Contains("no bow")).Count == 1, "and the missing weapon is reported once, not every tick");
        Check(bare.Brain.Action == NpcAction.Retreat && bare.Actor.Mode == NpcAction.Retreat, "with nothing to shoot an archer that can move runs, as Rust decides, " + bare.Brain.Action);

        // A weapon added after Awake is found.
        ResetWorld();
        GameObject p3 = Player(10f, true); Clear(p3);
        Fighter late = RgFighter("late bow", Vector3.zero, NpcRole.Enemy, AttackStyle.Ranged, true, false, ManagedNpcRangedKind.Projectile);
        Tick(0.1f);
        Check(late.Brain.Action == NpcAction.Retreat, "an archer is told to run while it has no weapon");
        ManagedNpcShooter added = late.Go.AddComponent<ManagedNpcShooter>(); Set(added, "_spreadDegrees", 0f);
        Time.time += 1f; Tick(0.1f);
        Check(late.Brain.Action == NpcAction.Shoot, "and shoots once a weapon is added to the object, no restart needed");
    }

    // ---------- decisions through the director, on whichever path the run uses ----------

    static void RangedDecisions()
    {
        // style, ready, can move, distance, expected action. The same rows as the library test, so the brain, the director
        // and the decision path are checked together with the decision on its own.
        object[][] rows =
        {
            new object[] { AttackStyle.Ranged, true, true, 20f, NpcAction.Shoot },
            new object[] { AttackStyle.Ranged, true, true, 7f, NpcAction.Shoot },
            new object[] { AttackStyle.Ranged, true, true, 5.5f, NpcAction.Kite },
            new object[] { AttackStyle.Ranged, true, true, 2f, NpcAction.Kite },
            new object[] { AttackStyle.Ranged, true, false, 2f, NpcAction.Shoot },
            new object[] { AttackStyle.Ranged, false, true, 20f, NpcAction.Retreat },
            new object[] { AttackStyle.Ranged, false, false, 20f, NpcAction.Idle },
            new object[] { AttackStyle.Hybrid, true, true, 2f, NpcAction.Attack },
            new object[] { AttackStyle.Hybrid, true, true, 12f, NpcAction.Shoot },
            new object[] { AttackStyle.Hybrid, false, true, 12f, NpcAction.Attack },
            new object[] { AttackStyle.Hybrid, true, false, 2f, NpcAction.Attack },
            new object[] { AttackStyle.Hybrid, true, false, 12f, NpcAction.Shoot },
            new object[] { AttackStyle.Melee, true, true, 12f, NpcAction.Attack },
        };
        foreach (object[] row in rows)
        {
            ResetWorld();
            float distance = (float)row[3];
            GameObject player = Player(distance, true); Clear(player);
            Fighter f = RgArcher(Vector3.zero, (AttackStyle)row[0], (bool)row[2]);
            if (!(bool)row[1]) RgGun(f).AddAmmo(-100f);
            Tick(0.1f);
            Check(f.Brain.Action == (NpcAction)row[4], "style " + row[0] + " ready " + row[1] + " can move " + row[2] + " at " + distance + " m decides " + row[4] + ", got " + f.Brain.Action);
        }

        // A hybrid keeps its stance in the buffer between 4 and 8 m and changes at the edges.
        ResetWorld();
        GameObject p = Player(12f, true); Clear(p);
        Fighter h = RgArcher(Vector3.zero, AttackStyle.Hybrid, false);
        List<string> changes = new List<string>();
        h.Brain.ActionChanged += (b, old, now) => changes.Add(old + ">" + now);
        Tick(0.1f);
        Check(h.Brain.Action == NpcAction.Shoot, "a hybrid shoots at 12 m");
        float[] walk = { 9f, 7f, 5f, 4.01f, 5f, 7.99f, 6f };
        bool stayed = true;
        foreach (float z in walk) { p.transform.position = new Vector3(0f, 0f, z); Tick(0.1f); if (h.Brain.Action != NpcAction.Shoot) stayed = false; }
        Check(stayed, "it keeps shooting as the target comes in through the buffer, down to just over 4 m");
        p.transform.position = new Vector3(0f, 0f, 4f); Tick(0.1f);
        Check(h.Brain.Action == NpcAction.Attack, "at 4 m it fights by hand");
        stayed = true;
        foreach (float z in new[] { 5f, 6f, 7f, 7.99f, 6f, 4.5f }) { p.transform.position = new Vector3(0f, 0f, z); Tick(0.1f); if (h.Brain.Action != NpcAction.Attack) stayed = false; }
        Check(stayed, "and keeps fighting by hand as the target backs off through the buffer");
        p.transform.position = new Vector3(0f, 0f, 8f); Tick(0.1f);
        Check(h.Brain.Action == NpcAction.Shoot, "at 8 m it shoots again");
        Check(changes.Count == 3 && changes[0] == "Idle>Shoot" && changes[1] == "Shoot>Attack" && changes[2] == "Attack>Shoot", "three changes in all, one at each edge of the buffer and none inside it: " + string.Join(",", changes));

        // Out of ammo it fights by hand wherever the target is, and regains the bow when a shot comes back.
        RgGun(h).AddAmmo(-100f);
        p.transform.position = new Vector3(0f, 0f, 15f); Tick(0.1f);
        Check(h.Brain.Action == NpcAction.Attack, "a hybrid with nothing to shoot fights by hand at 15 m");
        RgGun(h).AddAmmo(1f);
        Tick(0.1f);
        Check(h.Brain.Action == NpcAction.Shoot, "and shoots again as soon as it has one shot");
    }

    // ---------- the shooters' own pool and rings ----------

    static ManagedCombatEngagement.Member RgJoin(ManagedNpcThreatSource src, Fighter f, float now)
    {
        return src.Engagement.Join(f.Actor, f.Brain, now, 12f, 2.5f, 1.2f, 1.5f, true);
    }

    static Fighter RgArcherAt(float degrees, float radius)
    {
        float r = degrees * Mathf.Deg2Rad;
        return RgArcher(new Vector3(Mathf.Cos(r) * radius, 0f, Mathf.Sin(r) * radius));
    }

    static void RangedEngagement()
    {
        // Shooters and swordsmen have a pool each.
        ResetWorld();
        GameObject player = Player(0f, true);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        Check(src.MaxRangedAttackers == 4 && src.RangedRotationSeconds == 5f && src.MaxMeleeAttackers == 3, "by default four shooters and three swordsmen may act at once");
        List<ManagedCombatEngagement.Member> melee = new List<ManagedCombatEngagement.Member>();
        List<ManagedCombatEngagement.Member> shooters = new List<ManagedCombatEngagement.Member>();
        for (int i = 0; i < 5; i++) melee.Add(JoinAt(src, AtBearing(i * 72f, 3f + i * 0.1f), 0f));
        for (int i = 0; i < 6; i++) shooters.Add(RgJoin(src, RgArcherAt(i * 60f + 10f, 14f + i * 0.1f), 0f));
        Check(src.AttackerCount == 11 && src.ShooterCount == 6, "eleven in the fight, six of them shooters");
        src.Engagement.Refresh(1f);
        Check(src.StrikingCount == 3 && src.ShootingCount == 4, "three swordsmen and four shooters hold turns: separate pools, neither starves the other, got " + src.StrikingCount + " and " + src.ShootingCount);
        Check(shooters.FindAll(x => x.Ranged).Count == 6 && melee.FindAll(x => x.Ranged).Count == 0, "a member knows whether it is a shooter");

        // Places: swordsmen on the melee ring, shooters on a ring at the stand-off distance, waiting shooters outside it.
        bool meleeOn = true, shootOn = true, waitOn = true;
        foreach (ManagedCombatEngagement.Member m in melee) if (m.HasToken && Math.Abs((src.Engagement.SlotPosition(m) - src.Engagement.Center).magnitude - 1.26f) > 0.001f) meleeOn = false;
        List<ManagedCombatEngagement.Member> holders = shooters.FindAll(x => x.HasToken);
        List<ManagedCombatEngagement.Member> waiters = shooters.FindAll(x => !x.HasToken);
        foreach (ManagedCombatEngagement.Member m in holders) if (Math.Abs((src.Engagement.SlotPosition(m) - src.Engagement.Center).magnitude - 12f) > 0.001f) shootOn = false;
        foreach (ManagedCombatEngagement.Member m in waiters) if (Math.Abs((src.Engagement.SlotPosition(m) - src.Engagement.Center).magnitude - 14.5f) > 0.5f) waitOn = false;
        Check(meleeOn, "swordsmen who hold a turn stand on the melee ring at 1.26 m");
        Check(holders.Count == 4 && shootOn, "shooters who hold a turn stand on the ring at 12 m, the stand-off distance");
        Check(waiters.Count == 2 && waitOn, "the two shooters without a turn wait on the ring 2.5 m outside it");
        Check(EvenlySpaced(SortedAngles(holders), 0.02f), "the four shooters with turns are spread a quarter circle apart");

        // Changing one pool does not touch the other.
        src.MaxMeleeAttackers = 1;
        src.Engagement.Refresh(1.3f);
        Check(src.StrikingCount == 1 && src.ShootingCount == 4, "lowering the melee limit leaves the shooters alone, got " + src.StrikingCount + " and " + src.ShootingCount);
        src.MaxMeleeAttackers = 3; src.MaxRangedAttackers = 2;
        src.Engagement.Refresh(1.6f);
        Check(src.StrikingCount == 3 && src.ShootingCount == 2, "and lowering the shooters' limit leaves the swordsmen alone, got " + src.StrikingCount + " and " + src.ShootingCount);
        src.MaxRangedAttackers = 0;
        src.Engagement.Refresh(1.9f);
        Check(src.ShootingCount == 6, "a limit of zero lifts the cap on shooters");
        src.MaxRangedAttackers = -3;
        Check(src.MaxRangedAttackers == 0, "a negative limit is stored as zero");

        // Rotation among the shooters.
        ResetWorld();
        GameObject p2 = Player(0f, true);
        ManagedNpcThreatSource s2 = p2.GetComponent<ManagedNpcThreatSource>();
        s2.MaxRangedAttackers = 1; s2.RangedRotationSeconds = 2f; s2.AttackerRotationSeconds = 100f;
        ManagedCombatEngagement.Member[] rm = new ManagedCombatEngagement.Member[3];
        for (int i = 0; i < 3; i++) rm[i] = RgJoin(s2, RgArcherAt(i * 120f, 13f + i * 0.2f), 0f);
        HashSet<int> held = new HashSet<int>();
        int Holder() { for (int i = 0; i < 3; i++) if (rm[i].HasToken) return i; return -1; }
        s2.Engagement.Refresh(1f); int first = Holder(); held.Add(first);
        s2.Engagement.Refresh(2.5f);
        Check(Holder() == first, "a shooter keeps its turn until the shooters' rotation time has passed");
        s2.Engagement.Refresh(3.1f); held.Add(Holder());
        Check(Holder() != first && s2.ShootingCount == 1, "then the turn goes to a shooter that was waiting");
        s2.Engagement.Refresh(5.3f); held.Add(Holder());
        Check(held.Count == 3, "and every shooter has had one after two rotations, whatever the melee rotation time is");

        // Leaving.
        ResetWorld();
        GameObject p3 = Player(0f, true);
        ManagedNpcThreatSource s3 = p3.GetComponent<ManagedNpcThreatSource>();
        ManagedCombatEngagement.Member a = RgJoin(s3, RgArcherAt(0f, 13f), 0f);
        ManagedCombatEngagement.Member b = JoinAt(s3, AtBearing(90f, 3f), 0f);
        s3.Engagement.Refresh(1f);
        Check(s3.ShooterCount == 1 && s3.ShootingCount == 1 && s3.StrikingCount == 1 && s3.AttackerCount == 2, "one shooter and one swordsman, got shooters " + s3.ShooterCount + " shooting " + s3.ShootingCount + " striking " + s3.StrikingCount + " all " + s3.AttackerCount);
        s3.Engagement.Leave(a);
        Check(s3.ShooterCount == 0 && s3.ShootingCount == 0 && s3.StrikingCount == 1 && s3.AttackerCount == 1, "a shooter that leaves is no longer counted, the swordsman is");
        s3.Engagement.Leave(b);
        Check(s3.AttackerCount == 0 && s3.StrikingCount == 0, "and then nobody is");

        // The old seven argument join is a swordsman.
        ResetWorld();
        GameObject p4 = Player(0f, true);
        ManagedNpcThreatSource s4 = p4.GetComponent<ManagedNpcThreatSource>();
        ManagedCombatEngagement.Member old = JoinAt(s4, AtBearing(0f, 3f), 0f);
        Check(!old.Ranged && s4.ShooterCount == 0, "a join without the shooter flag makes a swordsman");
    }

    // ---------- the actor shooting ----------

    static void RangedActors()
    {
        // An archer walks to its place on the stand-off ring, fires from there and never walks into the kite line.
        ResetWorld();
        GameObject player = RgTarget(0f, 30f); Clear(player);
        ManagedHealth ph = player.GetComponent<ManagedHealth>();
        Fighter f = RgArcher(Vector3.zero);
        List<float> shotAt = RgWatch(f, player);
        float closest = float.MaxValue; int shootMode = 0;
        for (int i = 0; i < 240; i++)
        {
            Advance(0.05f);
            closest = Math.Min(closest, Dist(f.Go.transform.position, player.transform.position));
            if (f.Actor.Mode == NpcAction.Shoot) shootMode++;
        }
        float end = Dist(f.Go.transform.position, player.transform.position);
        Check(f.Actor.Mode == NpcAction.Shoot && f.Brain.Action == NpcAction.Shoot, "the archer is in shooting mode, " + f.Actor.Mode);
        Check(end > 11f && end < 13f, "it stopped at its place on the 12 m ring, " + end.ToString("F2") + " m from the target");
        Check(closest > 11f, "and never came closer than that, nearest " + closest.ToString("F2") + " m");
        Check(shotAt.Count >= 4 && RgGun(f).Ammo == 12f - shotAt.Count, "it fired " + shotAt.Count + " arrows in twelve seconds, one ammo each");
        bool inReach = true; foreach (float d in shotAt) if (d > 20.05f) inReach = false;
        Check(inReach, "none of them from beyond the weapon's reach of 20 m");
        Check(ph.Current < 100f && RgNear(ph.Current, 100f - 12f * RgHits(player), 1e-3f) && RgHits(player) >= 3, "and the arrows hit, the target lost " + (100f - ph.Current).ToString("F0") + " health");
        Check(f.Actor.IsEngaged && f.Actor.IsShooter && f.Actor.HasAttackToken, "it holds a shooter's turn in the target's engagement");
        Check(player.GetComponent<ManagedNpcThreatSource>().ShootingCount == 1 && player.GetComponent<ManagedNpcThreatSource>().StrikingCount == 0, "a shooter's turn, not a swordsman's, got shooting " + player.GetComponent<ManagedNpcThreatSource>().ShootingCount + " striking " + player.GetComponent<ManagedNpcThreatSource>().StrikingCount);
        Check(!f.Agent.updateRotation, "close to the target it turns itself to face it and not by its path");
        Check(f.Hits == 0, "and it never swings");

        // Not before the target is in reach: the first arrow leaves from 20 m or less.
        Check(shotAt.Count > 0 && shotAt[0] <= 20.05f && shotAt[0] > 11f, "the first arrow left from " + (shotAt.Count > 0 ? shotAt[0].ToString("F1") : "none") + " m");

        // A wall in the line of fire, with the target still seen: it asks for another place and holds its fire.
        ResetWorld();
        GameObject p2 = RgTarget(0f, 12f);
        bool wall = true;
        Physics.OnRaycast = (Vector3 o, Vector3 d, out RaycastHit h, float m, int mask) =>
        {
            GameObject w = null;
            if (wall && Math.Abs(o.y - 1.4f) < 0.001f) { w = new GameObject("wall"); w.AddComponent<Collider>(); h = new RaycastHit { collider = w.GetComponent<Collider>() }; UnityEngine.Object.All.Remove(w.GetComponent<Collider>()); return true; }
            h = new RaycastHit { collider = p2.GetComponent<Collider>() };
            return true;
        };
        Fighter g = RgArcher(Vector3.zero);
        HashSet<string> places = new HashSet<string>();
        for (int i = 0; i < 80; i++)
        {
            Advance(0.05f);
            Vector3 slot = g.Actor.EngagementSlot; places.Add(slot.x.ToString("F1") + "," + slot.z.ToString("F1"));
        }
        Check(g.Brain.ThreatVisible && RgGun(g).Ammo == 12f && ManagedNpcShots.Count == 0, "with the line of fire blocked and the target in sight the archer holds its fire and keeps its ammo");
        Check(places.Count >= 2, "and asks the engagement for another place on the ring, " + places.Count + " places tried");
        wall = false;
        Advance(2f);
        Check(RgGun(g).Ammo < 12f, "and fires when the line is clear");

        // Out of ammo: it runs, and shoots again when a shot has come back.
        ResetWorld();
        GameObject p3 = RgTarget(0f, 12f); Clear(p3);
        Fighter m = RgArcher(Vector3.zero);
        Set(RgGun(m), "_capacity", 2f); Set(RgGun(m), "_regenSecondsPerShot", 4f); Set(RgGun(m), "_cooldownSeconds", 0.5f);
        RgGun(m).Refill();
        List<float> shots = RgWatch(m, p3);
        bool sawRetreat = false; float farthest = 0f; bool shotAfterRetreat = false;
        for (int i = 0; i < 360; i++)
        {
            Advance(0.05f);
            if (m.Actor.Mode == NpcAction.Retreat) sawRetreat = true;
            farthest = Math.Max(farthest, Dist(m.Go.transform.position, p3.transform.position));
            if (sawRetreat && shots.Count >= 3) shotAfterRetreat = true;
        }
        Check(sawRetreat, "an archer that has used its last arrow runs");
        Check(farthest > 14f, "and gets well past its place, " + farthest.ToString("F1") + " m from the target");
        Check(shotAfterRetreat && shots.Count >= 3, "then shoots again once ammo has come back, " + shots.Count + " arrows in all");

        // A mage casts at its target and the spell hurts it.
        ResetWorld();
        List<ManagedSpellImpact> impacts = new List<ManagedSpellImpact>();
        ManagedSpellCaster.Impact += i => { impacts.Add(i); ManagedSpellDamage.Apply(i); };
        GameObject p4 = RgTarget(0f, 14f); Clear(p4);
        Fighter mage = RgArcher(Vector3.zero, AttackStyle.Ranged, true, ManagedNpcRangedKind.Spell);
        Advance(8f);
        Check(RgGun(mage).Ammo < 12f && impacts.Count >= 1, "a mage casts and its spells land");
        Check(impacts.Count > 0 && ReferenceEquals(impacts[0].Source, mage.Go), "each impact carries the mage as its source");
        Check(RgHealth(p4) < 100f && RgHealth(mage.Go) == 100f, "the target is hurt and the mage is not");

        // The target dies: the shooting stops and the archer goes back to patrolling.
        ResetWorld();
        GameObject p5 = RgTarget(0f, 14f); Clear(p5);
        Fighter a2 = RgArcher(Vector3.zero);
        List<float> s5 = RgWatch(a2, p5);
        Advance(5f);
        int before = s5.Count;
        p5.GetComponent<ManagedHealth>().TakeDamage(1000f);
        Advance(3f);
        Check(before >= 1 && s5.Count == before, "nothing more is fired at a dead target");
        Check(a2.Brain.Action == NpcAction.Patrol && !a2.Actor.IsEngaged, "the archer goes back to patrol and leaves the engagement, " + a2.Brain.Action);

        // An archer with no weapon on the object does not fire, does not fail, and is told to run.
        ResetWorld();
        GameObject p6 = RgTarget(0f, 14f); Clear(p6);
        Fighter none = RgFighter("unarmed", Vector3.zero, NpcRole.Enemy, AttackStyle.Ranged, true, false, ManagedNpcRangedKind.Projectile);
        Advance(3f);
        Check(ManagedNpcShots.Count == 0 && none.Actor.Mode == NpcAction.Retreat, "an archer with no bow runs and fires nothing");

        // A weapon added after the actor woke is found by the actor too.
        ResetWorld();
        GameObject p7 = RgTarget(0f, 14f); Clear(p7);
        Fighter lateBow = RgFighter("late", Vector3.zero, NpcRole.Enemy, AttackStyle.Ranged, true, false, ManagedNpcRangedKind.Projectile);
        ManagedNpcShooter gun = lateBow.Go.AddComponent<ManagedNpcShooter>(); Set(gun, "_spreadDegrees", 0f);
        Advance(5f);
        Check(gun.Ammo < 12f, "an actor finds a weapon that was added after it started");
    }

    static int RgHits(GameObject target)
    {
        // How many arrows hit: the health lost over the damage of one arrow.
        return (int)Math.Round((100f - RgHealth(target)) / 12f);
    }

    // ---------- backing away ----------

    static void RangedKiting()
    {
        // A threat inside the kite line: the archer backs away, facing it, and fires on the way.
        ResetWorld();
        GameObject player = RgTarget(0f, 4f); Clear(player);
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter f = RgArcher(Vector3.zero);
        List<float> shots = RgWatch(f, player);
        List<NpcAction> modes = new List<NpcAction>();
        float last = Dist(f.Go.transform.position, player.transform.position); bool grew = true; int shotsWhileKiting = 0; bool facing = true; bool rotationOff = true;
        int seenShots = 0;
        for (int i = 0; i < 200; i++)
        {
            Advance(0.05f);
            NpcAction mode = f.Actor.Mode;
            if (modes.Count == 0 || modes[modes.Count - 1] != mode) modes.Add(mode);
            float d = Dist(f.Go.transform.position, player.transform.position);
            if (mode == NpcAction.Kite)
            {
                if (d < last - 0.01f) grew = false;
                float yaw = Math.Abs(Mathf.DeltaAngle(f.Go.transform.rotation.yaw, Quaternion.LookRotation(player.transform.position - f.Go.transform.position).yaw));
                if (yaw > 15f) facing = false;
                if (f.Agent.updateRotation) rotationOff = false;
                if (shots.Count > seenShots) shotsWhileKiting += shots.Count - seenShots;
            }
            seenShots = shots.Count;
            last = d;
        }
        float end = Dist(f.Go.transform.position, player.transform.position);
        Check(modes.Count >= 2 && modes[0] == NpcAction.Kite && modes.Contains(NpcAction.Shoot), "the archer kites first and then, once clear of the line, shoots: " + string.Join(">", modes));
        Check(grew, "the distance only grows while it kites");
        Check(facing && rotationOff, "it keeps facing the target as it backs away, steering by itself and not by its path");
        Check(shotsWhileKiting >= 1, "and fires while it backs away, " + shotsWhileKiting + " shots on the way");
        Check(end > 11f && end < 13f, "it ends its retreat at its place on the stand-off ring, " + end.ToString("F2") + " m");
        Check(!f.Actor.IsEngaged || f.Actor.Mode == NpcAction.Shoot, "while it kited it held no place in the engagement");

        // Cornered: no walkable place behind it. It stands and fires.
        ResetWorld();
        GameObject p2 = RgTarget(0f, 4f); Clear(p2);
        p2.GetComponent<ManagedHealth>().Invulnerable = true;
        NavMesh.IsWalkable = p => p.z > -1f;
        Fighter c = RgArcher(Vector3.zero);
        List<float> cs = RgWatch(c, p2);
        Advance(4f);
        Check(c.Brain.Action == NpcAction.Kite && c.Actor.Mode == NpcAction.Kite, "a threat at 4 m still makes it kite");
        Check(Dist(c.Go.transform.position, Vector3.zero) < 0.05f, "but with nowhere to go it stays where it is");
        Check(cs.Count >= 1, "and fires, " + cs.Count + " shots");
        NavMesh.IsWalkable = null;

        // One that cannot move never backs away. It shoots from where it stands.
        ResetWorld();
        GameObject p3 = RgTarget(0f, 3f); Clear(p3);
        p3.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter sentry = RgArcher(Vector3.zero, AttackStyle.Ranged, false);
        List<float> ss = RgWatch(sentry, p3);
        Advance(5f);
        Check(sentry.Brain.Action == NpcAction.Shoot && sentry.Actor.Mode == NpcAction.Shoot, "a sentry with a bow shoots at a target 3 m away");
        Check(Dist(sentry.Go.transform.position, Vector3.zero) < 0.01f && sentry.Agent.SetDestinationCalls == 0, "and does not move or ask the agent for a path");
        Check(ss.Count >= 3, "it fires on the cooldown, " + ss.Count + " shots in five seconds");

        // A sentry's target out of reach: it waits, it does not walk.
        ResetWorld();
        GameObject p4 = RgTarget(0f, 30f); Clear(p4);
        Fighter post = RgArcher(Vector3.zero, AttackStyle.Ranged, false);
        List<float> ps = RgWatch(post, p4);
        Advance(3f);
        Check(ps.Count == 0 && Dist(post.Go.transform.position, Vector3.zero) < 0.01f && RgGun(post).Ammo == 12f, "a sentry whose target is beyond its reach neither fires nor walks");
        p4.transform.position = new Vector3(0f, 0f, 15f);
        Advance(2f);
        Check(ps.Count >= 1, "and fires when the target comes into reach");
    }

    // ---------- switching between the bow and the sword ----------

    static void RangedHybrids()
    {
        // Far away a hybrid shoots, close in it fights by hand, and it hands over its place when it switches.
        ResetWorld();
        GameObject player = RgTarget(0f, 12f); Clear(player);
        player.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter h = RgArcher(Vector3.zero, AttackStyle.Hybrid);
        ManagedNpcThreatSource src = player.GetComponent<ManagedNpcThreatSource>();
        List<float> shots = RgWatch(h, player);
        Advance(5f);
        Check(h.Actor.Mode == NpcAction.Shoot && shots.Count >= 2 && h.Hits == 0, "at 12 m a hybrid shoots and does not swing");
        Check(src.ShooterCount == 1 && src.AttackerCount == 1 && src.ShootingCount == 1 && src.StrikingCount == 0, "it counts as a shooter in the engagement, got " + src.ShooterCount + "/" + src.AttackerCount + "/" + src.ShootingCount + "/" + src.StrikingCount);
        int shotsBefore = shots.Count;
        player.transform.position = new Vector3(0f, 0f, 2f);
        Advance(5f);
        Check(h.Actor.Mode == NpcAction.Attack && h.Brain.Action == NpcAction.Attack, "with the target 2 m away it fights by hand");
        Check(src.ShooterCount == 0 && src.AttackerCount == 1 && src.StrikingCount == 1 && src.ShootingCount == 0, "and is now a swordsman in the engagement, it left the shooters' ring");
        Check(h.Hits >= 2 && shots.Count == shotsBefore, "it strikes, and shoots no more, " + h.Hits + " hits");
        player.transform.position = new Vector3(0f, 0f, 14f);
        Advance(8f);
        Check(h.Actor.Mode == NpcAction.Shoot && shots.Count > shotsBefore, "when the target backs off to 14 m it goes back to the bow, mode " + h.Actor.Mode + " action " + h.Brain.Action + " at " + Dist(h.Go.transform.position, player.transform.position).ToString("F1") + " m, shots " + shots.Count + " of " + shotsBefore);

        // Inside the buffer nothing flips, with a hybrid that holds its ground.
        foreach (bool startFar in new[] { true, false })
        {
            ResetWorld();
            GameObject p = RgTarget(0f, startFar ? 12f : 2f); Clear(p);
            p.GetComponent<ManagedHealth>().Invulnerable = true;
            Fighter s = RgArcher(Vector3.zero, AttackStyle.Hybrid, false);
            Advance(1f);
            NpcAction stance = s.Brain.Action;
            Check(stance == (startFar ? NpcAction.Shoot : NpcAction.Attack), "a standing hybrid starts " + (startFar ? "shooting at 12 m" : "fighting by hand at 2 m") + ", got " + stance);
            int changes = 0;
            s.Brain.ActionChanged += (b, old, now) => changes++;
            for (int i = 0; i < 20; i++)
            {
                p.transform.position = new Vector3(0f, 0f, i % 2 == 0 ? 5f : 7f);
                Advance(0.5f);
            }
            Check(changes == 0 && s.Brain.Action == stance, "a target stepping between 5 and 7 m for ten seconds changes nothing, " + changes + " changes");
        }

        // No ammo: it closes in and fights by hand and fires nothing.
        ResetWorld();
        GameObject p2 = RgTarget(0f, 15f); Clear(p2);
        p2.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter empty = RgArcher(Vector3.zero, AttackStyle.Hybrid);
        RgGun(empty).AddAmmo(-100f);
        List<float> es = RgWatch(empty, p2);
        Advance(8f);
        Check(empty.Actor.Mode == NpcAction.Attack && empty.Hits >= 1 && es.Count == 0 && ManagedNpcShots.Count == 0, "a hybrid with an empty quiver walks up and fights by hand, " + empty.Hits + " hits, no arrows");
        Check(Dist(empty.Go.transform.position, p2.transform.position) < 1.8f, "it stands in melee reach");

        // A hybrid mage with mana coming back.
        ResetWorld();
        GameObject p3 = RgTarget(0f, 15f); Clear(p3);
        p3.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter mage = RgArcher(Vector3.zero, AttackStyle.Hybrid, true, ManagedNpcRangedKind.Spell);
        Set(RgGun(mage), "_capacity", 1f); Set(RgGun(mage), "_regenSecondsPerShot", 3f); Set(RgGun(mage), "_cooldownSeconds", 0.5f);
        RgGun(mage).Refill();
        List<ManagedSpellImpact> impacts = new List<ManagedSpellImpact>();
        ManagedSpellCaster.Impact += i => impacts.Add(i);
        bool wentMelee = false;
        for (int i = 0; i < 160; i++)
        {
            Advance(0.05f);
            if (mage.Actor.Mode == NpcAction.Attack) wentMelee = true;
        }
        Check(wentMelee && impacts.Count == 1, "a hybrid mage casts once, and with its mana spent walks up and fights by hand, " + impacts.Count + " spells");
        Check(RgGun(mage).IsReady && mage.Actor.Mode == NpcAction.Attack, "with its mana back and the target at arm's length it keeps fighting by hand, the buffer holds its stance");
        p3.transform.position = new Vector3(0f, 0f, 32f);
        Advance(5f);
        Check(impacts.Count == 2, "and casts again once the target is far off, " + impacts.Count + " spells in all");
    }

    // ---------- the debug tools ----------

    static void RangedDebug()
    {
        // Colors: every action has its own, shoot and kite included.
        HashSet<string> colors = new HashSet<string>();
        foreach (NpcAction a in Enum.GetValues(typeof(NpcAction)))
        { Color c = ManagedNpcDebugView.ColorFor(a); colors.Add(c.r + "," + c.g + "," + c.b); }
        Check(colors.Count == Enum.GetValues(typeof(NpcAction)).Length, "every one of the " + Enum.GetValues(typeof(NpcAction)).Length + " actions has its own color, got " + colors.Count);
        Check(ManagedNpcDebugView.ColorFor(NpcAction.Shoot).r > 0.9f && ManagedNpcDebugView.ColorFor(NpcAction.Shoot).g > 0.4f && ManagedNpcDebugView.ColorFor(NpcAction.Shoot).b < 0.2f, "shoot is orange");

        // The filter enum lines up with the action enum, one place along.
        foreach (NpcAction a in new[] { NpcAction.Shoot, NpcAction.Kite })
            Check((int)a == (int)(ManagedNpcActionFilter)Enum.Parse(typeof(ManagedNpcActionFilter), a.ToString()) - 1, "filter " + a + " lines up with the action");

        ResetWorld(); UnityEditor.Handles.Reset();
        GameObject player = RgTarget(0f, 14f); Clear(player);
        Fighter archer = RgArcher(Vector3.zero);
        Fighter melee = Goblin(new Vector3(2f, 0f, 0f));
        Fighter unarmed = RgFighter("unarmed", new Vector3(-2f, 0f, 0f), NpcRole.Enemy, AttackStyle.Hybrid, true, false, ManagedNpcRangedKind.Projectile);
        archer.Go.name = "archer-1";
        Advance(4f);
        string line = ManagedNpcDebugView.Describe(archer.Brain);
        Check(line.Contains("archer-1") && line.Contains("Shoot") && line.Contains("Ranged") && line.Contains("ammo") && line.Contains("/12") , "the line of an archer shows its style and its ammo: " + line);
        Check(line.Contains("firing"), "and that it holds a turn to fire: " + line);
        RgGun(archer).AddAmmo(-100f);
        Check(ManagedNpcDebugView.Describe(archer.Brain).Contains("ammo 0/12"), "the ammo follows the weapon");
        Check(!ManagedNpcDebugView.Describe(melee.Brain).Contains("ammo") && !ManagedNpcDebugView.Describe(melee.Brain).Contains("Melee"), "a melee fighter's line is as it was, without style or ammo");
        Check(ManagedNpcDebugView.Describe(unarmed.Brain).Contains("no weapon"), "a ranged style with no weapon says so");

        ManagedNpcDrawOptions o = new ManagedNpcDrawOptions();
        RgGun(archer).Refill();
        Advance(0.5f);
        o.Filter = ManagedNpcActionFilter.Shoot;
        Check(ManagedNpcDebugView.Passes(o, archer.Brain) && !ManagedNpcDebugView.Passes(o, melee.Brain), "the shoot filter shows the archer and not the swordsman");
        o.Filter = ManagedNpcActionFilter.Attack;
        Check(!ManagedNpcDebugView.Passes(o, archer.Brain) && ManagedNpcDebugView.Passes(o, melee.Brain), "and the attack filter the other way round");

        // Drawing: a shooter shows the reach of its weapon and its place on the ring.
        List<Vector3> scratch = new List<Vector3>();
        UnityEditor.Handles.Reset();
        ManagedNpcDebugView.Draw(archer.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("WireDisc:20.00") == 1, "an archer in shooting mode is drawn with a disc at the weapon's reach of 20 m");
        Check(Count("WireDisc:0.35") == 1 && Count("Dotted") >= 1, "and its place on the ring, with a dotted line to it");
        Check(Count("WireDisc:1.80") == 0, "and not the melee reach");
        UnityEditor.Handles.Reset();
        ManagedNpcDebugView.Draw(melee.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("WireDisc:1.80") == 1 && Count("WireDisc:20.00") == 0, "a swordsman is still drawn with the melee reach");

        // A kiting archer is drawn with the reach too.
        ResetWorld(); UnityEditor.Handles.Reset();
        GameObject p2 = RgTarget(0f, 3f); Clear(p2);
        p2.GetComponent<ManagedHealth>().Invulnerable = true;
        NavMesh.IsWalkable = p => p.z > -1f;
        Fighter kiter = RgArcher(Vector3.zero);
        Advance(1f);
        Check(kiter.Actor.Mode == NpcAction.Kite, "the archer kites");
        ManagedNpcDebugView.Draw(kiter.Brain, new ManagedNpcDrawOptions(), true, scratch);
        Check(Count("WireDisc:20.00") == 1, "a kiting archer is drawn with the reach of its weapon");
        NavMesh.IsWalkable = null;
    }

    // ---------- shopkeepers that fight back ----------

    static Fighter RgShop(Vector3 position, NpcDisposition disposition, bool canMove)
    {
        Fighter f = Fight("shopkeeper", position, NpcRole.Merchant, DecisionBackend.StateMachine, canMove, null);
        f.Brain.Disposition = disposition;
        return f;
    }

    static void ShopkeeperRetaliation()
    {
        // Through the brain and the director: only a retaliatory shopkeeper that was attacked fights back.
        foreach (NpcDisposition d in new[] { NpcDisposition.Hostile, NpcDisposition.Retaliatory, NpcDisposition.Peaceful })
        {
            ResetWorld();
            GameObject player = Player(1.2f, true); Clear(player);
            Fighter shop = RgShop(Vector3.zero, d, false);
            Tick(0.1f);
            Check(shop.Brain.ThreatVisible && shop.Brain.Action == NpcAction.Idle, d + " shopkeeper that sees a customer it has not been hurt by stands still, " + shop.Brain.Action);
            shop.Health.TakeDamage(1f, player);
            Check(shop.Brain.Provoked, d + " shopkeeper is provoked by a hit from a source");
            Tick(0.1f);
            NpcAction expected = d == NpcDisposition.Retaliatory ? NpcAction.Attack : NpcAction.Idle;
            Check(shop.Brain.Action == expected, d + " shopkeeper that was attacked decides " + expected + ", got " + shop.Brain.Action);
        }

        // A hit with no source, a trap or a fall, provokes nobody.
        ResetWorld();
        GameObject p0 = Player(1.2f, true); Clear(p0);
        Fighter tripped = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        tripped.Health.TakeDamage(1f);
        Tick(0.1f);
        Check(!tripped.Brain.Provoked && tripped.Brain.Action == NpcAction.Idle, "a shopkeeper hurt by something with no source does not blame the customer");

        // Nothing in view, attacked or not: it trades.
        ResetWorld();
        Fighter alone = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        alone.Health.TakeDamage(1f, new GameObject("someone"));
        Player(50f, true);
        Tick(0.1f);
        Check(alone.Brain.Provoked && alone.Brain.Action == NpcAction.Trade, "a provoked shopkeeper with nothing in view goes back to trading, " + alone.Brain.Action);

        // A stationary shopkeeper hits back in reach and stays at its stall.
        ResetWorld();
        GameObject cust = Player(1.2f, true); Clear(cust);
        Fighter stall = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        Advance(2f);
        Check(stall.Hits == 0 && stall.Actor.Mode == NpcAction.Idle && RgHealth(cust) == 100f, "a customer who has done nothing is left alone");
        stall.Health.TakeDamage(1f, cust);
        Advance(4f);
        Check(stall.Actor.Mode == NpcAction.Attack, "after being hit the shopkeeper attacks, " + stall.Actor.Mode);
        Check(stall.Hits >= 3, "and strikes on the cooldown, " + stall.Hits + " hits in four seconds");
        Check(RgNear(RgHealth(cust), 100f - 10f * stall.Hits, 1e-3f), "each hit is the actor's attack damage, the customer lost " + (100f - RgHealth(cust)).ToString("F0"));
        Check(Dist(stall.Go.transform.position, Vector3.zero) < 0.01f && stall.Agent.SetDestinationCalls == 0, "and it never left its stall or asked the agent for a path");

        // Out of reach it stays put and waits, and hits when the customer steps in.
        ResetWorld();
        GameObject far = Player(6f, true); Clear(far);
        Fighter keeper = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        keeper.Health.TakeDamage(1f, far);
        Advance(4f);
        Check(keeper.Brain.Action == NpcAction.Attack && keeper.Hits == 0 && Dist(keeper.Go.transform.position, Vector3.zero) < 0.01f, "6 m away is out of reach: the shopkeeper decides to attack but does not leave its stall, and does not hit");
        far.transform.position = new Vector3(0f, 0f, 1.2f);
        Advance(3f);
        Check(keeper.Hits >= 1 && Dist(keeper.Go.transform.position, Vector3.zero) < 0.01f, "and hits once the customer steps within reach");

        // A shopkeeper who is free to move chases.
        ResetWorld();
        GameObject runner = Player(8f, true); Clear(runner);
        runner.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter vendor = RgShop(Vector3.zero, NpcDisposition.Retaliatory, true);
        Advance(2f);
        Check(Dist(vendor.Go.transform.position, Vector3.zero) < 0.01f, "a mobile shopkeeper that has not been attacked stays by its stall too");
        vendor.Health.TakeDamage(1f, runner);
        Advance(6f);
        Check(Dist(vendor.Go.transform.position, runner.transform.position) < 1.8f && vendor.Hits >= 1, "once attacked it walks up and strikes, " + vendor.Hits + " hits");

        // Provocation fades. A customer who stays in view and attacks no more is left alone again.
        ResetWorld();
        GameObject calm = Player(1.2f, true); Clear(calm);
        calm.GetComponent<ManagedHealth>().Invulnerable = true;
        Fighter patient = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        patient.Health.TakeDamage(1f, calm);
        Advance(3f);
        Check(patient.Hits >= 1 && patient.Brain.Provoked, "the shopkeeper is fighting back");
        Advance(20f);
        Check(!patient.Brain.Provoked && patient.Brain.Action == NpcAction.Idle, "twenty seconds after the last hit it has calmed down, " + patient.Brain.Action);
        int hitsNow = patient.Hits;
        Advance(3f);
        Check(patient.Hits == hitsNow, "and hits no more");

        // The threat leaves: trade resumes.
        ResetWorld();
        GameObject leaver = Player(1.2f, true); Clear(leaver);
        Fighter trader = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        trader.Health.TakeDamage(1f, leaver);
        Advance(1f);
        Check(trader.Brain.Action == NpcAction.Attack, "the shopkeeper fights");
        leaver.GetComponent<ManagedNpcThreatSource>().enabled = false;
        Advance(1.5f);
        Check(trader.Brain.Action == NpcAction.Trade && trader.Actor.Mode == NpcAction.Trade, "when the customer is gone it trades again, " + trader.Brain.Action);

        // A crossbow behind the counter.
        ResetWorld();
        GameObject buyer = RgTarget(0f, 8f); Clear(buyer);
        Fighter bowman = RgFighter("shop with a crossbow", Vector3.zero, NpcRole.Merchant, AttackStyle.Ranged, false, true, ManagedNpcRangedKind.Projectile);
        bowman.Brain.Disposition = NpcDisposition.Retaliatory;
        List<float> bolts = RgWatch(bowman, buyer);
        Advance(5f);
        Check(bolts.Count == 0 && RgGun(bowman).Ammo == 12f && bowman.Actor.Mode == NpcAction.Idle, "an unprovoked shopkeeper with a crossbow does not fire");
        bowman.Health.TakeDamage(1f, buyer);
        Advance(6f);
        Check(bowman.Actor.Mode == NpcAction.Shoot && bolts.Count >= 3, "attacked, it shoots, " + bolts.Count + " bolts");
        Check(RgHealth(buyer) < 100f && Dist(bowman.Go.transform.position, Vector3.zero) < 0.01f, "the bolts hit and the shopkeeper stays at its stall");

        // Fighting does not make a shopkeeper a combatant in the Crown's eyes: hurting one is a crime, hurting back is not.
        // The brain reports a hurt shopkeeper to the Crown, and nothing here reports the customer's wounds.
        ResetWorld();
        GameObject c2 = Player(1.2f, true); Clear(c2);
        Fighter s2 = RgShop(Vector3.zero, NpcDisposition.Retaliatory, false);
        s2.Health.TakeDamage(1f, c2);
        Advance(3f);
        Check(RgHealth(c2) < 100f, "the shopkeeper hurt the customer");
    }
}
