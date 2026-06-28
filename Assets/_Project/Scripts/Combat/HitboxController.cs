using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// Bật/tắt collider hitbox theo Animation Event (§3.4). MonoBehaviour trên prefab nhân vật. Nghe
    /// C# event PER-INSTANCE từ <see cref="AnimationEventRelay"/> (HitboxOnEvent/HitboxOffEvent) →
    /// enable/disable đúng <see cref="HitboxAuthoring"/> theo index. KHÔNG đếm frame (timing đến từ
    /// clip qua relay). 1 FILE 1 VIỆC: chỉ bật/tắt + reset hit-ignore + truyền MoveData hiện hành.
    /// Phát hiện va chạm = HitboxAuthoring (§3.5); input-gate = AttackState; entry = CombatController.
    ///
    /// CHỐNG DOUBLE-HIT: mỗi HitboxOn → reset ignore-set của hitbox đó (1 active-window = 1 hit/victim).
    /// Đòn nhiều hit = nhiều cặp On/Off trên clip → mỗi On reset → trúng lại.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitboxController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("AnimationEventRelay cùng object Animator. Bỏ trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Các hitbox child (index khớp tham số Animation Event HitboxOn(int)). Hitbox0 = index 0.")]
        [SerializeField] private HitboxAuthoring[] hitboxes;

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
        }

        private void OnEnable()
        {
            if (relay != null)
            {
                relay.HitboxOnEvent += OnHitboxOn;
                relay.HitboxOffEvent += OnHitboxOff;
            }
            // An toàn: hitbox luôn tắt mặc định lúc bật object (tránh "kẹt bật" sau disable giữa active).
            DisableAll();
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.HitboxOnEvent -= OnHitboxOn;
                relay.HitboxOffEvent -= OnHitboxOff;
            }
            DisableAll();
        }

        /// <summary>CombatController gọi khi kích đòn (TRƯỚC HitboxOn) → mọi hitbox biết tham số đòn hiện hành.</summary>
        public void SetCurrentMove(MoveData move)
        {
            if (hitboxes == null) return;
            for (int i = 0; i < hitboxes.Length; i++)
                if (hitboxes[i] != null) hitboxes[i].SetMove(move);
        }

        private void OnHitboxOn(int index)
        {
            HitboxAuthoring hb = Get(index);
            if (hb == null) return;
            hb.ResetIgnore();       // mỗi active-window = 1 hit/victim (§3.6)
            hb.SetEnabled(true);
        }

        private void OnHitboxOff(int index)
        {
            HitboxAuthoring hb = Get(index);
            if (hb != null) hb.SetEnabled(false);
        }

        private HitboxAuthoring Get(int index)
        {
            if (hitboxes == null || index < 0 || index >= hitboxes.Length) return null;
            return hitboxes[index];
        }

        private void DisableAll()
        {
            if (hitboxes == null) return;
            for (int i = 0; i < hitboxes.Length; i++)
                if (hitboxes[i] != null) hitboxes[i].SetEnabled(false);
        }
    }
}
