using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Core;
using FA.GameFlow;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// (MỤC D) DỰNG INTRO + POSE KẾT TRẬN cho Emiya Archer (idempotent). Từ MUGEN (Archer.air):
    ///   1. Copy sprite Intro(act191=group13, múa kiếm) / Win(act181) / Lose(act171) từ dump + import
    ///      PPU100 BottomCenter.
    ///   2. Build 3 clip vào Animations/Emiya Archer/: Intro (loop=false, RecoveryEnd ĐÚNG tick keyframe
    ///      cuối báo "xong"), Win/Lose (CHỈ tail LOOP MUGEN, loopTime=TRUE — pose tuần hoàn sạch thay vì
    ///      giữ 1 frame, KHÔNG event).
    ///   3. Thêm 4 Animator state (Intro=42/Win=43/Lose=44/Draw=45, Draw DÙNG CHUNG clip Lose) + transition
    ///      Any State→state khi StateId==N (Has Exit Time OFF, duration 0, KHÔNG self-transition) vào
    ///      SampleFighter.controller.
    ///   4. Tạo 2 kênh VoidEventChannelSO: OnRoundIntroStart / OnRoundIntroFinished (Data/GameFlow).
    ///   5. Wire scene MAP (Map_Ice/Fire/Swamp): thêm 2 vcam intro (CM_IntroP1/P2, ortho cận) + GO
    ///      "RoundDirector" (RoundIntroController + MatchOutroController), gán kênh + vcam (RoundIntroController
    ///      VÀ MatchOutroController — outro TÁI DÙNG 2 vcam intro để zoom winner, xem MatchOutroController).
    ///   6. Wire MatchManager (Lobby): gán onRoundIntroStart / onRoundIntroFinished.
    /// SFX/VFX Intro/Win/Lose (PoseEffectController) dựng ở tool riêng <see cref="PoseFxSetupTool"/>.
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.IntroWinLoseSetupTool.Run
    /// </summary>
    public static class IntroWinLoseSetupTool
    {
        private const string DumpDir = @"D:\Materials\Archer\Sprite";
        private const string CharArt = "Assets/_Project/Art/Characters/Emiya Archer";
        private const string AnimDir = "Assets/_Project/Animations/Emiya Archer";
        private const string ControllerPath = AnimDir + "/SampleFighter.controller";
        private const string ChannelDir = "Assets/_Project/Data/GameFlow";
        private const string IntroStartPath = ChannelDir + "/OnRoundIntroStart.asset";
        private const string IntroFinishedPath = ChannelDir + "/OnRoundIntroFinished.asset";
        private const string MatchResultPath = ChannelDir + "/OnMatchResultChannel.asset";
        private const string LobbyScenePath = "Assets/_Project/Scenes/Lobby.unity";
        private const float Fps = 60f;

        // (playtest #2, điểm 1) Intro act191 ĐẦY ĐỦ: 3 vòng múa kiếm 13-0..13-11 (7 tick, lần 3 hai
        // frame cuối 8 tick) rồi 13-12 (20t) → 13-13..13-21 (8t) → 13-22 GIỮ. MUGEN giữ 13-22 tới 300
        // tick (~5s) — CAP còn ngần này để đỡ dài (CẦN TEST, giảm thêm nếu playtest thấy lê thê).
        private const int IntroHoldCapTicks = 90;

        private static readonly string[] MapScenes =
        {
            "Assets/_Project/Scenes/Map_Ice.unity",
            "Assets/_Project/Scenes/Map_Fire.unity",
            "Assets/_Project/Scenes/Map_Swamp.unity",
        };

        // Ortho half-height vcam intro — zoom CẬN (nhân vật root ×2.5). CẦN TEST (5-6).
        private const float IntroOrtho = 5.5f;

        // (playtest #2, điểm 1) perPersonTimeout/introTimeout — RoundIntroController/MatchManager ĐÃ TỒN
        // TẠI trong scene từ lần chạy trước (giá trị serialize CŨ không tự theo default C# mới) → tool
        // GHI ĐÈ TƯỜNG MINH mỗi lần chạy (idempotent, giống EnsureIntroCam ghi đè Priority/OrthographicSize).
        private const float PerPersonTimeout = 9f;
        private const float IntroTimeoutSafety = 20f;

        // (folder, sprite dump) — Intro = act191 múa kiếm (group13, 13-0..13-22, ĐỦ trong dump, thay
        // act190 cũ); Win = act181 CHỈ phần LOOP tail 13-205..211 (bỏ lead-in 0,0/13-200..204 — clip đơn
        // Unity loop toàn bộ nên chỉ giữ pose tuần hoàn sạch); Lose = act171 CHỈ loop tail 13-228..231
        // (13-232/233 thiếu trong dump → bỏ; lead-in 13-220..227 bỏ).
        private static readonly (string dir, string[] sprites)[] Sprites =
        {
            ("Intro", new[] { "13-0", "13-1", "13-2", "13-3", "13-4", "13-5", "13-6", "13-7", "13-8",
                              "13-9", "13-10", "13-11", "13-12", "13-13", "13-14", "13-15", "13-16",
                              "13-17", "13-18", "13-19", "13-20", "13-21", "13-22" }),
            ("Win", new[] { "13-205", "13-206", "13-207", "13-208", "13-209", "13-210", "13-211" }),
            ("Lose", new[] { "13-228", "13-229", "13-230", "13-231" }),
        };

        [MenuItem("FA/Tools/Setup Intro + Win/Lose (MỤC D)")]
        public static void Run()
        {
            CopySpritesFromDump();
            AssetDatabase.Refresh();
            ConfigureSpriteImports();

            // Intro (act191 múa kiếm, group13) ĐẦY ĐỦ — 3 vòng múa + đòn kết, xem BuildIntroFrames().
            // Event RecoveryEnd ĐẶT ĐÚNG tại tick keyframe CUỐI (13-22) — bài học MAJOR-1: event SAU
            // keyframe cuối không fire. endTick = recoveryEndTick + IntroHoldCapTicks (giữ khung 13-22
            // thêm 1 đoạn sau khi đã báo "xong" — CAP thay vì 300 tick MUGEN).
            (int tick, string sprite)[] introFrames = BuildIntroFrames();
            int introRecoveryTick = introFrames[introFrames.Length - 1].tick;
            int introEndTick = introRecoveryTick + IntroHoldCapTicks;
            // Tổng dwell tới RecoveryEnd ≈ introRecoveryTick/60 ≈ 5.8s/người (đủ 3 vòng múa — điểm 1);
            // clip đầy đủ (kể cả hold cap) ≈ introEndTick/60 ≈ 7.3s. CẦN TEST — nới/giảm IntroHoldCapTicks
            // nếu playtest thấy lê thê.
            AnimationClip intro = BuildClip("Intro", false, introFrames,
                endTick: introEndTick, recoveryEndTick: introRecoveryTick);

            // Win (act181) — CHỈ tail LOOP MUGEN (13-205..211, mỗi 5 tick), loopTime=TRUE cho pose tuần
            // hoàn sạch (lead-in 0,0/13-200..204 bỏ — clip đơn Unity chỉ loop toàn bộ). endTick = tick
            // frame cuối + 5 để vòng lặp nối mượt về frame đầu (convention Idle loop — ArcherMovementSetupTool).
            AnimationClip win = BuildClip("Win", true, new (int, string)[] {
                (0, "13-205"), (5, "13-206"), (10, "13-207"), (15, "13-208"), (20, "13-209"),
                (25, "13-210"), (30, "13-211") }, endTick: 35, recoveryEndTick: -1);

            // Lose (act171) — CHỈ tail LOOP MUGEN (13-228..231, mỗi 5 tick), loopTime=TRUE. 13-232/233
            // thiếu trong dump → bỏ; lead-in 13-220..227 bỏ (clip đơn Unity chỉ loop toàn bộ).
            AnimationClip lose = BuildClip("Lose", true, new (int, string)[] {
                (0, "13-228"), (5, "13-229"), (10, "13-230"), (15, "13-231") }, endTick: 20, recoveryEndTick: -1);

            AddPoseState("Intro", intro, 42);
            AddPoseState("Win", win, 43);
            AddPoseState("Lose", lose, 44);
            // (playtest #2, điểm 7-8) Draw KHÁC Lose — DÙNG CHUNG clip Lose.anim (pose tuần hoàn), nhưng
            // StateId riêng (45) để PoseEffectController phân biệt và mờ dần thay vì giữ nguyên như Lose.
            AddPoseState("Draw", lose, 45);

            LoadOrCreateVoidChannel(IntroStartPath);
            LoadOrCreateVoidChannel(IntroFinishedPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Load kênh TRONG mỗi hàm wire (SAU khi OpenScene) — load asset TRƯỚC khi mở scene rồi gán vào
            // scene object cho ra fileID:0 (đã gặp); load sau OpenScene (như onMatchResult) thì bền.
            foreach (string mapScene in MapScenes)
                WireMapScene(mapScene);

            WireLobbyMatchManager();

            AssetDatabase.SaveAssets();
            Debug.Log("[IntroWinLose] DONE — 3 clip (Intro/Win/Lose) + 4 Animator state (Intro=42/Win=43/" +
                      "Lose=44/Draw=45) + 2 kênh + wire 3 map (vcam intro + RoundDirector) + MatchManager Lobby.");
        }

        // ---------------------------------------------------------------- intro frame timeline
        /// <summary>
        /// (playtest #2, điểm 1) act191 ĐẦY ĐỦ (KHÔNG bịa — đúng dữ liệu MUGEN cấp): 3 vòng múa kiếm
        /// 13-0..13-11 liên tiếp (mỗi 7 tick; RIÊNG 2 frame cuối của VÒNG 3 — 13-10/13-11 — giữ 8 tick),
        /// rồi 13-12 (20 tick), 13-13..13-21 (mỗi 8 tick), kết ở 13-22 (giữ — xem IntroHoldCapTicks).
        /// KHÔNG thêm frame 48 (sprite 0,0 — P1/P2 khác màu áo, xem điểm 6).
        /// </summary>
        private static (int tick, string sprite)[] BuildIntroFrames()
        {
            var frames = new List<(int tick, string sprite)>();
            int tick = 0;
            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i <= 11; i++)
                {
                    frames.Add((tick, $"13-{i}"));
                    bool lastRoundTail = round == 2 && i >= 10;   // 13-10/13-11 của vòng 3 giữ 8 tick.
                    tick += lastRoundTail ? 8 : 7;
                }
            }
            frames.Add((tick, "13-12"));
            tick += 20;
            for (int i = 13; i <= 21; i++)
            {
                frames.Add((tick, $"13-{i}"));
                tick += 8;
            }
            frames.Add((tick, "13-22"));   // keyframe CUỐI — RecoveryEnd đặt đúng tick này.
            return frames.ToArray();
        }

        // ---------------------------------------------------------------- sprite
        private static void CopySpritesFromDump()
        {
            foreach ((string dir, string[] sprites) in Sprites)
            {
                string projDir = $"{CharArt}/{dir}";
                string absDir = Path.GetFullPath(projDir);
                Directory.CreateDirectory(absDir);

                // Dọn sprite act190 cũ không còn dùng (Intro đổi sang act191/group13) — idempotent, chỉ
                // xoá PNG KHÔNG nằm trong danh sách sprite hiện tại của folder này.
                var keep = new HashSet<string>(sprites.Select(s => s + ".png"));
                foreach (string existing in Directory.GetFiles(absDir, "*.png"))
                {
                    string fileName = Path.GetFileName(existing);
                    if (!keep.Contains(fileName))
                        AssetDatabase.DeleteAsset($"{projDir}/{fileName}");
                }

                foreach (string s in sprites)
                {
                    string dest = Path.Combine(absDir, s + ".png");
                    if (File.Exists(dest)) continue;
                    string src = Path.Combine(DumpDir, s + ".png");
                    if (!File.Exists(src)) { Debug.LogError($"[IntroWinLose] Thiếu sprite dump: {src}"); continue; }
                    File.Copy(src, dest);
                }
            }
        }

        private static void ConfigureSpriteImports()
        {
            var dirs = Sprites.Select(b => $"{CharArt}/{b.dir}").Where(AssetDatabase.IsValidFolder).ToArray();
            if (dirs.Length == 0) return;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", dirs))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static Sprite LoadSprite(string dir, string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{CharArt}/{dir}/{name}.png");

        // ---------------------------------------------------------------- clip
        private static AnimationClip BuildClip(string action, bool loop, (int tick, string sprite)[] frames,
                                               int endTick, int recoveryEndTick)
        {
            string path = $"{AnimDir}/{action}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }
            clip.ClearCurves();
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            clip.frameRate = Fps;

            var keys = new List<ObjectReferenceKeyframe>();
            foreach ((int tick, string spriteName) in frames)
            {
                Sprite s = LoadSprite(action, spriteName);
                if (s == null) { Debug.LogError($"[IntroWinLose] {action}: thiếu sprite {spriteName}"); continue; }
                keys.Add(new ObjectReferenceKeyframe { time = tick / Fps, value = s });
            }
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys.ToArray());

            // RecoveryEnd ở FRAME CUỐI (Intro) — báo orchestrator "diễn xong" (tái dùng relay, KHÔNG đếm frame).
            if (recoveryEndTick >= 0)
            {
                AnimationUtility.SetAnimationEvents(clip, new[]
                {
                    new AnimationEvent { time = recoveryEndTick / Fps, functionName = "RecoveryEnd" }
                });
            }

            AnimationClipSettings cs = AnimationUtility.GetAnimationClipSettings(clip);
            cs.loopTime = loop;
            cs.stopTime = endTick / Fps;   // giữ frame cuối tới hết độ dài này (pose Win/Lose/Intro).
            AnimationUtility.SetAnimationClipSettings(clip, cs);

            EditorUtility.SetDirty(clip);
            return clip;
        }

        // ---------------------------------------------------------------- animator state
        private static void AddPoseState(string name, AnimationClip clip, int stateId)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) { Debug.LogError($"[IntroWinLose] Không thấy controller {ControllerPath}"); return; }

            var sm = controller.layers[0].stateMachine;
            var state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (state == null) state = sm.AddState(name);
            state.motion = clip;
            state.writeDefaultValues = true;

            var tr = sm.anyStateTransitions.FirstOrDefault(t => t.destinationState == state);
            if (tr == null) tr = sm.AddAnyStateTransition(state);
            tr.hasExitTime = false;
            tr.hasFixedDuration = true;
            tr.duration = 0f;
            tr.canTransitionToSelf = false;   // gotcha: bật thì mỗi frame StateId==N re-enter → kẹt frame 0.
            tr.conditions = new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.Equals,
                    parameter = "StateId",
                    threshold = stateId
                }
            };
            EditorUtility.SetDirty(controller);
        }

        // ---------------------------------------------------------------- channel
        private static VoidEventChannelSO LoadOrCreateVoidChannel(string path)
        {
            var ch = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(path);
            if (ch == null)
            {
                ch = ScriptableObject.CreateInstance<VoidEventChannelSO>();
                AssetDatabase.CreateAsset(ch, path);
            }
            return ch;
        }

        // ---------------------------------------------------------------- scene map
        private static void WireMapScene(string scenePath)
        {
            if (!File.Exists(scenePath)) { Debug.LogError($"[IntroWinLose] Thiếu scene {scenePath}"); return; }
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var introStart = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(IntroStartPath);
            var introFinished = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(IntroFinishedPath);
            if (introStart == null || introFinished == null)
                Debug.LogError("[IntroWinLose] Load kênh intro NULL — kiểm tra CreateAsset.");

            CinemachineCamera camP1 = EnsureIntroCam(scene, "CM_IntroP1");
            CinemachineCamera camP2 = EnsureIntroCam(scene, "CM_IntroP2");

            var director = FindInScene(scene, "RoundDirector") ?? NewIn(scene, "RoundDirector");
            var intro = Ensure<RoundIntroController>(director);
            var outro = Ensure<MatchOutroController>(director);

            var soIntro = new SerializedObject(intro);
            SetRef(soIntro, "onRoundIntroStart", introStart);
            SetRef(soIntro, "onRoundIntroFinished", introFinished);
            SetRef(soIntro, "introCamP1", camP1);
            SetRef(soIntro, "introCamP2", camP2);
            // (playtest #2, điểm 1) Giá trị SERIALIZE trong scene không tự theo default C# mới — ghi đè
            // tường minh (component đã tồn tại từ lần chạy trước với perPersonTimeout=3 cũ).
            soIntro.FindProperty("perPersonTimeout").floatValue = PerPersonTimeout;
            soIntro.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(intro);

            var matchResult = AssetDatabase.LoadAssetAtPath<MatchResultEventChannelSO>(MatchResultPath);
            if (matchResult == null) Debug.LogError($"[IntroWinLose] Thiếu {MatchResultPath}");
            var soOutro = new SerializedObject(outro);
            SetRef(soOutro, "onMatchResult", matchResult);
            // (NHÓM 3) TÁI DÙNG 2 vcam intro cho outro zoom-winner — không xung đột thời gian (outro
            // chạy SAU khi RoundIntroController đã tắt priority 2 vcam này về idlePriority).
            SetRef(soOutro, "introCamP1", camP1);
            SetRef(soOutro, "introCamP2", camP2);
            soOutro.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(outro);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[IntroWinLose] Wired map scene {Path.GetFileName(scenePath)} (2 vcam intro + RoundDirector).");
        }

        private static CinemachineCamera EnsureIntroCam(Scene scene, string name)
        {
            var go = FindInScene(scene, name) ?? NewIn(scene, name);
            go.transform.position = new Vector3(0f, 1f, -10f);
            var cm = Ensure<CinemachineCamera>(go);
            cm.Priority = 0;   // idle < CM_MatchCamera(10); RoundIntroController nâng lên khi diễn.
            var lens = cm.Lens;
            lens.OrthographicSize = IntroOrtho;   // zoom cận. CẦN TEST.
            cm.Lens = lens;
            var composer = Ensure<CinemachinePositionComposer>(go);
            composer.CameraDistance = 10f;
            EditorUtility.SetDirty(cm);
            EditorUtility.SetDirty(composer);
            return cm;
        }

        // ---------------------------------------------------------------- MatchManager (Lobby)
        private static void WireLobbyMatchManager()
        {
            if (!File.Exists(LobbyScenePath)) { Debug.LogError($"[IntroWinLose] Thiếu {LobbyScenePath}"); return; }
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            var introStart = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(IntroStartPath);
            var introFinished = AssetDatabase.LoadAssetAtPath<VoidEventChannelSO>(IntroFinishedPath);
            if (introStart == null || introFinished == null)
                Debug.LogError("[IntroWinLose] Load kênh intro NULL (Lobby) — kiểm tra CreateAsset.");

            MatchManager mm = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                mm = root.GetComponentInChildren<MatchManager>(true);
                if (mm != null) break;
            }
            if (mm == null) { Debug.LogError("[IntroWinLose] Không thấy MatchManager trong Lobby."); return; }

            var so = new SerializedObject(mm);
            SetRef(so, "onRoundIntroStart", introStart);
            SetRef(so, "onRoundIntroFinished", introFinished);
            // (playtest #2, điểm 1) Ghi đè tường minh — MatchManager đã tồn tại từ lần chạy trước với
            // introTimeout=10 cũ (serialize không tự theo default C# mới).
            so.FindProperty("introTimeout").floatValue = IntroTimeoutSafety;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mm);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[IntroWinLose] Wired MatchManager (Lobby): onRoundIntroStart/Finished.");
        }

        // ---------------------------------------------------------------- helpers
        private static void SetRef(SerializedObject so, string prop, UnityEngine.Object value)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[IntroWinLose] Thiếu field '{prop}' trên {so.targetObject.GetType().Name}"); return; }
            p.objectReferenceValue = value;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        private static GameObject NewIn(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }
    }
}
