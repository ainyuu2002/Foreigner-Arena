using UnityEngine;
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
            StageSceneKit.MakeBackdrop("BG_Vines", StageArtSwamp.Vines, new Vector3(0f, 8.4f, 0f), -70, 0.42f, null, 1.25f);
            StageSceneKit.MakeBackdrop("BG_Water", StageArtSwamp.Water, new Vector3(0f, -3.8f, 0f), -30, 0.12f, null, 1.6f);

            // ---- Đất: 2 bờ cao + bệ chìm ở giữa ----
            var tileTop = StageSceneKit.MakeTileAsset(StageArtSwamp.TileTop, StageArtSwamp.Dir + "/Tile_SwampTop.asset");
            var tileIn = StageSceneKit.MakeTileAsset(StageArtSwamp.TileInner, StageArtSwamp.Dir + "/Tile_SwampInner.asset");
            var tm = StageSceneKit.MakeGroundTilemap(null, null, -6);
            StageSceneKit.FillGround(tm, tileTop, tileIn, -16, -3, -1, 4);  // bờ trái chạm biên camera (mặt y=0)
            StageSceneKit.FillGround(tm, tileTop, tileIn, 2, 15, -1, 4);    // bờ phải chạm biên camera (mặt y=0)
            // Mép map KHÔNG hở: đi bộ không tự rơi; ring-out chỉ khi BỊ ĐÁNH VĂNG bay qua mép (trên không).
            StageSceneKit.FillGround(tm, tileTop, tileIn, -2, 1, -2, 3);    // bệ chìm giữa (mặt y=-1)

            // ---- 2 platform bay lệch tầng ----
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtSwamp.Platform, new Vector2(-6.5f, 1.8f), null, -5);
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtSwamp.Platform, new Vector2(6.5f, 2.8f), null, -5);

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

            // ---- Trang trí: nấm phát sáng + cỏ nến ----
            var mushroom = StageSceneKit.LoadSprite(StageArtSwamp.Mushroom);
            var cattail = StageSceneKit.LoadSprite(StageArtSwamp.Cattail);
            Vector2[] mushroomsAt = { new Vector2(-9.5f, 0.25f), new Vector2(-4.7f, 0.25f),
                                      new Vector2(8.2f, 0.25f), new Vector2(0.5f, -0.75f) };
            foreach (var p in mushroomsAt)
            {
                StageSceneKit.MakeSpriteGO("Deco_Mushroom", mushroom, p, -10, null);
                StageSceneKit.MakePointLight(null, "Light_Mushroom", p + new Vector2(0f, 0.15f),
                    new Color(0.48f, 0.85f, 0.63f), 0.6f, 2.4f);
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
