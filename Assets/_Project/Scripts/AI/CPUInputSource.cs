using FA.Core;
using UnityEngine;

namespace FA.AI
{
    /// <summary>
    /// Coordinates observable world perception, utility decisions, and held input for one CPU fighter.
    /// All output still enters CharacterMotor's existing InputBuffer and CommandParser.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class CPUInputSource : MonoBehaviour, IInputSnapshotSource
    {
        [SerializeField] private CPUConfigSO config;

        private Rigidbody2D body;
        private ICombatContext selfContext;
        private Transform opponent;
        private Rigidbody2D opponentBody;
        private ICombatContext opponentContext;

        private CPUOpponentModel opponentModel;
        private CPUUtilityBrain brain;
        private CPUActionExecutor executor;
        private CPUIntent currentIntent;
        private CPUHeldInput held;
        private Vector2 move;
        private float stageCenterX;
        private float threatVisibleFor;
        private float decisionRemaining;
        private float commitmentRemaining;
        private bool active;

        public bool IsInputActive => active;
        public Vector2 Move => move;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            selfContext = GetComponent<ICombatContext>();
            opponentModel = new CPUOpponentModel(config);
            brain = new CPUUtilityBrain(config);
            executor = new CPUActionExecutor(config);
        }

        /// <summary>Called once by PlayerSpawner. No device-change callback can alter this flag.</summary>
        public void SetCpuControl(bool value)
        {
            active = value;
            if (!active) ResetOutput();
        }

        /// <summary>Existing PlayerSpawner opponent-wiring message.</summary>
        public void SetOpponent(Transform value)
        {
            opponent = value;
            opponentBody = opponent != null ? opponent.GetComponent<Rigidbody2D>() : null;
            opponentContext = opponent != null ? opponent.GetComponent<ICombatContext>() : null;
            stageCenterX = opponent != null ? (transform.position.x + opponent.position.x) * 0.5f
                                            : transform.position.x;
            threatVisibleFor = 0f;
            decisionRemaining = 0f;
            commitmentRemaining = 0f;
        }

        public InputSnapshot ReadSnapshot(int frame)
        {
            if (!active || opponent == null || config == null || body == null)
            {
                ResetOutput();
                return held.ToSnapshot(frame);
            }

            float dt = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02f;
            bool opponentCommitted = IsOpponentCommitted();
            threatVisibleFor = opponentCommitted ? threatVisibleFor + dt : 0f;

            CPUPerceptionFrame perception = CapturePerception(opponentCommitted);
            opponentModel.Observe(perception, dt);
            decisionRemaining -= dt;
            commitmentRemaining -= dt;

            CPUIntent candidate = brain.Decide(perception, opponentModel.Profile, Random.value);
            bool urgent = candidate == CPUIntent.RecoverToStage ||
                          candidate == CPUIntent.EscapeImmediateDanger ||
                          candidate == CPUIntent.Defend;
            if (urgent || (decisionRemaining <= 0f && commitmentRemaining <= 0f))
            {
                currentIntent = candidate;
                decisionRemaining = config.decisionInterval;
                commitmentRemaining = config.commitmentDuration;
            }

            held = executor.Tick(currentIntent, perception, dt);
            move = new Vector2(held.DirX, held.DirY);
            return held.ToSnapshot(frame);
        }

        private CPUPerceptionFrame CapturePerception(bool opponentCommitted)
        {
            Vector2 selfPosition = transform.position;
            Vector2 opponentPosition = opponent.position;
            Vector2 opponentVelocity = opponentBody != null ? opponentBody.linearVelocity : Vector2.zero;
            bool selfGrounded = selfContext != null && selfContext.IsGrounded;
            bool opponentGrounded = opponentContext != null && opponentContext.IsGrounded;
            bool selfHitstun = selfContext != null && selfContext.IsInHitstun;
            bool hasGroundBelow = Physics2D.Raycast(
                selfPosition, Vector2.down, config.groundProbeDistance, GameLayers.StandableMask).collider != null;
            bool immediateDanger = Physics2D.OverlapCircle(
                selfPosition, 0.5f, GameLayers.DeathZoneMask) != null;

            return new CPUPerceptionFrame(
                selfPosition, opponentPosition,
                body.linearVelocity, opponentVelocity,
                stageCenterX, threatVisibleFor,
                selfGrounded, selfHitstun,
                opponentGrounded, opponentCommitted,
                hasGroundBelow, immediateDanger);
        }

        private bool IsOpponentCommitted()
        {
            if (opponentContext == null || opponentContext.Machine == null ||
                opponentContext.Machine.CurrentState == null || !opponentContext.IsGrounded ||
                opponentContext.IsInHitstun)
                return false;

            IState state = opponentContext.Machine.CurrentState;
            return !ReferenceEquals(state, opponentContext.IdleState()) &&
                   !ReferenceEquals(state, opponentContext.WalkState());
        }

        private void OnRespawn()
        {
            decisionRemaining = 0f;
            commitmentRemaining = 0f;
            threatVisibleFor = 0f;
            ResetOutput();
        }

        private void ResetOutput()
        {
            currentIntent = CPUIntent.Wait;
            held = CPUHeldInput.Neutral;
            move = Vector2.zero;
        }
    }
}
