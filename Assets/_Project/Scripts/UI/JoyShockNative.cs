using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FA.UI
{
    /// <summary>
    /// Wrapper P/Invoke TỐI THIỂU quanh JoyShockLibrary.dll — chỉ lấy IMU (accelerometer + gyroscope) của
    /// DualSense/DS4 trên Windows, phục vụ <see cref="GamepadAudioGestureController"/> dò "LẮC". Unity Input
    /// System KHÔNG expose gyro/accel của tay PS trên PC (chỉ trên máy PS thật) nên phải qua thư viện ngoài.
    ///
    /// CÀI ĐẶT (làm 1 lần, KHÔNG có trong repo — binary bên thứ 3, người dùng tự tải):
    ///   1) Tải JoyShockLibrary (JibbSmart/JoyShockLibrary) — file x64 <c>JoyShockLibrary.dll</c>.
    ///   2) Bỏ vào <c>Assets/Plugins/x86_64/JoyShockLibrary.dll</c> (Inspector: Platform = Windows x64).
    ///   3) Cắm DualSense bằng USB (IMU + mic chỉ chạy qua cáp).
    /// THIẾU DLL vẫn compile & chạy game bình thường: [DllImport] resolve lúc RUNTIME; lần gọi đầu bắt
    /// <see cref="DllNotFoundException"/>/<see cref="EntryPointNotFoundException"/> → <see cref="Available"/>=false,
    /// phần LẮC im lặng (không crash), phần THỔI (mic) vẫn hoạt động độc lập.
    /// </summary>
    internal static class JoyShockNative
    {
        private const string Dll = "JoyShockLibrary";

        /// <summary>IMU: accel theo g (nghỉ ≈ 1g), gyro theo độ/giây. Layout khớp struct IMU_STATE của JSL.</summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct ImuState
        {
            public float accelX, accelY, accelZ;
            public float gyroX, gyroY, gyroZ;
        }

        [DllImport(Dll)] private static extern int JslConnectDevices();
        [DllImport(Dll)] private static extern int JslGetConnectedDeviceHandles(int[] deviceHandleArray, int size);
        [DllImport(Dll)] private static extern ImuState JslGetIMUState(int deviceId);
        [DllImport(Dll)] private static extern void JslDisconnectAndDisposeAll();

        /// <summary>FALSE sau khi phát hiện thiếu DLL/entrypoint — mọi hàm dưới trở thành no-op an toàn.</summary>
        public static bool Available { get; private set; } = true;

        private static bool _loggedMissing;

        /// <summary>Quét & đăng ký thiết bị JSL. Trả số tay cầm (0 nếu thiếu DLL hoặc không có tay).</summary>
        public static int Connect()
        {
            if (!Available) return 0;
            try { return JslConnectDevices(); }
            catch (Exception e) { MarkUnavailable(e); return 0; }
        }

        /// <summary>Lấy handle các tay cầm JSL đang thấy vào <paramref name="buffer"/>; trả số handle đã ghi.</summary>
        public static int GetHandles(int[] buffer)
        {
            if (!Available || buffer == null || buffer.Length == 0) return 0;
            try { return JslGetConnectedDeviceHandles(buffer, buffer.Length); }
            catch (Exception e) { MarkUnavailable(e); return 0; }
        }

        /// <summary>Đọc IMU của 1 handle. FALSE nếu thiếu DLL → caller bỏ qua cử chỉ lắc.</summary>
        public static bool TryGetImu(int handle, out ImuState state)
        {
            state = default;
            if (!Available) return false;
            try { state = JslGetIMUState(handle); return true; }
            catch (Exception e) { MarkUnavailable(e); return false; }
        }

        /// <summary>Ngắt & giải phóng tất cả thiết bị JSL — gọi khi tắt cử chỉ / thoát game.</summary>
        public static void DisposeAll()
        {
            if (!Available) return;
            try { JslDisconnectAndDisposeAll(); }
            catch (Exception e) { MarkUnavailable(e); }
        }

        private static void MarkUnavailable(Exception e)
        {
            Available = false;
            if (_loggedMissing) return;
            _loggedMissing = true;
            Debug.LogWarning("[JoyShockNative] Không nạp được JoyShockLibrary.dll — phần 'LẮC → giảm âm lượng' " +
                             "bị tắt (phần 'THỔI' vẫn chạy). Cài DLL x64 vào Assets/Plugins/x86_64/ để bật. " +
                             $"Chi tiết: {e.GetType().Name}.");
        }
    }
}
