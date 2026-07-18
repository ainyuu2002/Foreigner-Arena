using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Effects
{
    /// <summary>
    /// ĐỔI BỘ MÀU kiểu MUGEN (feedback test P12: phân biệt 2P chọn trùng nhân vật) — KHÔNG làm lại
    /// animation, KHÔNG đổi shader render của nhân vật:
    ///   1. Texture gốc của sprite hiện tại được "nướng" 1 lần qua Graphics.Blit + shader
    ///      FA/PaletteSwapBake (map các cặp màu trong <see cref="PaletteSwapProfileSO"/>) → cache
    ///      RenderTexture per-texture (sprite MUGEN trải trên nhiều PNG — bake lười khi gặp).
    ///   2. Mỗi LateUpdate override <c>_MainTex</c> của SpriteRenderer bằng MaterialPropertyBlock →
    ///      mọi clip animation giữ nguyên (clip chỉ đổi sprite/UV; màu do texture đã nướng quyết).
    /// Kích hoạt qua BroadcastMessage("ApplyAltPalette") từ PlayerSpawner khi 2 người trùng char
    /// (pattern SendMessage của dự án — GameFlow không ref FA.Effects §1.3). Mặc định KHÔNG làm gì.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PaletteSwapper : MonoBehaviour
    {
        [Tooltip("Bảng màu bộ đồ ALT (1 bộ là đủ — chốt user). BẮT BUỘC để ApplyAltPalette có tác dụng.")]
        [SerializeField] private PaletteSwapProfileSO altProfile;

        [Tooltip("SpriteRenderer THÂN nhân vật. Bỏ trống = SpriteRenderer đầu tiên trong children " +
                 "(KHÔNG tự quét mọi renderer — tránh nhuộm nhầm VFX con).")]
        [SerializeField] private SpriteRenderer target;

        [Tooltip("Shader FA/PaletteSwapBake. Bỏ trống = Shader.Find (đủ cho Editor; build nên gán để " +
                 "shader chắc chắn được đóng gói).")]
        [SerializeField] private Shader bakeShader;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private readonly Dictionary<Texture, RenderTexture> _baked = new Dictionary<Texture, RenderTexture>(16);
        private Material _bakeMat;
        private MaterialPropertyBlock _mpb;
        private bool _active;

        private void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        }

        /// <summary>PlayerSpawner BroadcastMessage khi 2 người chọn TRÙNG nhân vật (P2 nhận).</summary>
        public void ApplyAltPalette()
        {
            if (altProfile == null || altProfile.SwapCount == 0)
            {
                Debug.LogWarning("[PaletteSwapper] Chưa gán PaletteSwapProfileSO/chưa khai màu — bỏ qua.", this);
                return;
            }
            Shader shader = bakeShader != null ? bakeShader : Shader.Find("FA/PaletteSwapBake");
            if (shader == null)
            {
                Debug.LogError("[PaletteSwapper] Không tìm thấy shader FA/PaletteSwapBake.", this);
                return;
            }
            if (_bakeMat == null)
            {
                _bakeMat = new Material(shader);
                altProfile.FillMaterial(_bakeMat);
            }
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _active = true;
            DebugLog.Log($"[PaletteSwapper] {name}: bật bộ màu alt ({altProfile.SwapCount} cặp màu).", this);
        }

        private void LateUpdate()
        {
            if (!_active || target == null || target.sprite == null) return;

            Texture tex = target.sprite.texture;
            if (tex == null) return;
            if (!_baked.TryGetValue(tex, out RenderTexture rt) || rt == null)
            {
                rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = tex.filterMode
                };
                Graphics.Blit(tex, rt, _bakeMat); // bake 1 lần/texture — GPU, không cần Read/Write texture.
                _baked[tex] = rt;
            }

            target.GetPropertyBlock(_mpb);
            _mpb.SetTexture(MainTexId, rt);
            target.SetPropertyBlock(_mpb);
        }

        private void OnDestroy()
        {
            foreach (RenderTexture rt in _baked.Values)
                if (rt != null) rt.Release();
            _baked.Clear();
            if (_bakeMat != null) Destroy(_bakeMat);
        }
    }
}
