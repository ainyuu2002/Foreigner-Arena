using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// HITSTOP per-nhân-vật (§5): khi 1 cú trúng liên quan MÌNH (Attacker HOẶC Victim == myRoot) →
    /// freeze N frame: Animator.speed=0 + lưu&zero velocity + gravityScale=0 → khôi phục sau.
    /// KHÔNG Time.timeScale (yêu cầu cứng §5) → 2 player/2 trận đấu độc lập. Đếm bằng FIXED-STEP
    /// (số FixedUpdate) → deterministic. ĐẾM FRAME Ở ĐÂY HỢP LỆ: hitstop là hiệu ứng gameplay tức
    /// thời, KHÔNG phải frame-data đòn (nó freeze chính clip đó — không nằm trên clip).
    ///
    /// Hit chồng nhau → RESTART bộ đếm (lấy max), KHÔNG cộng dồn → tránh đứng hình vô hạn.
    /// MoveData ĐÃ có hitstopFrames (4–8; đòn nặng lâu hơn) → đọc qua HitData.
    ///
    /// Cạm bẫy đã xử: Animator.speed=0 làm Animation Event NGỪNG trong hitstop (đúng mong muốn — đòn
    /// "đứng hình" rồi tiếp). Khôi phục gravityScale về baseline (đọc lúc bắt đầu freeze, đối xứng
    /// ResetGravity của Movement) → không rơi oan trong hitstop.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitstopController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Kênh OnHitLanded (HitEventChannelSO). BẮT BUỘC gán — phải CÙNG asset với HitboxAuthoring.")]
        [SerializeField] private HitEventChannelSO hitChannel;

        [Tooltip("Root để so 'cú trúng có liên quan mình không'. Bỏ trống = lấy transform.root.")]
        [SerializeField] private Transform root;

        [Tooltip("Rigidbody2D đóng băng trong hitstop. Bỏ trống = lấy trên cùng object root.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("Animator để set speed=0 trong hitstop. Bỏ trống = lấy trong children.")]
        [SerializeField] private Animator animator;

        private int _framesLeft;            // số FixedUpdate còn freeze (0 = không freeze).

        /// <summary>Đang trong hitstop? HurtState/BlockState (cùng module) hỏi để ĐÓNG BĂNG đồng hồ
        /// hitstun/blockstun trong freeze — nếu vẫn đếm, thời gian hiển thị hurt = hitstun − hitstop
        /// (vừa vào đã thoát) và pushback bị hitstop nuốt. Đồng hồ chỉ chạy khi hết freeze.</summary>
        public bool IsFrozen => _framesLeft > 0;
        private Vector2 _savedVelocity;
        private float _savedGravityScale;
        private float _savedAnimatorSpeed;

        private void Awake()
        {
            if (root == null) root = transform.root;
            if (body == null) body = root != null ? root.GetComponent<Rigidbody2D>() : GetComponent<Rigidbody2D>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void OnEnable()
        {
            if (hitChannel != null) hitChannel.OnEventRaised += OnHitLanded;
        }

        private void OnDisable()
        {
            if (hitChannel != null) hitChannel.OnEventRaised -= OnHitLanded;
            if (_framesLeft > 0) EndHitstop(); // dọn nếu disable giữa hitstop (khôi phục state vật lý).
        }

        private void OnHitLanded(HitData hit)
        {
            GameObject me = root != null ? root.gameObject : gameObject;
            bool involved = ReferenceEquals(hit.Attacker, me) || ReferenceEquals(hit.Victim, me);
            if (!involved) return;

            // Đang chant/flash (CinematicGate, đồng nhất HitReactionController/CombatController/
            // SpecialController/DefenseController): KHÔNG freeze Animator.speed=0 — freeze này chỉ tự mở
            // khoá qua FixedUpdate đếm frame, mà FixedUpdate NGỪNG HẲN khi cinematic set Time.timeScale=0 →
            // Animator.speed kẹt ở 0 vĩnh viễn → clip cinematic không bao giờ tới event kết (SuperFlashEnd/
            // ChantEnd) → CinematicGate không bao giờ Release → đơ trong flash vĩnh viễn (hit trúng đúng lúc
            // đang ở flash/chant của chính mình hoặc đối thủ).
            if (CinematicGate.IsActive) return;

            // (P10) ULTIMATE miễn nhiễm hitstop (animation speed CỐ ĐỊNH, research §3.5). Nếu attacker
            // HOẶC victim đang Ultimate → BỎ QUA freeze (cả Animator.speed lẫn velocity-freeze). Null-safe.
            if (IsUltimateInvolved(hit)) return;

            int frames = Mathf.Max(0, hit.HitstopFrames);
            if (frames <= 0) return;

            if (_framesLeft <= 0) BeginHitstop();          // bắt đầu freeze (lưu state 1 lần).
            _framesLeft = Mathf.Max(_framesLeft, frames);  // hit chồng → restart/max, KHÔNG cộng dồn.
        }

        /// <summary>(P10) True nếu attacker HOẶC victim đang Ultimate (IBuffContext.IsUltimateActive) →
        /// HitstopController bỏ qua freeze (Ultimate "speed cố định"). Null-safe: root không có IBuffContext
        /// → false. ADD tối thiểu, KHÔNG đổi logic hitstop P5.</summary>
        private static bool IsUltimateInvolved(in HitData hit)
        {
            return HasUltimate(hit.Attacker) || HasUltimate(hit.Victim);
        }

        private static bool HasUltimate(GameObject go)
        {
            if (go == null) return false;
            var buff = go.GetComponent<IBuffContext>();
            return buff != null && buff.IsUltimateActive;
        }

        private void FixedUpdate()
        {
            if (_framesLeft <= 0) return;

            // Giữ "đóng băng" mỗi fixed-step (đề phòng physics/state khác set lại velocity giữa hitstop).
            if (body != null) body.linearVelocity = Vector2.zero;

            _framesLeft--;
            if (_framesLeft <= 0) EndHitstop();
        }

        private void BeginHitstop()
        {
            if (body != null)
            {
                _savedVelocity = body.linearVelocity;
                _savedGravityScale = body.gravityScale;
                body.linearVelocity = Vector2.zero;
                body.gravityScale = 0f;
            }
            if (animator != null)
            {
                _savedAnimatorSpeed = animator.speed;
                animator.speed = 0f;
            }
        }

        private void EndHitstop()
        {
            _framesLeft = 0;
            if (body != null)
            {
                body.linearVelocity = _savedVelocity;
                body.gravityScale = _savedGravityScale;
            }
            if (animator != null) animator.speed = _savedAnimatorSpeed;
        }

        // (P12 fix) RespawnSystem BroadcastMessage("OnRespawn"): chết ĐÚNG giữa hitstop → hủy freeze
        // nhưng KHÔNG hoàn velocity đã lưu (đó là đà của mạng trước — Motor.OnRespawn vừa zero).
        private void OnRespawn()
        {
            if (_framesLeft <= 0) return;
            _framesLeft = 0;
            if (body != null) body.gravityScale = _savedGravityScale;
            if (animator != null) animator.speed = _savedAnimatorSpeed;
        }
    }
}
