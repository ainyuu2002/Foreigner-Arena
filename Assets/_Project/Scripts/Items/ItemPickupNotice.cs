using System.Collections;
using FA.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FA.Items
{
    /// <summary>Thông báo pickup gọn trên HUD. Chỉ hiện khi scene có component này (scene Items test).</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ItemPickupNotice : MonoBehaviour
    {
        [SerializeField] private Text label;
        [Min(0.1f)] [SerializeField] private float visibleDuration = 3f;
        [Min(0.05f)] [SerializeField] private float fadeDuration = 0.35f;

        private static ItemPickupNotice s_instance;
        private CanvasGroup _canvasGroup;
        private Coroutine _routine;

        private void Awake()
        {
            s_instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        public static void Show(string message)
        {
            if (s_instance == null || string.IsNullOrWhiteSpace(message)) return;
            s_instance.Display(message);
        }

        /// <summary>Hiện người nhặt, tên item và effect vừa nhận.</summary>
        public static void ShowPickup(Transform player, ItemData data, ItemEffect effect)
        {
            if (player == null || data == null) return;

            string playerName = player.gameObject.layer == GameLayers.Player2 ? "P2" : "P1";
            string separator = data.type == ItemType.MysteryBlock ? " → " : " • ";
            Show($"{playerName} PICKED UP: {data.DisplayName}{separator}{FormatEffect(effect)}");
        }

        private static string FormatEffect(ItemEffect effect)
        {
            string duration = effect.duration > 0f ? $" • {effect.duration:0.#} SECONDS" : string.Empty;
            switch (effect.type)
            {
                case ItemEffectType.ResistanceRecovery:
                    return $"RECOVER {effect.value:0.#}% RESISTANCE";
                case ItemEffectType.ManaRecovery:
                    return $"+{effect.value * 100f:0.#}% MANA";
                case ItemEffectType.ResistanceIncreaseDebuff:
                    return $"+{effect.value:0.#}% KNOCKBACK";
                case ItemEffectType.SlowFall:
                    return $"-{effect.value * 100f:0.#}% GRAVITY{duration}";
                case ItemEffectType.DamageBuff:
                    return $"+{effect.value * 100f:0.#}% DAMAGE{duration}";
                case ItemEffectType.TrapBall:
                    return $"KNOCKBACK TRAP +{effect.value * 100f:0.#}%";
                default:
                    return effect.type.ToString();
            }
        }

        private void Display(string message)
        {
            if (label == null) return;

            label.text = message;
            SetVisible(true);
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            yield return new WaitForSecondsRealtime(visibleDuration);

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            SetVisible(false);
            _routine = null;
        }

        private void SetVisible(bool visible)
        {
            if (_canvasGroup != null) _canvasGroup.alpha = visible ? 1f : 0f;
            if (!visible && label != null) label.text = string.Empty;
        }
    }
}
