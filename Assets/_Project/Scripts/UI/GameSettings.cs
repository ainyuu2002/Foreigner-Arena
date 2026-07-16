using UnityEngine;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings) CẤU HÌNH NGƯỜI CHƠI lưu <see cref="PlayerPrefs"/> — hiện chỉ ÂM LƯỢNG (Master/BGM/SFX).
    /// Project CHƯA có hệ thống audio (không AudioMixer/AudioSource nào trong _Project) nên:
    ///   • Master áp thẳng <see cref="AudioListener.volume"/> — có tác dụng ngay khi audio xuất hiện.
    ///   • BGM/SFX CHỈ LƯU giá trị — CONTRACT cho người làm audio sau này: đọc key
    ///     <see cref="KeyBgm"/>/<see cref="KeySfx"/> (0..1) và nhân vào volume nguồn phát tương ứng
    ///     (hoặc map sang AudioMixer exposed param khi có mixer).
    /// Áp lại lúc khởi động qua <see cref="RuntimeInitializeOnLoadMethodAttribute"/> — không cần object nào
    /// trong scene. KHÔNG đụng code gameplay (§1.3).
    /// </summary>
    public static class GameSettings
    {
        public const string KeyMaster = "FA.Volume.Master";
        public const string KeyBgm = "FA.Volume.BGM";
        public const string KeySfx = "FA.Volume.SFX";

        public static float Master
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 1f);
            set { PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value)); ApplyMaster(); }
        }

        public static float Bgm
        {
            get => PlayerPrefs.GetFloat(KeyBgm, 1f);
            set => PlayerPrefs.SetFloat(KeyBgm, Mathf.Clamp01(value));
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(KeySfx, 1f);
            set => PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value));
        }

        /// <summary>Cộng dồn Master theo delta (cho cử chỉ lắc/thổi) — clamp 0..1 + áp ngay qua setter Master.
        /// KHÔNG tự Save (gọi nhiều lần/frame); PlayerPrefs.Save() để lúc đóng panel như slider.</summary>
        public static void NudgeMaster(float delta) => Master = Master + delta;

        /// <summary>Ghi PlayerPrefs xuống đĩa — gọi khi đóng panel settings (kẻo mất khi crash).</summary>
        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnBoot() => ApplyMaster();

        private static void ApplyMaster() => AudioListener.volume = Master;
    }
}
