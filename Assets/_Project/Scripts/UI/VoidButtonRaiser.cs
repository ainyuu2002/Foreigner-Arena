using UnityEngine;
using UnityEngine.UI;
using FA.Core;

namespace FA.UI
{
    /// <summary>
    /// (P12) Nút uGUI bắn 1 <see cref="VoidEventChannelSO"/> khi click — dùng cho Rematch / Back-to-CharSelect
    /// trên End Screen. Decoupling (§1.3): UI raise kênh Core, MatchManager (FA.GameFlow) nghe. Tự đăng ký
    /// onClick lên <see cref="Button"/> cùng object → KHÔNG wire tay.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class VoidButtonRaiser : MonoBehaviour
    {
        [Tooltip("Kênh void bắn khi click (RematchPressed / BackToCharSelect).")]
        [SerializeField] private VoidEventChannelSO channel;

        private Button _button;

        private void Awake() => _button = GetComponent<Button>();
        private void OnEnable() { if (_button != null) _button.onClick.AddListener(Raise); }
        private void OnDisable() { if (_button != null) _button.onClick.RemoveListener(Raise); }

        private void Raise() { if (channel != null) channel.RaiseEvent(); }
    }
}
