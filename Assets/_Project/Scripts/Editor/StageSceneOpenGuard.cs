using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace FA.EditorTools
{
    /// <summary>
    /// VỆ SĨ chống bug "mở scene lại là rơi xuyên đất": scene lưu bởi phiên bản tool cũ có thể chứa
    /// CompositeCollider2D RỖNG (geometry sinh trễ 1 frame so với lúc save). Guard này chạy tự động
    /// mỗi khi MỞ SCENE hoặc VÀO PLAY: nếu thấy composite không có path nào thì ép sinh lại ngay.
    /// Vô hại với scene lành (chỉ sinh khi rỗng), không đụng gameplay.
    /// </summary>
    [InitializeOnLoad]
    internal static class StageSceneOpenGuard
    {
        static StageSceneOpenGuard()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => RegenerateIfEmpty();
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    RegenerateIfEmpty();
            };
        }

        private static void RegenerateIfEmpty()
        {
            var composites = Object.FindObjectsByType<CompositeCollider2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cc in composites)
            {
                if (cc == null || cc.pathCount > 0) continue; // lành → bỏ qua
                var tc = cc.GetComponent<TilemapCollider2D>();
                if (tc != null) tc.ProcessTilemapChanges();
                cc.GenerateGeometry();
                Debug.Log($"[StageSceneOpenGuard] Đã sinh lại collider rỗng cho '{cc.gameObject.name}' " +
                          "(scene được lưu bởi tool bản cũ).", cc);
            }
        }
    }
}
