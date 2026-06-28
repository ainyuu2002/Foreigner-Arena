using UnityEngine;
using FA.Core;

namespace FA.Combat
{
    /// <summary>
    /// PHÁT HIỆN va tường/sàn tốc-độ-cao KHI ĐANG TUMBLE (P7, research §3.4) → đổi sang WallSplat /
    /// GroundBounce. Nhỏ, 1 chức năng: <see cref="OnCollisionEnter2D"/> đo <c>relativeVelocity</c>, so
    /// ngưỡng config, phân biệt tường vs sàn theo Layer (GameLayers — Core). CHỈ hoạt động khi
    /// <see cref="KnockbackController.IsTumbling"/> (di chuyển thường va tường/sàn KHÔNG trigger → không
    /// hồi quy P1–P3).
    ///
    /// Nảy vật lý do PhysicsMaterial2D (KnockbackController swap material lúc tumble) — relay KHÔNG tự
    /// phản xạ vector (§1.1), chỉ ĐỔI STATE (animation + stun). Gắn cùng object có CapsuleCollider2D thân
    /// (collider va chạm với tường/sàn cần là collider VẬT LÝ, không phải hitbox trigger). Đặt FA.Combat,
    /// chỉ ref Core (GameLayers).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KnockbackCollisionRelay : MonoBehaviour
    {
        [Header("Tham chiếu (cùng prefab)")]
        [Tooltip("KnockbackController — IsTumbling gate + EnterWallSplat/EnterGroundBounce. Bỏ trống = tìm trên root.")]
        [SerializeField] private KnockbackController knockback;

        [Tooltip("Config — ngưỡng tốc độ splat/bounce. Bỏ trống = lấy từ KnockbackController.Config.")]
        [SerializeField] private KnockbackConfigSO config;

        [Header("Layer va chạm (khớp Project Settings)")]
        [Tooltip("Layer TƯỜNG (va tốc-độ-cao → WallSplat).")]
        [SerializeField] private LayerMask wallLayers;

        [Tooltip("Layer SÀN (va tốc-độ-cao → GroundBounce).")]
        [SerializeField] private LayerMask groundLayers;

        private void Awake()
        {
            if (knockback == null) knockback = GetComponentInParent<KnockbackController>();
            if (config == null && knockback != null) config = knockback.Config;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (knockback == null || config == null) return;
            if (!knockback.IsTumbling) return; // CHỈ khi đang bay-không-điều-khiển.

            float speed = collision.relativeVelocity.magnitude;
            int otherLayer = collision.collider.gameObject.layer;

            // TƯỜNG tốc-độ-cao → dán tường (splat). Ưu tiên xét tường trước (đập ngang).
            if (IsInMask(otherLayer, wallLayers) && speed >= config.wallSplatSpeedThreshold)
            {
                knockback.EnterWallSplat();
                return;
            }

            // SÀN tốc-độ-cao (đặc biệt spike) → nảy lên (material lo), combo tiếp.
            if (IsInMask(otherLayer, groundLayers) && speed >= config.groundBounceSpeedThreshold)
            {
                knockback.EnterGroundBounce();
            }
        }

        private static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
    }
}
