using ScreenLingo.Models;
using ScreenLingo.Services;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace ScreenLingo
{
    public partial class MainWindow : Window
    {

        private AppSettings _appSettings;
        private GlobalHotkey? _hotkey;             // основная горячая клавиша перевода
        private GlobalHotkey? _toggleHotkey;       // клавиша для показа/скрытия перевода
        private bool _overlayVisible = true;       // состояние видимости перевода

        // поля класса
        private bool _isAssigningToggleHotkey = false;
        private bool _isAssigningTranslateHotkey = false;


        private Settings _settings;
        private HotkeyManager? _hotkeyManager;

        private System.Drawing.Rectangle? _selectedRegion = null;
        private OverlayWindow? _overlayWindow;

        private const int HOTKEY_ID = 9000;
        private uint _selectedModifier = 0;
        private uint _selectedKey = 0;

        public MainWindow()
        {
            InitializeComponent();
            _appSettings = SettingsManager.Load();

            if (!string.IsNullOrEmpty(_appSettings.TranslateHotkey))
            {
                try
                {
                    (ModifierKeys mods, Key key) = ParseHotkeyString(_appSettings.TranslateHotkey);
                    _hotkey?.Dispose();
                    _hotkey = new GlobalHotkey(mods, key, this);
                    _hotkey.Pressed += OnHotkeyPressed;
                    HotkeyBox.Text = _appSettings.TranslateHotkey;
                }
                catch { /* логирование */ }
            }

            if (!string.IsNullOrEmpty(_appSettings.ToggleHotkey))
            {
                try
                {
                    (ModifierKeys mods, Key key) = ParseHotkeyString(_appSettings.ToggleHotkey);
                    _toggleHotkey?.Dispose();
                    _toggleHotkey = new GlobalHotkey(mods, key, this);
                    _toggleHotkey.Pressed += ToggleHotkey_Pressed;
                    ToggleHotkeyBox.Text = _appSettings.ToggleHotkey;
                }
                catch { /* логирование */ }
            }


            // Регистрируем Ctrl+Shift+H для скрытия/показа перевода
            _toggleHotkey = new GlobalHotkey(ModifierKeys.Control | ModifierKeys.Shift, Key.H, this);
            _toggleHotkey.Pressed += ToggleOverlayHotkeyPressed;
            _settings = ConfigService.Load();
            DataContext = _settings;
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        // ───────────────────────────────────────────────
        #region Hotkey registration

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);
            source.AddHook(HwndHook);
        }
        private static (ModifierKeys, Key) ParseHotkeyString(string combo)
        {
            ModifierKeys modifiers = ModifierKeys.None;
            Key key = Key.None;
            var parts = combo.Split('+', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var part = p.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;
                else Enum.TryParse(part, true, out key);
            }
            return (modifiers, key);
        }


        private void AssignToggleHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (_isAssigningToggleHotkey) return;
            _isAssigningToggleHotkey = true;
            ToggleHotkeyBox.Text = "Нажмите комбинацию...";
            this.PreviewKeyDown += CaptureToggleHotkey;
            this.Focus();
        }

        private void CaptureToggleHotkey(object sender, KeyEventArgs e)
        {
            if (!_isAssigningToggleHotkey) return;

            e.Handled = true;
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            if (IsModifierKey(key))
                return;

            ModifierKeys modifiers = Keyboard.Modifiers;

            // Освобождаем предыдущую регистрацию, если есть
            try { _toggleHotkey?.Dispose(); } catch { }

            try
            {
                _toggleHotkey = new GlobalHotkey(modifiers, key, this);
                _toggleHotkey.Pressed += ToggleHotkey_Pressed;

                string combo = BuildHotkeyString(modifiers, key);

                // Сохраняем и обновляем UI
                _appSettings.ToggleHotkey = combo;
                SettingsManager.Save(_appSettings);
                ToggleHotkeyBox.Text = combo;

                MessageBox.Show($"Хоткей для показа/скрытия назначен: {combo}", "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ToggleHotkeyBox.Text = "Ошибка регистрации";
                MessageBox.Show($"Не удалось зарегистрировать хоткей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _isAssigningToggleHotkey = false;
                this.PreviewKeyDown -= CaptureToggleHotkey;
            }
        }

        private void ToggleHotkey_Pressed(object? sender, EventArgs e)
        {
            // переключаем видимость переводов — делаем это в UI-потоке
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_overlayWindow == null) return;

                // получаем текущее состояние по первому найденному элементу (попробуем определить видимость)
                bool anyVisible = false;
                foreach (UIElement child in _overlayWindow.OverlayCanvas.Children)
                {
                    if (child is System.Windows.Shapes.Rectangle rect && rect.Tag != null && rect.Tag.ToString() == "region")
                        continue;
                    if (child.Visibility == Visibility.Visible) { anyVisible = true; break; }
                }

                // переключаем
                _overlayWindow.SetTranslationsVisibility(!anyVisible);
            });
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                UpdateTranslation();
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void ToggleOverlayHotkeyPressed(object? sender, EventArgs e)
        {
            if (_overlayWindow == null)
                return;

            _overlayVisible = !_overlayVisible;

            foreach (UIElement child in _overlayWindow.OverlayCanvas.Children)
            {
                // Не трогаем рамку области
                if (child is System.Windows.Shapes.Rectangle rect && rect.Tag != null && rect.Tag.ToString() == "region")
                    continue;

                child.Visibility = _overlayVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            try
            {
                // 🧹 1. Освобождаем все глобальные горячие клавиши
                _hotkey?.Dispose();
                _toggleHotkey?.Dispose();

                // 🧹 2. Закрываем OverlayWindow, если он открыт
                if (_overlayWindow != null)
                {
                    try
                    {
                        _overlayWindow.Close();
                        _overlayWindow = null;
                    }
                    catch { /* игнорируем возможные исключения */ }
                }

                // 🧹 3. Убираем область выделения (если она осталась прозрачной)
                if (_selectedRegion != null)
                    _selectedRegion = null;

                // 🧹 4. Завершаем процесс, чтобы не осталось фоновых потоков OCR/перевода
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка при закрытии приложения: " + ex.Message);
            }
        }

        #endregion
        // ───────────────────────────────────────────────

        private void AssignHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (_isAssigningTranslateHotkey) return;
            _isAssigningTranslateHotkey = true;
            HotkeyBox.Text = "Нажмите комбинацию...";
            this.PreviewKeyDown += CaptureHotkey;
            this.Focus();
        }

        private void CaptureHotkey(object sender, KeyEventArgs e)
        {
            if (!_isAssigningTranslateHotkey) return;

            e.Handled = true;
            Key key = (e.Key == Key.System ? e.SystemKey : e.Key);

            // Игнорируем чисто-модификаторные нажатия
            if (IsModifierKey(key))
                return;

            ModifierKeys modifiers = Keyboard.Modifiers;

            // Освобождаем предыдущую регистрацию, если есть
            try { _hotkey?.Dispose(); } catch { }

            try
            {
                // Регистрируем хоткей
                _hotkey = new GlobalHotkey(modifiers, key, this);
                _hotkey.Pressed += OnHotkeyPressed;

                // Формируем строку вида "Ctrl+Shift+T"
                string combo = BuildHotkeyString(modifiers, key);

                // Сохраняем в настройки и в UI
                _appSettings.TranslateHotkey = combo;
                SettingsManager.Save(_appSettings);
                HotkeyBox.Text = combo;

                MessageBox.Show($"Хоткей для перевода назначен: {combo}", "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HotkeyBox.Text = "Ошибка регистрации";
                MessageBox.Show($"Не удалось зарегистрировать хоткей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _isAssigningTranslateHotkey = false;
                this.PreviewKeyDown -= CaptureHotkey;
            }
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl
                   || key == Key.LeftShift || key == Key.RightShift
                   || key == Key.LeftAlt || key == Key.RightAlt
                   || key == Key.LWin || key == Key.RWin;
        }

        private static string BuildHotkeyString(ModifierKeys modifiers, Key key)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());
            return string.Join("+", parts);
        }

        // ───────────────────────────────────────────────

        private Rect DrawingRectangleToWpfRect(System.Drawing.Rectangle rectPixels)
        {
            // левый верх в пикселях
            var topLeftPx = new System.Windows.Point(rectPixels.X, rectPixels.Y);
            var bottomRightPx = new System.Windows.Point(rectPixels.X + rectPixels.Width, rectPixels.Y + rectPixels.Height);

            // преобразование device pixels -> DIPs
            var src = PresentationSource.FromVisual(this);
            Matrix transformFromDevice = Matrix.Identity;
            if (src?.CompositionTarget != null)
                transformFromDevice = src.CompositionTarget.TransformFromDevice;

            var topLeftDip = transformFromDevice.Transform(topLeftPx);
            var bottomRightDip = transformFromDevice.Transform(bottomRightPx);

            // Если Overlay ожидает координаты относительно виртуального экрана,
            // нужно учитывать SystemParameters.VirtualScreenLeft/Top. Многие реализации используют абсолютные экраны.
            // В таком случае можно вернуть Rect в абсолютных координатах DIPs:
            var rectDip = new Rect(topLeftDip, bottomRightDip);
            return rectDip;
        }

        private void StartCapture(object sender, RoutedEventArgs e)
        {
            var captureWindow = new ScreenCaptureOverlay();
            if (captureWindow.ShowDialog() == true)
            {
                _selectedRegion = captureWindow.SelectionRect;

                if (_overlayWindow == null)
                    _overlayWindow = new OverlayWindow();

                // <- ПЕРЕДАЁМ System.Drawing.Rectangle напрямую
                _overlayWindow.ShowTransparentRegion(_selectedRegion.Value);

                MessageBox.Show("Область выбрана.\nТеперь нажмите назначенную горячую клавишу для перевода.",
                                "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // ───────────────────────────────────────────────

        private async Task UpdateTranslation()
        {
            if (_selectedRegion == null)
            {
                MessageBox.Show("Сначала выберите область захвата!", "ScreenLingo",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var bmp = new Bitmap(_selectedRegion.Value.Width, _selectedRegion.Value.Height);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(_selectedRegion.Value.Location, System.Drawing.Point.Empty, _selectedRegion.Value.Size);
                }

                string ocrLang = GetSelectedLang();
                var ocrResults = OCRHelper.RecognizeWithBounds(bmp, ocrLang);
                // --- вставить вместо текущего блока перед показом overlay ---
                if (ocrResults == null || ocrResults.Count == 0)
                {
                    MessageBox.Show("Текст не распознан.", "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Уберём пустые строки
                var nonEmptyResults = ocrResults.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
                var lines = nonEmptyResults.Select(r => r.Text).ToList();
                if (lines.Count == 0)
                {
                    MessageBox.Show("Текст не распознан.", "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Перевод (как у тебя)
                string providerName = GetSelectedTranslator();
                var provider = providerName.Contains("Libre") ? TranslatorHelper.Provider.LibreTranslate : TranslatorHelper.Provider.Google;
                string srcLang = LangMapper.MapToTranslateCode(GetSelectedLang());

                List<string> translatedLines;
                try
                {
                    translatedLines = await TranslatorHelper.TranslateLinesAsync(lines, srcLang, "ru", provider);
                }
                catch (Exception ex)
                {
                    // при ошибке переводим в place-holders, чтобы UI всё ещё отрисовал позиции
                    translatedLines = lines.Select(_ => $"[Ошибка перевода: {ex.Message}]").ToList();
                }

                // ---- ВАЖНО: смещаем bounding boxes из координат захваченной картинки в экранные координаты ----
                // Предполагается, что _selectedRegion (или liveRegion) содержит System.Drawing.Rectangle — экранные пиксели.
                var sel = _selectedRegion ?? throw new InvalidOperationException("selectedRegion не задан");
                for (int i = 0; i < nonEmptyResults.Count && i < translatedLines.Count; i++)
                {
                    var orig = nonEmptyResults[i];
                    var b = orig.Bounds; // координаты относительно изображения
                    orig.Bounds = new System.Drawing.Rectangle(
                        b.X + sel.X,
                        b.Y + sel.Y,
                        b.Width,
                        b.Height
                    );
                }

                // Теперь передаём в Overlay — он ожидает screen pixels (System.Drawing.Rectangle)
                if (_overlayWindow == null) _overlayWindow = new OverlayWindow();
                _overlayWindow.ShowTranslatedLines(nonEmptyResults, translatedLines, GetFontSize());
                _overlayWindow.Show();

            }
            catch (Exception ex)
            {
                Debug.WriteLine("Ошибка перевода: " + ex.Message);
                MessageBox.Show("Ошибка при переводе:\n" + ex.Message);
            }
        }

        // ───────────────────────────────────────────────

        private async void OnHotkeyPressed(object? sender, EventArgs e)
        {
            try
            {
                // Проверяем, что есть выбранная область
                if (_selectedRegion == null)
                {
                    MessageBox.Show("Сначала выберите область для перевода.",
                                    "ScreenLingo", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await UpdateTranslation(); // твой метод, выполняющий OCR + перевод + вывод в Overlay
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка при вызове горячей клавиши: " + ex.Message);
            }
        }

        private string GetSelectedLang()
        {
            if (SourceLangComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag != null)
                return item.Tag.ToString()!;
            return "eng";
        }

        private string GetSelectedTranslator()
        {
            if (TranslatorComboBox.SelectedItem is ComboBoxItem item)
                return item.Content.ToString()!;
            return "Google";
        }

        private int GetFontSize()
        {
            if (int.TryParse(FontSizeBox.Text, out int fs))
                return Math.Clamp(fs, 10, 72);
            return 20;
        }

        private void FontSize_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
    }
}
