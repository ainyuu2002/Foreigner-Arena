using System.Collections;
using UnityEngine;

namespace FA.Items
{
    [DisallowMultipleComponent]
    public sealed class MysteryBlockSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject mysteryBlockTemplate;
        [SerializeField] private Vector2 flyBoundsCenter = new Vector2(0f, 2.5f);
        [SerializeField] private Vector2 flyBoundsSize = new Vector2(12f, 4f);
        [SerializeField] private Vector2 flySpeedRange = new Vector2(1.5f, 3f);
        [SerializeField] private Vector2 directionChangeIntervalRange = new Vector2(1.5f, 3.5f);
        [SerializeField] private Vector2 spawnIntervalRange = new Vector2(10f, 20f);
        [SerializeField] private int maxAliveBlocks = 5;
        [SerializeField] private float itemLifetime = 20f;
        [SerializeField] private float dropFromSpawnerHeightOffset = 0f;
        [SerializeField] private bool autoStart = true;

        private Coroutine _loop;
        private Vector2 _velocity;
        private float _directionTimer;

        private Rect FlyBounds => new Rect(
            flyBoundsCenter.x - flyBoundsSize.x * 0.5f,
            flyBoundsCenter.y - flyBoundsSize.y * 0.5f,
            flyBoundsSize.x,
            flyBoundsSize.y);

        private void OnEnable()
        {
            PickNewDirection();
            if (autoStart)
                _loop = StartCoroutine(SpawnLoop());
        }

        private void Update()
        {
            MoveAroundMap();
        }

        private void OnDisable()
        {
            if (_loop != null)
                StopCoroutine(_loop);
            _loop = null;
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                float min = Mathf.Min(spawnIntervalRange.x, spawnIntervalRange.y);
                float max = Mathf.Max(spawnIntervalRange.x, spawnIntervalRange.y);
                yield return new WaitForSeconds(Random.Range(min, max));

                if (CountAliveBlocks() >= maxAliveBlocks)
                    continue;

                SpawnOne();
            }
        }

        private int CountAliveBlocks()
        {
            int count = 0;
            var blocks = FindObjectsByType<MysteryBlockItem>(FindObjectsInactive.Exclude);
            for (int i = 0; i < blocks.Length; i++)
            {
                if (blocks[i] != null && blocks[i].gameObject != mysteryBlockTemplate)
                    count++;
            }
            return count;
        }

        private void SpawnOne()
        {
            if (mysteryBlockTemplate == null)
            {
                Debug.LogError("[MysteryBlockSpawner] Missing mysteryBlockTemplate.", this);
                return;
            }

            Vector3 position = transform.position + Vector3.up * dropFromSpawnerHeightOffset;
            GameObject block = Instantiate(mysteryBlockTemplate, position, Quaternion.identity);
            block.name = "Mystery_Block_66_Drop";
            block.SetActive(true);

            var item = block.GetComponent<MysteryBlockItem>();
            if (item != null)
            {
                item.ResetForDrop();
                item.StartLifetime(itemLifetime);
            }

            MysteryBlockPickupDebugText.ShowPassive("Flying spawner dropped a Mystery Block...", 1.5f);
        }

        private void MoveAroundMap()
        {
            _directionTimer -= Time.deltaTime;
            if (_directionTimer <= 0f)
                PickNewDirection();

            Vector3 next = transform.position + (Vector3)(_velocity * Time.deltaTime);
            Rect bounds = FlyBounds;

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

            if (bounced)
                ResetDirectionTimer();
        }

        private void PickNewDirection()
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < 0.01f)
                direction = Vector2.right;

            float min = Mathf.Min(flySpeedRange.x, flySpeedRange.y);
            float max = Mathf.Max(flySpeedRange.x, flySpeedRange.y);
            _velocity = direction * Random.Range(min, max);
            ResetDirectionTimer();
        }

        private void ResetDirectionTimer()
        {
            float min = Mathf.Min(directionChangeIntervalRange.x, directionChangeIntervalRange.y);
            float max = Mathf.Max(directionChangeIntervalRange.x, directionChangeIntervalRange.y);
            _directionTimer = Random.Range(min, max);
        }
    }
}
