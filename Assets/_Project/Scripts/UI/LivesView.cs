using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) HUD số mạng per-player — subscribe <see cref="IntEventChannelSO"/> (OnLivesChanged per-player,
    /// LivesSystem raise) → bật/tắt N icon mạng (giống ManaSegmentView P8) HOẶC set text số. Decoupling
    /// (§1.3): nghe kênh Core, KHÔNG ref FA.GameFlow. Layout dãy icon để HorizontalLayoutGroup lo (§1.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LivesView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnLivesChanged per-player (IntEventChannelSO) — số mạng còn lại.")]
        [SerializeField] private IntEventChannelSO channel;

        [Header("Hiển thị bằng icon (tùy)")]
        [Tooltip("N icon mạng (thứ tự). Icon i HIỆN khi i < lives. Bỏ trống nếu dùng text.")]
        [SerializeField] private Image[] icons;

        [Header("Hoặc bằng text (tùy)")]
        [Tooltip("Text số mạng (vd 'x3'). Bỏ trống nếu dùng icon.")]
        [SerializeField] private Text label;

        private void OnEnable() { if (channel != null) channel.OnEventRaised += OnLives; }
        private void OnDisable() { if (channel != null) channel.OnEventRaised -= OnLives; }

        private void OnLives(int lives)
        {
            if (icons != null)
                for (int i = 0; i < icons.Length; i++)
                    if (icons[i] != null) icons[i].enabled = i < lives;

            if (label != null) label.text = "x" + Mathf.Max(0, lives);
        }
    }
}
