using System.Collections.Generic;
using UnityEngine;

namespace FA.Effects
{
    /// <summary>
    /// KHÓA CAMERA cho cinematic (chant/super flash): Cinemachine Brain ghi đè camera MỖI LateUpdate
    /// (kể cả khi timeScale=0) → zoom cinematic bị nuốt (bug user: "cam khoá cứng từ cinemachine").
    /// Acquire = TẮT tạm mọi Behaviour Cinemachine trên object camera (nhận diện theo tên type —
    /// không ref package, FA.Effects giữ asmdef sạch §1.3); Release = bật lại (Brain tự snap về vcam).
    /// ĐẾM ĐỘ SÂU vì chant + flash LỒNG nhau (chant Acquire → flash Acquire/Release → chant Release).
    /// Follow script thường (không Cinemachine) đã bị LateUpdate-assert của controller đè — không cần tắt.
    /// </summary>
    internal static class CameraCinematicLock
    {
        private static readonly List<Behaviour> s_disabled = new List<Behaviour>();
        private static int s_depth;

        public static void Acquire(Camera cam)
        {
            s_depth++;
            if (s_depth > 1 || cam == null) return;
            foreach (Behaviour b in cam.GetComponents<Behaviour>())
            {
                if (b == null || !b.enabled || b is Camera) continue;
                if (!b.GetType().Name.Contains("Cinemachine")) continue;
                b.enabled = false;
                s_disabled.Add(b);
            }
        }

        public static void Release()
        {
            if (s_depth <= 0) return;
            if (--s_depth > 0) return;
            foreach (Behaviour b in s_disabled)
                if (b != null) b.enabled = true;
            s_disabled.Clear();
        }
    }
}
