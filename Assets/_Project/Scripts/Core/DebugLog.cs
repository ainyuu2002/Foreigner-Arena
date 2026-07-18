using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Cổng bật/tắt toàn cục cho debug log RUNTIME (Movement/Combat/Stage/UI/GameFlow/Resources/Effects...).
    /// Mặc định TẮT (<see cref="Enabled"/> = false) để log cũ của các tính năng đã ổn định không làm
    /// loãng Console khi sau này cần lấy log mới cho một bug khác — cần bật lại chỉ sửa 1 CHỖ DUY NHẤT
    /// ở đây, không phải mở lại từng file.
    ///
    /// KHÔNG áp cho Editor Tool (script trong FA.EditorTools.Editor, chạy tay qua menu) — log của chúng
    /// LÀ kết quả cần thiết khi người dùng chủ động bấm menu, không phải log rơi vãi lúc chơi game.
    /// </summary>
    public static class DebugLog
    {
        public static bool Enabled = false;

        public static void Log(string message)
        {
            if (Enabled) Debug.Log(message);
        }

        public static void Log(string message, Object context)
        {
            if (Enabled) Debug.Log(message, context);
        }
    }
}
