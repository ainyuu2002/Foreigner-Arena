namespace FA.Core
{
    /// <summary>
    /// Nhận diện ý định input (Phase 4, §3.2). Đọc TUYỆT ĐỐI chỉ TỪ <see cref="InputBuffer"/> —
    /// KHÔNG chạm Input System (yêu cầu cứng §3.2). Class thuần, 1 cặp/Motor.
    ///
    /// Cung cấp 3 nhóm truy vấn cho gameplay code (Motor P4, special P9, dodge P10):
    ///  - TryMatchMotion: QCF/QCB/DP **lenient** (bỏ hướng thừa, cho thiếu hướng chéo) — tương-đối-facing.
    ///  - GetDirectionHold: tap-vs-hold 1 hướng (Né-tap ↓ vs Ngồi-hold ↓, GDD §4.1).
    ///  - WasPressedWithin: suy EDGE từ buffer (held now &amp; !held prev) trong window, có cờ CONSUMED
    ///    để 1 lần nhấn không kích 2 lần qua window (research §4 "Edge double-fire").
    ///
    /// "Forward/back" theo facing (±1) do CALLER truyền — special thuận tay 2 phía. Số window
    /// (motionWindow/tapMaxHold) do caller truyền (đến từ ScriptableObject, KHÔNG hardcode ở đây).
    /// </summary>
    public sealed class CommandParser
    {
        private readonly InputBuffer _buffer;

        // Cờ CONSUMED cho edge mỗi nút: lưu Frame của lần nhấn đã được tiêu thụ. Lần nhấn mới
        // (edge ở frame khác) mới kích lại → giữ ngữ nghĩa "1 lần nhấn = 1 lần kích" của Motor cũ.
        private readonly int[] _consumedEdgeFrame;

        // Cờ CONSUMED cho RELEASE edge (held→nhả) mỗi nút — đối xứng với edge nhấn. Dùng cho charge
        // "giữ→nhả" (P9): release-edge kích biến thể charged đúng 1 lần.
        private readonly int[] _consumedReleaseFrame;

        public CommandParser(InputBuffer buffer)
        {
            _buffer = buffer;
            _consumedEdgeFrame = new int[8]; // 8 InputButton
            _consumedReleaseFrame = new int[8];
            for (int i = 0; i < _consumedEdgeFrame.Length; i++)
            {
                _consumedEdgeFrame[i] = int.MinValue;
                _consumedReleaseFrame[i] = int.MinValue;
            }
        }

        // ===================== EDGE (WasPressedWithin) =====================

        /// <summary>
        /// True nếu nút b vừa CHUYỂN từ nhả → giữ (edge) trong <paramref name="frames"/> snapshot
        /// gần nhất VÀ edge đó chưa bị tiêu thụ. KHÔNG tự đánh dấu — caller gọi
        /// <see cref="ConsumePress"/> khi thực sự dùng (giữ "tiêu thụ 1 lần" như Motor cũ).
        /// </summary>
        public bool WasPressedWithin(InputButton b, int frames)
        {
            int edgeFrame = FindEdgeFrame(b, frames);
            if (edgeFrame == int.MinValue) return false;
            return _consumedEdgeFrame[(int)b] != edgeFrame;
        }

        /// <summary>
        /// Đánh dấu edge mới nhất (trong window) của nút b ĐÃ tiêu thụ → WasPressedWithin trả false
        /// tới khi có lần nhấn MỚI. Thay cho "_jumpPressed=false" của Motor P3.
        /// </summary>
        public void ConsumePress(InputButton b, int frames)
        {
            int edgeFrame = FindEdgeFrame(b, frames);
            if (edgeFrame != int.MinValue) _consumedEdgeFrame[(int)b] = edgeFrame;
        }

        /// <summary>Frame của edge (nhả→giữ) mới nhất trong window, hoặc int.MinValue nếu không có.</summary>
        private int FindEdgeFrame(InputButton b, int frames)
        {
            int count = _buffer.Count;
            if (count < 2) return int.MinValue;
            int limit = frames < count ? frames : count - 1; // cần snapshot "prev" → tối đa count-1
            for (int ago = 0; ago < limit; ago++)
            {
                bool now = _buffer.Peek(ago).IsHeld(b);
                bool prev = _buffer.Peek(ago + 1).IsHeld(b);
                if (now && !prev) return _buffer.Peek(ago).Frame; // edge tại snapshot 'ago'
            }
            return int.MinValue;
        }

        // ===================== RELEASE EDGE (charge giữ→nhả, P9) =====================

        /// <summary>
        /// True nếu nút <paramref name="b"/> vừa CHUYỂN từ giữ → nhả (release edge) trong
        /// <paramref name="frames"/> snapshot gần nhất VÀ release đó chưa bị tiêu thụ. Dùng cho charge
        /// "giữ→nhả" (P9): SpecialController kích biến thể charged khi NHẢ nút sau khi giữ đủ lâu.
        /// KHÔNG tự đánh dấu — caller gọi <see cref="ConsumeRelease"/> khi thực dùng (1 lần nhả = 1 kích).
        /// </summary>
        public bool WasReleasedWithin(InputButton b, int frames)
        {
            int rf = FindReleaseFrame(b, frames);
            if (rf == int.MinValue) return false;
            return _consumedReleaseFrame[(int)b] != rf;
        }

        /// <summary>Đánh dấu release edge mới nhất (trong window) ĐÃ tiêu thụ (đối xứng <see cref="ConsumePress"/>).</summary>
        public void ConsumeRelease(InputButton b, int frames)
        {
            int rf = FindReleaseFrame(b, frames);
            if (rf != int.MinValue) _consumedReleaseFrame[(int)b] = rf;
        }

        /// <summary>
        /// Số snapshot LIÊN TỤC nút <paramref name="b"/> đã GIỮ NGAY TRƯỚC release edge gần nhất trong
        /// window (đo độ "charge" tại thời điểm nhả). 0 nếu không có release edge trong window. Đọc TỪ
        /// buffer (§3.2) — dùng cho charge: held-before-release ≥ chargeFrames ⇒ biến thể mạnh.
        /// </summary>
        public int HeldFramesBeforeRelease(InputButton b, int frames)
        {
            int count = _buffer.Count;
            if (count < 2) return 0;
            int limit = frames < count ? frames : count - 1;
            for (int ago = 0; ago < limit; ago++)
            {
                bool now = _buffer.Peek(ago).IsHeld(b);
                bool prev = _buffer.Peek(ago + 1).IsHeld(b);
                if (!now && prev) // release edge tại snapshot 'ago' (giữ ở ago+1, nhả ở ago)
                {
                    int run = 0;
                    for (int k = ago + 1; k < count; k++)
                    {
                        if (_buffer.Peek(k).IsHeld(b)) run++;
                        else break;
                    }
                    return run;
                }
            }
            return 0;
        }

        /// <summary>Frame của release edge (giữ→nhả) mới nhất trong window, hoặc int.MinValue.</summary>
        private int FindReleaseFrame(InputButton b, int frames)
        {
            int count = _buffer.Count;
            if (count < 2) return int.MinValue;
            int limit = frames < count ? frames : count - 1;
            for (int ago = 0; ago < limit; ago++)
            {
                bool now = _buffer.Peek(ago).IsHeld(b);
                bool prev = _buffer.Peek(ago + 1).IsHeld(b);
                if (!now && prev) return _buffer.Peek(ago).Frame;
            }
            return int.MinValue;
        }

        // ===================== HELD FRAMES (charge, P9) =====================

        /// <summary>
        /// Số snapshot LIÊN TỤC gần nhất (kể từ frame mới nhất) mà nút <paramref name="b"/> đang GIỮ.
        /// Dùng cho charge (P9, research §3.6): giữ nút lâu → biến thể mạnh hơn. Trả 0 nếu frame mới nhất
        /// không giữ. THÊM method (KHÔNG sửa method cũ — research §6 "chỉ ADD"). Vẫn đọc TỪ buffer, KHÔNG
        /// chạm Input System (§3.2).
        /// </summary>
        public int HeldFrames(InputButton b)
        {
            int count = _buffer.Count;
            if (count == 0) return 0;
            if (!_buffer.Peek(0).IsHeld(b)) return 0;
            int run = 0;
            for (int ago = 0; ago < count; ago++)
            {
                if (_buffer.Peek(ago).IsHeld(b)) run++;
                else break;
            }
            return run;
        }

        // ===================== TAP vs HOLD =====================

        /// <summary>
        /// Phân biệt tap-vs-hold cho 1 hướng (tương-đối-facing). Đếm số snapshot LIÊN TỤC gần nhất
        /// đang ép <paramref name="dir"/>. Nếu HIỆN TẠI còn giữ và đã giữ ≥ tapMaxHold → Hold (Ngồi).
        /// Nếu vừa nhả sau khi giữ ≤ tapMaxHold → Tap (Né). Không giữ gần đây → None.
        /// </summary>
        /// <param name="facing">Hướng nhìn (±1) để map Forward/Back theo màn hình.</param>
        /// <param name="tapMaxHold">Ngưỡng frame: giữ ≤ ngưỡng = Tap, vượt = Hold (từ SO, CẦN TEST).</param>
        public HoldKind GetDirectionHold(InputDirection dir, int facing, int tapMaxHold, out int heldFrames)
        {
            heldFrames = 0;
            int count = _buffer.Count;
            if (count == 0) return HoldKind.None;

            // Đang giữ ngay frame mới nhất?
            bool holdingNow = MatchesDirection(_buffer.Peek(0), dir, facing);
            if (holdingNow)
            {
                // Đếm chuỗi giữ liên tục lùi về quá khứ.
                int run = 0;
                for (int ago = 0; ago < count; ago++)
                {
                    if (MatchesDirection(_buffer.Peek(ago), dir, facing)) run++;
                    else break;
                }
                heldFrames = run;
                return run > tapMaxHold ? HoldKind.Hold : HoldKind.Tap;
            }

            // Không giữ bây giờ: tìm chuỗi giữ vừa nhả (kết thúc gần đây) để báo Tap.
            // Bỏ qua các frame neutral mới nhất, rồi đo chuỗi giữ liền trước đó.
            int idx = 0;
            while (idx < count && !MatchesDirection(_buffer.Peek(idx), dir, facing)) idx++;
            if (idx >= count) return HoldKind.None; // không hề giữ trong buffer
            int held = 0;
            for (int ago = idx; ago < count; ago++)
            {
                if (MatchesDirection(_buffer.Peek(ago), dir, facing)) held++;
                else break;
            }
            heldFrames = held;
            // Vừa nhả sau khi giữ ngắn = Tap; nếu đã giữ rất lâu rồi nhả thì coi như xong (None).
            return held <= tapMaxHold ? HoldKind.Tap : HoldKind.None;
        }

        // ===================== MOTION (QCF/QCB/DP, lenient) =====================

        /// <summary>
        /// Quét buffer tìm chuỗi hướng khớp motion (lenient: cho phép hướng thừa/lặp, thiếu hướng
        /// chéo). Tương-đối-facing qua <paramref name="facing"/>. P4 hiện thực QCF/QCB/DPForward/DPBack;
        /// Charge trả false (KHUNG — P9 hiện thực).
        /// </summary>
        /// <param name="withinFrames">Cửa sổ frame để hoàn thành motion (motionWindow từ SO).</param>
        public bool TryMatchMotion(MotionInput motion, int withinFrames, int facing)
        {
            switch (motion)
            {
                case MotionInput.QCF:       return MatchSequence(SeqQCF, withinFrames, facing);
                case MotionInput.QCB:       return MatchSequence(SeqQCB, withinFrames, facing);
                case MotionInput.DPForward: return MatchSequence(SeqDPForward, withinFrames, facing);
                case MotionInput.DPBack:    return MatchSequence(SeqDPBack, withinFrames, facing);
                default:                    return false; // None / Charge* (khung) → P9
            }
        }

        /// <summary>
        /// Tiện ích cho overlay/P9: motion ƯU TIÊN nào hoàn tất trong window (DP trước QCF vì DP
        /// chứa QCF-con). None nếu không có.
        /// </summary>
        public MotionInput DetectMotion(int withinFrames, int facing)
        {
            if (TryMatchMotion(MotionInput.DPForward, withinFrames, facing)) return MotionInput.DPForward;
            if (TryMatchMotion(MotionInput.DPBack, withinFrames, facing)) return MotionInput.DPBack;
            if (TryMatchMotion(MotionInput.QCF, withinFrames, facing)) return MotionInput.QCF;
            if (TryMatchMotion(MotionInput.QCB, withinFrames, facing)) return MotionInput.QCB;
            return MotionInput.None;
        }

        // Chuỗi mục tiêu theo InputDirection tương-đối-facing (đọc từ buffer mới→cũ nên so NGƯỢC).
        // QCF = Down, DownForward, Forward.  DP forward = Forward, Down, DownForward.
        private static readonly InputDirection[] SeqQCF =
            { InputDirection.Down, InputDirection.DownForward, InputDirection.Forward };
        private static readonly InputDirection[] SeqQCB =
            { InputDirection.Down, InputDirection.DownBack, InputDirection.Back };
        private static readonly InputDirection[] SeqDPForward =
            { InputDirection.Forward, InputDirection.Down, InputDirection.DownForward };
        private static readonly InputDirection[] SeqDPBack =
            { InputDirection.Back, InputDirection.Down, InputDirection.DownBack };

        /// <summary>
        /// Lenient match: tìm các bước của <paramref name="seq"/> theo ĐÚNG THỨ TỰ trong buffer
        /// (mới nhất = bước cuối seq), trong <paramref name="withinFrames"/>. Cho phép frame thừa
        /// (neutral/lặp) giữa các bước; cho thiếu hướng chéo bằng cách so "tương thích" (vd DownForward
        /// thỏa cả khi quét bước Down/Forward lân cận). Quét buffer mới→cũ, khớp seq cuối→đầu.
        /// </summary>
        private bool MatchSequence(InputDirection[] seq, int withinFrames, int facing)
        {
            int count = _buffer.Count;
            int limit = withinFrames < count ? withinFrames : count;
            if (limit < seq.Length) return false;

            int step = seq.Length - 1;     // bắt đầu khớp từ bước CUỐI (gần hiện tại nhất)
            for (int ago = 0; ago < limit && step >= 0; ago++)
            {
                InputSnapshot snap = _buffer.Peek(ago);
                if (DirectionSatisfies(snap, seq[step], facing))
                {
                    step--; // khớp 1 bước, lùi sang bước trước (cũ hơn)
                }
            }
            return step < 0; // khớp hết mọi bước
        }

        // ===================== Direction helpers =====================

        /// <summary>
        /// True nếu snapshot ép ĐÚNG hướng dir (chính xác, không chéo-suy-rộng). Dùng cho tap/hold.
        /// </summary>
        private static bool MatchesDirection(in InputSnapshot snap, InputDirection dir, int facing)
        {
            ToRelative(snap, facing, out int rel, out int dy);
            return RelToDirection(rel, dy) == dir;
        }

        /// <summary>
        /// Khoan dung hơn cho motion: bước seq được "thỏa" nếu hướng snapshot KHỚP hoặc TƯƠNG THÍCH
        /// (chéo thỏa cả thành phần ngang/dọc). VD bước Forward thỏa bởi Forward hoặc DownForward;
        /// bước Down thỏa bởi Down/DownForward/DownBack → cho phép "thiếu hướng chéo" như SF lenient.
        /// </summary>
        private static bool DirectionSatisfies(in InputSnapshot snap, InputDirection step, int facing)
        {
            ToRelative(snap, facing, out int rel, out int dy); // rel: -1 back,0,+1 forward; dy: -1 down,0,+1 up
            switch (step)
            {
                case InputDirection.Forward:      return rel > 0;            // forward (kèm chéo OK)
                case InputDirection.Back:         return rel < 0;
                case InputDirection.Down:         return dy < 0;             // xuống (kèm chéo OK)
                case InputDirection.Up:           return dy > 0;
                case InputDirection.DownForward:  return dy < 0 && rel > 0;  // cần đúng chéo
                case InputDirection.DownBack:     return dy < 0 && rel < 0;
                case InputDirection.UpForward:    return dy > 0 && rel > 0;
                case InputDirection.UpBack:       return dy > 0 && rel < 0;
                default:                          return rel == 0 && dy == 0;
            }
        }

        /// <summary>Đổi DirX thô (theo màn hình) sang forward/back theo facing. dy giữ nguyên (dọc).</summary>
        private static void ToRelative(in InputSnapshot snap, int facing, out int rel, out int dy)
        {
            int f = facing >= 0 ? 1 : -1;
            rel = snap.DirX * f;  // forward = cùng dấu facing
            dy = snap.DirY;
        }

        /// <summary>Ghép (rel, dy) thành InputDirection chính xác (cho MatchesDirection).</summary>
        private static InputDirection RelToDirection(int rel, int dy)
        {
            if (dy < 0)
            {
                if (rel > 0) return InputDirection.DownForward;
                if (rel < 0) return InputDirection.DownBack;
                return InputDirection.Down;
            }
            if (dy > 0)
            {
                if (rel > 0) return InputDirection.UpForward;
                if (rel < 0) return InputDirection.UpBack;
                return InputDirection.Up;
            }
            if (rel > 0) return InputDirection.Forward;
            if (rel < 0) return InputDirection.Back;
            return InputDirection.Neutral;
        }
    }
}
