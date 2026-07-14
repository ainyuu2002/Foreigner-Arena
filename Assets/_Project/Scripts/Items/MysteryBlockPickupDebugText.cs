using UnityEngine;

namespace FA.Items
{
    [DisallowMultipleComponent]
    public sealed class MysteryBlockPickupDebugText : MonoBehaviour
    {
        private static string _message = "Mystery Block: waiting for pickup...";
        private static float _visibleUntil;
        private static float _importantUntil;
        private static float _defaultDuration = 8f;

        [SerializeField] private Vector2 screenPosition = new Vector2(16f, 16f);
        [SerializeField] private Vector2 boxSize = new Vector2(620f, 112f);
        [SerializeField] private float messageDuration = 8f;

        public static void Show(string message)
        {
            Show(message, _defaultDuration);
        }

        public static void Show(string message, float duration)
        {
            Show(message, duration, false);
        }

        public static void ShowImportant(string message, float duration)
        {
            Show(message, duration, true);
        }

        public static void ShowPassive(string message, float duration)
        {
            if (Time.time <= _importantUntil)
                return;

            Show(message, duration, false);
        }

        private static void Show(string message, float duration, bool important)
        {
            _message = message;
            _visibleUntil = Time.time + Mathf.Max(0.5f, duration);
            if (important)
                _importantUntil = _visibleUntil;
        }

        private void Awake()
        {
            _defaultDuration = Mathf.Max(0.5f, messageDuration);
        }

        private void OnGUI()
        {
            string text = Time.time <= _visibleUntil ? _message : "Mystery Block: no buff picked yet";
            var rect = new Rect(screenPosition.x, screenPosition.y, boxSize.x, boxSize.y);

            GUI.Box(rect, GUIContent.none);

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            style.normal.textColor = Color.white;

            GUI.Label(new Rect(rect.x + 14f, rect.y + 8f, rect.width - 28f, rect.height - 16f), text, style);
        }
    }
}
