using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Сборщик low-poly мешей с плоскими гранями: каждый треугольник со своими вершинами,
    /// поэтому свет ложится «гранёно». Подмеши = материалы (по индексу).
    /// Ориентация граней исправляется автоматически по направлению «наружу».
    /// </summary>
    public class LowPolyMesh
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int>[] subs;
        bool useColors;

        public LowPolyMesh(int submeshCount)
        {
            subs = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++) subs[i] = new List<int>();
        }

        public int VertexCount => verts.Count;
        public bool IsEmpty => verts.Count == 0;

        /// <summary>Треугольник. outDir — куда должна смотреть лицевая сторона.</summary>
        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outDir)
        {
            Tri(sub, a, b, c, outDir, Color.white);
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 outDir, Color color)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outDir) < 0f)
            {
                var t = b; b = c; c = t;
                n = -n;
            }
            n.Normalize();
            if (color != Color.white) useColors = true;
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colors.Add(color); colors.Add(color); colors.Add(color);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        /// <summary>Треугольник с нормалями в вершинах: гладкое затенение (песчаный пол).</summary>
        public void TriSmooth(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, na + nb + nc) < 0f)
            {
                var t = b; b = c; c = t;
                var tn = nb; nb = nc; nc = tn;
            }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(na); normals.Add(nb); normals.Add(nc);
            colors.Add(Color.white); colors.Add(Color.white); colors.Add(Color.white);
            subs[sub].Add(i); subs[sub].Add(i + 1); subs[sub].Add(i + 2);
        }

        /// <summary>
        /// Тело вращения: кольца на высотах h с радиусами r (r = 0 — вершина-острие).
        /// Подходит для камней, сталагмитов, кристаллов, грибов, палаток, факелов, тел.
        /// </summary>
        public void Lathe(int sub, Matrix4x4 m, float[] h, float[] r, int sides,
            System.Random rng = null, float jitter = 0f, bool capBottom = false, float twist = 0f)
        {
            int rings = h.Length;
            var ring = new Vector3[rings][];
            for (int k = 0; k < rings; k++)
            {
                if (r[k] <= 0.0001f)
                {
                    ring[k] = new[] { new Vector3(0f, h[k], 0f) };
                    continue;
                }
                ring[k] = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float a = (j + twist * k) * Mathf.PI * 2f / sides;
                    float rr = r[k];
                    float hh = h[k];
                    if (rng != null && jitter > 0f)
                    {
                        a += ((float)rng.NextDouble() - 0.5f) * jitter * Mathf.PI / sides;
                        rr *= 1f + ((float)rng.NextDouble() - 0.5f) * 2f * jitter;
                        hh += ((float)rng.NextDouble() - 0.5f) * jitter * r[k] * 0.5f;
                    }
                    ring[k][j] = new Vector3(Mathf.Cos(a) * rr, hh, Mathf.Sin(a) * rr);
                }
            }

            for (int k = 0; k < rings - 1; k++)
            {
                var lo = ring[k];
                var hi = ring[k + 1];
                if (lo.Length == 1 && hi.Length == 1) continue;
                if (hi.Length == 1)
                {
                    for (int j = 0; j < sides; j++)
                        SideTri(sub, m, lo[j], lo[(j + 1) % sides], hi[0]);
                }
                else if (lo.Length == 1)
                {
                    for (int j = 0; j < sides; j++)
                        SideTri(sub, m, lo[0], hi[j], hi[(j + 1) % sides]);
                }
                else
                {
                    for (int j = 0; j < sides; j++)
                    {
                        var a = lo[j];
                        var b = lo[(j + 1) % sides];
                        var c = hi[(j + 1) % sides];
                        var d = hi[j];
                        SideTri(sub, m, a, b, c);
                        SideTri(sub, m, a, c, d);
                    }
                }
            }

            // Крышки
            var top = ring[rings - 1];
            if (top.Length > 1)
            {
                var center = Avg(top);
                for (int j = 0; j < sides; j++)
                    TriLocal(sub, m, center, top[j], top[(j + 1) % sides], Vector3.up);
            }
            var bottom = ring[0];
            if (capBottom && bottom.Length > 1)
            {
                var center = Avg(bottom);
                for (int j = 0; j < sides; j++)
                    TriLocal(sub, m, center, bottom[j], bottom[(j + 1) % sides], Vector3.down);
            }
        }

        /// <summary>Коробка в локальных координатах матрицы m.</summary>
        public void Box(int sub, Matrix4x4 m, Vector3 center, Vector3 size)
        {
            var e = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + new Vector3(x * e.x, y * e.y, z * e.z);
            Face(sub, m, center, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1));     // +z
            Face(sub, m, center, P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1)); // -z
            Face(sub, m, center, P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1));     // +x
            Face(sub, m, center, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1)); // -x
            Face(sub, m, center, P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1));     // +y
            Face(sub, m, center, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1)); // -y
        }

        void Face(int sub, Matrix4x4 m, Vector3 boxCenter, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var wa = m.MultiplyPoint3x4(a);
            var wb = m.MultiplyPoint3x4(b);
            var wc = m.MultiplyPoint3x4(c);
            var wd = m.MultiplyPoint3x4(d);
            var faceCenter = (wa + wb + wc + wd) * 0.25f;
            var outDir = faceCenter - m.MultiplyPoint3x4(boxCenter);
            Tri(sub, wa, wb, wc, outDir);
            Tri(sub, wa, wc, wd, outDir);
        }

        void SideTri(int sub, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c)
        {
            var centroid = (a + b + c) / 3f;
            var axis = new Vector3(0f, centroid.y, 0f);
            var wa = m.MultiplyPoint3x4(a);
            var wb = m.MultiplyPoint3x4(b);
            var wc = m.MultiplyPoint3x4(c);
            var outDir = m.MultiplyPoint3x4(centroid) - m.MultiplyPoint3x4(axis);
            Tri(sub, wa, wb, wc, outDir);
        }

        void TriLocal(int sub, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c, Vector3 localOut)
        {
            Tri(sub, m.MultiplyPoint3x4(a), m.MultiplyPoint3x4(b), m.MultiplyPoint3x4(c), m.MultiplyVector(localOut));
        }

        static Vector3 Avg(Vector3[] pts)
        {
            var s = Vector3.zero;
            foreach (var p in pts) s += p;
            return s / pts.Length;
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            if (useColors) mesh.SetColors(colors);
            mesh.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Меш из светящихся точек (светлячки, искры) для шейдера FalseParadise/UnlitGlow:
    /// каждая точка — два скрещённых ромба, цвет в вершинах.
    /// </summary>
    public class GlowPointsMesh
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();

        public int Count => verts.Count;

        public void Point(Vector3 p, float size, Color color, float yaw)
        {
            var right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * size;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * size;
            var up = Vector3.up * size * 1.4f;
            Diamond(p, right, up, color);
            Diamond(p, fwd, up, color);
        }

        void Diamond(Vector3 p, Vector3 side, Vector3 up, Color c)
        {
            int i = verts.Count;
            verts.Add(p + up); verts.Add(p + side); verts.Add(p - up); verts.Add(p - side);
            for (int k = 0; k < 4; k++) colors.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
