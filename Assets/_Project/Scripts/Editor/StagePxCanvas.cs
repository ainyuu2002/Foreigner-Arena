using System.IO;
using UnityEditor;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Canvas vẽ pixel-art cho art generator Phase 11 (Editor-only). Vẽ vào Color32[] rồi xuất PNG +
    /// tự cấu hình TextureImporter (Sprite, Point filter, PPU). Mọi sprite map/background sinh ra từ đây —
    /// KHÔNG cần asset ngoài. Tọa độ (0,0) = góc DƯỚI-TRÁI.
    /// </summary>
    internal sealed class StagePxCanvas
    {
        public readonly int W;
        public readonly int H;
        private readonly Color32[] _px;
        private static System.Random _rng = new System.Random(12345);

        public StagePxCanvas(int w, int h, Color32 fill)
        {
            W = w; H = h;
            _px = new Color32[w * h];
            for (int i = 0; i < _px.Length; i++) _px[i] = fill;
        }

        public static void Reseed(int seed) => _rng = new System.Random(seed);
        public static float Rand01() => (float)_rng.NextDouble();
        public static int RandInt(int minIncl, int maxExcl) => _rng.Next(minIncl, maxExcl);

        public static Color32 Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out Color c);
            return c;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            _px[y * W + x] = c;
        }

        /// <summary>Vẽ đè có alpha-blend (c.a &lt; 255 = trong mờ).</summary>
        public void Blend(int x, int y, Color32 c)
        {
            if (x < 0 || x >= W || y < 0 || y >= H) return;
            if (c.a >= 255) { _px[y * W + x] = c; return; }
            if (c.a == 0) return;
            Color32 d = _px[y * W + x];
            float a = c.a / 255f;
            _px[y * W + x] = new Color32(
                (byte)(c.r * a + d.r * (1f - a)),
                (byte)(c.g * a + d.g * (1f - a)),
                (byte)(c.b * a + d.b * (1f - a)),
                (byte)Mathf.Max(d.a, c.a));
        }

        public void FillRect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Set(xx, yy, c);
        }

        public void BlendRect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Blend(xx, yy, c);
        }

        /// <summary>Gradient dọc từ màu bottom (y=0) lên top (y=H-1), kèm dithering nhẹ cho chất pixel-art.</summary>
        public void VerticalGradient(Color32 bottom, Color32 top, float ditherStrength = 0.04f)
        {
            for (int y = 0; y < H; y++)
            {
                float t = H <= 1 ? 0f : (float)y / (H - 1);
                for (int x = 0; x < W; x++)
                {
                    float tt = Mathf.Clamp01(t + (Rand01() - 0.5f) * ditherStrength);
                    Set(x, y, Color32.Lerp(bottom, top, tt));
                }
            }
        }

        public void Disc(int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                    if (x * x + y * y <= r * r)
                        Blend(cx + x, cy + y, c);
        }

        public void Ring(int cx, int cy, int rOuter, int rInner, Color32 c)
        {
            for (int y = -rOuter; y <= rOuter; y++)
                for (int x = -rOuter; x <= rOuter; x++)
                {
                    int d2 = x * x + y * y;
                    if (d2 <= rOuter * rOuter && d2 >= rInner * rInner)
                        Blend(cx + x, cy + y, c);
                }
        }

        /// <summary>Tam giác cân đỉnh trên (núi, gai băng): đáy tại y, rộng baseW, cao h.</summary>
        public void Triangle(int cx, int y, int baseW, int h, Color32 c)
        {
            for (int row = 0; row < h; row++)
            {
                float t = h <= 1 ? 0f : (float)row / (h - 1);
                int half = Mathf.RoundToInt(baseW * 0.5f * (1f - t));
                for (int x = -half; x <= half; x++)
                    Blend(cx + x, y + row, c);
            }
        }

        /// <summary>Tam giác ngược (icicle/nhũ đá): đáy trên tại y, nhọn xuống dưới.</summary>
        public void TriangleDown(int cx, int yTop, int baseW, int h, Color32 c)
        {
            for (int row = 0; row < h; row++)
            {
                float t = h <= 1 ? 0f : (float)row / (h - 1);
                int half = Mathf.RoundToInt(baseW * 0.5f * (1f - t));
                for (int x = -half; x <= half; x++)
                    Blend(cx + x, yTop - row, c);
            }
        }

        /// <summary>Dải núi silhouette: fill từ đáy canvas lên đường ridge nội suy giữa các đỉnh random.</summary>
        public void MountainRidge(int baseY, int peakMin, int peakMax, int segments, Color32 c)
        {
            var peaks = new int[segments + 1];
            for (int i = 0; i <= segments; i++) peaks[i] = RandInt(peakMin, peakMax + 1);
            for (int x = 0; x < W; x++)
            {
                float fi = (float)x / W * segments;
                int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, segments - 1);
                float ft = fi - i0;
                // nội suy tam giác (nhọn) thay vì mượt — đúng chất núi pixel.
                float ridge = Mathf.Lerp(peaks[i0], peaks[i0 + 1], ft > 0.5f ? (ft - 0.5f) * 2f * 0.5f + 0.5f : ft);
                int top = baseY + Mathf.RoundToInt(ridge);
                for (int y = 0; y <= top && y < H; y++) Blend(x, y, c);
            }
        }

        /// <summary>Dải sine trong mờ (cực quang / sương mù).</summary>
        public void SineBand(float centerY, float amplitude, float wavelength, float phase, int thickness, Color32 c)
        {
            for (int x = 0; x < W; x++)
            {
                float cy = centerY + Mathf.Sin((x / wavelength + phase) * Mathf.PI * 2f) * amplitude;
                for (int t = -thickness / 2; t <= thickness / 2; t++)
                {
                    // mép dải mờ dần cho mềm.
                    float edge = 1f - Mathf.Abs(t) / (thickness / 2f + 0.01f);
                    var cc = c; cc.a = (byte)(c.a * Mathf.Clamp01(edge + 0.15f));
                    Blend(x, Mathf.RoundToInt(cy) + t, cc);
                }
            }
        }

        /// <summary>Rắc chấm ngẫu nhiên (sao, đom đóm nền, tàn lửa tĩnh).</summary>
        public void Speckle(int count, Color32 c, int yMin, int yMax, int size = 1)
        {
            for (int i = 0; i < count; i++)
            {
                int x = RandInt(0, W);
                int y = RandInt(yMin, Mathf.Min(yMax, H));
                if (size <= 1) Blend(x, y, c);
                else BlendRect(x, y, size, size, c);
            }
        }

        public void VLine(int x, int y0, int y1, Color32 c)
        {
            for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++) Blend(x, y, c);
        }

        public void HLine(int y, int x0, int x1, Color32 c)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++) Blend(x, y, c);
        }

        /// <summary>Xuất PNG + cấu hình importer thành Sprite (Point, uncompressed). Trả về Sprite đã import.</summary>
        public Sprite Save(string assetPath, float pixelsPerUnit)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(_px);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            string dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(assetPath, png);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = pixelsPerUnit;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
