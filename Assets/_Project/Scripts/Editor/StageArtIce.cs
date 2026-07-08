using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// Sinh art pixel map BĂNG/TUYẾT "Đỉnh Vĩnh Đông" (design 01): trời đêm + trăng + cực quang + núi tuyết
    /// parallax, tile băng phủ tuyết, platform bay có nhũ băng, tường băng vỡ được. PPU nền = 16, tile = 64.
    /// </summary>
    internal static class StageArtIce
    {
        public const string Dir = "Assets/_Project/Art/Maps/Ice";
        public const string Sky = Dir + "/Ice_Sky.png";
        public const string Mountains = Dir + "/Ice_Mountains.png";
        public const string Pines = Dir + "/Ice_Pines.png";
        public const string TileTop = Dir + "/Ice_TileTop.png";
        public const string TileInner = Dir + "/Ice_TileInner.png";
        public const string Platform = Dir + "/Ice_Platform.png";
        public const string Wall = Dir + "/Ice_Wall.png";
        public const string Icicle = Dir + "/Ice_Icicle.png";

        public static void Generate()
        {
            StagePxCanvas.Reseed(101);
            GenSky();
            GenMountains();
            GenPines();
            GenTiles();
            GenPlatform();
            GenWall();
            GenIcicle();
        }

        private static void GenSky()
        {
            var c = new StagePxCanvas(512, 288, StagePxCanvas.Hex("0a0e2a"));
            c.VerticalGradient(StagePxCanvas.Hex("1a2448"), StagePxCanvas.Hex("070a1e"), 0.05f);
            // Sao 3 tầng sáng.
            c.Speckle(90, StagePxCanvas.Hex("9fb4de"), 60, 288);
            c.Speckle(50, StagePxCanvas.Hex("dbe6ff"), 90, 288);
            c.Speckle(18, Color.white, 140, 288, 2);
            // Cực quang: 3 dải sine mềm.
            var aur1 = StagePxCanvas.Hex("3fd8b0"); aur1.a = 70;
            var aur2 = StagePxCanvas.Hex("57d868"); aur2.a = 55;
            var aur3 = StagePxCanvas.Hex("58c8e8"); aur3.a = 45;
            c.SineBand(226f, 12f, 210f, 0.10f, 16, aur1);
            c.SineBand(206f, 10f, 168f, 0.55f, 12, aur2);
            c.SineBand(244f, 8f, 250f, 0.30f, 9, aur3);
            // Trăng + quầng sáng.
            var halo = StagePxCanvas.Hex("e8e8c8"); halo.a = 26;
            c.Disc(430, 234, 34, halo);
            c.Disc(430, 234, 27, new Color32(232, 232, 200, 60));
            c.Disc(430, 234, 21, StagePxCanvas.Hex("efedd2"));
            c.Disc(422, 240, 4, StagePxCanvas.Hex("d8d6b4")); // crater
            c.Disc(437, 228, 3, StagePxCanvas.Hex("d8d6b4"));
            c.Save(Sky, 16f);
        }

        private static void GenMountains()
        {
            var c = new StagePxCanvas(512, 160, new Color32(0, 0, 0, 0));
            // Dãy xa (sáng hơn, thấp) rồi dãy gần (đậm, cao) — tương phản mạnh với trời.
            c.MountainRidge(0, 46, 92, 7, StagePxCanvas.Hex("3b4584"));
            // Chóp tuyết dãy xa.
            c.Speckle(300, new Color32(210, 224, 244, 130), 70, 130);
            c.MountainRidge(0, 24, 66, 9, StagePxCanvas.Hex("272f63"));
            c.Speckle(220, new Color32(196, 212, 238, 100), 34, 80);
            c.Save(Mountains, 16f);
        }

        private static void GenPines()
        {
            var c = new StagePxCanvas(512, 96, new Color32(0, 0, 0, 0));
            var dark = StagePxCanvas.Hex("0c1428");
            var dark2 = StagePxCanvas.Hex("111a33");
            // Nền đồi thấp.
            c.MountainRidge(0, 6, 18, 12, dark2);
            // Hàng thông silhouette cao thấp xen kẽ.
            for (int x = 6; x < 512; x += StagePxCanvas.RandInt(18, 34))
            {
                int h = StagePxCanvas.RandInt(26, 58);
                int baseY = StagePxCanvas.RandInt(0, 10);
                int tiers = 3;
                for (int t = 0; t < tiers; t++)
                {
                    int ty = baseY + (h / tiers) * t;
                    int bw = Mathf.RoundToInt((h * 0.55f) * (1f - t * 0.28f));
                    c.Triangle(x, ty, bw, h - (h / tiers) * t, dark);
                }
                c.FillRect(x - 1, baseY, 3, 4, dark); // gốc
            }
            c.Save(Pines, 16f);
        }

        private static void GenTiles()
        {
            // Tile mặt trên: tuyết phủ dày + thân băng đậm + viền khối rõ (đọc thành block như mockup).
            var t = new StagePxCanvas(64, 64, StagePxCanvas.Hex("3a7cb0"));
            IceBody(t, 0, 48);
            t.FillRect(0, 50, 64, 14, StagePxCanvas.Hex("eef6ff"));              // lớp tuyết dày
            t.FillRect(0, 47, 64, 3, StagePxCanvas.Hex("bcdcf2"));               // mép tuyết
            for (int i = 0; i < 7; i++)                                           // mô tuyết lùm phùm
                t.Disc(StagePxCanvas.RandInt(4, 60), 62, StagePxCanvas.RandInt(2, 5), StagePxCanvas.Hex("ffffff"));
            t.HLine(46, 0, 63, StagePxCanvas.Hex("1e4668"));
            TileBorder(t, StagePxCanvas.Hex("245a86"));
            t.Save(TileTop, 64f);

            // Tile trong lòng đất: băng đậm hẳn + viền khối.
            var i2 = new StagePxCanvas(64, 64, StagePxCanvas.Hex("2a5f8f"));
            IceBody(i2, 0, 64);
            TileBorder(i2, StagePxCanvas.Hex("1c4a72"));
            i2.Save(TileInner, 64f);
        }

        /// <summary>Viền 2px trái/phải + đáy để mỗi tile đọc thành khối riêng (chất pixel block).</summary>
        private static void TileBorder(StagePxCanvas c, Color32 border)
        {
            c.FillRect(0, 0, 2, 64, border);
            c.FillRect(62, 0, 2, 64, border);
            c.FillRect(0, 0, 64, 2, border);
        }

        private static void IceBody(StagePxCanvas c, int y0, int h)
        {
            // Vệt highlight chéo + đốm tối cho chất băng.
            var hi = StagePxCanvas.Hex("7ec5e8"); hi.a = 150;
            var lo = StagePxCanvas.Hex("2b5f8e"); lo.a = 170;
            for (int i = 0; i < 5; i++)
            {
                int x = StagePxCanvas.RandInt(2, 60);
                int y = y0 + StagePxCanvas.RandInt(2, Mathf.Max(3, h - 12));
                for (int k = 0; k < StagePxCanvas.RandInt(4, 9); k++) c.Blend(x + k, y + k, hi);
            }
            for (int i = 0; i < 7; i++)
                c.Blend(StagePxCanvas.RandInt(0, 64), y0 + StagePxCanvas.RandInt(0, h), lo);
        }

        private static void GenPlatform()
        {
            var c = new StagePxCanvas(256, 96, new Color32(0, 0, 0, 0));
            // Thân slab băng dày (y 34..76), tuyết phủ (76..92) — viền tối cho khối rõ.
            c.FillRect(4, 34, 248, 42, StagePxCanvas.Hex("3a7cb0"));
            c.FillRect(0, 70, 256, 6, StagePxCanvas.Hex("bcdcf2"));
            c.FillRect(0, 76, 256, 14, StagePxCanvas.Hex("eef6ff"));
            for (int i = 0; i < 10; i++)
                c.Disc(StagePxCanvas.RandInt(8, 248), 89, StagePxCanvas.RandInt(2, 5), Color.white);
            var hi = StagePxCanvas.Hex("7ec5e8"); hi.a = 180;
            for (int i = 0; i < 12; i++)
            {
                int x = StagePxCanvas.RandInt(8, 240), y = StagePxCanvas.RandInt(38, 64);
                for (int k = 0; k < StagePxCanvas.RandInt(5, 10); k++) c.Blend(x + k, y + k, hi);
            }
            c.HLine(34, 4, 251, StagePxCanvas.Hex("1e4668"));
            c.FillRect(4, 34, 3, 42, StagePxCanvas.Hex("245a86"));
            c.FillRect(249, 34, 3, 42, StagePxCanvas.Hex("245a86"));
            // Nhũ băng dưới đáy.
            int[] xs = { 30, 78, 128, 176, 224 };
            foreach (int x in xs)
                c.TriangleDown(x, 40, StagePxCanvas.RandInt(10, 16), StagePxCanvas.RandInt(18, 34), StagePxCanvas.Hex("3d7fb0"));
            c.Save(Platform, 64f);
        }

        private static void GenWall()
        {
            var c = new StagePxCanvas(64, 320, StagePxCanvas.Hex("3a7cb0"));
            WallDetail(c);
            c.Save(Wall, 64f);
        }

        private static void WallDetail(StagePxCanvas c)
        {
            // Cột băng: viền tối, highlight dọc, vết nứt ngang.
            c.FillRect(0, 0, 4, 320, StagePxCanvas.Hex("2b5f8e"));
            c.FillRect(60, 0, 4, 320, StagePxCanvas.Hex("2b5f8e"));
            c.FillRect(8, 0, 6, 320, new Color32(126, 197, 232, 150));
            // Chỏm tuyết trên đầu.
            c.FillRect(0, 308, 64, 12, StagePxCanvas.Hex("eef6ff"));
            for (int i = 0; i < 7; i++)
            {
                int y = StagePxCanvas.RandInt(20, 290);
                int x0 = StagePxCanvas.RandInt(6, 20);
                var crack = StagePxCanvas.Hex("1e4668"); crack.a = 190;
                for (int k = 0; k < StagePxCanvas.RandInt(10, 26); k++)
                    c.Blend(x0 + k, y + (k % 5 == 0 ? 1 : 0), crack);
            }
        }

        private static void GenIcicle()
        {
            var c = new StagePxCanvas(32, 64, new Color32(0, 0, 0, 0));
            c.TriangleDown(16, 63, 22, 60, StagePxCanvas.Hex("3d7fb0"));
            c.TriangleDown(13, 63, 8, 34, new Color32(126, 197, 232, 200));
            c.Save(Icicle, 64f);
        }
    }
}
