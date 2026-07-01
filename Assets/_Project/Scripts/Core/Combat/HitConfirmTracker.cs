using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// (P15) THEO DÕI "đòn HIỆN HÀNH đã trúng thật chưa" cho điều kiện nối <see cref="FollowUpCondition.OnHit"/>.
    /// MonoBehaviour trên prefab nhân vật. Subscribe kênh <see cref="onHitConfirmed"/> (= kênh
    /// "trúng THẬT không-block" mà HitReactionController raise từ P6). Khi attacker của cú trúng là CHÍNH
    /// MÌNH (so root) → bật cờ <see cref="HitSinceReset"/>.
    ///
    /// "BLOCK KHÔNG TÍNH" đạt TỰ NHIÊN (research §2): block raise kênh KHÁC (OnHitBlocked) rồi return TRƯỚC
    /// khi raise OnHitConfirmed → cờ không bao giờ bật khi đòn bị đỡ. PHẢI nghe đúng OnHitConfirmed, KHÔNG
    /// nghe OnHitLanded (kênh va-chạm-thô bắn cả khi block → sẽ tính nhầm).
    ///
    /// Vòng đời cờ: <see cref="BeginWindow"/> reset cờ ở ĐẦU MỖI đòn (state gọi trong BeginMove) → cờ chỉ
    /// phản ánh đòn HIỆN HÀNH; mắt nối kế không "thừa hưởng" hit của mắt trước.
    ///
    /// Đặt ở FA.Core (chỉ ref HitData/HitEventChannelSO ở Core) để cả FA.Combat (AttackState) lẫn
    /// FA.Specials (SpecialState/SuperState) dùng chung — §1.3 type dùng chung phải ở Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitConfirmTracker : MonoBehaviour
    {
        [Header("Kênh (gán asset OnHitConfirmed của P6 — CÙNG asset HitReactionController raise)")]
        [Tooltip("Kênh 'trúng THẬT không-block'. KHÔNG gán OnHitLanded (block sẽ bị tính nhầm).")]
        [SerializeField] private HitEventChannelSO onHitConfirmed;

        [Header("Tham chiếu")]
        [Tooltip("Root để so 'mình có phải ATTACKER không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        // Cờ "đòn hiện hành đã trúng thật chưa" — reset đầu mỗi đòn qua BeginWindow.
        private bool _hitSinceReset;

        private void Awake()
        {
            if (root == null) root = transform.root;
        }

        private void OnEnable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised += OnHitConfirmed;
        }

        private void OnDisable()
        {
            if (onHitConfirmed != null) onHitConfirmed.OnEventRaised -= OnHitConfirmed;
        }

        /// <summary>Kênh DÙNG CHUNG 2 người chơi → lọc Attacker == mình (KHÔNG bật cờ khi ĐỐI THỦ trúng
        /// mình). HitData.Attacker = root GameObject người đánh (research §4 cạm bẫy 4).</summary>
        private void OnHitConfirmed(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            if (ReferenceEquals(hit.Attacker, me)) _hitSinceReset = true;
        }

        /// <summary>Gọi ở ĐẦU mỗi đòn (BeginMove) — reset cờ để hit-confirm chỉ phản ánh đòn hiện hành.</summary>
        public void BeginWindow() => _hitSinceReset = false;

        /// <summary>True nếu đòn HIỆN HÀNH đã trúng thật (không-block) kể từ <see cref="BeginWindow"/> cuối.</summary>
        public bool HitSinceReset => _hitSinceReset;
    }
}
