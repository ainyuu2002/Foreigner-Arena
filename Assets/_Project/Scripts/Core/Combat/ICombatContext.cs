using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Hợp đồng NHỎ cho lớp Combat (P5+) truy cập state machine / facing / input của nhân vật mà
    /// KHÔNG reference FA.Movement (§1.3 — FA.Combat chỉ reference FA.Core). CharacterMotor (Movement)
    /// implement THÊM interface này (đã có sẵn mọi field — chỉ thêm vài getter, KHÔNG sửa logic).
    /// CombatController/AttackState GetComponent&lt;ICombatContext&gt;() → phụ thuộc Core interface,
    /// KHÔNG class Movement.
    ///
    /// Tương tự ICharacterContext (Movement) nhưng CHỈ những gì Combat cần: machine để ChangeState,
    /// body để zero vx khi đánh đứng, animator để set StateId đòn, parser để đọc nút L/M/H qua buffer,
    /// edge-window, grounded + facing để gate entry, và 2 accessor state-về (Idle/Walk) sau recovery.
    /// IState/StateMachine/CommandParser đều ở Core nên dùng được từ đây.
    /// </summary>
    public interface ICombatContext
    {
        /// <summary>State machine của nhân vật — AttackState ChangeState về Idle/Walk khi RecoveryEnd.</summary>
        StateMachine Machine { get; }

        /// <summary>Thân vật lý — AttackState zero vx mặt đất (đòn đứng tại chỗ); hitstop freeze velocity.</summary>
        Rigidbody2D Body { get; }

        /// <summary>Animator — AttackState set int StateId = MoveData.animatorStateId (cầu nối code↔Animator).</summary>
        Animator Animator { get; }

        /// <summary>Command Parser (đọc TỪ buffer) — CombatController hỏi WasPressedWithin(L/M/H).</summary>
        CommandParser Parser { get; }

        /// <summary>Cửa sổ frame để nhận edge nút (= InputConfigSO.edgeWindow). Dùng cho WasPressedWithin/ConsumePress.</summary>
        int EdgeWindow { get; }

        /// <summary>Đang chạm sàn không — gate "chỉ đánh khi đứng đất" ở P5 (aerial = P7).</summary>
        bool IsGrounded { get; }

        /// <summary>Hướng nhìn hiện tại: +1 phải, -1 trái. Dùng cho FacingSign trong HitData.</summary>
        int Facing { get; }

        /// <summary>Đang Ở TRẠNG THÁI BỊ ĐÁNH/JUGGLE (hitstun / tumble / wall-splat / ground-bounce). Các
        /// hurt-state của FA.Combat set cờ này (true ở Enter, false ở Exit) qua <see cref="SetHitstun"/>.
        /// FA.Specials đọc để CHẶN Air Dodge khi đang bị juggle (chỉ Burst mới thoát combo — quyết định
        /// người dùng), KHÔNG cần reference FA.Combat (§1.3).</summary>
        bool IsInHitstun { get; }

        /// <summary>Hurt-state (FA.Combat) báo vào/ra trạng thái bị-đánh: true ở Enter, false ở Exit.
        /// Additive — không đụng logic di chuyển/đòn đã có.</summary>
        void SetHitstun(bool value);

        /// <summary>Ý định di chuyển ngang (analog) — AttackState chọn về Idle hay Walk sau recovery.</summary>
        float MoveX { get; }

        /// <summary>Ý định di chuyển dọc (analog) — BlockState/CombatController phân biệt block-Low
        /// (giữ Back + Down) vs block-High (P6). Motor đã có MoveY public; chỉ expose qua interface.</summary>
        float MoveY { get; }

        /// <summary>Lật hướng nhìn (gom 1 chỗ ở Motor). dir &lt; 0 trái, &gt; 0 phải, 0 giữ.</summary>
        void SetFacing(int dir);

        /// <summary>State Idle dùng lại — AttackState ChangeState về đây sau recovery nếu đứng yên.</summary>
        IState IdleState();

        /// <summary>State Walk dùng lại — AttackState ChangeState về đây sau recovery nếu đang ép MoveX.</summary>
        IState WalkState();
    }
}
