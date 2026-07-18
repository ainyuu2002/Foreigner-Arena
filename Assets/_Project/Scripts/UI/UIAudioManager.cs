using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FA.UI
{
    /// <summary>
    /// (P12 Audio) TRUNG TÂM ÂM THANH UI — tự khởi động qua <see cref="RuntimeInitializeOnLoadMethodAttribute"/>
    /// (không cần object nào trong scene, sống xuyên scene bằng DontDestroyOnLoad), gồm 3 nhiệm vụ:
    ///   1. SFX NÚT: mỗi lần load scene, quét mọi <see cref="Button"/> (kể cả inactive) và gắn
    ///      <see cref="UIButtonSfx"/> lúc RUNTIME — không đụng prefab/builder, không dirty asset.
    ///      Panel sinh nút động sau đó thì gọi <see cref="Rescan"/>.
    ///   2. BGM THEO SCENE: phát clip Resources tên "BGM_&lt;tên scene&gt;" (vd <c>BGM_Lobby</c>), loop;
    ///      scene không có file tương ứng → tắt nhạc (match scene im lặng là chủ đích).
    ///   3. Route 2 AudioSource của chính nó vào group BGM/SFX của FA_AudioMixer → ăn slider Settings.
    /// Clip nạp từ Assets/_Project/Audio/Resources/: <c>UI_Click</c>, <c>UI_Hover</c>, <c>BGM_*</c>
    /// (wav/ogg/mp3 đều được — Resources.Load theo tên, không cần đuôi). THIẾU file vẫn chạy êm
    /// (im lặng + marker log báo thiếu). KHÔNG đụng code gameplay (§1.3).
    /// </summary>
    public sealed class UIAudioManager : MonoBehaviour
    {
        private const string ClipClick = "UI_Click";
        private const string ClipHover = "UI_Hover";
        private const string BgmPrefix = "BGM_";

        private static UIAudioManager _instance;

        private AudioSource _sfxSource;
        private AudioSource _bgmSource;
        private AudioClip _click;
        private AudioClip _hover;
        private string _currentBgm; // tên resource đang phát — tránh restart khi load lại cùng scene.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("[FA] UIAudioManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UIAudioManager>();
        }

        /// <summary>Nút click — gọi từ <see cref="UIButtonSfx"/>. Null-safe mọi nhánh.</summary>
        public static void PlayClick()
        {
            if (_instance != null && _instance._click != null)
                _instance._sfxSource.PlayOneShot(_instance._click);
        }

        /// <summary>Hover chuột / chọn bằng gamepad — gọi từ <see cref="UIButtonSfx"/>.</summary>
        public static void PlayHover()
        {
            if (_instance != null && _instance._hover != null)
                _instance._sfxSource.PlayOneShot(_instance._hover);
        }

        /// <summary>Gắn lại SFX cho nút SINH ĐỘNG sau khi scene load (panel spawn nút mới thì gọi cái này).</summary>
        public static void Rescan() { if (_instance != null) _instance.WireButtons(); }

        private void Awake()
        {
            var mixer = Resources.Load<AudioMixer>(GameSettings.MixerResourcePath);

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;

            if (mixer != null)
            {
                var sfx = mixer.FindMatchingGroups("SFX");
                var bgm = mixer.FindMatchingGroups("BGM");
                if (sfx.Length > 0) _sfxSource.outputAudioMixerGroup = sfx[0];
                if (bgm.Length > 0) _bgmSource.outputAudioMixerGroup = bgm[0];
            }

            _click = Resources.Load<AudioClip>(ClipClick);
            _hover = Resources.Load<AudioClip>(ClipHover);

            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single); // scene đầu đã load xong trước Boot.

            // Marker xác nhận (đối chiếu Console khi test — như AUDIO v3 của GameSettings).
            Debug.Log($"[FA.Audio] UIAUDIO v1 — mixer: {(mixer != null ? "OK" : "THIẾU")}, " +
                      $"{ClipClick}: {(_click != null ? "OK" : "thiếu")}, {ClipHover}: {(_hover != null ? "OK" : "thiếu")}");
        }

        private void OnDestroy()
        {
            if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WireButtons();
            PlayBgmForScene(scene.name);
        }

        private void WireButtons()
        {
            // Include cả inactive — panel đang tắt (Settings, Pause...) vẫn được gắn sẵn.
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (button.GetComponent<UIButtonSfx>() == null)
                    button.gameObject.AddComponent<UIButtonSfx>();
        }

        private void PlayBgmForScene(string sceneName)
        {
            string resource = BgmPrefix + sceneName;
            if (resource == _currentBgm) return; // cùng track (vd load lại Lobby) → phát tiếp, không restart.

            var clip = Resources.Load<AudioClip>(resource);
            _currentBgm = resource;
            _bgmSource.Stop();
            if (clip != null)
            {
                _bgmSource.clip = clip;
                _bgmSource.Play();
                Debug.Log($"[FA.Audio] BGM ▶ {resource}");
            }
            // Không có file cho scene này → im lặng là chủ đích (match scene chưa có nhạc trong scope).
        }
    }
}
