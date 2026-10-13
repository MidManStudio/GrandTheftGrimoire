// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/building.md, section "ManagedPlacedRenderer.cs"
// ============================================================================

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace MidManStudio.Gtg.Managed.Building
{
    /// <summary>
    /// Draws every placed piece without a GameObject each. The store keeps one array of matrices
    /// per definition, in blocks of 1023, and each block goes to the GPU as one instanced draw
    /// straight from its array. The ghost that follows the cursor and the hover marker are single
    /// box draws. Call <see cref="DrawPieces"/> once per frame.
    /// </summary>
    public sealed class ManagedPlacedRenderer : IDisposable
    {
        // Slightly larger than the piece, so a ghost standing on the ground does not fight it for depth.
        private const float OverlayInflate = 1.02f;

        private static readonly Color ValidColor = new Color(0.25f, 0.55f, 1f, 0.55f);
        private static readonly Color InvalidColor = new Color(1f, 0.2f, 0.2f, 0.55f);
        private static readonly Color HoverColor = new Color(1f, 0.9f, 0.3f, 0.35f);

        private readonly ManagedPlacedStore _store;
        private readonly int _layer;
        private readonly Mesh _box;
        private readonly Material[] _pieceMaterials;
        private readonly Material _validMaterial;
        private readonly Material _invalidMaterial;
        private readonly Material _hoverMaterial;

        /// <param name="pieceTemplate">Lit material the piece materials are copied from. Null looks up a URP Lit shader by name, which a player build only finds when something else references it.</param>
        public ManagedPlacedRenderer(ManagedPlacedStore store, int layer, Material pieceTemplate)
        {
            _store = store;
            _layer = layer;
            _box = CreateBoxMesh();

            _pieceMaterials = new Material[ManagedBuildableCatalog.Count];
            for (int i = 0; i < _pieceMaterials.Length; i++)
            {
                _pieceMaterials[i] = CreatePieceMaterial(pieceTemplate, ManagedBuildableCatalog.Get(i).Color);
            }

            _validMaterial = CreateOverlayMaterial(ValidColor);
            _invalidMaterial = CreateOverlayMaterial(InvalidColor);
            _hoverMaterial = CreateOverlayMaterial(HoverColor);
        }

        public void DrawPieces()
        {
            for (int type = 0; type < _pieceMaterials.Length; type++)
            {
                Material material = _pieceMaterials[type];
                ManagedPieceBucket bucket = _store.GetBucket(type);
                if (material == null || bucket.Count == 0)
                {
                    continue;
                }

                int chunks = bucket.ChunkCount;
                for (int c = 0; c < chunks; c++)
                {
                    Graphics.DrawMeshInstanced(
                        _box, 0, material, bucket.GetChunk(c), bucket.GetChunkLength(c), null,
                        ShadowCastingMode.On, true, _layer);
                }
            }
        }

        /// <summary>Draws the box that shows where the piece would go. Blue when it can stay, red when it cannot.</summary>
        public void DrawGhost(ManagedBuildableDefinition def, ManagedPlacementResult result, int quarter)
        {
            if (result.Status == ManagedPlacementStatus.NoAim)
            {
                return;
            }

            Material material = result.IsValid ? _validMaterial : _invalidMaterial;
            DrawOverlay(def, result.CellX, result.CellZ, quarter, result.GroundY, material);
        }

        /// <summary>Marks the placed piece the cursor is over.</summary>
        public void DrawHover(int pieceId)
        {
            ManagedPlacedPiece piece;
            if (!_store.TryGet(pieceId, out piece))
            {
                return;
            }

            DrawOverlay(
                ManagedBuildableCatalog.Get(piece.TypeIndex), piece.CellX, piece.CellZ, piece.Quarter,
                piece.GroundY, _hoverMaterial);
        }

        public void Dispose()
        {
            for (int i = 0; i < _pieceMaterials.Length; i++)
            {
                DestroyObject(_pieceMaterials[i]);
                _pieceMaterials[i] = null;
            }

            DestroyObject(_validMaterial);
            DestroyObject(_invalidMaterial);
            DestroyObject(_hoverMaterial);
            DestroyObject(_box);
        }

        private void DrawOverlay(
            ManagedBuildableDefinition def, int cellX, int cellZ, int quarter, float groundY, Material material)
        {
            if (material == null)
            {
                return;
            }

            Matrix4x4 matrix = ManagedPlacedStore.BuildMatrix(def, _store.Grid.CellSize, cellX, cellZ, quarter, groundY)
                * Matrix4x4.Scale(new Vector3(OverlayInflate, OverlayInflate, OverlayInflate));
            Graphics.DrawMesh(_box, matrix, material, _layer);
        }

        private static Mesh CreateBoxMesh()
        {
            Vector3[] vertices;
            Vector3[] normals;
            int[] triangles;
            ManagedBoxMesh.Build(out vertices, out normals, out triangles);

            var mesh = new Mesh { name = "GtgBuildBox" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;

            // White vertex color, so the overlay shader that multiplies by it shows the pure tint.
            var white = new Color32[vertices.Length];
            for (int i = 0; i < white.Length; i++)
            {
                white[i] = new Color32(255, 255, 255, 255);
            }

            mesh.colors32 = white;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreatePieceMaterial(Material template, Color color)
        {
            Material material;
            if (template != null)
            {
                material = new Material(template);
            }
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                if (shader == null)
                {
                    Debug.LogError("[GTG Build] No usable shader found. Placed pieces will not draw.");
                    return null;
                }

                material = new Material(shader);
            }

            // URP Lit names its tint _BaseColor and the built in shaders name it _Color.
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            material.enableInstancing = true;
            return material;
        }

        // Sprites/Default is an unlit alpha blended shader that every build includes. It multiplies
        // by vertex color and by _Color, which is all a ghost needs.
        private static Material CreateOverlayMaterial(Color color)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[GTG Build] Sprites/Default not found. The placement ghost will not draw.");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_Color", color);
            return material;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
