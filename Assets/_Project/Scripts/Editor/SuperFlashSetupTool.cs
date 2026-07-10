using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FA.Core;
using FA.Effects;

namespace FA.EditorTools
{
    /// <summary>
    /// TOOL DỰNG SUPER FLASH CINEMATIC cho Super 1 (idempotent) — theo showcase MUGEN (helper 9935):
    /// vài frame đầu super → freeze + zoom caster + tối màn (che cả HUD) + cut-in chân dung 9000-10
    /// trượt vào từ phía caster đứng.
    ///  1. Import sprite Portrait/ (9000-10/12/20 — 12/20 để dành silhouette + Super 2).
    ///  2. Prefab SuperFlashOverlay (Canvas Screen-Space-Overlay sorting 500 + Dim + Portrait).
    ///  3. THÊM event SuperFlashStart(t0)/SuperFlashEnd(t44 — hết pose, trước loạt bắn) vào
    ///     Super_1.anim — GIỮ nguyên event cũ.
    ///  4. Gắn SuperFlashController lên prefab Emiya (overlay + portrait + side Auto).
    /// CLI: Unity.exe -batchmode -quit -executeMethod FA.EditorTools.SuperFlashSetupTool.Run
    /// </summary>
    public static class SuperFlashSetupTool
    {
        private const string PortraitDir = "Assets/_Project/Art/Characters/Emiya Archer/Portrait";
        private const string ClipPath = "Assets/_Project/Animations/SampleFighter/Super_1.anim";
        private const string PrefabPath = "Assets/_Project/Prefabs/Characters/Emiya Archer.prefab";
        private const string OverlayPrefabPath = "Assets/_Project/Prefabs/VFX/SuperFlashOverlay.prefab";

        private const int FlashEndTick = 44;   // hết pose (loạt bắn bắt đầu tick 45) — chạy real-time.

        [MenuItem("FA/Tools/Setup Super Flash (zoom + dim + cut-in)")]
        public static void Run()
        {
            ConfigurePortraitImports();
            GameObject overlay = CreateOverlayPrefab();
            AddFlashEventsToClip();
            WirePrefab(overlay);
            AssetDatabase.SaveAssets();
            Debug.Log("[SuperFlashSetup] DONE — freeze + zoom + dim + cut-in 9000-10 cho Super 1.");
        }

        private static void ConfigurePortraitImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { PortraitDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                if (imp == null) continue;
                var settings = new TextureImporterSettings();
                imp.ReadTextureSettings(settings);
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spritePixelsPerUnit = 100f;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.mipmapEnabled = false;
                imp.SetTextureSettings(settings);
                imp.maxTextureSize = 2048;   // artwork 1312×1000 — giữ nét.
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();
        }

        private static GameObject CreateOverlayPrefab()
        {
            var root = new GameObject("SuperFlashOverlay");
            try
            {
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 500;   // che cả HUD.
                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                // KHÔNG còn lớp Dim trên canvas: tối màn = quad đen TRONG WORLD (SuperFlashController)
                // để caster nổi lên trên — canvas overlay luôn vẽ đè mọi world content nên dim ở đây
                // sẽ đen luôn cả nhân vật (mất ý nghĩa zoom).
                var pGo = new GameObject("Portrait", typeof(RectTransform));
                pGo.transform.SetParent(root.transform, false);
                var pRt = (RectTransform)pGo.transform;
                // 620px (~1/3 màn 1920) thay 820: camera cinematic zoom sâu làm nhân vật to giữa màn,
                // portrait to sẽ ĐÈ lên người khi caster gần góc (user bắt). Tỉ lệ gốc 1.312.
                pRt.sizeDelta = new Vector2(620f, 473f);
                var portrait = pGo.AddComponent<Image>();
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                portrait.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-10.png");

                var overlay = root.AddComponent<SuperFlashOverlay>();
                var so = new SerializedObject(overlay);
                so.FindProperty("dim").objectReferenceValue = null;   // tối màn = world quad, không phải canvas.
                so.FindProperty("portrait").objectReferenceValue = portrait;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, OverlayPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AddFlashEventsToClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null) { Debug.LogError("[SuperFlashSetup] Không thấy Super_1.anim (chạy Super1SetupTool trước)."); return; }

            List<AnimationEvent> events = AnimationUtility.GetAnimationEvents(clip).ToList();
            if (events.Any(e => e.functionName == "SuperFlashStart")) return;   // idempotent.

            const float fps = 60f;
            events.Add(new AnimationEvent { time = 0f, functionName = "SuperFlashStart" });
            events.Add(new AnimationEvent { time = FlashEndTick / fps, functionName = "SuperFlashEnd" });
            AnimationUtility.SetAnimationEvents(clip, events.OrderBy(e => e.time).ToArray());
            EditorUtility.SetDirty(clip);
        }

        private static void WirePrefab(GameObject overlayPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ctrl = root.GetComponent<SuperFlashController>();
                if (ctrl == null) ctrl = root.AddComponent<SuperFlashController>();
                var so = new SerializedObject(ctrl);
                so.FindProperty("relay").objectReferenceValue = root.GetComponentInChildren<AnimationEventRelay>();
                so.FindProperty("overlayPrefab").objectReferenceValue =
                    overlayPrefab.GetComponent<SuperFlashOverlay>();
                SerializedProperty pPortraits = so.FindProperty("portraits");
                if (pPortraits.arraySize < 1) pPortraits.arraySize = 1;
                pPortraits.GetArrayElementAtIndex(0).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>($"{PortraitDir}/9000-10.png");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
