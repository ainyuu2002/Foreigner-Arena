using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// Kênh sự kiện mang <see cref="HitData"/> — "OnHitLanded" (§3.3/§3.6). Combat (P5) RAISE khi
    /// hitbox chạm hurtbox; Resources/UI/Combo/Knockback (P6/P7/P8) SUBSCRIBE mà KHÔNG biết Combat
    /// tồn tại → cơ chế chống reference chéo module (§1.3).
    ///
    /// Subclass non-generic (base generic không gắn được [CreateAssetMenu] — pattern y hệt
    /// <see cref="IntEventChannelSO"/>/<see cref="FloatEventChannelSO"/>). API kế thừa:
    /// RaiseEvent(HitData), OnEventRaised. Tạo 1 asset "OnHitLandedChannel" gán vào HitboxAuthoring
    /// (raise) + HitstopController + listener debug (subscribe).
    ///
    /// HitData là struct → Action&lt;HitData&gt; KHÔNG boxing (generic typed) → an toàn GC.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Events/Hit Event Channel", fileName = "HitEventChannel")]
    public class HitEventChannelSO : EventChannelSO<HitData>
    {
    }
}
