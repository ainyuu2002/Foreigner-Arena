using System.Collections;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (P13) SPAWNER RAGE ORB — spawn 1 LẦN/TRẬN tại thời điểm RANDOM (research §3.6). Giữ cờ _spawned; reset
    /// khi vào trận mới qua VoidEventChannelSO "OnMatchStart" (P12 MatchManager raise — nếu chưa nối thì cờ
    /// reset ở OnEnable scene test). KHÔNG ref FA.GameFlow: chỉ subscribe channel (Core, §1.3).
    ///
    /// Vị trí spawn = tâm vùng bay trong RageOrbConfigSO (orb tự chọn hướng bay ngay). Gắn 1 cái trong scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RageOrbSpawner : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [Tooltip("Prefab Rage Orb (có component RageOrb + Rigidbody2D + child Hurtbox).")]
        [SerializeField] private RageOrb orbPrefab;

        [Tooltip("RageOrbConfigSO — dùng tâm vùng bay làm điểm spawn. BẮT BUỘC gán (cũng gán vào prefab).")]
        [SerializeField] private RageOrbConfigSO config;

        [Header("Thời điểm spawn (CẦN TEST)")]
        [Tooltip("Sớm nhất sau khi trận bắt đầu (giây) trước khi orb có thể xuất hiện.")]
        [Min(0f)] [SerializeField] private float earliestSeconds = 8f;

        [Tooltip("Muộn nhất (giây). Orb spawn tại thời điểm random [earliest, latest].")]
        [Min(0f)] [SerializeField] private float latestSeconds = 30f;

        [Header("(Tùy) Reset 1-lần/trận qua P12")]
        [Tooltip("VoidEventChannelSO 'OnMatchStart' — MatchManager P12 raise khi vào trận mới → reset cờ + hẹn giờ lại.")]
        [SerializeField] private VoidEventChannelSO onMatchStart;

        [Tooltip("Tự bắt đầu hẹn giờ ở scene test (không cần MatchManager). Tắt nếu để P12 điều khiển.")]
        [SerializeField] private bool autoStartOnEnable = true;

        private bool _spawned;
        private Coroutine _timer;

        private void OnEnable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised += BeginMatch;
            if (autoStartOnEnable) BeginMatch();
        }

        private void OnDisable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised -= BeginMatch;
            if (_timer != null) { StopCoroutine(_timer); _timer = null; }
        }

        /// <summary>Vào trận mới: reset cờ + hẹn giờ spawn ngẫu nhiên (P12 gọi qua OnMatchStart).</summary>
        public void BeginMatch()
        {
            _spawned = false;
            if (_timer != null) StopCoroutine(_timer);
            _timer = StartCoroutine(SpawnTimer());
        }

        private IEnumerator SpawnTimer()
        {
            float delay = Random.Range(earliestSeconds, Mathf.Max(earliestSeconds, latestSeconds));
            yield return new WaitForSeconds(delay);
            SpawnOrb();
        }

        private void SpawnOrb()
        {
            if (_spawned) return;
            if (orbPrefab == null || config == null)
            {
                Debug.LogError("[RageOrbSpawner] Thiếu orbPrefab hoặc config.", this);
                return;
            }
            _spawned = true;
            Vector3 pos = new Vector3(config.boundsCenter.x, config.boundsCenter.y, 0f);
            Instantiate(orbPrefab, pos, Quaternion.identity);
        }

        /// <summary>DEBUG/test — ép spawn orb ngay (nút test). Bỏ qua hẹn giờ (vẫn 1 lần/trận).</summary>
        public void DebugSpawnNow() => SpawnOrb();
    }
}
