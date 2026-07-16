using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using FA.Core;
using FA.Items;

namespace FA.EditorTools
{
    /// <summary>
    /// (Item Chặng 2) VÁ prefab Mystery_Block_66 + DỰNG Item.prefab CHUNG cho bình thuốc. Idempotent.
    ///
    /// 1) Mystery_Block_66.prefab: gỡ mọi MonoBehaviour missing-script (MysteryBlockItem/Temporary…Pickup
    ///    đã xoá) ở root + child PickupTrigger; gắn <see cref="Item"/> lên root, wire spriteRenderer/
    ///    visualAnimator (child Visual), minGroundNormalY=0.5, maxLandingSpeedY=0.5 (nâng — research), statusTint,
    ///    trapKnockbackForce=(5,8). Giữ Rigidbody2D + BoxCollider2D thân, Layer Item, controller flip của Visual.
    /// 2) Item.prefab (Prefabs/Items): KHUÔN Mystery — root(Rigidbody2D Dynamic + BoxCollider2D thân +
    ///    <see cref="Item"/>) + child Visual(SpriteRenderer + Animator Item_Land.controller, disabled) +
    ///    child PickupTrigger(BoxCollider2D trigger). Layer Item.
    /// 3) (fa-fix ISSUE 2) BoxCollider2D THÂN (non-trigger) của cả 2 prefab: excludeLayers = Player1|Player2
    ///    (Unity-native Collider2D.excludeLayers — KHÔNG đụng Collision Matrix toàn cục vì trigger nhặt cũng ở
    ///    layer Item). Thân vẫn va Ground (Land), PickupTrigger giữ excludeLayers=0 (vẫn overlap player để nhặt).
    /// CLI: -executeMethod FA.EditorTools.ItemPrefabSetupTool.Run
    /// </summary>
    public static class ItemPrefabSetupTool
    {
        // (fa-fix ISSUE 2) Thân item KHÔNG va chạm vật lý với player — chỉ trigger nhặt mới overlap.
        private static readonly LayerMask BodyExcludeLayers = GameLayers.Player1Mask | GameLayers.Player2Mask;

        private const string MysteryPrefabPath = "Assets/_Project/Prefabs/Mystery_Block_66.prefab";
        private const string ItemsDir = "Assets/_Project/Prefabs/Items";
        private const string ItemPrefabPath = ItemsDir + "/Item.prefab";
        private const string LandControllerPath = "Assets/_Project/Animations/ItemAnime/Item_Land.controller";
        private const string BuffSheet = "Assets/_Project/Art/Items/Buff_Debuff Items.png";
        private const string PlaceholderSprite = "Mobile - Idle Skilling - Apothecary - Potions_0";

        private static readonly Color StatusTint = new Color(1f, 0.35f, 0.25f, 1f);
        private static readonly Vector2 TrapForce = new Vector2(5f, 8f);

        [MenuItem("FA/Tools/Items/2. Setup Item Prefabs (fix mystery + Item.prefab)")]
        public static void Run()
        {
            FixMysteryPrefab();
            CreateItemPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ItemPrefabSetup] DONE — mystery fixed + Item.prefab created + body excludeLayers Player1|Player2.");
        }

        private static Sprite LoadSlice(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(BuffSheet)
                .OfType<Sprite>().FirstOrDefault(s => s.name == name);
        }

        private static void FixMysteryPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MysteryPrefabPath);
            try
            {
                // Gỡ missing-script trên toàn hierarchy (root + Visual + PickupTrigger).
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

                var item = root.GetComponent<Item>() ?? root.AddComponent<Item>();

                Transform visual = root.transform.Find("Visual");
                SpriteRenderer sr = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
                Animator anim = visual != null ? visual.GetComponent<Animator>() : null;

                var so = new SerializedObject(item);
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.FindProperty("visualAnimator").objectReferenceValue = anim;
                so.FindProperty("minGroundNormalY").floatValue = 0.5f;
                so.FindProperty("maxLandingSpeedY").floatValue = 0.5f;
                so.FindProperty("downThreshold").floatValue = -0.5f;
                so.FindProperty("statusTint").colorValue = StatusTint;
                so.FindProperty("trapKnockbackForce").vector2Value = TrapForce;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (anim != null) anim.enabled = false;
                root.layer = GameLayers.Item;

                // (fa-fix ISSUE 2) thân (non-trigger) bỏ qua va chạm player, giữ va Ground.
                BoxCollider2D body = root.GetComponent<BoxCollider2D>();
                if (body != null) body.excludeLayers = BodyExcludeLayers;

                PrefabUtility.SaveAsPrefabAsset(root, MysteryPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void CreateItemPrefab()
        {
            if (!AssetDatabase.IsValidFolder(ItemsDir))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Items");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(LandControllerPath);
            var root = new GameObject("Item");
            try
            {
                root.layer = GameLayers.Item;

                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 1f;
                rb.freezeRotation = true;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;

                var body = root.AddComponent<BoxCollider2D>();
                body.isTrigger = false;
                body.size = new Vector2(0.5f, 0.6f);   // thân đặc rơi-đáp. CẦN TEST.
                body.excludeLayers = BodyExcludeLayers; // (fa-fix ISSUE 2) bỏ qua va chạm player, giữ va Ground.

                // child Visual
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite = LoadSlice(PlaceholderSprite);   // placeholder — Item.ApplyVisual đặt icon theo ItemData.
                sr.sortingOrder = 3;
                var anim = visual.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.enabled = false;                       // bật khi Land().

                // child PickupTrigger
                var trigger = new GameObject("PickupTrigger");
                trigger.transform.SetParent(root.transform, false);
                trigger.layer = GameLayers.Item;
                var tBox = trigger.AddComponent<BoxCollider2D>();
                tBox.isTrigger = true;
                tBox.size = new Vector2(1.4f, 1.4f);        // vùng nhặt rộng hơn thân. CẦN TEST.

                var item = root.AddComponent<Item>();
                var so = new SerializedObject(item);
                so.FindProperty("spriteRenderer").objectReferenceValue = sr;
                so.FindProperty("visualAnimator").objectReferenceValue = anim;
                so.FindProperty("minGroundNormalY").floatValue = 0.5f;
                so.FindProperty("maxLandingSpeedY").floatValue = 0.5f;
                so.FindProperty("downThreshold").floatValue = -0.5f;
                so.FindProperty("statusTint").colorValue = StatusTint;
                so.FindProperty("trapKnockbackForce").vector2Value = TrapForce;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
