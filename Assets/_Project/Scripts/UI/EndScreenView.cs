using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) MÀN KẾT THÚC — hiện đúng người thắng / DRAW.
    /// FIX bug "luôn hiện Player 1 win": component này phải nằm trên OBJECT LUÔN ACTIVE (root canvas
    /// Menu_EndScreen / canvas harness), KHÔNG nằm trên EndScreenPanel. WinCondition raise kết quả NGAY
    /// khi trận xong, còn MatchManager giữ pose outro vài giây rồi MỚI bật panel — subscribe trong
    /// OnEnable của panel (đang inactive) thì LỠ sự kiện → label kẹt text mặc định của builder
    /// ("PLAYER 1 WINS" cũ). Đặt ở root: subscribe từ lúc load scene, ghi text lúc panel còn ẩn
    /// (Text.text set được trên object inactive — hiện đúng khi panel bật).
    /// BẢNG STATS/TÍNH ĐIỂM ĐÃ BỎ (out of scope — chốt 2026-07-18).
    /// Decoupling (§1.3): nghe kênh Core (MatchResult), KHÔNG ref FA.GameFlow.
    ///
    /// Nút Rematch / Back = <see cref="VoidButtonRaiser"/> raise kênh riêng (MatchManager nghe) —
    /// KHÔNG gọi MatchManager trực tiếp.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EndScreenView : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnMatchResult (MatchResultEventChannelSO) — kết quả từ WinCondition.")]
        [SerializeField] private MatchResultEventChannelSO channel;

        [Header("Hiển thị")]
        [Tooltip("Label người thắng — CON của EndScreenPanel (panel có thể đang tắt lúc nhận event, không sao).")]
        [SerializeField] private Text winnerLabel;

        private void OnEnable() { if (channel != null) channel.OnEventRaised += OnResult; }
        private void OnDisable() { if (channel != null) channel.OnEventRaised -= OnResult; }

        private void OnResult(MatchResult result)
        {
            string text = result.Winner switch
            {
                MatchWinner.P1 => "PLAYER 1 WINS",
                MatchWinner.P2 => "PLAYER 2 WINS",
                _ => "DRAW"
            };
            if (winnerLabel != null) winnerLabel.text = text;
            Debug.Log($"[FA.UI] EndScreen ← {text}"); // marker đối chiếu khi test (so với kết quả trận thật).
        }
    }
}
