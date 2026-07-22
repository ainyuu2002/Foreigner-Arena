using UnityEngine;
using UnityEngine.Audio;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12 Settings) CẤU HÌNH NGƯỜI CHƠI lưu <see cref="PlayerPrefs"/> — hiện chỉ ÂM LƯỢNG (Master/BGM/SFX).
    /// Kiến trúc âm lượng (v2):
    ///   • Master áp thẳng <see cref="AudioListener.volume"/> — âm TỔNG, ảnh hưởng MỌI nguồn phát
    ///     (kể cả nguồn chưa route qua mixer) → không bao giờ "sót" âm thanh.
    ///   • BGM/SFX áp qua AudioMixer <see cref="MixerResourcePath"/> (nạp từ Resources lúc boot) — 2 group con
    ///     BGM/SFX với exposed param <see cref="ParamBgm"/>/<see cref="ParamSfx"/> (dB). Nguồn phát muốn ăn
    ///     slider nào thì route outputAudioMixerGroup vào group đó (prefab wiring — FA/Audio tools).
    ///   • CHƯA có mixer trong project → BGM/SFX chỉ lưu giá trị như cũ (contract giữ nguyên: key
    ///     <see cref="KeyBgm"/>/<see cref="KeySfx"/> 0..1) — không lỗi, chỉ log để biết.
    /// Áp lại lúc khởi động qua <see cref="RuntimeInitializeOnLoadMethodAttribute"/> — không cần object nào
    /// trong scene. KHÔNG đụng code gameplay (§1.3).
    /// </summary>
    public static class GameSettings
    {
        public const string KeyMaster = "FA.Volume.Master";
        public const string KeyBgm = "FA.Volume.BGM";
        public const string KeySfx = "FA.Volume.SFX";

        /// <summary>Đường dẫn Resources của mixer — file đặt tại Assets/_Project/Audio/Resources/FA_AudioMixer.mixer.</summary>
        public const string MixerResourcePath = "FA_AudioMixer";
        /// <summary>Exposed param (dB) của group BGM/SFX trong mixer — đặt tên đúng y vậy khi expose.</summary>
        public const string ParamBgm = "BgmVol";
        public const string ParamSfx = "SfxVol";

        private static AudioMixer _mixer;

        public static float Master
        {
            get => PlayerPrefs.GetFloat(KeyMaster, 1f);
            set { PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(value)); ApplyMaster(); }
        }

        public static float Bgm
        {
            get => PlayerPrefs.GetFloat(KeyBgm, 1f);
            set { PlayerPrefs.SetFloat(KeyBgm, Mathf.Clamp01(value)); ApplyBgm(); }
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(KeySfx, 1f);
            set { PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(value)); ApplySfx(); }
        }

        /// <summary>Cộng dồn Master theo delta (cho cử chỉ lắc/thổi) — clamp 0..1 + áp ngay qua setter Master.
        /// KHÔNG tự Save (gọi nhiều lần/frame); PlayerPrefs.Save() để lúc đóng panel như slider.</summary>
        public static void NudgeMaster(float delta) => Master = Master + delta;

        /// <summary>Ghi PlayerPrefs xuống đĩa — gọi khi đóng panel settings (kẻo mất khi crash).</summary>
        public static void Save() => PlayerPrefs.Save();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnBoot()
        {
            _mixer = Resources.Load<AudioMixer>(MixerResourcePath);
            ApplyMaster();
            ApplyBgm();
            ApplySfx();
            // Marker xác nhận hệ audio boot (đối chiếu Console khi test — như marker LOBBY của builder).
            // Runtime SetFloat là "sự thật cuối" về exposed param (editor Verify chỉ là dự đoán ở Edit Mode).
            string paramState = _mixer == null ? "" :
                $", param {ParamBgm}: {_mixer.SetFloat(ParamBgm, ToDb(Bgm))}, {ParamSfx}: {_mixer.SetFloat(ParamSfx, ToDb(Sfx))}";
            DebugLog.Log($"[FA.Audio] AUDIO v3 — mixer: {(_mixer != null ? "OK" : "CHƯA CÓ (BGM/SFX chỉ lưu giá trị)")}{paramState}");
        }

        private static void ApplyMaster() => AudioListener.volume = Master;

        private static void ApplyBgm() { if (_mixer != null) _mixer.SetFloat(ParamBgm, ToDb(Bgm)); }

        private static void ApplySfx() { if (_mixer != null) _mixer.SetFloat(ParamSfx, ToDb(Sfx)); }

        // 0..1 tuyến tính → dB cho mixer: 1 → 0dB, 0.5 → ~-6dB, 0 → -80dB (mute). Log scale khớp cảm nhận tai
        // người — kéo slider nửa chừng nghe "nhỏ đi một nửa" thay vì gần như tắt.
        private static float ToDb(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
    }
}
