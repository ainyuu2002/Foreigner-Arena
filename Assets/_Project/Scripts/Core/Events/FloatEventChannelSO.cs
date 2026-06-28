using UnityEngine;

namespace FA.Core
{
    /// <summary>Kênh sự kiện mang một float (vd: knockback %, mana fill, rage fill...).</summary>
    [CreateAssetMenu(menuName = "FA/Events/Float Event Channel", fileName = "FloatEventChannel")]
    public class FloatEventChannelSO : EventChannelSO<float>
    {
    }
}
