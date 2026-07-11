using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// DỰNG RIG CAMERA TRẬN (Cinemachine 3, Unity-native §1.1) vào scene map. Scene map KHÔNG có
    /// camera render riêng (chốt user 2026-07-11, phương án A): camera duy nhất + Brain + AudioListener
    /// nằm ở scene Lobby (<see cref="UIKit.EnsureCamera"/>); scene map chỉ chứa "đạo diễn":
    ///   • "CM_TargetGroup": <see cref="CinemachineTargetGroup"/> + <see cref="MatchCameraTargets"/>
    ///     (runtime tự nạp 2 fighter theo layer — player spawn runtime, không gán sẵn được).
    ///   • "CM_MatchCamera": <see cref="CinemachineCamera"/> priority 10 (&gt; vcam menu 0 — map load
    ///     là Brain tự blend sang) Follow=group + PositionComposer + <see cref="CinemachineGroupFraming"/>
    ///     — 2 người gần = zoom in, xa = zoom out, GIỚI HẠN qua <c>OrthoSizeRange</c> (CẦN TEST).
    /// Camera cũ trong scene map (MapCamera) bị XÓA. Idempotent. EDITOR-ONLY.
    /// Lưu ý: mở scene map ĐỘC LẬP sẽ không có camera render — test map qua flow Lobby.
    /// </summary>
    internal static class StageCameraRigBuilder
    {
        private const string GroupName = "CM_TargetGroup";
        private const string CamName = "CM_MatchCamera";
        private const string MapScenePath = "Assets/_Project/Scenes/Test_Phase12_Map.unity";

        // Giới hạn zoom (ortho half-height): min = zoom-in sát nhất, max = zoom-out xa nhất.
        // Đồng bộ RIG CAMERA CỦA Test_Phase05 (user chốt "giống phase 5"): OrthoSizeRange (3,10),
        // FramingSize 0.8, CenterOffset (0,-0.5), Damping 2, lens 5.
        private const float OrthoMin = 3f;
        private const float OrthoMax = 10f;

        [MenuItem("FA/Stage/Build match camera rig (current scene)")]
        public static void BuildIntoCurrentScene()
        {
            BuildRig(SceneManager.GetActiveScene());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[StageCameraRig] Đã dựng rig camera trận vào scene đang mở — nhớ Save Scene.");
        }

        /// <summary>Chạy được headless (CLI -executeMethod): mở scene map harness, dựng rig, save.</summary>
        public static void BuildIntoHarnessMapScene()
        {
            var scene = EditorSceneManager.OpenScene(MapScenePath, OpenSceneMode.Single);
            BuildRig(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StageCameraRig] Đã dựng rig camera trận vào {MapScenePath}.");
        }

        private static void BuildRig(Scene scene)
        {
            // 1. XÓA camera render riêng của map (nếu còn) — camera duy nhất ở Lobby (phương án A).
            //    2 camera cùng tag MainCamera làm Camera.main hên xui (flash zoom P17 dính) và rig
            //    Cinemachine drive camera này trong khi camera kia render → "zoom không có".
            Camera cam = FindSceneCamera(scene);
            if (cam != null) Object.DestroyImmediate(cam.gameObject);

            // 2. TargetGroup + finder runtime.
            var groupGo = FindInScene(scene, GroupName) ?? NewIn(scene, GroupName);
            var group = Ensure<CinemachineTargetGroup>(groupGo);
            var finder = Ensure<MatchCameraTargets>(groupGo);
            var soF = new SerializedObject(finder);
            var pG = soF.FindProperty("targetGroup");
            if (pG != null) pG.objectReferenceValue = group;
            soF.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(finder);

            // 3. CinemachineCamera Follow group + composer + group framing (zoom có giới hạn).
            var cmGo = FindInScene(scene, CamName) ?? NewIn(scene, CamName);
            cmGo.transform.position = new Vector3(0f, 1f, -10f);
            var cm = Ensure<CinemachineCamera>(cmGo);
            cm.Priority = 10; // > vcam menu (0) ở Lobby → map load additive là Brain blend sang ngay.
            cm.Follow = group.transform;
            var lens = cm.Lens;
            lens.OrthographicSize = 5f; // scale phase-5
            cm.Lens = lens;

            var composer = Ensure<CinemachinePositionComposer>(cmGo);
            composer.CameraDistance = 10f; // giữ camera ở z = -10 (2D).

            var framing = Ensure<CinemachineGroupFraming>(cmGo);
            framing.FramingMode = CinemachineGroupFraming.FramingModes.HorizontalAndVertical;
            framing.FramingSize = 0.8f;
            framing.CenterOffset = new Vector2(0f, -0.5f); // nhân vật hơi thấp khung — như phase 5.
            framing.Damping = 2f;
            framing.OrthoSizeRange = new Vector2(OrthoMin, OrthoMax);

            EditorUtility.SetDirty(cm);
            EditorUtility.SetDirty(composer);
            EditorUtility.SetDirty(framing);
        }

        // ───────────────────── helpers ─────────────────────

        private static Camera FindSceneCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<Camera>(true);
                if (c != null) return c;
            }
            return null;
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
