using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 tuần 3) MÀN CHỌN NHÂN VẬT bố cục mới (theo mock): lưới nhân vật CHUNG ở giữa + 2 card preview
    /// P1/P2 hai bên + nút TIẾP TỤC CHỌN MAP dưới cùng. Chơi chung 1 chuột nên chọn THEO LƯỢT:
    /// click lưới gán cho người CHƯA SẴN SÀNG đầu tiên (P1 trước) → XÁC NHẬN khóa lựa chọn (nút đổi thành
    /// CHỌN LẠI để đổi ý) → đủ 2 người sẵn sàng thì TIẾP TỤC sáng.
    ///
    /// KHÁC bản cũ (2 hàng IntButtonRaiser bắn kênh NGAY khi click): kênh CharSelected_P1/_P2 chỉ RAISE
    /// khi bấm TIẾP TỤC — nhờ vậy đổi ý thoải mái trước khi chốt, MatchManager không nhận giá trị rác.
    /// Raise P1 rồi P2 liên tiếp → gate 2 lựa chọn trong MatchManager tự chuyển StageSelect (không sửa
    /// gameplay §1.3). Panel bật lại (Back từ End) → OnEnable reset sạch lượt chọn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharSelectScreenView : MonoBehaviour
    {
        [Header("Kênh (raise khi TIẾP TỤC)")]
        [SerializeField] private IntEventChannelSO charSelectedP1;
        [SerializeField] private IntEventChannelSO charSelectedP2;

        [Header("Dữ liệu hiển thị (builder bake từ CharacterRegistry)")]
        [SerializeField] private string[] names;
        [SerializeField] private Sprite[] portraits;

        [Header("Lưới — tag chọn từng ô (builder wire theo index)")]
        [SerializeField] private GameObject[] tagsP1;
        [SerializeField] private GameObject[] tagsP2;

        [Header("Card P1")]
        [SerializeField] private Image previewImageP1;
        [SerializeField] private Text previewLetterP1;
        [SerializeField] private Text nameLabelP1;
        [SerializeField] private Text statusLabelP1;
        [SerializeField] private Button confirmButtonP1;
        [SerializeField] private Text confirmLabelP1;

        [Header("Card P2")]
        [SerializeField] private Image previewImageP2;
        [SerializeField] private Text previewLetterP2;
        [SerializeField] private Text nameLabelP2;
        [SerializeField] private Text statusLabelP2;
        [SerializeField] private Button confirmButtonP2;
        [SerializeField] private Text confirmLabelP2;

        [Header("Tiếp tục")]
        [SerializeField] private Button continueButton;

        private int _pendingP1 = -1, _pendingP2 = -1;
        private bool _readyP1, _readyP2;

        private void OnEnable()
        {
            _pendingP1 = _pendingP2 = -1;
            _readyP1 = _readyP2 = false;
            Refresh();
        }

        /// <summary>Ô lưới index i (persistent listener int) — gán cho người chưa sẵn sàng đầu tiên.</summary>
        public void SelectCell(int index)
        {
            if (names == null || index < 0 || index >= names.Length) return;
            if (!_readyP1) _pendingP1 = index;
            else if (!_readyP2) _pendingP2 = index;
            Refresh();
        }

        /// <summary>Nút XÁC NHẬN P1 — toggle: chưa khóa thì khóa, khóa rồi thì CHỌN LẠI.</summary>
        public void ConfirmP1()
        {
            if (_readyP1) _readyP1 = false;
            else if (_pendingP1 >= 0) _readyP1 = true;
            Refresh();
        }

        public void ConfirmP2()
        {
            if (_readyP2) _readyP2 = false;
            else if (_pendingP2 >= 0) _readyP2 = true;
            Refresh();
        }

        /// <summary>Nút TIẾP TỤC CHỌN MAP — chốt: raise 2 kênh liên tiếp → MatchManager sang StageSelect.</summary>
        public void Continue()
        {
            if (!_readyP1 || !_readyP2) return;
            if (charSelectedP1 != null) charSelectedP1.RaiseEvent(_pendingP1);
            if (charSelectedP2 != null) charSelectedP2.RaiseEvent(_pendingP2);
        }

        // ───────────────────── trình bày ─────────────────────

        private void Refresh()
        {
            RefreshTags(tagsP1, _pendingP1);
            RefreshTags(tagsP2, _pendingP2);

            RefreshCard(_pendingP1, _readyP1, previewImageP1, previewLetterP1, nameLabelP1,
                        statusLabelP1, confirmButtonP1, confirmLabelP1);
            RefreshCard(_pendingP2, _readyP2, previewImageP2, previewLetterP2, nameLabelP2,
                        statusLabelP2, confirmButtonP2, confirmLabelP2);

            if (continueButton != null) continueButton.interactable = _readyP1 && _readyP2;
        }

        private static void RefreshTags(GameObject[] tags, int selected)
        {
            if (tags == null) return;
            for (int i = 0; i < tags.Length; i++)
                if (tags[i] != null) tags[i].SetActive(i == selected);
        }

        private void RefreshCard(int pending, bool ready, Image previewImage, Text previewLetter,
                                 Text nameLabel, Text statusLabel, Button confirmButton, Text confirmLabel)
        {
            bool has = pending >= 0 && names != null && pending < names.Length;
            string nm = has ? names[pending] : "—";
            Sprite sp = has && portraits != null && pending < portraits.Length ? portraits[pending] : null;

            if (previewImage != null)
            {
                previewImage.sprite = sp;
                previewImage.enabled = sp != null;
            }
            if (previewLetter != null)
            {
                // Chưa có portrait art → chữ cái đầu tên cỡ lớn làm placeholder (code-only).
                previewLetter.text = sp != null ? "" : (has ? nm.Substring(0, 1).ToUpperInvariant() : "?");
            }
            if (nameLabel != null) nameLabel.text = nm;
            if (statusLabel != null) statusLabel.text = ready ? "Sẵn sàng!" : (has ? "Đang chọn..." : "Chưa chọn");
            if (confirmButton != null) confirmButton.interactable = has;
            if (confirmLabel != null) confirmLabel.text = ready ? "CHỌN LẠI" : "XÁC NHẬN";
        }
    }
}
