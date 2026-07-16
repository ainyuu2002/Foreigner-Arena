using UnityEngine;
using FA.Core;

namespace FA.Effects
{
    /// <summary>
    /// VFX MỘT LẦN dạng lật sprite (hit spark, flash, glow kiểu MUGEN explod): chạy hết dãy sprite rồi
    /// TỰ HỦY. Ghi chú §1.1: VFX one-shot KHÔNG phải state nhân vật — dựng Animator riêng cho từng
    /// prefab hiệu ứng (1 controller + 1 clip + destroy-behaviour mỗi cái) nặng asset hơn hẳn 1 dãy
    /// sprite serialize; component này chỉ ĐIỀU PHỐI (cycle + destroy), không thay thế hệ Animator của
    /// nhân vật. Hỗ trợ loop (mũi tên bay/aura) + lifeSeconds; phức tạp hơn nữa (blend) → Animator/Particle.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FlipbookOneShot : MonoBehaviour
    {
        [Tooltip("Dãy sprite phát theo thứ tự, hết dãy tự Destroy.")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Tốc độ lật (frame hiệu ứng / giây). MUGEN 2 tick/hình ≈ 30fps.")]
        [Min(1f)] [SerializeField] private float framesPerSecond = 30f;

        [Tooltip("LẶP vô hạn thay vì tự hủy sau 1 lượt (mũi tên bay, aura charge). Kết hợp " +
                 "lifeSeconds để tự tắt, hoặc để 0 nếu sống theo object cha (projectile).")]
        [SerializeField] private bool loop;

        [Tooltip("Khi loop: lặp TỪ chỉ số này (LoopStart của MUGEN AIR — intro chạy 1 lần, chỉ lặp " +
                 "phần sau; xem gotcha bake anim). 0 = lặp cả dãy như cũ.")]
        [Min(0)] [SerializeField] private int loopStartIndex;

        [Tooltip("Tự hủy sau bấy nhiêu giây unscaled (0 = không giới hạn — one-shot vẫn tự hủy khi hết dãy).")]
        [Min(0f)] [SerializeField] private float lifeSeconds;

        private SpriteRenderer _sr;
        private float _t;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        private void OnEnable()
        {
            _t = 0f;
            if (frames != null && frames.Length > 0) _sr.sprite = frames[0];
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) { PrefabPool.Despawn(gameObject); return; }
            // UNSCALED: VFX vẫn chạy trong super flash (Time.timeScale=0). Gameplay thường không đổi
            // (timeScale=1 → unscaled == scaled); hitstop dùng Animator.speed nên không liên quan.
            _t += Time.unscaledDeltaTime;
            if (lifeSeconds > 0f && _t >= lifeSeconds) { PrefabPool.Despawn(gameObject); return; }
            int idx = (int)(_t * framesPerSecond);
            if (idx >= frames.Length)
            {
                if (!loop) { PrefabPool.Despawn(gameObject); return; }
                int start = Mathf.Clamp(loopStartIndex, 0, frames.Length - 1);
                idx = start + (idx - start) % (frames.Length - start);
            }
            _sr.sprite = frames[idx];
        }
    }
}
