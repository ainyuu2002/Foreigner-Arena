using UnityEngine;
using UnityEngine.Events;

namespace FA.Core
{
    /// <summary>
    /// Component tùy chọn cho ai muốn nối <see cref="VoidEventChannelSO"/> vào hành động
    /// trong Inspector (UnityEvent) thay vì code. Subscribe trong OnEnable, unsubscribe
    /// trong OnDisable để không leak khi domain reload bị tắt (Enter Play Mode Options).
    /// </summary>
    public class VoidEventListener : MonoBehaviour
    {
        [SerializeField] private VoidEventChannelSO channel;
        [SerializeField] private UnityEvent response;

        private void OnEnable()
        {
            if (channel != null)
            {
                channel.OnEventRaised += OnRaised;
            }
        }

        private void OnDisable()
        {
            if (channel != null)
            {
                channel.OnEventRaised -= OnRaised;
            }
        }

        private void OnRaised()
        {
            response?.Invoke();
        }
    }
}
