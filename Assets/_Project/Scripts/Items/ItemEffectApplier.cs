using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>Áp effect item lên nhân vật qua interface hoặc status tương ứng.</summary>
    public static class ItemEffectApplier
    {
        /// <summary>Chọn cách áp dựa trên loại effect.</summary>
        public static void Apply(Transform playerRoot, ItemEffect effect, Color statusTint, Vector2 trapKnockbackForce)
        {
            if (playerRoot == null) return;

            switch (effect.type)
            {
                case ItemEffectType.ResistanceRecovery:
                    AddPercent(playerRoot, -effect.value);
                    break;

                case ItemEffectType.ManaRecovery:
                    AddMana(playerRoot, effect.value);
                    break;

                case ItemEffectType.ResistanceIncreaseDebuff:
                    AddPercent(playerRoot, effect.value);
                    break;

                case ItemEffectType.DamageBuff:
                    ApplyDamageBuff(playerRoot, effect, statusTint);
                    break;

                case ItemEffectType.SlowFall:
                    ApplySlowFall(playerRoot, effect, statusTint);
                    break;

                case ItemEffectType.TrapBall:
                    ApplyTrap(playerRoot, effect, trapKnockbackForce);
                    break;
            }
        }

        private static void AddPercent(Transform root, float amount)
        {
            var receiver = root.GetComponentInChildren<IPercentReceiver>();
            receiver?.AddPercent(amount); // âm = giảm (clamp đáy 0 ở implement); dương = tăng.
        }

        private static void AddMana(Transform root, float fraction)
        {
            var mana = root.GetComponentInChildren<IManaSpender>();
            mana?.AddMana(fraction);
        }

        private static void ApplyDamageBuff(Transform root, ItemEffect effect, Color tint)
        {
            var buff = root.GetComponentInChildren<ItemDamageBuff>();
            if (buff == null) buff = root.gameObject.AddComponent<ItemDamageBuff>();
            buff.ApplyOrRefresh(1f + Mathf.Max(0f, effect.value), effect.duration, tint);
        }

        private static void ApplySlowFall(Transform root, ItemEffect effect, Color tint)
        {
            var status = root.GetComponentInChildren<SlowFallStatus>();
            if (status == null) status = root.gameObject.AddComponent<SlowFallStatus>();
            status.ApplyOrRefresh(effect.value, effect.duration, tint);
        }

        private static void ApplyTrap(Transform root, ItemEffect effect, Vector2 trapKnockbackForce)
        {
            var receiver = root.GetComponentInChildren<IPercentReceiver>();
            receiver?.AddPercent(effect.value);

            var body = root.GetComponentInChildren<Rigidbody2D>();
            if (body == null) return;

            Vector2 force = trapKnockbackForce;
            if (Mathf.Abs(force.x) > 0.001f)
                force.x *= root.localScale.x >= 0f ? -1f : 1f; // đẩy ngược hướng mặt.
            body.AddForce(force, ForceMode2D.Impulse);
        }
    }
}
