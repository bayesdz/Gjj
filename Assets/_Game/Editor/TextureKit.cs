using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FalseParadise.EditorTools
{
    /// <summary>
    /// Процедурные бесшовные текстуры для уровней (альбедо + нормаль), без внешних ассетов:
    /// скала, кладка из блоков, плиты пола, доски и фрески (несшитые, 2:1).
    /// Альбедо — светлота с лёгким оттенком (умножается на _BaseColor материала).
    /// Файлы пишутся в Assets/_Game/Art/Common/Textures и переиспользуются всеми сборщиками.
    /// </summary>
    internal static class TextureKit
    {
        const string Dir = "Assets/_Game/Art/Common/Textures";

        internal struct Pair
        {
            public Texture2D albedo, normal;
        }

        internal enum MuralTheme { GoblinLife, ElvenHistory }

        static readonly Dictionary<string, Pair> cache = new Dictionary<string, Pair>();

        /// <summary>Сбросить кэш: в начале каждой сборки сцены текстуры генерируются заново.</summary>
        internal static void BeginBuild() => cache.Clear();

        // ---------------------------------------------------------------- шум

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(s * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;

        /// <summary>Периодический value-noise в координатах 0..1, решётка cells×cells.</summary>
        static float VNoise(float u, float v, int cells, int seed)
        {
            float fx = u * cells, fy = v * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Mathf.Lerp(Hash(Wrap(x0, cells), Wrap(y0, cells), seed), Hash(Wrap(x0 + 1, cells), Wrap(y0, cells), seed), tx);
            float b = Mathf.Lerp(Hash(Wrap(x0, cells), Wrap(y0 + 1, cells), seed), Hash(Wrap(x0 + 1, cells), Wrap(y0 + 1, cells), seed), tx);
            return Mathf.Lerp(a, b, ty);
        }

        static float Fbm(float u, float v, int baseCells, int octaves, float gain, int seed)
        {
            float sum = 0f, amp = 1f, tot = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * VNoise(u, v, cells, seed + o * 31);
                tot += amp;
                amp *= gain;
                cells *= 2;
            }
            return sum / tot;
        }

        /// <summary>Периодический Ворони: F1, F2 и id ближайшей клетки.</summary>
        static void Worley(float u, float v, int cells, int seed, out float f1, out float f2, out int id)
        {
            float fx = u * cells, fy = v * cells;
            int cx = Mathf.FloorToInt(fx), cy = Mathf.FloorToInt(fy);
            f1 = f2 = 9f;
            id = 0;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                float px = gx + 0.05f + 0.9f * Hash(wx, wy, seed);
                float py = gy + 0.05f + 0.9f * Hash(wx, wy, seed + 1);
                float d = Mathf.Sqrt((px - fx) * (px - fx) + (py - fy) * (py - fy));
                if (d < f1) { f2 = f1; f1 = d; id = wx + wy * cells; }
                else if (d < f2) f2 = d;
            }
        }

        static float SStep(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

        // ---------------------------------------------------------------- скала

        internal static Pair Rock()
        {
            if (cache.TryGetValue("rock", out var p)) return p;
            const int N = 1024;
            var h = new float[N * N];
            var col = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                // Тёплая/холодная пятнистость, крупные плиты, мелкое зерно, трещины
                float big = Fbm(u, v, 3, 3, 0.5f, 10);
                float grain = Fbm(u, v, 16, 5, 0.6f, 20);
                float ridge = 1f - Mathf.Abs(Fbm(u, v, 5, 3, 0.5f, 30) * 2f - 1f);
                float crackMask = SStep(0.6f, 0.75f, VNoise(u, v, 4, 40));
                float crack = SStep(0.965f, 0.995f, ridge) * crackMask * 0.6f;
                float strata = 0.5f + 0.5f * Mathf.Sin((v + big * 0.25f) * Mathf.PI * 2f * 9f);
                float hv = grain * 0.6f + big * 0.3f + strata * 0.1f - crack * 0.35f;
                h[y * N + x] = hv;
                float lum = Mathf.Lerp(0.72f, 1f, SStep(0.25f, 0.75f, hv)) * (1f - crack * 0.5f);
                lum *= 0.92f + 0.16f * Fbm(u, v, 64, 2, 0.5f, 50); // мелкие вкрапления
                var tint = Color.Lerp(new Color(0.94f, 0.97f, 1.02f), new Color(1.04f, 0.98f, 0.9f), big);
                col[y * N + x] = tint * lum;
            }
            p = Save("T_Rock", N, N, col, h, 5f, true);
            cache["rock"] = p;
            return p;
        }

        // ---------------------------------------------------------------- кладка

        /// <summary>Кладка: 8 рядов блоков разной длины со швами, сколами, фасками и пятнами.</summary>
        internal static Pair Masonry()
        {
            if (cache.TryGetValue("masonry", out var p)) return p;
            const int N = 1024;
            const int rows = 8;
            float rowH = 1f / rows;
            // Длины блоков в ряду (в долях ширины тайла), сумма = 1
            var edges = new List<float>[rows];
            var rnd = new System.Random(7);
            for (int r = 0; r < rows; r++)
            {
                edges[r] = new List<float> { 0f };
                float acc = 0f;
                while (acc < 1f)
                {
                    float w = rowH * (1.3f + (float)rnd.NextDouble() * 1.6f);
                    if (1f - (acc + w) < rowH * 1.1f) w = 1f - acc;
                    acc += w;
                    edges[r].Add(Mathf.Min(acc, 1f));
                }
            }
            var h = new float[N * N];
            var col = new Color[N * N];
            float mortar = rowH * 0.035f;
            float bevel = rowH * 0.12f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                int r = Mathf.Min(rows - 1, (int)(v / rowH));
                float ru = Mathf.Repeat(u + r * 0.37f, 1f); // сдвиг рядов
                var e = edges[r];
                int b = 1;
                while (b < e.Count - 1 && ru > e[b]) b++;
                float dx = Mathf.Min(ru - e[b - 1], e[b] - ru);
                float dy = Mathf.Min(v - r * rowH, (r + 1) * rowH - v);
                // Неровный шов: край блока гуляет по шуму
                float wobble = (Fbm(u, v, 24, 2, 0.5f, 60) - 0.5f) * mortar * 1.4f;
                float edge = Mathf.Min(dx, dy) + wobble;
                int blockId = r * 97 + b;
                float bh = Hash(blockId, 3, 70);
                float surface = Fbm(u, v, 12, 5, 0.55f, 80 + blockId % 5);
                // Сколы на углах и кромках
                float chip = SStep(0.6f, 0.8f, VNoise(u, v, 40, 90)) * (1f - SStep(0f, bevel * 2.5f, edge));
                float hv;
                Color c;
                if (edge < mortar)
                {
                    hv = 0.08f + 0.06f * Fbm(u, v, 64, 2, 0.5f, 100);
                    c = new Color(0.66f, 0.62f, 0.57f) * (0.85f + 0.3f * surface);
                }
                else
                {
                    float bev = SStep(mortar, mortar + bevel, edge);
                    hv = 0.35f + 0.45f * bev + 0.2f * surface - chip * 0.3f;
                    float lum = Mathf.Lerp(0.72f, 1f, bh) * (0.82f + 0.3f * surface) * (1f - chip * 0.25f);
                    // Грязь к нижней кромке ряда, светлые потёртые верхние кромки
                    float rowT = (v - r * rowH) / rowH;
                    lum *= Mathf.Lerp(0.88f, 1.03f, SStep(0f, 0.35f, rowT));
                    var tint = Color.Lerp(new Color(1.02f, 0.98f, 0.92f), new Color(0.94f, 0.97f, 1.02f), Hash(blockId, 5, 71));
                    c = tint * lum;
                    // Пятна сырости
                    float stain = SStep(0.62f, 0.8f, Fbm(u, v, 4, 3, 0.5f, 110));
                    c *= 1f - stain * 0.22f;
                }
                h[y * N + x] = hv;
                col[y * N + x] = c;
            }
            p = Save("T_Masonry", N, N, col, h, 7f, true);
            cache["masonry"] = p;
            return p;
        }

        // ---------------------------------------------------------------- плиты пола

        internal static Pair Flagstone()
        {
            if (cache.TryGetValue("flag", out var p)) return p;
            const int N = 1024;
            const int cells = 6;
            var h = new float[N * N];
            var col = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                float wu = u + (Fbm(u, v, 8, 2, 0.5f, 120) - 0.5f) * 0.02f;
                float wv = v + (Fbm(u, v, 8, 2, 0.5f, 130) - 0.5f) * 0.02f;
                Worley(wu, wv, cells, 140, out float f1, out float f2, out int id);
                float gap = f2 - f1;
                float surface = Fbm(u, v, 10, 5, 0.55f, 150);
                float ridge = 1f - Mathf.Abs(Fbm(u, v, 6, 3, 0.5f, 160) * 2f - 1f);
                float crack = SStep(0.95f, 0.99f, ridge) * SStep(0.5f, 0.7f, Hash(id, 1, 161));
                float tilt = (Hash(id, 2, 162) - 0.5f) * 0.1f; // плиты чуть просели по-разному
                float hv, lum;
                Color c;
                if (gap < 0.028f)
                {
                    hv = 0.12f;
                    lum = 0.58f + 0.2f * surface;
                    c = new Color(0.9f, 0.85f, 0.78f) * lum; // земля и пыль в швах
                }
                else
                {
                    float bev = SStep(0.028f, 0.09f, gap);
                    hv = 0.35f + 0.4f * bev + 0.2f * surface + tilt - crack * 0.25f;
                    lum = Mathf.Lerp(0.7f, 1f, Hash(id, 3, 163)) * (0.8f + 0.3f * surface) * (1f - crack * 0.45f);
                    // Края плит потёрты и светлее, центр темнее от грязи
                    lum *= Mathf.Lerp(0.95f, 1.03f, SStep(0.03f, 0.3f, gap));
                    lum *= 0.85f + 0.2f * Fbm(u, v, 3, 3, 0.5f, 165); // пыль и грязь пятнами поверх плит
                    var tint = Color.Lerp(new Color(1.03f, 0.98f, 0.92f), new Color(0.95f, 0.97f, 1.01f), Hash(id, 4, 164));
                    c = tint * lum;
                }
                h[y * N + x] = hv;
                col[y * N + x] = c;
            }
            p = Save("T_Flagstone", N, N, col, h, 3.5f, true);
            cache["flag"] = p;
            return p;
        }

        // ---------------------------------------------------------------- доски

        internal static Pair Planks()
        {
            if (cache.TryGetValue("planks", out var p)) return p;
            const int N = 512;
            const int boards = 5;
            var h = new float[N * N];
            var col = new Color[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float u = (float)x / N, v = (float)y / N;
                int b = Mathf.Min(boards - 1, (int)(v * boards));
                float bv = v * boards - b;
                float grain = VNoise(u + Hash(b, 0, 170), v * 1f, 3, 171 + b) * 0.5f + Fbm(u * 1f, v, 4, 4, 0.6f, 172) * 0.5f;
                float lines = 0.5f + 0.5f * Mathf.Sin((v * boards * 18f + grain * 6f + Hash(b, 1, 173) * 10f) * Mathf.PI);
                float gapT = Mathf.Min(bv, 1f - bv);
                float hv = SStep(0f, 0.04f, gapT) * (0.6f + 0.25f * lines + 0.15f * grain);
                float lum = Mathf.Lerp(0.7f, 1f, Hash(b, 2, 174)) * (0.78f + 0.22f * lines) * (0.85f + 0.3f * grain);
                // Сучки
                float kx = Hash(b, 5, 175), ky = (b + 0.5f) / boards;
                float kd = Vector2.Distance(new Vector2(Mathf.Repeat(u - kx + 0.5f, 1f) - 0.5f, (v - ky) * 3f), Vector2.zero);
                lum *= 1f - 0.4f * (1f - SStep(0.01f, 0.03f, kd));
                if (gapT < 0.025f) lum *= 0.3f;
                h[y * N + x] = hv;
                col[y * N + x] = new Color(1.02f, 0.99f, 0.95f) * lum;
            }
            p = Save("T_Planks", N, N, col, h, 3f, true);
            cache["planks"] = p;
            return p;
        }

        // ---------------------------------------------------------------- фрески

        /// <summary>Роспись по штукатурке 1024×512: охра и уголь, стёртая краска, трещины, отслоения.</summary>
        internal static Pair Mural(MuralTheme theme, int seed)
        {
            string key = $"mural_{theme}_{seed}";
            if (cache.TryGetValue(key, out var p)) return p;
            const int W = 1024, H = 512;
            var h = new float[W * H];
            var col = new Color[W * H];
            // Штукатурка
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float stain = Fbm(u * 2f, v, 3, 4, 0.55f, 200 + seed);
                float grain = Fbm(u * 2f, v, 32, 3, 0.5f, 210);
                float lum = 0.78f + 0.22f * stain;
                lum *= 0.94f + 0.08f * grain;
                // Края темнее (копоть, сырость)
                float border = Mathf.Min(Mathf.Min(u, 1f - u) * 2f, Mathf.Min(v, 1f - v));
                lum *= Mathf.Lerp(0.62f, 1f, SStep(0f, 0.12f, border));
                col[y * W + x] = new Color(0.97f, 0.86f, 0.68f) * lum;
                h[y * W + x] = 0.5f + 0.1f * grain;
            }

            var painter = new Painter(col, W, H, seed);
            var ochre = new Color(0.62f, 0.27f, 0.12f);
            var coal = new Color(0.12f, 0.1f, 0.09f);
            var umber = new Color(0.38f, 0.22f, 0.13f);
            var r = new System.Random(seed);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);

            // Ряды знаков по верху и низу
            for (int band = 0; band < 2; band++)
            {
                float y0 = band == 0 ? 0.9f : 0.08f;
                painter.Line(0.04f, y0 + 0.03f, 0.96f, y0 + 0.03f, 2.5f, umber);
                painter.Line(0.04f, y0 - 0.03f, 0.96f, y0 - 0.03f, 2.5f, umber);
                for (float gx = 0.06f; gx < 0.95f; gx += R(0.025f, 0.04f))
                {
                    float gy = y0 - 0.015f;
                    int k = r.Next(4);
                    if (k == 0) painter.Line(gx, gy, gx, gy + 0.03f, 2f, umber);
                    else if (k == 1) { painter.Line(gx - 0.006f, gy, gx + 0.006f, gy + 0.03f, 2f, umber); painter.Line(gx + 0.006f, gy, gx - 0.006f, gy + 0.03f, 2f, umber); }
                    else if (k == 2) painter.Circle(gx, gy + 0.015f, 0.008f, umber, 1.8f);
                    else { painter.Line(gx - 0.007f, gy, gx, gy + 0.03f, 2f, umber); painter.Line(gx, gy + 0.03f, gx + 0.007f, gy, 2f, umber); }
                }
            }

            if (theme == MuralTheme.GoblinLife)
            {
                // Слева охота: гоблины с копьями гонят зверя
                Beast(painter, 0.2f, 0.32f, 0.11f, coal, true);
                for (int i = 0; i < 3; i++) Goblin(painter, 0.31f + i * 0.055f, 0.24f, 0.18f, ochre, coal, true, r);
                // Центр: семья у костра (большие и маленькие фигуры, держатся за руки)
                float fx = 0.52f;
                painter.Line(fx - 0.03f, 0.25f, fx, 0.36f, 4f, ochre);
                painter.Line(fx + 0.03f, 0.25f, fx, 0.36f, 4f, ochre);
                painter.Line(fx - 0.015f, 0.25f, fx + 0.01f, 0.31f, 3f, new Color(0.85f, 0.5f, 0.18f));
                painter.Line(fx - 0.04f, 0.24f, fx + 0.04f, 0.24f, 3.5f, coal);
                float[] fam = { 0.43f, 0.465f, 0.585f, 0.62f, 0.65f };
                float[] size = { 0.2f, 0.12f, 0.19f, 0.11f, 0.09f };
                for (int i = 0; i < fam.Length; i++) Goblin(painter, fam[i], 0.24f, size[i], ochre, coal, false, r);
                painter.Line(fam[0] + 0.01f, 0.33f, fam[1] - 0.01f, 0.31f, 3f, ochre);
                painter.Line(fam[2] + 0.01f, 0.33f, fam[3] - 0.01f, 0.31f, 3f, ochre);
                painter.Line(fam[3] + 0.01f, 0.31f, fam[4] - 0.01f, 0.3f, 3f, ochre);
                // Справа горы, солнце, вход в пещеру
                for (int i = 0; i < 4; i++)
                {
                    float mx = 0.73f + i * 0.055f;
                    painter.Line(mx - 0.04f, 0.3f, mx, 0.52f + R(-0.05f, 0.05f), 3.5f, umber);
                    painter.Line(mx, 0.52f, mx + 0.04f, 0.3f, 3.5f, umber);
                }
                Sun(painter, 0.88f, 0.7f, 0.04f, ochre);
                painter.Arc(0.8f, 0.24f, 0.05f, 0f, Mathf.PI, coal, 4f);
                // Над всем — большая фигура с сиянием (богиня?) перечёркнута
                Goblin(painter, 0.25f, 0.55f, 0.16f, umber, coal, false, r);
                Sun(painter, 0.6f, 0.68f, 0.035f, umber);
            }
            else
            {
                // Древо в центре
                float tx = 0.5f;
                painter.Line(tx, 0.18f, tx, 0.62f, 6f, umber);
                for (int i = 0; i < 6; i++)
                for (int s = -1; s <= 1; s += 2)
                {
                    float y = 0.3f + i * 0.055f;
                    painter.Line(tx, y, tx + s * (0.13f - i * 0.015f), y + 0.08f, 3.5f, umber);
                }
                painter.Circle(tx, 0.68f, 0.035f, ochre, 4f); // Око над древом
                painter.Circle(tx, 0.68f, 0.012f, coal, 6f);
                // Эльфы с луками слева
                for (int i = 0; i < 4; i++) Elf(painter, 0.1f + i * 0.07f, 0.22f, 0.2f, coal, ochre);
                // Справа: Страж с нимбом и врата
                Goblin(painter, 0.74f, 0.22f, 0.36f, coal, coal, false, r);
                painter.Arc(0.74f, 0.55f, 0.07f, 0.2f, Mathf.PI - 0.2f, ochre, 4f);
                for (int i = 0; i < 7; i++)
                {
                    float a = 0.4f + i * 0.33f;
                    painter.Line(0.74f, 0.53f, 0.74f + Mathf.Cos(a) * 0.05f, 0.53f + Mathf.Sin(a) * 0.1f, 2.5f, ochre);
                }
                painter.Line(0.86f, 0.18f, 0.86f, 0.5f, 5f, umber);
                painter.Line(0.95f, 0.18f, 0.95f, 0.5f, 5f, umber);
                painter.Arc(0.905f, 0.5f, 0.045f, 0f, Mathf.PI, umber, 5f);
                Sun(painter, 0.2f, 0.65f, 0.035f, ochre);
            }

            // Износ: краска облезла пятнами, трещины поверх всего
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = (float)x / W, v = (float)y / H;
                float ridge = 1f - Mathf.Abs(Fbm(u * 2f, v, 5, 3, 0.5f, 220 + seed) * 2f - 1f);
                float crack = SStep(0.955f, 0.99f, ridge);
                float flake = SStep(0.66f, 0.72f, Fbm(u * 2f, v, 6, 4, 0.55f, 230 + seed));
                var c = col[y * W + x];
                if (flake > 0f)
                {
                    // Отслоившаяся штукатурка: под ней серый камень
                    var stone = new Color(0.55f, 0.53f, 0.5f) * (0.8f + 0.3f * Fbm(u * 2f, v, 40, 2, 0.5f, 240));
                    c = Color.Lerp(c, stone, flake);
                }
                c *= 1f - crack * 0.6f;
                col[y * W + x] = c;
                h[y * W + x] -= flake * 0.25f + crack * 0.15f;
            }
            p = Save($"T_Mural_{theme}_{seed}", W, H, col, h, 3f, false);
            cache[key] = p;
            return p;
        }

        static void Goblin(Painter p, float x, float y, float s, Color body, Color dark, bool spear, System.Random r)
        {
            float head = y + s * 0.82f;
            p.Line(x, y + s * 0.42f, x, y + s * 0.72f, 5f * s / 0.18f * 0.8f, body);     // тело
            p.Circle(x, head, s * 0.1f, body, 0f);                                          // голова
            p.Line(x - s * 0.08f, head + s * 0.04f, x - s * 0.2f, head + s * 0.14f, 2.5f, body); // уши
            p.Line(x + s * 0.08f, head + s * 0.04f, x + s * 0.2f, head + s * 0.14f, 2.5f, body);
            float step = (float)r.NextDouble() * 0.06f * s / 0.18f;
            p.Line(x, y + s * 0.42f, x - s * 0.12f - step, y, 3f, body);                   // ноги
            p.Line(x, y + s * 0.42f, x + s * 0.12f + step, y, 3f, body);
            p.Line(x, y + s * 0.66f, x - s * 0.18f, y + s * 0.45f, 2.6f, body);            // руки
            p.Line(x, y + s * 0.66f, x + s * 0.2f, y + s * 0.6f, 2.6f, body);
            if (spear) p.Line(x - s * 0.3f, y + s * 0.5f, x + s * 0.12f, y + s * 0.7f, 2.2f, dark);
        }

        static void Elf(Painter p, float x, float y, float s, Color body, Color accent)
        {
            float head = y + s * 0.88f;
            p.Line(x, y + s * 0.45f, x, y + s * 0.78f, 3.5f, body);
            p.Circle(x, head, s * 0.07f, body, 0f);
            p.Line(x + s * 0.05f, head + s * 0.02f, x + s * 0.13f, head + s * 0.1f, 2f, body);
            p.Line(x, y + s * 0.45f, x - s * 0.1f, y, 2.6f, body);
            p.Line(x, y + s * 0.45f, x + s * 0.08f, y, 2.6f, body);
            p.Line(x, y + s * 0.72f, x + s * 0.2f, y + s * 0.72f, 2.4f, body);
            p.Arc(x + s * 0.2f, y + s * 0.72f, s * 0.18f, -1.2f, 1.2f, accent, 2.5f);
            p.Line(x + s * 0.27f, y + s * 0.55f, x + s * 0.27f, y + s * 0.89f, 1.2f, body);
        }

        static void Beast(Painter p, float x, float y, float s, Color c, bool horns)
        {
            p.Ellipse(x, y, s * 0.9f, s * 0.38f, c);
            for (int i = 0; i < 4; i++)
            {
                float lx = x - s * 0.6f + i * s * 0.4f;
                p.Line(lx, y - s * 0.2f, lx + (i % 2 == 0 ? -1f : 1f) * s * 0.08f, y - s * 0.75f, 3f, c);
            }
            p.Line(x + s * 0.8f, y + s * 0.1f, x + s * 1.1f, y + s * 0.35f, 4f, c);
            p.Circle(x + s * 1.15f, y + s * 0.38f, s * 0.12f, c, 0f);
            if (horns) p.Arc(x + s * 1.15f, y + s * 0.55f, s * 0.15f, 0.3f, 2.8f, c, 2.5f);
            p.Line(x - s * 0.85f, y + s * 0.05f, x - s * 1.1f, y + s * 0.3f, 2f, c);
        }

        static void Sun(Painter p, float x, float y, float r, Color c)
        {
            p.Circle(x, y, r, c, 0f);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                p.Line(x + Mathf.Cos(a) * r * 1.4f, y + Mathf.Sin(a) * r * 2.8f * 0.5f,
                    x + Mathf.Cos(a) * r * 2.2f, y + Mathf.Sin(a) * r * 4.4f * 0.5f, 2.5f, c);
            }
        }

        /// <summary>Рисование мазками с мягким краем и неровным нажимом. Координаты 0..1 (x по ширине, y снизу вверх).</summary>
        class Painter
        {
            readonly Color[] col;
            readonly int w, h, seed;
            public Painter(Color[] col, int w, int h, int seed) { this.col = col; this.w = w; this.h = h; this.seed = seed; }

            void Dab(int px, int py, float coverage, Color c)
            {
                if (px < 0 || py < 0 || px >= w || py >= h || coverage <= 0f) return;
                float u = (float)px / w, v = (float)py / h;
                float wear = 0.55f + 0.45f * Fbm(u * 2f, v, 20, 3, 0.6f, 300 + seed); // неравномерный слой краски
                int i = py * w + px;
                col[i] = Color.Lerp(col[i], c * (col[i].grayscale * 0.35f + 0.75f), Mathf.Clamp01(coverage * wear * 0.92f));
            }

            public void Line(float x0, float y0, float x1, float y1, float width, Color c)
            {
                float ax = x0 * w, ay = y0 * h, bx = x1 * w, by = y1 * h;
                float hw = width * 0.5f + 1f;
                int minX = Mathf.FloorToInt(Mathf.Min(ax, bx) - hw), maxX = Mathf.CeilToInt(Mathf.Max(ax, bx) + hw);
                int minY = Mathf.FloorToInt(Mathf.Min(ay, by) - hw), maxY = Mathf.CeilToInt(Mathf.Max(ay, by) + hw);
                var a = new Vector2(ax, ay);
                var ab = new Vector2(bx - ax, by - ay);
                float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
                for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    var pt = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / len2);
                    float d = Vector2.Distance(pt, a + ab * t);
                    // Мазок сужается к концам
                    float taper = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI);
                    float cov = 1f - SStep(width * 0.5f * taper - 0.8f, width * 0.5f * taper + 0.8f, d);
                    Dab(x, y, cov, c);
                }
            }

            /// <summary>Круг: width = 0 — залитый, иначе контур.</summary>
            public void Circle(float cx, float cy, float r, Color c, float width)
            {
                float px = cx * w, py = cy * h, pr = r * h;
                int ext = Mathf.CeilToInt(pr + width + 2f);
                for (int y = (int)py - ext; y <= (int)py + ext; y++)
                for (int x = (int)px - ext; x <= (int)px + ext; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(px, py));
                    float cov = width <= 0f ? 1f - SStep(pr - 0.8f, pr + 0.8f, d) : 1f - SStep(width * 0.5f - 0.8f, width * 0.5f + 0.8f, Mathf.Abs(d - pr));
                    Dab(x, y, cov, c);
                }
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color c)
            {
                float px = cx * w, py = cy * h, prx = rx * h, pry = ry * h;
                for (int y = (int)(py - pry) - 2; y <= (int)(py + pry) + 2; y++)
                for (int x = (int)(px - prx) - 2; x <= (int)(px + prx) + 2; x++)
                {
                    float dx = (x - px) / prx, dy = (y - py) / pry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Dab(x, y, 1f - SStep(0.96f, 1.04f, d), c);
                }
            }

            public void Arc(float cx, float cy, float r, float a0, float a1, Color c, float width)
            {
                int seg = 16;
                for (int i = 0; i < seg; i++)
                {
                    float t0 = Mathf.Lerp(a0, a1, (float)i / seg), t1 = Mathf.Lerp(a0, a1, (float)(i + 1) / seg);
                    Line(cx + Mathf.Cos(t0) * r * h / w, cy + Mathf.Sin(t0) * r, cx + Mathf.Cos(t1) * r * h / w, cy + Mathf.Sin(t1) * r, width, c);
                }
            }
        }

        // ---------------------------------------------------------------- сохранение

        static Pair Save(string name, int w, int hgt, Color[] col, float[] height, float strength, bool tile)
        {
            EnsureFolder(Dir);
            string albedoPath = $"{Dir}/{name}.png";
            string normalPath = $"{Dir}/{name}_N.png";

            var a = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            var px = new Color32[w * hgt];
            for (int i = 0; i < px.Length; i++)
            {
                var c = col[i];
                px[i] = new Color32((byte)Mathf.Clamp(c.r * 255f, 0f, 255f), (byte)Mathf.Clamp(c.g * 255f, 0f, 255f), (byte)Mathf.Clamp(c.b * 255f, 0f, 255f), 255);
            }
            a.SetPixels32(px);
            System.IO.File.WriteAllBytes(albedoPath, a.EncodeToPNG());
            Object.DestroyImmediate(a);

            var n = new Texture2D(w, hgt, TextureFormat.RGB24, false);
            var np = new Color32[w * hgt];
            float H(int x, int y)
            {
                if (tile) { x = Wrap(x, w); y = Wrap(y, hgt); }
                else { x = Mathf.Clamp(x, 0, w - 1); y = Mathf.Clamp(y, 0, hgt - 1); }
                return height[y * w + x];
            }
            float k = strength * w / 256f;
            for (int y = 0; y < hgt; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (H(x + 1, y) - H(x - 1, y)) * 0.5f;
                float dy = (H(x, y + 1) - H(x, y - 1)) * 0.5f;
                var nv = new Vector3(-dx * k, -dy * k, 1f).normalized;
                np[y * w + x] = new Color32((byte)((nv.x * 0.5f + 0.5f) * 255f), (byte)((nv.y * 0.5f + 0.5f) * 255f), (byte)((nv.z * 0.5f + 0.5f) * 255f), 255);
            }
            n.SetPixels32(np);
            System.IO.File.WriteAllBytes(normalPath, n.EncodeToPNG());
            Object.DestroyImmediate(n);

            AssetDatabase.ImportAsset(albedoPath);
            AssetDatabase.ImportAsset(normalPath);
            var ai = (TextureImporter)AssetImporter.GetAtPath(albedoPath);
            ai.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ai.anisoLevel = 8;
            ai.sRGBTexture = true;
            ai.maxTextureSize = 2048;
            ai.SaveAndReimport();
            var ni = (TextureImporter)AssetImporter.GetAtPath(normalPath);
            ni.textureType = TextureImporterType.NormalMap;
            ni.wrapMode = tile ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            ni.anisoLevel = 8;
            ni.maxTextureSize = 2048;
            ni.SaveAndReimport();
            return new Pair
            {
                albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath),
                normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath),
            };
        }

        static void EnsureFolder(string path) => SandboxBuilder.EnsureFolder(path);

        /// <summary>Назначить пару текстур материалу URP Lit.</summary>
        internal static void Apply(Material m, Pair p, float tiling, float bump)
        {
            if (m == null || p.albedo == null) return;
            m.SetTexture("_BaseMap", p.albedo);
            m.SetTextureScale("_BaseMap", new Vector2(tiling, tiling));
            m.mainTexture = p.albedo;
            if (p.normal != null)
            {
                m.SetTexture("_BumpMap", p.normal);
                m.SetFloat("_BumpScale", bump);
                m.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(m);
        }
    }
}
