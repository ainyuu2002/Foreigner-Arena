using UnityEngine;
using FA.Core;

namespace FA.Items
{
    /// <summary>
    /// Pickup hook for Mystery_Block_66. The player must stand on the trigger and crouch to consume it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TemporaryMysteryBlockPickup : MonoBehaviour
    {
        [SerializeField] private GameObject blockRoot;
        [SerializeField] private MysteryBlockOutcomeConfigSO outcomeConfig;
        [Tooltip("Fallback display time for instant effects. Timed buffs use their own durationSeconds.")]
        [SerializeField] private float debugMessageDuration = 8f;
        [Range(-1f, 0f)] [SerializeField] private float crouchThreshold = -0.5f;

        [Header("Temporary visuals / trap tuning")]
        [SerializeField] private Color damageBuffTint = new Color(1f, 0.35f, 0.25f, 1f);
        [SerializeField] private Color weatherImmunityTint = new Color(0.45f, 0.85f, 1f, 1f);
        [SerializeField] private Color slowFallTint = new Color(0.65f, 0.9f, 1f, 1f);
        [SerializeField] private Vector2 trapKnockbackForce = new Vector2(5f, 8f);

        private bool _pickedUp;

        private void Awake()
        {
            if (blockRoot == null || blockRoot.transform.root != transform.root)
                blockRoot = transform.root.gameObject;
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_pickedUp || !IsPlayer(other)) return;

            Transform root = ResolvePlayerRoot(other);
            if (root == null || !IsActivelyCrouching(root)) return;
            if (IsInHitstun(root)) return;

            _pickedUp = true;
            ApplyRandomOutcome(root);
            Destroy(blockRoot != null ? blockRoot : gameObject);
        }

        private void ApplyRandomOutcome(Transform playerRoot)
        {
            MysteryBlockOutcome outcome = outcomeConfig != null
                ? outcomeConfig.PickRandom()
                : new MysteryBlockOutcome();

            string effectText = MysteryBlockOutcomeApplier.Apply(
                playerRoot,
                outcome,
                damageBuffTint,
                weatherImmunityTint,
                slowFallTint,
                trapKnockbackForce);

            MysteryBlockPickupDebugText.ShowImportant(
                $"{playerRoot.name} picked Mystery Block: {outcome.DebugSummary()}\n{effectText}",
                GetDebugDuration(outcome));
        }

        private float GetDebugDuration(MysteryBlockOutcome outcome)
        {
            if (outcome != null && outcome.durationSeconds > 0f)
                return outcome.durationSeconds;

            return debugMessageDuration;
        }

        private static Transform ResolvePlayerRoot(Collider2D other)
        {
            return other.attachedRigidbody != null
                ? other.attachedRigidbody.transform
                : other.transform.root;
        }

        private bool IsActivelyCrouching(Transform playerRoot)
        {
            var moveInput = playerRoot.GetComponentInChildren<IMoveInput>();
            return moveInput != null && moveInput.MoveY <= crouchThreshold;
        }

        private static bool IsInHitstun(Transform playerRoot)
        {
            var combat = playerRoot.GetComponentInChildren<ICombatContext>();
            return combat != null && combat.IsInHitstun;
        }

        private static bool IsPlayer(Collider2D other)
        {
            int layer = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject.layer
                : other.gameObject.layer;

            return layer == GameLayers.Player1 || layer == GameLayers.Player2;
        }
    }
}
