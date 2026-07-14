using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// VFX/SFX cho DI CHUYỂN (bụi nhảy, cột khí, đáp đất, dash, bước chân) — dựa trên CHUYỂN TIẾP
    /// TRẠNG THÁI chứ KHÔNG dựa Animation Event. Lý do (§ memory archer-movement-rework): các clip
    /// Jump/Fall/Dash DÙNG CHUNG cho cả đất lẫn không (wall-jump cũng set StateId=Jump), nên không thể
    /// gắn event "nhảy trên đất" lên clip — phải phân biệt đất/khí bằng CODE: poll Animator param
    /// (StateId + IsGrounded) mỗi frame, edge-detect, fire ĐÚNG 1 LẦN.
    ///
    /// Tự-chứa (KHÔNG dùng chung mảng effects[] của <see cref="CharacterEffectPlayer"/> — mảng đó bị các
    /// SetupTool khác ghi đè toàn bộ nên không bền để chèn slot movement). Chỉ đọc Animator param + lấy
    /// Facing qua interface Core <see cref="ICombatContext"/> → không reference module khác (§1.3).
    /// Logic spawn/lật-facing gương theo <see cref="CharacterEffectPlayer.OnPlayVfx"/> (bụi world-space).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementEffectController : MonoBehaviour
    {
        // StateId DÙNG CHUNG toàn dự án (xem memory fighter-stateid-map). Literal ở đây vì enum
        // CharacterStateId nằm trong FA.Movement — FA.Effects KHÔNG được ref module đó (§1.3).
        private const int IdIdle = 0, IdWalk = 1, IdJump = 2, IdFall = 3, IdDoubleJump = 4, IdDash = 5;

        [Header("Tham chiếu (trống = tự tìm trên object)")]
        [SerializeField] private Animator animator;
        [SerializeField] private AudioSource audioSource;

        [Header("Slot hiệu ứng di chuyển")]
        [Tooltip("Bụi bật nhảy TRÊN ĐẤT (fire khi Idle/Walk → Jump). Đặt ở chân.")]
        [SerializeField] private EffectData jumpGround;
        [Tooltip("Cột khí nhảy TRÊN KHÔNG / double-jump (fire khi vào DoubleJump). Đặt ở thân.")]
        [SerializeField] private EffectData jumpAir;
        [Tooltip("Bụi đáp đất (fire khi air → grounded). Đặt ở chân.")]
        [SerializeField] private EffectData land;
        [Tooltip("Bụi dash TRÊN ĐẤT (fire khi vào Dash lúc đang chạm đất). Đặt ở chân.")]
        [SerializeField] private EffectData dashGround;
        [Tooltip("Vòng khí dash TRÊN KHÔNG (fire khi vào Dash lúc trên không). Đặt ở thân.")]
        [SerializeField] private EffectData dashAir;
        [Tooltip("Bụi bước chân khi Walk (fire 2 lần/chu kỳ theo normalizedTime). Đặt ở chân.")]
        [SerializeField] private EffectData walkStep;

        [Header("Bước chân")]
        [Tooltip("Các mốc normalizedTime (0..1) trong chu kỳ Walk để phát bụi bước chân (2 chân chạm).")]
        [SerializeField] private float[] footContactPhases = { 0.15f, 0.65f };

        // Facing để lật offset/hình theo hướng nhìn (qua interface Core — không ref module khác §1.3).
        private ICombatContext _ctx;

        private int _prevState = -1;
        private bool _prevGrounded = true;
        private bool _hasLeftGround;   // đã thực sự rời đất ≥1 frame — chống land-VFX flicker khi lướt mép sàn.
        private int _lastFootPhase = -1;

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            _ctx = GetComponent<ICombatContext>();
        }

        private void OnEnable()
        {
            // Reset baseline theo trạng thái hiện tại → không fire nhầm ngay khi bật (respawn).
            if (animator == null) return;
            _prevState = animator.GetInteger(CoreAnimatorParams.StateId);
            _prevGrounded = animator.GetBool(CoreAnimatorParams.IsGrounded);
            _hasLeftGround = false;
            _lastFootPhase = -1;
        }

        private void Update()
        {
            if (animator == null) return;

            int state = animator.GetInteger(CoreAnimatorParams.StateId);
            bool grounded = animator.GetBool(CoreAnimatorParams.IsGrounded);

            if (!grounded) _hasLeftGround = true;

            // --- Chuyển tiếp state (edge) ---
            if (state != _prevState)
            {
                switch (state)
                {
                    case IdJump:
                        // Bụi bật đất CHỈ khi trước đó ở Idle/Walk (đứng đất) — wall-jump/double-jump cũng
                        // set StateId=Jump khi ĐANG BAY → không nổ bụi-đất giữa không trung (bẫy đã biết).
                        if (_prevState == IdIdle || _prevState == IdWalk)
                            Fire(jumpGround, atFeet: true);
                        break;
                    case IdDoubleJump:
                        Fire(jumpAir, atFeet: false);
                        break;
                    case IdDash:
                        Fire(grounded ? dashGround : dashAir, atFeet: grounded);
                        break;
                }
            }

            // --- Đáp đất (rising edge grounded) — cần đã thực sự rời đất trước đó ---
            if (grounded && !_prevGrounded && _hasLeftGround)
            {
                Fire(land, atFeet: true);
                _hasLeftGround = false;
            }

            // --- Bước chân khi Walk (theo normalizedTime, 2 lần/chu kỳ) ---
            if (state == IdWalk && grounded)
                TickFootsteps();
            else
                _lastFootPhase = -1;

            _prevState = state;
            _prevGrounded = grounded;
        }

        private void TickFootsteps()
        {
            if (walkStep == null || footContactPhases == null || footContactPhases.Length == 0) return;
            float t = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            float frac = t - Mathf.Floor(t);   // pha trong chu kỳ [0,1)

            // Tìm mốc chân gần nhất mà frac vừa vượt qua; fire 1 lần mỗi mốc (đổi index = qua mốc mới).
            int phase = -1;
            for (int i = 0; i < footContactPhases.Length; i++)
                if (frac >= footContactPhases[i]) phase = i;

            if (phase != _lastFootPhase && phase >= 0)
            {
                Fire(walkStep, atFeet: true);
                _lastFootPhase = phase;
            }
            else if (phase < 0)
            {
                // frac quấn vòng về đầu chu kỳ (< mốc đầu) → cho phép fire lại mốc 0 ở vòng kế.
                _lastFootPhase = -1;
            }
        }

        /// <summary>
        /// Spawn VFX + phát SFX của 1 slot. atFeet=true → đặt tại CHÂN (world-space, offset y≈0 vì pivot
        /// nhân vật BottomCenter → position đã ở chân). atFeet=false → cộng offset.y (thân). VFX movement
        /// LUÔN world-space (bụi phải Ở LẠI mặt đất khi nhân vật đi tiếp) → controller CỐ Ý bỏ qua
        /// EffectData.attachToOwner (khác CharacterEffectPlayer); mọi slot movement để attachToOwner=false.
        /// Lật x theo facing như CharacterEffectPlayer (bụi bất đối xứng ra đúng phía).
        /// </summary>
        private void Fire(EffectData fx, bool atFeet)
        {
            if (fx == null) return;

            int facing = _ctx != null ? _ctx.Facing : (transform.lossyScale.x >= 0f ? 1 : -1);
            float ownerScale = Mathf.Abs(transform.lossyScale.y);   // root phóng ×N → offset/scale theo đó.

            if (fx.vfxPrefab != null)
            {
                Vector3 pos = transform.position +
                              new Vector3(fx.offset.x * facing * ownerScale, fx.offset.y * ownerScale, 0f);
                GameObject vfx = Instantiate(fx.vfxPrefab, pos, fx.vfxPrefab.transform.rotation);
                Vector3 s = vfx.transform.localScale * fx.scale * ownerScale;
                s.x = Mathf.Abs(s.x) * facing;   // world-space → tự lật theo hướng nhìn.
                vfx.transform.localScale = s;
            }

            if (audioSource != null)
            {
                AudioClip clip = fx.PickClip();
                if (clip != null) audioSource.PlayOneShot(clip, fx.volume);
            }
        }
    }
}
