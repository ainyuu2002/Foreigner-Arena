using UnityEngine;

namespace FA.AI
{
    [CreateAssetMenu(menuName = "FA/AI/CPU Config", fileName = "CPUConfig")]
    public sealed class CPUConfigSO : ScriptableObject
    {
        [Header("Timing - CẦN TEST")]
        [Min(0f)] public float reactionDelay = 0.22f;
        [Min(0.02f)] public float decisionInterval = 0.18f;
        [Min(0.02f)] public float commitmentDuration = 0.28f;
        [Range(0f, 1f)] public float mistakeChance = 0.12f;

        [Header("Spacing - CẦN TEST")]
        [Min(0f)] public float preferredRange = 2.4f;
        [Min(0f)] public float lightRange = 1.5f;
        [Min(0f)] public float mediumRange = 2.2f;
        [Min(0f)] public float heavyRange = 3f;
        [Min(0f)] public float verticalPursuitHeight = 1.4f;
        [Min(0.02f)] public float pursuitJumpCooldown = 0.35f;

        [Header("Recovery - CẦN TEST")]
        [Min(0.1f)] public float groundProbeDistance = 4f;
        [Min(0.1f)] public float recoveryHorizontalDistance = 8f;
        [Min(0f)] public float recoveryVerticalOffset = 2f;
        [Min(0.02f)] public float recoveryJumpCooldown = 0.28f;
        [Min(0.02f)] public float recoveryDashCooldown = 0.42f;

        [Header("Adaptation - CẦN TEST")]
        [Range(0f, 1f)] public float adaptationStrength = 0.18f;
        [Range(0f, 1f)] public float adaptationClamp = 0.2f;
        [Min(0f)] public float observationRate = 2f;
        [Min(0f)] public float observationDecayPerSecond = 0.35f;
    }
}
