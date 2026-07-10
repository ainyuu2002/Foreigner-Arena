namespace FA.Core
{
    /// <summary>
    /// CỔNG INPUT toàn cục khi CINEMATIC đang mở (chant niệm chú / super flash — Time.timeScale=0,
    /// thế giới đứng hình). Static đếm ĐỘ SÂU (chant + flash lồng nhau được). FA.Effects Acquire/Release
    /// quanh cinematic; các entry-point (Combat/Special/DefenseController) tra <see cref="IsActive"/> để
    /// KHÔNG kích move mới giữa cinematic — kích được sẽ giật Animator khỏi clip cinematic giữa chừng,
    /// các event kết (ChantEnd/SuperFlashEnd/UltimateInstall) không bao giờ bắn → freeze/dim/camera-lock
    /// kẹt VĨNH VIỄN (bug user bắt: spam super ngay khi vừa vào Rage Mode UBW → đơ trong flash).
    /// Đặt ở Core để mọi module tra được mà không reference chéo (§1.3).
    /// </summary>
    public static class CinematicGate
    {
        private static int s_depth;

        // Editor tắt domain-reload: static sống qua các lần Play → depth rác khoá input cả trận. Reset.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => s_depth = 0;

        /// <summary>Có cinematic đang mở? Entry-point input trả về sớm khi true.</summary>
        public static bool IsActive => s_depth > 0;

        /// <summary>Cinematic mở (gọi ở ChantStart/SuperFlashStart). Đếm sâu — lồng nhau an toàn.</summary>
        public static void Acquire() => s_depth++;

        /// <summary>Cinematic đóng (gọi ở ChantEnd/SuperFlashEnd — caller tự chống double-release).</summary>
        public static void Release()
        {
            if (s_depth > 0) s_depth--;
        }
    }
}
