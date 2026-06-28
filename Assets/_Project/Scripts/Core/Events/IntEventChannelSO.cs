using UnityEngine;

namespace FA.Core
{
    /// <summary>Kênh sự kiện mang một int (vd: combo count, lives, mana ô...).</summary>
    [CreateAssetMenu(menuName = "FA/Events/Int Event Channel", fileName = "IntEventChannel")]
    public class IntEventChannelSO : EventChannelSO<int>
    {
    }
}
