using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScreenLingo
{
    public static class TranslatorHelper
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly Dictionary<string, string> _cache = new Dictionary<string, string>();

        public enum Provider { Google, LibreTranslate }

        // Перевод списка строк — возвращает столько же элементов, сколько lines (ни один элемент не null)
        public static async Task<List<string>> TranslateLinesAsync(List<string> lines, string sourceLang, string targetLang, Provider provider)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            var results = Enumerable.Repeat<string>(null, lines.Count).ToList();
            var indicesToTranslate = new List<int>();

            // Проверка кэша
            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                if (string.IsNullOrWhiteSpace(l))
                {
                    results[i] = "";
                }
                else if (_cache.TryGetValue(l, out var cached))
                {
                    results[i] = cached;
                }
                else
                {
                    indicesToTranslate.Add(i);
                }
            }

            if (indicesToTranslate.Count == 0)
                return results;

            // Формируем список строк для перевода
            var toTranslateList = indicesToTranslate.Select(i => lines[i]).ToList();

            // Попытка батчевого перевода (одним запросом)
            try
            {
                string joined = string.Join("\n", toTranslateList);
                string translatedJoined;

                if (provider == Provider.Google)
                    translatedJoined = await TranslateGoogleAsync(joined, "auto", targetLang); // auto detect
                else
                    translatedJoined = await TranslateLibreAsync(joined, sourceLang, targetLang);

                if (string.IsNullOrWhiteSpace(translatedJoined))
                    throw new Exception("пустой ответ от переводчика");

                // Разбиваем по строкам — если меньше элементов, распределяем «последний» оставшийся
                var splitted = translatedJoined.Split('\n');
                for (int k = 0; k < indicesToTranslate.Count; k++)
                {
                    int idx = indicesToTranslate[k];
                    string tr = (k < splitted.Length) ? splitted[k].Trim() : splitted.Last().Trim();
                    if (string.IsNullOrWhiteSpace(tr))
                        tr = "[Ошибка перевода: пустой ответ]";
                    results[idx] = tr;
                    _cache[lines[idx]] = tr;
                }

                return results;
            }
            catch (Exception exBatch)
            {
                // Батч не удался — попробуем построчно (и не бросаем исключение наружу)
                for (int k = 0; k < indicesToTranslate.Count; k++)
                {
                    int idx = indicesToTranslate[k];
                    string srcLine = lines[idx];
                    try
                    {
                        string t;
                        if (provider == Provider.Google)
                            t = await TranslateGoogleAsync(srcLine, "auto", targetLang);
                        else
                            t = await TranslateLibreAsync(srcLine, sourceLang, targetLang);

                        if (string.IsNullOrWhiteSpace(t))
                            t = $"[Ошибка перевода: пустой ответ]";

                        results[idx] = t;
                        _cache[srcLine] = t;
                    }
                    catch (Exception exLine)
                    {
                        results[idx] = $"[Ошибка перевода: {exLine.Message}]";
                    }
                }

                return results;
            }
        }

        // Google (неофициальный endpoint). Возвращает весь перевод как строку (включая \n если были).
        public static async Task<string> TranslateGoogleAsync(string text, string sourceLang, string targetLang)
        {
            try
            {
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLang}&tl={targetLang}&dt=t&q={Uri.EscapeDataString(text)}";
                var response = await _http.GetAsync(url);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"HTTP {response.StatusCode}: {body}");

                using var doc = JsonDocument.Parse(body);
                // doc[0] — массив предложений, каждый элемент — [translated, original, null, ...]
                var sb = new StringBuilder();
                foreach (var sentence in doc.RootElement[0].EnumerateArray())
                {
                    if (sentence.GetArrayLength() > 0)
                    {
                        var part = sentence[0].GetString() ?? "";
                        sb.Append(part);
                    }
                }

                string combined = sb.ToString();

                // Попробуем восстановить разбиение по строкам: если в исходном тексте были '\n', Google обычно сохраняет их.
                // Если нет — оставим как есть.
                return combined;
            }
            catch (Exception ex)
            {
                throw new Exception($"Google error: {ex.Message}");
            }
        }

        // LibreTranslate через HTTP POST (ожидает JSON {q, source, target, format})
        public static async Task<string> TranslateLibreAsync(string text, string sourceLang, string targetLang)
        {
            try
            {
                var payload = new { q = text, source = sourceLang, target = targetLang, format = "text" };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                // Попробуй локальный сервер http://127.0.0.1:5000/translate если у тебя поднят, иначе публичный
                var url = "https://libretranslate.de/translate";
                var resp = await _http.PostAsync(url, content);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                    throw new Exception($"HTTP {resp.StatusCode}: {body}");

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("translatedText", out var el))
                    return el.GetString() ?? "";
                else
                    throw new Exception("неверный ответ LibreTranslate");
            }
            catch (Exception ex)
            {
                throw new Exception($"LibreTranslate error: {ex.Message}");
            }
        }

        // Удобная функция для очистки кэша при отладке
        public static void ClearCache() => _cache.Clear();
    }
}
