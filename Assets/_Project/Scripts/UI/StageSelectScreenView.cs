using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 tuần 3) MÀN CHỌN MAP bố cục mock: CAROUSEL giữa (card map lớn + 2 card mờ 2 bên + mũi tên
    /// &lt; &gt; + chấm trang) + card BẢN ĐỒ NGẪU NHIÊN bên phải + nút BẮT ĐẦU TRẬN ĐẤU dưới cùng.
    ///
    /// Kênh StageSelected CHỈ raise khi bấm BẮT ĐẦU (xem map thoải mái không sợ vào trận nhầm — bản cũ
    /// click nút map là đi luôn). Chế độ ngẫu nhiên: click card random để bật/tắt; khi bật, BẮT ĐẦU sẽ
    /// Random.Range trong danh sách map. Ảnh map lấy từ StageRegistry.icon (builder bake mảng) —
    /// chưa có icon thì hiện chữ cái đầu tên map (code-only). MatchManager nghe kênh như cũ (§1.3).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSelectScreenView : MonoBehaviour
    {
        [Header("Kênh (raise khi BẮT ĐẦU)")]
        [SerializeField] private IntEventChannelSO stageSelectedChannel;

        [Header("Dữ liệu (builder bake từ StageRegistry)")]
        [SerializeField] private string[] names;
        [SerializeField] private Sprite[] previews;

        [Header("Carousel")]
        [SerializeField] private Text mapNameLabel;
        [SerializeField] private Image centerImage;
        [SerializeField] private Text centerLetter;
        [SerializeField] private Image prevImage;
        [SerializeField] private Text prevLetter;
        [SerializeField] private Image nextImage;
        [SerializeField] private Text nextLetter;
        [SerializeField] private Image[] dots;

        [Header("Ngẫu nhiên")]
        [Tooltip("Viền sáng quanh card random — bật khi đang ở chế độ ngẫu nhiên.")]
        [SerializeField] private GameObject randomHighlight;

        [Header("Màu chấm trang")]
        [SerializeField] private Color dotActive = Color.white;
        [SerializeField] private Color dotInactive = new Color(1f, 1f, 1f, 0.25f);

        private int _index;
        private bool _random;

        private void OnEnable()
        {
            _index = 0;
            _random = false;
            Refresh();
        }

        public void Next() => Step(+1);
        public void Prev() => Step(-1);

        private void Step(int dir)
        {
            if (names == null || names.Length == 0) return;
            _random = false; // lật carousel = chọn tay, tắt chế độ ngẫu nhiên.
            _index = (_index + dir + names.Length) % names.Length;
            Refresh();
        }

        /// <summary>Card BẢN ĐỒ NGẪU NHIÊN (persistent listener) — toggle chế độ ngẫu nhiên.</summary>
        public void ToggleRandom()
        {
            _random = !_random;
            Refresh();
        }

        /// <summary>Nút BẮT ĐẦU TRẬN ĐẤU — chốt map (random thì bốc thăm lúc này) → MatchManager vào trận.</summary>
        public void StartMatch()
        {
            if (stageSelectedChannel == null || names == null || names.Length == 0) return;
            int idx = _random ? Random.Range(0, names.Length) : _index;
            stageSelectedChannel.RaiseEvent(idx);
        }

        // ───────────────────── trình bày ─────────────────────

        private void Refresh()
        {
            if (names == null || names.Length == 0) return;
            int n = names.Length;

            SetCard(centerImage, centerLetter, _index);
            SetCard(prevImage, prevLetter, (_index - 1 + n) % n);
            SetCard(nextImage, nextLetter, (_index + 1) % n);

            if (mapNameLabel != null) mapNameLabel.text = _random ? "? ? ?" : names[_index];
            if (randomHighlight != null) randomHighlight.SetActive(_random);

            if (dots != null)
                for (int i = 0; i < dots.Length; i++)
                    if (dots[i] != null) dots[i].color = (!_random && i == _index) ? dotActive : dotInactive;
        }

        private void SetCard(Image img, Text letter, int idx)
        {
            Sprite sp = previews != null && idx < previews.Length ? previews[idx] : null;
            if (img != null)
            {
                img.sprite = sp;
                img.enabled = sp != null;
            }
            if (letter != null)
                letter.text = sp != null ? "" : names[idx].Substring(0, 1).ToUpperInvariant();
        }
    }
}
