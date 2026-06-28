using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Kênh sự kiện mang một <see cref="GameObject"/> — vd "OnPlayerEnteredDeathZone(victim)" (P11).
    /// FA.Stage (DeathZone/RisingLava) RAISE khi player chạm vùng chết; P12 LivesSystem SUBSCRIBE để
    /// trừ mạng/respawn — cơ chế chống reference chéo module (§1.3): Stage không biết GameFlow tồn tại.
    ///
    /// Subclass non-generic (base generic không gắn được [CreateAssetMenu] — pattern y hệt
    /// <see cref="HitEventChannelSO"/>/<see cref="IntEventChannelSO"/>). API kế thừa: RaiseEvent(GameObject),
    /// OnEventRaised. Tạo 1 asset "OnPlayerDiedChannel" gán vào DeathZone (raise) + listener debug/P12.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Events/GameObject Event Channel", fileName = "GameObjectEventChannel")]
    public class GameObjectEventChannelSO : EventChannelSO<GameObject>
    {
    }
}
