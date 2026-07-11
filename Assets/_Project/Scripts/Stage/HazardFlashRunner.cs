using UnityEngine;

namespace FA.Stage
{
    /// <summary>
    /// FLASH MÀU NHÂN VẬT khi dính hazard môi trường (feedback test P11: "không có gì báo hiệu mất
    /// máu"). Zone gọi <see cref="Flash"/> → nhuộm SpriteRenderer victim 1 nhịp ngắn rồi TỰ trả về màu
    /// cũ. Runner tự gắn lên object SpriteRenderer lần đầu (1 victim = 1 runner, tái dùng — không
    /// Instantiate rác). Chỉ đụng màu sprite, KHÔNG đụng gameplay.
    /// Lưu ý: màu-gốc chụp tại lúc BẮT ĐẦU flash — nếu DMG Buff (P13) đổi tint ĐÚNG giữa nhịp flash,
    /// tint buff bị trả về màu trước đó ~0.1s (hiếm + tự đúng lại ở flash sau). CẦN TEST.
    /// </summary>
    public sealed class HazardFlashRunner : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private Color _original;
        private float _until;
        private bool _active;

        /// <summary>Nhuộm SpriteRenderer (tìm trong children của victim) màu <paramref name="color"/>
        /// trong <paramref name="seconds"/> giây. Gọi chồng khi đang flash = gia hạn, không chụp lại màu.</summary>
        public static void Flash(Component victim, Color color, float seconds)
        {
            if (victim == null) return;
            var sr = victim.GetComponentInChildren<SpriteRenderer>();
            if (sr == null) return;

            var runner = sr.GetComponent<HazardFlashRunner>();
            if (runner == null) runner = sr.gameObject.AddComponent<HazardFlashRunner>();
            runner.Play(sr, color, seconds);
        }

        private void Play(SpriteRenderer sr, Color color, float seconds)
        {
            _sr = sr;
            if (!_active) _original = sr.color; // chỉ chụp màu gốc khi KHÔNG đang flash.
            _active = true;
            _until = Time.time + seconds;
            sr.color = color;
        }

        private void Update()
        {
            if (!_active || Time.time < _until) return;
            if (_sr != null) _sr.color = _original;
            _active = false;
        }
    }
}
