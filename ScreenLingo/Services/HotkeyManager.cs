using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ScreenLingo.Services
{
    public class HotkeyManager : IDisposable
    {
        private readonly Window _window;
        private readonly IntPtr _handle;
        private int _currentHotkeyId = 0;
        private static int _idCounter = 0;

        public event Action? HotkeyPressed;

        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyManager(Window window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _handle = new WindowInteropHelper(_window).EnsureHandle();

            var source = HwndSource.FromHwnd(_handle);
            if (source != null)
                source.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                HotkeyPressed?.Invoke();
                handled = true;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Регистрирует одну горячую клавишу. Если уже была зарегистрирована — автоматически отписывает её и регистрирует новую.
        /// Возвращает true, если регистрация успешна.
        /// </summary>
        public bool RegisterHotKey(uint vk, uint modifiers)
        {
            // Отрегестрировать предыдущую, если есть
            if (_currentHotkeyId != 0)
            {
                try { UnregisterHotKey(_handle, _currentHotkeyId); }
                catch { /*ignore*/ }
                _currentHotkeyId = 0;
            }

            int id = System.Threading.Interlocked.Increment(ref _idCounter);
            bool ok = RegisterHotKey(_handle, id, modifiers, vk);
            if (ok)
                _currentHotkeyId = id;
            return ok;
        }

        public void Dispose()
        {
            if (_currentHotkeyId != 0)
            {
                try { UnregisterHotKey(_handle, _currentHotkeyId); }
                catch { /*ignore*/ }
                _currentHotkeyId = 0;
            }

            // Не удаляем Hwnd hook — HwndSource сам освободит его при уничтожении окна.
        }
    }
}
