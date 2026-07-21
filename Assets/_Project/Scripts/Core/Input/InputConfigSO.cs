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
        [Tooltip("Sức chứa ring buffer (số frame lưu). §3.2 chốt ~10; nâng ~24 để chứa 2 nhịp tap ↓ " +
                 "cách nhau doubleTapDownWindow (~20f) + frame 'prev' cho edge detection. Window nhỏ hơn " +
                 "(edge/motion/tap) vẫn dùng min(window,count) → KHÔNG bị ảnh hưởng. CẦN TEST.")]
        [Min(1)] public int bufferFrames = 24;

        [Header("Double-tap ↓ Drop-through platform")]
        [Tooltip("Cửa sổ (frame @60Hz fixed) để 2 nhịp tap Xuống tính là double-tap → rơi xuyên " +
                 "one-way platform. Đo TRỌN chu kỳ nhấn→nhả→nhấn lại nên cần nới rộng hơn cảm giác " +
                 "'0.25s' thô — ~20f ≈ 0.33s. CẦN TEST.")]
        [Min(2)] public int doubleTapDownWindow = 20;

        [Tooltip("Số giây TẠM tắt va chạm Player↔OneWayPlatform (Physics2D.IgnoreLayerCollision) để " +
                 "nhân vật rơi khỏi platform trước khi bật lại. Khoảng 0.3-0.5. CẦN TEST.")]
        [Min(0.05f)] public float dropThroughIgnoreSeconds = 0.35f;

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
