using System;
using FA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FA.Effects
{
    /// <summary>
    /// DÒNG CHỮ NIỆM CHÚ (UBW) bằng SPRITE GỐC MUGEN (group 1500-1507 — mỗi group là 1 dòng, mỗi index
    /// là 1 frame "viết dần" 1 tick/frame): nghe Animation Event <c>ChantLine(Int)</c> → chạy flipbook
    /// dòng đó trên Image (60fps unscaled, GIỮ frame cuối tới dòng kế/ChantEnd) + PHÁT VOICE của dòng
    /// (1 event điều khiển cả chữ lẫn tiếng — đồng bộ tuyệt đối). Canvas do editor tool dựng (sorting
    /// 400 — dưới cut-in flash 500); SuperFlash tắt mọi Canvas khi mở → ShowLine tự bật lại để 2 dòng
    /// cuối ("So as I pray…") vẫn hiện TRONG flash.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChantTextOverlay : MonoBehaviour
    {
        [Serializable]
        public sealed class Line
        {
            [Tooltip("Dãy frame 'viết dần' của dòng (sprite MUGEN 1 tick/frame — phát 60fps).")]
            public Sprite[] frames;

            [Tooltip("Voice đọc dòng này (phát cùng lúc chữ bắt đầu chạy). Trống = chỉ chữ.")]
            public AudioClip voice;
        }

        [Tooltip("AnimationEventRelay cùng prefab. Trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("uGUI Image hiển thị dòng chú (editor tool dựng sẵn trong Canvas con).")]
        [SerializeField] private Image image;

        [Tooltip("AudioSource phát voice. Trống = tìm trên root.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("Các dòng niệm chú — Animation Event ChantLine(Int) chọn theo chỉ số.")]
        [SerializeField] private Line[] lines;

        [Tooltip("Tốc độ chạy chữ MẶC ĐỊNH khi dòng KHÔNG có voice (frame/giây). Dòng CÓ voice tự kéo " +
                 "dãn: chữ viết xong ĐÚNG lúc voice đọc xong (user chốt).")]
        [Min(1f)] [SerializeField] private float framesPerSecond = 30f;

        [Tooltip("Phóng sprite chữ theo canvas 1920×1080 (sprite gốc ~300-480px). CẦN TEST.")]
        [Min(0.1f)] [SerializeField] private float spriteScale = 2.4f;

        [Range(0f, 1f)] [SerializeField] private float voiceVolume = 1f;

        private Canvas _canvas;
        private Sprite[] _frames;
        private float _fps;   // tốc độ dòng hiện tại (kéo dãn theo độ dài voice).
        private float _t;
        private bool _playing;

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (image != null) _canvas = image.GetComponentInParent<Canvas>();
            if (_canvas != null) _canvas.enabled = false;   // chỉ hiện trong lúc niệm chú.
        }

        private void OnEnable()
        {
            if (relay == null) return;
            relay.ChantLineEvent += ShowLine;
            relay.ChantEndEvent += Hide;
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.ChantLineEvent -= ShowLine;
                relay.ChantEndEvent -= Hide;
            }
            Hide();
        }

        private void ShowLine(int index)
        {
            if (image == null || lines == null || index < 0 || index >= lines.Length) return;
            Line line = lines[index];
            if (line == null || line.frames == null || line.frames.Length == 0) return;
            if (_canvas != null) _canvas.enabled = true;   // SuperFlash có thể vừa tắt mọi canvas.
            _frames = line.frames;
            // Dòng có voice → kéo dãn: chữ viết hết dãy frame ĐÚNG bằng thời lượng voice (user chốt
            // "anim chữ ngang độ dài voice"). Không voice → tốc độ mặc định.
            _fps = line.voice != null && line.voice.length > 0.05f
                ? line.frames.Length / line.voice.length
                : framesPerSecond;
            _t = 0f;
            _playing = true;
            SetFrame(0);
            if (line.voice != null && audioSource != null)
                audioSource.PlayOneShot(line.voice, voiceVolume);
        }

        private void Hide()
        {
            _playing = false;
            _frames = null;
            if (_canvas != null) _canvas.enabled = false;
        }

        private void Update()
        {
            if (!_playing || _frames == null) return;
            _t += Time.unscaledDeltaTime;   // chant freeze thế giới — chữ chạy unscaled.
            int idx = Mathf.Min((int)(_t * _fps), _frames.Length - 1);   // GIỮ frame cuối.
            SetFrame(idx);
        }

        private void SetFrame(int idx)
        {
            Sprite s = _frames[idx];
            if (s == null) return;
            image.sprite = s;
            image.SetNativeSize();
            image.rectTransform.sizeDelta *= spriteScale;
        }
    }
}
