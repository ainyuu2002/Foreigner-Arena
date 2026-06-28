namespace FA.Core
{
    /// <summary>
    /// Ảnh chụp (snapshot) trạng thái input THÔ của đúng 1 fixed-step (Phase 4, §3.2).
    /// Là **struct value-type** → lưu trong mảng cố định của <see cref="InputBuffer"/> mà KHÔNG
    /// cấp phát/box (hot path mỗi FixedUpdate, research §4 "GC alloc trong buffer").
    ///
    /// Lưu trạng thái HELD (đang-giữ) của mỗi nút, KHÔNG lưu edge: edge được SUY RA bằng cách so
    /// 2 snapshot liên tiếp (held[now] &amp;&amp; !held[prev]) trong CommandParser → 1 nguồn dữ liệu đủ
    /// cho cả "edge" (entry-point) lẫn "held bao lâu" (tap/hold). Hướng lưu dạng sbyte ∈ {-1,0,1}.
    ///
    /// LƯU Ý FACING: DirX ở đây là hướng input THÔ THEO MÀN HÌNH (trái = -1, phải = +1). Parser
    /// dịch sang forward/back theo facing khi cần — buffer KHÔNG biết facing (giữ buffer thuần raw).
    /// </summary>
    public readonly struct InputSnapshot
    {
        /// <summary>Trục ngang thô theo màn hình: -1 trái, 0, +1 phải.</summary>
        public readonly sbyte DirX;

        /// <summary>Trục dọc thô: -1 xuống, 0, +1 lên.</summary>
        public readonly sbyte DirY;

        // --- Trạng thái HELD mỗi nút (raw IsPressed của fixed-step này) ---
        public readonly bool Jump;
        public readonly bool Dash;
        public readonly bool L;
        public readonly bool M;
        public readonly bool H;
        public readonly bool Super1;
        public readonly bool Super2;
        public readonly bool Rage;

        /// <summary>Số fixed-step khi chụp (đo window theo FRAME, deterministic — KHÔNG dùng Time.time).</summary>
        public readonly int Frame;

        public InputSnapshot(sbyte dirX, sbyte dirY, bool jump, bool dash, bool l, bool m, bool h,
            bool super1, bool super2, bool rage, int frame)
        {
            DirX = dirX;
            DirY = dirY;
            Jump = jump;
            Dash = dash;
            L = l;
            M = m;
            H = h;
            Super1 = super1;
            Super2 = super2;
            Rage = rage;
            Frame = frame;
        }

        /// <summary>
        /// Notation số-pad (1..9, 5 = neutral) tính sẵn từ DirX/DirY — THEO MÀN HÌNH (chưa facing).
        /// 7 8 9 / 4 5 6 / 1 2 3 (giống bàn phím numpad). Parser dùng để so chuỗi motion.
        /// </summary>
        public int NumpadDir
        {
            get
            {
                // Hàng: dưới(1-3)=0, giữa(4-6)=1, trên(7-9)=1*... → tính trực tiếp.
                int col = DirX + 1;            // 0=trái,1=giữa,2=phải
                int rowBase = DirY < 0 ? 1     // dưới → 1,2,3
                            : DirY > 0 ? 7      // trên → 7,8,9
                            : 4;               // giữa → 4,5,6
                return rowBase + col;
            }
        }

        /// <summary>True nếu nút b đang được giữ trong snapshot này (cho parser suy edge/held).</summary>
        public bool IsHeld(InputButton b)
        {
            switch (b)
            {
                case InputButton.Jump: return Jump;
                case InputButton.Dash: return Dash;
                case InputButton.L: return L;
                case InputButton.M: return M;
                case InputButton.H: return H;
                case InputButton.Super1: return Super1;
                case InputButton.Super2: return Super2;
                case InputButton.Rage: return Rage;
                default: return false;
            }
        }
    }
}
