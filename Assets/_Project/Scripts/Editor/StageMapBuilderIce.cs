using UnityEditor;
using UnityEngine;
using FA.Core;
using FA.Stage;

namespace FA.EditorTools
{
    /// <summary>
    /// Dựng scene MAP_ICE "Đỉnh Vĩnh Đông" (design 01): sàn băng chính + 2 platform bay đối xứng,
    /// tường băng vỡ được 2 bên, trời đêm cực quang + trăng + núi tuyết parallax, tuyết rơi ambient,
    /// bão tuyết chu kỳ (SnowstormHazard), mặt băng trơn (IceSurface), death zone 4 phía, camera Confiner.
    /// </summary>
    internal static class StageMapBuilderIce
    {
        public const string SceneName = "Map_Ice";

        public static void Build()
        {
            var scene = StageSceneKit.NewScene();

            // ---- Nền parallax (xa → gần) ----
            StageSceneKit.MakeBackdrop("BG_Sky", StageArtIce.Sky, new Vector3(0f, 4f, 0f), -100, 0.92f, null, 1.5f);
            StageSceneKit.MakeBackdrop("BG_Mountains", StageArtIce.Mountains, new Vector3(0f, 1.6f, 0f), -90, 0.72f, null, 1.35f);
            StageSceneKit.MakeBackdrop("BG_Pines", StageArtIce.Pines, new Vector3(0f, 1.2f, 0f), -80, 0.5f, null, 1.3f);

            // ---- Sàn băng (Tilemap + Composite + IceSurface trượt) ----
            var iceMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(StageSceneKit.IceMaterialPath);
            var tileTop = StageSceneKit.MakeTileAsset(StageArtIce.TileTop, StageArtIce.Dir + "/Tile_IceTop.asset");
            var tileIn = StageSceneKit.MakeTileAsset(StageArtIce.TileInner, StageArtIce.Dir + "/Tile_IceInner.asset");
            var tm = StageSceneKit.MakeGroundTilemap(null, iceMat, -6);
            StageSceneKit.FillGround(tm, tileTop, tileIn, -14, 13, -1, 4);

            // Nhũ băng trang trí dưới đáy sàn.
            var icicle = StageSceneKit.LoadSprite(StageArtIce.Icicle);
            for (float x = -12.5f; x <= 12.5f; x += 2.6f)
                StageSceneKit.MakeSpriteGO("Deco_Icicle", icicle,
                    new Vector3(x + Random.Range(-0.5f, 0.5f), -4.45f, 0f), -6, null,
                    new Vector3(1f, Random.Range(0.7f, 1.3f), 1f));

            // ---- 2 platform bay 1 chiều (đối xứng) ----
            StageSceneKit.MakeOneWayPlatform("Platform_L", StageArtIce.Platform, new Vector2(-5.5f, 1.8f), null, -5);
            StageSceneKit.MakeOneWayPlatform("Platform_R", StageArtIce.Platform, new Vector2(5.5f, 1.8f), null, -5);

            // ---- Spawn points + Death zones ----
            var respawn = StageSceneKit.MakeSpawnPoints(null, new Vector2(-6f, 1.2f), new Vector2(6f, 1.2f), new Vector2(0f, 3.5f));
            StageSceneKit.MakeDeathZones(null, 24f, 20f, -14f, respawn);

            // ---- Tường băng VỠ ĐƯỢC 2 bên (BreakableWall, wallRecovers=true theo StageConfig_Ice) ----
            var iceCfg = AssetDatabase.LoadAssetAtPath<StageConfigSO>(StageSceneKit.DataFolder + "/StageConfig_Ice.asset");
            MakeBreakableWall("BreakableWall_L", new Vector2(-13.5f, 2.5f), -1f, iceCfg, respawn);
            MakeBreakableWall("BreakableWall_R", new Vector2(13.5f, 2.5f), 1f, iceCfg, respawn);

            // ---- Bão tuyết + hạt tuyết ----
            var hazardGo = new GameObject("SnowstormHazard");
            hazardGo.transform.position = new Vector3(0f, 4f, 0f);
            var area = hazardGo.AddComponent<BoxCollider2D>();
            area.isTrigger = true;
            area.size = new Vector2(36f, 18f);
            var stormPs = StageSceneKit.MakeParticles(hazardGo.transform, "PS_StormSnow", new Vector2(0f, 6f),
                new Vector2(40f, 14f), Color.white, new Color(0.82f, 0.89f, 1f),
                350f, 1.4f, new Vector2(-13f, -2.5f), 0.05f, 0.1f, 0f, false);
            var hazard = hazardGo.AddComponent<SnowstormHazard>();
            StageSceneKit.SetObj(hazard, "config", iceCfg);
            StageSceneKit.SetMask(hazard, "playerLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetObj(hazard, "stormArea", area);
            StageSceneKit.SetObj(hazard, "snowParticles", stormPs);

            StageSceneKit.MakeParticles(null, "PS_AmbientSnow", new Vector2(0f, 12f), new Vector2(44f, 2f),
                Color.white, new Color(0.81f, 0.88f, 1f), 40f, 10f, new Vector2(-0.4f, -1.2f), 0.05f, 0.12f, 0.015f);

            // ---- Ánh sáng URP 2D ----
            StageSceneKit.MakeGlobalLight(null, new Color(0.8f, 0.86f, 1f), 0.95f);
            StageSceneKit.MakePointLight(null, "Light_Moon", new Vector2(13f, 10.5f),
                new Color(0.91f, 0.91f, 0.78f), 0.7f, 9f);
            StageSceneKit.MakePointLight(null, "Light_Aurora", new Vector2(-8f, 11f),
                new Color(0.25f, 0.85f, 0.69f), 0.35f, 12f);

            // ---- Camera ----
            StageSceneKit.MakeCameraRig(null, new Rect(-17.5f, -6f, 35f, 18f), 6.5f,
                new Color32(7, 10, 30, 255));

            StageSceneKit.SaveScene(scene, SceneName);
            StageSceneKit.AddStageToRegistry("Đỉnh Vĩnh Đông (Băng)", SceneName,
                StageSceneKit.LoadSprite(StageArtIce.Platform));
            Debug.Log("[Phase11] Đã dựng xong Map_Ice.");
        }

        /// <summary>Tường băng vỡ được: sprite + collider Wall + child DeathZone (mở khi vỡ).</summary>
        private static void MakeBreakableWall(string name, Vector2 pos, float outwardSign,
                                              StageConfigSO cfg, Transform respawn)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.layer = GameLayers.Wall;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = StageSceneKit.LoadSprite(StageArtIce.Wall);
            sr.sortingOrder = -4;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1f, 5f);

            // Death zone sau tường — BreakableWall tự tắt lúc Awake, bật khi vỡ.
            var channel = AssetDatabase.LoadAssetAtPath<ScriptableObject>(StageSceneKit.PlayerDiedChannelPath);
            var dz = StageSceneKit.MakeDeathZone("DeathZoneObject", pos + new Vector2(outwardSign * 1.5f, 0f),
                new Vector2(1.4f, 5.6f), go.transform, channel, respawn);

            var wall = go.AddComponent<BreakableWall>();
            StageSceneKit.SetObj(wall, "config", cfg);
            StageSceneKit.SetMask(wall, "impactLayers", GameLayers.Player1Mask | GameLayers.Player2Mask);
            StageSceneKit.SetObj(wall, "wallCollider", col);
            StageSceneKit.SetObj(wall, "deathZoneObject", dz.gameObject);
        }
    }
}
