using UnityEditor;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// (Item Chặng 2) ORCHESTRATOR — chạy 4 tool item theo đúng thứ tự phụ thuộc (anim → prefab → data →
    /// scene) trong 1 lần CLI. Mỗi tool con vẫn idempotent + chạy riêng được.
    /// CLI: -executeMethod FA.EditorTools.ItemStage2SetupTool.Run
    /// </summary>
    public static class ItemStage2SetupTool
    {
        [MenuItem("FA/Tools/Items/0. Setup ALL (anim + prefab + data + scene)")]
        public static void Run()
        {
            ItemAnimSetupTool.Run();
            ItemPrefabSetupTool.Run();
            ItemDataSetupTool.Run();
            ItemSceneSetupTool.Run();
            AssetDatabase.SaveAssets();
            Debug.Log("[ItemStage2Setup] ALL DONE.");
        }
    }
}
