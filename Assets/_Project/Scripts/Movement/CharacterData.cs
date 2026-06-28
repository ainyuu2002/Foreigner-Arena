using UnityEngine;

namespace FA.Movement
{
    /// <summary>
    /// Dữ liệu nhân vật (ScriptableObject) — KHÔNG hardcode hằng số trong code (§1.2/§3.4).
    /// Schema MỞ RỘNG: thêm field cho phase sau = thêm field, không phá asset cũ.
    /// Tạo asset: Create → FA → Character Data. 1 asset/nhân vật (vd SampleFighter_Data).
    ///
    /// Phase 1 dùng: walkSpeed (đi bộ), mass + gravityScale (Motor set lên Rigidbody2D lúc Start;
    /// gravityScale thực sự phát huy ở P2 nhưng set sẵn từ P1 vô hại).
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterData", menuName = "FA/Character Data", order = 0)]
    public sealed class CharacterData : ScriptableObject
    {
        [Header("Di chuyển ngang (Phase 1)")]
        [Tooltip("Tốc độ đi bộ (units/s). CẦN TEST cảm giác đi theo scale tile.")]
        public float walkSpeed = 4f;

        [Header("Vật lý cơ thể")]
        [Tooltip("Khối lượng Rigidbody2D. P7 knockback dùng. Motor set lên rb.mass lúc Start.")]
        public float mass = 1f;

        [Tooltip("gravityScale của Rigidbody2D. Dùng THẬT ở Phase 2 (nhảy/rơi). " +
                 "Baseline CẦN TEST ở P2; nhân vật NẶNG tăng lên → rơi nhanh & nhảy thấp hơn. " +
                 "Motor set lên rb.gravityScale lúc Start; ResetGravity() trả về đúng giá trị này.")]
        public float gravityScale = 3f;

        [Header("Nhảy (Phase 2) — CẦN TEST cảm giác (game-feel)")]
        [Tooltip("Vận tốc LÊN ban đầu (units/s) khi nhảy. Set THẲNG vào linearVelocity.y " +
                 "(deterministic, không AddForce). Với gravityScale=3 → đỉnh ~jumpSpeed²/(2·g). CẦN TEST.")]
        public float jumpSpeed = 12f;

        [Tooltip("Số lần nhảy thêm trên KHÔNG. GDD: double jump 1 lần → maxAirJumps = 1. " +
                 "Reset mỗi khi chạm đất. Nhân vật khác có thể khác.")]
        public int maxAirJumps = 1;

        [Tooltip("Mức điều khiển NGANG trên không (× walkSpeed). 0 = không drift, 1 = đầy đủ như đi bộ. " +
                 "0..1. CẦN TEST.")]
        [Range(0f, 1f)] public float airControl = 0.8f;

        [Header("Dash / Tường (Phase 3) — CẦN TEST cảm giác (game-feel)")]
        [Tooltip("Vận tốc NGANG khi dash (units/s). Set THẲNG vào linearVelocity.x (deterministic, " +
                 "không AddForce → không phụ thuộc mass). Nên > walkSpeed. CẦN TEST.")]
        public float dashSpeed = 10f;

        [Tooltip("Thời lượng 1 cú dash (giây) trước khi tự về Idle/Walk (mặt đất) hoặc Air (trên không). " +
                 "Spam = re-enter DashState (reset timer). Đây là thời lượng GAMEPLAY của state cơ động " +
                 "(không phải frame-data đòn đánh) → đếm bằng timer là chấp nhận; P5+ có thể đổi sang " +
                 "Animation Event nếu cần frame-chính-xác. CẦN TEST.")]
        public float dashDuration = 0.18f;

        [Tooltip("Cho phép dash khi TRÊN KHÔNG (air dash). Cần BẬT để dash vào tường → bám (Wall Hang). " +
                 "Có thể tắt nếu muốn dash chỉ ở mặt đất. CẦN TEST.")]
        public bool allowAirDash = true;

        [Tooltip("Số lần air-dash cho phép giữa 2 cú nhảy. GDD/quyết định người dùng: 1 air-dash, " +
                 "chỉ nạp lại khi NHẢY (ground/double/wall jump) hoặc chạm đất. Reset khi grounded; " +
                 "refresh = maxAirDashes mỗi cú nhảy; tiêu 1 khi vào air-dash. CẦN TEST.")]
        public int maxAirDashes = 1;

        [Tooltip("Tốc độ trượt xuống khi BÁM tường (units/s, dương). Mỗi FixedTick SET THẲNG " +
                 "vy = -wallSlideSpeed → trượt chậm CỐ ĐỊNH, deterministic, chậm hơn rơi tự do. " +
                 "Dùng property Rigidbody2D, không tự tích phân. CẦN TEST.")]
        public float wallSlideSpeed = 2f;

        [Tooltip("Tốc độ NGANG ép VÀO tường khi bám (units/s, dương nhỏ). WallHang set " +
                 "vx = WallSide * wallStickSpeed để CapsuleCollider luôn áp sát tường, không tạo khe hở " +
                 "siêu nhỏ làm IsTouchingLayers chớp false → tránh flicker WallHang⇄Air. Giữ NHỎ để không " +
                 "trông như trượt ngang. CẦN TEST.")]
        public float wallStickSpeed = 1.5f;

        [Tooltip("Thành phần NGANG cú wall-jump (units/s, bật RA khỏi tường). PHẢI > 0 để bật KHỎI tường " +
                 "thật (nếu = 0 nhân vật không rời tường → re-grab không bao giờ được arm → không leo " +
                 "bậc được). Đủ để 'ăn' ra khỏi tường trước khi điều khiển lại. CẦN TEST.")]
        public float wallJumpSpeedX = 7f;

        [Tooltip("Thành phần DỌC cú wall-jump (units/s, leo LÊN). ≈ jumpSpeed để leo đều. CẦN TEST.")]
        public float wallJumpSpeedY = 12f;

        [Tooltip("Cửa sổ NGẮN sau wall-jump (giây) GIỮ momentum bật ngang (không bị ApplyAirDrift " +
                 "triệt tiêu ngay frame kế). Hết cửa sổ này → WallJump tự chuyển sang AirState (rơi " +
                 "tự do thật, khớp Animator Fall, cho phép re-grab leo bậc). 0 = thoát ngay. CẦN TEST.")]
        public float wallJumpControlLock = 0.1f;

        // --- Chừa chỗ cho phase sau (KHÔNG set giá trị gameplay sớm) ---
        // [Header("Knockback (Phase 7)")] public float percentCap;
    }
}
