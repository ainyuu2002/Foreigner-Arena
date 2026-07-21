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
    /// Dựng scene MAP_ICE "Đỉnh Vĩnh Đông" (design 01): sàn băng lớn + 5 platform bay + 2 tường bám,
    /// trời đêm cực quang + trăng + núi tuyết parallax, tuyết rơi ambient (nghiêng theo gió khi bão),
    /// bão tuyết chu kỳ (SnowstormHazard), gai băng +%, death zone 4 phía, camera Confiner.
    /// (Leader: BỎ cơ chế trơn — ma sát thường; sàn to hơn hợp cỡ Archer.)
    /// </summary>
    internal static class StageMapBuilderIce
    {
        public const string SceneName = "Map_Ice";

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
            StageSceneKit.AddStageToRegistry("Đỉnh Vĩnh Đông (Băng)", SceneName,
                StageSceneKit.LoadSprite(StageArtIce.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Ice.");
        }

        /// <summary>Dựng lại NỘI DUNG map (sàn/platform/hazard/ánh sáng...) trên scene ĐÃ CÓ SẴN, giữ
        /// nguyên rig camera/intro (<see cref="RigRootNames"/>) — dùng khi redesign map mà không muốn
        /// build-từ-đầu xoá mất rig đã wire bởi StageCameraRigBuilder/IntroWinLoseSetupTool. Idempotent.</summary>
        [MenuItem("FA/Stage/Restyle map content only (keep camera+intro rig)/Map_Ice")]
        public static void RestyleExistingScene()
        {
            var path = "Assets/_Project/Scenes/" + SceneName + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (!RigRootNames.Contains(root.name)) Object.DestroyImmediate(root);

            PopulateScene(includeCameraRig: false);

            StageSceneKit.SaveScene(scene, SceneName);
            Debug.Log("[StageMapBuilderIce] Đã restyle nội dung Map_Ice — giữ nguyên rig camera/intro.");
        }

        private static void PopulateScene(bool includeCameraRig)
        {
            // ---- Nền parallax (xa → gần) ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtIce.Sky, new Vector3(0f, 4f, 0f), -100, 0.92f, null, 1.5f);
            // Cực quang lớp riêng TRÔI LỮNG LỜ (AmbientDrift) — nền 'sống' như Smash.
            // Hạ thấp vào KHUNG CAMERA MẶC ĐỊNH (feedback: cảnh đẹp phải thấy được ngay khi đứng dưới sàn).
            var aurora = StageSceneKit.MakeBackdrop("BG_Aurora", StageArtIce.Aurora, new Vector3(0f, 5.1f, 0f), -95, 0.9f, null, 1.5f);
            StageSceneKit.AddDrift(aurora, new Vector2(1.4f, 0.18f), 22f);
            StageSceneKit.MakeBackdrop("BG_Mountains", StageArtIce.Mountains, new Vector3(0f, 1.6f, 0f), -90, 0.72f, null, 1.35f);
            // NÚI BĂNG KHỔNG LỒ sau sàn đấu (redesign kiểu Summit) — khối chính của cảnh.
            StageSceneKit.MakeBackdrop("BG_Iceberg", StageArtIce.Iceberg, new Vector3(0f, 3.6f, 0f), -85, 0.68f, null, 0.92f);
            StageSceneKit.MakeBackdrop("BG_Pines", StageArtIce.Pines, new Vector3(0f, 1.2f, 0f), -80, 0.5f, null, 1.3f);

            // ---- Sàn: TOÀN BỘ LÀ BĂNG về HÌNH ẢNH, nhưng BỎ CƠ CHẾ TRƠN (yêu cầu leader — ma sát
            // thường, không gán IceSurface nữa; gánh nặng cơ chế map băng dồn về BÃO TUYẾT + GAI BĂNG).
            // 2 variant mặt = băng xanh tuyết phủ; mảng TileGloss = băng TRẦN bóng loáng làm điểm nhấn. ----
            var topA = StageSceneKit.MakeTileAsset(StageArtIce.TileTop, StageArtIce.Dir + "/Tile_IceTop.asset");
            var topB = StageSceneKit.MakeTileAsset(StageArtIce.TileTopB, StageArtIce.Dir + "/Tile_IceTopB.asset");
            var gloss = StageSceneKit.MakeTileAsset(StageArtIce.TileGloss, StageArtIce.Dir + "/Tile_IceGloss.asset");
            var inA = StageSceneKit.MakeTileAsset(StageArtIce.TileInner, StageArtIce.Dir + "/Tile_IceInner.asset");
            var inB = StageSceneKit.MakeTileAsset(StageArtIce.TileInnerB, StageArtIce.Dir + "/Tile_IceInnerB.asset");
            Tile[] tops = { topA, topB };
            Tile[] inners = { inA, inB };

            // (Size up theo yêu cầu leader: sàn 28 → 34 unit — hợp cỡ Archer hiện tại; ma sát THƯỜNG.)
            var tmIce = StageSceneKit.MakeGroundTilemap(null, null, -5);
            tmIce.gameObject.name = "Tilemap (GroundIce)";

            bool IsGlossPatch(int x) => (x >= -11 && x <= -8) || (x >= 1 && x <= 4) || (x >= 11 && x <= 14);
            for (int x = -17; x <= 16; x++)
            {
                tmIce.SetTile(new Vector3Int(x, -1, 0),
                    IsGlossPatch(x) ? gloss : StageSceneKit.PickVariant(tops, x, -1));
                for (int d = 2; d <= 4; d++)                                     // lòng sàn = băng sâu xanh đậm
                    tmIce.SetTile(new Vector3Int(x, -d, 0), StageSceneKit.PickVariant(inners, x, -d));
            }

            // MÉP BẬC DƯỚI 2 BÊN (feedback: "mép bên dưới cũng phải đứng được"): vai băng nhô ra của
            // FloorFace giờ có COLLISION THẬT — bậc thấp hơn mặt sàn 1 unit, đứng/bám được như mép đài Smash.
            foreach (int sx in new[] { -18, 17 })
            {
                tmIce.SetTile(new Vector3Int(sx, -2, 0), StageSceneKit.PickVariant(tops, sx, -2));
                for (int d = 3; d <= 4; d++)
                    tmIce.SetTile(new Vector3Int(sx, -d, 0), StageSceneKit.PickVariant(inners, sx, -d));
            }

            // (Đã bỏ gò chặn mép theo feedback — mép sàn để thoáng, leo cao bằng TẦNG PLATFORM BAY.)

            // MẶT SÀN HỮU CƠ (redesign Summit): khối băng lượn tròn phủ lên mặt tile phẳng —
            // chỉ visual, collision vẫn là tilemap; chừa hở hàng tile trên cùng (tuyết + mảng băng trơn).
            StageSceneKit.MakeSpriteGO("Deco_FloorFace", StageSceneKit.LoadSprite(StageArtIce.FloorFace),
                new Vector3(0f, -3.3f, 0f), -4, null, new Vector3(1.23f, 1f, 1f)); // giãn ngang khớp sàn 34+bậc

            // Cụm GAI BĂNG — không chỉ trang trí: chạm vào bị tăng Resistance % liên tục
            // (tái dùng LavaFloorZone qua IPercentReceiver; tốc độ = lavaFloorPercentPerSec trong StageConfig_Ice).
            var iceCfgEarly = AssetDatabase.LoadAssetAtPath<StageConfigSO>(StageSceneKit.DataFolder + "/StageConfig_Ice.asset");
            MakeSpikeHazard(new Vector3(-15f, 1f, 0f), new Vector3(1.1f, 1.1f, 1f), iceCfgEarly);
            MakeSpikeHazard(new Vector3(3.2f, 0.85f, 0f), new Vector3(-0.8f, 0.8f, 1f), iceCfgEarly);
            MakeSpikeHazard(new Vector3(15.1f, 1f, 0f), new Vector3(-1.05f, 1.05f, 1f), iceCfgEarly);

            // Nhũ băng trang trí dưới đáy sàn (rủ dưới khối FloorFace).
            var icicle = StageSceneKit.LoadSprite(StageArtIce.Icicle);
            for (float x = -15.5f; x <= 15.5f; x += 2.6f)
                StageSceneKit.MakeSpriteGO("Deco_Icicle", icicle,
                    new Vector3(x + Random.Range(-0.5f, 0.5f), -5.9f, 0f), -3, null,
                    new Vector3(1f, Random.Range(0.7f, 1.3f), 1f));

            // ---- Platform bay 1 chiều: 2 bên đối xứng + ĐẢO NHỎ TRÊN CAO (feedback test) ----
            // (Size up: giãn X các platform — ĐỘ CAO GIỮ NGUYÊN để tầm nhảy Archer không đổi.)
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtIce.Platform, new Vector2(-6.8f, 1.8f), null, -5, 1f, 0.5f);
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtIce.Platform, new Vector2(6.8f, 1.8f), null, -5, 1f, 0.5f);
            StageSceneKit.MakeOneWayPlatform("Platform_Top", StageArtIce.Platform, new Vector2(0f, 4.2f), null, -5, 1f, 0.5f);
            // TẦNG CAO (feedback): leo lên đây camera nâng theo → thấy trọn cực quang trôi + trăng.
            StageSceneKit.MakeOneWayPlatform("Platform_High_L", StageArtIce.Platform, new Vector2(-5.5f, 6.5f), null, -5, 1f, 0.5f);
            StageSceneKit.MakeOneWayPlatform("Platform_High_R", StageArtIce.Platform, new Vector2(5.5f, 6.5f), null, -5, 1f, 0.5f);

            // ---- Spawn points + Death zones (KHÔNG tường chặn 2 bên — mép hở ring-out như Smash) ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-7f, 1.2f), new Vector2(7f, 1.2f), new Vector2(0f, 3.5f));
            StageSceneKit.MakeDeathZones(null, 27f, 20f, -14f, respawn);

            var iceCfg = AssetDatabase.LoadAssetAtPath<StageConfigSO>(StageSceneKit.DataFolder + "/StageConfig_Ice.asset");

            // ---- TƯỜNG BÁM lơ lửng 2 bên (feedback: chạm tường trên không là bám được) ----
            // Treo CAO (chân cột cách sàn ~4.6 — gấp đôi bản đầu, feedback): với tới từ TẦNG CAO
            // (±5.5, 6.5) hoặc nấm/đà nhảy đôi; đứng được trên đỉnh cột.
            // (Feedback: dời tường XA platform thêm 1 unit — hết dính sát nhau.)
            StageSceneKit.MakeClingWall("ClingWall_L", StageArtIce.ClingWall, new Vector2(-11.5f, 6.6f), iceCfg);
            StageSceneKit.MakeClingWall("ClingWall_R", StageArtIce.ClingWall, new Vector2(11.5f, 6.6f), iceCfg);

            // ---- Bão tuyết + hạt tuyết ----
            var hazardGo = new GameObject("SnowstormHazard");
            hazardGo.transform.position = new Vector3(0f, 4f, 0f);
            var area = hazardGo.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(44f, 20f);
            var stormPs = StageSceneKit.MakeParticles(hazardGo.transform, "PS_StormSnow", new Vector2(0f, 6f),
                new Vector2(48f, 14f), Color.white, new Color(0.82f, 0.89f, 1f),
                380f, 1.4f, new Vector2(-13f, -2.5f), 0.05f, 0.1f, 0f, false);
            var ambientPs = StageSceneKit.MakeParticles(null, "PS_AmbientSnow", new Vector2(0f, 13f), new Vector2(52f, 2f),
                Color.white, new Color(0.81f, 0.88f, 1f), 46f, 10f, new Vector2(-0.4f, -1.2f), 0.05f, 0.12f, 0.015f);
            var hazard = hazardGo.AddComponent<SnowstormHazard>();
            StageSceneKit.SetObj(hazard, "config", iceCfg);
            StageSceneKit.SetMask(hazard, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetObj(hazard, "stormArea", area);
            StageSceneKit.SetObj(hazard, "snowParticles", stormPs);
            // Feedback test: tuyết nền cũng phải NGHIÊNG THEO GIÓ khi bão (SnowstormHazard tự đổi hướng hạt).
            StageSceneKit.SetObj(hazard, "ambientSnow", ambientPs);

            // ---- Ánh sáng URP 2D ----
            StageSceneKit.MakeGlobalLight(null, new Color(0.8f, 0.86f, 1f), 0.95f);
            StageSceneKit.MakePointLight(null, "Light_Moon", new Vector2(15f, 11f),
                new Color(0.91f, 0.91f, 0.78f), 0.7f, 9f);
            StageSceneKit.MakePointLight(null, "Light_Aurora", new Vector2(-8f, 11f),
                new Color(0.25f, 0.85f, 0.69f), 0.35f, 12f);

            if (includeCameraRig)
            {
                // ---- Camera (size up: ortho 7.2 + confiner rộng theo sàn 34) ----
                // ĐÁY confiner NÂNG lên -4 (feedback: camera bị chếch xuống thân băng — khu chơi là NỬA TRÊN):
                // camera thấp nhất cũng chỉ lộ ~4 unit dưới mặt sàn, phần trời/cực quang luôn chiếm khung.
                StageSceneKit.MakeCameraRig(null, new Rect(-20.5f, -4f, 41f, 17.5f), 7.2f,
                    new Color32(7, 10, 30, 255));
            }
        }

        /// <summary>Cụm gai băng v2: sprite + IceSpikeHazard — CHẠM là +% một phát + hất nhẹ + flash
        /// (redesign feedback test, thay DoT LavaFloorZone vô hình).</summary>
        private static void MakeSpikeHazard(Vector3 pos, Vector3 scale, StageConfigSO cfg)
        {
            var sr = StageSceneKit.MakeSpriteGO("Hazard_Spikes", StageSceneKit.LoadSprite(StageArtIce.Spikes),
                pos, -2, null, scale);
            var go = sr.gameObject;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.7f, 1.2f);   // ôm SÁT cụm gai — phải giẫm lên mới dính.
            col.offset = new Vector2(0f, -0.1f);
            var zone = go.AddComponent<IceSpikeHazard>();
            StageSceneKit.SetObj(zone, "config", cfg);
            StageSceneKit.SetObj(zone, "spikeArea", col);
            StageSceneKit.SetMask(zone, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
        }
    }
}
