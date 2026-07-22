using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Movement
{
    /// <summary>
    /// RƠI XUYÊN ONE-WAY PLATFORM kiểu Smash Bros (double-tap ↓). Hệ thống DÙNG CHUNG — CharacterMotor
    /// tự gắn component này (Awake AddComponent) nên áp dụng cho MỌI nhân vật, KHÔNG wire tay prefab.
    ///
    /// Unity-native: TẮT va chạm giữa collider thân nhân vật và (các) collider platform ĐANG THỰC SỰ
    /// CHẠM bằng <c>Physics2D.IgnoreCollision</c> (per-collider-pair) trong <see cref="_restoreAt"/> giây
    /// rồi BẬT LẠI. KHÔNG cần biết trước platform nào — tự tìm qua <c>Collider2D.GetContacts</c> mỗi lần
    /// TryDrop (§3.1, Unity-native). Motor chỉ điều phối: phát hiện double-tap ↓ (CommandParser) rồi gọi
    /// <see cref="TryDrop"/>; logic thật ở đây.
    ///
    /// LƯU Ý QUAN TRỌNG (gotcha đã gặp — xem FixPlatformEffectorMaskTool.cs): one-way platform trong dự
    /// án dùng <c>PlatformEffector2D.colliderMask</c> để lọc ai được hưởng hiệu ứng 1-chiều — mask này
    /// TỰ QUẢN LÝ contact ở tầng effector, ĐÈ LÊN <c>Physics2D.IgnoreLayerCollision</c> (tắt theo LAYER
    /// không ăn thua vì effector áp lại va chạm cho mọi collider nằm trong mask của nó, kể cả khi layer
    /// đã bị ignore). PHẢI dùng <c>Physics2D.IgnoreCollision</c> theo ĐÚNG CẶP collider — filter này nằm
    /// dưới tầng effector nên effector không có gì để áp lại.
    ///
    /// Chỉ ref FA.Core (GameLayers) — đúng module FA.Movement, KHÔNG ref chéo module khác (§2).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlatformDropThrough : MonoBehaviour
    {
        [Tooltip("Collider VẬT LÝ thân nhân vật (để kiểm 'đang đứng trên one-way'). Bỏ trống = collider non-trigger đầu tiên.")]
        [SerializeField] private Collider2D bodyCollider;

        private Rigidbody2D _body;   // để WakeUp() — Box2D auto-sleep body đứng yên lâu → tắt va chạm
                                      // KHÔNG tự đánh thức → không rơi (gotcha đã gặp, xác nhận qua debug).
        private bool _ignoring;      // đang trong cửa sổ tắt va chạm?
        private float _restoreAt;    // Time.time để bật lại va chạm.

        private ContactFilter2D _oneWayFilter;                 // lọc contact theo Layer OneWayPlatform.
        private readonly List<Collider2D> _contactsBuf = new List<Collider2D>(4);   // scratch, KHÔNG cấp phát mỗi frame.
        private readonly List<Collider2D> _ignoredPlatforms = new List<Collider2D>(4); // platform đang bị tắt va chạm.

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            if (bodyCollider == null)
            {
                foreach (var c in GetComponentsInChildren<Collider2D>())
                    if (!c.isTrigger) { bodyCollider = c; break; }
            }
            _oneWayFilter = new ContactFilter2D();
            _oneWayFilter.useTriggers = false;
            _oneWayFilter.SetLayerMask(GameLayers.OneWayPlatformMask);
        }

        /// <summary>
        /// True nếu thân đang THỰC SỰ chạm bề mặt Layer OneWayPlatform (đứng trên platform 1 chiều). Gate
        /// để double-tap ↓ trên sàn Ground cứng thì no-op (không kích drop).
        /// </summary>
        public bool IsOnOneWayPlatform()
        {
            return bodyCollider != null && bodyCollider.IsTouchingLayers(GameLayers.OneWayPlatformMask);
        }

        /// <summary>
        /// Kích rơi xuyên: tìm (các) collider OneWayPlatform ĐANG THỰC SỰ CHẠM thân nhân vật rồi
        /// <c>Physics2D.IgnoreCollision</c> từng cặp trong <paramref name="ignoreSeconds"/> giây (re-trigger
        /// chỉ RESET timer + gộp thêm platform mới chạm, không nest). Trả false nếu không chạm platform nào.
        /// </summary>
        public bool TryDrop(float ignoreSeconds)
        {
            if (bodyCollider == null) return false;

            _contactsBuf.Clear();
            int count = bodyCollider.GetContacts(_oneWayFilter, _contactsBuf);
            if (count == 0) return false;

            foreach (Collider2D platform in _contactsBuf)
            {
                if (platform == null || _ignoredPlatforms.Contains(platform)) continue;
                Physics2D.IgnoreCollision(bodyCollider, platform, true);
                _ignoredPlatforms.Add(platform);
            }

            _ignoring = true;
            _restoreAt = Time.time + ignoreSeconds;
            // Đứng yên lâu → Rigidbody2D tự sleep (Box2D auto-sleep, tối ưu hiệu năng mặc định của Unity).
            // Tắt va chạm KHÔNG tự đánh thức body — phải WakeUp() tường minh để trọng lực bắt đầu tích
            // phân lại, nếu không velocity giữ nguyên 0 mãi dù hết va chạm (đã xác nhận qua debug log).
            if (_body != null) _body.WakeUp();
            return true;
        }

        private void Update()
        {
            if (_ignoring && Time.time >= _restoreAt)
                RestoreNow();
        }

        private void OnDisable()  => RestoreNow();
        private void OnDestroy()  => RestoreNow();

        private void RestoreNow()
        {
            if (bodyCollider != null)
            {
                foreach (Collider2D platform in _ignoredPlatforms)
                    if (platform != null) Physics2D.IgnoreCollision(bodyCollider, platform, false);
            }
            _ignoredPlatforms.Clear();
            _ignoring = false;
        }
    }
}
