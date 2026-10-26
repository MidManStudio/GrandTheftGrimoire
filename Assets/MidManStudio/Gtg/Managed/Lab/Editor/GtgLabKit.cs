// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/lab.md, section "GtgLabKit.cs"
// ============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MidManStudio.Gtg.Managed.Lab
{
    /// <summary>
    /// Small helpers shared by the Editor test tools of every feature folder. Editor only. It reads no
    /// game folder, so a feature lab can use it without pulling another feature in.
    /// </summary>
    internal static class GtgLabKit
    {
        internal const int LayerGround = 6;
        internal const int LayerPlayer = 7;
        internal const int LayerWall = 8;
        internal const int LayerNpc = 9;

        // Ground only, for build mode.
        internal const int GroundMask = 1 << LayerGround;

        // Ground, Player and Wall: what an NPC sight ray may hit (448).
        internal const int SightMask = (1 << LayerGround) | (1 << LayerPlayer) | (1 << LayerWall);

        // Ground, Wall and Npc: what a spell or a sword may hit, which leaves the player out (832).
        internal const int HitMask = (1 << LayerGround) | (1 << LayerWall) | (1 << LayerNpc);

        private const string MaterialFolder = "Assets/MidManStudio/Gtg/Lab/Materials";

        internal static void Log(string message)
        {
            Debug.Log("[GTG Lab] " + message);
        }

        internal static void Warn(string message)
        {
            Debug.LogWarning("[GTG Lab] " + message);
        }

        /// <summary>True in Play mode. Otherwise logs one line that says to press Play.</summary>
        internal static bool RequirePlaying(string action)
        {
            if (Application.isPlaying)
            {
                return true;
            }

            Warn(action + " works in Play mode. Press Play first.");
            return false;
        }

        /// <summary>Names layers 6 to 9 Ground, Player, Wall and Npc when those slots are empty.</summary>
        internal static void EnsureLayerNames()
        {
            SetLayerName(LayerGround, "Ground");
            SetLayerName(LayerPlayer, "Player");
            SetLayerName(LayerWall, "Wall");
            SetLayerName(LayerNpc, "Npc");
        }

        private static void SetLayerName(int index, string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            SerializedObject manager = new SerializedObject(assets[0]);
            SerializedProperty layers = manager.FindProperty("layers");
            if (layers == null || index >= layers.arraySize)
            {
                return;
            }

            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (!string.IsNullOrEmpty(slot.stringValue))
            {
                return;
            }

            slot.stringValue = layerName;
            manager.ApplyModifiedProperties();
        }

        /// <summary>The first root object of the open scene with this name, or null.</summary>
        internal static GameObject FindRoot(string objectName)
        {
            GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == objectName)
                {
                    return roots[i];
                }
            }

            return null;
        }

        /// <summary>A new empty object, registered for undo.</summary>
        internal static GameObject NewObject(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            return go;
        }

        /// <summary>The component when the object has it, otherwise a new one.</summary>
        internal static T Ensure<T>(GameObject go) where T : Component
        {
            T found = go.GetComponent<T>();
            return found != null ? found : Undo.AddComponent<T>(go);
        }

        /// <summary>A primitive with a flat color material and, optionally, no collider.</summary>
        internal static GameObject Primitive(PrimitiveType type, string objectName, Transform parent,
            Vector3 localPosition, Vector3 localScale, string materialName, Color color, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
            go.name = objectName;
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }

            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            Renderer drawn = go.GetComponent<Renderer>();
            Material material = ColorMaterial(materialName, color);
            if (drawn != null && material != null)
            {
                drawn.sharedMaterial = material;
            }

            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            return go;
        }

        /// <summary>A material asset under Assets/MidManStudio/Gtg/Lab/Materials, created on first use.</summary>
        internal static Material ColorMaterial(string materialName, Color color)
        {
            string path = MaterialFolder + "/Lab_" + materialName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                return null;
            }

            EnsureFolder(MaterialFolder);
            Material created = new Material(shader);
            if (created.HasProperty("_BaseColor"))
            {
                created.SetColor("_BaseColor", color);
            }

            if (created.HasProperty("_Color"))
            {
                created.SetColor("_Color", color);
            }

            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        /// <summary>Marks the scene of the object changed, selects the object and logs the message.</summary>
        internal static void Finish(GameObject focus, string message)
        {
            if (focus != null)
            {
                if (!Application.isPlaying)
                {
                    EditorSceneManager.MarkSceneDirty(focus.scene);
                }

                Selection.activeGameObject = focus;
                EditorGUIUtility.PingObject(focus);
            }

            Log(message);
        }

        /// <summary>The point the Scene view looks at, flattened to the ground. The origin without a Scene view.</summary>
        internal static Vector3 ViewPivot()
        {
            SceneView view = SceneView.lastActiveSceneView;
            Vector3 pivot = view != null ? view.pivot : Vector3.zero;
            return new Vector3(pivot.x, 0f, pivot.z);
        }

        // Serialized field setters. They write private [SerializeField] fields by name, with undo. A name
        // that does not exist logs a warning and changes nothing, so a renamed field never fails silently.

        internal static bool SetFloat(Object target, string field, float value)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.floatValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        /// <summary>Integers, enums and layer masks.</summary>
        internal static bool SetInt(Object target, string field, int value)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.intValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        internal static bool SetBool(Object target, string field, bool value)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.boolValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        internal static bool SetVector3(Object target, string field, Vector3 value)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.vector3Value = value;
            so.ApplyModifiedProperties();
            return true;
        }

        internal static bool SetObject(Object target, string field, Object value)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
            return true;
        }

        internal static bool SetVector3Array(Object target, string field, Vector3[] values)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).vector3Value = values[i];
            }

            so.ApplyModifiedProperties();
            return true;
        }

        internal static bool ClearObjectArray(Object target, string field)
        {
            SerializedObject so;
            SerializedProperty property = Find(target, field, out so);
            if (property == null)
            {
                return false;
            }

            property.arraySize = 0;
            so.ApplyModifiedProperties();
            return true;
        }

        internal static SerializedProperty Find(Object target, string field, out SerializedObject so)
        {
            so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Warn(target.GetType().Name + " has no serialized field " + field + ", so it was left as it is.");
            }

            return property;
        }
    }
}
