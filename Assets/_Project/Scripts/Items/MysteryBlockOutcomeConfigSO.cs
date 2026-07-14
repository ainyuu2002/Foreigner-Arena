using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    public enum MysteryBlockOutcomeType
    {
        ResistanceRecovery = 0,
        ManaRecovery = 1,
        WeatherImmunity = 2,
        ResistanceIncreaseDebuff = 3,
        SlowFall = 4,
        DamageBuff = 5,
        TrapBall = 6,
    }

    [System.Serializable]
    public sealed class MysteryBlockOutcome
    {
        public MysteryBlockOutcomeType type = MysteryBlockOutcomeType.ResistanceRecovery;
        [Range(1, 3)] public int tier = 1;
        [Min(0f)] public float value = 0.05f;
        [Min(0f)] public float durationSeconds = 0f;
        [Min(0f)] public float spawnWeight = 1f;

        public string DisplayName
        {
            get
            {
                switch (type)
                {
                    case MysteryBlockOutcomeType.ResistanceRecovery: return "Resistance recovery";
                    case MysteryBlockOutcomeType.ManaRecovery: return "Mana recovery";
                    case MysteryBlockOutcomeType.WeatherImmunity: return "Weather immunity";
                    case MysteryBlockOutcomeType.ResistanceIncreaseDebuff: return "Resistance increase";
                    case MysteryBlockOutcomeType.SlowFall: return "Slow fall";
                    case MysteryBlockOutcomeType.DamageBuff: return "Damage buff";
                    case MysteryBlockOutcomeType.TrapBall: return "Trap ball";
                    default: return type.ToString();
                }
            }
        }

        public string TierName
        {
            get
            {
                switch (tier)
                {
                    case 1: return "Small";
                    case 2: return "Medium";
                    case 3: return "Large";
                    default: return $"Tier {tier}";
                }
            }
        }

        public string DebugSummary()
        {
            string amount = value > 0f ? $" - {value * 100f:0.#}%" : string.Empty;
            string duration = durationSeconds > 0f ? $" / {durationSeconds:0.#}s" : string.Empty;
            return $"{DisplayName} - Tier {tier} ({TierName}){amount}{duration}";
        }
    }

    [CreateAssetMenu(menuName = "FA/Items/Mystery Block Outcome Config", fileName = "MysteryBlockOutcomeConfig")]
    public sealed class MysteryBlockOutcomeConfigSO : ScriptableObject
    {
        [SerializeField] private List<MysteryBlockOutcome> outcomes = new List<MysteryBlockOutcome>();

        public MysteryBlockOutcome PickRandom()
        {
            if (outcomes == null || outcomes.Count == 0)
                return FallbackOutcome();

            float total = 0f;
            for (int i = 0; i < outcomes.Count; i++)
            {
                if (outcomes[i] == null) continue;
                total += Mathf.Max(0f, outcomes[i].spawnWeight);
            }

            if (total <= 0f)
                return outcomes[0] ?? FallbackOutcome();

            float roll = Random.value * total;
            for (int i = 0; i < outcomes.Count; i++)
            {
                MysteryBlockOutcome outcome = outcomes[i];
                if (outcome == null) continue;

                roll -= Mathf.Max(0f, outcome.spawnWeight);
                if (roll <= 0f)
                    return outcome;
            }

            return outcomes[outcomes.Count - 1] ?? FallbackOutcome();
        }

        private static MysteryBlockOutcome FallbackOutcome()
        {
            return new MysteryBlockOutcome
            {
                type = MysteryBlockOutcomeType.ResistanceRecovery,
                tier = 1,
                value = 0.05f,
                durationSeconds = 0f,
                spawnWeight = 1f
            };
        }
    }
}
