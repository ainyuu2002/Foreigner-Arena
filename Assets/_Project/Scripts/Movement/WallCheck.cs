using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Kiểm tra chạm TƯỜNG bằng cách Unity-native (Phase 3, mẫu giống GroundCheck):
    /// Collider2D.IsTouchingLayers(GameLayers.WallMask) — KHÔNG raycast tay (§1.1) vì
    /// CapsuleCollider2D thân đã chạm tường. WallMask do FA.Core cấp (không hardcode index).
    ///
    /// IsTouchingLayers cập nhật theo physics step → đọc/refresh trong FixedUpdate (research §3.3).
    /// CÓ grace/coyote (wallGraceTime): giữ IsWalled thêm vài chục ms sau khi mất contact để lọc nhiễu
    /// chớp-false của CapsuleCollider (cạnh bo tròn) → tránh flicker WallHang⇄Air khi đang bám.
    /// Ngoài "có chạm tường không", còn suy PHÍA tường (trái/phải) từ ContactPoint2D normal để
    /// wall-jump bật RA đúng phía (research §3.3). normal trỏ TỪ tường RA nhân vật → tường nằm
    /// phía -normal.x; quy ước WallSide = +1 tường bên phải, -1 bên trái, 0 không xác định.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class WallCheck : MonoBehaviour
    {
        [Tooltip("Collider thân để kiểm tra chạm tường. Bỏ trống = lấy Collider2D trên cùng object.")]
        [SerializeField] private Collider2D bodyCollider;

        [Tooltip("Ngưỡng |normal.x| để coi contact là 'tường đứng' (lọc contact sàn/đỉnh). 0..1.")]
        [SerializeField, Range(0f, 1f)] private float horizontalNormalThreshold = 0.5f;

        [Tooltip("Grace/coyote (giây) GIỮ IsWalled=true sau khi vừa MẤT contact tường. CapsuleCollider " +
                 "(cạnh bo tròn) có thể chớp false 1 vài physics-step dù người chơi vẫn ép vào tường → " +
                 "gây flicker WallHang⇄Air. Giữ thêm grace này lọc nhiễu đó. WallSide cũng giữ theo lần " +
                 "chạm gần nhất trong thời gian grace. CẦN TEST (~0.08–0.1s).")]
        [SerializeField] private float wallGraceTime = 0.1f;

        /// <summary>True nếu collider thân đang chạm bề mặt Layer Wall, HOẶC còn trong cửa sổ grace.</summary>
        public bool IsWalled { get; private set; }

        /// <summary>
        /// Chạm tường THẬT của frame physics hiện tại (IsTouchingLayers, TRƯỚC khi áp grace).
        /// Dùng để "arm" re-grab WallHang: Motor cần biết nhân vật đã RỜI tường thật ≥1 frame
        /// (IsTouchingRaw==false) thì mới cho bám lại — không bị grace giữ IsWalled=true che mất.
        /// </summary>
        public bool IsTouchingRaw { get; private set; }

        /// <summary>Phía tường: +1 bên phải nhân vật, -1 bên trái, 0 không xác định.</summary>
        public int WallSide { get; private set; }

        // Buffer dùng lại để KHÔNG cấp phát mỗi FixedUpdate (tránh rác GC).
        private readonly ContactPoint2D[] _contacts = new ContactPoint2D[8];

        // Thời điểm physics gần nhất CÒN chạm tường thật (để tính grace). -inf = chưa từng chạm.
        private float _lastTouchTime = float.NegativeInfinity;

        private void Awake()
        {
            if (bodyCollider == null)
                bodyCollider = GetComponent<Collider2D>();
        }

        private void FixedUpdate()
        {
            // Đọc đúng nhịp physics. WallMask do Core cấp (không hardcode index).
            bool touchingNow = bodyCollider != null &&
                               bodyCollider.IsTouchingLayers(GameLayers.WallMask);
            IsTouchingRaw = touchingNow; // raw (chưa grace) cho việc arm re-grab ở Motor

            if (touchingNow)
            {
                _lastTouchTime = Time.fixedTime;
                WallSide = ResolveWallSide();              // cập nhật phía theo contact thật
                IsWalled = true;
            }
            else
            {
                // Grace/coyote: còn trong cửa sổ → giữ IsWalled + WallSide của lần chạm gần nhất.
                IsWalled = (Time.fixedTime - _lastTouchTime) <= wallGraceTime;
                if (!IsWalled) WallSide = 0;
            }
        }

        /// <summary>
        /// Suy phía tường từ contact có normal NGANG nhất, chỉ tính contact với collider Layer Wall.
        /// normal trỏ từ bề mặt ra nhân vật → tường ở phía -normal.x: normal.x < 0 ⇒ tường bên phải (+1).
        /// </summary>
        private int ResolveWallSide()
        {
            int count = bodyCollider.GetContacts(_contacts);
            float best = horizontalNormalThreshold;
            int side = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D other = _contacts[i].collider;
                if (other == null || other.gameObject.layer != GameLayers.Wall) continue;

                float nx = _contacts[i].normal.x;
                float absNx = Mathf.Abs(nx);
                if (absNx >= best)
                {
                    best = absNx;
                    side = nx < 0f ? 1 : -1; // normal trỏ ra nhân vật → tường ở phía ngược lại
                }
            }
            return side;
        }
    }
}
