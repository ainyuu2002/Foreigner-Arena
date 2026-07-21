using System.Collections;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>Spawn Rage Orb theo chế độ trận đấu hoặc chế độ test.</summary>
    [DisallowMultipleComponent]
    public sealed class RageOrbSpawner : MonoBehaviour
    {
        [Header("Tham chiếu")]
        [SerializeField] private RageOrb orbPrefab;

        [SerializeField] private RageOrbConfigSO config;

        [Header("Thời điểm spawn")]
        [Min(0f)] [SerializeField] private float earliestSeconds = 45f;

        [Min(0f)] [SerializeField] private float latestSeconds = 60f;

        [Header("Event trận đấu")]
        [SerializeField] private VoidEventChannelSO onMatchStart;

        [SerializeField] private VoidEventChannelSO onMatchEnded;

        [SerializeField] private bool autoStartOnEnable = true;

        private bool _spawned;
        private Coroutine _timer;
        private RageOrb _activeOrb;

        private void OnEnable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised += BeginMatch;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised += EndMatch;
            if (autoStartOnEnable) BeginMatch();
        }

        private void OnDisable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised -= BeginMatch;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised -= EndMatch;
            EndMatch();
        }

        /// <summary>Bắt đầu lịch spawn cho trận mới.</summary>
        public void BeginMatch()
        {
            ReleaseActiveOrb();
            _spawned = false;
            if (_timer != null) StopCoroutine(_timer);
            _timer = StartCoroutine(SpawnTimer());
        }

        private IEnumerator SpawnTimer()
        {
            float delay = Random.Range(earliestSeconds, Mathf.Max(earliestSeconds, latestSeconds));
            yield return new WaitForSeconds(delay);
            _timer = null;
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
            GameObject instance = PrefabPool.Spawn(orbPrefab.gameObject, pos, Quaternion.identity);
            _activeOrb = instance != null ? instance.GetComponent<RageOrb>() : null;
            if (_activeOrb != null) _activeOrb.Finished += OnOrbFinished;
        }

        public void EndMatch()
        {
            if (_timer != null) StopCoroutine(_timer);
            _timer = null;
            ReleaseActiveOrb();
        }

        private void OnOrbFinished(RageOrb orb)
        {
            if (!ReferenceEquals(_activeOrb, orb)) return;
            _activeOrb.Finished -= OnOrbFinished;
            _activeOrb = null;
        }

        private void ReleaseActiveOrb()
        {
            if (_activeOrb == null) return;
            RageOrb orb = _activeOrb;
            _activeOrb = null;
            orb.Finished -= OnOrbFinished;
            if (orb.gameObject.activeSelf) orb.Despawn();
        }
    }
}
