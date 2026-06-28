using UnityEditor;
using UnityEngine;
using FA.GameFlow;

namespace FA.EditorTools
{
    /// <summary>
    /// PHẦN SYSTEMS của tool "Build Test_Phase12 (GameFlow harness)" (xem <see cref="GameFlowHarnessSetup"/>).
    /// 1 chức năng: dựng GameObject "__GameFlowSystems" gồm <see cref="MatchManager"/>/<see cref="PlayerSpawner"/>/
    /// <see cref="MatchTimer"/>/<see cref="WinCondition"/> + wire toàn bộ kênh/registry/MatchSettings + panel
    /// (lấy từ <see cref="GameFlowHarnessCanvas.Refs"/>). Đặt trong scene MENU (Test_Phase12).
    ///
    /// MatchManager DontDestroyOnLoad sống xuyên suốt; PlayerSpawner Instantiate player vào scene map additive.
    /// EDITOR-ONLY. Chỉ add/wire qua SerializedObject (Unity-native), KHÔNG sửa code gameplay.
    /// </summary>
    internal static class GameFlowHarnessSystems
    {
        public const string RootName = "__GameFlowSystems";

        public sealed class Refs
        {
            public MatchManager MatchManager;
            public PlayerSpawner PlayerSpawner;
            public MatchTimer MatchTimer;
            public WinCondition WinCondition;
        }

        public static Refs Build(GameFlowHarnessAssets.Bundle b, GameFlowHarnessCanvas.Refs ui)
        {
            var r = new Refs();
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create GameFlow Systems");

            r.MatchTimer = root.AddComponent<MatchTimer>();
            r.WinCondition = root.AddComponent<WinCondition>();
            r.PlayerSpawner = root.AddComponent<PlayerSpawner>();
            r.MatchManager = root.AddComponent<MatchManager>();

            WireMatchTimer(r.MatchTimer, b);
            WireWinCondition(r.WinCondition, b);
            WirePlayerSpawner(r.PlayerSpawner, b, r.WinCondition);
            WireMatchManager(r.MatchManager, b, r, ui);
            return r;
        }

        private static void WireMatchTimer(MatchTimer c, GameFlowHarnessAssets.Bundle b)
        {
            var so = new SerializedObject(c);
            SetObj(so, "settings", b.MatchSettings);
            SetObj(so, "onTimerChanged", b.TimerChanged);
            SetObj(so, "onTimeUp", b.TimeUp);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c); // BẮT BUỘC: không SetDirty → object-ref không flush vào scene khi SaveScene → field null lúc Play.
        }

        private static void WireWinCondition(WinCondition c, GameFlowHarnessAssets.Bundle b)
        {
            var so = new SerializedObject(c);
            SetObj(so, "settings", b.MatchSettings);
            SetObj(so, "onOutOfLives", b.OutOfLives);
            SetObj(so, "onTimeUp", b.TimeUp);
            SetObj(so, "onMatchResult", b.MatchResult);
            SetObj(so, "onMatchEnded", b.MatchEnded);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c); // BẮT BUỘC: flush object-ref vào scene khi SaveScene.
        }

        private static void WirePlayerSpawner(PlayerSpawner c, GameFlowHarnessAssets.Bundle b, WinCondition win)
        {
            var so = new SerializedObject(c);
            SetObj(so, "characterRegistry", b.CharacterRegistry);
            SetObj(so, "winCondition", win);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(c); // BẮT BUỘC: flush object-ref vào scene khi SaveScene.
        }

        private static void WireMatchManager(MatchManager c, GameFlowHarnessAssets.Bundle b,
                                             Refs r, GameFlowHarnessCanvas.Refs ui)
        {
            var so = new SerializedObject(c);
            // Panels.
            SetObj(so, "charSelectPanel", ui.CharSelectPanel);
            SetObj(so, "stageSelectPanel", ui.StageSelectPanel);
            SetObj(so, "endScreenPanel", ui.EndScreenPanel);
            SetObj(so, "matchHud", ui.HudPanel);
            // Systems.
            SetObj(so, "playerSpawner", r.PlayerSpawner);
            SetObj(so, "matchTimer", r.MatchTimer);
            SetObj(so, "stageRegistry", b.StageRegistry);
            // Kênh nghe.
            SetObj(so, "onCharSelectedP1", b.CharSelP1);
            SetObj(so, "onCharSelectedP2", b.CharSelP2);
            SetObj(so, "onStageSelected", b.StageSel);
            SetObj(so, "onRematchPressed", b.Rematch);
            SetObj(so, "onBackToCharSelect", b.BackToCharSelect);
            SetObj(so, "onMatchEnded", b.MatchEnded);
            so.ApplyModifiedPropertiesWithoutUndo();
            // ROOT-CAUSE FIX: trước đây THIẾU dòng này → mọi field channel/panel/system của MatchManager
            // không được flush vào scene asset khi SaveScene → null lúc Play → OnEnable subscribe P1=False...
            EditorUtility.SetDirty(c);
        }

        private static void SetObj(SerializedObject so, string prop, Object val)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = val;
        }
    }
}
