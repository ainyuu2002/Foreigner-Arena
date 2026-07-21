using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>Bay trong map và spawn item từ pool theo cấu hình.</summary>
    [DisallowMultipleComponent]
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Cấu hình")]
        [SerializeField] private ItemSpawnConfigSO config;

        [SerializeField] private Item itemPrefab;

        [SerializeField] private bool autoStart = true;

        [Header("(Tùy) VFX spawn-in")]
        [SerializeField] private ParticleSystem spawnVfxPrefab;

        [Header("Kênh vòng đời trận (Void Event Channel)")]
        [SerializeField] private VoidEventChannelSO onMatchStart;
        [SerializeField] private VoidEventChannelSO onMatchEnded;

        private readonly List<Item> _alive = new List<Item>();
        private Coroutine _loop;
        private Vector2 _velocity;
        private float _directionTimer;
        private float _nextMysterySpawnTime;

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
            ScheduleNextMystery();
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
                bool spawnMystery = Time.time >= _nextMysterySpawnTime;
                SpawnOne(spawnMystery);
            }
        }

        private void PruneDead()
        {
            for (int i = _alive.Count - 1; i >= 0; i--)
            {
                Item item = _alive[i];
                if (item != null && item.gameObject.activeSelf) continue;
                if (item != null) item.Finished -= OnItemFinished;
                _alive.RemoveAt(i);
            }
        }

        private void SpawnOne(bool spawnMystery)
        {
            ItemData data = spawnMystery ? config.PickMystery() : config.PickRegular();
            if (data == null && spawnMystery) data = config.PickRegular();
            if (data == null) return;

            Vector3 pos = transform.position; // thả từ vị trí spawner đang bay → rơi bằng gravity.

            Item item = SpawnItem(data, pos);
            if (item == null) return;

            item.Init(data, config.itemLifetime, config.SafeRect, config.keepItemsInRect);
            item.Finished -= OnItemFinished;
            item.Finished += OnItemFinished;
            _alive.Add(item);
            if (spawnMystery) ScheduleNextMystery();

            if (spawnVfxPrefab != null)
            {
                GameObject vfxObject = PrefabPool.Spawn(spawnVfxPrefab.gameObject, pos, Quaternion.identity);
                ParticleSystem vfx = vfxObject != null ? vfxObject.GetComponent<ParticleSystem>() : null;
                if (vfx == null)
                {
                    if (vfxObject != null) PrefabPool.Despawn(vfxObject);
                    return;
                }
                var main = vfx.main;
                main.stopAction = ParticleSystemStopAction.None;
                vfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                vfx.Play();
                StartCoroutine(ReturnSpawnVfxWhenFinished(vfx));
            }
        }

        private void ScheduleNextMystery()
        {
            float min = Mathf.Min(config.mysterySpawnIntervalRange.x, config.mysterySpawnIntervalRange.y);
            float max = Mathf.Max(config.mysterySpawnIntervalRange.x, config.mysterySpawnIntervalRange.y);
            _nextMysterySpawnTime = Time.time + Random.Range(Mathf.Max(0.5f, min), Mathf.Max(0.5f, max));
        }

        private Item SpawnItem(ItemData data, Vector3 pos)
        {
            GameObject prefab = data.prefabOverride != null ? data.prefabOverride : itemPrefab.gameObject;
            GameObject go = PrefabPool.Spawn(prefab, pos, Quaternion.identity);
            if (go == null) return null;

            Item item = go.GetComponent<Item>();
            if (item == null)
            {
                Debug.LogError($"[ItemSpawner] prefab của {data.name} thiếu component Item.", this);
                PrefabPool.Despawn(go);
            }
            return item;
        }

        private static IEnumerator ReturnSpawnVfxWhenFinished(ParticleSystem vfx)
        {
            while (vfx != null && vfx.gameObject.activeSelf && vfx.IsAlive(true)) yield return null;
            if (vfx != null && vfx.gameObject.activeSelf) PrefabPool.Despawn(vfx.gameObject);
        }

        private void OnItemFinished(Item item)
        {
            if (item == null) return;
            item.Finished -= OnItemFinished;
            _alive.Remove(item);
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
