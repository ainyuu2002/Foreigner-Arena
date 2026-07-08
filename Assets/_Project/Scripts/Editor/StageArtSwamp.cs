using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Sinh art pixel map ĐẦM LẦY "Rừng Sương Độc" (design 02): trời đêm xanh độc + trăng mờ + sương,
    /// rừng cây silhouette + dây leo rủ, nước đầm phản chiếu, bờ đất cỏ + nấm phát sáng. PPU nền = 16, tile = 64.
    /// </summary>
    internal static class StageArtSwamp
    {
        public const string Dir = "Assets/_Project/Art/Maps/Swamp";
        public const string Sky = Dir + "/Swamp_Sky.png";
        public const string Trees = Dir + "/Swamp_Trees.png";
        public const string Vines = Dir + "/Swamp_Vines.png";
        public const string Water = Dir + "/Swamp_Water.png";
        public const string TileTop = Dir + "/Swamp_TileTop.png";
        public const string TileInner = Dir + "/Swamp_TileInner.png";
        public const string Platform = Dir + "/Swamp_Platform.png";
        public const string Mushroom = Dir + "/Swamp_Mushroom.png";
        public const string Cattail = Dir + "/Swamp_Cattail.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(202);
            GenSky();
            GenTrees();
            GenVines();
            GenWater();
            GenTiles();
            GenPlatform();
            GenDecor();
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("0a140c"));
            c.VerticalGradient(StagePxCanvas.Hex("16281a"), StagePxCanvas.Hex("070f09"), 0.05f);
            // Vài ngôi sao mờ xuyên tán rừng.
            c.Speckle(40, new Color32(159, 190, 160, 120), 150, 288);
            // Dải sương độc ngang trời.
            var fog1 = StagePxCanvas.Hex("2a4a34"); fog1.a = 60;
            var fog2 = StagePxCanvas.Hex("3a5e42"); fog2.a = 40;
            c.SineBand(120f, 6f, 260f, 0.2f, 18, fog1);
            c.SineBand(170f, 8f, 300f, 0.7f, 14, fog2);
            c.SineBand(80f, 5f, 220f, 0.45f, 12, fog1);
            // Trăng xanh nhạt + quầng.
            c.Disc(120, 226, 30, new Color32(216, 232, 192, 26));
            c.Disc(120, 226, 22, new Color32(216, 232, 192, 60));
            c.Disc(120, 226, 17, StagePxCanvas.Hex("d8e8c0"));
            c.Save(Sky, 16f);
        }

        private static void GenTrees()
        {
            var c = new StagePxCanvas(512, 176, new Color32(0, 0, 0, 0));
            var trunkFar = StagePxCanvas.Hex("101f14");
            var trunkNear = StagePxCanvas.Hex("0c1810");
            var canopy = StagePxCanvas.Hex("1e4028");
            var canopyHi = StagePxCanvas.Hex("2a5434");
            // Hàng thân cây chết mảnh (xa).
            for (int i = 0; i < 22; i++)
            {
                int x = StagePxCanvas.RandInt(0, 512);
                int h = StagePxCanvas.RandInt(50, 130);
                int w = StagePxCanvas.RandInt(2, 5);
                c.FillRect(x, 0, w, h, trunkFar);
                if (StagePxCanvas.Rand01() < 0.5f) // cành khẳng khiu
                    c.HLine(h - StagePxCanvas.RandInt(6, 20), x - StagePxCanvas.RandInt(4, 10), x, trunkFar);
            }
            // Cây cổ thụ tán tròn bên phải (theo design 02).
            c.FillRect(420, 0, 14, 120, trunkNear);
            c.HLine(88, 408, 424, trunkNear);
            c.HLine(70, 434, 452, trunkNear);
            for (int k = 0; k < 10; k++)
                c.Disc(430 + StagePxCanvas.RandInt(-34, 35), 128 + StagePxCanvas.RandInt(-16, 22),
                       StagePxCanvas.RandInt(14, 26), canopy);
            for (int k = 0; k < 6; k++)
                c.Disc(428 + StagePxCanvas.RandInt(-28, 29), 138 + StagePxCanvas.RandInt(-10, 16),
                       StagePxCanvas.RandInt(8, 14), canopyHi);
            // Cụm cây vừa bên trái.
            c.FillRect(52, 0, 9, 92, trunkNear);
            for (int k = 0; k < 6; k++)
                c.Disc(56 + StagePxCanvas.RandInt(-20, 21), 98 + StagePxCanvas.RandInt(-10, 14),
                       StagePxCanvas.RandInt(10, 18), canopy);
            // Sương mù thấp phủ gốc cây.
            var fog = StagePxCanvas.Hex("2a4a34"); fog.a = 80;
            c.BlendRect(0, 0, 512, 26, fog);
            c.Save(Trees, 16f);
        }

        private static void GenVines()
        {
            var c = new StagePxCanvas(512, 120, new Color32(0, 0, 0, 0));
            var vine = StagePxCanvas.Hex("2a5434");
            var leaf = StagePxCanvas.Hex("3a7444");
            for (int i = 0; i < 16; i++)
            {
                int x = StagePxCanvas.RandInt(8, 504);
                int len = StagePxCanvas.RandInt(30, 110);
                int sway = StagePxCanvas.RandInt(0, 3) - 1;
                for (int k = 0; k < len; k++)
                {
                    int xx = x + (k / 22) * sway;
                    c.Blend(xx, 119 - k, vine);
                    if (k % 9 == 4) { c.Blend(xx - 1, 119 - k, leaf); c.Blend(xx + 1, 119 - k - 1, leaf); }
                }
                if (StagePxCanvas.Rand01() < 0.4f) // đốm đom đóm đậu trên dây
                    c.Disc(x + sway * 2, 119 - len + 2, 1, StagePxCanvas.Hex("b8e878"));
            }
            c.Save(Vines, 16f);
        }

        private static void GenWater()
        {
            var c = new StagePxCanvas(512, 96, StagePxCanvas.Hex("0f2018"));
            c.VerticalGradient(StagePxCanvas.Hex("0a1710"), StagePxCanvas.Hex("15291d"), 0.05f);
            // Sóng phản chiếu ngang.
            for (int i = 0; i < 22; i++)
            {
                int y = StagePxCanvas.RandInt(6, 90);
                int x0 = StagePxCanvas.RandInt(0, 440);
                var stripe = StagePxCanvas.Hex("1e3828"); stripe.a = 200;
                c.HLine(y, x0, x0 + StagePxCanvas.RandInt(20, 70), stripe);
            }
            // Vệt phản chiếu trăng loang nhẹ.
            var moonRef = new Color32(216, 232, 192, 60);
            for (int i = 0; i < 8; i++)
                c.HLine(84 - i * 9, 104 + StagePxCanvas.RandInt(-8, 9), 136 + StagePxCanvas.RandInt(-8, 9), moonRef);
            // Mặt nước viền sáng.
            c.FillRect(0, 92, 512, 4, StagePxCanvas.Hex("2a4a34"));
            c.Save(Water, 16f);
        }

        private static void GenTiles()
        {
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a2a1e"));
            MudDetail(t);
            // Thảm cỏ trên mặt.
            t.FillRect(0, 58, 64, 6, StagePxCanvas.Hex("4a8a3a"));
            t.FillRect(0, 61, 64, 3, StagePxCanvas.Hex("57a844"));
            for (int x = 2; x < 64; x += StagePxCanvas.RandInt(4, 9)) // cọng cỏ nhô + rủ xuống
            {
                t.VLine(x, 58, 58 - StagePxCanvas.RandInt(2, 6), StagePxCanvas.Hex("4a8a3a"));
            }
            t.HLine(57, 0, 63, StagePxCanvas.Hex("24180f"));
            TileBorder(t, StagePxCanvas.Hex("1c130c"));
            t.Save(TileTop, 64f);

            var i2 = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2c1f15"));
            MudDetail(i2);
            TileBorder(i2, StagePxCanvas.Hex("18100a"));
            i2.Save(TileInner, 64f);
        }

        private static void TileBorder(StagePxCanvas c, Color32 border)
        {
            c.FillRect(0, 0, 2, 64, border);
            c.FillRect(62, 0, 2, 64, border);
            c.FillRect(0, 0, 64, 2, border);
        }

        private static void MudDetail(StagePxCanvas c)
        {
            var dark = StagePxCanvas.Hex("24180f");
            var root = StagePxCanvas.Hex("1c130c");
            for (int i = 0; i < 12; i++)
                c.BlendRect(StagePxCanvas.RandInt(0, 58), StagePxCanvas.RandInt(0, 54),
                            StagePxCanvas.RandInt(3, 8), StagePxCanvas.RandInt(2, 4), dark);
            for (int i = 0; i < 3; i++) // rễ cây len lỏi
            {
                int x = StagePxCanvas.RandInt(6, 56), y = StagePxCanvas.RandInt(8, 50);
                for (int k = 0; k < StagePxCanvas.RandInt(8, 16); k++)
                    c.Blend(x + k, y - (k % 3 == 0 ? 1 : 0), root);
            }
        }

        private static void GenPlatform()
        {
            var c = new StagePxCanvas(256, 80, new Color32(0, 0, 0, 0));
            // Bệ đất treo rêu: cỏ trên, đất giữa, rễ rủ dưới.
            c.FillRect(4, 30, 248, 28, StagePxCanvas.Hex("3a2a1e"));
            c.FillRect(0, 52, 256, 6, StagePxCanvas.Hex("4a8a3a"));
            c.FillRect(0, 56, 256, 4, StagePxCanvas.Hex("57a844"));
            c.HLine(51, 0, 255, StagePxCanvas.Hex("24180f"));
            var moss = StagePxCanvas.Hex("3a7444"); moss.a = 200;
            for (int i = 0; i < 14; i++)
                c.Disc(StagePxCanvas.RandInt(8, 248), StagePxCanvas.RandInt(32, 50), StagePxCanvas.RandInt(2, 4), moss);
            // Rễ + giọt rêu rủ xuống.
            for (int i = 0; i < 12; i++)
            {
                int x = StagePxCanvas.RandInt(10, 246);
                c.VLine(x, 30, 30 - StagePxCanvas.RandInt(6, 20), StagePxCanvas.Hex("2a5434"));
            }
            c.Save(Platform, 64f);
        }

        private static void GenDecor()
        {
            // Nấm phát sáng.
            var m = new StagePxCanvas(32, 32, new Color32(0, 0, 0, 0));
            m.Disc(16, 16, 10, new Color32(122, 216, 160, 40));       // quầng sáng
            m.FillRect(14, 6, 4, 10, StagePxCanvas.Hex("d8e8c0"));    // thân
            m.Disc(16, 17, 7, StagePxCanvas.Hex("7ad8a0"));           // mũ
            m.Disc(14, 19, 2, StagePxCanvas.Hex("aef0c8"));           // đốm mũ
            m.Save(Mushroom, 64f);

            // Cỏ nến (cattail).
            var ct = new StagePxCanvas(16, 48, new Color32(0, 0, 0, 0));
            ct.VLine(8, 0, 40, StagePxCanvas.Hex("2a5434"));
            ct.FillRect(6, 32, 5, 12, StagePxCanvas.Hex("5a4028"));
            ct.VLine(11, 20, 34, StagePxCanvas.Hex("2a5434"));
            ct.Save(Cattail, 64f);
        }
    }
}
