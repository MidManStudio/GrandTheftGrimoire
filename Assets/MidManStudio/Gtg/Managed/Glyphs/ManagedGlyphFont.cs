// ============================================================================
// NOTICE: Full documentation, design decisions, and fix history for this file
// live in docs/GrandTheftGrimoire/glyphs.md, section "ManagedGlyphFont.cs"
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace MidManStudio.Gtg.Managed.Glyphs
{
    /// <summary>
    /// A tiny stroke font for the element letters of a Molecule glyph: N, O, S, H, C, l, P, F, B and the digits 2 and 3.
    /// Every letter is one to three polylines in a cell one unit high, origin at the bottom left. There are no
    /// textures and no TextMesh: a letter is drawn like any other stroke, so it grows and glows with the rest.
    /// Pure arithmetic, so it runs in the CI harness.
    /// </summary>
    public static class ManagedGlyphFont
    {
        /// <summary>One line of a letter. A closed one wraps from its last point to its first.</summary>
        public struct Poly
        {
            public Vector2[] Points;
            public bool Closed;
        }

        /// <summary>Gap between two letters, in cell units.</summary>
        public const float Gap = 0.22f;

        /// <summary>True when the font has the letter.</summary>
        public static bool Has(char c)
        {
            return Width(c) > 0f;
        }

        /// <summary>Width of the letter's cell in cell units, or 0 for a letter the font does not have.</summary>
        public static float Width(char c)
        {
            switch (c)
            {
                case 'N': return 0.7f;
                case 'O': return 0.8f;
                case 'S': return 0.6f;
                case 'H': return 0.7f;
                case 'C': return 0.8f;
                case 'l': return 0.16f;
                case 'P': return 0.6f;
                case 'F': return 0.6f;
                case 'B': return 0.65f;
                case '2': return 0.6f;
                case '3': return 0.6f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Appends the lines of a letter. The cell's bottom left corner is placed at <paramref name="origin"/> and the
        /// cell is <paramref name="height"/> tall. An unknown letter adds nothing.
        /// </summary>
        public static void Append(char c, Vector2 origin, float height, List<Poly> output)
        {
            switch (c)
            {
                case 'N':
                    Open(output, origin, height, 0f, 0f, 0f, 1f, 0.7f, 0f, 0.7f, 1f);
                    break;
                case 'O':
                    output.Add(Place(Arc(0.4f, 0.5f, 0.4f, 0.5f, 0f, 360f, 16, true), origin, height, true));
                    break;
                case 'S':
                    OpenPoints(output, origin, height, S());
                    break;
                case 'H':
                    Open(output, origin, height, 0f, 0f, 0f, 1f);
                    Open(output, origin, height, 0.7f, 0f, 0.7f, 1f);
                    Open(output, origin, height, 0f, 0.5f, 0.7f, 0.5f);
                    break;
                case 'C':
                    OpenPoints(output, origin, height, Arc(0.4f, 0.5f, 0.4f, 0.5f, 40f, 320f, 14, false));
                    break;
                case 'l':
                    Open(output, origin, height, 0.08f, 0f, 0.08f, 1f);
                    break;
                case 'P':
                    Open(output, origin, height, 0f, 0f, 0f, 1f);
                    {
                        List<Vector2> bowl = new List<Vector2>();
                        bowl.Add(new Vector2(0f, 1f));
                        bowl.AddRange(Arc(0.3f, 0.75f, 0.3f, 0.25f, 90f, -90f, 8, false));
                        bowl.Add(new Vector2(0f, 0.5f));
                        OpenPoints(output, origin, height, bowl.ToArray());
                    }

                    break;
                case 'F':
                    Open(output, origin, height, 0.6f, 1f, 0f, 1f, 0f, 0f);
                    Open(output, origin, height, 0f, 0.55f, 0.45f, 0.55f);
                    break;
                case 'B':
                    Open(output, origin, height, 0f, 0f, 0f, 1f);
                    {
                        List<Vector2> top = new List<Vector2>();
                        top.Add(new Vector2(0f, 1f));
                        top.AddRange(Arc(0.33f, 0.77f, 0.3f, 0.23f, 90f, -90f, 7, false));
                        top.Add(new Vector2(0f, 0.54f));
                        OpenPoints(output, origin, height, top.ToArray());

                        List<Vector2> bottom = new List<Vector2>();
                        bottom.Add(new Vector2(0f, 0.54f));
                        bottom.AddRange(Arc(0.33f, 0.27f, 0.32f, 0.27f, 90f, -90f, 7, false));
                        bottom.Add(new Vector2(0f, 0f));
                        OpenPoints(output, origin, height, bottom.ToArray());
                    }

                    break;
                case '2':
                    {
                        List<Vector2> two = new List<Vector2>();
                        two.AddRange(Arc(0.3f, 0.72f, 0.3f, 0.28f, 160f, -40f, 9, false));
                        two.Add(new Vector2(0f, 0f));
                        two.Add(new Vector2(0.6f, 0f));
                        OpenPoints(output, origin, height, two.ToArray());
                    }

                    break;
                case '3':
                    {
                        List<Vector2> three = new List<Vector2>();
                        three.AddRange(Arc(0.3f, 0.74f, 0.3f, 0.25f, 150f, -90f, 9, false));
                        three.AddRange(Arc(0.3f, 0.26f, 0.3f, 0.26f, 90f, -150f, 9, false));
                        OpenPoints(output, origin, height, three.ToArray());
                    }

                    break;
            }
        }

        // The two bowls of an S, one point list. The upper arc turns counter clockwise, the lower one clockwise, and they meet at (0.3, 0.5).
        private static Vector2[] S()
        {
            List<Vector2> points = new List<Vector2>();
            points.AddRange(Arc(0.3f, 0.75f, 0.3f, 0.25f, 35f, 270f, 14, false));
            points.AddRange(Arc(0.3f, 0.25f, 0.3f, 0.25f, 90f, -145f, 14, false));
            return points.ToArray();
        }

        // Points of an elliptical arc. Angles are in degrees. A full closed ellipse leaves out the repeated last point.
        private static Vector2[] Arc(float cx, float cy, float rx, float ry, float fromDeg, float toDeg, int steps, bool closed)
        {
            int count = closed ? steps : steps + 1;
            Vector2[] points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float a = (fromDeg + (toDeg - fromDeg) * i / steps) * Mathf.Deg2Rad;
                points[i] = new Vector2(cx + rx * Mathf.Cos(a), cy + ry * Mathf.Sin(a));
            }

            return points;
        }

        // A polyline from cell coordinates given as x, y pairs.
        private static void Open(List<Poly> output, Vector2 origin, float height, params float[] xy)
        {
            Vector2[] points = new Vector2[xy.Length / 2];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            }

            OpenPoints(output, origin, height, points);
        }

        private static void OpenPoints(List<Poly> output, Vector2 origin, float height, Vector2[] cellPoints)
        {
            output.Add(Place(cellPoints, origin, height, false));
        }

        private static Poly Place(Vector2[] cellPoints, Vector2 origin, float height, bool closed)
        {
            Vector2[] points = new Vector2[cellPoints.Length];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = origin + cellPoints[i] * height;
            }

            return new Poly { Points = points, Closed = closed };
        }
    }
}
