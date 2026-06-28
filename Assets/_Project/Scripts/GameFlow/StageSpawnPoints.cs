using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.7) LOCATOR điểm spawn trong SCENE MAP — đặt 1 cái trong mỗi scene map P11. Giữ 3
    /// Transform: spawn P1, spawn P2 (đầu trận), và respawn point GIỮA map (mất mạng quay lại). PlayerSpawner
    /// (FA.GameFlow) đọc khi map load additive — tránh <c>GameObject.Find</c> (research §3.7): locator tự
    /// đăng ký vào <see cref="Active"/> static khi OnEnable, gỡ khi OnDisable.
    ///
    /// CHỈ giữ Transform (Unity-native) — KHÔNG logic. Map khác = đặt locator + kéo 3 empty trong Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSpawnPoints : MonoBehaviour
    {
        /// <summary>Locator của scene map đang active (PlayerSpawner đọc). 1 map active tại 1 thời điểm.</summary>
        public static StageSpawnPoints Active { get; private set; }

        [Tooltip("Empty GameObject vị trí spawn P1 đầu trận.")]
        [SerializeField] private Transform spawnP1;

        [Tooltip("Empty GameObject vị trí spawn P2 đầu trận.")]
        [SerializeField] private Transform spawnP2;

        [Tooltip("Empty GameObject điểm respawn (GIỮA map) — mất mạng quay về đây. Có thể trùng giữa 2 spawn.")]
        [SerializeField] private Transform respawnPoint;

        public Transform SpawnP1 => spawnP1;
        public Transform SpawnP2 => spawnP2;
        public Transform RespawnPoint => respawnPoint;

        public Transform SpawnFor(PlayerSlot slot) => slot == PlayerSlot.P1 ? spawnP1 : spawnP2;

        private void OnEnable() => Active = this;

        private void OnDisable() { if (Active == this) Active = null; }
    }
}
