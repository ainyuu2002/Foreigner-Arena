using UnityEngine;

namespace FA.Movement
{
    /// <summary>
    /// Tên + hash tham số Animator dùng chung cho mọi MovementState. Tập trung 1 chỗ để
    /// tên KHỚP đúng với parameter khai trong AnimatorController (research §3.8). Dùng hash
    /// (Animator.StringToHash) cho hiệu năng. Code chỉ SET tham số; transition do cửa sổ Animator.
    /// </summary>
    public static class MovementAnimatorParams
    {
        /// <summary>Tên parameter int trong AnimatorController. Phải khớp đúng chuỗi này.</summary>
        public const string StateIdName = "StateId";

        /// <summary>Hash của "StateId" — truyền vào Animator.SetInteger(...).</summary>
        public static readonly int StateId = Animator.StringToHash(StateIdName);

        // --- Phase 2: param PHỤ TRỢ blend (set-only mỗi frame, KHÔNG thay StateId quyết logic) ---

        /// <summary>Tên parameter float "VelY" (vận tốc dọc) — cho Blend Tree Up/Apex/Down nếu dùng.</summary>
        public const string VelYName = "VelY";

        /// <summary>Hash của "VelY" — Animator.SetFloat(...). Phụ trợ blend rơi, không quyết logic.</summary>
        public static readonly int VelY = Animator.StringToHash(VelYName);

        /// <summary>Tên parameter bool "IsGrounded" (optional) — tiện transition "Any → Idle/Walk khi đáp".</summary>
        public const string IsGroundedName = "IsGrounded";

        /// <summary>Hash của "IsGrounded" — Animator.SetBool(...). Optional, set-only.</summary>
        public static readonly int IsGrounded = Animator.StringToHash(IsGroundedName);
    }
}
