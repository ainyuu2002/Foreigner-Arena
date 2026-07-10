using UnityEngine;

namespace FA.Specials
{
    /// <summary>
    /// THIẾT LẬP LÚC PHÓNG cho projectile bay VÒNG CUNG + XOAY (kiếm ném Special 3 — MUGEN Archer
    /// state 1205: vx=5, vy=-7, gravity 0.4/tick). Chạy ở <c>Start()</c> = SAU khi ProjectileSpawner
    /// đã set vx theo facing (spawner GIỮ NGUYÊN vy — xem ProjectileSpawner.OnSpawnProjectile):
    ///  1) vy = <see cref="launchUpSpeed"/> (cung đi lên rồi rơi theo gravityScale của Rigidbody2D —
    ///     KHÔNG tự tích phân §1.1).
    ///  2) Xoay = <see cref="Rigidbody2D.angularVelocity"/> (spin vật lý Unity-native, chiều theo
    ///     hướng bay).
    ///  3) BẬT hitbox con: HitboxAuthoring.Awake mặc định TẮT collider (nhân vật bật bằng HitboxOn),
    ///     projectile không có Animation Event → bật qua SendMessage "SetEnabled" (không ref
    ///     FA.Combat §1.3). Chống double-hit vẫn do ignore-set của HitboxAuthoring + despawn-on-hit.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class ProjectileLaunchSetup : MonoBehaviour
    {
        [Tooltip("Vận tốc LÊN lúc phóng (units/s). MUGEN Archer: 7px/tick ≈ 4.2. CẦN TEST.")]
        [SerializeField] private float launchUpSpeed = 4.2f;

        [Tooltip("Tốc độ xoay (độ/giây, dấu tự đảo theo chiều bay). 0 = không xoay. CẦN TEST.")]
        [SerializeField] private float spinDegPerSec = 720f;

        [Tooltip("(Tùy chọn) GHI ĐÈ tốc độ bay ngang (units/s). 0 = giữ tốc độ ProjectileSpawner đã set. " +
                 "Dùng khi 1 nhân vật có nhiều loại đạn tốc độ khác nhau (spawner chỉ có 1 speed chung) " +
                 "— vd blade barrage Super 1 nhanh hơn kiếm ném Special 3. CẦN TEST.")]
        [Min(0f)] [SerializeField] private float overrideForwardSpeed = 0f;

        [Tooltip("Nhiễu ngẫu nhiên vận tốc DỌC ±jitter (units/s) — loạt bắn nhiều viên tỏa nhẹ cho tự " +
                 "nhiên (MUGEN volley bắn từ nhiều vị trí). 0 = tắt.")]
        [Min(0f)] [SerializeField] private float upSpeedJitter = 0f;

        // Độ phóng theo NGƯỜI BẮN (nhân vật ×2.5) — ProjectileSpawner BroadcastMessage TRƯỚC Start.
        private float _speedScale = 1f;

        /// <summary>ProjectileSpawner gọi (BroadcastMessage) — nhân tốc độ riêng theo scale người bắn.</summary>
        public void SetSpeedScale(float s) => _speedScale = Mathf.Max(0.01f, s);

        private void Start()
        {
            var rb = GetComponent<Rigidbody2D>();

            float vx = rb.linearVelocity.x;
            if (overrideForwardSpeed > 0f)
                vx = (vx < 0f ? -1f : 1f) * overrideForwardSpeed * _speedScale; // giữ CHIỀU spawner đã set.

            float vy = launchUpSpeed * _speedScale;
            if (upSpeedJitter > 0f) vy += Random.Range(-upSpeedJitter, upSpeedJitter) * _speedScale;

            rb.linearVelocity = new Vector2(vx, vy);

            // Kiếm chém XUÔI chiều bay: bay phải = xoay theo chiều kim đồng hồ (âm).
            rb.angularVelocity = (vx >= 0f ? -1f : 1f) * spinDegPerSec;

            BroadcastMessage("SetEnabled", true, SendMessageOptions.DontRequireReceiver);
        }
    }
}
