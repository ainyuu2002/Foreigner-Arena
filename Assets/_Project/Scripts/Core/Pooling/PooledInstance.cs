using System.Collections.Generic;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// MARKER gắn LÚC createFunc của <see cref="PrefabPool"/> (KHÔNG có trên prefab gốc — pool tự
    /// AddComponent khi tạo instance đầu). Giữ 2 việc để pool tái dùng an toàn (§3.1, cạm bẫy pooling):
    ///  1) NHỚ prefab NGUỒN → <see cref="PrefabPool.Despawn"/> trả về ĐÚNG pool.
    ///  2) CACHE state GỐC (localScale + gravityScale của Rigidbody2D + enabled GỐC của từng Renderer)
    ///     lúc create → <see cref="OnGet"/> RESTORE TUYỆT ĐỐI mỗi lần lấy ra, vì spawner đời trước có
    ///     thể đã nhân/đổi (scale ×k, gravityScale ×k, linearVelocity, renderer.enabled=false lúc nổ...).
    ///     Không restore = state cộng dồn qua các lần tái dùng.
    /// Đồng thời zero linear/angularVelocity + RESTORE ĐÚNG trạng thái enabled gốc của từng Renderer
    /// (không ép true — prefab có thể cố tình để 1 renderer con enabled=false lúc authoring) và gọi
    /// <see cref="IPoolable"/> hook trên các component con.
    ///
    /// Đặt ở FA.Core (type dùng chung — mọi module spawn qua PrefabPool). KHÔNG ref module nào.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledInstance : MonoBehaviour
    {
        private GameObject _sourcePrefab;
        private Vector3 _origScale;
        private Rigidbody2D _rb;
        private float _origGravity;
        private Renderer[] _renderers;
        private bool[] _origRendererEnabled;
        private IPoolable[] _poolables;

        /// <summary>Prefab đã Instantiate ra instance này — key để Despawn trả đúng pool.</summary>
        public GameObject SourcePrefab => _sourcePrefab;

        /// <summary>PrefabPool.createFunc gọi 1 LẦN lúc tạo instance: nhớ prefab + cache state gốc.</summary>
        public void Setup(GameObject sourcePrefab)
        {
            _sourcePrefab = sourcePrefab;
            _origScale = transform.localScale;
            _rb = GetComponent<Rigidbody2D>();
            _origGravity = _rb != null ? _rb.gravityScale : 0f;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _origRendererEnabled = new bool[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _origRendererEnabled[i] = _renderers[i] != null && _renderers[i].enabled;
            var found = new List<IPoolable>();
            GetComponentsInChildren(true, found);
            _poolables = found.ToArray();
        }

        /// <summary>PrefabPool.actionOnGet gọi (sau SetActive(true)) — RESTORE tuyệt đối state gốc rồi
        /// báo IPoolable. Chạy TRƯỚC khi spawner set position/scale/velocity đời mới (ordering an toàn).</summary>
        public void OnGet()
        {
            transform.localScale = _origScale;
            if (_rb != null)
            {
                _rb.gravityScale = _origGravity;
                _rb.linearVelocity = Vector2.zero;
                _rb.angularVelocity = 0f;
                _rb.rotation = 0f;
            }
            if (_renderers != null)
                for (int i = 0; i < _renderers.Length; i++)
                    if (_renderers[i] != null) _renderers[i].enabled = _origRendererEnabled[i];

            if (_poolables != null)
                foreach (IPoolable p in _poolables) p?.OnSpawned();
        }

        /// <summary>PrefabPool.actionOnRelease gọi (trước SetActive(false)) — báo IPoolable dọn dẹp.</summary>
        public void OnRelease()
        {
            if (_poolables != null)
                foreach (IPoolable p in _poolables) p?.OnDespawned();
        }
    }
}
