using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Dựng scene MAP_FIRE "Lò Dung Nham" (design 03): đảo bazan chính + 3 platform bay, biển dung nham
    /// dâng (RisingLava + DeathZone: CHẠM LÀ CHẾT) có icon cảnh báo nhấp nháy,
    /// FireBiome (đứng yên tăng %), núi lửa
    /// phun nền sau, tàn lửa bay, ánh sáng cam hắt từ lava, death zone 4 phía, camera Confiner.
    /// </summary>
    internal static class StageMapBuilderFire
    {
        public const string SceneName = "Map_Fire";

        /// <summary>Tên root object thuộc rig camera/intro — KHÔNG đụng khi restyle scene có sẵn
        /// (dựng bởi StageCameraRigBuilder/IntroWinLoseSetupTool, chạy sau map builder này).</summary>
        private static readonly HashSet<string> RigRootNames = new HashSet<string>
        {
            "CM_MatchCamera", "CM_TargetGroup", "CM_IntroP1", "CM_IntroP2", "RoundDirector",
            "ItemSpawner",
        };

        public static void Build()
        {
            var scene = StageSceneKit.NewScene();
            PopulateScene(includeCameraRig: true);
            StageSceneKit.SaveScene(scene, SceneName);
            StageSceneKit.AddStageToRegistry("Lò Dung Nham (Lửa)", SceneName,
                StageSceneKit.LoadSprite(StageArtFire.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Fire.");
        }

        /// <summary>Dựng lại NỘI DUNG map trên scene ĐÃ CÓ SẴN, giữ nguyên rig camera/intro
        /// (<see cref="RigRootNames"/>) — xem StageMapBuilderIce.RestyleExistingScene. Idempotent.</summary>
        [MenuItem("FA/Stage/Restyle map content only (keep camera+intro rig)/Map_Fire")]
        public static void RestyleExistingScene()
        {
            var path = "Assets/_Project/Scenes/" + SceneName + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (!RigRootNames.Contains(root.name)) Object.DestroyImmediate(root);

            PopulateScene(includeCameraRig: false);

            StageSceneKit.SaveScene(scene, SceneName);
            Debug.Log("[StageMapBuilderFire] Đã restyle nội dung Map_Fire — giữ nguyên rig camera/intro.");
        }

        private static void PopulateScene(bool includeCameraRig)
        {
            // ---- Nền parallax ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtFire.Sky, new Vector3(0f, 4.5f, 0f), -100, 0.92f, null, 1.5f);
            // BIỂN LAVA XA + măng đá silhouette (redesign Norfair: hang động, lava là nguồn sáng duy nhất).
            StageSceneKit.MakeBackdrop("BG_LavaField", StageArtFire.LavaField, new Vector3(0f, 0.4f, 0f), -92, 0.8f, null, 1.35f);
            // TRẦN HANG lớp riêng — măng đá lộ ngay mép trên khung hình mặc định, leo cao thấy trọn.
            StageSceneKit.MakeBackdrop("BG_Ceiling", StageArtFire.Ceiling, new Vector3(0f, 8.2f, 0f), -91, 0.85f, null, 1.4f);
            // (Bỏ 3 chóp núi lửa cũ — bối cảnh giờ là TRONG hang, không có núi hình nón.)

            // ---- Đảo bazan chính: 3 variant tile chống lặp + DẢI DUNG NHAM NÓNG giữa đảo (feedback test) ----
            var topA = StageSceneKit.MakeTileAsset(StageArtFire.TileTop, StageArtFire.Dir + "/Tile_FireTop.asset");
            var topB = StageSceneKit.MakeTileAsset(StageArtFire.TileTopB, StageArtFire.Dir + "/Tile_FireTopB.asset");
            var topC = StageSceneKit.MakeTileAsset(StageArtFire.TileTopC, StageArtFire.Dir + "/Tile_FireTopC.asset");
            var hotT = StageSceneKit.MakeTileAsset(StageArtFire.TileHot, StageArtFire.Dir + "/Tile_FireHot.asset");
            var inA = StageSceneKit.MakeTileAsset(StageArtFire.TileInner, StageArtFire.Dir + "/Tile_FireInner.asset");
            var inB = StageSceneKit.MakeTileAsset(StageArtFire.TileInnerB, StageArtFire.Dir + "/Tile_FireInnerB.asset");
            Tile[] tops = { topA, topB, topC };
            Tile[] inners = { inA, inB };
            var tm = StageSceneKit.MakeGroundTilemap(null, null, -6);
            // (Size up theo yêu cầu leader: đảo 20 → 24 tile — hợp cỡ Archer hiện tại.)
            StageSceneKit.FillGroundVaried(tm, tops, inners, -12, 11, -1, 3);
            for (int x = -2; x <= 1; x++)
                tm.SetTile(new Vector3Int(x, -1, 0), hotT);                    // dải đất dung nham nóng rực

            // MÉP BẬC DƯỚI 2 BÊN (feedback: "mép bên dưới cũng phải đứng được"): vai bazan nhô ra của
            // FloorFace giờ có COLLISION THẬT — bậc thấp hơn mặt đảo 1 unit, đứng/bám được như mép đài Smash.
            foreach (int sx in new[] { -13, 12 })
            {
                tm.SetTile(new Vector3Int(sx, -2, 0), StageSceneKit.PickVariant(tops, sx, -2));
                tm.SetTile(new Vector3Int(sx, -3, 0), StageSceneKit.PickVariant(inners, sx, -3));
            }

            // (Đã bỏ gò chặn mép theo feedback — leo cao bằng TẦNG PLATFORM BAY.)

            // MẶT ĐẢO HỮU CƠ kiểu Norfair: vách bazan cột lởm chởm + nứt magma + măng đá rủ xuống lava.
            // Chỉ visual — collision vẫn là tilemap. Chừa hở hàng tile mặt (variant + dải nóng).
            StageSceneKit.MakeSpriteGO("Deco_FloorFace", StageSceneKit.LoadSprite(StageArtFire.FloorFace),
                new Vector3(0f, -2.9f, 0f), -4, null, new Vector3(1.17f, 1f, 1f)); // giãn ngang khớp đảo 24+bậc

            // (Đã XÓA 2 tháp đá silhouette Deco_RockSpire theo feedback — đứng trùng chỗ cột bám,
            // nhìn thành khối đen khó hiểu mà không có collision. Măng đá nền xa ở BG_LavaField đủ đẹp.)

            // ---- 3 platform bay 1 chiều (trái/phải thấp + giữa cao) ----
            // (Size up: giãn X — ĐỘ CAO GIỮ NGUYÊN để tầm nhảy Archer không đổi.)
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtFire.Platform, new Vector2(-13f, 1.8f), null, -5, 1f, 0.33f); // mép trong platform chớm đè mép đảo → nhảy sang dễ
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtFire.Platform, new Vector2(13f, 1.8f), null, -5, 1f, 0.33f);
            StageSceneKit.MakeOneWayPlatform("Platform_Top", StageArtFire.Platform, new Vector2(0f, 3.6f), null, -5, 1f, 0.33f);
            // TẦNG CAO (feedback): leo lên camera nâng theo → thấy miệng núi lửa khổng lồ phun trào.
            StageSceneKit.MakeOneWayPlatform("Platform_High_L", StageArtFire.Platform, new Vector2(-6.5f, 5.8f), null, -5, 1f, 0.33f);
            StageSceneKit.MakeOneWayPlatform("Platform_High_R", StageArtFire.Platform, new Vector2(6.5f, 5.8f), null, -5, 1f, 0.33f);

            // ---- Spawn + Death zones ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-6f, 1.2f), new Vector2(6f, 1.2f), new Vector2(0f, 5.2f));
            StageSceneKit.MakeDeathZones(null, 27f, 22f, -18f, respawn);

            // ---- Dung nham dâng (RisingLava + DeathZone cùng object — CHẠM LÀ CHẾT, feedback test) ----
            var fireCfg = AssetDatabase.LoadAssetAtPath<StageConfigSO>(StageSceneKit.DataFolder + "/StageConfig_Fire.asset");
            var lavaGo = new GameObject("RisingLava");
            lavaGo.transform.position = new Vector3(0f, -9.5f, 0f);
            lavaGo.layer = GameLayers.DeathZone;
            var lavaSr = lavaGo.AddComponent<SpriteRenderer>();
            lavaSr.sprite = StageSceneKit.LoadSprite(StageArtFire.Lava);
            lavaSr.sortingOrder = 30; // trước platform/nhân vật → nuốt chửng khi dâng.
            // Scale (1,1): sprite vẽ ĐÚNG kích thước thật 51×10.5 unit — hết kéo giãn pixel méo (fix xấu).
            var lavaCol = lavaGo.AddComponent<BoxCollider2D>();
            lavaCol.isTrigger = true;
            // Mép trên collider KHỚP MẶT SÓNG THẤY ĐƯỢC (đáy sóng ~+3.95 local) — không "chết trên không"
            // phía trên lava; sprite cao 10.5 nhưng ~1 unit trên cùng là quầng nhiệt trong suốt.
            lavaCol.size = new Vector2(51f, 9.2f);
            lavaCol.offset = new Vector2(0f, -0.65f);

            var warnGo = new GameObject("WarningIcon");
            warnGo.transform.SetParent(lavaGo.transform, false);
            warnGo.transform.localPosition = new Vector3(0f, 7.4f, 0f);
            var warnSr = warnGo.AddComponent<SpriteRenderer>();
            warnSr.sprite = StageSceneKit.LoadSprite(StageArtFire.WarnIcon);
            warnSr.sortingOrder = 60;
            warnGo.AddComponent<CameraEdgeAnchor>(); // feedback test: cảnh báo đi theo màn hình, không kẹt đáy map.

            // (Đã BỎ lớp váng sáng LavaShine rời — art lava mới tự có viền crest trắng-nóng;
            // lớp rời đặt lệch trông như 1 vạch trôi lơ lửng — feedback "dung nham bị lệch".)

            var lava = lavaGo.AddComponent<RisingLava>(); // RequireComponent tự thêm Rigidbody2D.
            StageSceneKit.SetObj(lava, "config", fireCfg);
            StageSceneKit.SetObj(lava, "warningIcon", warnSr);
            StageSceneKit.SetBool(lava, "autoStart", true);
            // maxRiseY là tọa độ TÂM khối lava. Mặt sóng THẤY ĐƯỢC cao ~+4.4 so với tâm →
            // -2.9 → mặt lava dừng ở ~+1.5: NUỐT TRỌN mặt đảo (y=0), liếm SÁT gầm platform thấp
            // (mặt đứng ~2.1) → ép leo platform ("lava phải dâng tới gò đất gần nhất"). CẦN TEST.
            StageSceneKit.SetFloat(lava, "maxRiseY", -2.9f);

            // CHẠM LAVA = CHẾT LUÔN (feedback test) — DeathZone di động cùng object.
            var channel = AssetDatabase.LoadAssetAtPath<ScriptableObject>(StageSceneKit.PlayerDiedChannelPath);
            var lavaDz = lavaGo.AddComponent<DeathZone>();
            StageSceneKit.SetObj(lavaDz, "onPlayerDied", channel);
            StageSceneKit.SetMask(lavaDz, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetBool(lavaDz, "debugSelfRespawn", false);
            StageSceneKit.SetObj(lavaDz, "debugSpawnPoint", respawn);

            // ---- FireBiome: đứng yên trên đảo chính → tăng %/giây ----
            var biomeGo = new GameObject("FireBiome");
            biomeGo.transform.position = new Vector3(0f, 1f, 0f);
            var fireArea = biomeGo.AddComponent<BoxCollider2D>();
            fireArea.isTrigger = true;
            fireArea.size = new Vector2(25f, 3.4f);
            var biome = biomeGo.AddComponent<FireBiome>();
            StageSceneKit.SetObj(biome, "config", fireCfg);
            StageSceneKit.SetObj(biome, "fireArea", fireArea);
            StageSceneKit.SetMask(biome, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(biome, "groundLayers", GameLayers.GroundMask | GameLayers.OneWayPlatformMask);

            // ---- Dải dung nham nóng: đứng lên là mất % (kể cả đang di chuyển) ----
            var hotGo = new GameObject("LavaFloorZone");
            hotGo.transform.position = new Vector3(0f, 0.35f, 0f);
            var hotCol = hotGo.AddComponent<BoxCollider2D>();
            hotCol.isTrigger = true;
            hotCol.size = new Vector2(4f, 0.8f);
            var hotZone = hotGo.AddComponent<LavaFloorZone>();
            StageSceneKit.SetObj(hotZone, "config", fireCfg);
            StageSceneKit.SetObj(hotZone, "hotArea", hotCol);
            StageSceneKit.SetMask(hotZone, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(hotZone, "groundLayers", GameLayers.GroundMask);

            // ---- TƯỜNG BÁM lơ lửng 2 bên (feedback: chạm tường trên không là bám được) ----
            // Treo CAO (chân cột cách mặt đảo ~4.2 — gấp đôi bản đầu, feedback): với tới từ tầng cao
            // (±5.5, 5.8); khi lava dâng đỉnh vẫn là chỗ bám né an toàn trên không.
            // (Feedback: dời tường XA platform tầng cao thêm 1 unit — hết dính sát nhau.)
            StageSceneKit.MakeClingWall("ClingWall_L", StageArtFire.ClingWall, new Vector2(-10.8f, 6.2f), fireCfg);
            StageSceneKit.MakeClingWall("ClingWall_R", StageArtFire.ClingWall, new Vector2(10.8f, 6.2f), fireCfg);

            // ---- Tàn lửa + tia lửa ----
            StageSceneKit.MakeParticles(null, "PS_Embers", new Vector2(0f, -3f), new Vector2(46f, 2f),
                new Color32(255, 154, 34, 220), new Color32(255, 221, 85, 200),
                28f, 5.5f, new Vector2(0.4f, 1.8f), 0.05f, 0.12f, -0.01f);
            StageSceneKit.MakeParticles(null, "PS_LavaSparks", new Vector2(0f, -4.2f), new Vector2(44f, 1f),
                new Color32(255, 221, 85, 255), new Color32(255, 122, 30, 230),
                13f, 1.3f, new Vector2(0f, 3.6f), 0.06f, 0.14f, 0.06f);

            // ---- Ánh sáng ----
            StageSceneKit.MakeGlobalLight(null, new Color(1f, 0.9f, 0.82f), 0.95f);
            StageSceneKit.MakePointLight(null, "Light_Lava_L", new Vector2(-14f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);
            StageSceneKit.MakePointLight(null, "Light_Lava_C", new Vector2(0f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);
            StageSceneKit.MakePointLight(null, "Light_Lava_R", new Vector2(14f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);

            if (includeCameraRig)
            {
                // ---- Camera (size up: ortho 6.5 → 7.2 + confiner rộng theo đảo 24) ----
                StageSceneKit.MakeCameraRig(null, new Rect(-19f, -4.5f, 38f, 19.5f), 7.2f, new Color32(18, 3, 3, 255));
            }
        }
    }
}
