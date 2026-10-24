// Checks for the camera rig wiring, the eye point and the feet position. Verification harness only.
using System;
using System.Linq;
using MidManStudio.Gtg.Managed.Camera;
using MidManStudio.Gtg.Managed.CharacterController;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Building.Tests
{
    public static partial class Tests
    {
        // Stand-ins for the Cinemachine parts. The rig reaches them by name and by property, so these only
        // need the same names.
        private sealed class FakeVcam : MonoBehaviour
        {
            public Transform Follow { get; set; }
            public Transform LookAt { get; set; }
        }

        private sealed class CinemachinePOV : MonoBehaviour { }

        private sealed class CinemachineSameAsFollowTarget : MonoBehaviour { }

        private sealed class ReadOnlyFollow : MonoBehaviour
        {
            public Transform Follow { get { return null; } }
        }

        private static ManagedCharacter MakeRigPlayer(Vector3 pivot, Vector3 capsuleCenter, bool withController)
        {
            var go = new GameObject("rig player");
            go.transform.position = pivot;
            ManagedCharacter player = go.AddComponent<ManagedCharacter>();
            if (withController)
            {
                var cc = go.AddComponent<UnityEngine.CharacterController>();
                cc.height = 2f;
                cc.center = capsuleCenter;
            }

            return player;
        }

        private static GameObject MakeVcam(string name, bool active, Transform follow, bool withPov)
        {
            var cam = new GameObject(name);
            cam.AddComponent<FakeVcam>().Follow = follow;
            var pipeline = new GameObject("cm");
            pipeline.transform.SetParent(cam.transform, false);
            pipeline.AddComponent<CinemachineSameAsFollowTarget>();
            if (withPov) pipeline.AddComponent<CinemachinePOV>();
            cam.SetActive(active);
            return cam;
        }

        private static Transform RigTarget(ManagedCameraRig rig) { return (Transform)GetField(rig, "_cameraTarget"); }

        private static void RigTests()
        {
            Section("rig feet");
            Reset();
            ManagedCharacter mid = MakeRigPlayer(new Vector3(0f, 1.15f, 0f), Vector3.zero, true);
            Near(mid.FeetPosition.y, 0.15f, "a pivot at the middle of a 2 m capsule puts the feet 1 m below it");
            ManagedCharacter low = MakeRigPlayer(new Vector3(2f, 0f, 3f), new Vector3(0f, 1f, 0f), true);
            Near(low.FeetPosition.y, 0f, "a pivot at the feet is its own feet position");
            Near(low.FeetPosition.x, 2f, "x follows the pivot"); Near(low.FeetPosition.z, 3f, "z follows the pivot");
            ManagedCharacter bare = MakeRigPlayer(new Vector3(1f, 5f, 1f), Vector3.zero, false);
            Near(bare.FeetPosition.y, 5f, "with no controller the pivot is the feet");

            Section("rig eye");
            Reset();
            ManagedCharacter p = MakeRigPlayer(new Vector3(3f, 1.15f, 4f), Vector3.zero, true);
            SetField(p, "_yaw", 90f); SetField(p, "_pitch", 20f);
            var rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake"); Call(rig, "Start"); Call(rig, "LateUpdate");
            Transform target = RigTarget(rig);
            Near(target.position.x, 3f, "the target sits over the character in x"); Near(target.position.z, 4f, "and in z");
            Near(target.position.y, 0.15f + 1.6f, "the eye is the eye height above the feet, not above the pivot");
            Vector3 fwd = target.rotation * Vector3.forward;
            Vector3 want = Quaternion.Euler(20f, 90f, 0f) * Vector3.forward;
            Near(fwd.x, want.x, "the target turns to the yaw and the pitch, x"); Near(fwd.y, want.y, "y"); Near(fwd.z, want.z, "z");
            Check(fwd.y < 0f, "a positive pitch looks down");

            Section("rig order");
            object[] order = typeof(ManagedCameraRig).GetCustomAttributes(typeof(DefaultExecutionOrderAttribute), false);
            Eq(order.Length, 1, "the rig declares an execution order");
            Check(((DefaultExecutionOrderAttribute)order[0]).order < 100, "it runs before the Cinemachine Brain, which is at 100");
            Check(((DefaultExecutionOrderAttribute)order[0]).order > -50, "and after the character, which is at -50");

            Section("rig wiring");
            Reset();
            p = MakeRigPlayer(new Vector3(0f, 1f, 0f), Vector3.zero, true);
            rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            target = RigTarget(rig);
            GameObject third = MakeVcam("Third", true, p.transform, false);
            GameObject first = MakeVcam("First", false, p.transform, true);
            SetField(rig, "_thirdPersonCamera", third); SetField(rig, "_firstPersonCamera", first);
            Call(rig, "Start");
            Check(third.GetComponent<FakeVcam>().Follow == target, "the third person camera follows the target");
            Check(first.GetComponent<FakeVcam>().Follow == target, "an inactive first person camera is wired too");
            Check(Debug.Warnings.Any(w => w.Contains("Third") && w.Contains("followed rig player")), "the old Follow is named in a warning");
            Check(Debug.Warnings.Any(w => w.Contains("First") && w.Contains("CinemachinePOV")), "a part that turns the view on its own is named");
            Check(!Debug.Warnings.Any(w => w.Contains("Third") && w.Contains("CinemachinePOV")), "and only on the camera that has it");
            Check(first.activeSelf == false, "the first person camera is switched off in third person");
            Check(third.activeSelf, "and the third person one stays on");

            // A camera that already follows the target is left alone without a word.
            Reset();
            p = MakeRigPlayer(new Vector3(0f, 1f, 0f), Vector3.zero, true);
            rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            target = RigTarget(rig);
            third = MakeVcam("Third", true, target, false);
            SetField(rig, "_thirdPersonCamera", third);
            Call(rig, "Start");
            Check(third.GetComponent<FakeVcam>().Follow == target, "a camera that already follows the target keeps it");
            Eq(Debug.Warnings.Count, 0, "and nothing is reported");

            // With wiring off the Follow stays where the scene put it.
            Reset();
            p = MakeRigPlayer(new Vector3(0f, 1f, 0f), Vector3.zero, true);
            rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            third = MakeVcam("Third", true, p.transform, false);
            SetField(rig, "_thirdPersonCamera", third); SetField(rig, "_wireCameras", false);
            Call(rig, "Start");
            Check(third.GetComponent<FakeVcam>().Follow == p.transform, "with wiring off the Follow is untouched");
            Eq(Debug.Warnings.Count, 0, "and nothing is reported");

            // A camera object with nothing to point is reported once and does not stop the rest.
            Reset();
            p = MakeRigPlayer(new Vector3(0f, 1f, 0f), Vector3.zero, true);
            rig = new GameObject("rig").AddComponent<ManagedCameraRig>();
            Call(rig, "Awake");
            target = RigTarget(rig);
            GameObject plain = new GameObject("Plain");
            first = MakeVcam("First", true, null, false);
            SetField(rig, "_thirdPersonCamera", plain); SetField(rig, "_firstPersonCamera", first);
            Call(rig, "Start");
            Check(Debug.Warnings.Any(w => w.Contains("Plain") && w.Contains("no component with a Follow target")), "a camera with no Follow property is reported");
            Check(first.GetComponent<FakeVcam>().Follow == target, "the other camera is still wired");
            Check(Debug.Warnings.Any(w => w.Contains("First") && w.Contains("followed nothing")), "a camera with no Follow says it followed nothing");

            Section("camera wiring helper");
            Reset();
            var t = new GameObject("t").transform;
            Transform prev;
            Check(!ManagedCameraWiring.PointFollow(null, t, out prev), "no camera object returns false");
            Check(!ManagedCameraWiring.PointFollow(new GameObject("x"), null, out prev), "no target returns false");
            var ro = new GameObject("ro"); ro.AddComponent<ReadOnlyFollow>();
            Check(!ManagedCameraWiring.PointFollow(ro, t, out prev), "a Follow that cannot be written does not count");
            var ok = MakeVcam("ok", true, null, false);
            Check(ManagedCameraWiring.PointFollow(ok, t, out prev), "a writable Follow is set");
            Check(prev == null, "the previous Follow was nothing");
            Check(ok.GetComponent<FakeVcam>().Follow == t, "the new Follow is the target");
            Eq(ManagedCameraWiring.FindSelfRotating(null), "", "no object has no self rotating parts");
            Eq(ManagedCameraWiring.FindSelfRotating(ok), "", "a camera with Same As Follow Target has none");
            var pov = MakeVcam("pov", true, null, true);
            Eq(ManagedCameraWiring.FindSelfRotating(pov), "CinemachinePOV", "POV is found under the camera");
        }
    }
}
