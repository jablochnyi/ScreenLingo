using System.Collections.Generic;

namespace ScreenLingo
{
    public static class LangMapper
    {
        private static readonly Dictionary<string, string> _ocrToTranslate = new()
        {
            { "eng", "en" },
            { "jpn", "ja" },
            { "kor", "ko" },
            { "chi_sim", "zh" }
        };

        public static string MapToTranslateCode(string ocrCode)
        {
            return _ocrToTranslate.TryGetValue(ocrCode, out var mapped)
                ? mapped
                : "en"; // по умолчанию английский
        }
    }
}
