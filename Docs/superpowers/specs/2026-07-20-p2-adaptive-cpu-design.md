# P2 Adaptive CPU Design

## Goal

Add a fair, medium-strength CPU controller for P2 when no gamepad is present immediately before a match starts. The control mode is locked for that match and evaluated again on rematch or the next match.

The first version uses full movement, recovery, blocking, anti-air decisions, and normal L/M/H attacks. It does not intentionally use items, specials, supers, Rage, or ultimate attacks.

## Player Experience

- If at least one gamepad is available when players are spawned, P2 remains human-controlled through the existing `Gamepad` input scheme.
- If no gamepad is available, P2 is CPU-controlled for the whole match.
- Connecting or disconnecting a gamepad during a match does not change the selected controller.
- A rematch creates the players again and re-evaluates gamepad availability.
- Character Select and P2 character choice remain unchanged. This version adds no difficulty-selection UI.
- The CPU should feel like a fair intermediate opponent: it applies pressure and punishes obvious mistakes, but has visible reaction delay and sometimes chooses a suboptimal response.

## Architecture

### Input boundary

Add a small input-source contract to `FA.Core`. `CharacterMotor` continues to own `InputBuffer` and `CommandParser`, but obtains each fixed-step `InputSnapshot` from either:

1. the existing Unity Input System reader, or
2. a CPU snapshot source implementing the Core contract.

Movement, combat, specials, and state classes continue consuming the same buffer/parser APIs. They do not know whether input came from a device or CPU.

The human path remains the default. If a CPU source is missing, invalid, or inactive, `CharacterMotor` falls back safely to its existing device path.

### Module boundary

Create `FA.AI` as a new runtime module referencing only `FA.Core`. It must not reference Movement, Combat, Specials, GameFlow, or other gameplay modules.

The AI module is split into focused components:

- `CPUInputSource`: coordinates the CPU and emits an `InputSnapshot` for the current fixed step.
- `CPUPerception`: reads observable Unity state such as transforms, rigidbody velocity, grounded/hitstun state exposed through Core contracts, distance to the opponent, and stage-relative danger.
- `CPUOpponentModel`: maintains decaying short-term measurements of P1 behavior.
- `CPUUtilityBrain`: scores legal high-level intentions and chooses one at a configured decision interval.
- `CPUActionExecutor`: turns the chosen intention into held directions and button press/release sequences.
- `CPUConfigSO`: contains all tuning values and utility weights.

Each class remains single-purpose and below the project limit of 500 lines.

### Activation

P2 character prefabs contain a disabled/inactive CPU source. During `PlayerSpawner.SpawnPlayers`, GameFlow checks `Gamepad.all` once after P2 is instantiated and activates P2 CPU control only when the collection is empty.

Cross-module activation and opponent wiring use a contract declared in Core or the existing one-time `BroadcastMessage` pattern. `FA.GameFlow` must not reference `FA.AI` or `FA.Movement`.

The choice is stored on the spawned P2 and is not changed by `InputSystem.onDeviceChange`. Existing device hot-plug behavior remains intact for human-controlled characters, but cannot take ownership from an active CPU during the match.

## Decision Model

### Hard priorities

The brain evaluates these before ordinary utility scoring:

1. `RecoverToStage`: P2 is outside the safe stage region, falling toward a death zone, or lacks safe ground below.
2. `EscapeImmediateDanger`: P2 is in an imminent environmental hazard or unsafe edge position.
3. `Defend`: a visible opponent attack threatens P2 after the configured reaction delay.

Hard priorities cannot bypass the current state machine. During hitstun, attack recovery, or another state that rejects input, the CPU input is buffered or ignored exactly like human input.

### Ordinary intentions

When no hard priority applies, the brain scores:

- `Approach`
- `KeepDistance`
- `AttackLight`
- `AttackMedium`
- `AttackHeavy`
- `AntiAir`
- `Wait`

Scores use distance, relative velocity, grounded state, stage position, recent choices, commitment state, and opponent-model modifiers. The chosen intention is held for a short commitment window so the CPU does not jitter or reverse direction every frame.

Attack selection respects the configured effective ranges. The executor may press only L, M, or H in version one. It must never generate Jump/Dash combinations or button edges intended to invoke special, super, Rage, ultimate, or item actions.

### Recovery behavior

Recovery is a deterministic survival policy rather than a learned behavior. Based on position, vertical velocity, facing, remaining air options, and nearby wall/ground observations, it can:

- steer toward the stage,
- jump or double-jump,
- air-dash toward safety,
- use wall contact and wall-jump,
- avoid spending all recovery options at the same instant.

Exact safe margins and action timing live in `CPUConfigSO` and require manual gameplay tuning.

## Fairness and Adaptation

The CPU can observe only state that has already occurred in the game world. It must not read P1's current device controls, pending input buffer, private move choice, future animation events, or random outcomes.

Responses are gated by configured reaction delay and decision interval. Optional error probability can replace the best ordinary choice with another legal choice. The CPU receives no damage, speed, resource, physics, or frame-data advantage.

`CPUOpponentModel` tracks a bounded, decaying recent window for:

- jump frequency,
- approach frequency,
- defensive/passive frequency,
- long-range spacing frequency.

These measurements apply small, clamped modifiers to ordinary utility weights. Adaptation cannot override hard survival priorities, remove reaction delay, or exceed the configured maximum modifier. The model resets whenever a new P2 CPU instance is spawned.

Examples:

- Frequent P1 jumps slightly increase `AntiAir` weight.
- Frequent approaches slightly increase defensive spacing and blocking readiness.
- Frequent passive defense slightly increases approach and delayed normal pressure.
- Frequent long-range play slightly increases careful approach instead of reckless chasing.

## Data and Tuning

`CPUConfigSO` owns all gameplay-relevant tuning, including:

- reaction delay range,
- decision interval and commitment duration,
- error probability,
- preferred and attack ranges,
- stage safety margins,
- recovery thresholds and delays,
- base utility weights,
- observation decay/window,
- adaptation strength and clamp.

Initial values are baselines marked **CẦN TEST**. No balance values are hardcoded in behavior classes. A single balanced profile is created for version one; future Easy/Normal/Hard profiles can reuse the same architecture without changing the UI now.

## Failure Handling

- Missing CPU component or config: log a clear warning and keep the existing human/device input path.
- Missing opponent: emit neutral input until opponent wiring succeeds.
- Missing optional perception data: omit the affected utility signal rather than throwing an exception.
- CPU activation with an available gamepad: human input wins because activation is decided only by `PlayerSpawner`.
- Device changes after activation: do not switch controller until players are spawned again.

## Verification

### Automated tests

- No gamepad at spawn activates the P2 CPU source.
- A gamepad at spawn leaves P2 human-controlled.
- Simulated device changes after spawn do not change the locked mode.
- CPU snapshots generate valid held states and press/release edges through `InputBuffer` and `CommandParser`.
- CPU action executor never presses Super1, Super2, Rage, or disallowed item/special commands.
- Recovery and immediate danger override ordinary attack scores.
- Reaction delay prevents immediate defense/anti-air response.
- Adaptation modifiers remain clamped and reset with a new model.
- Missing opponent/config follows the documented safe behavior.

### Unity validation

After every C# change, run Unity MCP `assets-refresh` and inspect Error logs with `console-get-logs`. The change is not compile-complete until there are zero `error CS` entries.

Inspect the real P2 prefab and `CPUConfigSO` asset wiring through Unity MCP. Run relevant EditMode tests and any practical PlayMode tests.

### Manual playtest gate

Launch the normal Lobby flow and verify:

1. With no gamepad, P2 moves and fights as CPU.
2. With a gamepad before starting, P2 remains human-controlled.
3. Plugging in a gamepad during a CPU match does not steal control; rematch re-evaluates it.
4. CPU can return from both sides and from below the stage using movement resources.
5. CPU spacing is stable, does not jitter, and does not endlessly rush or retreat.
6. Block and anti-air responses have perceptible delay and are not perfect.
7. CPU uses L/M/H but does not intentionally use special, super, Rage, ultimate, or items.
8. Repeated P1 jumping, rushing, defending, and zoning cause only subtle, believable adjustment.

Gameplay feel and all **CẦN TEST** values require the user's manual confirmation before the feature is considered complete.

## Out of Scope

- Difficulty-selection UI or multiple CPU profiles.
- Machine learning, persistent player profiling, or cross-match learning.
- Intentional use of specials, supers, Rage, ultimate, or items.
- Mid-match human/CPU takeover.
- Online AI or server-authoritative behavior.
