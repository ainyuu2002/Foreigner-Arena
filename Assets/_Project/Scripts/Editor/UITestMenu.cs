using UnityEditor;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// MENU TEST HUD (P12, editor-only): bắn sự kiện THẲNG vào kênh per-player trong lúc Play để xem HUD
    /// phản ứng (glow đầy thanh, ô mana, thanh nộ, màu % knockback) mà KHÔNG cần gameplay bắn thật —
    /// harness fighter hiện không có hệ mana/nộ nên không có nguồn sự kiện nào khác.
    ///
    /// Kênh load qua <see cref="UIChannels.Resolve"/> (cùng asset instance scene đang ref — AssetDatabase
    /// chỉ có 1 bản/asset) → RaiseEvent là HUD đang chạy nhận ngay. Chỉ chạy trong Play mode.
    /// </summary>
    internal static class UITestMenu
    {
        [MenuItem("FA/UI/Test/HUD ĐẦY (mana + nộ + % cao, cả P1 P2)")]
        public static void TestHudFull()
        {
            if (!RequirePlaying()) return;
            var ch = UIChannels.Resolve();
            if (ch.ManaSegP1 != null) ch.ManaSegP1.RaiseEvent(9);   // ≥ số ô → đầy, khỏi cần biết n.
            if (ch.ManaSegP2 != null) ch.ManaSegP2.RaiseEvent(9);
            if (ch.RageP1 != null) ch.RageP1.RaiseEvent(1f);
            if (ch.RageP2 != null) ch.RageP2.RaiseEvent(1f);
            if (ch.PercentP1 != null) ch.PercentP1.RaiseEvent(180f); // % cao → thanh vitality ngả đỏ.
            if (ch.PercentP2 != null) ch.PercentP2.RaiseEvent(60f);  // % vừa — so màu 2 bên cho dễ thấy.
            Debug.Log("[UITest] Đã bắn: mana 9 ô, nộ 1.0, % P1=180/P2=60 — glow phải thở ở cả 2 góc dưới + thanh nộ.");
        }

        [MenuItem("FA/UI/Test/HUD VỀ 0 (tắt glow)")]
        public static void TestHudReset()
        {
            if (!RequirePlaying()) return;
            var ch = UIChannels.Resolve();
            if (ch.ManaSegP1 != null) ch.ManaSegP1.RaiseEvent(0);
            if (ch.ManaSegP2 != null) ch.ManaSegP2.RaiseEvent(0);
            if (ch.RageP1 != null) ch.RageP1.RaiseEvent(0f);
            if (ch.RageP2 != null) ch.RageP2.RaiseEvent(0f);
            if (ch.PercentP1 != null) ch.PercentP1.RaiseEvent(0f);
            if (ch.PercentP2 != null) ch.PercentP2.RaiseEvent(0f);
            Debug.Log("[UITest] Đã bắn 0 toàn bộ — glow tắt, ô mana rỗng, % trắng.");
        }

        private static bool RequirePlaying()
        {
            if (Application.isPlaying) return true;
            EditorUtility.DisplayDialog("Test HUD",
                "Menu này bắn sự kiện vào HUD ĐANG CHẠY — hãy vào Play, vào tới trong trận (HUD hiện) rồi bấm lại.",
                "OK");
            return false;
        }
    }
}
