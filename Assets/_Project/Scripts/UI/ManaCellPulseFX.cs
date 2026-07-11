using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (HUD v12.1 — user chốt) NHÁY Ô MANA ĐÃ ĐẦY: nghe kênh SỐ Ô (IntEventChannelSO, ManaBar raise) —
    /// mọi ô ĐÃ ĐẦY sáng dần về trắng theo nhịp sin. Nhịp tính từ <c>Time.unscaledTime</c> TUYỆT ĐỐI
    /// (không phase riêng per-ô) → 2/3/4 ô đầy nháy CÙNG PHA thành một khối liền mạch, không đứt đoạn
    /// theo từng bar. Công thức + mặc định (pulseSpeed 6, tintToWhite 0.45) ĐỒNG BỘ
    /// <see cref="BarFullGlowFX"/> để khi đầy cả dãy, vầng glow (component đó lo) thở cùng nhịp với ô.
    /// P2 đầy từ mép PHẢI vào → <see cref="fillFromRight"/>. Thuần uGUI (HUD overlay), chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManaCellPulseFX : MonoBehaviour
    {
        [Header("Kênh số ô (ManaBar raise)")]
        [SerializeField] private IntEventChannelSO segmentsChannel;

        [Header("Các ô fill — THỨ TỰ TRÁI→PHẢI trên màn hình")]
        [SerializeField] private Image[] cells;

        [Tooltip("P2: ô đầy dồn từ mép PHẢI màn hình vào trong (mirror P1).")]
        [SerializeField] private bool fillFromRight;

        [Header("Nhịp (đồng bộ BarFullGlowFX)")]
        [SerializeField] private float pulseSpeed = 6f;
        [Range(0f, 1f)]
        [SerializeField] private float tintToWhite = 0.45f;

        private Color[] _baseCols;
        private int _fullCells;

        private void Awake()
        {
            _baseCols = new Color[cells != null ? cells.Length : 0];
            for (int i = 0; i < _baseCols.Length; i++)
                if (cells[i] != null) _baseCols[i] = cells[i].color;
        }

        private void OnEnable()
        {
            _fullCells = 0;
            RestoreBase();
            if (segmentsChannel != null) segmentsChannel.OnEventRaised += OnSegments;
        }

        private void OnDisable()
        {
            if (segmentsChannel != null) segmentsChannel.OnEventRaised -= OnSegments;
            RestoreBase(); // không kẹt ô sáng khi HUD tắt giữa chừng.
        }

        private void OnSegments(int full)
        {
            _fullCells = Mathf.Clamp(full, 0, cells != null ? cells.Length : 0);
            if (_fullCells == 0) RestoreBase(); // Update không chạy nhánh tint nữa — trả màu ngay.
        }

        private void Update()
        {
            if (cells == null || _baseCols == null || _fullCells <= 0) return;

            // Cùng công thức pha với BarFullGlowFX (sin theo unscaledTime tuyệt đối) → mọi ô + glow
            // đồng nhịp; pause (timeScale=0) vẫn thở.
            float p = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;

            int n = cells.Length;
            for (int i = 0; i < n; i++)
            {
                if (cells[i] == null) continue;
                bool isFull = fillFromRight ? i >= n - _fullCells : i < _fullCells;
                cells[i].color = isFull
                    ? Color.Lerp(_baseCols[i], Color.white, tintToWhite * p)
                    : _baseCols[i];
            }
        }

        private void RestoreBase()
        {
            if (cells == null || _baseCols == null) return;
            for (int i = 0; i < cells.Length && i < _baseCols.Length; i++)
                if (cells[i] != null) cells[i].color = _baseCols[i];
        }
    }
}
