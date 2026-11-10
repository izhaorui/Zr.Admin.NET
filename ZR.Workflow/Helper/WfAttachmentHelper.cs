using System.Net;
using System.Net.Sockets;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ZR.Workflow.Helper
{
    /// <summary>
    /// 附件下载与纯文本抽取的静态工具。
    /// 抽取失败统一返回 null（调用方据此回退到仅列出文件名），非致命异常不向上冒泡。
    /// </summary>
    public static class WfAttachmentHelper
    {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        private static readonly HashSet<string> _textExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".csv", ".md", ".log", ".json", ".xml", ".html", ".htm", ".css", ".js", ".cs", ".sql"
        };

        // 视觉模型可访问的白名单主机（避免把内网地址泄露给第三方视觉模型）
        private static readonly HashSet<string> _allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "your-cdn-domain.com"
        };

        /// <summary>
        /// 判断 URL 是否属于服务端可安全下载的地址：仅 http/https，且回环、内网、链路本地、
        /// CGNAT、云元数据（169.254.169.254）、组播/保留地址一律拒绝。
        /// 附件 URL 来自用户提交的表单内容，下载/抽取前必须过此校验（fail-closed），防 SSRF。
        /// 域名主机会做"解析后校验实际 IP"，近似拦截 DNS rebinding；解析失败按拒绝处理。
        /// </summary>
        public static bool IsUrlSafeToFetch(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme != "https" && uri.Scheme != "http") return false;
                if (IsBlockedHost(uri.Host)) return false;
                // 域名主机：解析后逐个校验实际 IP，拦截"公网域名解析到内网地址"的 DNS rebinding
                var host = uri.Host.Trim('[', ']');
                if (!IPAddress.TryParse(host, out _))
                {
                    foreach (var addr in Dns.GetHostAddresses(host))
                    {
                        if (IsBlockedIp(addr)) return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 判断 URL 是否属于允许发给视觉模型的公开主机（防把内网地址泄露给第三方模型）。
        /// 在 <see cref="IsUrlSafeToFetch"/>（http/https + 非保留地址）基础上再加白名单主机限制。
        /// </summary>
        public static bool IsUrlAllowed(string url)
        {
            if (!IsUrlSafeToFetch(url)) return false;
            try
            {
                var host = new Uri(url).Host;
                return _allowedHosts.Count == 0 || _allowedHosts.Contains(host);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 主机名/IP 字面量是否属于禁止访问的保留地址（回环、内网、链路本地、云元数据、组播等）。
        /// </summary>
        private static bool IsBlockedHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return true;
            // Uri.Host 对 IPv6 字面量返回带方括号形式（如 [::1]），去掉后再解析
            var h = host.Trim().Trim('[', ']');
            if (h.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
            // IP 字面量：IPAddress.TryParse 同样接受十进制形式（2130706433 → 127.0.0.1），一并拦截
            return IPAddress.TryParse(h, out var ip) && IsBlockedIp(ip);
        }

        /// <summary>
        /// IP 是否落在禁止访问的保留地址段：回环、0.0.0.0/8、10/8、100.64/10(CGNAT)、
        /// 169.254/16（链路本地/云元数据）、172.16/12、192.168/16、组播/广播/保留段，
        /// 以及 IPv6 回环/链路本地/组播/唯一本地（fc00::/7）。
        /// </summary>
        private static bool IsBlockedIp(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip)) return true;
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = ip.GetAddressBytes();
                if (b[0] == 0 || b[0] == 10 || b[0] == 127) return true;   // 0.0.0.0/8、10.0.0.0/8、127.0.0.0/8
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true;  // 100.64.0.0/10 CGNAT
                if (b[0] == 169 && b[1] == 254) return true;                // 169.254.0.0/16 链路本地/云元数据
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;   // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;                // 192.168.0.0/16
                return b[0] >= 224;                                          // 224/4 组播、240/4 保留、255.255.255.255 广播
            }
            if (ip.IsIPv6LinkLocal || ip.IsIPv6Multicast) return true;
            var first = ip.GetAddressBytes()[0];
            return first == 0xFC || first == 0xFD; // fc00::/7 唯一本地地址
        }

        /// <summary>
        /// 下载附件并抽取纯文本；失败或格式不支持返回 null。
        /// </summary>
        public static async Task<string> ExtractTextAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            // SSRF 防护（fail-closed）：附件 URL 来自用户提交的表单内容，
            // 下载/抽取前必须拒绝回环、内网、链路本地、云元数据等保留地址（见 IsUrlSafeToFetch）。
            if (!IsUrlSafeToFetch(url)) return null;
            if (IsImageExtension(url)) return null; // 图片不抽文本
            if (_textExt.Contains(Path.GetExtension(url))) return await TryDownloadTextAsync(url).ConfigureAwait(false);
            if (string.Equals(Path.GetExtension(url), ".pdf", StringComparison.OrdinalIgnoreCase))
                return await TryExtractPdfAsync(url).ConfigureAwait(false);
            if (string.Equals(Path.GetExtension(url), ".docx", StringComparison.OrdinalIgnoreCase))
                return await TryExtractDocxAsync(url).ConfigureAwait(false);
            if (string.Equals(Path.GetExtension(url), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return await TryExtractXlsxAsync(url).ConfigureAwait(false);
            return null;
        }

        private static bool IsImageExtension(string url)
        {
            var ext = Path.GetExtension(url);
            return new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" }
                .Any(e => string.Equals(ext, e, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<string> TryDownloadTextAsync(string url)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
                return Encoding.UTF8.GetString(bytes).Trim();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "TryDownloadTextAsync: 文本下载失败，返回 null");
                return null;
            }
        }

        private static async Task<string> TryExtractPdfAsync(string url)
        {
            try
            {
                using var ms = await DownloadStreamAsync(url).ConfigureAwait(false);
                if (ms == null) return null;
                using PdfDocument doc = PdfDocument.Open(ms);
                var sb = new StringBuilder();
                foreach (Page page in doc.GetPages())
                {
                    sb.AppendLine(page.Text);
                }
                var text = sb.ToString().Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "TryExtractPdfAsync: PDF 抽取失败，返回 null");
                return null;
            }
        }

        private static async Task<string> TryExtractDocxAsync(string url)
        {
            try
            {
                using var ms = await DownloadStreamAsync(url).ConfigureAwait(false);
                if (ms == null) return null;
                using var doc = WordprocessingDocument.Open(ms, false);
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null) return null;
                var text = body.InnerText?.Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "TryExtractDocxAsync: DOCX 抽取失败，返回 null");
                return null;
            }
        }

        private static async Task<string> TryExtractXlsxAsync(string url)
        {
            try
            {
                using var ms = await DownloadStreamAsync(url).ConfigureAwait(false);
                if (ms == null) return null;
                using var doc = SpreadsheetDocument.Open(ms, false);
                var workbookPart = doc.WorkbookPart;
                if (workbookPart == null) return null;
                var shared = workbookPart.SharedStringTablePart?.SharedStringTable;
                var sb = new StringBuilder();
                foreach (var sheet in workbookPart.WorksheetParts)
                {
                    var sheetData = sheet.Worksheet.GetFirstChild<DocumentFormat.OpenXml.Spreadsheet.SheetData>();
                    if (sheetData == null) continue;
                    foreach (var row in sheetData.Elements<DocumentFormat.OpenXml.Spreadsheet.Row>())
                    {
                        var cells = row.Elements<DocumentFormat.OpenXml.Spreadsheet.Cell>().ToList();
                        var values = cells.Select(c =>
                        {
                            if (c.DataType != null && c.DataType.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.SharedString && shared != null)
                            {
                                var idx = int.Parse(c.InnerText);
                                return shared.Elements<DocumentFormat.OpenXml.Spreadsheet.SharedStringItem>().ElementAtOrDefault(idx)?.InnerText ?? c.InnerText;
                            }
                            return c.InnerText;
                        });
                        sb.AppendLine(string.Join("\t", values));
                    }
                }
                var text = sb.ToString().Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "TryExtractXlsxAsync: XLSX 抽取失败，返回 null");
                return null;
            }
        }

        private static async Task<MemoryStream> DownloadStreamAsync(string url)
        {
            var resp = await _http.GetAsync(url).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var ms = new MemoryStream();
            await resp.Content.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;
            return ms;
        }
    }
}
