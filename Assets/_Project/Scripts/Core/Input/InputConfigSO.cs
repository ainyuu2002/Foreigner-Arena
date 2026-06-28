using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Cấu hình tuning cho Input Buffer + Command Parser (Phase 4, §3.2). ScriptableObject DÙNG CHUNG
    /// (1 asset, mọi Motor đọc cùng) — KHÔNG hardcode window trong code (§1.2 + §6 "để ngỏ → SO").
    /// Mọi giá trị dưới đây là CẢM-GIÁC fighting-game → đánh dấu CẦN TEST trong Guide.
    ///
    /// Để ở FA.Core vì cả Movement (Motor) lẫn Specials (P9/P10) đọc chung qua interface input.
    /// Motor nên gán asset này; nếu null → Motor fallback sang giá trị mặc định an toàn (bằng đây).
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Input/Input Config", fileName = "InputConfig")]
    public sealed class InputConfigSO : ScriptableObject
    {
        [Header("Buffer")]
        [Tooltip("Sức chứa ring buffer (số frame lưu). §3.2 chốt ~10. CẦN TEST.")]
        [Min(1)] public int bufferFrames = 10;

        [Header("Edge (WasPressedWithin)")]
        [Tooltip("Cửa sổ tha-thứ cho edge nút (Jump/Dash...): nhập sớm trong N frame vẫn ăn. CẦN TEST.")]
        [Min(1)] public int edgeWindow = 6;

        [Header("Motion (QCF/QCB/DP)")]
        [Tooltip("Số frame để hoàn tất 1 motion input. Lenient. CẦN TEST.")]
        [Min(1)] public int motionWindow = 12;

        [Header("Tap vs Hold (Né ↓ vs Ngồi ↓ — GDD §4.1)")]
        [Tooltip("Giữ ≤ ngưỡng (frame) = Tap (Né); vượt = Hold (Ngồi). CẦN TEST (~6-8).")]
        [Min(1)] public int tapMaxHold = 7;
    }
}
