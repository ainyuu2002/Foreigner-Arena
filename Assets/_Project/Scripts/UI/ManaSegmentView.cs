using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// HUD WIDGET mana CHIA Ô (P8, research §3.1) — Unity-native: N <see cref="Image"/> ô RỜI, mỗi ô
    /// bật/tắt (đầy/rỗng) theo SỐ Ô mana hiện có. Subscribe <see cref="IntEventChannelSO"/>
    /// (OnManaSegmentsChanged) → ô thứ i ĐẦY nếu i &lt; segments. Code chỉ điều phối enable/sprite; layout
    /// dãy ô để <c>HorizontalLayoutGroup</c> lo (§1.1) → KHÔNG tự tính vị trí.
    ///
    /// Hai cách hiện ô (chọn trong Inspector): (a) bật/tắt GameObject ô đầy, hoặc (b) đổi sprite
    /// full/empty trên cùng Image. Mặc định: nếu có fullSprite/emptySprite → đổi sprite; nếu không →
    /// bật/tắt enabled. Mỗi ô = 1 Image gán theo thứ tự trái→phải. Đặt FA.UI, chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManaSegmentView : MonoBehaviour
    {
        [Header("Kênh sự kiện (số ô nguyên)")]
        [Tooltip("OnManaSegmentsChanged (IntEventChannelSO) — số ô mana ĐẦY. Per-player asset.")]
        [SerializeField] private IntEventChannelSO segmentsChannel;

        [Header("Các ô (Image, thứ tự trái→phải)")]
        [Tooltip("N Image = maxSegments của ResourceConfig. Ô i đầy khi i < segments.")]
        [SerializeField] private Image[] cells;

        [Header("Sprite đầy/rỗng (tùy — bỏ trống thì bật/tắt enabled)")]
        [SerializeField] private Sprite fullSprite;
        [SerializeField] private Sprite emptySprite;

        [Header("Reset khi bật (HUD bật mỗi đầu trận)")]
        [Tooltip("TRUE → OnEnable áp initialSegments ngay. Không có thì HUD hiện TRẠNG THÁI BUILD/TRẬN TRƯỚC " +
                 "(ô đầy giả) cho tới khi hệ mana raise lần đầu — nhân vật không có hệ mana sẽ 'đầy' vĩnh viễn.")]
        [SerializeField] private bool resetOnEnable;
        [SerializeField] private int initialSegments;

        private void OnEnable()
        {
            if (resetOnEnable) OnSegmentsChanged(Mathf.Max(0, initialSegments));
            if (segmentsChannel != null) segmentsChannel.OnEventRaised += OnSegmentsChanged;
        }

        private void OnDisable()
        {
            if (segmentsChannel != null) segmentsChannel.OnEventRaised -= OnSegmentsChanged;
        }

        private void OnSegmentsChanged(int segments)
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i] == null) continue;
                bool full = i < segments;
                ApplyCell(cells[i], full);
            }
        }

        private void ApplyCell(Image cell, bool full)
        {
            if (fullSprite != null || emptySprite != null)
            {
                cell.enabled = true;
                cell.sprite = full ? fullSprite : emptySprite;
            }
            else
            {
                // Không có sprite riêng → ô đầy hiện, ô rỗng ẩn.
                cell.enabled = full;
            }
        }
    }
}
