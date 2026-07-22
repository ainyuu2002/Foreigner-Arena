using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) Nút uGUI bắn 1 <see cref="IntEventChannelSO"/> mang <see cref="value"/> khi click — dùng cho
    /// CharSelect (index nhân vật → kênh CharSelectedP1/P2) và StageSelect (index map → StageSelected).
    /// Decoupling (§1.3): UI raise kênh Core, MatchManager (FA.GameFlow) nghe — UI KHÔNG ref GameFlow.
    ///
    /// Tự đăng ký onClick lên <see cref="Button"/> cùng object (Unity-native) → KHÔNG cần wire tay trong
    /// Inspector. Mỗi button = 1 entry; thêm nhân vật/map = thêm button + set value (extensible §1.2).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class IntButtonRaiser : MonoBehaviour
    {
        [Tooltip("Kênh int bắn khi click (CharSelectedP1/P2 hoặc StageSelected).")]
        [SerializeField] private IntEventChannelSO channel;

        [Tooltip("Giá trị int bắn (index nhân vật / index map).")]
        [SerializeField] private int value;

        private Button _button;

        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Raise); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Raise); }

        private void Raise()
        {
            // DEBUG harness P12
            DebugLog.Log($"[IntButtonRaiser] {name} click → value={value} channel={(channel != null ? channel.name : "NULL")}", this);
            if (channel == null) { Debug.LogWarning($"[IntButtonRaiser] {name} channel CHƯA WIRE (NULL) — click bị bỏ qua.", this); return; } // DEBUG harness P12
            channel.RaiseEvent(value);
        }
    }
}
