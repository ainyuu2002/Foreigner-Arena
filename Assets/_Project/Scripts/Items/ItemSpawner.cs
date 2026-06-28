using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// (P13) SPAWNER ITEM — code chỉ ĐIỀU PHỐI (research §3.2): coroutine timer theo spawnInterval, giữ
    /// danh sách item đang sống, cap maxConcurrent, chọn ItemData (weighted) + điểm trong safe-rect (SO) rồi
    /// Instantiate. KHÔNG ref FA.Stage: vùng spawn an toàn là Rect trong <see cref="ItemSpawnConfigSO"/>
    /// (người dựng map khai 1 lần). Rơi-từ-trên = spawn cao hơn rồi để Rigidbody2D rớt; teleport-in = spawn
    /// đúng chỗ + particle. Code chọn vị trí + Instantiate; rơi/đáp/trượt do physics (§1.1).
    ///
    /// Gắn 1 cái trong scene map. Item prefab phải có <see cref="Item"/>. Tự dọn item null khỏi list (đã bị
    /// nhặt/despawn Destroy). Coroutine = Unity-native timer (KHÔNG đếm frame tay).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemSpawner : MonoBehaviour
    {
        [Header("Cấu hình")]
        [Tooltip("ItemSpawnConfigSO — nhịp/cap/lifetime/safe-rect/pool. BẮT BUỘC gán.")]
        [SerializeField] private ItemSpawnConfigSO config;

        [Tooltip("Prefab item (có component Item + Rigidbody2D + collider thân + child trigger Layer Item).")]
        [SerializeField] private Item itemPrefab;

        [Tooltip("Tự bắt đầu spawn khi scene chạy. Tắt nếu MatchManager (P12) muốn điều khiển bằng StartSpawning.")]
        [SerializeField] private bool autoStart = true;

        [Header("(Tùy) VFX teleport-in")]
        [Tooltip("ParticleSystem prefab phát tại chỗ khi item TELEPORT-IN. Optional.")]
        [SerializeField] private ParticleSystem teleportVfxPrefab;

        private readonly List<Item> _alive = new List<Item>();
        private Coroutine _loop;

        private void OnEnable()
        {
            if (autoStart) StartSpawning();
        }

        private void OnDisable() => StopSpawning();

        /// <summary>Bắt đầu vòng spawn (MatchManager P12 có thể gọi khi vào Match).</summary>
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

            Rect rect = config.SafeRect;
            float x = Random.Range(rect.xMin, rect.xMax);
            float groundY = Random.Range(rect.yMin, rect.yMax);

            bool dropFromAbove = Random.value < config.dropFromAboveChance;
            Vector3 pos = new Vector3(x, dropFromAbove ? rect.yMax + config.dropExtraHeight : groundY, 0f);

            Item item = Instantiate(itemPrefab, pos, Quaternion.identity);
            item.Init(data, config.itemLifetime, rect, config.keepItemsInRect);
            _alive.Add(item);

            if (!dropFromAbove && teleportVfxPrefab != null)
            {
                var vfx = Instantiate(teleportVfxPrefab, pos, Quaternion.identity);
                vfx.Play();
            }
        }
    }
}
