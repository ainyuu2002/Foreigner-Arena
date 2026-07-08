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
        public const string TileInner = Dir + "/Fire_TileInner.png";
        public const string Platform = Dir + "/Fire_Platform.png";
        public const string WarnIcon = Dir + "/Fire_WarnIcon.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(303);
            GenSky();
            GenVolcanoes();
            GenLava();
            GenTiles();
            GenPlatform();
            GenWarnIcon();
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("160404"));
            // Gradient: chân trời rực cam → đỉnh trời đen kịt.
            c.VerticalGradient(StagePxCanvas.Hex("5a1a05"), StagePxCanvas.Hex("120303"), 0.05f);
            // Quầng nóng sát chân trời.
            var glow = StagePxCanvas.Hex("8a3208"); glow.a = 90;
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
            c.Speckle(60, StagePxCanvas.Hex("ff9a22"), 20, 240);
            c.Speckle(25, StagePxCanvas.Hex("ffdd55"), 20, 180, 1);
            // Mặt trăng máu mờ sau khói.
            c.Disc(90, 236, 22, new Color32(90, 26, 10, 140));
            c.Disc(90, 236, 17, new Color32(140, 44, 16, 170));
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
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2c1c28"));
            BasaltDetail(t, true);
            TileBorder(t, StagePxCanvas.Hex("140b12"));
            t.Save(TileTop, 64f);

            var i2 = new StagePxCanvas(64, 64, StagePxCanvas.Hex("201420"));
            BasaltDetail(i2, false);
            TileBorder(i2, StagePxCanvas.Hex("120a10"));
            i2.Save(TileInner, 64f);
        }

        private static void TileBorder(StagePxCanvas c, Color32 border)
        {
            c.FillRect(0, 0, 2, 64, border);
            c.FillRect(62, 0, 2, 64, border);
            c.FillRect(0, 0, 64, 2, border);
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
