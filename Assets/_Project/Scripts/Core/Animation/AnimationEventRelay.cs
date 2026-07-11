using System;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// CẦU NỐI DUY NHẤT Unity-native ↔ code cho toàn bộ frame data (§1.1 "quy tắc vàng", §3.5).
    /// MonoBehaviour đặt CÙNG GameObject với Animator (Unity gọi Animation Event theo tên method
    /// trên component cùng object). Mọi clip CHỈ gọi các tên method chuẩn hóa dưới đây → 1 nguồn sự
    /// thật cho frame data; designer chỉnh timing trên CLIP, code chỉ NHẬN (KHÔNG đếm frame).
    ///
    /// Mỗi sự kiện raise theo 2 đường (research §3.5 — tránh "2 player chung 1 kênh hitbox"):
    ///  (a) C# event CỤC BỘ (per-instance) → component cùng prefab (P5 HitboxAuthoring) subscribe trực
    ///      tiếp → đúng "nhân vật này". Đây là đường dùng cho HITBOX/RECOVERY/CANCEL (per-character).
    ///  (b) VoidEventChannelSO GLOBAL (optional, gán Inspector) → VFX/Audio/UI subscribe — dùng cho
    ///      Footstep/SpawnProjectile (sự kiện toàn cục, không phụ thuộc "ai").
    ///
    /// Danh sách tên method CỐ ĐỊNH (research §5): HitboxOn, HitboxOff, RecoveryEnd, CancelWindowOpen,
    /// CancelWindowClose, Footstep, SpawnProjectile, HurtboxOff, HurtboxOn,
    /// MoveImpulse, MoveDriftOn, MoveDriftOff (P14 — di chuyển trong đòn),
    /// KnockbackPoint (P16 — frame hit-văng của đòn; chỉ frame có event này mới văng knockback),
    /// ChargeCheck (đòn giữ-để-charge: đặt tại frame "ngắm" — còn giữ nút thì AttackState swap sang
    /// MoveData.chargedVariant),
    /// PassThroughOn, PassThroughOff (cửa sổ đòn XUYÊN THÂN đối thủ — tắt pushbox P1↔P2),
    /// PlayVfx, PlaySfx (VFX/SFX — int = chỉ số slot EffectData trên CharacterEffectPlayer),
    /// SuperFlashStart, SuperFlashEnd (cinematic super: freeze + zoom + tối màn + cut-in),
    /// ChantStart, ChantLine, ChantEnd (cinematic niệm chú ultimate: freeze + tối nhẹ + zoom 1/2 +
    /// dòng chữ theo int), UltimateInstall (kích install-mode ultimate — UBW đổi bg/buff). Thêm tên
    /// mới = thêm 1 method ở đây + (tùy) 1 kênh. Animation Event gọi tên KHÔNG có ở đây → Unity log
    /// warning → giữ danh sách này là duy nhất.
    /// </summary>
    public sealed class AnimationEventRelay : MonoBehaviour
    {
        [Header("Kênh GLOBAL (optional — VFX/Audio/UI). Để trống nếu chỉ cần C# event cục bộ.")]
        [Tooltip("Raise khi HitboxOn. Per-instance hitbox nên dùng C# event Action thay vì kênh này.")]
        [SerializeField] private VoidEventChannelSO hitboxOnChannel;
        [SerializeField] private VoidEventChannelSO hitboxOffChannel;
        [SerializeField] private VoidEventChannelSO recoveryEndChannel;
        [SerializeField] private VoidEventChannelSO cancelWindowOpenChannel;
        [SerializeField] private VoidEventChannelSO cancelWindowCloseChannel;
        [SerializeField] private VoidEventChannelSO footstepChannel;
        [SerializeField] private VoidEventChannelSO spawnProjectileChannel;
        [Tooltip("(P10) Raise khi HurtboxOff/On (i-frame né). Per-instance nên dùng C# event thay kênh này.")]
        [SerializeField] private VoidEventChannelSO hurtboxOffChannel;
        [SerializeField] private VoidEventChannelSO hurtboxOnChannel;

        // ===== C# event CỤC BỘ (per-instance) — P5+ subscribe trực tiếp trên prefab này =====
        /// <summary>Bật hitbox (đầu cửa sổ active). Tham số int = chỉ số hitbox (0 nếu không truyền).</summary>
        public event Action<int> HitboxOnEvent;
        /// <summary>Tắt hitbox (cuối cửa sổ active). int = chỉ số hitbox.</summary>
        public event Action<int> HitboxOffEvent;
        /// <summary>Hết recovery → mở lại input (P5 AttackState về Idle).</summary>
        public event Action RecoveryEndEvent;
        /// <summary>Mở cửa sổ cancel/combo (P7).</summary>
        public event Action CancelWindowOpenEvent;
        /// <summary>Đóng cửa sổ cancel/combo (P7).</summary>
        public event Action CancelWindowCloseEvent;
        /// <summary>Bước chân (P4 verify pipeline; P11 audio/dust).</summary>
        public event Action FootstepEvent;
        /// <summary>Spawn projectile (P9). int = id biến thể nếu cần.</summary>
        public event Action<int> SpawnProjectileEvent;
        /// <summary>(P10) TẮT hurtbox = bắt đầu cửa sổ i-frame né (HurtboxController disable collider).</summary>
        public event Action HurtboxOffEvent;
        /// <summary>(P10) BẬT lại hurtbox = kết thúc cửa sổ i-frame né.</summary>
        public event Action HurtboxOnEvent;
        /// <summary>(P14) Áp 1 CÚ ĐẨY tức thời (impulse) trong đòn. int = chỉ số trong MoveData.motionImpulses
        /// (0 nếu clip không truyền). MoveMotionController set linearVelocity theo facing.</summary>
        public event Action<int> MoveImpulseEvent;
        /// <summary>(P14) MỞ cửa sổ drift (vx sustained = facing×driftSpeed mỗi FixedTick tới khi Off).</summary>
        public event Action MoveDriftOnEvent;
        /// <summary>(P14) ĐÓNG cửa sổ drift (ngừng gán vx — trượt dừng dứt khoát).</summary>
        public event Action MoveDriftOffEvent;
        /// <summary>(P16) FRAME HIT-VĂNG của đòn (đặt trên hit CUỐI). Chỉ frame có event này mới quyết
        /// văng knockback: attacker nghe → nếu KHÔNG đang nối thì raise OnKnockbackLaunch cho victim áp
        /// pending. Hit thường (không có event) chỉ tích %/hitstun. KHÔNG đếm/đoán "hit cuối" trong code.</summary>
        public event Action KnockbackPointEvent;
        /// <summary>Frame QUYẾT CHARGE của đòn giữ-để-charge (đặt tại frame "ngắm" trước khi bắn/vung).
        /// AttackState nghe → còn GIỮ nút đủ lâu thì BeginMove(MoveData.chargedVariant) (swap sang clip
        /// charged). Đòn không có chargedVariant → event vô hại (không ai xử lý).</summary>
        public event Action ChargeCheckEvent;
        /// <summary>MỞ cửa sổ XUYÊN THÂN đối thủ (đòn lướt xuyên người): KnockbackController tắt pushbox
        /// P1↔P2 (IgnoreCollision) tới khi PassThroughOff. Chỉ tắt va chạm THÂN — Hitbox/Hurtbox không đổi.</summary>
        public event Action PassThroughOnEvent;
        /// <summary>ĐÓNG cửa sổ xuyên thân — trả lại pushbox (trừ khi đang tumble, P7 vẫn giữ tắt).</summary>
        public event Action PassThroughOffEvent;
        /// <summary>(VFX) Spawn hiệu ứng HÌNH theo chỉ số slot EffectData (CharacterEffectPlayer subscribe).
        /// Đặt trên clip tại frame muốn hiệu ứng xuất hiện (aura, flash, glow...).</summary>
        public event Action<int> PlayVfxEvent;
        /// <summary>(SFX) Phát ÂM THANH theo chỉ số slot EffectData (CharacterEffectPlayer subscribe).
        /// Đặt trên clip tại frame muốn nghe (vung đòn, hét, kích chiêu...).</summary>
        public event Action<int> PlaySfxEvent;
        /// <summary>(Cinematic) MỞ super flash: freeze toàn cục + zoom caster + tối màn + cut-in
        /// (SuperFlashController subscribe). Đặt ở frame đầu clip super; caster chạy tiếp bằng
        /// unscaled time nên các event sau vẫn bắn. int = chỉ số portrait trên SuperFlashController
        /// (mỗi super 1 ảnh cut-in riêng; không điền Int → 0 = portrait mặc định).</summary>
        public event Action<int> SuperFlashStartEvent;
        /// <summary>(Cinematic) ĐÓNG super flash — trả timeScale/camera/overlay về như cũ. Đặt tại
        /// frame kết thúc đoạn trình diễn (trước phần đòn thật).</summary>
        public event Action SuperFlashEndEvent;
        /// <summary>(Ultimate) MỞ đoạn NIỆM CHÚ: freeze + tối NHẸ màn + zoom 1/2 (ChantController).</summary>
        public event Action ChantStartEvent;
        /// <summary>(Ultimate) Hiện DÒNG CHỮ niệm chú thứ int (ChantTextOverlay — lines khai trên component).</summary>
        public event Action<int> ChantLineEvent;
        /// <summary>(Ultimate) ĐÓNG đoạn niệm chú — trả freeze/dim/zoom về như cũ.</summary>
        public event Action ChantEndEvent;
        /// <summary>(Ultimate) KÍCH install-mode (UBW: đổi background + buff + drain nộ — UBWInstallController).</summary>
        public event Action UltimateInstallEvent;

        // ===================== Method được Animation Event gọi =====================
        // Mỗi method CHỈ raise (decoupling §3.3). KHÔNG đếm frame, KHÔNG logic gameplay.

        // ⚠️ KHÔNG ĐƯỢC OVERLOAD tên method Animation Event: Unity dispatch theo TÊN, gặp 2 overload thì
        // chọn KHÔNG XÁC ĐỊNH (có thể đổi giữa các lần compile) → từng gọi nhầm bản () làm HitboxOn(2)
        // trên clip bị nhận thành index 0. MỖI TÊN = MỘT method duy nhất nhận int; event không điền Int
        // vẫn chạy đúng (Unity truyền intParameter mặc định 0).

        /// <summary>Animation Event: bật hitbox theo chỉ số (clip không điền Int → Unity truyền 0).</summary>
        public void HitboxOn(int index)
        {
            HitboxOnEvent?.Invoke(index);
            if (hitboxOnChannel != null) hitboxOnChannel.RaiseEvent();
        }

        /// <summary>Animation Event: tắt hitbox theo chỉ số (clip không điền Int → Unity truyền 0).</summary>
        public void HitboxOff(int index)
        {
            HitboxOffEvent?.Invoke(index);
            if (hitboxOffChannel != null) hitboxOffChannel.RaiseEvent();
        }

        /// <summary>Animation Event: hết recovery.</summary>
        public void RecoveryEnd()
        {
            RecoveryEndEvent?.Invoke();
            if (recoveryEndChannel != null) recoveryEndChannel.RaiseEvent();
        }

        /// <summary>Animation Event: mở cửa sổ cancel.</summary>
        public void CancelWindowOpen()
        {
            CancelWindowOpenEvent?.Invoke();
            if (cancelWindowOpenChannel != null) cancelWindowOpenChannel.RaiseEvent();
        }

        /// <summary>Animation Event: đóng cửa sổ cancel.</summary>
        public void CancelWindowClose()
        {
            CancelWindowCloseEvent?.Invoke();
            if (cancelWindowCloseChannel != null) cancelWindowCloseChannel.RaiseEvent();
        }

        /// <summary>Animation Event: bước chân (P4 verify; đặt thử trên clip Walk).</summary>
        public void Footstep()
        {
            FootstepEvent?.Invoke();
            if (footstepChannel != null) footstepChannel.RaiseEvent();
        }

        /// <summary>Animation Event: spawn projectile theo id biến thể (không điền Int → 0). KHÔNG overload.</summary>
        public void SpawnProjectile(int variantId)
        {
            SpawnProjectileEvent?.Invoke(variantId);
            if (spawnProjectileChannel != null) spawnProjectileChannel.RaiseEvent();
        }

        /// <summary>(P10) Animation Event: TẮT hurtbox (đầu cửa sổ né — i-frame Unity-native, §3.1).</summary>
        public void HurtboxOff()
        {
            HurtboxOffEvent?.Invoke();
            if (hurtboxOffChannel != null) hurtboxOffChannel.RaiseEvent();
        }

        /// <summary>(P10) Animation Event: BẬT lại hurtbox (cuối cửa sổ né).</summary>
        public void HurtboxOn()
        {
            HurtboxOnEvent?.Invoke();
            if (hurtboxOnChannel != null) hurtboxOnChannel.RaiseEvent();
        }

        /// <summary>(P14) Animation Event: cú đẩy tức thời theo chỉ số (không điền Int → 0). KHÔNG overload.</summary>
        public void MoveImpulse(int index) => MoveImpulseEvent?.Invoke(index);

        /// <summary>(P14) Animation Event: mở cửa sổ drift.</summary>
        public void MoveDriftOn() => MoveDriftOnEvent?.Invoke();

        /// <summary>(P14) Animation Event: đóng cửa sổ drift.</summary>
        public void MoveDriftOff() => MoveDriftOffEvent?.Invoke();

        /// <summary>(P16) Animation Event: FRAME hit-văng (đặt trên hit CUỐI của đòn). Attacker state
        /// nghe → xét nối; không nối thì raise OnKnockbackLaunch để victim văng pending.</summary>
        public void KnockbackPoint() => KnockbackPointEvent?.Invoke();

        /// <summary>Animation Event: frame quyết charge (đòn giữ-để-charge — còn giữ nút → biến thể mạnh).</summary>
        public void ChargeCheck() => ChargeCheckEvent?.Invoke();

        /// <summary>Animation Event: MỞ cửa sổ xuyên thân đối thủ (đặt bao khoảng lướt của đòn).</summary>
        public void PassThroughOn() => PassThroughOnEvent?.Invoke();

        /// <summary>Animation Event: ĐÓNG cửa sổ xuyên thân (trả pushbox).</summary>
        public void PassThroughOff() => PassThroughOffEvent?.Invoke();

        /// <summary>Animation Event: spawn VFX theo slot (không điền Int → 0). KHÔNG overload.</summary>
        public void PlayVfx(int index) => PlayVfxEvent?.Invoke(index);

        /// <summary>Animation Event: phát SFX theo slot (không điền Int → 0). KHÔNG overload.</summary>
        public void PlaySfx(int index) => PlaySfxEvent?.Invoke(index);

        /// <summary>Animation Event: MỞ super flash cinematic (frame đầu clip super). Int = chỉ số
        /// portrait cut-in trên SuperFlashController (không điền → 0). KHÔNG overload.</summary>
        public void SuperFlashStart(int portraitIndex) => SuperFlashStartEvent?.Invoke(portraitIndex);

        /// <summary>Animation Event: ĐÓNG super flash cinematic (hết đoạn trình diễn).</summary>
        public void SuperFlashEnd() => SuperFlashEndEvent?.Invoke();

        /// <summary>Animation Event: MỞ đoạn niệm chú ultimate (frame đầu clip chant).</summary>
        public void ChantStart() => ChantStartEvent?.Invoke();

        /// <summary>Animation Event: hiện dòng chữ niệm chú theo Int (0-based). KHÔNG overload.</summary>
        public void ChantLine(int index) => ChantLineEvent?.Invoke(index);

        /// <summary>Animation Event: ĐÓNG đoạn niệm chú (trả freeze/dim/zoom).</summary>
        public void ChantEnd() => ChantEndEvent?.Invoke();

        /// <summary>Animation Event: KÍCH install-mode ultimate (sau flash — đổi bg/buff/drain nộ).</summary>
        public void UltimateInstall() => UltimateInstallEvent?.Invoke();
    }
}
