# P2 Adaptive CPU Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a fair, medium-strength adaptive CPU for P2 whenever no gamepad exists at match spawn, while preserving the existing input buffer and all human-control behavior.

**Architecture:** `CharacterMotor` selects between its existing Unity Input System path and an optional `IInputSnapshotSource` declared in Core. A new `FA.AI` module observes public world state, chooses a utility-based intention, and emits ordinary `InputSnapshot` values. `PlayerSpawner` activates that source once at spawn through the existing `BroadcastMessage` boundary.

**Tech Stack:** Unity 6000.5.0f1, C# 9, Unity Input System 1.19.0, Rigidbody2D/Physics2D, ScriptableObject, NUnit/EditMode tests, Unity MCP.

## Global Constraints

- Every runtime module references only `FA.Core`; no cross-module class references.
- Physics and stage sensing use Unity-native `Rigidbody2D`, colliders, and `Physics2D` queries.
- CPU output must pass through the existing `InputBuffer` and `CommandParser`.
- All CPU balance values live in `CPUConfigSO` and are marked **CẦN TEST**.
- Version one may press movement, Jump, Dash, L, M, and H only; Super1, Super2, and Rage are always false.
- CPU selection is locked until player objects are spawned again.
- Each file has one responsibility and remains below 500 lines.
- After every C# edit, compile-check with Unity MCP `assets-refresh` followed by Error-filtered `console-get-logs`.

---

## File Map

- Create `Assets/_Project/Scripts/Core/Input/IInputSnapshotSource.cs`: alternate input contract.
- Create `Assets/_Project/Scripts/Core/Input/LocalInputDevices.cs`: Core-owned gamepad availability query.
- Modify `Assets/_Project/Scripts/Movement/CharacterMotor.cs`: select human or alternate snapshots without changing FSM behavior.
- Create `Assets/_Project/Scripts/AI/FA.AI.asmdef`: isolated AI module referencing Core only.
- Create `Assets/_Project/Scripts/AI/CPUIntent.cs`: intention enum and immutable perception/input records.
- Create `Assets/_Project/Scripts/AI/CPUConfigSO.cs`: all balanced tuning values.
- Create `Assets/_Project/Scripts/AI/CPUOpponentModel.cs`: decaying, clamped observations.
- Create `Assets/_Project/Scripts/AI/CPUUtilityBrain.cs`: hard priorities and utility scoring.
- Create `Assets/_Project/Scripts/AI/CPUActionExecutor.cs`: intention-to-held-input sequences.
- Create `Assets/_Project/Scripts/AI/CPUInputSource.cs`: MonoBehaviour coordinator, perception, physics probes, and Core contract implementation.
- Modify `Assets/_Project/Scripts/GameFlow/PlayerSpawner.cs`: one-time P2 CPU activation decision.
- Create `Assets/_Project/Tests/EditMode/FA.AI.Tests.asmdef`: EditMode test assembly.
- Create `Assets/_Project/Tests/EditMode/InputSourceTests.cs`: alternate-input and edge tests.
- Create `Assets/_Project/Tests/EditMode/CPUOpponentModelTests.cs`: decay/clamp/reset tests.
- Create `Assets/_Project/Tests/EditMode/CPUUtilityBrainTests.cs`: priority, delay, adaptation, and disallowed-input tests.
- Create `Assets/_Project/Tests/EditMode/PlayerControlSelectionTests.cs`: spawn-mode resolver tests.
- Create `Assets/_Project/Data/AI/CPU_Balanced.asset`: one balanced profile.
- Modify P2 variants `Assets/_Project/Prefabs/Harness_FighterP2.prefab`, `Harness_WolverineP2.prefab`, and `Harness_BlackHeartP2.prefab`: add and wire `CPUInputSource`.

---

### Task 1: Alternate Snapshot Boundary

**Files:**
- Create: `Assets/_Project/Scripts/Core/Input/IInputSnapshotSource.cs`
- Modify: `Assets/_Project/Scripts/Movement/CharacterMotor.cs`
- Create: `Assets/_Project/Tests/EditMode/FA.AI.Tests.asmdef`
- Create: `Assets/_Project/Tests/EditMode/InputSourceTests.cs`

**Interfaces:**
- Produces: `bool IInputSnapshotSource.IsInputActive`, `Vector2 IInputSnapshotSource.Move`, and `InputSnapshot IInputSnapshotSource.ReadSnapshot(int frame)`.
- Preserves: `CharacterMotor.Parser`, `IMoveInput.MoveX/MoveY`, and all existing device-pairing behavior.

- [ ] **Step 1: Create the Core-only EditMode assembly and add a failing alternate-source contract test**

```json
{
  "name": "FA.AI.Tests",
  "rootNamespace": "FA.AI.Tests",
  "references": ["FA.Core"],
  "includePlatforms": ["Editor"],
  "optionalUnityReferences": ["TestAssemblies"]
}
```

```csharp
using NUnit.Framework;
using UnityEngine;
using FA.Core;

namespace FA.AI.Tests
{
    public sealed class InputSourceTests
    {
        private sealed class FakeSource : IInputSnapshotSource
        {
            public bool IsInputActive => true;
            public Vector2 Move => Vector2.right;
            public InputSnapshot ReadSnapshot(int frame) =>
                new InputSnapshot(1, 0, false, false, true, false, false,
                                  false, false, false, frame);
        }

        [Test]
        public void CpuSnapshots_CreateOneLightPressEdge()
        {
            var buffer = new InputBuffer(8);
            var parser = new CommandParser(buffer);
            buffer.Push(new InputSnapshot(0, 0, false, false, false, false, false, false, false, false, 1));
            var source = new FakeSource();
            buffer.Push(source.ReadSnapshot(2));
            Assert.That(parser.WasPressedWithin(InputButton.L, 4), Is.True);
            parser.ConsumePress(InputButton.L, 4);
            Assert.That(parser.WasPressedWithin(InputButton.L, 4), Is.False);
        }
    }
}
```

- [ ] **Step 2: Run the focused test and verify the new test assembly/source contract is not yet complete**

Run Unity MCP `tests-run` with `testMode: EditMode` and filter `FA.AI.Tests.InputSourceTests`.
Expected: FAIL until the test assembly and Core contract compile.

- [ ] **Step 3: Add the Core contract**

```csharp
using UnityEngine;

namespace FA.Core
{
    public interface IInputSnapshotSource
    {
        bool IsInputActive { get; }
        Vector2 Move { get; }
        InputSnapshot ReadSnapshot(int frame);
    }
}
```

- [ ] **Step 4: Integrate the source into `CharacterMotor`**

Add `_alternateInput = GetComponent<IInputSnapshotSource>();` in `Awake`. In `Update`, replace only the raw move read with:

```csharp
bool useAlternate = _alternateInput != null && _alternateInput.IsInputActive;
Vector2 move = useAlternate ? _alternateInput.Move
                            : _controls != null ? _controls.Gameplay.Move.ReadValue<Vector2>() : Vector2.zero;
_moveX = move.x;
_moveY = move.y;
_jumpPressed = _parser != null && _parser.WasPressedWithin(InputButton.Jump, EdgeWindow);
_dashPressed = _parser != null && _parser.WasPressedWithin(InputButton.Dash, EdgeWindow);
```

In `PushInputSnapshot`, increment `_fixedFrame` once, then push alternate input when active; otherwise retain the current Input System snapshot construction:

```csharp
_fixedFrame++;
if (_alternateInput != null && _alternateInput.IsInputActive)
{
    InputSnapshot alternate = _alternateInput.ReadSnapshot(_fixedFrame);
    _inputBuffer.Push(in alternate);
    return;
}
```

- [ ] **Step 5: Compile-check and run the focused test**

Run `assets-refresh`, Error-filtered `console-get-logs`, then `tests-run` for `InputSourceTests`.
Expected: zero `error CS`; test PASS.

- [ ] **Step 6: Commit the boundary**

```powershell
git add Assets/_Project/Scripts/Core/Input/IInputSnapshotSource.cs Assets/_Project/Scripts/Movement/CharacterMotor.cs Assets/_Project/Tests/EditMode
git commit -m "feat: support alternate fighter input snapshots"
```

---

### Task 2: Pure CPU Model, Utility Brain, and Executor

**Files:**
- Create: `Assets/_Project/Scripts/AI/FA.AI.asmdef`
- Create: `Assets/_Project/Scripts/AI/CPUIntent.cs`
- Create: `Assets/_Project/Scripts/AI/CPUConfigSO.cs`
- Create: `Assets/_Project/Scripts/AI/CPUOpponentModel.cs`
- Create: `Assets/_Project/Scripts/AI/CPUUtilityBrain.cs`
- Create: `Assets/_Project/Scripts/AI/CPUActionExecutor.cs`
- Create: `Assets/_Project/Tests/EditMode/CPUOpponentModelTests.cs`
- Create: `Assets/_Project/Tests/EditMode/CPUUtilityBrainTests.cs`

**Interfaces:**
- Produces: `CPUIntent CPUUtilityBrain.Decide(in CPUPerceptionFrame frame, CPUOpponentProfile profile, float random01)`.
- Produces: `CPUHeldInput CPUActionExecutor.Tick(CPUIntent intent, in CPUPerceptionFrame frame, float dt)`.
- Consumes: tuning from `CPUConfigSO`; no GameObject or module-specific types in pure decision classes.

- [ ] **Step 1: Write failing tests for survival priority, reaction delay, clamp, and allowed buttons**

First add `FA.AI` to the test assembly references so the new tests compile once the runtime module exists:

```json
"references": ["FA.Core", "FA.AI"]
```

```csharp
[Test]
public void RecoveryOverridesAttack()
{
    CPUConfigSO config = ScriptableObject.CreateInstance<CPUConfigSO>();
    var brain = new CPUUtilityBrain(config);
    CPUPerceptionFrame frame = CPUPerceptionFrame.TestFrame(
        distance: 1f, offStage: true, opponentCommitted: true, threatVisibleFor: 1f);
    Assert.That(brain.Decide(frame, CPUOpponentProfile.Neutral, 0.5f), Is.EqualTo(CPUIntent.RecoverToStage));
}

[Test]
public void DefenseWaitsForReactionDelay()
{
    CPUConfigSO config = ScriptableObject.CreateInstance<CPUConfigSO>();
    config.reactionDelay = 0.22f;
    var brain = new CPUUtilityBrain(config);
    CPUPerceptionFrame frame = CPUPerceptionFrame.TestFrame(
        distance: 1f, offStage: false, opponentCommitted: true, threatVisibleFor: 0.1f);
    Assert.That(brain.Decide(frame, CPUOpponentProfile.Neutral, 0.5f), Is.Not.EqualTo(CPUIntent.Defend));
}

[Test]
public void ExecutorNeverEmitsForbiddenButtons()
{
    CPUConfigSO config = ScriptableObject.CreateInstance<CPUConfigSO>();
    var executor = new CPUActionExecutor(config);
    CPUHeldInput input = executor.Tick(CPUIntent.AttackHeavy, CPUPerceptionFrame.TestFrame(), 0.02f);
    Assert.That(input.Super1 || input.Super2 || input.Rage, Is.False);
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run Unity MCP `tests-run`, EditMode, filters `CPUOpponentModelTests` and `CPUUtilityBrainTests`.
Expected: FAIL with missing AI types.

- [ ] **Step 3: Add immutable domain records and config**

Define `CPUIntent` values `Wait`, `Approach`, `KeepDistance`, `AttackLight`, `AttackMedium`, `AttackHeavy`, `AntiAir`, `Defend`, `EscapeImmediateDanger`, and `RecoverToStage`.

`CPUHeldInput` contains `sbyte DirX/DirY` and held booleans for Jump, Dash, L, M, H. Its `ToSnapshot(int frame)` always passes `false, false, false` for Super1, Super2, Rage.

`CPUConfigSO` exposes serialized/public **CẦN TEST** fields with safe defaults: `reactionDelay=0.22f`, `decisionInterval=0.18f`, `commitmentDuration=0.28f`, `mistakeChance=0.12f`, `preferredRange=2.4f`, `lightRange=1.5f`, `mediumRange=2.2f`, `heavyRange=3f`, `groundProbeDistance=4f`, `recoveryHorizontalDistance=8f`, `recoveryVerticalOffset=2f`, `adaptationStrength=0.18f`, `adaptationClamp=0.2f`, and `observationDecayPerSecond=0.35f`.

- [ ] **Step 4: Implement bounded opponent observations**

```csharp
public void Observe(in CPUPerceptionFrame frame, float dt)
{
    float decay = Mathf.Clamp01(config.observationDecayPerSecond * dt);
    jump = Mathf.Lerp(jump, frame.OpponentGrounded ? 0f : 1f, decay);
    approach = Mathf.Lerp(approach, frame.OpponentMovingTowardSelf ? 1f : 0f, decay);
    passive = Mathf.Lerp(passive, frame.OpponentPassive ? 1f : 0f, decay);
    spacing = Mathf.Lerp(spacing, frame.Distance > config.preferredRange ? 1f : 0f, decay);
}

private float Modifier(float sample) =>
    Mathf.Clamp((sample - 0.5f) * config.adaptationStrength,
                -config.adaptationClamp, config.adaptationClamp);
```

- [ ] **Step 5: Implement hard priorities and utility scoring**

`Decide` returns recovery first, then environmental escape, then defense only when `ThreatVisibleFor >= reactionDelay`. Ordinary scores combine distance and the clamped profile. Apply `mistakeChance` only to ordinary choices and never to survival priorities.

- [ ] **Step 6: Implement held-input execution**

The executor emits screen-relative directions, pulses Jump/Dash/L/M/H for one fixed snapshot, releases them on the next snapshot, and holds movement for the commitment window. `Defend` holds away from the opponent; recovery steers toward `StageCenterX` and spaces jump/dash pulses using config cooldowns.

- [ ] **Step 7: Compile-check and run all pure AI tests**

Run `assets-refresh`, Error-filtered `console-get-logs`, then `tests-run` EditMode for `FA.AI.Tests`.
Expected: zero `error CS`; all pure tests PASS.

- [ ] **Step 8: Commit the pure AI**

```powershell
git add Assets/_Project/Scripts/AI Assets/_Project/Tests/EditMode
git commit -m "feat: add balanced adaptive CPU decision model"
```

---

### Task 3: Runtime Perception and CPU Input Source

**Files:**
- Create: `Assets/_Project/Scripts/AI/CPUInputSource.cs`
- Modify: `Assets/_Project/Tests/EditMode/InputSourceTests.cs`

**Interfaces:**
- Implements: `IInputSnapshotSource`.
- Receives existing wiring message: `void SetOpponent(Transform opponent)`.
- Receives activation message: `void SetCpuControl(bool active)`.

- [ ] **Step 1: Add failing activation/locking and neutral-without-opponent tests**

```csharp
[Test]
public void CpuSource_IsNeutralUntilActivatedAndWired()
{
    var go = new GameObject("CPU");
    var source = go.AddComponent<CPUInputSource>();
    source.SetCpuControl(true);
    InputSnapshot snap = source.ReadSnapshot(1);
    Assert.That(snap.DirX, Is.Zero);
    Assert.That(snap.L || snap.M || snap.H, Is.False);
    Object.DestroyImmediate(go);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run Unity MCP `tests-run`, EditMode, filter `InputSourceTests`.
Expected: FAIL with missing `CPUInputSource`.

- [ ] **Step 3: Implement the coordinator**

`CPUInputSource` caches its own `Rigidbody2D` and `ICombatContext`, accepts opponent wiring, computes stage center as the midpoint of initial fighter positions, and builds one `CPUPerceptionFrame` per `ReadSnapshot`.

Use `Physics2D.Raycast(transform.position, Vector2.down, config.groundProbeDistance, GameLayers.StandableMask)` for safe-ground perception and the existing Core context for grounded/hitstun/facing. Determine opponent commitment from its `ICombatContext`: current state not equal to its Idle/Walk states, with a timer that starts only after that observable state begins.

Coordinator flow:

```csharp
public InputSnapshot ReadSnapshot(int frame)
{
    if (!active || opponent == null || config == null)
        return CPUHeldInput.Neutral.ToSnapshot(frame);

    CPUPerceptionFrame perceived = perception.Capture(frameDelta);
    model.Observe(perceived, frameDelta);
    decisionRemaining -= frameDelta;
    if (decisionRemaining <= 0f)
    {
        currentIntent = brain.Decide(perceived, model.Profile, Random.value);
        decisionRemaining = config.decisionInterval;
    }
    held = executor.Tick(currentIntent, perceived, frameDelta);
    move = new Vector2(held.DirX, held.DirY);
    return held.ToSnapshot(frame);
}
```

- [ ] **Step 4: Compile-check and run source tests**

Run `assets-refresh`, Error-filtered `console-get-logs`, then focused EditMode tests.
Expected: zero `error CS`; PASS.

- [ ] **Step 5: Commit runtime AI**

```powershell
git add Assets/_Project/Scripts/AI/CPUInputSource.cs Assets/_Project/Tests/EditMode/InputSourceTests.cs
git commit -m "feat: drive CPU snapshots from world perception"
```

---

### Task 4: One-Time P2 Control Selection

**Files:**
- Create: `Assets/_Project/Scripts/Core/Input/LocalInputDevices.cs`
- Modify: `Assets/_Project/Scripts/GameFlow/PlayerSpawner.cs`
- Create: `Assets/_Project/Tests/EditMode/PlayerControlSelectionTests.cs`

**Interfaces:**
- Produces: `LocalInputDevices.HasGamepad`.
- Produces: `PlayerSpawner.ShouldEnableCpu(PlayerSlot slot, bool hasGamepad)` for deterministic testing.
- Activates: `_playerP2.BroadcastMessage("SetCpuControl", true, DontRequireReceiver)` exactly once per spawn.

- [ ] **Step 1: Write failing selection tests**

Add `FA.GameFlow` to the test assembly references:

```json
"references": ["FA.Core", "FA.AI", "FA.GameFlow"]
```

```csharp
[TestCase(PlayerSlot.P1, false, false)]
[TestCase(PlayerSlot.P2, true, false)]
[TestCase(PlayerSlot.P2, false, true)]
public void CpuSelection_IsOnlyP2WithoutGamepad(PlayerSlot slot, bool hasGamepad, bool expected)
{
    Assert.That(PlayerSpawner.ShouldEnableCpu(slot, hasGamepad), Is.EqualTo(expected));
}
```

- [ ] **Step 2: Run and verify failure**

Run Unity MCP `tests-run`, EditMode, filter `PlayerControlSelectionTests`.
Expected: FAIL because resolver does not exist.

- [ ] **Step 3: Add Core-owned device availability**

```csharp
using UnityEngine.InputSystem;

namespace FA.Core
{
    public static class LocalInputDevices
    {
        public static bool HasGamepad => Gamepad.all.Count > 0;
    }
}
```

- [ ] **Step 4: Activate CPU once during spawn**

After spawning both fighters and before normal opponent wiring, evaluate `bool cpuP2 = ShouldEnableCpu(PlayerSlot.P2, LocalInputDevices.HasGamepad);` and broadcast `SetCpuControl(cpuP2)` to P2. Do not subscribe PlayerSpawner or CPU source to device-change events.

```csharp
public static bool ShouldEnableCpu(PlayerSlot slot, bool hasGamepad)
    => slot == PlayerSlot.P2 && !hasGamepad;
```

- [ ] **Step 5: Compile-check and run selection tests**

Run `assets-refresh`, Error-filtered `console-get-logs`, then the selection tests.
Expected: zero `error CS`; PASS.

- [ ] **Step 6: Commit spawn selection**

```powershell
git add Assets/_Project/Scripts/Core/Input/LocalInputDevices.cs Assets/_Project/Scripts/GameFlow/PlayerSpawner.cs Assets/_Project/Tests/EditMode/PlayerControlSelectionTests.cs
git commit -m "feat: select P2 CPU when no gamepad is present"
```

---

### Task 5: Assets, Prefab Wiring, and End-to-End Verification

**Files:**
- Create: `Assets/_Project/Data/AI/CPU_Balanced.asset`
- Modify: `Assets/_Project/Prefabs/Harness_FighterP2.prefab`
- Modify: `Assets/_Project/Prefabs/Harness_WolverineP2.prefab`
- Modify: `Assets/_Project/Prefabs/Harness_BlackHeartP2.prefab`

**Interfaces:**
- Every P2 registry prefab exposes an inactive `CPUInputSource` with the same balanced config.
- Base character prefabs and P1 variants remain unchanged.

- [ ] **Step 1: Create the balanced ScriptableObject asset through Unity MCP**

Create `Assets/_Project/Data/AI/CPU_Balanced.asset` using the default values declared in Task 2. Treat every value as **CẦN TEST**; do not claim final balance from automated tests.

- [ ] **Step 2: Add and wire CPU source on all registered P2 prefab variants**

Use Unity MCP prefab/component tools to add `FA.AI.CPUInputSource`, assign `CPU_Balanced.asset`, and leave CPU control inactive by default. Inspect the actual serialized component after each save.

- [ ] **Step 3: Refresh and inspect compile errors**

Run `assets-refresh`, then Error-filtered `console-get-logs`.
Expected: zero `error CS` and no missing-script errors.

- [ ] **Step 4: Run the complete EditMode suite**

Run Unity MCP `tests-run` with `testMode: EditMode` and filter `FA.AI.Tests`.
Expected: all tests PASS.

- [ ] **Step 5: Self-review against project rules**

Verify every module reference, changed file length, prefab assignment, disallowed CPU button, ScriptableObject tuning field, and human-input fallback. Inspect `git diff` around `CharacterMotor` and `PlayerSpawner` for regressions.

- [ ] **Step 6: Commit assets and wiring**

```powershell
git add Assets/_Project/Data/AI Assets/_Project/Prefabs/Harness_FighterP2.prefab Assets/_Project/Prefabs/Harness_WolverineP2.prefab Assets/_Project/Prefabs/Harness_BlackHeartP2.prefab
git commit -m "feat: wire balanced CPU to P2 fighter prefabs"
```

- [ ] **Step 7: Stop at the manual playtest gate**

Open the normal Lobby flow. With no gamepad, verify CPU movement, recovery, spacing, delayed defense, L/M/H-only attacks, and subtle adaptation on all three maps. Plugging in a gamepad during the match must not take control; rematch must re-evaluate device presence. Report the exact `CPU_Balanced.asset` fields to tune based on feel.
