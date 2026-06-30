using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 HUD) Bộ đếm COMBO kiểu SF6 — subscribe <see cref="IntEventChannelSO"/> (OnComboChanged per-player,
    /// ComboCounter raise) → hiện số đòn liên tiếp ("7" + nhãn "HITS"). Ẩn khi combo &lt; <see cref="minToShow"/>
    /// (mặc định 2 — chỉ hiện khi thành chuỗi). Decoupling (§1.3): nghe kênh Core, KHÔNG ref FA.Combat.
    /// Code chỉ format chuỗi + bật/tắt; layout/hiệu ứng để uGUI lo (§1.1).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComboView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnComboChanged per-player (IntEventChannelSO) — số hit combo hiện tại.")]
        [SerializeField] private IntEventChannelSO channel;

        [Header("Hiển thị")]
        [Tooltip("Text số combo (vd '7'). Bắt buộc để thấy số.")]
        [SerializeField] private Text countLabel;

        [Tooltip("GameObject cụm combo — ẩn/hiện theo combo. Bỏ trống = dùng chính object này.")]
        [SerializeField] private GameObject content;

        [Tooltip("Combo tối thiểu để HIỆN (≥ giá trị này mới bật). SF6 ~2.")]
        [SerializeField] private int minToShow = 2;

        private GameObject Target => content != null ? content : gameObject;

        private void OnEnable()
        {
            if (channel != null) channel.OnEventRaised += OnCombo;
            Target.SetActive(false); // bắt đầu ẩn (chưa combo).
        }

        private void OnDisable()
        {
            if (channel != null) channel.OnEventRaised -= OnCombo;
        }

        private void OnCombo(int count)
        {
            bool show = count >= minToShow;
            if (Target.activeSelf != show) Target.SetActive(show);
            if (show && countLabel != null) countLabel.text = count.ToString();
        }
    }
}
