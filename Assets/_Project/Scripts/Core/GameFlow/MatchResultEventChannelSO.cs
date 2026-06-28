using UnityEngine;

namespace FA.Core
{
    /// <summary>
    /// (P12) Kênh mang <see cref="MatchResult"/> — WinCondition (FA.GameFlow) RAISE khi trận xong;
    /// EndScreenView (FA.UI) SUBSCRIBE để in winner + stats. Cơ chế chống reference chéo (§1.3): UI không
    /// biết GameFlow tồn tại, chỉ nghe kênh ở Core.
    ///
    /// Subclass non-generic theo pattern (base generic không gắn được [CreateAssetMenu] — y hệt
    /// <see cref="HitEventChannelSO"/>). MatchResult là struct → Action&lt;MatchResult&gt; KHÔNG boxing.
    /// </summary>
    [CreateAssetMenu(menuName = "FA/Events/Match Result Event Channel", fileName = "MatchResultEventChannel")]
    public class MatchResultEventChannelSO : EventChannelSO<MatchResult>
    {
    }
}
