using UnityEngine;
using FA.Core;

namespace FA.Items
{
    public static class MysteryBlockOutcomeApplier
    {
        public static string Apply(
            Transform playerRoot,
            MysteryBlockOutcome outcome,
            Color damageBuffTint,
            Color weatherImmunityTint,
            Color slowFallTint,
            Vector2 trapKnockbackForce)
        {
            if (playerRoot == null)
                return "No player target found.";

            if (outcome == null)
                outcome = new MysteryBlockOutcome();

            switch (outcome.type)
            {
                case MysteryBlockOutcomeType.ResistanceRecovery:
                    return AddPercent(playerRoot, -outcome.value, "Recovered resistance");

                case MysteryBlockOutcomeType.ManaRecovery:
                    return AddMana(playerRoot, outcome.value);

                case MysteryBlockOutcomeType.WeatherImmunity:
                    ApplyTimedStatus(playerRoot, outcome, weatherImmunityTint);
                    return FormatTimed("Weather immunity active", outcome);

                case MysteryBlockOutcomeType.ResistanceIncreaseDebuff:
                    return AddPercent(playerRoot, outcome.value, "Resistance debuff applied");

                case MysteryBlockOutcomeType.SlowFall:
                    ApplyTimedStatus(playerRoot, outcome, slowFallTint);
                    return FormatTimed("Slow fall active", outcome);

                case MysteryBlockOutcomeType.DamageBuff:
                    return ApplyDamageBuff(playerRoot, outcome, damageBuffTint);

                case MysteryBlockOutcomeType.TrapBall:
                    return ApplyTrap(playerRoot, outcome, trapKnockbackForce);

                default:
                    return $"Unhandled outcome: {outcome.type}";
            }
        }

        private static string AddPercent(Transform playerRoot, float amount, string label)
        {
            var receiver = playerRoot.GetComponentInChildren<IPercentReceiver>();
            if (receiver == null)
                return $"{label}: no IPercentReceiver on player.";

            receiver.AddPercent(amount);
            return $"{label}: {(amount >= 0f ? "+" : string.Empty)}{amount * 100f:0.#}%.";
        }

        private static string AddMana(Transform playerRoot, float fraction)
        {
            var mana = playerRoot.GetComponentInChildren<IManaSpender>();
            if (mana == null)
                return "Mana recovery: no IManaSpender on player.";

            mana.AddMana(fraction);
            return $"Mana recovered: +{fraction * 100f:0.#}% max mana.";
        }

        private static string ApplyDamageBuff(Transform playerRoot, MysteryBlockOutcome outcome, Color tint)
        {
            var buff = playerRoot.GetComponentInChildren<ItemDamageBuff>();
            if (buff == null)
                buff = playerRoot.gameObject.AddComponent<ItemDamageBuff>();

            float multiplier = 1f + Mathf.Max(0f, outcome.value);
            buff.ApplyOrRefresh(multiplier, outcome.durationSeconds, tint);
            return $"Damage buff active: x{multiplier:0.##} for {outcome.durationSeconds:0.#}s.";
        }

        private static void ApplyTimedStatus(Transform playerRoot, MysteryBlockOutcome outcome, Color tint)
        {
            var statuses = playerRoot.GetComponents<MysteryBlockTimedStatus>();
            for (int i = 0; i < statuses.Length; i++)
            {
                if (statuses[i] != null && statuses[i].Type == outcome.type)
                {
                    statuses[i].ApplyOrRefresh(outcome, tint);
                    return;
                }
            }

            var status = playerRoot.gameObject.AddComponent<MysteryBlockTimedStatus>();
            status.ApplyOrRefresh(outcome, tint);
        }

        private static string ApplyTrap(Transform playerRoot, MysteryBlockOutcome outcome, Vector2 trapKnockbackForce)
        {
            var receiver = playerRoot.GetComponentInChildren<IPercentReceiver>();
            if (receiver != null)
                receiver.AddPercent(outcome.value);

            var body = playerRoot.GetComponentInChildren<Rigidbody2D>();
            if (body != null)
            {
                Vector2 force = trapKnockbackForce;
                if (Mathf.Abs(force.x) > 0.001f)
                    force.x *= playerRoot.localScale.x >= 0f ? -1f : 1f;

                body.AddForce(force, ForceMode2D.Impulse);
            }

            string percent = receiver != null ? $"+{outcome.value * 100f:0.#}% resistance" : "no resistance receiver";
            string knockback = body != null ? "knockback applied" : "no Rigidbody2D";
            return $"Trap triggered: {percent}, {knockback}.";
        }

        private static string FormatTimed(string label, MysteryBlockOutcome outcome)
        {
            return $"{label}: {outcome.value * 100f:0.#}% for {outcome.durationSeconds:0.#}s.";
        }
    }
}
