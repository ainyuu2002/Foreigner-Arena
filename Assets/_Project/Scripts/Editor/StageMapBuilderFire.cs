using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Dựng scene MAP_FIRE "Lò Dung Nham" (design 03): đảo bazan chính + 3 platform bay, biển dung nham
    /// dâng (RisingLava + DeathZone) có icon cảnh báo nhấp nháy, FireBiome (đứng yên tăng %), núi lửa
    /// phun nền sau, tàn lửa bay, ánh sáng cam hắt từ lava, death zone 4 phía, camera Confiner.
    /// </summary>
    internal static class StageMapBuilderFire
    {
        public const string SceneName = "Map_Fire";

        public static void Build()
        {
            var scene = StageSceneKit.NewScene();

            // ---- Nền parallax ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtFire.Sky, new Vector3(0f, 4.5f, 0f), -100, 0.92f, null, 1.5f);
            StageSceneKit.MakeBackdrop("BG_Volcanoes", StageArtFire.Volcanoes, new Vector3(0f, 2.2f, 0f), -90, 0.7f, null, 1.35f);

            // ---- Đảo bazan chính ----
            var tileTop = StageSceneKit.MakeTileAsset(StageArtFire.TileTop, StageArtFire.Dir + "/Tile_FireTop.asset");
            var tileIn = StageSceneKit.MakeTileAsset(StageArtFire.TileInner, StageArtFire.Dir + "/Tile_FireInner.asset");
            var tm = StageSceneKit.MakeGroundTilemap(null, null, -6);
            StageSceneKit.FillGround(tm, tileTop, tileIn, -10, 9, -1, 3); // đảo chính 20 tile — rộng thoải mái

            // ---- 3 platform bay 1 chiều (trái/phải thấp + giữa cao) ----
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtFire.Platform, new Vector2(-11f, 1.8f), null, -5); // mép trong platform (x=-9) chớm đè mép đảo (x=-10) → nhảy sang dễ
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtFire.Platform, new Vector2(11f, 1.8f), null, -5);
            StageSceneKit.MakeOneWayPlatform("Platform_Top", StageArtFire.Platform, new Vector2(0f, 3.6f), null, -5);

            // ---- Spawn + Death zones ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-5f, 1.2f), new Vector2(5f, 1.2f), new Vector2(0f, 5.2f));
            StageSceneKit.MakeDeathZones(null, 24f, 22f, -18f, respawn);

            // ---- Dung nham dâng (RisingLava + DeathZone cùng object) ----
            var fireCfg = AssetDatabase.LoadAssetAtPath<StageConfigSO>(StageSceneKit.DataFolder + "/StageConfig_Fire.asset");
            var lavaGo = new GameObject("RisingLava");
            lavaGo.transform.position = new Vector3(0f, -9.5f, 0f);
            lavaGo.layer = GameLayers.DeathZone;
            var lavaSr = lavaGo.AddComponent<SpriteRenderer>();
            lavaSr.sprite = StageSceneKit.LoadSprite(StageArtFire.Lava);
            lavaSr.sortingOrder = 30; // trước platform/nhân vật → nuốt chửng khi dâng.
            lavaGo.transform.localScale = new Vector3(1.6f, 1.3f, 1f);
            var lavaCol = lavaGo.AddComponent<BoxCollider2D>();
            lavaCol.isTrigger = true;
            lavaCol.size = new Vector2(32f, 8f);

            var warnGo = new GameObject("WarningIcon");
            warnGo.transform.SetParent(lavaGo.transform, false);
            warnGo.transform.localPosition = new Vector3(0f, 6.2f, 0f);
            warnGo.transform.localScale = new Vector3(0.625f, 0.77f, 1f); // bù scale cha ≈ icon 1×1.
            var warnSr = warnGo.AddComponent<SpriteRenderer>();
            warnSr.sprite = StageSceneKit.LoadSprite(StageArtFire.WarnIcon);
            warnSr.sortingOrder = 60;

            var lava = lavaGo.AddComponent<RisingLava>(); // RequireComponent tự thêm Rigidbody2D.
            StageSceneKit.SetObj(lava, "config", fireCfg);
            StageSceneKit.SetObj(lava, "warningIcon", warnSr);
            StageSceneKit.SetBool(lava, "autoStart", true);
            // maxRiseY là tọa độ TÂM khối lava (nửa cao 5.2): -5.35 → mặt lava dừng ở -0.15,
            // SÁT mép đảo nhưng KHÔNG nuốt mặt đảo — đảo chính luôn đứng được (theo feedback test).
            // Muốn gắt hơn (nuốt đảo ép leo cao) thì chỉnh lên -4.4 trong Inspector. CẦN TEST.
            StageSceneKit.SetFloat(lava, "maxRiseY", -5.35f);

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
            fireArea.size = new Vector2(21f, 3.4f);
            var biome = biomeGo.AddComponent<FireBiome>();
            StageSceneKit.SetObj(biome, "config", fireCfg);
            StageSceneKit.SetObj(biome, "fireArea", fireArea);
            StageSceneKit.SetMask(biome, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetMask(biome, "groundLayers", GameLayers.GroundMask | GameLayers.OneWayPlatformMask);

            // ---- Tàn lửa + tia lửa ----
            StageSceneKit.MakeParticles(null, "PS_Embers", new Vector2(0f, -3f), new Vector2(40f, 2f),
                new Color32(255, 154, 34, 220), new Color32(255, 221, 85, 200),
                26f, 5.5f, new Vector2(0.4f, 1.8f), 0.05f, 0.12f, -0.01f);
            StageSceneKit.MakeParticles(null, "PS_LavaSparks", new Vector2(0f, -4.2f), new Vector2(38f, 1f),
                new Color32(255, 221, 85, 255), new Color32(255, 122, 30, 230),
                12f, 1.3f, new Vector2(0f, 3.6f), 0.06f, 0.14f, 0.06f);

            // ---- Ánh sáng ----
            StageSceneKit.MakeGlobalLight(null, new Color(1f, 0.9f, 0.82f), 0.95f);
            StageSceneKit.MakePointLight(null, "Light_Lava_L", new Vector2(-12f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);
            StageSceneKit.MakePointLight(null, "Light_Lava_C", new Vector2(0f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);
            StageSceneKit.MakePointLight(null, "Light_Lava_R", new Vector2(12f, -4.5f), new Color(1f, 0.48f, 0.12f), 0.9f, 8f);

            // ---- Camera ----
            StageSceneKit.MakeCameraRig(null, new Rect(-16f, -4f, 32f, 17f), 6.5f, new Color32(18, 3, 3, 255));

            StageSceneKit.SaveScene(scene, SceneName);
            StageSceneKit.AddStageToRegistry("Lò Dung Nham (Lửa)", SceneName,
                StageSceneKit.LoadSprite(StageArtFire.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Fire.");
        }
    }
}
