using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Процедурные 3D-меши и детализированные элементы окружения для всех карт False Paradise.
    /// Меши сохраняются в Assets/_Game/Art/GeneratedMeshes/, чтобы сохраняться в сценах и билдах.
    /// </summary>
    public static class EnvironmentProps
    {
        public const string MeshDir = "Assets/_Game/Art/GeneratedMeshes";

        // ==================== ПРОЦЕДУРНЫЕ МЕШИ ====================

        public static Mesh GetOrCreateCone(string name = "Cone12", int sides = 12)
        {
            SandboxBuilder.EnsureFolder(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Боковая поверхность
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                int b = verts.Count;
                verts.Add(new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f));
                verts.Add(new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f));
                verts.Add(new Vector3(0f, 1f, 0f));
                uvs.Add(new Vector2(i / (float)sides, 0f));
                uvs.Add(new Vector2((i + 1) / (float)sides, 0f));
                uvs.Add(new Vector2((i + 0.5f) / sides, 1f));
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            }

            // Дно
            int center = verts.Count;
            verts.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                uvs.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.Add(center); tris.Add(center + 1 + i); tris.Add(center + 1 + next);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh GetOrCreateRock(int variant)
        {
            SandboxBuilder.EnsureFolder(MeshDir);
            string name = $"Rock_{variant}";
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            // Икосаэдр с подразделением + деформация шумом для естественных граней камня
            var mesh = new Mesh { name = name };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            int lat = 6, lon = 10;
            var grid = new Vector3[(lat + 1) * (lon + 1)];
            float seed = variant * 17.3f + 3.1f;

            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat;
                float phi = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 dir = new Vector3(
                        Mathf.Sin(phi) * Mathf.Cos(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Sin(theta));

                    float n = Mathf.PerlinNoise(dir.x * 1.8f + seed, dir.z * 1.8f + dir.y * 1.3f + seed);
                    float n2 = Mathf.PerlinNoise(dir.y * 3.1f + seed * 2f, dir.x * 3.1f + seed);
                    float r = 0.65f + n * 0.42f + (n2 - 0.5f) * 0.22f;
                    Vector3 p = dir * r;
                    if (p.y < -0.35f) p.y = -0.35f + (p.y + 0.35f) * 0.25f; // плоское основание
                    grid[y * (lon + 1) + x] = p;
                }
            }

            // Плоские (faceted) грани для выразительного рельефа
            for (int y = 0; y < lat; y++)
            {
                for (int x = 0; x < lon; x++)
                {
                    int i00 = y * (lon + 1) + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + (lon + 1);
                    int i11 = i01 + 1;

                    AddTri(verts, uvs, tris, grid[i00], grid[i10], grid[i11]);
                    AddTri(verts, uvs, tris, grid[i00], grid[i11], grid[i01]);
                }
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static Mesh GetOrCreateParadiseTerrain(string name, float size, int res, Vector3 pondPos, Vector3 castlePos, Vector3 goddessPos)
        {
            SandboxBuilder.EnsureFolder(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            var mesh = new Mesh { name = name };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            int count = (res + 1) * (res + 1);
            var verts = new Vector3[count];
            var uvs = new Vector2[count];
            var tris = new int[res * res * 6];

            float half = size * 0.5f;
            for (int z = 0; z <= res; z++)
            {
                float fz = z / (float)res;
                float wz = Mathf.Lerp(-half, half, fz);
                for (int x = 0; x <= res; x++)
                {
                    float fx = x / (float)res;
                    float wx = Mathf.Lerp(-half, half, fx);

                    float h = SampleParadiseHeight(wx, wz, pondPos, castlePos, goddessPos);
                    int idx = z * (res + 1) + x;
                    verts[idx] = new Vector3(wx, h, wz);
                    uvs[idx] = new Vector2(fx * 28f, fz * 28f);
                }
            }

            int t = 0;
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int i00 = z * (res + 1) + x;
                    int i10 = i00 + 1;
                    int i01 = i00 + (res + 1);
                    int i11 = i01 + 1;
                    tris[t++] = i00; tris[t++] = i01; tris[t++] = i10;
                    tris[t++] = i10; tris[t++] = i01; tris[t++] = i11;
                }
            }

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        public static float SampleParadiseHeight(float wx, float wz, Vector3 pondPos, Vector3 castlePos, Vector3 goddessPos)
        {
            // Ровная центральная зона для дороги, богини и замка
            float distCenter = new Vector2(wx * 0.85f, (wz - 18f) * 0.7f).magnitude;
            float rimMask = Mathf.SmoothStep(34f, 88f, distCenter);

            // Живописные холмы и горы по периметру (закрывают горизонт!)
            float n1 = Mathf.PerlinNoise(wx * 0.025f + 12.4f, wz * 0.025f + 7.1f);
            float n2 = Mathf.PerlinNoise(wx * 0.065f + 41.2f, wz * 0.065f + 19.8f);
            float hills = (n1 * 18f + n2 * 5.5f) * rimMask;

            // Мягкая волнистость луга вне дороги
            float pathClear = Mathf.SmoothStep(3.5f, 11f, Mathf.Abs(wx));
            float castleClear = Mathf.SmoothStep(16f, 26f, Vector2.Distance(new Vector2(wx, wz), new Vector2(castlePos.x, castlePos.z)));
            float goddessClear = Mathf.SmoothStep(7f, 14f, Vector2.Distance(new Vector2(wx, wz), new Vector2(goddessPos.x, goddessPos.z)));
            float meadow = (Mathf.PerlinNoise(wx * 0.05f + 5f, wz * 0.05f + 5f) - 0.5f) * 1.4f * pathClear * castleClear * goddessClear;

            // Чаша под пруд / лавовое озеро
            float dx = (wx - pondPos.x) / 7.2f;
            float dz = (wz - pondPos.z) / 5.2f;
            float pondD = Mathf.Sqrt(dx * dx + dz * dz);
            float pondDip = (1f - Mathf.SmoothStep(0.65f, 1.25f, pondD)) * -1.35f;

            return hills + meadow + pondDip;
        }

        static void AddTri(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
        {
            int idx = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(a.x + a.z, a.y));
            uvs.Add(new Vector2(b.x + b.z, b.y));
            uvs.Add(new Vector2(c.x + c.z, c.y));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
        }

        // ==================== ПРИМИТИВЫ И МЕШИ ====================

        public static GameObject CreateMeshObj(Transform parent, string name, Mesh mesh, Vector3 pos, Vector3 scale, Material mat, bool collider = false, bool local = false, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }
            return go;
        }

        public static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool collider = false, bool local = false, Quaternion? rot = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        // ==================== АРХИТЕКТУРА ПОДЗЕМЕЛЬЯ ====================

        /// <summary>Детализированная каменная колонна с плинтом, стволом, кольцами и капителью.</summary>
        public static Transform BuildDungeonPillar(Transform parent, string name, Vector3 pos, float height, float width, Material stoneMat, Material trimMat, bool collider = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            // Основной коллайдер на стволе
            Prim(PrimitiveType.Cube, root, "Shaft", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, width), stoneMat, collider, local: true);
            // Плинт и база
            Prim(PrimitiveType.Cube, root, "Plinth", new Vector3(0f, 0.22f, 0f), new Vector3(width * 1.38f, 0.44f, width * 1.38f), trimMat, false, local: true);
            Prim(PrimitiveType.Cube, root, "Base", new Vector3(0f, 0.55f, 0f), new Vector3(width * 1.2f, 0.24f, width * 1.2f), trimMat, false, local: true);
            // Капитель сверху
            Prim(PrimitiveType.Cube, root, "Neck", new Vector3(0f, height - 0.55f, 0f), new Vector3(width * 1.18f, 0.22f, width * 1.18f), trimMat, false, local: true);
            Prim(PrimitiveType.Cube, root, "Capital", new Vector3(0f, height - 0.22f, 0f), new Vector3(width * 1.42f, 0.44f, width * 1.42f), trimMat, false, local: true);

            // Вертикальные рёбра (каннелюры)
            float ribOff = width * 0.51f;
            for (int s = -1; s <= 1; s += 2)
            {
                Prim(PrimitiveType.Cube, root, "RibX", new Vector3(s * ribOff, height * 0.5f, 0f), new Vector3(0.12f, height - 1.1f, width * 0.35f), trimMat, false, local: true);
                Prim(PrimitiveType.Cube, root, "RibZ", new Vector3(0f, height * 0.5f, s * ribOff), new Vector3(width * 0.35f, height - 1.1f, 0.12f), trimMat, false, local: true);
            }
            return root;
        }

        /// <summary>Каменная арка проёма/коридора из сегментов клинчатой кладки.</summary>
        public static Transform BuildStoneArch(Transform parent, string name, Vector3 pos, float span, float height, float thickness, Material stoneMat, Material trimMat, float yRot = 0f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yRot, 0f);

            float half = span * 0.5f;
            float pillarH = Mathf.Max(1.5f, height - half * 0.7f);
            BuildDungeonPillar(root, "PillarL", root.TransformPoint(new Vector3(-half, 0f, 0f)), pillarH, thickness, stoneMat, trimMat, collider: false);
            BuildDungeonPillar(root, "PillarR", root.TransformPoint(new Vector3(half, 0f, 0f)), pillarH, thickness, stoneMat, trimMat, collider: false);

            int segs = 9;
            float radius = half;
            for (int i = 0; i < segs; i++)
            {
                float a = Mathf.Lerp(Mathf.PI, 0f, (i + 0.5f) / segs);
                Vector3 lp = new Vector3(Mathf.Cos(a) * radius, pillarH + Mathf.Sin(a) * radius * 0.72f, 0f);
                float deg = (a - Mathf.PI * 0.5f) * Mathf.Rad2Deg;
                bool keystone = i == segs / 2;
                Vector3 sz = new Vector3(span / segs * 1.25f, keystone ? thickness * 0.95f : thickness * 0.7f, keystone ? thickness * 1.15f : thickness);
                Prim(PrimitiveType.Cube, root, $"Voussoir_{i}", lp, sz, keystone ? trimMat : stoneMat, false, local: true, rot: Quaternion.Euler(0f, 0f, deg));
            }
            return root;
        }

        /// <summary>Кованый настенный/колонный факел с живым огнём, частицами искр и мерцающим светом.</summary>
        public static Light BuildTorch(Transform parent, string name, Vector3 pos, Vector3 wallNormal, Material ironMat, Material woodMat, Material flameMat, Material sparkMat, Color lightColor, float range = 11f, float intensity = 3.2f, bool castShadows = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            if (wallNormal.sqrMagnitude > 0.01f)
                root.rotation = Quaternion.LookRotation(wallNormal, Vector3.up);

            // Настенная пластина и кронштейн
            Prim(PrimitiveType.Cube, root, "Plate", new Vector3(0f, -0.15f, -0.05f), new Vector3(0.22f, 0.42f, 0.06f), ironMat, false, local: true);
            Prim(PrimitiveType.Cube, root, "Bracket", new Vector3(0f, -0.18f, 0.14f), new Vector3(0.08f, 0.08f, 0.34f), ironMat, false, local: true);
            // Рукоять факела под углом
            var handle = Prim(PrimitiveType.Cylinder, root, "Handle", new Vector3(0f, 0.02f, 0.28f), new Vector3(0.08f, 0.32f, 0.08f), woodMat, false, local: true, rot: Quaternion.Euler(18f, 0f, 0f));
            // Кованая чаша
            Prim(PrimitiveType.Cylinder, root, "Cup", new Vector3(0f, 0.28f, 0.36f), new Vector3(0.24f, 0.08f, 0.24f), ironMat, false, local: true);
            // Светящееся ядро пламени
            Prim(PrimitiveType.Sphere, root, "FlameCore", new Vector3(0f, 0.42f, 0.36f), new Vector3(0.16f, 0.28f, 0.16f), flameMat, false, local: true);

            // Система частиц огня и искр
            AddFireParticles(root, new Vector3(0f, 0.42f, 0.36f), sparkMat, scale: 0.32f, rate: 16f);

            // Свет
            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(0f, 0.6f, 0.45f);
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = lightColor;
            l.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            l.gameObject.AddComponent<TorchFlicker>();
            return l;
        }

        /// <summary>Напольная кованая жаровня с углями и огнём.</summary>
        public static Light BuildBrazier(Transform parent, string name, Vector3 pos, Material ironMat, Material stoneMat, Material emberMat, Material sparkMat, Color lightColor, float scale = 1f, bool castShadows = false)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.localScale = Vector3.one * scale;

            Prim(PrimitiveType.Cylinder, root, "Pedestal", new Vector3(0f, 0.35f, 0f), new Vector3(0.75f, 0.35f, 0.75f), stoneMat, true, local: true);
            Prim(PrimitiveType.Cylinder, root, "Bowl", new Vector3(0f, 0.85f, 0f), new Vector3(1.1f, 0.22f, 1.1f), ironMat, false, local: true);
            Prim(PrimitiveType.Sphere, root, "Coals", new Vector3(0f, 0.98f, 0f), new Vector3(0.85f, 0.25f, 0.85f), emberMat, false, local: true);

            AddFireParticles(root, new Vector3(0f, 1.08f, 0f), sparkMat, scale: 0.65f, rate: 22f);

            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            l.type = LightType.Point;
            l.range = 12f * scale;
            l.intensity = 4f;
            l.color = lightColor;
            l.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
            l.gameObject.AddComponent<TorchFlicker>();
            return l;
        }

        /// <summary>Деревянная бочка с железными обручами.</summary>
        public static void BuildBarrel(Transform parent, string name, Vector3 pos, Material woodMat, Material ironMat, float scale = 1f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.localScale = Vector3.one * scale;
            Prim(PrimitiveType.Cylinder, root, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 0.55f, 0.75f), woodMat, true, local: true);
            Prim(PrimitiveType.Cylinder, root, "HoopB", new Vector3(0f, 0.25f, 0f), new Vector3(0.79f, 0.04f, 0.79f), ironMat, false, local: true);
            Prim(PrimitiveType.Cylinder, root, "HoopT", new Vector3(0f, 0.85f, 0f), new Vector3(0.79f, 0.04f, 0.79f), ironMat, false, local: true);
        }

        /// <summary>Деревянный ящик с окантовкой.</summary>
        public static void BuildCrate(Transform parent, string name, Vector3 pos, float size, Material woodMat, Material trimMat, float yRot = 0f)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            root.rotation = Quaternion.Euler(0f, yRot, 0f);
            Prim(PrimitiveType.Cube, root, "Core", new Vector3(0f, size * 0.5f, 0f), Vector3.one * size, woodMat, true, local: true);
            Prim(PrimitiveType.Cube, root, "FrameT", new Vector3(0f, size * 0.94f, 0f), new Vector3(size * 1.06f, size * 0.12f, size * 1.06f), trimMat, false, local: true);
            Prim(PrimitiveType.Cube, root, "FrameB", new Vector3(0f, size * 0.06f, 0f), new Vector3(size * 1.06f, size * 0.12f, size * 1.06f), trimMat, false, local: true);
        }

        /// <summary>Свисающая с потолка цепь.</summary>
        public static void BuildHangingChain(Transform parent, string name, Vector3 topPos, float length, Material ironMat)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = topPos;
            int links = Mathf.Max(3, Mathf.RoundToInt(length / 0.35f));
            for (int i = 0; i < links; i++)
            {
                float y = -i * (length / links) - 0.15f;
                Prim(PrimitiveType.Cube, root, $"L_{i}", new Vector3(0f, y, 0f), new Vector3(0.08f, 0.28f, 0.16f), ironMat, false, local: true, rot: Quaternion.Euler(0f, (i % 2) * 90f, 0f));
            }
        }

        /// <summary>Группа скальных камней или сталагмитов.</summary>
        public static void BuildRockCluster(Transform parent, string name, Vector3 pos, Material rockMat, System.Random rng, int count = 3, float baseScale = 1.2f, bool collider = true)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;
            for (int i = 0; i < count; i++)
            {
                var mesh = GetOrCreateRock(rng.Next(0, 4));
                float s = baseScale * (i == 0 ? 1f : (float)(0.45 + rng.NextDouble() * 0.5));
                Vector3 lp = i == 0 ? Vector3.up * (s * 0.3f) : new Vector3((float)(rng.NextDouble() * 2 - 1) * baseScale * 0.9f, s * 0.22f, (float)(rng.NextDouble() * 2 - 1) * baseScale * 0.9f);
                Vector3 sc = new Vector3(s * (float)(0.8 + rng.NextDouble() * 0.5), s * (float)(0.6 + rng.NextDouble() * 0.5), s * (float)(0.8 + rng.NextDouble() * 0.5));
                CreateMeshObj(root, $"Rock_{i}", mesh, lp, sc, rockMat, collider && i == 0, local: true, rot: Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f));
            }
        }

        /// <summary>Сталактит, свисающий с потолка пещеры.</summary>
        public static void BuildStalactite(Transform parent, Vector3 ceilingPos, float length, float radius, Material rockMat)
        {
            var cone = GetOrCreateCone("Cone8", 8);
            CreateMeshObj(parent, "Stalactite", cone, ceilingPos, new Vector3(radius * 2f, length, radius * 2f), rockMat, false, local: false, rot: Quaternion.Euler(180f, 0f, 0f));
        }

        /// <summary>Костёр с камнями, поленьями и живым огнём.</summary>
        public static Light BuildCampfire(Transform parent, string name, Vector3 pos, Material rockMat, Material woodMat, Material emberMat, Material sparkMat, Color lightColor)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.position = pos;

            var rockMesh = GetOrCreateRock(0);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                CreateMeshObj(root, $"Stone_{i}", rockMesh, new Vector3(Mathf.Cos(a) * 0.85f, 0.14f, Mathf.Sin(a) * 0.85f), new Vector3(0.38f, 0.26f, 0.38f), rockMat, false, local: true, rot: Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f));
            }
            for (int i = 0; i < 4; i++)
            {
                float a = i * 45f;
                Prim(PrimitiveType.Cylinder, root, $"Log_{i}", new Vector3(0f, 0.18f, 0f), new Vector3(0.16f, 0.55f, 0.16f), woodMat, false, local: true, rot: Quaternion.Euler(68f, a, 0f));
            }
            Prim(PrimitiveType.Sphere, root, "Embers", new Vector3(0f, 0.15f, 0f), new Vector3(0.75f, 0.25f, 0.75f), emberMat, false, local: true);
            AddFireParticles(root, new Vector3(0f, 0.35f, 0f), sparkMat, scale: 0.75f, rate: 25f);

            var l = new GameObject("Light").AddComponent<Light>();
            l.transform.SetParent(root, false);
            l.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            l.type = LightType.Point;
            l.range = 14f;
            l.intensity = 4.2f;
            l.color = lightColor;
            l.shadows = LightShadows.Soft;
            l.gameObject.AddComponent<TorchFlicker>();
            return l;
        }

        // ==================== ПРИРОДА (РАЙ / АД) ====================

        /// <summary>Реалистичное ветвистое дерево с корнями и многокластерной кроной.</summary>
        public static Transform BuildRealisticTree(Transform parent, string name, Vector3 pos, float scale, Material barkMat, Material leafMat, System.Random rng, bool dead = false)
        {
            var tree = new GameObject(name).transform;
            tree.SetParent(parent, false);
            tree.position = pos;
            tree.rotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360), 0f);
            tree.localScale = Vector3.one * scale;

            // Основной ствол с лёгким наклоном
            float trunkH = dead ? 3.8f : 3.4f;
            float trunkR = dead ? 0.34f : 0.44f;
            Prim(PrimitiveType.Cylinder, tree, "Trunk", new Vector3(0f, trunkH * 0.5f, 0f), new Vector3(trunkR, trunkH * 0.5f, trunkR), barkMat, true, local: true);

            // Корни у основания
            for (int r = 0; r < 3; r++)
            {
                float a = r * 120f + (float)(rng.NextDouble() * 30);
                var rad = a * Mathf.Deg2Rad;
                Prim(PrimitiveType.Cylinder, tree, $"Root_{r}", new Vector3(Mathf.Cos(rad) * 0.32f, 0.25f, Mathf.Sin(rad) * 0.32f), new Vector3(0.22f, 0.45f, 0.22f), barkMat, false, local: true, rot: Quaternion.Euler(0f, -a, 45f));
            }

            // Ветви
            int branches = dead ? 5 : 4;
            for (int b = 0; b < branches; b++)
            {
                float yaw = b * (360f / branches) + (float)(rng.NextDouble() * 35 - 17.5);
                float pitch = (float)(32 + rng.NextDouble() * 24);
                float bh = (float)(1.3 + rng.NextDouble() * 0.9);
                float yStart = trunkH * (0.62f + (b % 2) * 0.18f);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(pitch, 0f, 0f);
                Vector3 dir = rot * Vector3.up;
                Vector3 mid = new Vector3(0f, yStart, 0f) + dir * (bh * 0.5f);
                Prim(PrimitiveType.Cylinder, tree, $"Branch_{b}", mid, new Vector3(trunkR * 0.42f, bh * 0.5f, trunkR * 0.42f), barkMat, false, local: true, rot: rot);

                if (!dead)
                {
                    Vector3 tip = new Vector3(0f, yStart, 0f) + dir * bh;
                    float cs = (float)(1.9 + rng.NextDouble() * 0.9);
                    Prim(PrimitiveType.Sphere, tree, $"LeafCluster_{b}", tip + Vector3.up * 0.25f, new Vector3(cs * 1.15f, cs * 0.78f, cs * 1.15f), leafMat, false, local: true);
                }
                else
                {
                    // Сухая боковая веточка для мёртвого дерева
                    Vector3 tip = new Vector3(0f, yStart, 0f) + dir * (bh * 0.85f);
                    Quaternion subRot = rot * Quaternion.Euler(35f, 40f, 0f);
                    Prim(PrimitiveType.Cylinder, tree, $"Twig_{b}", tip + subRot * Vector3.up * 0.45f, new Vector3(0.08f, 0.55f, 0.08f), barkMat, false, local: true, rot: subRot);
                }
            }

            if (!dead)
            {
                // Верхняя центральная крона
                Prim(PrimitiveType.Sphere, tree, "CrownTop", new Vector3(0f, trunkH + 1.45f, 0f), new Vector3(2.8f, 2.0f, 2.8f), leafMat, false, local: true);
            }

            return tree;
        }

        /// <summary>Куст из нескольких перекрывающихся сфер листвы.</summary>
        public static void BuildBush(Transform parent, Vector3 pos, float scale, Material leafMat, System.Random rng)
        {
            var b = new GameObject("Bush").transform;
            b.SetParent(parent, false);
            b.position = pos;
            for (int i = 0; i < 3; i++)
            {
                Vector3 off = i == 0 ? Vector3.up * 0.4f * scale : new Vector3((float)(rng.NextDouble() * 1.2 - 0.6) * scale, 0.3f * scale, (float)(rng.NextDouble() * 1.2 - 0.6) * scale);
                float s = scale * (i == 0 ? 1.1f : 0.75f);
                Prim(PrimitiveType.Sphere, b, $"L_{i}", off, new Vector3(s * 1.2f, s * 0.85f, s * 1.2f), leafMat, false, local: true);
            }
        }

        // ==================== СИСТЕМЫ ЧАСТИЦ ====================

        public static void AddFireParticles(Transform parent, Vector3 localPos, Material particleMat, float scale = 0.4f, float rate = 18f)
        {
            var go = new GameObject("FireFX");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f * scale, 1.8f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f * scale, 0.42f * scale);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.25f, 0.9f), new Color(1f, 0.28f, 0.05f, 0.85f));
            main.gravityModifier = -0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;

            var em = ps.emission;
            em.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.14f * scale;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.3f, 0.04f), 0.55f), new GradientColorKey(new Color(0.25f, 0.08f, 0.05f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = particleMat;
        }

        public static void BuildAmbientParticles(Transform parent, string name, Vector3 center, Vector3 boxSize, Material mat, Color c1, Color c2, float rate, float minSize, float maxSize, float gravity, float speed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(c1, c2);
            main.gravityModifier = gravity;
            main.maxParticles = 350;
            main.prewarm = true;

            var em = ps.emission;
            em.rateOverTime = rate;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0.85f, 0.8f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = g;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
        }
    }
}
