using System.Collections.Generic;
using Tesseract;
using System.Drawing;

namespace ScreenLingo
{
    public class OCRResult
    {
        public string Text { get; set; } = "";
        public Rectangle Bounds { get; set; }
    }

    public static class OCRHelper
    {
        public static List<OCRResult> RecognizeWithBounds(Bitmap image, string lang)
        {
            var results = new List<OCRResult>();

            using var engine = new TesseractEngine(@"./tessdata", lang, EngineMode.Default);
            using var pix = PixConverter.ToPix(image);
            using var page = engine.Process(pix);

            using var iter = page.GetIterator();
            iter.Begin();

            do
            {
                if (iter.TryGetBoundingBox(PageIteratorLevel.TextLine, out var rect))
                {
                    string line = iter.GetText(PageIteratorLevel.TextLine);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        results.Add(new OCRResult
                        {
                            Text = line.Trim(),
                            Bounds = new Rectangle(rect.X1, rect.Y1, rect.X2 - rect.X1, rect.Y2 - rect.Y1)
                        });
                    }
                }
            } while (iter.Next(PageIteratorLevel.TextLine));

            return results;
        }

        internal static string ExtractTextFromBitmap(Bitmap bmp)
        {
            throw new NotImplementedException();
        }
    }
}
