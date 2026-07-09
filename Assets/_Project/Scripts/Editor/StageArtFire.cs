using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Sinh art pixel map LỬA "Lò Dung Nham" (design 03): trời đỏ sẫm + khói + tàn lửa, núi lửa phun nền sau,
    /// biển dung nham sáng rực, platform bazan nứt phát sáng. PPU nền = 16, tile = 64.
    /// </summary>
    internal static class StageArtFire
    {
        public const string Dir = "Assets/_Project/Art/Maps/Fire";
        public const string Sky = Dir + "/Fire_Sky.png";
        public const string Volcanoes = Dir + "/Fire_Volcanoes.png";
        public const string Lava = Dir + "/Fire_Lava.png";
        public const string TileTop = Dir + "/Fire_TileTop.png";
        public const string TileTopB = Dir + "/Fire_TileTopB.png";
        public const string TileTopC = Dir + "/Fire_TileTopC.png";
        public const string TileHot = Dir + "/Fire_TileHot.png";
        public const string TileInner = Dir + "/Fire_TileInner.png";
        public const string TileInnerB = Dir + "/Fire_TileInnerB.png";
        public const string Platform = Dir + "/Fire_Platform.png";
        public const string WarnIcon = Dir + "/Fire_WarnIcon.png";
        public const string FloorFace = Dir + "/Fire_FloorFace.png";
        public const string RockSpire = Dir + "/Fire_RockSpire.png";
        public const string LavaShine = Dir + "/Fire_LavaShine.png";
        public const string BigVolcano = Dir + "/Fire_BigVolcano.png";
        public const string LavaField = Dir + "/Fire_LavaField.png";
        public const string Ceiling = Dir + "/Fire_Ceiling.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(303);
            GenSky();
            GenVolcanoes();
            GenLava();
            GenTiles();
            GenPlatform();
            GenWarnIcon();
            GenFloorFace();
            GenRockSpire();
            GenLavaShine();
            GenLavaField();
            GenCeiling();
        }

        /// <summary>TRẦN HANG — LỚP RIÊNG đặt đúng tầm camera (măng đá rủ lộ ngay khung hình mặc định).</summary>
        private static void GenCeiling()
        {
            StagePxCanvas.Reseed(414);
            var c = new StagePxCanvas(512, 120, new Color32(0, 0, 0, 0)); // 32×7.5 unit @16ppu
            var ceil = StagePxCanvas.Hex("0f0406");
            var ceilLite = StagePxCanvas.Hex("1f0b10");
            c.FillRect(0, 78, 512, 42, ceil); // dải trần đặc phía trên
            for (int x = 2; x < 512; x += StagePxCanvas.RandInt(20, 44))
            {
                int w = StagePxCanvas.RandInt(12, 32);
                int len = StagePxCanvas.RandInt(20, 66);
                c.TriangleDown(x, 78, w, len, ceil);
                c.TriangleDown(x - w / 5, 78, w / 2, len * 2 / 3, ceilLite);
            }
            // Viền lava hắt mỏng lên mép măng đá (bắt sáng từ biển lava dưới).
            for (int i = 0; i < 60; i++)
                c.Blend(StagePxCanvas.RandInt(0, 512), StagePxCanvas.RandInt(10, 76),
                        new Color32(190, 70, 20, 90));
            c.Save(Ceiling, 16f);
        }

        /// <summary>BIỂN LAVA XA toàn cảnh (Norfair): lava vằn sáng phủ chân trời + đảo đá đen + măng đá silhouette.</summary>
        private static void GenLavaField()
        {
            StagePxCanvas.Reseed(818);
            var c = new StagePxCanvas(512, 200, new Color32(0, 0, 0, 0));
            // Nửa dưới: biển lava sáng mờ dần vào tối phía trên (kéo CAO cho lộ rõ sau mép đảo).
            for (int y = 0; y < 122; y++)
            {
                float t = y / 122f;
                var col = Color32.Lerp(StagePxCanvas.Hex("ff9a22"), StagePxCanvas.Hex("4a1206"), t);
                for (int x = 0; x < 512; x++) c.Set(x, y, col);
            }
            // Vằn crust đá đen trôi trên biển lava (pattern Norfair đặc trưng).
            var crust = StagePxCanvas.Hex("1c0a0c");
            for (int i = 0; i < 46; i++)
            {
                int w = StagePxCanvas.RandInt(16, 60);
                int y = StagePxCanvas.RandInt(2, 88);
                c.BlendRect(StagePxCanvas.RandInt(0, 512 - 12), y, w, StagePxCanvas.RandInt(3, 8),
                            new Color32(28, 10, 12, (byte)(230 - y * 2)));
            }
            // Vệt sáng chói ngang.
            for (int i = 0; i < 20; i++)
                c.HLine(StagePxCanvas.RandInt(4, 80), StagePxCanvas.RandInt(0, 440),
                        StagePxCanvas.RandInt(20, 70) + 440, StagePxCanvas.Hex("ffdd55"));
            // Măng đá silhouette mọc từ biển lava, gốc có viền sáng phản chiếu.
            var spire = StagePxCanvas.Hex("170709");
            for (int i = 0; i < 11; i++)
            {
                int x = 20 + i * 46 + StagePxCanvas.RandInt(-12, 13);
                int h = StagePxCanvas.RandInt(70, 190);
                int w = StagePxCanvas.RandInt(14, 30);
                c.Triangle(x, 64 + StagePxCanvas.RandInt(0, 34), w, h, spire);
                c.HLine(66 + StagePxCanvas.RandInt(0, 30), x - w / 2, x + w / 2,
                        new Color32(255, 154, 34, 140)); // phản chiếu gốc
            }
            c.Save(LavaField, 16f);
        }

        /// <summary>NÚI LỬA KHỔNG LỒ — chủ thể hậu cảnh (đối xứng vai trò với núi băng bên map Ice).</summary>
        private static void GenBigVolcano()
        {
            StagePxCanvas.Reseed(999);
            var c = new StagePxCanvas(512, 320, new Color32(0, 0, 0, 0));
            var rock = StagePxCanvas.Hex("241016");
            var rockLite = StagePxCanvas.Hex("341b20");
            // Nón núi lớn lệch nhẹ, miệng bằng phẳng có khuyết.
            for (int x = 0; x < 512; x++)
            {
                float t = (x - 250f) / 260f;
                float h = 285f * (1f - t * t * 1.05f);
                int hh = Mathf.RoundToInt(Mathf.Max(0f, h)) + StagePxCanvas.RandInt(-2, 3);
                hh = Mathf.Min(hh, 268 + StagePxCanvas.RandInt(0, 8)); // cắt phẳng miệng núi
                for (int y = 0; y < hh && y < 320; y++)
                    c.Set(x, y, x < 250 - (hh - y) / 4 ? rockLite : rock);
            }
            // Miệng núi rực + cột khói + dòng lava chảy dọc sườn.
            var crater = StagePxCanvas.Hex("ff7a1e");
            var craterCore = StagePxCanvas.Hex("ffdd55");
            c.FillRect(196, 262, 110, 10, crater);
            c.FillRect(216, 264, 70, 6, craterCore);
            var plume = new Color32(26, 12, 14, 210);
            for (int k = 0; k < 16; k++)
                c.Disc(250 + StagePxCanvas.RandInt(-16, 17) + k, 276 + k * 3, StagePxCanvas.RandInt(8, 16), plume);
            var stream = StagePxCanvas.Hex("e8541c");
            var streamHot = StagePxCanvas.Hex("ffdd55");
            for (int s = 0; s < 4; s++)
            {
                int x = 210 + s * 28 + StagePxCanvas.RandInt(-6, 7);
                int len = StagePxCanvas.RandInt(120, 240);
                for (int k = 0; k < len; k++)
                {
                    int xx = x + (k / 24) * (s % 2 == 0 ? -1 : 1);
                    c.Blend(xx, 262 - k, stream);
                    if (k % 7 == 3) c.Blend(xx, 262 - k, streamHot);
                }
            }
            // Đốm dung nham lấm tấm trên sườn.
            c.Speckle(120, new Color32(255, 106, 34, 120), 60, 250);
            c.Save(BigVolcano, 16f);
        }

        /// <summary>Váng sáng mặt lava (lớp child dập dềnh bằng AmbientDrift → lava 'sống').</summary>
        private static void GenLavaShine()
        {
            StagePxCanvas.Reseed(77);
            var c = new StagePxCanvas(512, 20, new Color32(0, 0, 0, 0));
            for (int x = 0; x < 512; x++)
            {
                int h = 6 + StagePxCanvas.RandInt(0, 8);
                for (int y = 20 - h; y < 20; y++)
                {
                    float t = (float)(y - (20 - h)) / h;
                    var col = t > 0.6f ? StagePxCanvas.Hex("ffe98a") : StagePxCanvas.Hex("ffdd55");
                    col.a = (byte)(120 + t * 135);
                    c.Blend(x, y, col);
                }
            }
            c.Speckle(40, new Color32(255, 255, 200, 220), 8, 20);
            c.Save(LavaShine, 32f);
        }

        /// <summary>Mặt đảo bazan hữu cơ (kiểu Norfair): cột đá lởm chởm, nứt magma rực đáy, măng đá rủ xuống lava.</summary>
        private static void GenFloorFace()
        {
            StagePxCanvas.Reseed(505);
            var c = new StagePxCanvas(768, 152, new Color32(0, 0, 0, 0)); // 24×4.75 unit @32ppu
            var lite = StagePxCanvas.Hex("4a3040");
            var body = StagePxCanvas.Hex("332134");
            var dark = StagePxCanvas.Hex("221525");
            var glow = StagePxCanvas.Hex("ff6a22");
            var hot = StagePxCanvas.Hex("ffdd55");
            for (int x = 0; x < 768; x++)
            {
                float edge = 1f;
                if (x < 64) edge = Mathf.Sqrt(x / 64f);
                else if (x > 703) edge = Mathf.Sqrt((767 - x) / 64f);
                // Cột đá: chiều cao đáy nhấp nhô theo khối 16px (chất cột bazan).
                int block = (x / 16);
                StagePxCanvas.Reseed(505 + block);
                int jag = StagePxCanvas.RandInt(0, 26);
                int top = 140;
                int yBot = Mathf.RoundToInt((26 + jag) * edge) + 8;
                yBot = 140 - Mathf.RoundToInt((140 - yBot) * edge);
                for (int y = 152 - yBot > 0 ? yBot : 0; y < top; y++)
                {
                    float d = (float)(y - yBot) / Mathf.Max(1, top - yBot);
                    c.Set(x, y, d > 0.8f ? lite : d > 0.35f ? body : dark);
                }
                // Viền đáy rực magma (gần mặt lava).
                if (x % 3 != 0) c.Blend(x, yBot, new Color32(255, 106, 34, 150));
            }
            StagePxCanvas.Reseed(506);
            // Nứt magma phát sáng loang trên mặt đá.
            for (int i = 0; i < 18; i++)
            {
                int x = StagePxCanvas.RandInt(30, 720), y = StagePxCanvas.RandInt(30, 120);
                for (int k = 0; k < StagePxCanvas.RandInt(10, 26); k++)
                {
                    c.Blend(x + k, y - (k % 4 == 0 ? 1 : 0), glow);
                    if (k % 6 == 2) c.Blend(x + k, y, hot);
                }
            }
            // Măng đá rủ xuống lava.
            for (int i = 0; i < 20; i++)
            {
                int x = StagePxCanvas.RandInt(50, 720);
                c.TriangleDown(x, 20 + StagePxCanvas.RandInt(0, 14), StagePxCanvas.RandInt(8, 18),
                               StagePxCanvas.RandInt(14, 34), dark);
            }
            c.Save(FloorFace, 32f);
        }

        /// <summary>Tháp đá núi lửa trang trí (đặt sau mép đảo — chất Norfair).</summary>
        private static void GenRockSpire()
        {
            StagePxCanvas.Reseed(606);
            var c = new StagePxCanvas(80, 192, new Color32(0, 0, 0, 0)); // 2.5×6 unit @32ppu
            var body = StagePxCanvas.Hex("2a1a2c");
            var lite = StagePxCanvas.Hex("3d2838");
            for (int y = 0; y < 192; y++)
            {
                float t = y / 192f;
                int half = Mathf.RoundToInt(Mathf.Lerp(36f, 6f, Mathf.Pow(t, 0.7f))) + StagePxCanvas.RandInt(-2, 3);
                for (int x = 40 - half; x <= 40 + half; x++)
                    c.Set(x, y, x < 40 - half / 2 ? lite : body);
            }
            var glow = StagePxCanvas.Hex("ff6a22"); glow.a = 200;
            for (int i = 0; i < 6; i++)
            {
                int x = StagePxCanvas.RandInt(20, 58), y = StagePxCanvas.RandInt(10, 120);
                for (int k = 0; k < StagePxCanvas.RandInt(6, 14); k++) c.Blend(x + k / 2, y + k, glow);
            }
            c.Save(RockSpire, 32f);
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("160404"));
            // Gradient: chân trời rực cam → đỉnh trời đen kịt.
            // Nền TỐI hơn (độ đọc trận đấu: nhân vật đỏ phải nổi trên nền).
            c.VerticalGradient(StagePxCanvas.Hex("3f1305"), StagePxCanvas.Hex("0e0202"), 0.05f);
            var glow = StagePxCanvas.Hex("8a3208"); glow.a = 55;
            c.BlendRect(0, 0, 512, 40, glow);
            // Mây khói đen lơ lửng.
            var smoke = StagePxCanvas.Hex("1c0a0c"); smoke.a = 160;
            for (int i = 0; i < 14; i++)
            {
                int cx = StagePxCanvas.RandInt(0, 512);
                int cy = StagePxCanvas.RandInt(160, 280);
                for (int k = 0; k < 4; k++)
                    c.Disc(cx + StagePxCanvas.RandInt(-26, 26), cy + StagePxCanvas.RandInt(-6, 6),
                           StagePxCanvas.RandInt(10, 24), smoke);
            }
            // Tàn lửa bay lơ lửng (đốm tĩnh; đốm động do ParticleSystem lo).
            c.Speckle(32, StagePxCanvas.Hex("ff9a22"), 20, 240);
            c.Speckle(14, StagePxCanvas.Hex("ffdd55"), 20, 180, 1);
            // TRẦN HANG ĐỘNG (tham chiếu Norfair): KHÔNG có trời — dải đá đen + măng đá rủ xuống,
            // thay cho trăng máu. Lava bên dưới là nguồn sáng duy nhất.
            var ceil = StagePxCanvas.Hex("0f0406");
            var ceilLite = StagePxCanvas.Hex("1d0a0e");
            c.FillRect(0, 262, 512, 26, ceil);
            StagePxCanvas.Reseed(313);
            for (int x = 4; x < 512; x += StagePxCanvas.RandInt(24, 50))
            {
                int w = StagePxCanvas.RandInt(14, 34);
                int len = StagePxCanvas.RandInt(24, 80);
                c.TriangleDown(x, 262, w, len, ceil);
                c.TriangleDown(x - w / 4, 262, w / 2, len * 2 / 3, ceilLite);
            }
            c.Save(Sky, 16f);
        }

        private static void GenVolcanoes()
        {
            var c = new StagePxCanvas(512, 176, new Color32(0, 0, 0, 0));
            var rock = StagePxCanvas.Hex("2a0f0f");
            var rockFar = StagePxCanvas.Hex("381412");
            // 3 nón núi lửa: xa 2 bên + 1 giữa cao đang phun.
            c.Triangle(96, 0, 220, 96, rockFar);
            c.Triangle(430, 0, 240, 110, rockFar);
            c.Triangle(256, 0, 260, 150, rock);
            // Miệng phun sáng + vệt dung nham chảy dọc sườn núi giữa.
            var crater = StagePxCanvas.Hex("ff7a1e");
            var craterCore = StagePxCanvas.Hex("ffdd55");
            c.Disc(256, 148, 10, crater);
            c.Disc(256, 150, 5, craterCore);
            for (int i = 0; i < 3; i++)
            {
                int x = 250 + i * 6;
                var lavaStreak = StagePxCanvas.Hex("e8541c"); lavaStreak.a = 220;
                for (int y = 148; y > 148 - StagePxCanvas.RandInt(40, 90); y--)
                    c.Blend(x + ((148 - y) / 18), y, lavaStreak);
            }
            // Cột khói phun lên từ miệng.
            var plume = StagePxCanvas.Hex("241012"); plume.a = 200;
            for (int k = 0; k < 8; k++)
                c.Disc(256 + StagePxCanvas.RandInt(-8, 9), 156 + k * 3, StagePxCanvas.RandInt(5, 10), plume);
            // Đốm sáng lửa quanh miệng các núi xa.
            c.Disc(96, 94, 4, crater);
            c.Disc(430, 108, 5, crater);
            c.Save(Volcanoes, 16f);
        }

        private static void GenLava()
        {
            var c = new StagePxCanvas(512, 128, StagePxCanvas.Hex("e8541c"));
            c.VerticalGradient(StagePxCanvas.Hex("b03410"), StagePxCanvas.Hex("ff9a22"), 0.06f);
            // Váng đá đen trôi trên mặt.
            var crust = StagePxCanvas.Hex("7a2408"); crust.a = 230;
            for (int i = 0; i < 26; i++)
            {
                int w = StagePxCanvas.RandInt(14, 44);
                c.BlendRect(StagePxCanvas.RandInt(0, 512 - 10), StagePxCanvas.RandInt(4, 116), w, StagePxCanvas.RandInt(3, 7), crust);
            }
            // Vệt sáng cháy chói ngang mặt (đường nứt phát sáng).
            for (int i = 0; i < 16; i++)
            {
                int y = StagePxCanvas.RandInt(8, 124);
                int x0 = StagePxCanvas.RandInt(0, 460);
                c.HLine(y, x0, x0 + StagePxCanvas.RandInt(12, 50), StagePxCanvas.Hex("ffdd55"));
            }
            // Mặt trên cùng sáng nhất (viền tiếp xúc).
            c.FillRect(0, 124, 512, 4, StagePxCanvas.Hex("ffe98a"));
            c.Save(Lava, 16f);
        }

        private static void GenTiles()
        {
            // FEEDBACK TEST: sprite đất KHÔNG lặp lại (3 variant mặt + 2 variant lòng, không viền khối)
            // + Ô ĐẤT DUNG NHAM nóng rực (đứng lên mất %) cho đặc sắc.
            GenBasaltTop(TileTop, 31, 2);
            GenBasaltTop(TileTopB, 67, 1);
            GenBasaltTop(TileTopC, 143, 3);

            // Ô DUNG NHAM NÓNG: nứt toác sáng rực — dùng cho dải giữa đảo + LavaFloorZone gây %.
            StagePxCanvas.Reseed(55);
            var hcv = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a1410"));
            hcv.VerticalGradient(StagePxCanvas.Hex("55160c"), StagePxCanvas.Hex("2c1210"), 0.05f);
            var mag = StagePxCanvas.Hex("ff6a22");
            var hot = StagePxCanvas.Hex("ffdd55");
            for (int i = 0; i < 6; i++) // mạng nứt magma lớn
            {
                int x = StagePxCanvas.RandInt(2, 30), y = StagePxCanvas.RandInt(6, 58);
                for (int k = 0; k < StagePxCanvas.RandInt(18, 32); k++)
                {
                    hcv.Blend(x + k, y + (k % 5 == 0 ? StagePxCanvas.RandInt(-2, 3) : 0), mag);
                    hcv.Blend(x + k, y + 1, new Color32(255, 106, 34, 130));
                    if (k % 4 == 1) hcv.Blend(x + k, y, hot);
                }
            }
            hcv.FillRect(0, 60, 64, 4, StagePxCanvas.Hex("7a2408")); // mặt trên rám đỏ
            hcv.Speckle(24, hot, 4, 60);
            hcv.Save(TileHot, 64f);

            GenBasaltInner(TileInner, 83);
            GenBasaltInner(TileInnerB, 129);
        }

        private static void GenBasaltTop(string path, int seed, int glowStreaks)
        {
            StagePxCanvas.Reseed(seed);
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2c1c28"));
            t.FillRect(0, 58, 64, 6, StagePxCanvas.Hex("4a3040"));   // mép trên sáng
            BasaltSpecks(t, glowStreaks);
            t.Save(path, 64f);
        }

        private static void GenBasaltInner(string path, int seed)
        {
            StagePxCanvas.Reseed(seed);
            var c = new StagePxCanvas(64, 64, StagePxCanvas.Hex("201420"));
            BasaltSpecks(c, 1);
            c.Save(path, 64f);
        }

        private static void BasaltSpecks(StagePxCanvas c, int glowStreaks)
        {
            var darkSpec = StagePxCanvas.Hex("120a10");
            var stone = StagePxCanvas.Hex("3d2836");
            for (int i = 0; i < 9; i++)
                c.BlendRect(StagePxCanvas.RandInt(0, 58), StagePxCanvas.RandInt(0, 54),
                            StagePxCanvas.RandInt(3, 8), StagePxCanvas.RandInt(2, 5), stone);
            for (int i = 0; i < 8; i++)
                c.BlendRect(StagePxCanvas.RandInt(0, 60), StagePxCanvas.RandInt(0, 56),
                            StagePxCanvas.RandInt(2, 5), StagePxCanvas.RandInt(2, 4), darkSpec);
            var glow = StagePxCanvas.Hex("ff6a22"); glow.a = 210;
            var glowHot = StagePxCanvas.Hex("ffdd55"); glowHot.a = 190;
            for (int i = 0; i < glowStreaks; i++)
            {
                int x = StagePxCanvas.RandInt(4, 40), y = StagePxCanvas.RandInt(6, 48);
                for (int k = 0; k < StagePxCanvas.RandInt(10, 20); k++)
                {
                    c.Blend(x + k, y + (k % 4 == 0 ? 1 : 0), glow);
                    if (k % 5 == 2) c.Blend(x + k, y + 1, glowHot);
                }
            }
        }

        private static void BasaltDetail(StagePxCanvas c, bool top)
        {
            var edge = StagePxCanvas.Hex("4a3040");
            var darkSpec = StagePxCanvas.Hex("120a10");
            if (top)
            {
                c.FillRect(0, 58, 64, 6, edge);                      // mép trên sáng hơn
                c.HLine(57, 0, 63, StagePxCanvas.Hex("120a10"));
            }
            for (int i = 0; i < 10; i++)
                c.BlendRect(StagePxCanvas.RandInt(0, 60), StagePxCanvas.RandInt(0, 56),
                            StagePxCanvas.RandInt(2, 6), StagePxCanvas.RandInt(2, 4), darkSpec);
            // Vết nứt phát sáng dung nham.
            var glow = StagePxCanvas.Hex("ff6a22"); glow.a = 220;
            var glowHot = StagePxCanvas.Hex("ffdd55"); glowHot.a = 200;
            for (int i = 0; i < (top ? 2 : 1); i++)
            {
                int x = StagePxCanvas.RandInt(4, 40), y = StagePxCanvas.RandInt(6, 48);
                for (int k = 0; k < StagePxCanvas.RandInt(10, 20); k++)
                {
                    c.Blend(x + k, y + (k % 4 == 0 ? 1 : 0), glow);
                    if (k % 5 == 2) c.Blend(x + k, y + 1, glowHot);
                }
            }
        }

        private static void GenPlatform()
        {
            var c = new StagePxCanvas(256, 80, new Color32(0, 0, 0, 0));
            // Slab bazan: mặt trên sáng, thân tối, nứt phát sáng, gai đá dưới đáy.
            c.FillRect(0, 56, 256, 10, StagePxCanvas.Hex("4a3040"));
            c.FillRect(4, 28, 248, 28, StagePxCanvas.Hex("241722"));
            c.HLine(55, 0, 255, StagePxCanvas.Hex("120a10"));
            // RIM LIGHT mép trên (độ đọc: platform nổi khỏi nền tối, nhân vật đứng lên có đường chân trời rõ).
            c.FillRect(0, 63, 256, 3, StagePxCanvas.Hex("6f4658"));
            c.HLine(65, 0, 255, StagePxCanvas.Hex("96607a"));
            var glow = StagePxCanvas.Hex("ff6a22");
            for (int i = 0; i < 6; i++)
            {
                int x = StagePxCanvas.RandInt(10, 220), y = StagePxCanvas.RandInt(32, 52);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 18); k++)
                    c.Blend(x + k, y + (k % 4 == 0 ? 1 : 0), glow);
            }
            int[] xs = { 34, 86, 134, 186, 228 };
            foreach (int x in xs)
                c.TriangleDown(x, 28, StagePxCanvas.RandInt(10, 16), StagePxCanvas.RandInt(12, 22), StagePxCanvas.Hex("1a0f14"));
            // Đốm than hồng viền mặt trên.
            c.Speckle(30, new Color32(255, 106, 34, 180), 56, 66);
            c.Save(Platform, 64f);
        }

        private static void GenWarnIcon()
        {
            var c = new StagePxCanvas(32, 32, new Color32(0, 0, 0, 0));
            c.Triangle(16, 2, 28, 28, StagePxCanvas.Hex("ffdd55"));
            c.Triangle(16, 5, 20, 22, StagePxCanvas.Hex("e8a020"));
            // Dấu chấm than.
            c.FillRect(15, 12, 3, 10, StagePxCanvas.Hex("2a0f0f"));
            c.FillRect(15, 7, 3, 3, StagePxCanvas.Hex("2a0f0f"));
            c.Save(WarnIcon, 32f);
        }
    }
}
