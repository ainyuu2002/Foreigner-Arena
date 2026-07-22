using System;
using System.Collections.Generic;
using UnityEngine;

namespace FA.Items
{
    /// <summary>Loại hiệu ứng mà item có thể áp dụng.</summary>
    public enum ItemEffectType
    {
        /// <summary>Hồi resistance: IPercentReceiver.AddPercent(-value). [Knockback Potion / Mystery hồi %].</summary>
        ResistanceRecovery = 0,

        /// <summary>Hồi mana: IManaSpender.AddMana(value fraction). [Mana Potion / Mystery hồi mana].</summary>
        ManaRecovery = 1,

        /// <summary>Debuff tăng resistance: IPercentReceiver.AddPercent(+value). [Mystery bẫy nhẹ].</summary>
        ResistanceIncreaseDebuff = 3,

        /// <summary>Rơi chậm có thời hạn (SlowFallStatus): gravityScale ×(1-value).</summary>
        SlowFall = 4,

        /// <summary>Buff damage có thời hạn (ItemDamageBuff): multiplier = 1 + value. [DMG Buff / Mystery].</summary>
        DamageBuff = 5,

        /// <summary>Bẫy: AddPercent(+value) + đẩy knockback tức thời. [Mystery bẫy].</summary>
        TrapBall = 6,
    }

    /// <summary>Dữ liệu effect chỉnh trực tiếp trong Inspector.</summary>
    [Serializable]
    public struct ItemEffectData
    {
        public ItemEffectType type;
        [Min(0f)] public float value;
        [Min(0f)] public float duration;

        public ItemEffect Build() => new ItemEffect(type, value, duration);
    }

    /// <summary>Effect runtime đã được chốt khi nhặt item.</summary>
    public readonly struct ItemEffect
    {
        public readonly ItemEffectType type;
        public readonly float value;
        public readonly float duration;

        public ItemEffect(ItemEffectType type, float value, float duration)
        {
            this.type = type;
            this.value = value;
            this.duration = duration;
        }
    }

    /// <summary>Chọn phần tử theo trọng số, dùng chung cho item và Mystery Block.</summary>
    internal static class WeightedRandom
    {
        public static T Pick<T>(IList<T> entries, Func<T, float> getWeight, Predicate<T> filter = null) where T : class
        {
            if (entries == null || entries.Count == 0 || getWeight == null) return null;

            float total = 0f;
            T firstValid = null;
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null || (filter != null && !filter(entry))) continue;
                firstValid ??= entry;
                total += Mathf.Max(0f, getWeight(entry));
            }

            if (firstValid == null || total <= 0f) return firstValid;

            float roll = UnityEngine.Random.value * total;
            T lastValid = firstValid;
            for (int i = 0; i < entries.Count; i++)
            {
                T entry = entries[i];
                if (entry == null || (filter != null && !filter(entry))) continue;
                lastValid = entry;
                roll -= Mathf.Max(0f, getWeight(entry));
                if (roll <= 0f) return entry;
            }

            return lastValid;
        }
    }
}
