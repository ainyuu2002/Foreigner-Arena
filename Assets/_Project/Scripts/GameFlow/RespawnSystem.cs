using UnityEngine;
using FA.Core;

namespace FA.GameFlow
{
    /// <summary>
    /// (P12, research §3.3) RESPAWN per-player — gắn lên player prefab. LivesSystem (cùng module) gọi
    /// <see cref="Respawn"/> khi chết mà CÒN mạng. Việc làm (GDD §8.1):
    ///   1. Dời về spawn point GIỮA map qua <c>Rigidbody2D.position</c> + <c>linearVelocity=0</c> (Unity vật
    ///      lý lo, KHÔNG set transform thuần — §1.1).
    ///   2. % VỀ 0 qua <c>IPercentReceiver.ResetPercent()</c> (interface Core → KHÔNG ref FA.Combat).
    ///   3. Mana/Nộ GIỮ NGUYÊN — TUYỆT ĐỐI không gọi reset (P8 không decay; cơ chế comeback). KHÔNG đụng.
    ///   4. Bật i-frame qua <see cref="RespawnInvulnerability"/> tới khi đáp đất.
    ///
    /// Spawn point lấy từ <see cref="StageSpawnPoints"/> trong scene map (đăng ký runtime) hoặc set tay qua
    /// <see cref="SetRespawnPoint"/>. KHÔNG Instantiate lại player (giữ state mana/nộ + reference HUD —
    /// research §4 cạm bẫy 7): player object SỐNG suốt trận, respawn = CHỈ reposition.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RespawnSystem : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("MatchSettingsSO — resetPercentOnRespawn, i-frame end-on-landing, max-seconds. BẮT BUỘC gán.")]
        [SerializeField] private MatchSettingsSO settings;

        [Header("Tham chiếu cùng player")]
        [Tooltip("Rigidbody2D thân player để dời vị trí. Bỏ trống = GetComponent.")]
        [SerializeField] private Rigidbody2D body;

        [Tooltip("RespawnInvulnerability trên CÙNG player. Bỏ trống = GetComponent.")]
        [SerializeField] private RespawnInvulnerability invulnerability;

        [Tooltip("(Tùy) điểm respawn cố định khi test scene phẳng (không có StageSpawnPoints). " +
                 "Khi vào map thật → MatchManager/Spawner set qua SetRespawnPoint.")]
        [SerializeField] private Transform fallbackRespawnPoint;

        private Transform _respawnPoint;

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (invulnerability == null) invulnerability = GetComponent<RespawnInvulnerability>();
            if (_respawnPoint == null) _respawnPoint = fallbackRespawnPoint;
        }

        /// <summary>MatchManager/PlayerSpawner set điểm respawn của map đang chơi (giữa map).</summary>
        public void SetRespawnPoint(Transform point)
        {
            if (point != null) _respawnPoint = point;
        }

        /// <summary>
        /// Thực hiện respawn (LivesSystem gọi). Reposition + % về 0 + i-frame. KHÔNG đụng mana/nộ.
        /// </summary>
        public void Respawn()
        {
            // 1. Reposition (Unity-native — qua Rigidbody2D).
            Vector3 pos = _respawnPoint != null ? _respawnPoint.position : new Vector3(0f, 5f, 0f);
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = pos;
            }
            else
            {
                transform.position = pos;
            }

            // 2. % về 0 qua interface Core (không ref Combat). GetComponentInChildren vì KnockbackPercent
            //    có thể nằm trên root/child tuỳ prefab.
            bool resetPercent = settings == null || settings.resetPercentOnRespawn;
            if (resetPercent)
            {
                IPercentReceiver pr = GetComponentInChildren<IPercentReceiver>();
                pr?.ResetPercent();
            }

            // 3. Mana/Nộ: KHÔNG làm gì (giữ nguyên — GDD comeback).

            // 4. i-frame tới khi đáp.
            if (invulnerability != null)
            {
                bool endOnLanding = settings == null || settings.iFrameEndsOnLanding;
                float maxSec = settings != null ? settings.iFrameMaxSeconds : 3f;
                invulnerability.Begin(endOnLanding, maxSec);
            }
        }
    }
}
