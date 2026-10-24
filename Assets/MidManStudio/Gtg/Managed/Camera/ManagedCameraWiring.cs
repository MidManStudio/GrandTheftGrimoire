// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/managed.md, section "ManagedCameraWiring.cs"
// ============================================================================

using System.Reflection;
using System.Text;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Camera
{
    /// <summary>
    /// Points a virtual camera at the rig's follow target and finds camera parts that turn the
    /// view on their own. It reaches the camera through reflection, so the scripts never
    /// reference Cinemachine and compile with any version that has a Follow property.
    /// </summary>
    internal static class ManagedCameraWiring
    {
        // Component type names that rotate the camera from their own input, so the view stops
        // following the character. Matched by name, because the types are not referenced.
        private static readonly string[] SelfRotating = { "CinemachinePOV", "CinemachineOrbitalTransposer" };

        /// <summary>
        /// Sets Follow on the first component under <paramref name="cameraObject"/> that has a
        /// writable Transform property named Follow. Inactive children are searched too.
        /// Returns false when there is no such component. <paramref name="previous"/> is the
        /// Follow it had, and the property is only written when that differs from the target.
        /// </summary>
        internal static bool PointFollow(GameObject cameraObject, Transform target, out Transform previous)
        {
            previous = null;
            if (cameraObject == null || target == null)
            {
                return false;
            }

            MonoBehaviour[] parts = cameraObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                PropertyInfo follow = parts[i].GetType().GetProperty(
                    "Follow", BindingFlags.Public | BindingFlags.Instance, null, typeof(Transform), System.Type.EmptyTypes, null);
                if (follow == null || !follow.CanRead || !follow.CanWrite)
                {
                    continue;
                }

                previous = (Transform)follow.GetValue(parts[i], null);
                if (previous != target)
                {
                    follow.SetValue(parts[i], target, null);
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Type names, comma separated, of the parts under <paramref name="cameraObject"/> that
        /// turn the camera by their own input. Empty when there are none.
        /// </summary>
        internal static string FindSelfRotating(GameObject cameraObject)
        {
            if (cameraObject == null)
            {
                return string.Empty;
            }

            StringBuilder found = new StringBuilder();
            MonoBehaviour[] parts = cameraObject.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null)
                {
                    continue;
                }

                string name = parts[i].GetType().Name;
                for (int n = 0; n < SelfRotating.Length; n++)
                {
                    if (name == SelfRotating[n])
                    {
                        if (found.Length > 0)
                        {
                            found.Append(", ");
                        }

                        found.Append(name);
                    }
                }
            }

            return found.ToString();
        }
    }
}
