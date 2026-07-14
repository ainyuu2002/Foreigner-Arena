using UnityEngine;

namespace FA.Items
{
    /// <summary>
    /// Mystery block runtime behavior: fall upright, lock in place after landing, then start the visual flip.
    /// Later random-buff pickup logic can live here too, so the box stays one coherent item.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class MysteryBlockItem : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Animator visualAnimator;

        [Header("Landing")]
        [SerializeField] private float minGroundNormalY = 0.5f;
        [SerializeField] private float maxLandingSpeedY = 0.2f;

        private Rigidbody2D _body;
        private bool _landed;
        private Coroutine _despawnRoutine;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();

            if (visualAnimator == null)
                visualAnimator = GetComponentInChildren<Animator>(true);

            ResetForDrop();
        }

        public void ResetForDrop()
        {
            if (_body == null)
                _body = GetComponent<Rigidbody2D>();

            _landed = false;

            if (_body != null)
            {
                _body.bodyType = RigidbodyType2D.Dynamic;
                _body.gravityScale = 1f;
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
                _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            if (visualAnimator == null)
                visualAnimator = GetComponentInChildren<Animator>(true);

            if (visualAnimator != null)
            {
                visualAnimator.enabled = false;
                visualAnimator.transform.localRotation = Quaternion.identity;
            }
        }

        public void ResetForTeleportIn()
        {
            ResetForDrop();
            Land();
        }

        public void StartLifetime(float lifetimeSeconds)
        {
            if (_despawnRoutine != null)
                StopCoroutine(_despawnRoutine);

            if (lifetimeSeconds > 0f)
                _despawnRoutine = StartCoroutine(DespawnAfter(lifetimeSeconds));
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryLand(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryLand(collision);
        }

        private void TryLand(Collision2D collision)
        {
            if (_landed || _body == null) return;
            if (Mathf.Abs(_body.linearVelocity.y) > maxLandingSpeedY) return;

            for (int i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < minGroundNormalY) continue;

                Land();
                return;
            }
        }

        private void Land()
        {
            _landed = true;
            _body.linearVelocity = Vector2.zero;
            _body.angularVelocity = 0f;
            _body.gravityScale = 0f;
            _body.bodyType = RigidbodyType2D.Kinematic;
            _body.constraints = RigidbodyConstraints2D.FreezeAll;

            if (visualAnimator != null)
            {
                visualAnimator.enabled = true;
                visualAnimator.Play(0, 0, 0f);
            }
        }

        private System.Collections.IEnumerator DespawnAfter(float lifetimeSeconds)
        {
            yield return new WaitForSeconds(lifetimeSeconds);
            Destroy(gameObject);
        }
    }
}
