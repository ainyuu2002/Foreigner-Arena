using System.Text;
using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// Overlay debug cho Test_Phase04 (research §E): in nội dung InputBuffer + kết quả CommandParser
    /// (motion QCF/QCB/DP, tap/hold ↓, edge nút) để VERIFY bằng mắt. Chỉ là công cụ test — không ảnh
    /// hưởng gameplay; gỡ khỏi prefab khi build thật. Đọc qua DebugBuffer/DebugParser của Motor
    /// (KHÔNG chạm Input System / field input nội bộ).
    ///
    /// Footstep verify: gán FootstepChannel (VoidEventChannelSO) → overlay đếm + hiện "Footstep #n"
    /// khi Animation Event Footstep bắn qua relay → channel (chứng minh pipeline frame-data).
    /// </summary>
    [RequireComponent(typeof(CharacterMotor))]
    public sealed class InputDebugOverlay : MonoBehaviour
    {
        [SerializeField] private CharacterMotor motor;

        [Header("Footstep verify (gán cùng channel đã gán vào AnimationEventRelay)")]
        [SerializeField] private VoidEventChannelSO footstepChannel;

        [Header("Vị trí overlay (đặt lệch nếu có 2 player)")]
        [SerializeField] private float originX = 10f;
        [SerializeField] private float originY = 120f;

        private int _footstepCount;
        private float _lastFootstepTime = -99f;
        private readonly StringBuilder _sb = new StringBuilder(128);

        private void Awake()
        {
            if (motor == null) motor = GetComponent<CharacterMotor>();
        }

        private void OnEnable()
        {
            if (footstepChannel != null) footstepChannel.OnEventRaised += OnFootstep;
        }

        private void OnDisable()
        {
            if (footstepChannel != null) footstepChannel.OnEventRaised -= OnFootstep;
        }

        private void OnFootstep()
        {
            _footstepCount++;
            _lastFootstepTime = Time.time;
            DebugLog.Log($"[P4] Footstep relay #{_footstepCount} @ frame {Time.frameCount}");
        }

        private void OnGUI()
        {
            if (motor == null) return;
            InputBuffer buf = motor.DebugBuffer;
            CommandParser parser = motor.DebugParser;
            if (buf == null || parser == null) return;

            int facing = motor.Facing;
            float y = originY;

            // Motion parse (tương-đối-facing) — DP ưu tiên trước QCF.
            MotionInput motion = parser.DetectMotion(motor.MotionWindowFrames, facing);
            string motionTxt = motion == MotionInput.None ? "-" : motion.ToString() + " detected";
            GUI.Label(new Rect(originX, y, 460, 20), $"[Motion] {motionTxt}  (facing {facing})"); y += 20;

            // Tap/hold ↓ (Né vs Ngồi).
            HoldKind hold = parser.GetDirectionHold(InputDirection.Down, facing, motor.TapMaxHoldFrames, out int heldFrames);
            GUI.Label(new Rect(originX, y, 460, 20), $"[Down hold] {hold}  ({heldFrames}f)"); y += 20;

            // Edge các nút (buffer-tha-thứ).
            _sb.Clear();
            _sb.Append("[Edge] ");
            AppendEdge(parser, InputButton.Jump, "J");
            AppendEdge(parser, InputButton.Dash, "Dsh");
            AppendEdge(parser, InputButton.L, "L");
            AppendEdge(parser, InputButton.M, "M");
            AppendEdge(parser, InputButton.H, "H");
            AppendEdge(parser, InputButton.Super1, "S1");
            AppendEdge(parser, InputButton.Super2, "S2");
            AppendEdge(parser, InputButton.Rage, "R");
            GUI.Label(new Rect(originX, y, 460, 20), _sb.ToString()); y += 20;

            // Dòng buffer (numpad-dir mới→cũ) để thấy chuỗi hướng.
            _sb.Clear();
            _sb.Append("[Buf dir] ");
            int show = Mathf.Min(buf.Count, buf.Capacity);
            for (int ago = 0; ago < show; ago++) _sb.Append(buf.Peek(ago).NumpadDir);
            GUI.Label(new Rect(originX, y, 460, 20), _sb.ToString()); y += 20;

            // Footstep verify.
            bool recent = Time.time - _lastFootstepTime < 0.25f;
            GUI.Label(new Rect(originX, y, 460, 20),
                $"[Footstep] count={_footstepCount} {(recent ? "<<< now" : "")}");
        }

        private void AppendEdge(CommandParser parser, InputButton b, string label)
        {
            // 4 là window đọc nhanh chỉ để hiển thị (không tiêu thụ — overlay là read-only).
            if (parser.WasPressedWithin(b, 4)) { _sb.Append(label); _sb.Append(' '); }
        }
    }
}
