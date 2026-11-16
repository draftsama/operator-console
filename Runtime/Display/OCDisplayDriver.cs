#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace OperatorConsole
{
    /// <summary>Reads and changes the app's window through <see cref="Screen"/> and, for Borderless, Win32. Does nothing outside a Windows player.</summary>
    internal static class OCDisplayDriver
    {
        private const int SettleFrames = 3;
        private const double SettleTimeoutSeconds = 2;

        private static readonly List<DisplayInfo> s_layout = new();

        public static bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        public static void GetMonitorNames(List<string> names)
        {
            names.Clear();
            Screen.GetDisplayLayout(s_layout);
            for (var i = 0; i < s_layout.Count; i++)
                names.Add($"{i + 1}: {s_layout[i].name} ({s_layout[i].width}×{s_layout[i].height})");
            if (names.Count == 0) names.Add("1");
        }

        public static void GetResolutions(List<Vector2Int> resolutions)
        {
            resolutions.Clear();
            foreach (var r in Screen.resolutions)
            {
                var size = new Vector2Int(r.width, r.height);
                if (!resolutions.Contains(size)) resolutions.Add(size);
            }
            resolutions.Sort(CompareDescending);
        }

        private static int CompareDescending(Vector2Int a, Vector2Int b) =>
            a.x != b.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y);

        public static OCDisplaySettings Capture()
        {
            Screen.GetDisplayLayout(s_layout);
            var info = Screen.mainWindowDisplayInfo;
            var monitor = 0;
            for (var i = 0; i < s_layout.Count; i++)
            {
                if (s_layout[i].workArea.Equals(info.workArea) && s_layout[i].name == info.name)
                {
                    monitor = i;
                    break;
                }
            }

            var mode = Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => OCDisplayMode.FullScreen,
                FullScreenMode.Windowed            => HasNoCaption() ? OCDisplayMode.Borderless : OCDisplayMode.Windowed,
                _                                   => OCDisplayMode.Borderless
            };
            var position = Screen.mainWindowPosition;
            return new OCDisplaySettings(mode, monitor, Screen.width, Screen.height,
                position.x - info.workArea.x, position.y - info.workArea.y).Normalized();
        }

        public static async UniTask ApplyAsync(OCDisplaySettings requested, CancellationToken token)
        {
            if (!IsSupported) return;
            var s = requested.Normalized();
            switch (s.Mode)
            {
                case OCDisplayMode.Windowed:
                    RestoreFrame();
                    Screen.SetResolution(s.Width, s.Height, FullScreenMode.Windowed);
                    await SettleAsync(FullScreenMode.Windowed, s.Width, s.Height, token);
                    break;

                case OCDisplayMode.FullScreen:
                    await MoveToMonitorAsync(s.Monitor, token);
                    Screen.SetResolution(s.Width, s.Height, FullScreenMode.ExclusiveFullScreen);
                    await SettleAsync(FullScreenMode.ExclusiveFullScreen, s.Width, s.Height, token);
                    break;

                default:
                    RestoreFrame();
                    Screen.SetResolution(s.Width, s.Height, FullScreenMode.Windowed);
                    await SettleAsync(FullScreenMode.Windowed, s.Width, s.Height, token);
                    Screen.GetDisplayLayout(s_layout);
                    var origin = s.Monitor < s_layout.Count ? s_layout[s.Monitor].workArea.position : Vector2Int.zero;
                    StripFrame(origin.x + s.X, origin.y + s.Y, s.Width, s.Height);
                    break;
            }
        }

        private static async UniTask MoveToMonitorAsync(int monitor, CancellationToken token)
        {
            Screen.GetDisplayLayout(s_layout);
            if (monitor >= s_layout.Count) return;
            try
            {
                await Screen.MoveMainWindowTo(s_layout[monitor], Vector2Int.zero).ToUniTask(cancellationToken: token);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Debug.LogWarning($"OC: could not move the window to monitor {monitor + 1}: {e.Message}");
            }
        }

        private static async UniTask SettleAsync(FullScreenMode mode, int width, int height, CancellationToken token)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfterSlim(TimeSpan.FromSeconds(SettleTimeoutSeconds));
            try
            {
                await UniTask.WaitUntil(
                    () => Screen.fullScreenMode == mode && Screen.width == width && Screen.height == height,
                    cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Debug.LogWarning($"OC: the window is {Screen.width}×{Screen.height} ({Screen.fullScreenMode}), expected {width}×{height} ({mode}).");
            }
            await UniTask.DelayFrame(SettleFrames, cancellationToken: token);
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int GwlStyle = -16;
        private const int WsCaption = 0x00C00000;
        private const int WsThickFrame = 0x00040000;
        private const int WsSysMenu = 0x00080000;
        private const int WsMinimizeBox = 0x00020000;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpFrameChanged = 0x0020;

        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string windowName);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")] private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")] private static extern int SetWindowLong(IntPtr window, int index, int value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        private static IntPtr FindPlayerWindow()
        {
            var window = GetActiveWindow();
            return window != IntPtr.Zero ? window : FindWindow(null, Application.productName);
        }
#endif

        private static bool HasNoCaption()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var window = FindPlayerWindow();
            return window != IntPtr.Zero && (GetWindowLong(window, GwlStyle) & WsCaption) != WsCaption;
#else
            return false;
#endif
        }

        private static void StripFrame(int x, int y, int width, int height)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var window = FindPlayerWindow();
            if (window == IntPtr.Zero)
            {
                Debug.LogWarning("OC: the player window was not found, so it stays bordered.");
                return;
            }
            SetWindowLong(window, GwlStyle, GetWindowLong(window, GwlStyle) & ~(WsCaption | WsThickFrame));
            SetWindowPos(window, IntPtr.Zero, x, y, width, height, SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
#endif
        }

        private static void RestoreFrame()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var window = FindPlayerWindow();
            if (window == IntPtr.Zero) return;
            var style = GetWindowLong(window, GwlStyle);
            if ((style & WsCaption) == WsCaption) return;
            SetWindowLong(window, GwlStyle, style | WsCaption | WsSysMenu | WsMinimizeBox);
            SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
#endif
        }
    }
}
