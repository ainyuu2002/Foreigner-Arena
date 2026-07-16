using System.Collections;
using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// (Hợp nhất item) BASE cho MỌI trạng thái NHÂN VẬT có thời hạn từ item (DMG buff / slow-fall / weather
    /// immunity...). Rút chung phần lặp: cache SpriteRenderer + màu gốc, tint báo hiệu, coroutine đếm GIÂY
    /// THỰC (NGOẠI LỆ hợp lệ — buff theo thời gian, KHÔNG phải frame-data clip), nháy 3s cuối, tự Destroy.
    ///
    /// DÙNG: subclass override <see cref="OnStatusApplied"/>/<see cref="OnStatusRemoved"/> cho hành vi riêng
    /// (đổi gravity, báo BuffAggregator...). Applier DEDUP theo TYPE bằng GetComponent&lt;TSubclass&gt;() —
    /// mỗi loại 1 subclass ⇒ 1 component/nhân vật. Refresh = reset thời lượng (KHÔNG stack duration).
    ///
    /// CẠM BẪY (CẦN TEST): 2+ status cùng lúc dùng CHUNG danh sách renderer + màu gốc ⇒ tint/nháy có thể
    /// đè màu gốc của nhau. Chấp nhận trước mắt (không dựng arbiter) — CẦN TEST.
    /// </summary>
    public abstract class TimedCharacterStatus : MonoBehaviour
    {
        protected const float BlinkLeadSeconds = 3f;
        protected const float BlinkPeriod = 0.2f;

        protected float _timeLeft;
        protected bool _active;

        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;
        private Color _tint = Color.white;
        private Coroutine _routine;

        /// <summary>Trạng thái còn hiệu lực (subclass/hệ khác đọc để kiểm tra, vd weather-immunity).</summary>
        public bool IsActive => _active;

        /// <summary>Áp/refresh: cache renderer, đặt thời lượng + tint, gọi hook, chạy 1 coroutine duy nhất.</summary>
        public void ApplyOrRefresh(float duration, Color tint)
        {
            CacheRenderers();

            _timeLeft = Mathf.Max(0.1f, duration);
            _tint = tint;
            _active = true;

            OnStatusApplied();
            ApplyTint(true);

            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Loop());
        }

        /// <summary>Hook: gọi 1 lần mỗi ApplyOrRefresh (đổi gravity, báo aggregator...). Mặc định no-op.</summary>
        protected virtual void OnStatusApplied() { }

        /// <summary>Hook: gọi khi hết giờ (trả gravity, báo aggregator...). Mặc định no-op.</summary>
        protected virtual void OnStatusRemoved() { }

        private IEnumerator Loop()
        {
            while (_timeLeft > 0f)
            {
                _timeLeft -= Time.deltaTime;

                if (_timeLeft <= BlinkLeadSeconds)
                {
                    bool on = Mathf.Repeat(_timeLeft, BlinkPeriod * 2f) > BlinkPeriod;
                    ApplyTint(on);
                }

                yield return null;
            }

            _active = false;
            ApplyTint(false);
            OnStatusRemoved();
            _routine = null;
            Destroy(this);
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

        protected virtual void OnDisable()
        {
            if (!_active) ApplyTint(false);
        }
    }
}
