using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Общие куски «древнего зала» для уровней: стены из скошенных блоков, пилястры из каменных барабанов,
    /// панели с фресками, слоистая дымка, Reflection Probe. Геометрия — настоящие блоки со швами
    /// (не только текстура), чтобы свет ложился на фаски, как на референсе.
    /// </summary>
    internal static class RuinKit
    {
        /// <summary>Точка у стены зала: центр, направление внутрь, высота пола.</summary>
        internal struct WallSpot
        {
            public Vector3 pos;
            public Vector3 inward;
        }

        /// <summary>
        /// Обходит зал по кругу и находит точки у скалы (sdf ≈ -offset).
        /// skip(x, z) — true, где стену ставить нельзя (проходы, предметы).
        /// </summary>
        internal static List<WallSpot> FindWall(Vector2 center, float maxR, int count, float offset,
            System.Func<float, float, float> sdf, System.Func<float, float, float> height, System.Func<float, float, bool> skip)
        {
            var list = new List<WallSpot>();
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 last = center;
                bool found = false;
                for (float r = 2f; r <= maxR; r += 0.25f)
                {
                    var q = center + dir * r;
                    if (sdf(q.x, q.y) > -offset) { found = true; break; }
                    last = q;
                }
                if (!found || skip(last.x, last.y)) { list.Add(new WallSpot { pos = new Vector3(float.NaN, 0f, 0f) }); continue; }
                var inward = -new Vector3(dir.x, 0f, dir.y);
                list.Add(new WallSpot { pos = new Vector3(last.x, height(last.x, last.y), last.y), inward = inward });
            }
            return list;
        }

        /// <summary>Стена из рядов блоков между a и b (по полу), лицом в сторона inward. Верх руинный.</summary>
        internal static void Wall(LowPolyMesh lp, int sub, Vector3 a, Vector3 b, Vector3 inward, float height, float thick, System.Random r, bool ruined)
        {
            var along = b - a;
            along.y = 0f;
            float len = along.magnitude;
            if (len < 0.5f) return;
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var right = rot * Vector3.right;
            if (Vector3.Dot(right, along) < 0f) { var t = a; a = b; b = t; }
            float baseY = Mathf.Min(a.y, b.y) - 0.4f;
            var origin = new Vector3(a.x, baseY, a.z);
            const float rowH = 0.55f;
            int rows = Mathf.Max(2, Mathf.RoundToInt(height / rowH));
            // Цоколь шире стены
            lp.Box(sub, Matrix4x4.TRS(origin, rot, Vector3.one), new Vector3(len * 0.5f, 0.35f, 0f), new Vector3(len + 0.3f, 0.7f, thick + 0.35f));
            for (int row = 0; row < rows; row++)
            {
                float y = 0.7f + row * rowH;
                float x = -(row % 2) * 0.45f;
                while (x < len)
                {
                    float w = 0.9f + (float)r.NextDouble() * 0.8f;
                    float x0 = Mathf.Max(0f, x), x1 = Mathf.Min(len, x + w);
                    x += w;
                    if (x1 - x0 < 0.2f) continue;
                    // Руинный верх: верхние ряды местами выпали
                    if (ruined && row >= rows - 3 && r.NextDouble() < 0.25 + (row - (rows - 3)) * 0.22) continue;
                    float inset = (float)r.NextDouble() * 0.05f;
                    var c = new Vector3((x0 + x1) * 0.5f, y + rowH * 0.5f, (float)(r.NextDouble() - 0.3) * 0.08f);
                    var tilt = Quaternion.Euler((float)(r.NextDouble() - 0.5) * 1.5f, (float)(r.NextDouble() - 0.5) * 1.5f, (float)(r.NextDouble() - 0.5) * 1.2f);
                    // Блок с фаской: сердцевина + чуть меньшая «подушка» спереди (ловит свет кромкой)
                    var m = Matrix4x4.TRS(origin, rot, Vector3.one) * Matrix4x4.TRS(c, tilt, Vector3.one);
                    // Узкий шов 2–3 см: блоки почти вплотную, разный вынос ловит свет кромками
                    lp.Box(sub, m, Vector3.zero, new Vector3(x1 - x0 - 0.025f - inset * 0.5f, rowH - 0.025f, thick));
                }
            }
        }

        /// <summary>Пилястра: база, барабаны с зазорами, капитель.</summary>
        internal static void Pilaster(LowPolyMesh lp, int sub, Vector3 p, Vector3 inward, float height, float width, System.Random r)
        {
            var rot = Quaternion.LookRotation(inward, Vector3.up);
            var m = Matrix4x4.TRS(p + Vector3.down * 0.4f, rot, Vector3.one);
            lp.Box(sub, m, new Vector3(0f, 0.45f, 0f), new Vector3(width + 0.45f, 0.9f, width + 0.45f));
            lp.Box(sub, m, new Vector3(0f, 1.05f, 0f), new Vector3(width + 0.2f, 0.3f, width + 0.2f));
            float y = 1.2f;
            while (y < height - 0.9f)
            {
                float hh = 0.55f + (float)r.NextDouble() * 0.35f;
                var jit = new Vector3((float)(r.NextDouble() - 0.5) * 0.06f, 0f, (float)(r.NextDouble() - 0.5) * 0.06f);
                lp.Box(sub, m * Matrix4x4.TRS(jit, Quaternion.Euler(0f, (float)(r.NextDouble() - 0.5) * 3f, 0f), Vector3.one),
                    new Vector3(0f, y + hh * 0.5f, 0f), new Vector3(width - 0.06f, hh - 0.06f, width - 0.06f));
                y += hh;
            }
            lp.Box(sub, m, new Vector3(0f, y + 0.18f, 0f), new Vector3(width + 0.25f, 0.36f, width + 0.25f));
            lp.Box(sub, m, new Vector3(0f, y + 0.5f, 0f), new Vector3(width + 0.5f, 0.3f, width + 0.5f));
        }

        /// <summary>Плоская панель с UV 0..1 (для фресок), лицом в normal.</summary>
        internal static Mesh Panel(string name, float w, float h)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new Vector3(-w / 2f, 0f, 0f), new Vector3(w / 2f, 0f, 0f), new Vector3(w / 2f, h, 0f), new Vector3(-w / 2f, h, 0f) });
            mesh.SetNormals(new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); // лицевая сторона смотрит в +z
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        /// <summary>Материал фрески: URP Lit, роспись + нормаль штукатурки.</summary>
        internal static Material MuralMaterial(string path, TextureKit.Pair tex, Color tint)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", 0.12f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            TextureKit.Apply(m, tex, 1f, 1.2f);
            m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Слоистая дымка: крупные очень прозрачные частицы медленно дрейфуют в объёме.
        /// Вместо объёмного тумана HDRP (в URP его нет): дешёво и даёт «воздух» в свете огня и лучей.
        /// </summary>
        internal static ParticleSystem Haze(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
            Color color, int count, float sizeMin, float sizeMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            float life = 26f;
            var ps = Zone1Builder.Particles(go, mat, count / life, life * 0.8f, life * 1.2f, 0.02f, 0.12f, sizeMin, sizeMax,
                color, color * new Color(0.92f, 0.95f, 1f, 0.85f), 0f, count, Zone1Builder.FlatCurve(), 0.12f);
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;
            var colorLife = ps.colorOverLifetime;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            colorLife.color = new ParticleSystem.MinMaxGradient(g);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sortMode = ParticleSystemSortMode.Distance;
            pr.maxParticleSize = 2f;
            return ps;
        }

        /// <summary>Отражения для металла и луж: один снимок окружения при загрузке сцены.</summary>
        internal static ReflectionProbe Probe(Transform parent, Vector3 pos, Vector3 size, float intensity)
        {
            var go = new GameObject("ReflectionProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var rp = go.AddComponent<ReflectionProbe>();
            rp.mode = ReflectionProbeMode.Realtime;
            rp.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            rp.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            rp.size = size;
            rp.resolution = 128;
            rp.intensity = intensity;
            rp.boxProjection = true;
            rp.shadowDistance = 0f;
            rp.clearFlags = ReflectionProbeClearFlags.SolidColor;
            rp.backgroundColor = Color.black;
            rp.farClipPlane = Mathf.Max(size.x, size.z);
            return rp;
        }

        /// <summary>Включить отражения окружения у металла и мокрых поверхностей (Lit по умолчанию их выключает).</summary>
        internal static void Reflective(Material m)
        {
            if (m == null) return;
            m.SetFloat("_EnvironmentReflections", 1f);
            m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(m);
        }
    }
}
