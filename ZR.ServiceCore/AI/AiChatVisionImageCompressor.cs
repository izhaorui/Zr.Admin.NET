using SkiaSharp;

namespace ZR.ServiceCore.AI
{
    /// <summary>
    /// 发给视觉模型前压缩 data URI，不改用户已上传原文件。
    /// </summary>
    public static class AiChatVisionImageCompressor
    {
        public const int MaxEdge = 1600;
        public const int JpegQuality = 75;

        public static List<string> CompressForModel(IReadOnlyList<string> urls)
        {
            var result = new List<string>();
            if (urls == null || urls.Count == 0) return result;
            foreach (var url in urls)
            {
                result.Add(CompressOne(url) ?? url);
            }
            return result;
        }

        private static string CompressOne(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
            var comma = url.IndexOf(',');
            if (comma < 0 || comma >= url.Length - 1) return url;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(url[(comma + 1)..]);
            }
            catch
            {
                return url;
            }
            if (bytes.Length == 0) return url;

            try
            {
                using var bitmap = SKBitmap.Decode(bytes);
                if (bitmap == null) return url;
                var oversized = bitmap.Width > MaxEdge || bitmap.Height > MaxEdge;
                if (!oversized && bytes.Length <= 400 * 1024)
                {
                    return url;
                }
                var target = bitmap;
                SKBitmap resized = null;
                if (oversized)
                {
                    var scale = Math.Min((float)MaxEdge / bitmap.Width, (float)MaxEdge / bitmap.Height);
                    resized = bitmap.Resize(new SKImageInfo(
                        Math.Max(1, (int)Math.Round(bitmap.Width * scale)),
                        Math.Max(1, (int)Math.Round(bitmap.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
                    target = resized;
                }
                try
                {
                    using var image = SKImage.FromBitmap(target);
                    using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
                    return "data:image/jpeg;base64," + Convert.ToBase64String(data.ToArray());
                }
                finally
                {
                    resized?.Dispose();
                }
            }
            catch
            {
                return url;
            }
        }
    }
}
