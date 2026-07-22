using System.Collections.Generic;
using UnityEngine;

namespace FA.UI
{
    /// <summary>
    /// (P12 Menu FX tầng 2) SÉT THỦ TỤC 2D tỏa từ orb năng lượng — port từ demo HTML canvas (midpoint
    /// displacement), TINH CHỈNH cho menu:
    ///   • Rate hạ thấp so với demo (menu cần tĩnh lặng có ĐIỂM NHẤN, không phải bão liên tục).
    ///   • Mỗi tia vẽ THÊM 1 line to mờ phía sau = GLOW GIẢ — không cần Bloom/HDR (không đụng URP Asset chung).
    ///   • Material qua <see cref="MenuFXAssets"/> (shader built-in "Particles/Standard Unlit" ra MAGENTA
    ///     trong URP build).
    ///   • <see cref="syncFlash"/>: burst sét lớn kích chớp lóe màn hình (LightningFlashFX) CÙNG NHỊP —
    ///     sét có hình + khung cảnh bừng sáng theo.
    /// Đặt GameObject tại tâm orb của BG art. LineRenderer pool — không xả rác mỗi frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralLightning : MonoBehaviour
    {
        [Header("Màu sắc (khớp orb tím/xanh của BG art)")]
        public Color majorColor = new Color(0.69f, 0.31f, 0.97f); // tím neon
        public Color minorColor = new Color(0.48f, 0.90f, 1f);    // xanh băng
        public Color coreColor = Color.white;                      // lõi trắng nóng

        [Header("Tần suất (mỗi giây) — TIẾT CHẾ cho menu, thấp hơn demo HTML ~3 lần")]
        public float minorRate = 2f;
        public float majorRate = 0.9f;
        public float burstRate = 0.12f;

        [Header("Kích thước (world units — camera menu ortho size 8)")]
        public float majorLengthMin = 1.5f;
        public float majorLengthMax = 4f;
        public float minorLengthMin = 0.6f;
        public float minorLengthMax = 1.6f;
        public float displacement = 1.1f;
        [Range(0f, 0.5f)] public float branchChance = 0.18f;

        [Header("Thời gian sống")]
        public float fadeSpeedMin = 4f;
        public float fadeSpeedMax = 8f;

        [Header("Render")]
        public string sortingLayerName = "Default";
        [Tooltip("Giữa BG canvas (250) và menu content (300) — sét nằm SAU chữ/nút, TRƯỚC nền.")]
        public int sortingOrder = 255;
        public float majorWidth = 0.055f;
        public float minorWidth = 0.028f;
        [Tooltip("Bề rộng lớp glow giả = width × hệ số này.")]
        public float glowWidthScale = 3.5f;

        [Header("Đồng bộ chớp lóe")]
        [Tooltip("(Tùy) LightningFlashFX của menu — burst sét sẽ kích FlashOnce cùng lúc.")]
        [SerializeField] private LightningFlashFX syncFlash;

        // ---------------------------------------------------------------

        private sealed class Bolt
        {
            public readonly List<LineRenderer> core = new List<LineRenderer>();
            public readonly List<LineRenderer> glow = new List<LineRenderer>();
            public float life = 1f;
            public float decay;
            public Color color;
        }

        private readonly List<Bolt> _activeBolts = new List<Bolt>();
        private readonly Queue<LineRenderer> _pool = new Queue<LineRenderer>();
        private Material _lineMat;

        private void Awake()
        {
            _lineMat = MenuFXAssets.AdditiveMaterial(withSoftCircle: false);
        }

        private void OnDisable()
        {
            // Menu ẩn (transition) → dọn tia đang sống, trả hết vào pool.
            foreach (var b in _activeBolts)
            {
                foreach (var lr in b.core) Recycle(lr);
                foreach (var lr in b.glow) Recycle(lr);
            }
            _activeBolts.Clear();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // menu không phụ thuộc timeScale.

            if (Random.value < minorRate * dt) SpawnBolt(false);
            if (Random.value < majorRate * dt) SpawnBolt(true);
            if (Random.value < burstRate * dt)
            {
                int n = Random.Range(4, 9);
                for (int i = 0; i < n; i++) SpawnBolt(i % 2 == 0);
                if (syncFlash != null) syncFlash.FlashOnce(); // khung cảnh bừng sáng cùng burst.
            }

            for (int i = _activeBolts.Count - 1; i >= 0; i--)
            {
                var b = _activeBolts[i];
                b.life -= b.decay * dt;
                if (b.life <= 0f)
                {
                    foreach (var lr in b.core) Recycle(lr);
                    foreach (var lr in b.glow) Recycle(lr);
                    _activeBolts.RemoveAt(i);
                    continue;
                }

                // Chớp nhấp nháy: alpha dao động dữ dội.
                float flicker = b.life * Random.Range(0.6f, 1f);
                Color c = b.color;

                Color coreCol = Color.Lerp(new Color(c.r, c.g, c.b, flicker), coreColor, 0.6f * flicker);
                foreach (var lr in b.core)
                {
                    lr.startColor = coreCol;
                    lr.endColor = new Color(c.r, c.g, c.b, flicker);
                }

                Color glowCol = new Color(c.r, c.g, c.b, flicker * 0.30f); // glow giả: mờ, to.
                foreach (var lr in b.glow)
                {
                    lr.startColor = glowCol;
                    lr.endColor = new Color(c.r, c.g, c.b, flicker * 0.12f);
                }
            }
        }

        // ------------------------- SPAWN -------------------------------

        private void SpawnBolt(bool major)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float len = major ? Random.Range(majorLengthMin, majorLengthMax)
                              : Random.Range(minorLengthMin, minorLengthMax);
            Vector2 start = transform.position;
            Vector2 end = start + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.8f) * len;

            var bolt = new Bolt
            {
                decay = Random.Range(fadeSpeedMin, fadeSpeedMax),
                color = major ? majorColor : minorColor
            };

            float width = major ? majorWidth : minorWidth;
            var path = JaggedPath(start, end, major ? displacement : displacement * 0.45f);
            bolt.glow.Add(DrawPath(path, width * glowWidthScale));
            bolt.core.Add(DrawPath(path, width));

            // Nhánh phụ cho bolt chính.
            if (major)
            {
                for (int i = 2; i < path.Count - 2; i++)
                {
                    if (Random.value < branchChance)
                    {
                        float ba = angle + Random.Range(-0.8f, 0.8f);
                        float bl = Random.Range(0.3f, 1f);
                        Vector2 bEnd = path[i] + new Vector2(Mathf.Cos(ba), Mathf.Sin(ba)) * bl;
                        var bPath = JaggedPath(path[i], bEnd, displacement * 0.35f);
                        bolt.glow.Add(DrawPath(bPath, minorWidth * 0.7f * glowWidthScale));
                        bolt.core.Add(DrawPath(bPath, minorWidth * 0.7f));
                    }
                }
            }
            _activeBolts.Add(bolt);
        }

        /// <summary>Midpoint displacement — giống hàm jaggedPath của bản JS.</summary>
        private List<Vector2> JaggedPath(Vector2 a, Vector2 b, float disp)
        {
            var result = new List<Vector2>();
            Subdivide(a, b, disp, result);
            result.Add(b);
            return result;
        }

        private void Subdivide(Vector2 a, Vector2 b, float disp, List<Vector2> outList)
        {
            if (disp < 0.05f) { outList.Add(a); return; }
            Vector2 mid = (a + b) * 0.5f + Random.insideUnitCircle * disp;
            Subdivide(a, mid, disp / 1.9f, outList);
            Subdivide(mid, b, disp / 1.9f, outList);
        }

        // ---------------------- LINE POOLING ---------------------------

        private LineRenderer DrawPath(List<Vector2> path, float width)
        {
            LineRenderer lr = _pool.Count > 0 ? _pool.Dequeue() : CreateLine();
            lr.gameObject.SetActive(true);
            lr.positionCount = path.Count;
            for (int i = 0; i < path.Count; i++)
                lr.SetPosition(i, new Vector3(path[i].x, path[i].y, 0f));
            lr.startWidth = width;
            lr.endWidth = width * 0.3f; // thon dần về đầu tia.
            return lr;
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("bolt");
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = _lineMat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 2;
            lr.sortingLayerName = sortingLayerName;
            lr.sortingOrder = sortingOrder;
            return lr;
        }

        private void Recycle(LineRenderer lr)
        {
            lr.gameObject.SetActive(false);
            _pool.Enqueue(lr);
        }
    }

    /// <summary>
    /// Helper material/texture DÙNG CHUNG cho FX menu (ProceduralLightning + FireSparks).
    /// URP-first: cấu hình blend ADDITIVE trên "Universal Render Pipeline/Particles/Unlit"
    /// (shader built-in ra magenta trong URP build). Fallback: Sprites/Default (URP 2D luôn có,
    /// alpha-blend — kém rực hơn nhưng an toàn tuyệt đối).
    /// </summary>
    internal static class MenuFXAssets
    {
        private static Texture2D _softCircle;

        public static Material AdditiveMaterial(bool withSoftCircle)
        {
            Material m;
            var urp = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urp != null)
            {
                m = new Material(urp);
                m.SetFloat("_Surface", 1f); // Transparent
                m.SetFloat("_Blend", 2f);   // Additive
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                m = new Material(Shader.Find("Sprites/Default"));
            }

            if (withSoftCircle) m.mainTexture = SoftCircle();
            return m;
        }

        /// <summary>Chấm tròn mềm 64×64 sinh bằng code (thay shadowBlur của canvas JS) — không cần asset.</summary>
        private static Texture2D SoftCircle()
        {
            if (_softCircle != null) return _softCircle;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "MenuFX_SoftCircle"
            };
            float half = (size - 1) * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2.2f); // lõi đặc, viền tan mềm.
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _softCircle = tex;
            return tex;
        }
    }
}
