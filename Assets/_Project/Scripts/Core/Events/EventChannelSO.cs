using System;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Lớp base generic cho mọi kênh sự kiện mang một payload kiểu T.
    /// Đây là cơ chế decoupling giữa các module (§3.3 CLAUDE.md): một module
    /// "Raise" sự kiện, module khác "Subscribe" mà KHÔNG reference chéo class của nhau.
    ///
    /// LƯU Ý: Không gắn [CreateAssetMenu] lên class generic — Unity không tạo asset cho
    /// generic SO. Mỗi kiểu T cần dùng phải có một subclass non-generic cụ thể
    /// (vd <see cref="IntEventChannelSO"/>) mang attribute đó.
    /// </summary>
    public abstract class EventChannelSO<T> : ScriptableObject
    {
        /// <summary>Listener subscribe trong OnEnable, unsubscribe trong OnDisable (tránh leak).</summary>
        public event Action<T> OnEventRaised;

        /// <summary>Module phát sự kiện gọi hàm này; mọi listener đã subscribe sẽ nhận payload.</summary>
        public void RaiseEvent(T value)
        {
            // DEBUG harness P12
            Debug.Log($"[EventChannel] {name} Raise({value}) listeners={(OnEventRaised == null ? 0 : OnEventRaised.GetInvocationList().Length)}");
            OnEventRaised?.Invoke(value);
        }
    }
}
