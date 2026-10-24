using Infrastructure;
using Infrastructure.Attribute;
using Microsoft.AspNetCore.Hosting;

namespace ZR.ServiceCore.Email
{
    /// <summary>
    /// 邮件附件地址解析与安全校验。
    ///
    /// 背景：SendEmailDto.FileUrl 来自客户端，改造前直接当作服务器文件路径读取，
    /// 拥有 tool:email:send 权限的用户可以把 appsettings.json（数据库密码/JWT 密钥）等任意可读文件作为附件发出。
    ///
    /// 规则：
    /// 1) 只接受「上传接口返回的访问地址」（http/https URL 或 /xxx 形式的 Web 相对路径），
    ///    映射到 WebRoot（wwwroot）下的物理文件；
    /// 2) 一律拒绝服务器绝对路径（C:\...、\\\\server\\share、/etc/passwd 之外的盘符路径等）；
    /// 3) 路径规范化（Path.GetFullPath）后必须仍在上传根目录内，阻止 ../ 穿越；
    /// 4) 从文件系统根开始「逐级」解析真实路径：根目录自身、父目录、最终文件任一处是软链/联接点
    ///    都会被展开后再与真实根目录比较，防止 wwwroot 下某级目录软链指向根外绕过检查；
    /// 5) 可选剥离 Upload.uploadUrl 配置的访问前缀（如 /prod-api）。
    ///
    /// 残余风险（需本机文件系统写权限才能制造，上传接口无法创建）：硬链接、挂载点无法通过路径解析识别；
    /// 校验与实际打开文件之间仍存在极小的 TOCTOU 窗口。
    /// </summary>
    public interface IEmailAttachmentResolver
    {
        /// <summary>
        /// 把客户端提交的附件地址解析为受信任的本地文件绝对路径。
        /// 返回 false 时 error 为可直接展示给用户的原因。
        /// </summary>
        bool TryResolve(string source, out string fullPath, out string error);

        /// <summary>
        /// 解析内部落库的本地附件路径（email_log.FileUrl，绝对路径）。
        /// 仅允许真实上传根目录内的文件，供后台重发使用；不接受 URL 与根外路径。
        /// </summary>
        bool TryResolveStoredPath(string storedPath, out string fullPath, out string error);
    }

    [AppService(ServiceType = typeof(IEmailAttachmentResolver), ServiceLifetime = LifeTime.Singleton)]
    public class EmailAttachmentResolver : IEmailAttachmentResolver
    {
        private static readonly StringComparison PathComparison =
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        /// <summary>允许的附件根目录（WebRoot，通常是 wwwroot），用于计算字面相对路径</summary>
        private readonly string rootPath;

        /// <summary>根目录的真实路径（逐级解析软链后），所有安全检查都基于它比较</summary>
        private readonly string realRootPath;

        /// <summary>上传访问前缀（Upload.uploadUrl 的 Path 部分，如 /prod-api），映射前需剥离</summary>
        private readonly string urlPathPrefix;

        public EmailAttachmentResolver(IWebHostEnvironment environment)
        {
            var webRoot = environment?.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(environment?.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot");
            }

            rootPath = Path.GetFullPath(webRoot);
            // 记录根的「真实路径」：wwwroot 自身或其任意父级可能是软链，比较时必须用真实路径
            realRootPath = ResolveRealPath(rootPath);
            urlPathPrefix = ResolveUrlPathPrefix();
        }

        public bool TryResolve(string source, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(source))
            {
                error = "附件地址不能为空";
                return false;
            }

            var value = source.Trim();
            if (value.Contains('\0'))
            {
                error = "附件地址非法";
                return false;
            }

            string relative;
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                {
                    error = "附件地址格式不正确";
                    return false;
                }

                // 只取路径部分，主机名不参与映射（避免把任意 URL 变成文件读取）
                relative = uri.AbsolutePath;
            }
            else if (value.Contains("://", StringComparison.Ordinal))
            {
                error = "不支持的附件地址协议";
                return false;
            }
            else if (Path.IsPathRooted(value))
            {
                // /xxx 视为 Web 相对路径（上传接口返回的就是这种形式）；其余盘符/UNC 路径一律拒绝
                if (!value.StartsWith('/') || value.StartsWith("//"))
                {
                    error = "不允许使用服务器绝对路径作为附件";
                    return false;
                }

                relative = value;
            }
            else
            {
                // 裸相对路径按 Web 根处理
                relative = "/" + value;
            }

            try
            {
                relative = Uri.UnescapeDataString(relative);
            }
            catch
            {
                error = "附件地址编码不合法";
                return false;
            }

            relative = relative.Replace('\\', '/');
            // 只拦「..」路径段，避免误伤文件名中含连续点的合法文件（如 report..final.pdf）
            if (relative.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment == ".."))
            {
                error = "附件地址不允许包含上级目录";
                return false;
            }

            // 生产环境 Upload.uploadUrl 可能带前缀（如 /prod-api），需剥离后再映射物理目录。
            // 必须校验边界，避免 /prod-apix/... 被误当成 /prod-api 前缀剥离。
            if (!string.IsNullOrEmpty(urlPathPrefix)
                && relative.StartsWith(urlPathPrefix, PathComparison)
                && (relative.Length == urlPathPrefix.Length
                    || relative[urlPathPrefix.Length] == '/'))
            {
                relative = relative.Substring(urlPathPrefix.Length);
            }

            string candidate;
            try
            {
                var localRelative = relative.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                candidate = Path.GetFullPath(Path.Combine(rootPath, localRelative));
            }
            catch
            {
                error = "附件地址非法";
                return false;
            }

            if (!IsUnderRoot(candidate))
            {
                error = "附件地址不在允许的上传目录内";
                return false;
            }

            // 关键：逐级解析路径上的软链/联接点，而不只是最终文件。
            // 否则 wwwroot 下某一级目录是指向根外的软链时，最终文件本身不是链接，会被绕过。
            var realPath = ResolveRealPath(candidate);
            if (!IsUnderRealRoot(realPath))
            {
                error = "附件地址不在允许的上传目录内";
                return false;
            }

            if (!File.Exists(realPath))
            {
                error = "附件不存在或不可访问";
                return false;
            }

            fullPath = realPath;
            return true;
        }

        public bool TryResolveStoredPath(string storedPath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;

            if (string.IsNullOrWhiteSpace(storedPath))
            {
                error = "附件路径为空";
                return false;
            }

            if (storedPath.Contains('\0'))
            {
                error = "附件路径非法";
                return false;
            }

            string candidate;
            try
            {
                // 内部记录是物理绝对路径；非绝对路径按非法处理，避免与客户端地址混用
                if (!Path.IsPathRooted(storedPath))
                {
                    error = "附件路径非法";
                    return false;
                }

                candidate = Path.GetFullPath(storedPath);
            }
            catch
            {
                error = "附件路径非法";
                return false;
            }

            // 与客户端校验同一口径：逐级解析软链后必须仍在真实上传根目录内
            var realPath = ResolveRealPath(candidate);
            if (!IsUnderRealRoot(realPath))
            {
                error = "附件路径不在允许的上传目录内";
                return false;
            }

            if (!File.Exists(realPath))
            {
                error = "附件不存在或不可访问";
                return false;
            }

            fullPath = realPath;
            return true;
        }

        private bool IsUnderRoot(string path)
        {
            var rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            return path.StartsWith(rootWithSeparator, PathComparison);
        }

        private bool IsUnderRealRoot(string path)
        {
            if (string.Equals(path, realRootPath, PathComparison)) return true;

            var rootWithSeparator = realRootPath.EndsWith(Path.DirectorySeparatorChar)
                ? realRootPath
                : realRootPath + Path.DirectorySeparatorChar;

            return path.StartsWith(rootWithSeparator, PathComparison);
        }

        /// <summary>
        /// 解析完整真实路径：从文件系统根开始逐级拼接，遇到软链/联接点即替换为最终目标。
        /// 这样父目录软链、根目录自身软链都能被识别；不存在路径按字面返回（最终由 File.Exists 判定）。
        /// 说明：硬链接/挂载点无法通过路径解析识别，属已知残余风险（需要本机文件系统写权限才能制造）。
        /// </summary>
        private static string ResolveRealPath(string fullPath)
        {
            var path = Path.GetFullPath(fullPath);
            var pathRoot = Path.GetPathRoot(path) ?? string.Empty;
            var current = pathRoot;

            var segments = path.Substring(pathRoot.Length)
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                current = Path.Combine(current, segment);

                var target = ResolveLink(current);
                if (target != null)
                {
                    current = target;
                }
            }

            return Path.GetFullPath(current);
        }

        /// <summary>解析单个路径节点的软链最终目标；不是链接或不存在时返回 null</summary>
        private static string ResolveLink(string path)
        {
            try
            {
                FileSystemInfo info;
                if (Directory.Exists(path))
                {
                    info = new DirectoryInfo(path);
                }
                else if (File.Exists(path))
                {
                    info = new FileInfo(path);
                }
                else
                {
                    return null;
                }

                var target = info.ResolveLinkTarget(returnFinalTarget: true);
                return target == null ? null : Path.GetFullPath(target.FullName);
            }
            catch
            {
                // 无权限/链接损坏：按非链接处理，后续存在性判断会给出明确错误
                return null;
            }
        }

        private static string ResolveUrlPathPrefix()
        {
            try
            {
                var uploadUrl = App.OptionsSetting?.Upload?.UploadUrl;
                if (string.IsNullOrWhiteSpace(uploadUrl)) return string.Empty;

                if (!Uri.TryCreate(uploadUrl, UriKind.Absolute, out var uri)) return string.Empty;

                var path = uri.AbsolutePath.TrimEnd('/');
                return path == "/" ? string.Empty : path;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
