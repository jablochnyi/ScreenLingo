// GlobalHotkey.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ScreenLingo
{
    public class GlobalHotkey : IDisposable
    {
        private readonly IntPtr _hwnd;
        private readonly HwndSource _source;
        private int _id;
        private bool _registered;

        public event EventHandler? Pressed;

        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public GlobalHotkey(ModifierKeys modifiers, Key key, Window window)
        {
            var helper = new WindowInteropHelper(window);
            _hwnd = helper.EnsureHandle();
            _source = HwndSource.FromHwnd(_hwnd) ?? throw new InvalidOperationException("HwndSource is null");
            _source.AddHook(WndProc);

            _id = GetHashCode(); // простая id (можно улучшить)
            Register(modifiers, key);
        }

        private void Register(ModifierKeys modifiers, Key key)
        {
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            uint modMask = ConvertModifiers(modifiers);

            _registered = RegisterHotKey(_hwnd, _id, modMask, vk);
            if (!_registered)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException($"Не удалось зарегистрировать хоткей (Win32 error {err}). Возможно комбинация занята.");
            }
        }

        private uint ConvertModifiers(ModifierKeys m)
        {
            uint mask = 0;
            if (m.HasFlag(ModifierKeys.Alt)) mask |= 0x0001;
            if (m.HasFlag(ModifierKeys.Control)) mask |= 0x0002;
            if (m.HasFlag(ModifierKeys.Shift)) mask |= 0x0004;
            if (m.HasFlag(ModifierKeys.Windows)) mask |= 0x0008;
            return mask;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            {
                Pressed?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_registered)
            {
                UnregisterHotKey(_hwnd, _id);
                _registered = false;
            }
            try { _source.RemoveHook(WndProc); } catch { }
        }
    }
}
