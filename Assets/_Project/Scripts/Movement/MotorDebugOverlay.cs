using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Overlay debug TÙY CHỌN cho Test_Phase01: in state FSM hiện tại + MoveX + IsGrounded
    /// lên góc màn hình (OnGUI) để verify bằng mắt (research §E). Chỉ là công cụ test —
    /// không ảnh hưởng gameplay; gỡ component khỏi prefab khi build thật.
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class MotorDebugOverlay : MonoBehaviour
    {
        [SerializeField] private CharacterMotor motor;
        [SerializeField] private GroundCheck groundCheck;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
            if (groundCheck == null) groundCheck = GetComponent<GroundCheck>();
        }

        private void OnGUI()
        {
            if (motor == null) return;

            IState cur = motor.Machine.CurrentState;
            string stateName = cur != null ? cur.GetType().Name : "<none>";
            string grounded = groundCheck != null ? groundCheck.IsGrounded.ToString() : "n/a";

            float velY = motor.Body != null ? motor.Body.linearVelocity.y : 0f;
            float gScale = motor.Body != null ? motor.Body.gravityScale : 0f;

            GUI.Label(new Rect(10, 10, 400, 20), $"State: {stateName}");
            GUI.Label(new Rect(10, 30, 400, 20), $"MoveX: {motor.MoveX:0.00}  MoveY: {motor.MoveY:0.00}");
            GUI.Label(new Rect(10, 50, 400, 20), $"Grounded: {grounded}  AirJumps: {motor.AirJumpsRemaining}");
            GUI.Label(new Rect(10, 70, 400, 20), $"VelY: {velY:0.00}  gravityScale: {gScale:0.00}");
            GUI.Label(new Rect(10, 90, 400, 20),
                $"Facing: {motor.Facing}  Walled: {motor.IsWalled}  WallSide: {motor.WallSide}");
        }
    }
}
