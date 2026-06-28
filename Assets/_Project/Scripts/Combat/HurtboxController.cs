using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// Bật/tắt collider HURTBOX theo Animation Event (P10, research §3.1) — SONG SONG <see cref="HitboxController"/>
    /// (P5). Nghe C# event PER-INSTANCE từ <see cref="AnimationEventRelay"/> (HurtboxOff/On) → disable/enable
    /// Hurtbox collider. Khi Hurtbox disabled → HitboxAuthoring.OnTriggerEnter2D của ĐỊCH KHÔNG kêu →
    /// vô hiệu hit TẬN GỐC (i-frame Unity-native, không hitstop/mana/nộ cho địch). KHÔNG đếm frame
    /// (timing đến từ clip né qua relay — QUY TẮC VÀNG §1.1).
    ///
    /// Hurtbox MẶC ĐỊNH BẬT (luôn-bật trên thân P5) — chỉ TẮT trong cửa sổ né (dodge/airdodge/rcdash/burst).
    /// FAIL-SAFE: state né cũng gọi relay.HurtboxOn() ở Exit (research §4.2) → 2 lớp đảm bảo không "bất tử
    /// vĩnh viễn". 1 FILE 1 VIỆC. Đặt FA.Combat, chỉ ref Core.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HurtboxController : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("AnimationEventRelay cùng object Animator. Bỏ trống = tìm trong children.")]
        [SerializeField] private AnimationEventRelay relay;

        [Tooltip("Các collider HURTBOX (luôn-bật thân P5). TẮT khi né, BẬT khi hết né.")]
        [SerializeField] private Collider2D[] hurtboxes;

        private void Awake()
        {
            if (relay == null) relay = GetComponentInChildren<AnimationEventRelay>();
        }

        private void OnEnable()
        {
            if (relay != null)
            {
                relay.HurtboxOffEvent += OnHurtboxOff;
                relay.HurtboxOnEvent += OnHurtboxOn;
            }
            SetEnabled(true); // an toàn: hurtbox luôn bật khi object bật (tránh kẹt-tắt sau disable giữa né).
        }

        private void OnDisable()
        {
            if (relay != null)
            {
                relay.HurtboxOffEvent -= OnHurtboxOff;
                relay.HurtboxOnEvent -= OnHurtboxOn;
            }
        }

        private void OnHurtboxOff() => SetEnabled(false);
        private void OnHurtboxOn() => SetEnabled(true);

        private void SetEnabled(bool on)
        {
            if (hurtboxes == null) return;
            for (int i = 0; i < hurtboxes.Length; i++)
                if (hurtboxes[i] != null) hurtboxes[i].enabled = on;
        }
    }
}
