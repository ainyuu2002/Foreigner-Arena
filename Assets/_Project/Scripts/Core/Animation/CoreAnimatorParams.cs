using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Tham số Animator DÙNG CHUNG nhiều module (cầu nối code↔Animator §3.1). Đặt ở Core vì cả
    /// FA.Movement (Idle/Walk/Jump...) lẫn FA.Combat (Attack_L/M/H) cùng GHI vào param "StateId" của
    /// CÙNG một Animator → tên param là HỢP ĐỒNG chung, không thuộc riêng module nào.
    ///
    /// MovementAnimatorParams (FA.Movement) vẫn giữ tên "StateId" RIÊNG cho tiện dùng nội bộ Movement
    /// (giá trị chuỗi trùng đúng) — KHÔNG đổi để tránh hồi quy. Module nào chỉ ref Core (vd Combat) thì
    /// dùng hằng này. Code chỉ SET param; transition cấu hình TRONG cửa sổ Animator (Unity-native).
    /// </summary>
    public static class CoreAnimatorParams
    {
        /// <summary>Tên param int "StateId" — phải KHỚP đúng param khai trong AnimatorController.</summary>
        public const string StateIdName = "StateId";

        /// <summary>Hash của "StateId" — truyền vào Animator.SetInteger(...).</summary>
        public static readonly int StateId = Animator.StringToHash(StateIdName);

        /// <summary>Tên param bool "IsGrounded" — CharacterMotor (FA.Movement) GHI mỗi frame; module chỉ
        /// ref Core (vd FA.Effects MovementEffectController) ĐỌC để phân biệt đất/khí. Hợp đồng liên-module
        /// nên đặt ở Core (giá trị chuỗi trùng đúng MovementAnimatorParams.IsGroundedName — KHÔNG đổi bên đó).</summary>
        public const string IsGroundedName = "IsGrounded";

        /// <summary>Hash của "IsGrounded" — truyền vào Animator.GetBool(...).</summary>
        public static readonly int IsGrounded = Animator.StringToHash(IsGroundedName);

        /// <summary>Tên param int "HurtVariant" — chọn 1 trong N clip Hurt (P6). CHỈ điều khiển HÌNH ẢNH
        /// (variant biểu cảm trúng đòn), KHÔNG ảnh hưởng logic/hitbox/frame → dùng được Random không phá
        /// determinism gameplay. Animator: nhiều state Hurt_0/1/2 cùng StateId==11, lọc thêm HurtVariant.</summary>
        public const string HurtVariantName = "HurtVariant";

        /// <summary>Hash của "HurtVariant".</summary>
        public static readonly int HurtVariant = Animator.StringToHash(HurtVariantName);
    }
}
