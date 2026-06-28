using System;
using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Kênh sự kiện không mang payload (chỉ báo "có chuyện xảy ra").
    /// Placeholder Phase 0; các phase sau tạo asset cụ thể để nối module qua Inspector/code.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Events/Void Event Channel", fileName = "VoidEventChannel")]
    public class VoidEventChannelSO : ScriptableObject
    {
        public event Action OnEventRaised;

        public void RaiseEvent()
        {
            OnEventRaised?.Invoke();
        }
    }
}
