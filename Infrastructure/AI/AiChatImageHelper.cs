using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Infrastructure.Model;

namespace Infrastructure.AI
{
    /// <summary>
    /// 办公助手发图：校验客户端图片 URL、转成视觉模型可消费的地址（本机文件转 data URI，公网 URL 原样下发）。
    /// 禁止把任意内网地址直接转给第三方模型（SSRF / 内网泄露）。
    /// </summary>
    public static class AiChatImageHelper
    {
        public const int MaxCount = 4;
        public const int MaxBytes = 4 * 1024 * 1024;
        public const string DefaultUserPrompt = "请查看图片并回答。";

        private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        private static readonly Dictionary<string, string> MimeByExt = new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".bmp"] = "image/bmp"
        };

        /// <summary>去空白、去重、上限截断；非法项抛友好异常。</summary>
        /// <param name="urls">待规范化的图片地址</param>
        /// <param name="maxCount">数量上限，默认 <see cref="MaxCount"/>（聊天一次性发图的产品限制）；
        /// 工作流等批量输入场景可放宽，避免因附件张数直接把整个 AI 能力判失败。</param>
        public static List<string> NormalizeClientUrls(IEnumerable<string> urls, int maxCount = MaxCount)
        {
            var list = new List<string>();
            if (urls == null) return list;
            foreach (var raw in urls)
            {
                var u = (raw ?? "").Trim();
                if (string.IsNullOrWhiteSpace(u)) continue;
                if (u.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("请先上传图片，不要直接粘贴 Base64");
                }
                if (u.Contains("..", StringComparison.Ordinal))
                {
                    throw new Exception("图片地址不合法");
                }
                if (!list.Contains(u, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(u);
                }
                if (list.Count > maxCount)
                {
                    throw new Exception($"一次最多发送 {maxCount} 张图片");
                }
            }
            return list;
        }

        /// <summary>
        /// 转成发给视觉模型的 URL 列表：本机/相对路径读文件转 data URI；公网 http(s) 校验主机后原样传递。
        /// </summary>
        public static List<string> ToModelImageUrls(IReadOnlyList<string> urls, string webRootPath, AiOptions options)
        {
            var result = new List<string>();
            if (urls == null || urls.Count == 0) return result;
            foreach (var url in urls)
            {
                var modelUrl = ResolveOne(url, webRootPath, options);
                if (string.IsNullOrWhiteSpace(modelUrl))
                {
                    throw new Exception("图片不存在或无法访问，请重新上传");
                }
                result.Add(modelUrl);
            }
            return result;
        }

        public static string ToImagesDataJson(IReadOnlyList<string> storeUrls)
        {
            if (storeUrls == null || storeUrls.Count == 0) return null;
            return JsonConvert.SerializeObject(new { images = storeUrls });
        }

        public static List<string> FromImagesDataJson(string dataJson)
        {
            if (string.IsNullOrWhiteSpace(dataJson)) return [];
            try
            {
                var jo = JObject.Parse(dataJson);
                var arr = jo["images"] as JArray;
                if (arr == null) return [];
                return arr.Select(x => x?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }
            catch
            {
                return [];
            }
        }

        private static string ResolveOne(string url, string webRootPath, AiOptions options)
        {
            var ext = GetExtension(url);
            if (!AllowedExt.Contains(ext))
            {
                throw new Exception("仅支持 jpg / png / gif / webp / bmp 图片");
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new Exception("图片地址不合法");
                }
                if (IsLoopbackOrPrivate(uri.Host))
                {
                    var local = TryReadLocalFromPath(uri.AbsolutePath, webRootPath, ext);
                    if (local != null) return local;
                    throw new Exception("内网图片无法发送给视觉模型，请使用已上传的文件");
                }
                if (!IsHostAllowed(uri.Host, options))
                {
                    throw new Exception("图片域名不在允许范围内");
                }
                return uri.ToString();
            }

            var path = url.StartsWith('/') ? url : "/" + url;
            var data = TryReadLocalFromPath(path, webRootPath, ext);
            if (data != null) return data;
            throw new Exception("图片不存在或无法访问，请重新上传");
        }

        private static string TryReadLocalFromPath(string urlPath, string webRootPath, string ext)
        {
            if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(urlPath)) return null;
            var relative = urlPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var full = Path.GetFullPath(Path.Combine(webRootPath, relative));
            var root = Path.GetFullPath(webRootPath).TrimEnd(Path.DirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return null;
            if (!File.Exists(full)) return null;
            var info = new FileInfo(full);
            if (info.Length <= 0 || info.Length > MaxBytes)
            {
                throw new Exception($"单张图片不能超过 {MaxBytes / 1024 / 1024}MB");
            }
            var bytes = File.ReadAllBytes(full);
            var mime = MimeByExt.TryGetValue(ext, out var m) ? m : "image/jpeg";
            return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }

        private static bool IsHostAllowed(string host, AiOptions options)
        {
            var allowed = options?.AttachmentDownloadAllowedHosts;
            if (allowed == null || allowed.Count == 0) return true;
            return allowed.Any(h => string.Equals((h ?? "").Trim(), host, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsLoopbackOrPrivate(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return true;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            if (!IPAddress.TryParse(host, out var ip))
            {
                return host.EndsWith(".local", StringComparison.OrdinalIgnoreCase);
            }
            if (IPAddress.IsLoopback(ip)) return true;
            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && bytes.Length >= 4)
            {
                if (bytes[0] == 10) return true;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                if (bytes[0] == 192 && bytes[1] == 168) return true;
                if (bytes[0] == 169 && bytes[1] == 254) return true;
            }
            return false;
        }

        private static string GetExtension(string url)
        {
            var path = url ?? "";
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
            }
            else
            {
                var q = path.IndexOf('?');
                if (q >= 0) path = path[..q];
            }
            return Path.GetExtension(path);
        }
    }
}
