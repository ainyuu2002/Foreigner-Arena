using System.IO;
using UnityEditor;
using UnityEngine;

namespace FA.EditorTools
{
    /// <summary>
    /// SINH BGM PROCEDURAL cho 3 map Phase 11 (yêu cầu leader: "mỗi map cần có 1 BGM riêng") —
    /// không cần asset ngoài, cùng triết lý StagePxCanvas (art sinh bằng code). Xuất WAV mono 22050Hz
    /// 16-bit vào <c>Assets/_Project/Audio/Resources/</c> theo convention <c>BGM_&lt;tên scene&gt;</c>
    /// → UIAudioManager TỰ PHÁT khi load scene (không cần sửa scene/builder, ăn sẵn slider BGM).
    /// LOOP LIỀN MẠCH: mọi nốt render kiểu WRAP-AROUND (đuôi nốt tràn qua cuối buffer thì cộng vòng
    /// về đầu) → không khựng tại điểm nối.
    /// Chạy: menu FA/Stage/0. Generate BGM (3 map).
    /// </summary>
    internal static class StageBgmGen
    {
        private const string OutDir = "Assets/_Project/Audio/Resources";
        private const int Rate = 22050;

        [MenuItem("FA/Stage/0. Generate BGM (3 map)")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutDir);
            GenIce();
            GenSwamp();
            GenFire();
            AssetDatabase.Refresh();
            Debug.Log("[Phase11] Đã sinh 3 BGM: BGM_Map_Ice / BGM_Map_Swamp / BGM_Map_Fire " +
                      "(Resources — UIAudioManager tự phát theo tên scene).");
        }

        // ---------- MAP BĂNG: pad Am mộng mị + chuông băng + gió hú (32s, chậm rãi lạnh lẽo) ----------
        private static void GenIce()
        {
            var rng = new System.Random(101);
            var buf = new float[Rate * 32];

            // Vòng hợp âm 4×8s: Am → F → C → G (pad mềm, attack chậm như sương).
            float[][] chords =
            {
                new[] { 110.00f, 164.81f, 220.00f, 261.63f }, // Am: A2 E3 A3 C4
                new[] { 87.31f, 130.81f, 174.61f, 220.00f },  // F:  F2 C3 F3 A3
                new[] { 130.81f, 196.00f, 261.63f, 329.63f }, // C:  C3 G3 C4 E4
                new[] { 98.00f, 146.83f, 196.00f, 246.94f },  // G:  G2 D3 G3 B3
            };
            for (int c = 0; c < 4; c++)
                foreach (float f in chords[c])
                    AddNote(buf, c * 8f, f, 8.2f, 0.055f, 2.4f, 3.5f, harmonics: 2);

            // Chuông băng: mẫu 16 nốt × 2s (pentatonic Am) — nốt 0 = lặng để loop có "hơi thở".
            float[] bell = { 440f, 0f, 523.25f, 659.26f, 0f, 493.88f, 440f, 0f,
                             587.33f, 0f, 523.25f, 392f, 0f, 659.26f, 0f, 493.88f };
            for (int i = 0; i < 16; i++)
                if (bell[i] > 0f)
                    AddNote(buf, i * 2f + 0.9f, bell[i], 2.2f, 0.075f, 0.01f, 1.9f, harmonics: 3, detune: 1.003f);

            // Gió hú: 2 đợt noise swell (lowpass random-walk) — khớp cảm giác bão tuyết của map.
            AddWind(buf, rng, 2f, 7f, 0.045f);
            AddWind(buf, rng, 18f, 8f, 0.055f);

            WriteWav(OutDir + "/BGM_Map_Ice.wav", buf);
        }

        // ---------- MAP ĐẦM LẦY: drone Dm + mộc cầm huyền bí + dế đêm (32s, tĩnh mịch rừng độc) ----------
        private static void GenSwamp()
        {
            var rng = new System.Random(202);
            var buf = new float[Rate * 32];

            // Drone nền D2+A2 suốt bài (đầm lầy nặng, ẩm).
            AddNote(buf, 0f, 73.42f, 32f, 0.05f, 3f, 3f, harmonics: 2);
            AddNote(buf, 0f, 110.00f, 32f, 0.035f, 4f, 4f, harmonics: 1);
            // Pad Dm dâng giữa bài.
            foreach (float f in new[] { 146.83f, 220.00f, 293.66f, 349.23f }) // D3 A3 D4 F4
                AddNote(buf, 12f, f, 9f, 0.03f, 3.2f, 4f, harmonics: 1);

            // Mộc cầm (kalimba) D dorian: mẫu 20 bước × 1.6s, thưa — nhiều bước lặng.
            float[] pluck = { 293.66f, 0f, 349.23f, 0f, 392f, 0f, 0f, 440f, 0f, 293.66f,
                              0f, 523.25f, 0f, 0f, 392f, 0f, 349.23f, 0f, 0f, 440f };
            for (int i = 0; i < 20; i++)
                if (pluck[i] > 0f)
                    AddNote(buf, i * 1.6f + 0.4f, pluck[i], 1.5f, 0.085f, 0.005f, 1.3f, harmonics: 2);

            // Dế đêm: blip cao li ti rải deterministic (không phải nhạc — là KHÔNG KHÍ).
            for (int i = 0; i < 46; i++)
            {
                float t = (float)(rng.NextDouble() * 32.0);
                float f = 3400f + (float)rng.NextDouble() * 900f;
                AddNote(buf, t, f, 0.05f, 0.016f, 0.004f, 0.04f, harmonics: 1);
            }

            WriteWav(OutDir + "/BGM_Map_Swamp.wav", buf);
        }

        // ---------- MAP LỬA: bass dồn dập E phrygian + trống trầm + rền hang (24s, căng thẳng) ----------
        private static void GenFire()
        {
            var rng = new System.Random(303);
            var buf = new float[Rate * 24];

            // Drone E1 rền hang động suốt bài.
            AddNote(buf, 0f, 41.20f, 24f, 0.06f, 2f, 2f, harmonics: 3);

            // Bass pulse 100bpm (nốt 1/8 = 0.3s): E2, chen F2 (bậc II phrygian) + D2 — 80 bước.
            float[] bassPat = { 82.41f, 82.41f, 82.41f, 87.31f, 82.41f, 82.41f, 73.42f, 82.41f };
            for (int i = 0; i < 80; i++)
            {
                float f = bassPat[i % 8];
                AddNote(buf, i * 0.3f, f, 0.24f, 0.11f, 0.004f, 0.16f, harmonics: 3);
            }

            // Trống trầm (timpani 55Hz tụt cao độ) mỗi 2.4s + stab quãng 2 thứ B3→C4 mỗi 4.8s.
            for (float t = 0f; t < 24f; t += 2.4f)
                AddDrum(buf, t, 0.16f);
            for (float t = 1.2f; t < 24f; t += 4.8f)
            {
                AddNote(buf, t, 246.94f, 0.5f, 0.06f, 0.01f, 0.4f, harmonics: 2);
                AddNote(buf, t + 0.3f, 261.63f, 0.9f, 0.06f, 0.01f, 0.7f, harmonics: 2);
            }

            // Lửa tí tách: crackle blip ngắn rải ngẫu nhiên (deterministic).
            for (int i = 0; i < 60; i++)
            {
                float t = (float)(rng.NextDouble() * 24.0);
                AddNote(buf, t, 900f + (float)rng.NextDouble() * 1600f, 0.03f, 0.02f, 0.002f, 0.025f, harmonics: 1);
            }
            AddWind(buf, rng, 0f, 24f, 0.02f); // rền gió nóng rất khẽ nền sau.

            WriteWav(OutDir + "/BGM_Map_Fire.wav", buf);
        }

        // ==================== BỘ TỔNG HỢP ÂM (wrap-around → loop liền mạch) ====================

        /// <summary>Nốt sine + bồi âm, envelope attack/release; ghi WRAP modulo chiều dài buffer.</summary>
        private static void AddNote(float[] buf, float startSec, float freq, float durSec, float amp,
                                    float attackSec, float releaseSec, int harmonics = 1, float detune = 0f)
        {
            int n = Mathf.RoundToInt(durSec * Rate);
            int start = Mathf.RoundToInt(startSec * Rate);
            float twoPiF = 2f * Mathf.PI * freq / Rate;
            float twoPiF2 = detune > 0f ? 2f * Mathf.PI * freq * detune / Rate : 0f;
            int atk = Mathf.Max(1, Mathf.RoundToInt(attackSec * Rate));
            int rel = Mathf.Max(1, Mathf.RoundToInt(releaseSec * Rate));

            for (int i = 0; i < n; i++)
            {
                float env = Mathf.Min(1f, i / (float)atk) * Mathf.Min(1f, (n - i) / (float)rel);
                float s = Mathf.Sin(twoPiF * i);
                if (harmonics >= 2) s += 0.3f * Mathf.Sin(2f * twoPiF * i);
                if (harmonics >= 3) s += 0.14f * Mathf.Sin(3f * twoPiF * i);
                if (twoPiF2 > 0f) s += 0.5f * Mathf.Sin(twoPiF2 * i); // detune nhẹ → chuông lung linh.
                buf[(start + i) % buf.Length] += s * amp * env;
            }
        }

        /// <summary>Trống trầm: sine tụt cao độ 55→30Hz decay nhanh (đấm ngực kiểu núi lửa).</summary>
        private static void AddDrum(float[] buf, float startSec, float amp)
        {
            int n = Mathf.RoundToInt(0.5f * Rate);
            int start = Mathf.RoundToInt(startSec * Rate);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = Mathf.Lerp(55f, 30f, t);
                phase += 2f * Mathf.PI * f / Rate;
                buf[(start + i) % buf.Length] += Mathf.Sin(phase) * amp * (1f - t) * (1f - t);
            }
        }

        /// <summary>Gió/rền: noise random-walk (tự lowpass) với swell sin — wrap như AddNote.</summary>
        private static void AddWind(float[] buf, System.Random rng, float startSec, float durSec, float amp)
        {
            int n = Mathf.RoundToInt(durSec * Rate);
            int start = Mathf.RoundToInt(startSec * Rate);
            float v = 0f;
            for (int i = 0; i < n; i++)
            {
                v = v * 0.985f + ((float)rng.NextDouble() * 2f - 1f) * 0.05f; // random-walk = noise trầm.
                float swell = Mathf.Sin(Mathf.PI * i / (float)n);             // dâng rồi lặng.
                buf[(start + i) % buf.Length] += v * amp * swell * 6f;
            }
        }

        /// <summary>Chuẩn hóa chống clip rồi ghi WAV PCM16 mono 22050Hz.</summary>
        private static void WriteWav(string path, float[] samples)
        {
            float peak = 0.0001f;
            foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
            float norm = 0.86f / peak; // headroom nhẹ — BGM nền không được át SFX.

            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int dataLen = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataLen);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1);           // PCM, mono
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataLen);
                foreach (float s in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * norm * short.MaxValue),
                                               short.MinValue, short.MaxValue));
            }
            AssetDatabase.ImportAsset(path);
        }
    }
}
