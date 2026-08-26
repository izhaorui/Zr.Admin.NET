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
        /// 判断 URL 是否属于允许发给视觉模型的公开主机。
        /// </summary>
        public static bool IsUrlAllowed(string url)
        {
            try
            {
                var uri = new Uri(url);
                if (uri.Scheme != "https" && uri.Scheme != "http") return false;
                var host = uri.Host;
                if (host == "localhost" || host == "127.0.0.1" || host.StartsWith("192.168.") || host.StartsWith("10."))
                    return false;
                return _allowedHosts.Count == 0 || _allowedHosts.Contains(host);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 下载附件并抽取纯文本；失败或格式不支持返回 null。
        /// </summary>
        public static async Task<string> ExtractTextAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
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
