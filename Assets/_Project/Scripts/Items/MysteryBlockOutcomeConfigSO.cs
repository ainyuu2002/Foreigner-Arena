using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    [System.Serializable]
    public sealed class MysteryBlockOutcome
    {
        public ItemEffectType type = ItemEffectType.ResistanceRecovery;
        [Range(1, 3)] public int tier = 1;
        [Min(0f)] public float value = 0.05f;
        [Min(0f)] public float durationSeconds = 0f;
        [Min(0f)] public float spawnWeight = 1f;

        /// <summary>Chuyển dữ liệu Mystery thành effect runtime.</summary>
        public ItemEffect ToEffect() => new ItemEffect(type, value, durationSeconds);
    }

    [CreateAssetMenu(menuName = "FA/Items/Mystery Block Outcome Config", fileName = "MysteryBlockOutcomeConfig")]
    public sealed class MysteryBlockOutcomeConfigSO : ScriptableObject
    {
        [SerializeField] private List<MysteryBlockOutcome> outcomes = new List<MysteryBlockOutcome>();

        public MysteryBlockOutcome PickRandom()
        {
            return WeightedRandom.Pick(outcomes, outcome => outcome.spawnWeight) ?? FallbackOutcome();
        }

        /// <summary>Chọn ngẫu nhiên effect; dùng giá trị mặc định nếu bảng rỗng.</summary>
        public ItemEffect PickRandomEffect() => PickRandom().ToEffect();

        private static MysteryBlockOutcome FallbackOutcome()
        {
            return new MysteryBlockOutcome
            {
                type = ItemEffectType.ResistanceRecovery,
                tier = 1,
                value = 0.05f,
                durationSeconds = 0f,
                spawnWeight = 1f
            };
        }
    }
}
