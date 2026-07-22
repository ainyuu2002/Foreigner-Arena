namespace FA.Core
{
    /// <summary>
    /// (P15) HELPER tĩnh DÙNG CHUNG cho cả 3 state có thể nối (AttackState/SpecialState/SuperState).
    /// Nơi MATCH follow-up DUY NHẤT — Combat/Specials chỉ gọi vào đây (không tự duyệt followUps).
    ///
    /// (P15 tinh chỉnh) TÁCH "nhận input" khỏi "thực thi nối" để bắt cú ấn follow-up SỚM:
    ///   • <see cref="TryLatch"/> — gọi MỖI Tick TỪ ĐẦU đòn (KHÔNG chỉ khi cancel window mở). Nếu player
    ///     vừa nhấn-edge một nút CÓ trong followUps của đòn hiện hành → CONSUME edge ngay (bắt intent) và
    ///     trả nút đó để state ghi nhớ (latch). Chỉ latch nút có trong followUps → KHÔNG nuốt nút lạ.
    ///   • <see cref="TryResolveLatched"/> — gọi KHI cancel window mở (Animation Event CancelWindowOpen).
    ///     Lấy nút đã latch + đọc didHit TẠI thời điểm window → quyết "có nối / nối đòn nào". KHÔNG consume
    ///     (đã consume lúc latch).
    /// Vì sao tách: trước đây chỉ match TẠI lúc window mở; player ấn rất sớm (frame 2 của đòn 30 frame) thì
    /// tới lúc window mở (frame ~18) edge đã ngoài edgeWindow/buffer → MISS. Latch lưu intent từ sớm.
    ///
    /// Đặt ở FA.Core (chỉ ref MoveData/CommandParser/InputVocabulary — đều ở Core) để Combat lẫn Specials
    /// dùng chung mà KHÔNG ref chéo (§1.3). Thuần hàm quyết định — KHÔNG chứa state.
    ///
    /// TIMING (§1.1): cửa sổ THỰC THI nối vẫn do Animation Event CancelWindowOpen/Close (caller gọi
    /// TryResolveLatched chỉ khi _canCancel). Latch chỉ là BỘ NHỚ INTENT, đọc edge/motion qua window do
    /// caller truyền (InputConfigSO) — KHÔNG đếm frame để quyết timing nối.
    /// </summary>
    public static class MoveChainResolver
    {
        /// <summary>
        /// (P15 tinh chỉnh) Bắt INTENT sớm: duyệt followUps của <paramref name="move"/>; nếu player vừa
        /// nhấn-edge nút của một follow-up trong <paramref name="edgeWindow"/> → CONSUME edge đó (để cùng
        /// nhấn không re-fire đòn 1 từ neutral) và trả <paramref name="latched"/> = nút đó, return true.
        ///
        /// CHỈ xét nút CÓ trong followUps → không latch/nuốt nút không liên quan. Bỏ mắt nextMove==null.
        /// KHÔNG xét điều kiện/motion ở đây — chỉ "đã ấn nút nối?". Điều kiện/motion để TryResolveLatched
        /// xét tại thời điểm window (didHit có thể thay đổi giữa lúc latch và lúc window mở).
        /// </summary>
        /// <param name="move">Đòn hiện hành. Null hoặc followUps rỗng → trả false ngay (no-regression).</param>
        /// <param name="parser">CommandParser của nhân vật (đọc edge từ buffer).</param>
        /// <param name="edgeWindow">Cửa sổ frame nhận nút nối (= ICombatContext.EdgeWindow).</param>
        /// <param name="latched">[out] Nút follow-up vừa bắt được (chỉ hợp lệ khi return true).</param>
        public static bool TryLatch(MoveData move, CommandParser parser, int edgeWindow,
                                    out InputButton latched)
        {
            latched = default;
            if (move == null || parser == null) return false;
            var list = move.followUps;
            if (list == null || list.Count == 0) return false;

            for (int i = 0; i < list.Count; i++)
            {
                FollowUp fu = list[i];
                if (fu.nextMove == null) continue;
                if (!parser.WasPressedWithin(fu.button, edgeWindow)) continue;

                // Bắt intent: tiêu thụ edge NGAY (1 nhấn = 1 nối). Nếu không, edge còn trong buffer ~10f →
                // frame kế CombatController lại thấy "nút vừa nhấn" → bắn đòn 1 từ neutral (double-fire).
                parser.ConsumePress(fu.button, edgeWindow);
                latched = fu.button;
                return true;
            }
            return false;
        }

        /// <summary>
        /// (P15 tinh chỉnh) THỰC THI nối từ nút đã latch — gọi KHI cancel window mở. Tìm follow-up có
        /// button==<paramref name="latched"/> mà: nextMove≠null, điều kiện thoả (Always, HOẶC OnHit &amp;&amp;
        /// <paramref name="didHit"/>), motion≠None thì khớp trong <paramref name="motionWindow"/>. Mắt ĐẦU
        /// TIÊN khớp → trả nextMove. Không mắt nào → null.
        ///
        /// KHÔNG ConsumePress (đã consume lúc latch). KHÔNG clear latch (caller giữ latch nếu chưa thoả —
        /// vd OnHit chưa trúng nhưng hit có thể tới sau trong cùng window).
        /// </summary>
        /// <param name="move">Đòn hiện hành (chứa followUps).</param>
        /// <param name="latched">Nút đã latch (từ TryLatch).</param>
        /// <param name="parser">CommandParser (khớp motion nếu cần).</param>
        /// <param name="motionWindow">Cửa sổ frame khớp motion (đồng nhất state: EdgeWindow×2).</param>
        /// <param name="facing">Hướng nhìn (±1) — motion tương-đối-facing.</param>
        /// <param name="didHit">Đòn hiện hành đã TRÚNG THẬT chưa (HitConfirmTracker.HitSinceReset) TẠI thời điểm window.</param>
        public static MoveData TryResolveLatched(MoveData move, InputButton latched, CommandParser parser,
                                                 int motionWindow, int facing, bool didHit)
        {
            if (move == null) return null;
            var list = move.followUps;
            if (list == null || list.Count == 0) return null;

            for (int i = 0; i < list.Count; i++)
            {
                FollowUp fu = list[i];
                if (fu.nextMove == null) continue;
                if (!fu.button.Equals(latched)) continue;
                if (fu.condition == FollowUpCondition.OnHit && !didHit) continue;
                if (fu.motion != MotionInput.None &&
                    (parser == null || !parser.TryMatchMotion(fu.motion, motionWindow, facing))) continue;

                return fu.nextMove;
            }
            return null;
        }
    }
}
