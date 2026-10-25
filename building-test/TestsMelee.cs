// Checks for melee, the equip bridge and body facing. Verification harness only.
using System;
using System.Linq;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using MidManStudio.Gtg.Managed.Facing;
using MidManStudio.Gtg.Managed.Health;
using MidManStudio.Gtg.Managed.Hud;
using MidManStudio.Gtg.Managed.Items;
using MidManStudio.Gtg.Managed.Melee;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        // Exact in binary, so the sums in the swing clock carry no rounding.
        private const float MeleeStep = 0.125f;

        private static ManagedHealth MeleeTarget(Vector3 position, int layer = 9, float size = 1f)
        {
            var go = new GameObject("target");
            go.layer = layer;
            go.transform.position = position;
            go.AddComponent<BoxCollider>().size = new Vector3(size, size, size);
            ManagedHealth health = go.AddComponent<ManagedHealth>();
            Call(health, "Awake");
            return health;
        }

        private static ManagedCharacter MeleePlayer(out ManagedMelee melee, bool equipOnStart = false)
        {
            var go = new GameObject("melee player");
            go.layer = 7;
            UnityEngine.CharacterController capsule = go.AddComponent<UnityEngine.CharacterController>();
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            ManagedCharacter player = go.AddComponent<ManagedCharacter>();
            melee = go.AddComponent<ManagedMelee>();
            SetField(melee, "_hitMask", (LayerMask)(1 << 9));
            SetField(melee, "_logHits", false);
            SetField(melee, "_equipOnStart", equipOnStart);
            Call(melee, "Awake");
            return player;
        }

        private static void MeleeInput(ManagedCharacter player, bool fire, int spellSlot = 0, int cycle = 0)
        {
            SetField(player, "_input", new ManagedCharacterInputFrame { FirePressed = fire, SpellSlot = spellSlot, CycleSpell = cycle });
        }

        private static void MeleeFrame(ManagedCharacter player, ManagedMelee melee, bool fire)
        {
            MeleeInput(player, fire);
            Call(melee, "Update");
        }

        private static void MeleeTests()
        {
            float savedStep = Time.deltaTime;
            Time.deltaTime = MeleeStep;

            Section("melee swing clock");
            Reset();
            var swing = new ManagedMeleeSwing();
            Check(!swing.IsSwinging, "a new swing clock is idle");
            Check(!swing.Tick(1f), "ticking an idle clock reports no hit");
            Check(swing.Start(0.25f, 0.6f), "a swing starts");
            Check(!swing.Start(0.25f, 0.6f), "a second start during a swing is refused");
            Check(swing.IsSwinging, "the swing is under way");
            Check(!swing.Tick(0f) && !swing.Tick(-1f), "a step of zero or less does nothing");
            Near(swing.Elapsed, 0f, "and no time has passed");
            Check(!swing.Tick(0.125f), "before the hit moment there is no hit");
            Near(swing.Progress, 0.125f / 0.6f, "progress is the share of the length");
            Check(swing.Tick(0.125f), "the step that reaches the hit moment reports the hit");
            Check(!swing.Tick(0.125f), "the hit is reported once");
            Check(swing.IsSwinging, "the swing goes on after the hit");
            swing.Tick(0.125f); swing.Tick(0.125f);
            Check(!swing.IsSwinging, "the swing ends at its length");
            Near(swing.Progress, 0f, "an idle clock has no progress");
            Check(swing.Start(0.25f, 0.6f), "a new swing can start after one ended");

            swing.Cancel();
            Check(!swing.IsSwinging, "cancel ends the swing");
            Check(swing.Start(0.1f, 0.5f) && !swing.Tick(0.05f), "a swing after a cancel starts from zero, with its hit still to come");
            Check(swing.Tick(0.1f), "and reports its hit");

            swing = new ManagedMeleeSwing();
            swing.Start(0.1f, 0.3f);
            Check(swing.Tick(5f), "one large step still reports the hit");
            Check(!swing.IsSwinging, "and ends the swing in the same step");

            swing = new ManagedMeleeSwing();
            swing.Start(-1f, 0.2f);
            Check(swing.Tick(0.01f), "a negative hit delay counts as zero, so the first step hits");
            swing = new ManagedMeleeSwing();
            swing.Start(0.375f, 0.125f);
            Check(!swing.Tick(0.25f) && swing.IsSwinging, "a length below the hit delay is raised to it");
            Check(swing.Tick(0.125f) && !swing.IsSwinging, "so the swing hits and ends together");

            Section("melee weapon numbers");
            var weapon = new ManagedMeleeWeapon();
            Eq(weapon.ItemId, 1, "the default weapon is item 1");
            Eq(weapon.DisplayName, "Iron Sword", "the Iron Sword");
            weapon.HitDelaySeconds = -2f;
            Near(weapon.SafeHitDelay, 0f, "a negative hit delay reads as zero");
            weapon.HitDelaySeconds = 0.5f; weapon.SwingSeconds = 0.2f;
            Near(weapon.SafeSwingSeconds, 0.5f, "a swing time below the hit delay reads as the hit delay");

            Section("melee cone");
            Vector3 origin = new Vector3(0f, 1.1f, 0f);
            Vector3 forward = Vector3.forward;
            Check(ManagedMeleeStrike.InCone(origin, forward, new Vector3(0f, 1.1f, 2f), 100f), "straight ahead is inside");
            Check(ManagedMeleeStrike.InCone(origin, forward, new Vector3(1f, 1.1f, 1f), 100f), "45 degrees off is inside a 100 degree cone");
            Check(!ManagedMeleeStrike.InCone(origin, forward, new Vector3(1.2f, 1.1f, 1f), 100f), "50.2 degrees off is outside it");
            Check(!ManagedMeleeStrike.InCone(origin, forward, new Vector3(2f, 1.1f, 0f), 100f), "to the side is outside");
            Check(!ManagedMeleeStrike.InCone(origin, forward, new Vector3(0f, 1.1f, -2f), 100f), "behind is outside");
            Check(ManagedMeleeStrike.InCone(origin, forward, new Vector3(0f, 6f, 2f), 100f), "height does not change the cone");
            Check(ManagedMeleeStrike.InCone(origin, forward, new Vector3(0f, 4f, 0f), 10f), "a point straight above counts as inside");
            Check(ManagedMeleeStrike.InCone(origin, forward, new Vector3(0f, 1.1f, -2f), 360f), "360 degrees covers everything");
            Check(!ManagedMeleeStrike.InCone(origin, Vector3.up, new Vector3(0f, 1.1f, 2f), 100f), "a forward with no horizontal part has no cone");
            Check(ManagedMeleeStrike.InCone(origin, new Vector3(0f, -0.5f, 1f), new Vector3(0f, 1.1f, 2f), 100f), "a forward that points down still uses its horizontal part");
            Check(ManagedMeleeStrike.InCone(origin, new Vector3(1f, 0f, 0f), new Vector3(2f, 1.1f, 0f), 100f), "the cone turns with the forward");

            Section("melee strike");
            Reset();
            weapon = new ManagedMeleeWeapon();
            LayerMask mask = 1 << 9;
            ManagedHealth ahead = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            ManagedHealth behind = MeleeTarget(new Vector3(0f, 1.1f, -1.5f));
            ManagedHealth side = MeleeTarget(new Vector3(1.5f, 1.1f, 0f));
            ManagedHealth far = MeleeTarget(new Vector3(0f, 1.1f, 4f));
            ManagedHealth edge = MeleeTarget(new Vector3(1.5f, 1.1f, 1.5f));
            ManagedHealth high = MeleeTarget(new Vector3(0f, 2.0f, 1.5f));
            ManagedHealth otherLayer = MeleeTarget(new Vector3(-0.5f, 1.1f, 1.5f), 8);
            int hurt = ManagedMeleeStrike.Strike(origin, forward, weapon, null, mask);
            Eq(hurt, 3, "three targets are inside the cone and the reach");
            Near(ahead.Current, 75f, "the target ahead takes the weapon damage");
            Near(edge.Current, 75f, "a target at the edge of the cone takes it too");
            Near(high.Current, 75f, "so does one a little higher");
            Near(behind.Current, 100f, "one behind is not hurt");
            Near(side.Current, 100f, "one at the side is not hurt");
            Near(far.Current, 100f, "one out of reach is not hurt");
            Near(otherLayer.Current, 100f, "one on a layer outside the mask is not hurt");

            Reset();
            ManagedHealth twoBoxes = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            twoBoxes.gameObject.AddComponent<BoxCollider>().size = Vector3.one;
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, null, mask), 1, "a body with two colliders counts once");
            Near(twoBoxes.Current, 75f, "and takes the damage once");

            Reset();
            ManagedHealth self = MeleeTarget(new Vector3(0f, 1.1f, 0f));
            ManagedHealth foe = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, self.gameObject, mask), 1, "the source is left out");
            Near(self.Current, 100f, "the attacker is not hurt");
            Near(foe.Current, 75f, "the foe is");

            Reset();
            ManagedHealth downed = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            downed.TakeDamage(1000f);
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, null, mask), 0, "a target already down is not counted");

            Reset();
            ManagedHealth dummy = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            Eq(ManagedMeleeStrike.Strike(origin, forward, null, null, mask), 0, "no weapon hurts nothing");
            weapon.Damage = 0f;
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, null, mask), 0, "a weapon with no damage hurts nothing");
            weapon.Damage = 25f; weapon.Reach = 0f;
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, null, mask), 0, "a weapon with no reach hurts nothing");
            weapon.Reach = 2.2f;
            Eq(ManagedMeleeStrike.Strike(origin, forward, weapon, null, 1 << 8), 0, "a mask without the layer finds nothing");
            Near(dummy.Current, 100f, "and the target is untouched");

            Section("melee fire press");
            Reset();
            Keyboard.current = new Keyboard();
            ManagedMelee melee;
            ManagedCharacter p = MeleePlayer(out melee);
            Call(p, "Awake");
            Keyboard.current.fKey.wasPressedThisFrame = true;
            Call(p, "Update");
            Check(p.FireWasPressed && p.Input.FirePressed, "the press is read");
            Check(p.ConsumeFire(), "consuming reports the press");
            Check(!p.Input.FirePressed, "and clears it for modules that run later");
            Check(p.FireWasPressed, "while FireWasPressed stays true for the frame");
            Check(!p.ConsumeFire(), "a second consume finds nothing");
            Keyboard.current.fKey.wasPressedThisFrame = false;
            Call(p, "Update");
            Check(!p.FireWasPressed && !p.Input.FirePressed, "the next frame starts clean");

            Section("melee component");
            Reset();
            p = MeleePlayer(out melee);
            ManagedHealth foeAhead = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            int equipChanges = 0, started = 0, struck = 0, lastHits = -1;
            bool lastEquipped = false;
            melee.EquippedChanged += e => { equipChanges++; lastEquipped = e; };
            melee.SwingStarted += () => started++;
            melee.Struck += h => { struck++; lastHits = h; };
            Check(!melee.IsEquipped, "the weapon starts away");

            MeleeFrame(p, melee, true);
            Check(p.Input.FirePressed, "with the weapon away the press is left for the caster");
            Check(!melee.IsSwinging && started == 0, "and nothing swings");

            melee.Equip();
            melee.Equip();
            Eq(equipChanges, 1, "equipping twice raises one event");
            Check(lastEquipped && melee.IsEquipped, "and the weapon is in hand");

            MeleeFrame(p, melee, true);
            Check(!p.Input.FirePressed, "with the weapon in hand the press is taken before the caster sees it");
            Check(melee.IsSwinging && started == 1, "and a swing starts");
            MeleeFrame(p, melee, false);
            Eq(struck, 0, "no hit before the hit moment");
            MeleeFrame(p, melee, false);
            Eq(struck, 1, "the hit comes at the hit moment"); Eq(lastHits, 1, "and finds the target");
            Near(foeAhead.Current, 75f, "which takes the damage");
            Check(melee.IsSwinging, "the swing is not over yet");
            MeleeFrame(p, melee, false); MeleeFrame(p, melee, false); MeleeFrame(p, melee, false);
            Check(!melee.IsSwinging, "it ends at the swing time");
            Eq(struck, 1, "with one hit for the swing");

            // A press during a swing is taken and does not restart it.
            MeleeFrame(p, melee, true);
            Eq(started, 2, "a new swing starts");
            MeleeFrame(p, melee, true);
            Check(!p.Input.FirePressed, "a press during the swing is taken too");
            Eq(started, 2, "and does not start another");
            MeleeFrame(p, melee, false); MeleeFrame(p, melee, false); MeleeFrame(p, melee, false);
            MeleeFrame(p, melee, true);
            Eq(started, 3, "a press on the frame the swing ends starts the next one");

            // The cone follows the aim at the hit moment.
            Reset();
            p = MeleePlayer(out melee);
            foeAhead = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            struck = 0; lastHits = -1;
            melee.Struck += h => { struck++; lastHits = h; };
            melee.Equip();
            MeleeFrame(p, melee, true);
            SetField(p, "_yaw", 90f);
            MeleeFrame(p, melee, false); MeleeFrame(p, melee, false);
            Eq(struck, 1, "the swing reaches its hit moment"); Eq(lastHits, 0, "and misses, because the player turned away");
            Near(foeAhead.Current, 100f, "so the target is untouched");

            // Unequipping ends the swing.
            Reset();
            p = MeleePlayer(out melee);
            foeAhead = MeleeTarget(new Vector3(0f, 1.1f, 1.5f));
            struck = 0;
            melee.Struck += h => struck++;
            melee.Equip();
            MeleeFrame(p, melee, true);
            melee.Unequip();
            Check(!melee.IsSwinging, "putting the weapon away ends the swing");
            MeleeFrame(p, melee, false); MeleeFrame(p, melee, false);
            Eq(struck, 0, "and no hit follows");
            Near(foeAhead.Current, 100f, "the target is untouched");

            // Picking a spell puts the weapon away and leaves the press for the caster.
            melee.Equip();
            MeleeInput(p, true, 1);
            Call(melee, "Update");
            Check(!melee.IsEquipped, "a spell slot key puts the weapon away");
            Check(p.Input.FirePressed, "and the press stays for the caster");
            melee.Equip();
            MeleeInput(p, false, 0, 1);
            Call(melee, "Update");
            Check(!melee.IsEquipped, "the spell cycle puts it away too");

            // With the Cast module off, as in build mode, the weapon is idle and the press is left alone.
            Reset();
            p = MeleePlayer(out melee);
            melee.Equip();
            p.Features = ManagedCharacterFeature.All & ~ManagedCharacterFeature.Cast;
            MeleeFrame(p, melee, true);
            Check(p.Input.FirePressed, "with Cast off the press is left alone");
            Check(!melee.IsSwinging, "and nothing swings");
            Check(melee.IsEquipped, "while the weapon stays in hand");
            p.Features = ManagedCharacterFeature.All;
            MeleeFrame(p, melee, true);
            Check(melee.IsSwinging, "with Cast back on it swings");
            p.Features = ManagedCharacterFeature.All & ~ManagedCharacterFeature.Cast;
            MeleeFrame(p, melee, false);
            Check(!melee.IsSwinging, "turning Cast off ends a swing under way");

            // Equipped from the start, and toggling.
            Reset();
            p = MeleePlayer(out melee, true);
            Check(melee.IsEquipped, "the equip on start option puts the weapon in hand");
            melee.Toggle();
            Check(!melee.IsEquipped, "toggle puts it away");
            melee.Toggle();
            Check(melee.IsEquipped, "and back in hand");
            Near(melee.SwingProgress, 0f, "an idle weapon has no swing progress");

            Time.deltaTime = savedStep;
        }

        private static void MeleeBridgeTests()
        {
            Section("melee equip bridge");
            World w = NewWorld(true);
            ManagedPlayerItemBag bag;
            ManagedHud hud = NewHud(w, out bag);
            HudFrame(hud, 1f);

            var melee = w.PlayerGo.AddComponent<ManagedMelee>();
            SetField(melee, "_logHits", false);
            Call(melee, "Awake");
            melee.Weapon.ItemId = 7;
            var bridge = w.PlayerGo.AddComponent<ManagedMeleeEquipBridge>();
            Call(bridge, "Awake");
            Call(bridge, "Start");

            Check(hud.UseQuickSlot(0), "the quick slot holds the item");
            Check(melee.IsEquipped, "using the quick slot of the weapon item equips it");
            hud.UseQuickSlot(0);
            Check(!melee.IsEquipped, "using it again puts it away");
            melee.Weapon.ItemId = 99;
            hud.UseQuickSlot(0);
            Check(!melee.IsEquipped, "a quick slot with another item does not equip it");

            melee.Weapon.ItemId = 7;
            hud.Wheel.SetEntry(0, new ManagedWheelEntry("Sword", ManagedHudSprites.ItemIconKey(7)));
            hud.Wheel.SetEntry(1, new ManagedWheelEntry("Fireball", "spell_fireball"));
            Check(hud.Wheel.Pick(0), "the sword section is picked");
            Check(melee.IsEquipped, "picking the section with the weapon icon equips it");
            Check(hud.Wheel.Pick(1), "the spell section is picked");
            Check(!melee.IsEquipped, "picking any other section puts it away");
            Check(!hud.Wheel.Pick(2), "an empty section cannot be picked");
            Check(!melee.IsEquipped, "and changes nothing");

            Call(bridge, "OnDestroy");
            hud.UseQuickSlot(0);
            hud.Wheel.Pick(0);
            Check(!melee.IsEquipped, "after the bridge is destroyed the HUD no longer reaches the weapon");

            // A HUD that appears later is found on the next search.
            w = NewWorld(true);
            melee = w.PlayerGo.AddComponent<ManagedMelee>();
            SetField(melee, "_logHits", false);
            Call(melee, "Awake");
            melee.Weapon.ItemId = 7;
            bridge = w.PlayerGo.AddComponent<ManagedMeleeEquipBridge>();
            Call(bridge, "Awake");
            Call(bridge, "Start");
            Check(GetField(bridge, "_hud") == null, "with no HUD the bridge binds to nothing");
            hud = NewHud(w, out bag);
            HudFrame(hud, 1f);
            Time.frameCount = 29;
            Call(bridge, "Update");
            Check(GetField(bridge, "_hud") == null, "it does not search on every frame");
            Time.frameCount = 30;
            Call(bridge, "Update");
            Check(GetField(bridge, "_hud") != null, "it finds the HUD on the search frame");
            hud.UseQuickSlot(0);
            Check(melee.IsEquipped, "and then equips from it");
        }

        private static ManagedCharacter FacingPlayer(bool withBody, out ManagedBodyFacing facing, bool root = false)
        {
            var go = new GameObject("facing player");
            UnityEngine.CharacterController capsule = go.AddComponent<UnityEngine.CharacterController>();
            capsule.height = 2f;
            capsule.center = new Vector3(0f, 1f, 0f);
            ManagedCharacter player = go.AddComponent<ManagedCharacter>();
            SetField(player, "_createBodyView", withBody);
            Call(player, "Awake");
            facing = go.AddComponent<ManagedBodyFacing>();
            Call(facing, "Awake");
            if (root)
            {
                SetField(facing, "_body", go.transform);
            }

            Call(facing, "Start");
            return player;
        }

        private static void FacingFrame(ManagedCharacter p, ManagedBodyFacing f)
        {
            Call(p, "Update");
            Call(f, "Update");
        }

        private static void FacingTests()
        {
            float savedStep = Time.deltaTime;
            Time.deltaTime = 0.0625f;

            Section("body turn math");
            Near(ManagedBodyTurn.MoveYaw(0f, new Vector2(0f, 1f)), 0f, "forward faces the aim");
            Near(ManagedBodyTurn.MoveYaw(0f, new Vector2(1f, 0f)), 90f, "right faces 90 degrees more");
            Near(ManagedBodyTurn.MoveYaw(0f, new Vector2(-1f, 0f)), -90f, "left faces 90 degrees less");
            Near(Math.Abs(ManagedBodyTurn.MoveYaw(0f, new Vector2(0f, -1f))), 180f, "back faces the other way");
            Near(ManagedBodyTurn.MoveYaw(90f, new Vector2(1f, 0f)), 180f, "the aim yaw is added");
            Near(ManagedBodyTurn.MoveYaw(0f, new Vector2(1f, 1f)), 45f, "a diagonal faces between");
            Near(ManagedBodyTurn.Target(33f, 10f, new Vector2(1f, 0f), true, 0.1f), 10f, "face aim wins over movement");
            Near(ManagedBodyTurn.Target(33f, 10f, new Vector2(0.05f, 0f), false, 0.1f), 33f, "a move inside the dead zone keeps the body yaw");
            Near(ManagedBodyTurn.Target(33f, 10f, new Vector2(1f, 0f), false, 0.1f), 100f, "a move outside it turns to the movement");
            Near(ManagedBodyTurn.Step(0f, 90f, 720f, 0.0625f), 45f, "a step moves at most the speed times the time");
            Near(ManagedBodyTurn.Step(0f, 90f, 720f, 1f), 90f, "and stops on the target");
            float wrapped = ManagedBodyTurn.Step(350f, 10f, 100f, 0.1f);
            Near(Mathf.DeltaAngle(wrapped, 0f), 0f, "it turns the short way round the 360 mark");
            Near(ManagedBodyTurn.Step(5f, 90f, 0f, 1f), 5f, "a speed of zero leaves the yaw");
            Near(ManagedBodyTurn.Step(5f, 90f, 720f, 0f), 5f, "and so does a step of zero");

            Section("body facing");
            Reset();
            Keyboard kb = Keyboard.current = new Keyboard();
            ManagedBodyFacing facing;
            ManagedCharacter p = FacingPlayer(true, out facing);
            Check(facing.enabled, "facing runs when there is a body under the character");
            Eq(((Transform)GetField(facing, "_body")).gameObject.name, "BodyView", "and it found the created body view");
            Transform body = (Transform)GetField(facing, "_body");

            kb.dKey.isPressed = true;
            FacingFrame(p, facing);
            Near(facing.BodyYaw, 45f, "moving right turns the body toward the right, a step at a time");
            FacingFrame(p, facing);
            Near(facing.BodyYaw, 90f, "and it arrives");
            Vector3 fwd = body.rotation * Vector3.forward;
            Near(fwd.x, 1f, "the body faces right in the world"); Near(fwd.z, 0f, "not forward");
            Near(p.Yaw, 0f, "the aim yaw is not touched by the body");
            kb.dKey.isPressed = false;
            FacingFrame(p, facing); FacingFrame(p, facing);
            Near(facing.BodyYaw, 90f, "at rest the body keeps its yaw");

            // A fire press turns the body to the aim and holds it there.
            kb.fKey.wasPressedThisFrame = true; kb.dKey.isPressed = true;
            FacingFrame(p, facing);
            kb.fKey.wasPressedThisFrame = false;
            Near(facing.BodyYaw, 45f, "a fire press turns the body toward the aim");
            FacingFrame(p, facing);
            Near(facing.BodyYaw, 0f, "and it faces the aim");
            for (int i = 0; i < 4; i++) FacingFrame(p, facing);
            Near(facing.BodyYaw, 0f, "while the hold lasts, walking right does not turn it away");
            for (int i = 0; i < 8; i++) FacingFrame(p, facing);
            Near(facing.BodyYaw, 90f, "once the hold is over it turns to the movement again");

            // Without the Cast module a press means nothing, so the body keeps following the movement.
            Reset();
            kb = Keyboard.current = new Keyboard();
            p = FacingPlayer(true, out facing);
            p.Features = ManagedCharacterFeature.All & ~ManagedCharacterFeature.Cast;
            kb.fKey.wasPressedThisFrame = true; kb.dKey.isPressed = true;
            FacingFrame(p, facing);
            kb.fKey.wasPressedThisFrame = false;
            FacingFrame(p, facing);
            Near(facing.BodyYaw, 90f, "with Cast off a press does not hold the aim");

            // Without the Move module the move input is ignored.
            Reset();
            kb = Keyboard.current = new Keyboard();
            p = FacingPlayer(true, out facing);
            p.Features = ManagedCharacterFeature.All & ~ManagedCharacterFeature.Move;
            kb.dKey.isPressed = true;
            FacingFrame(p, facing); FacingFrame(p, facing);
            Near(facing.BodyYaw, 0f, "with Move off the body does not turn to the movement");

            // First person faces the aim at once.
            Reset();
            kb = Keyboard.current = new Keyboard();
            var rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            rig.SetFirstPerson(true);
            p = FacingPlayer(true, out facing);
            SetField(p, "_yaw", 30f);
            kb.dKey.isPressed = true;
            FacingFrame(p, facing);
            Near(facing.BodyYaw, 30f, "in first person the body takes the aim yaw in one frame, whatever the movement");

            // A third person view turns to the movement.
            Reset();
            kb = Keyboard.current = new Keyboard();
            rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            rig.SetFirstPerson(false);
            p = FacingPlayer(true, out facing);
            SetField(p, "_yaw", 30f);
            kb.dKey.isPressed = true;
            FacingFrame(p, facing); FacingFrame(p, facing); FacingFrame(p, facing);
            Near(facing.BodyYaw, 120f, "in third person it turns to the movement, 90 degrees past the aim");

            // No body to turn.
            Reset();
            p = FacingPlayer(false, out facing);
            Check(!facing.enabled, "with no child to turn, facing switches itself off");
            Check(Debug.Logs.Any(l => l.Contains("facing is idle")), "and says why");

            // The character root carries the aim, so it is refused as the body.
            Reset();
            p = FacingPlayer(true, out facing, true);
            Check(Debug.Warnings.Any(w => w.Contains("character root")), "the root as body is reported");
            Eq(((Transform)GetField(facing, "_body")).gameObject.name, "BodyView", "and the body view is used instead");

            Time.deltaTime = savedStep;
        }
    }
}
