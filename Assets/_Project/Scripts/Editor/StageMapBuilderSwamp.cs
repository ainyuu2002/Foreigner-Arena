using UnityEngine;
using UnityEngine.Tilemaps;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Dựng scene MAP_SWAMP "Rừng Sương Độc" (design 02, GDD Trang 9): 2 bờ đất cao + bệ chìm giữa nước,
    /// 2 platform bay lệch tầng, SwampBiome (slowness thường trực + giảm lực nhảy ~25%, KHÔNG thiên tai),
    /// rừng cây + dây leo + sương parallax, nấm phát sáng, đom đóm, death zone 4 phía, camera Confiner.
    /// </summary>
    internal static class StageMapBuilderSwamp
    {
        public const string SceneName = "Map_Swamp";
        public const string ConfigPath = StageSceneKit.DataFolder + "/StageConfig_Swamp.asset";

        public static void Build()
        {
            var scene = StageSceneKit.NewScene();
            var cfg = StageSceneKit.EnsureStageConfig(ConfigPath);

            // ---- Nền parallax ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtSwamp.Sky, new Vector3(0f, 4f, 0f), -100, 0.92f, null, 1.5f);
            StageSceneKit.MakeBackdrop("BG_Trees", StageArtSwamp.Trees, new Vector3(0f, 2.4f, 0f), -80, 0.6f, null, 1.3f);
            // CÂY CỔ THỤ KHỔNG LỒ (điểm nhấn kiểu Distant Planet — Pikmin).
            StageSceneKit.MakeBackdrop("BG_BigTree", StageArtSwamp.BigTree, new Vector3(11.5f, 6.5f, 0f), -75, 0.55f, null, 1f);
            var vines = StageSceneKit.MakeBackdrop("BG_Vines", StageArtSwamp.Vines, new Vector3(0f, 8.4f, 0f), -70, 0.42f, null, 1.25f);
            StageSceneKit.AddDrift(vines, new Vector2(0.28f, 0.05f), 9f); // dây leo đung đưa nhẹ
            StageSceneKit.MakeBackdrop("BG_Water", StageArtSwamp.Water, new Vector3(0f, -3.8f, 0f), -30, 0.12f, null, 1.6f);

            // ---- Đất: 2 bờ cao + bệ chìm giữa — 4 variant mặt (cỏ ×2, cỏ nấm, BÙN ƯỚT) chống lặp ----
            var topA = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTop, StageArtSwamp.Dir + "/Tile_SwampTop.asset");
            var topB = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTopB, StageArtSwamp.Dir + "/Tile_SwampTopB.asset");
            var topMud = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTopMud, StageArtSwamp.Dir + "/Tile_SwampTopMud.asset");
            var inA = StageSceneKit.MakeTileAsset(StageArtSwamp.TileInner, StageArtSwamp.Dir + "/Tile_SwampInner.asset");
            var inB = StageSceneKit.MakeTileAsset(StageArtSwamp.TileInnerB, StageArtSwamp.Dir + "/Tile_SwampInnerB.asset");
            Tile[] tops = { topA, topB, topA, topMud };   // bùn ướt ~1/4 số ô — lầy rõ rệt từng mảng
            Tile[] inners = { inA, inB };
            var tm = StageSceneKit.MakeGroundTilemap(null, null, -6);
            StageSceneKit.FillGroundVaried(tm, tops, inners, -16, -3, -1, 4); // bờ trái chạm biên camera
            StageSceneKit.FillGroundVaried(tm, tops, inners, 2, 15, -1, 4);   // bờ phải chạm biên camera
            // Mép map KHÔNG hở: đi bộ không tự rơi; ring-out chỉ khi BỊ ĐÁNH VĂNG bay qua mép (trên không).
            StageSceneKit.FillGroundVaried(tm, tops, inners, -2, 1, -2, 3);   // bệ chìm giữa (mặt y=-1)

            // (Đã bỏ gò chặn mép theo feedback — leo cao bằng TẦNG PLATFORM BAY.)

            // ---- 2 platform bay lệch tầng ----
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtSwamp.Platform, new Vector2(-6.5f, 1.8f), null, -5, 1f, 0.31f);
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtSwamp.Platform, new Vector2(6.5f, 2.8f), null, -5, 1f, 0.31f);
            // TẦNG CAO (feedback): nhảy đôi từ platform phải lên — camera nâng theo, thấy tán cây khổng lồ.
            StageSceneKit.MakeOneWayPlatform("Platform_High", StageArtSwamp.Platform, new Vector2(0f, 4.9f), null, -5, 1f, 0.31f);

            // ---- Spawn + Death zones ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-6f, 1.2f), new Vector2(6f, 1.2f), new Vector2(-7.5f, 3f)); // respawn trên BỜ TRÁI rộng — KHÔNG đặt giữa map vì bên dưới là bệ chìm kẹp 2 khe nước chết, rơi lệch là death-loop
            StageSceneKit.MakeDeathZones(null, 24f, 22f, -16f, respawn);

            // ---- SwampBiome: slowness thường trực + giảm lực nhảy (GDD Trang 9 — KHÔNG thiên tai) ----
            var biomeGo = new GameObject("SwampBiome");
            biomeGo.transform.position = new Vector3(0f, 2f, 0f);
            var area = biomeGo.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(34f, 16f);
            var biome = biomeGo.AddComponent<SwampBiome>();
            StageSceneKit.SetObj(biome, "config", cfg);
            StageSceneKit.SetObj(biome, "swampArea", area);
            StageSceneKit.SetMask(biome, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(biome, "groundLayers", GameLayers.GroundMask | GameLayers.OneWayPlatformMask);

            // MẶT BỜ ĐẤT HỮU CƠ: tầng rêu-đất-rễ, vai tròn phía khe nước (trái thường, phải lật gương).
            var bankFace = StageSceneKit.LoadSprite(StageArtSwamp.FloorFace);
            StageSceneKit.MakeSpriteGO("Deco_BankFace_L", bankFace, new Vector3(-9f, -3.05f, 0f), -4, null);
            StageSceneKit.MakeSpriteGO("Deco_BankFace_R", bankFace, new Vector3(9f, -3.05f, 0f), -4, null,
                new Vector3(-1f, 1f, 1f));
            // Chùm rễ rủ dưới bệ chìm giữa (treo trên mặt nước).
            StageSceneKit.MakeSpriteGO("Deco_Roots", StageSceneKit.LoadSprite(StageArtSwamp.Roots),
                new Vector3(0f, -4.8f, 0f), -4, null);

            // ---- Trang trí: nấm phát sáng + cỏ nến ----
            var mushroom = StageSceneKit.LoadSprite(StageArtSwamp.Mushroom);
            var cattail = StageSceneKit.LoadSprite(StageArtSwamp.Cattail);
            Vector2[] mushroomsAt = { new Vector2(-9.5f, 0.25f), new Vector2(-4.7f, 0.25f),
                                      new Vector2(8.2f, 0.25f), new Vector2(0.5f, -0.75f) };
            float[] mushScale = { 1.7f, 1.2f, 2.1f, 1.4f };               // nấm TO kiểu Pikmin, kích cỡ so le
            for (int i = 0; i < mushroomsAt.Length; i++)
            {
                var p = mushroomsAt[i];
                float s = mushScale[i % mushScale.Length];
                StageSceneKit.MakeSpriteGO("Deco_Mushroom", mushroom,
                    new Vector3(p.x, p.y + (s - 1f) * 0.25f, 0f), -10, null, new Vector3(s, s, 1f));
                StageSceneKit.MakePointLight(null, "Light_Mushroom", p + new Vector2(0f, 0.15f + (s - 1f) * 0.3f),
                    new Color(0.48f, 0.85f, 0.63f), 0.55f + s * 0.15f, 2.2f + s);
            }
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(-3.3f, 0.37f), -10, null);
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(3.2f, 0.37f), -10, null);
            StageSceneKit.MakeSpriteGO("Deco_Cattail", cattail, new Vector2(13.3f, 0.37f), -10, null);

            // ---- Đom đóm ----
            StageSceneKit.MakeParticles(null, "PS_Fireflies", new Vector2(0f, 3f), new Vector2(30f, 8f),
                new Color32(184, 232, 120, 210), new Color32(122, 216, 160, 190),
                14f, 7f, new Vector2(0.15f, 0.1f), 0.04f, 0.09f);

            // ---- Ánh sáng ----
            StageSceneKit.MakeGlobalLight(null, new Color(0.82f, 0.92f, 0.84f), 0.9f);
            StageSceneKit.MakePointLight(null, "Light_Moon", new Vector2(-12.5f, 10f),
                new Color(0.85f, 0.91f, 0.75f), 0.55f, 8f);

            // ---- Camera ----
            StageSceneKit.MakeCameraRig(null, new Rect(-16.5f, -6f, 33f, 18f), 6.5f, new Color32(7, 15, 9, 255));

            StageSceneKit.SaveScene(scene, SceneName);
            StageSceneKit.AddStageToRegistry("Rừng Sương Độc (Đầm Lầy)", SceneName,
                StageSceneKit.LoadSprite(StageArtSwamp.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Swamp.");
        }
    }
}
