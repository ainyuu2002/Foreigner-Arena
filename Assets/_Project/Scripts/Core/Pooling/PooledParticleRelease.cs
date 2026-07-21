using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// TỰ TRẢ VỀ POOL khi mọi ParticleSystem con tắt hẳn — analog <c>FlipbookOneShot</c> cho VFX
    /// particle one-shot (§3.1). Gắn ở ROOT prefab (root có thể KHÔNG có ParticleSystem — vd
    /// Super2Wind_VFX chỉ chứa 2 con Wind/BowGlint) nên không dùng OnParticleSystemStopped
    /// (callback đó chỉ bắn cho PS trên CÙNG GameObject) mà poll IsAlive mỗi frame.
    ///
    /// stopAction=Destroy trên PS con PHÁ pooling: con tự hủy → instance tái dùng MẤT con vĩnh viễn,
    /// pool giữ xác thiếu bộ phận. Awake ép các stopAction Destroy về None — component này là chủ
    /// vòng đời duy nhất. KHÔNG gắn cho VFX loop/bám chủ dài hạn (aura) — thứ đó spawner tự Despawn.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledParticleRelease : MonoBehaviour
    {
        private ParticleSystem[] _systems;

        private void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in _systems)
            {
                ParticleSystem.MainModule main = ps.main;
                if (main.stopAction == ParticleSystemStopAction.Destroy)
                    main.stopAction = ParticleSystemStopAction.None;
            }
        }

        private void Update()
        {
            for (int i = 0; i < _systems.Length; i++)
                if (_systems[i] != null && _systems[i].IsAlive()) return;
            PrefabPool.Despawn(gameObject);
        }
    }
}
