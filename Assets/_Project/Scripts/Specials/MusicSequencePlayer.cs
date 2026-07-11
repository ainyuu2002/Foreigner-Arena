using FA.Core;
using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// NHẠC NỀN chuỗi cho Ultimate UBW (user chốt): 3 đoạn (S3002-0 → 5 → 6) chạy LIỀN TÙ TÌ từ lúc
    /// bắt đầu niệm chú (nghe relay <c>ChantStart</c>) tới khi thoát UBW (UBWInstallController gọi
    /// <see cref="StopMusic"/> lúc màn đen chuyển cảnh). Đây là KÊNH NHẠC NỀN — không phải SFX:
    /// AudioSource riêng, sau này route vào AudioMixer group "Music" khi có menu chỉnh âm lượng,
    /// và sẽ THAY nhạc nền của game (pause/resume BGM map — chờ hệ nhạc nền chung).
    /// Hết chuỗi → loop lại từ <see cref="loopFromIndex"/> (bỏ đoạn intro). Chạy xuyên freeze
    /// (audio không phụ thuộc Time.timeScale).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicSequencePlayer : MonoBehaviour
    {
        [Tooltip("AnimationEventRelay cùng prefab (nghe ChantStart). Trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Chuỗi đoạn nhạc phát nối tiếp (UBW: S3002-0, S3002-5, S3002-6).")]
        [SerializeField] private AudioClip[] tracks;

        [Tooltip("Hết chuỗi thì quay lại phát từ đoạn này (bỏ intro). -1 = không loop.")]
        [SerializeField] private int loopFromIndex = 1;

        [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;

        private AudioSource _src;
        private int _track = -1;

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            // AudioSource RIÊNG cho nhạc (không dùng chung nguồn SFX của CharacterEffectPlayer).
            _src = gameObject.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = false;
            _src.spatialBlend = 0f;
            _src.volume = volume;
        }

        private void OnEnable()
        {
            if (relay != null) relay.ChantStartEvent += StartSequence;
        }

        private void OnDisable()
        {
            if (relay != null) relay.ChantStartEvent -= StartSequence;
            StopMusic();
        }

        /// <summary>Bắt đầu chuỗi từ đoạn 0 (ChantStart tự gọi; public để tái dùng).</summary>
        public void StartSequence()
        {
            if (tracks == null || tracks.Length == 0) return;
            _track = 0;
            _src.clip = tracks[0];
            _src.Play();
        }

        /// <summary>Dừng nhạc (UBWInstallController gọi khi thoát UBW — lúc màn đen chuyển cảnh).</summary>
        public void StopMusic()
        {
            _track = -1;
            if (_src != null) _src.Stop();
        }

        private void Update()
        {
            if (_track < 0 || _src.isPlaying) return;
            // Đoạn hiện tại vừa hết → nối đoạn kế (hết chuỗi thì loop từ loopFromIndex).
            _track++;
            if (_track >= tracks.Length)
            {
                if (loopFromIndex < 0 || loopFromIndex >= tracks.Length) { _track = -1; return; }
                _track = loopFromIndex;
            }
            _src.clip = tracks[_track];
            _src.Play();
        }
    }
}
