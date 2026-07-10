using FA.Core;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// VFX/SFX TRÚNG/ĐỠ ĐÒN của nhân vật: nghe kênh GLOBAL <c>OnHitConfirmed</c>/<c>OnHitBlocked</c>
    /// (P6 — HitData có ContactPoint), lọc <c>hit.Attacker == root MÌNH</c> → spark + tiếng "bốp" của
    /// ĐÒN MÌNH đánh trúng, spawn tại điểm chạm. Decoupling §1.3: Combat raise, Effects nghe — Combat
    /// không biết module này tồn tại.
    /// PER-MOVE như MUGEN (mỗi HitDef 1 hitsound + spark riêng): ưu tiên <c>hit.HitEffect</c>/
    /// <c>hit.BlockEffect</c> (HitData mang từ MoveData.hitEffect/blockEffect) — move không khai báo
    /// thì rơi về hiệu ứng MẶC ĐỊNH của nhân vật ở dưới (no-regression).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HitEffectPlayer : MonoBehaviour
    {
        [Header("Kênh (CÙNG asset toàn trận — P6)")]
        [Tooltip("OnHitConfirmedChannel — hit TRÚNG THẬT (không block).")]
        [SerializeField] private HitEventChannelSO hitConfirmedChannel;
        [Tooltip("OnHitBlockedChannel — hit bị ĐỠ.")]
        [SerializeField] private HitEventChannelSO hitBlockedChannel;

        [Header("Hiệu ứng MẶC ĐỊNH (fallback khi MoveData.hitEffect/blockEffect trống)")]
        [Tooltip("Khi ĐÒN MÌNH trúng: vfxPrefab spawn tại điểm chạm + sfx.")]
        [SerializeField] private EffectData hitEffect;
        [Tooltip("Khi ĐÒN MÌNH bị đỡ: spark đỡ + tiếng chặn.")]
        [SerializeField] private EffectData blockEffect;

        [Tooltip("Trống = AudioSource trên cùng object.")]
        [SerializeField] private AudioSource audioSource;

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (hitConfirmedChannel != null) hitConfirmedChannel.OnEventRaised += OnHitConfirmed;
            if (hitBlockedChannel != null) hitBlockedChannel.OnEventRaised += OnHitBlocked;
        }

        private void OnDisable()
        {
            if (hitConfirmedChannel != null) hitConfirmedChannel.OnEventRaised -= OnHitConfirmed;
            if (hitBlockedChannel != null) hitBlockedChannel.OnEventRaised -= OnHitBlocked;
        }

        private void OnHitConfirmed(HitData hit)
        {
            if (!ReferenceEquals(hit.Attacker, gameObject)) return;
            Play(hit.HitEffect != null ? hit.HitEffect : hitEffect, hit.ContactPoint);
        }

        private void OnHitBlocked(HitData hit)
        {
            if (!ReferenceEquals(hit.Attacker, gameObject)) return;
            Play(hit.BlockEffect != null ? hit.BlockEffect : blockEffect, hit.ContactPoint);
        }

        private void Play(EffectData fx, Vector2 at)
        {
            if (fx == null) return;
            if (fx.vfxPrefab != null)
            {
                GameObject vfx = Instantiate(fx.vfxPrefab, new Vector3(at.x, at.y, 0f), Quaternion.identity);
                // × độ phóng nhân vật (root ×2.5) — spark tương xứng thân người. .y không đổi dấu facing.
                vfx.transform.localScale *= fx.scale * Mathf.Abs(transform.lossyScale.y);
            }
            if (audioSource != null)
            {
                AudioClip clip = fx.PickClip();
                if (clip != null) audioSource.PlayOneShot(clip, fx.volume);
            }
        }
    }
}
