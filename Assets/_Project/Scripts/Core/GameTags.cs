namespace FA.Core
{
    /// <summary>
    /// Hằng số tag để tránh "magic string" rải rác khi gọi CompareTag/FindWithTag.
    /// Tên phải khớp tag khai trong Project Settings → Tags and Layers.
    /// Hạn chế dùng tag để tìm object; ưu tiên reference trực tiếp / event channel.
    /// </summary>
    public static class GameTags
    {
        public const string Player1 = "Player1";
        public const string Player2 = "Player2";
    }
}
