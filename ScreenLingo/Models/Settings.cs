using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ScreenLingo.Models
{
    public class Settings : INotifyPropertyChanged
    {
        private int _fontSize = 20;
        private string _translator = "Yandex";
        private string _language = "English";
        private string _hotkey = "F10";

        public int FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(); }
        }

        public string Translator
        {
            get => _translator;
            set { _translator = value; OnPropertyChanged(); }
        }

        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(); }
        }

        public string Hotkey
        {
            get => _hotkey;
            set { _hotkey = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
