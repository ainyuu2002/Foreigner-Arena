using System.Collections;
using UnityEngine;

namespace FA.Items
{
    /// <summary>Base cho status có thời hạn: đếm giờ, tint, nhấp nháy và tự gỡ.</summary>
    public abstract class TimedCharacterStatus : MonoBehaviour
    {
        protected const float BlinkLeadSeconds = 3f;
        protected const float BlinkPeriod = 0.2f;

        protected float _timeLeft;
        protected bool _active;
        private float _duration;

        private SpriteRenderer[] _renderers;
        private Color[] _originalColors;
        private Color _tint = Color.white;
        private Coroutine _routine;

        /// <summary>Trạng thái còn hiệu lực (subclass/hệ khác đọc để kiểm tra, vd weather-immunity).</summary>
        public bool IsActive => _active;

        /// <summary>Thời gian còn lại cho HUD.</summary>
        public float TimeLeft => Mathf.Max(0f, _timeLeft);

        /// <summary>Tổng thời gian của lần áp gần nhất.</summary>
        public float Duration => Mathf.Max(0.1f, _duration);

        /// <summary>Áp/refresh: cache renderer, đặt thời lượng + tint, gọi hook, chạy 1 coroutine duy nhất.</summary>
        public void ApplyOrRefresh(float duration, Color tint)
        {
            CacheRenderers();

            _timeLeft = Mathf.Max(0.1f, duration);
            _duration = _timeLeft;
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
