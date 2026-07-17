using System;
using System.Text;
using ZR.Common;

namespace Infrastructure.Captcha
{
    /// <summary>
    /// 默认验证码实现：服务端生成 SVG 矢量图，答案存入进程内缓存
    /// </summary>
    public class SvgCaptchaProvider : ICaptchaProvider
    {
        private static readonly Random Rand = new();

        // 排除易混淆字符：0/O、1/l/I
        private const string Chars = "验证码abcdefghjkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int Width = 120;
        private const int Height = 40;

        private bool IgnoreCase
            => bool.TryParse(AppSettings.App("CaptchaOptions", "IgnoreCase"), out var b) && b;

        private int Length
            => int.TryParse(AppSettings.App("CaptchaOptions", "Length"), out var n) && n > 0 ? n : 4;

        public CaptchaResult Generate(string id, int expiredSeconds = 60)
        {
            var code = GenerateCode(Length);
            var svg = BuildSvg(code);

            CacheHelper.SetCacheDateTime(CacheKey(id), code, expiredSeconds);

            var dataUrl = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
            return new CaptchaResult
            {
                Code = code,
                DataUrl = dataUrl,
                ContentType = "image/svg+xml"
            };
        }

        public bool Validate(string id, string code, bool removeIfSuccess = true)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(code))
            {
                return false;
            }

            var saved = CacheHelper.Get(CacheKey(id)) as string;
            if (saved == null)
            {
                return false;
            }

            var ok = IgnoreCase
                ? string.Equals(saved, code, StringComparison.OrdinalIgnoreCase)
                : saved == code;

            if (ok && removeIfSuccess)
            {
                CacheHelper.Remove(CacheKey(id));
            }

            return ok;
        }

        private static string CacheKey(string id) => "captcha_" + id;

        private static string GenerateCode(int len)
        {
            var sb = new StringBuilder(len);
            for (var i = 0; i < len; i++)
            {
                sb.Append(Chars[Rand.Next(Chars.Length)]);
            }
            return sb.ToString();
        }

        private string BuildSvg(string code)
        {
            var sb = new StringBuilder();
            sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Width}\" height=\"{Height}\" viewBox=\"0 0 {Width} {Height}\">");
            sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"{RandomLightColor()}\"/>");

            // 干扰线
            for (var i = 0; i < 4; i++)
            {
                var x1 = Rand.Next(Width);
                var y1 = Rand.Next(Height);
                var x2 = Rand.Next(Width);
                var y2 = Rand.Next(Height);
                sb.Append($"<line x1=\"{x1}\" y1=\"{y1}\" x2=\"{x2}\" y2=\"{y2}\" stroke=\"{RandomColor(80, 200)}\" stroke-width=\"{Rand.Next(1, 3)}\"/>");
            }

            // 验证码字符
            var step = Width / code.Length;
            for (var i = 0; i < code.Length; i++)
            {
                var fs = Rand.Next(22, 33);
                var x = step * i + step / 2;
                var y = Rand.Next(28, Height - 4);
                var angle = Rand.Next(-30, 31);
                sb.Append($"<text x=\"{x}\" y=\"{y}\" font-size=\"{fs}\" font-family=\"Arial, Verdana, sans-serif\" font-weight=\"bold\" fill=\"{RandomColor(0, 160)}\" text-anchor=\"middle\" transform=\"rotate({angle} {x} {y})\">{code[i]}</text>");
            }

            // 噪点
            for (var i = 0; i < 30; i++)
            {
                var cx = Rand.Next(Width);
                var cy = Rand.Next(Height);
                sb.Append($"<circle cx=\"{cx}\" cy=\"{cy}\" r=\"{Rand.Next(1, 2)}\" fill=\"{RandomColor(80, 200)}\"/>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        private static string RandomColor(int min, int max)
        {
            var r = Rand.Next(min, max);
            var g = Rand.Next(min, max);
            var b = Rand.Next(min, max);
            return $"#{r:X2}{g:X2}{b:X2}";
        }

        private static string RandomLightColor()
        {
            var r = Rand.Next(200, 256);
            var g = Rand.Next(200, 256);
            var b = Rand.Next(200, 256);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}
