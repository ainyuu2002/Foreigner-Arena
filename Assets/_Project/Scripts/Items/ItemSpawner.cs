using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) SPAWNER ITEM DUY NHẤT — BAY quanh map (gộp từ MysteryBlockSpawner) rồi THẢ item từ vị
    /// trí đang bay để rơi bằng physics. Code chỉ ĐIỀU PHỐI: coroutine timer theo spawnInterval, giữ list
    /// item sống, cap maxConcurrent, chọn ItemData (weighted) rồi Instantiate prefab (data.prefabOverride ??
    /// itemPrefab — Mystery dùng prefab riêng, bình dùng prefab chung). Vùng an toàn (giữ-trong-map) +
    /// vùng BAY đều nằm trong <see cref="ItemSpawnConfigSO"/> (KHÔNG ref FA.Stage).
    ///
    /// VÒNG ĐỜI TRẬN: nghe VoidEventChannelSO onMatchStart → StartSpawning; onMatchEnded → StopSpawning
    /// (subscribe OnEnable / unsubscribe OnDisable). autoStart giữ cho scene test độc lập.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("ItemSpawnConfigSO — nhịp/cap/lifetime/safe-rect/fly-bounds/pool. BẮT BUỘC gán.")]
        [SerializeField] private ItemSpawnConfigSO config;

        [Tooltip("Prefab item CHUNG (có Item + Rigidbody2D + collider thân + child trigger Layer Item). " +
                 "ItemData.prefabOverride (nếu có) ghi đè cho item đó.")]
        [SerializeField] private Item itemPrefab;

        [Tooltip("Tự bắt đầu spawn khi scene chạy (test). Tắt nếu để onMatchStart điều khiển.")]
        [SerializeField] private bool autoStart = true;

        [Header("(Tùy) VFX spawn-in")]
        [Tooltip("ParticleSystem phát tại chỗ khi item xuất hiện. Optional.")]
        [SerializeField] private ParticleSystem spawnVfxPrefab;

        [Header("Kênh vòng đời trận (Void Event Channel)")]
        [Tooltip("onMatchStart — MatchManager raise khi vào Match → StartSpawning.")]
        [SerializeField] private VoidEventChannelSO onMatchStart;
        [Tooltip("onMatchEnded — WinCondition/MatchManager raise → StopSpawning.")]
        [SerializeField] private VoidEventChannelSO onMatchEnded;

        private readonly List<Item> _alive = new List<Item>();
        private Coroutine _loop;
        private Vector2 _velocity;
        private float _directionTimer;

        private void OnEnable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised += StartSpawning;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised += StopSpawning;

            PickNewDirection();
            if (autoStart) StartSpawning();
        }

        private void OnDisable()
        {
            if (onMatchStart != null) onMatchStart.OnEventRaised -= StartSpawning;
            if (onMatchEnded != null) onMatchEnded.OnEventRaised -= StopSpawning;
            StopSpawning();
        }

        private void Update() => MoveAroundMap();

        /// <summary>Bắt đầu vòng spawn (onMatchStart hoặc autoStart gọi).</summary>
        public void StartSpawning()
        {
            if (_loop != null) return;
            if (config == null || itemPrefab == null)
            {
                Debug.LogError("[ItemSpawner] Thiếu config hoặc itemPrefab.", this);
                return;
            }
            _loop = StartCoroutine(SpawnLoop());
        }

        /// <summary>Dừng vòng spawn (item đã spawn vẫn sống tới lifetime).</summary>
        public void StopSpawning()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
        }

        private IEnumerator SpawnLoop()
        {
            var wait = new WaitForSeconds(config.spawnInterval);
            while (true)
            {
                yield return wait;
                PruneDead();
                if (_alive.Count >= config.maxConcurrent) continue;
                SpawnOne();
            }
        }

        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
                if (_alive[i] == null) _alive.RemoveAt(i);
        }

        private void SpawnOne()
        {
            ItemData data = config.PickWeighted();
            if (data == null) return;

            Vector3 pos = transform.position; // thả từ vị trí spawner đang bay → rơi bằng gravity.

            Item item = InstantiateItem(data, pos);
            if (item == null) return;

            item.Init(data, config.itemLifetime, config.SafeRect, config.keepItemsInRect);
            _alive.Add(item);

            if (spawnVfxPrefab != null)
            {
                // POOLING (§3.1): VFX spawn qua pool. PooledParticleRelease tự Despawn khi hạt tắt —
                // ensure lúc runtime để prefab gán vào field này không bắt buộc gắn sẵn component.
                GameObject vfxGo = PrefabPool.Spawn(spawnVfxPrefab.gameObject, pos, Quaternion.identity);
                if (vfxGo.GetComponent<PooledParticleRelease>() == null)
                    vfxGo.AddComponent<PooledParticleRelease>();
                var vfx = vfxGo.GetComponent<ParticleSystem>();
                if (vfx != null) vfx.Play();
            }
        }

        private Item InstantiateItem(ItemData data, Vector3 pos)
        {
            if (data.prefabOverride != null)
            {
                GameObject go = Instantiate(data.prefabOverride, pos, Quaternion.identity);
                Item item = go.GetComponent<Item>();
                if (item == null)
                {
                    Debug.LogError($"[ItemSpawner] prefabOverride của {data.name} thiếu component Item.", this);
                    Destroy(go);
                }
                return item;
            }
            return Instantiate(itemPrefab, pos, Quaternion.identity);
        }

        // ---- Bay quanh map (gộp từ MysteryBlockSpawner) ----

        private void MoveAroundMap()
        {
            if (config == null) return;

            _directionTimer -= Time.deltaTime;
            if (_directionTimer <= 0f) PickNewDirection();

            Vector3 next = transform.position + (Vector3)(_velocity * Time.deltaTime);
            Rect bounds = config.FlyBounds;

            bool bounced = false;
            if (next.x < bounds.xMin || next.x > bounds.xMax)
            {
                _velocity.x *= -1f;
                next.x = Mathf.Clamp(next.x, bounds.xMin, bounds.xMax);
                bounced = true;
            }
            if (next.y < bounds.yMin || next.y > bounds.yMax)
            {
                _velocity.y *= -1f;
                next.y = Mathf.Clamp(next.y, bounds.yMin, bounds.yMax);
                bounced = true;
            }

            transform.position = next;
            if (bounced) ResetDirectionTimer();
        }

        private void PickNewDirection()
        {
            if (config == null) return;

            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

            float min = Mathf.Min(config.flySpeedRange.x, config.flySpeedRange.y);
            float max = Mathf.Max(config.flySpeedRange.x, config.flySpeedRange.y);
            _velocity = dir * Random.Range(min, max);
            ResetDirectionTimer();
        }

        private void ResetDirectionTimer()
        {
            float min = Mathf.Min(config.directionChangeIntervalRange.x, config.directionChangeIntervalRange.y);
            float max = Mathf.Max(config.directionChangeIntervalRange.x, config.directionChangeIntervalRange.y);
            _directionTimer = Random.Range(min, max);
        }
    }
}
