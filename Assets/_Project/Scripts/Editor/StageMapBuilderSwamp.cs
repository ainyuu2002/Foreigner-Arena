using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Dựng scene MAP_SWAMP "Rừng Sương Độc" (design 02, GDD Trang 9): 2 bờ đất cao SẠCH (chạy nhảy
    /// bình thường) + HỐ BÙN GIỮA dài 6 ô — TOÀN BỘ chất lầy dồn vào hố (SwampBiome + MudPitZone chỉ
    /// phủ lòng hố, theo yêu cầu leader), NẤM NHÚN (đáp mũ bật cao — 1 con trong hố làm đường thoát),
    /// sương độc 2 lớp trước/sau nhân vật, đom đóm + bào tử, nấm phát sáng, death zone 4 phía, camera Confiner.
    /// </summary>
    internal static class StageMapBuilderSwamp
    {
        public const string SceneName = "Map_Swamp";
        public const string ConfigPath = StageSceneKit.DataFolder + "/StageConfig_Swamp.asset";

        /// <summary>Tên root object thuộc rig camera/intro — KHÔNG đụng khi restyle scene có sẵn
        /// (dựng bởi StageCameraRigBuilder/IntroWinLoseSetupTool, chạy sau map builder này).</summary>
        private static readonly HashSet<string> RigRootNames = new HashSet<string>
        {
            "CM_MatchCamera", "CM_TargetGroup", "CM_IntroP1", "CM_IntroP2", "RoundDirector",
        };

        public static void Build()
        {
            var scene = StageSceneKit.NewScene();
            var cfg = StageSceneKit.EnsureStageConfig(ConfigPath);
            PopulateScene(cfg, includeCameraRig: true);
            StageSceneKit.SaveScene(scene, SceneName);
            StageSceneKit.AddStageToRegistry("Rừng Sương Độc (Đầm Lầy)", SceneName,
                StageSceneKit.LoadSprite(StageArtSwamp.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Swamp.");
        }

        /// <summary>Dựng lại NỘI DUNG map trên scene ĐÃ CÓ SẴN, giữ nguyên rig camera/intro
        /// (<see cref="RigRootNames"/>) — xem StageMapBuilderIce.RestyleExistingScene. Idempotent.</summary>
        [UnityEditor.MenuItem("FA/Stage/Restyle map content only (keep camera+intro rig)/Map_Swamp")]
        public static void RestyleExistingScene()
        {
            var path = "Assets/_Project/Scenes/" + SceneName + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (!RigRootNames.Contains(root.name)) Object.DestroyImmediate(root);

            var cfg = StageSceneKit.EnsureStageConfig(ConfigPath);
            PopulateScene(cfg, includeCameraRig: false);

            StageSceneKit.SaveScene(scene, SceneName);
            Debug.Log("[StageMapBuilderSwamp] Đã restyle nội dung Map_Swamp — giữ nguyên rig camera/intro.");
        }

        private static void PopulateScene(StageConfigSO cfg, bool includeCameraRig)
        {
            // ---- Nền parallax ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtSwamp.Sky, new Vector3(0f, 4f, 0f), -100, 0.92f, null, 1.5f);
            StageSceneKit.MakeBackdrop("BG_Trees", StageArtSwamp.Trees, new Vector3(0f, 2.4f, 0f), -80, 0.6f, null, 1.3f);
            // CÂY CỔ THỤ KHỔNG LỒ (điểm nhấn kiểu Distant Planet — Pikmin).
            StageSceneKit.MakeBackdrop("BG_BigTree", StageArtSwamp.BigTree, new Vector3(11.5f, 6.5f, 0f), -75, 0.55f, null, 1f);
            var vines = StageSceneKit.MakeBackdrop("BG_Vines", StageArtSwamp.Vines, new Vector3(0f, 8.4f, 0f), -70, 0.42f, null, 1.25f);
            StageSceneKit.AddDrift(vines, new Vector2(0.28f, 0.05f), 9f); // dây leo đung đưa nhẹ
            StageSceneKit.MakeBackdrop("BG_Water", StageArtSwamp.Water, new Vector3(0f, -3.8f, 0f), -30, 0.12f, null, 1.6f);
            // Vệt lấp lánh mặt nước TRÔI (chỉ lộ ở khe nước giữa các bờ — nằm sau tilemap đất).
            var glint = StageSceneKit.MakeSpriteGO("Deco_WaterGlint", StageSceneKit.LoadSprite(StageArtSwamp.WaterGlint),
                new Vector3(0f, -2.3f, 0f), -29, null, new Vector3(1.6f, 1f, 1f));
            StageSceneKit.AddDrift(glint.gameObject, new Vector2(0.6f, 0.03f), 7f);
            // SƯƠNG ĐỘC 2 LỚP (tên map "Rừng Sương Độc"): lớp SAU nhân vật + lớp TRƯỚC mờ nhẹ —
            // chiều sâu kiểu Smash; trôi lững lờ 2 nhịp lệch nhau.
            var mistB = StageSceneKit.MakeBackdrop("BG_Mist", StageArtSwamp.Fog, new Vector3(0f, -0.7f, 0f), -20, 0.78f, null, 1.1f);
            StageSceneKit.AddDrift(mistB, new Vector2(0.55f, 0.05f), 26f);
            var mistF = StageSceneKit.MakeBackdrop("FG_Mist", StageArtSwamp.Fog, new Vector3(0f, -1.7f, 0f), 45, 1.1f, null, 1.3f);
            mistF.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.55f);
            StageSceneKit.AddDrift(mistF, new Vector2(0.8f, 0.06f), 31f);

            // ---- Đất: 2 bờ cao SẠCH + HỐ BÙN GIỮA — bờ dùng tile cỏ sạch, hố lát toàn bùn ướt ----
            var topA = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTop, StageArtSwamp.Dir + "/Tile_SwampTop.asset");
            var topB = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTopB, StageArtSwamp.Dir + "/Tile_SwampTopB.asset");
            var topMud = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTopMud, StageArtSwamp.Dir + "/Tile_SwampTopMud.asset");
            var inA = StageSceneKit.MakeTileAsset(StageArtSwamp.TileInner, StageArtSwamp.Dir + "/Tile_SwampInner.asset");
            var inB = StageSceneKit.MakeTileAsset(StageArtSwamp.TileInnerB, StageArtSwamp.Dir + "/Tile_SwampInnerB.asset");
            // (Yêu cầu leader: BỎ HẾT làm chậm trên 2 bờ — bờ dùng tile cỏ SẠCH, không rải bùn;
            // toàn bộ chất LẦY dồn về HỐ GIỮA: hố dài ra 4 → 6 ô, lát TOÀN BÙN ướt = vũng lầy thật.)
            Tile[] tops = { topA, topB };                 // bờ: cỏ sạch — KHÔNG còn ô bùn
            Tile[] mudTops = { topMud };                  // hố giữa: lát toàn bùn ướt
            Tile[] inners = { inA, inB };
            var tm = StageSceneKit.MakeGroundTilemap(null, null, -6);
            // (Size up theo yêu cầu leader: tổng bề ngang 26 → 32 unit — hợp cỡ Archer hiện tại.)
            StageSceneKit.FillGroundVaried(tm, tops, inners, -16, -4, -1, 4);   // bờ trái
            StageSceneKit.FillGroundVaried(tm, tops, inners, 3, 15, -1, 4);     // bờ phải
            // Mép map KHÔNG hở: đi bộ không tự rơi; ring-out chỉ khi BỊ ĐÁNH VĂNG bay qua mép (trên không).
            StageSceneKit.FillGroundVaried(tm, mudTops, inners, -3, 2, -2, 3);  // HỐ BÙN giữa (mặt y=-1, DÀI 6 ô)

            // ---- VŨNG LẦY = ĐÚNG CÁI HỐ GIỮA (yêu cầu leader): MudPitZone phủ trọn mặt hố —
            // đứng trong hố mới bị lầy; 2 bờ và platform hoàn toàn SẠCH slow. ----
            var mudGo = new GameObject("MudPitZone");
            mudGo.transform.position = new Vector3(0f, -0.7f, 0f); // mặt hố y=-1, vùng phủ cao 0.6
            var mudCol = mudGo.AddComponent<BoxCollider2D>();
            mudCol.isTrigger = true;
            mudCol.size = new Vector2(6f, 0.6f);
            var mud = mudGo.AddComponent<MudPitZone>();
            StageSceneKit.SetObj(mud, "config", cfg);
            StageSceneKit.SetObj(mud, "mudArea", mudCol);
            StageSceneKit.SetMask(mud, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(mud, "groundLayers", GameLayers.GroundMask | GameLayers.OneWayPlatformMask);

            // (Đã bỏ gò chặn mép theo feedback — leo cao bằng TẦNG PLATFORM BAY.)

            // ---- 2 platform bay lệch tầng (size up: giãn X, độ cao giữ nguyên) ----
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtSwamp.Platform, new Vector2(-7.5f, 1.8f), null, -5, 1f, 0.31f);
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtSwamp.Platform, new Vector2(7.5f, 2.8f), null, -5, 1f, 0.31f);
            // TẦNG CAO (feedback): nhảy đôi từ platform phải lên — camera nâng theo, thấy tán cây khổng lồ.
            StageSceneKit.MakeOneWayPlatform("Platform_High", StageArtSwamp.Platform, new Vector2(0f, 4.9f), null, -5, 1f, 0.31f);

            // ---- NẤM NHÚN (cơ chế mới, deterministic — GDD: swamp KHÔNG thiên tai): đáp xuống mũ →
            // bật tung lên. Bù trừ giảm lực nhảy 25%; con GIỮA map là ĐƯỜNG THOÁT khỏi hố bùn. ----
            var bounceSprite = StageSceneKit.LoadSprite(StageArtSwamp.BounceShroom);
            var bounceMat = StageSceneKit.EnsureMushroomMaterial(cfg);
            void MakeBouncer(Vector2 at, float scale)
            {
                var bSr = StageSceneKit.MakeSpriteGO("BounceShroom", bounceSprite,
                    new Vector3(at.x, at.y, 0f), -2, null, new Vector3(scale, scale, 1f));
                var bGo = bSr.gameObject;
                bGo.layer = GameLayers.OneWayPlatform;
                var cap = bGo.AddComponent<BoxCollider2D>();
                cap.isTrigger = false;                 // v2: mũ ĐẶC + bounciness (bật theo lực rơi).
                cap.size = new Vector2(1.9f, 0.32f);   // dải mỏng trên ĐỈNH mũ (local — tự scale theo GO).
                cap.offset = new Vector2(0f, 0.72f);
                cap.sharedMaterial = bounceMat;
                cap.usedByEffector = true;
                var eff = bGo.AddComponent<PlatformEffector2D>(); // one-way: nhảy xuyên từ dưới lên.
                eff.useOneWay = true;
                eff.surfaceArc = 180f;
                var bouncer = bGo.AddComponent<MushroomBouncer>();
                StageSceneKit.SetObj(bouncer, "config", cfg);
                StageSceneKit.SetObj(bouncer, "capArea", cap);
                StageSceneKit.SetMask(bouncer, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
                StageSceneKit.MakePointLight(null, "Light_BounceShroom", at + new Vector2(0f, 0.7f * scale),
                    new Color(1f, 0.62f, 0.8f), 0.6f, 3.4f);
            }
            // (Feedback: KHÔNG đặt nấm sát mép — chừa thoáng 2 mép sàn để đánh văng ring-out.
            //  Feedback 2: hạ thấp — GỐC NẤM GHIM HẲN vào mặt cỏ, không hở chân lơ lửng.)
            MakeBouncer(new Vector2(-11f, 1.38f), 1f);      // giữa bờ trái (chân lún nhẹ vào cỏ y=0)
            MakeBouncer(new Vector2(11f, 1.38f), 1f);       // giữa bờ phải
            MakeBouncer(new Vector2(-1f, 0.15f), 0.85f);    // trong HỐ BÙN (mặt y=-1) — đường thoát hố

            // ---- TƯỜNG BÁM lơ lửng 2 bên (feedback: chạm tường trên không là bám được) ----
            // Treo CAO (chân cột cách bờ ~4 — gấp đôi bản đầu, feedback): bật NẤM NHÚN lên là bám
            // đúng tầm — combo nấm + tường thành route leo lên cao.
            StageSceneKit.MakeClingWall("ClingWall_L", StageArtSwamp.ClingWall, new Vector2(-12.5f, 6f), cfg);
            StageSceneKit.MakeClingWall("ClingWall_R", StageArtSwamp.ClingWall, new Vector2(12.5f, 6f), cfg);

            // ---- Spawn + Death zones ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-7f, 1.2f), new Vector2(7f, 1.2f), new Vector2(-9f, 3f)); // respawn trên BỜ TRÁI rộng — KHÔNG đặt giữa map vì bên dưới là hố bùn, rơi lệch là death-loop
            StageSceneKit.MakeDeathZones(null, 24f, 22f, -16f, respawn);

            // ---- SwampBiome: slowness + giảm lực nhảy CHỈ TRONG HỐ GIỮA (yêu cầu leader: bỏ slow
            // toàn map, dồn hết chất lầy về cái hố — 2 bờ chạy nhảy hoàn toàn bình thường) ----
            var biomeGo = new GameObject("SwampBiome");
            biomeGo.transform.position = new Vector3(0f, 0.1f, 0f);
            var area = biomeGo.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(6f, 2.4f); // phủ đúng lòng hố (world x -3..3, y -1.1..1.3)
            var biome = biomeGo.AddComponent<SwampBiome>();
            StageSceneKit.SetObj(biome, "config", cfg);
            StageSceneKit.SetObj(biome, "swampArea", area);
            StageSceneKit.SetMask(biome, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(biome, "groundLayers", GameLayers.GroundMask | GameLayers.OneWayPlatformMask);

            // MẶT BỜ ĐẤT HỮU CƠ: tầng rêu-đất-rễ, vai tròn phía khe nước (trái thường, phải lật gương).
            // Nén ngang 0.8 khớp bờ đã thu gọn (sprite 14u → phủ 11.2u, mép trong vẫn ôm khe nước).
            var bankFace = StageSceneKit.LoadSprite(StageArtSwamp.FloorFace);
            StageSceneKit.MakeSpriteGO("Deco_BankFace_L", bankFace, new Vector3(-9.5f, -3.05f, 0f), -4, null,
                new Vector3(0.93f, 1f, 1f));
            StageSceneKit.MakeSpriteGO("Deco_BankFace_R", bankFace, new Vector3(9.5f, -3.05f, 0f), -4, null,
                new Vector3(-0.93f, 1f, 1f));
            // Chùm rễ rủ dưới bệ chìm giữa (treo trên mặt nước).
            StageSceneKit.MakeSpriteGO("Deco_Roots", StageSceneKit.LoadSprite(StageArtSwamp.Roots),
                new Vector3(0f, -4.8f, 0f), -4, null);

            // ---- Trang trí: nấm phát sáng + cỏ nến ----
            var mushroom = StageSceneKit.LoadSprite(StageArtSwamp.Mushroom);
            var cattail = StageSceneKit.LoadSprite(StageArtSwamp.Cattail);
            // (Feedback: nấm trang trí bị LƠ LỬNG — sprite pivot GIỮA, chân nấm cách pivot ~0.16×scale
            //  → tính y từ MẶT ĐẤT dưới chân từng cây, ghim gốc lún nhẹ vào đất.)
            Vector2[] mushroomsAt = { new Vector2(-13.5f, 0f), new Vector2(-5.5f, 0f),
                                      new Vector2(8.6f, 0f), new Vector2(1.2f, -1f) }; // y = MẶT ĐẤT chân nấm
            float[] mushScale = { 1.7f, 1.2f, 2.1f, 1.4f };               // nấm TO kiểu Pikmin, kích cỡ so le
            for (int i = 0; i < mushroomsAt.Length; i++)
            {
                var p = mushroomsAt[i];
                float s = mushScale[i % mushScale.Length];
                float y = p.y + 0.16f * s - 0.04f; // pivot sao cho CHÂN chạm/lún nhẹ mặt đất
                StageSceneKit.MakeSpriteGO("Deco_Mushroom", mushroom,
                    new Vector3(p.x, y, 0f), -10, null, new Vector3(s, s, 1f));
                StageSceneKit.MakePointLight(null, "Light_Mushroom", new Vector2(p.x, y + 0.2f * s),
                    new Color(0.48f, 0.85f, 0.63f), 0.55f + s * 0.15f, 2.2f + s);
            }
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(-3.8f, 0.37f), -10, null);
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(3.8f, 0.37f), -10, null);
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(12.4f, 0.37f), -10, null);

            // ---- Đom đóm + BÀO TỬ rơi từ tán cây cổ thụ (polish) ----
            StageSceneKit.MakeParticles(null, "PS_Fireflies", new Vector2(0f, 3f), new Vector2(36f, 8f),
                new Color32(184, 232, 120, 210), new Color32(122, 216, 160, 190),
                14f, 7f, new Vector2(0.15f, 0.1f), 0.04f, 0.09f);
            StageSceneKit.MakeParticles(null, "PS_Spores", new Vector2(8f, 6.5f), new Vector2(18f, 8f),
                new Color32(196, 232, 160, 150), new Color32(150, 216, 190, 120),
                6f, 12f, new Vector2(-0.25f, -0.35f), 0.03f, 0.07f, 0.002f);

            // ---- Ánh sáng ----
            StageSceneKit.MakeGlobalLight(null, new Color(0.82f, 0.92f, 0.84f), 0.9f);
            StageSceneKit.MakePointLight(null, "Light_Moon", new Vector2(-12.5f, 10f),
                new Color(0.85f, 0.91f, 0.75f), 0.55f, 8f);

            if (includeCameraRig)
            {
                // ---- Camera (size up: ortho 6.5 → 7.2 + confiner theo map 32 — vẫn bao quát gần trọn) ----
                StageSceneKit.MakeCameraRig(null, new Rect(-16.5f, -6.5f, 33f, 20f), 7.2f, new Color32(7, 15, 9, 255));
            }
        }
    }
}
