using System.Collections;
using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// (P13) BUFF DAMAGE từ item — implement <see cref="IBuffSource"/> (Core) → <see cref="BuffAggregator"/>
    /// gom chung với Rage Mode (STACK với Rage bằng nhân chồng — research §4, quyết định người dùng #1: DMG
    /// Buff = NHÂN ×, CHỈ damage, KHÔNG knockback).
    ///
    /// SỐNG TRÊN NHÂN VẬT (KHÔNG trên item đã Destroy): Item nhặt → gọi <see cref="ApplyOrRefresh"/> trên root
    /// người nhặt (AddComponent nếu chưa có, hoặc refresh nếu đã có). KHÔNG STACK với chính nó (ăn cái mới =
    /// RESET thời lượng + đổi mult sang cái mới, KHÔNG cộng dồn duration, KHÔNG nhân 2 lần — research §5).
    ///
    /// VISUAL (Unity-native, §1.1): color tint qua <see cref="SpriteRenderer.color"/> (lưu màu gốc, trả lại
    /// khi hết); nhấp nháy 3s cuối bằng coroutine bật/tắt alpha. KHÔNG hardcode VFX. Timer buff = đếm GIÂY
    /// THỰC (NGOẠI LỆ hợp lệ: buff theo thời gian, KHÔNG phải frame-data clip — giống RageModeController P10).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ItemDamageBuff : MonoBehaviour, IBuffSource
    {
        [Tooltip("Thời gian nhấp nháy cảnh báo TRƯỚC khi hết (giây). GDD: 3s cuối.")]
        [Min(0f)] private const float BlinkLeadSeconds = 3f;

        [Tooltip("Chu kỳ nhấp nháy (giây) — bật/tắt tint trong 3s cuối.")]
        private const float BlinkPeriod = 0.2f;

        private float _multiplier = 1f;
        private float _timeLeft;
        private bool _active;

        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;
        private Color _tint = Color.white;
        private Coroutine _routine;

        // ===== IBuffSource (chỉ DamageDealtMult thay đổi; còn lại trung tính ×1.0/false) =====
        public float DamageDealtMult => _active ? _multiplier : 1f;
        public float KnockbackDealtMult => 1f;   // quyết định #1: KHÔNG buff knockback.
        public float HitstunTakenMult => 1f;
        public float MoveSpeedMult => 1f;
        public bool IsRageActive => false;        // item buff KHÔNG bật cờ Rage.
        public bool IsUltimateActive => false;

        /// <summary>
        /// Áp/refresh buff lên NHÂN VẬT này. Item (khi nhặt) tìm/AddComponent ItemDamageBuff trên root người
        /// nhặt rồi gọi cái này. Không-stack: luôn ghi đè mult + reset duration. Báo BuffAggregator quét lại
        /// (nguồn mới AddComponent runtime). <paramref name="tint"/> = màu báo hiệu đang có buff (item DMG đỏ).
        /// </summary>
        public void ApplyOrRefresh(float multiplier, float duration, Color tint)
        {
            CacheRenderers();
            _multiplier = Mathf.Max(1f, multiplier);
            _timeLeft = Mathf.Max(0f, duration);
            _tint = tint;
            _active = true;

            // BuffAggregator (cùng nhân vật) gom lại để tính ngay (nguồn vừa được thêm/đổi giá trị).
            var agg = GetComponentInParent<BuffAggregator>();
            if (agg != null) agg.RefreshSources();

            ApplyTint(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(BuffLoop());
        }

        private IEnumerator BuffLoop()
        {
            // Pha NHÁY 3s cuối + đếm thời lượng. Coroutine = Unity-native timer (KHÔNG đếm frame tay).
            while (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;

                if (_timeLeft <= BlinkLeadSeconds)
                {
                    // Nhấp nháy: bật/tắt tint theo chu kỳ để cảnh báo sắp hết.
                    bool on = Mathf.Repeat(_timeLeft, BlinkPeriod * 2f) > BlinkPeriod;
                    ApplyTint(on);
                }
                yield return null;
            }

            _active = false;
            ApplyTint(false);          // trả màu gốc.
            var agg = GetComponentInParent<BuffAggregator>();
            if (agg != null) agg.RefreshSources();
            _routine = null;
        }

        private void CacheRenderers()
        {
            if (_renderers != null) return;
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _originalColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalColors[i] = _renderers[i].color;
        }

        private void ApplyTint(bool on)
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].color = on ? _tint : _originalColors[i];
            }
        }

        private void OnDisable()
        {
            // An toàn: trả màu gốc nếu bị tắt giữa chừng.
            if (!_active) ApplyTint(false);
        }
    }
}
