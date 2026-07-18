using UnityEngine;
using FA.Core;

namespace FA.Stage
{
    /// <summary>
    /// VÙNG CHẾT (P11, research §3.1) — Smash blast-zone. 1 chức năng: <see cref="OnTriggerEnter2D"/> khi
    /// PLAYER chạm → raise <see cref="GameObjectEventChannelSO"/> mang victim (root). P12 LivesSystem
    /// subscribe để trừ mạng/respawn — Stage KHÔNG xử lý mạng (chống cross-module §1.3).
    ///
    /// Unity-native lo phần "ra ngoài màn": đây là <c>BoxCollider2D isTrigger</c> Layer DeathZone đặt XA
    /// rìa tường (collision matrix DeathZone↔Player1/Player2). KHÔNG so toạ độ trong Update (§1.1). Lava
    /// dâng (RisingLava) cũng dùng component này (Layer DeathZone) → chạm lava = chết, tái dùng cùng kênh.
    ///
    /// Cờ debug self-respawn (P12 chưa có LivesSystem) để TEST P11 độc lập: teleport victim về spawnPoint
    /// (đừng để player rơi mãi). Khi P12 vào → tắt debug, để LivesSystem qua kênh lo.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DeathZone : MonoBehaviour
    {
        [Header("Kênh sự kiện")]
        [Tooltip("OnPlayerDiedChannel (GameObjectEventChannelSO) — raise victim root khi chạm. P12 subscribe. " +
                 "Có thể bỏ trống nếu CHỈ dùng debug self-respawn để test.")]
        [SerializeField] private GameObjectEventChannelSO onPlayerDied;

        [Header("Lọc đối tượng")]
        [Tooltip("Layer được tính là PLAYER (Player1|Player2). Trigger khác (item...) bỏ qua. " +
                 "Để trống = mọi thứ chạm đều raise (không khuyến nghị).")]
        [SerializeField] private LayerMask playerLayers;

        [Header("Debug (P11 — P12 chưa có mạng)")]
        [Tooltip("Bật: tự teleport victim về spawnPoint để test P11 độc lập (không rơi mãi). " +
                 "Tắt khi P12 LivesSystem nắm việc respawn.")]
        [SerializeField] private bool debugSelfRespawn = true;

        [Tooltip("Điểm respawn cho debug self-respawn. Bỏ trống = về world (0,5,0).")]
        [SerializeField] private Transform debugSpawnPoint;

        [Tooltip("Log ra console khi player chạm vùng chết (debug).")]
        [SerializeField] private bool logOnEnter = true;

        private void OnTriggerEnter2D(Collider2D other)
        {
            GameObject victim = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject
                : other.transform.root.gameObject;

            // Lọc theo layer player (Unity-native filter; matrix đã lọc thô, đây lọc tinh trên root).
            if (playerLayers.value != 0 && !IsInMask(victim.layer, playerLayers) && !IsInMask(other.gameObject.layer, playerLayers))
                return;

            if (logOnEnter)
                DebugLog.Log($"[DeathZone] '{victim.name}' chạm vùng chết '{name}'.", this);

            if (onPlayerDied != null)
                onPlayerDied.RaiseEvent(victim);

            if (debugSelfRespawn)
                Respawn(victim);
        }

        private void Respawn(GameObject victim)
        {
            Vector3 pos = debugSpawnPoint != null ? debugSpawnPoint.position : new Vector3(0f, 5f, 0f);
            Rigidbody2D rb = victim.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.position = pos;       // dời qua Rigidbody2D (Unity vật lý lo) thay vì transform thuần.
            }
            else
            {
                victim.transform.position = pos;
            }
        }

        private static bool IsInMask(int layer, LayerMask mask) => (mask.value & (1 << layer)) != 0;
    }
}
