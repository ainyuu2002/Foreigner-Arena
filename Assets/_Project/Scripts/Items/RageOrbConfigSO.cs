using UnityEngine;

namespace FA.Items
{
    /// <summary>Các thông số gameplay của Rage Orb.</summary>
    [CreateAssetMenu(menuName = "FA/Items/Rage Orb Config", fileName = "RageOrbConfig")]
    public sealed class RageOrbConfigSO : ScriptableObject
    {
        [Header("Điều kiện phá")]
        [Min(1)] public int hitsToBreak = 5;

        [Min(0f)] public float counterDecayPerSecond = 0.4f;

        [Min(0f)] public float counterDecayDelay = 2f;

        [Header("Bay")]
        [Min(0f)] public float speedMin = 1.75f;

        [Min(0f)] public float speedMax = 3.5f;

        [Min(0.1f)] public float redirectInterval = 2.5f;

        [Min(0)] public int hitstunFramesOnHit = 6;

        public float visualSpinDegreesPerSecond = 35f;

        [Header("Vùng bay")]
        public Vector2 boundsCenter = Vector2.zero;

        public Vector2 boundsSize = new Vector2(20f, 10f);

        [Min(0.05f)] public float worldCollisionRadius = 0.72f;

        [Min(0f)] public float worldCollisionSkin = 0.04f;

        [Header("Vòng đời")]
        [Min(1f)] public float lifetime = 20f;

        [Range(0.05f, 1f)] public float minScaleAtEnd = 0.3f;
    }
}
