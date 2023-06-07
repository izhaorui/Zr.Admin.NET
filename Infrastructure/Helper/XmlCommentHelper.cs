using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.XPath;

namespace Infrastructure.Helper
{
    public class XmlCommentHelper
    {
        private static Regex RefTagPattern = new Regex(@"<(see|paramref) (name|cref)=""([TPF]{1}:)?(?<display>.+?)"" ?/>");
        private static Regex CodeTagPattern = new Regex(@"<c>(?<display>.+?)</c>");
        private static Regex ParaTagPattern = new Regex(@"<para>(?<display>.+?)</para>", RegexOptions.Singleline);

        List<XPathNavigator> navigators = new List<XPathNavigator>();

        /// <summary>
        /// 从当前dll文件中加载所有的xml文件
        /// </summary>
        public void LoadAll()
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory());
            foreach (var file in files)
            {
                if (string.Equals(Path.GetExtension(file), ".xml", StringComparison.OrdinalIgnoreCase))
                {
                    Load(file);
                }
            }
        }
        /// <summary>
        /// 从xml中加载
        /// </summary>
        /// <param name="xmls"></param>
        public void LoadXml(params string[] xmls)
        {
            foreach (var xml in xmls)
            {
                Load(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
            }
        }
        /// <summary>
        /// 从文件中加载
        /// </summary>
        /// <param name="xmlFiles"></param>
        public void Load(params string[] xmlFiles)
        {
            foreach (var xmlFile in xmlFiles)
            {
                var doc = new XPathDocument(xmlFile);
                navigators.Add(doc.CreateNavigator());

                //Console.WriteLine("加载xml文件=" + xmlFile);
            }
        }
        /// <summary>
        /// 从流中加载
        /// </summary>
        /// <param name="streams"></param>
        public void Load(params Stream[] streams)
        {
            foreach (var stream in streams)
            {
                var doc = new XPathDocument(stream);
                navigators.Add(doc.CreateNavigator());
            }
        }

        /// <summary>
        /// 读取类型中的注释
        /// </summary>
        /// <param name="type">类型</param>
        /// <param name="xPath">注释路径</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetTypeComment(Type type, string xPath = "summary", bool humanize = true)
        {
            var typeMemberName = GetMemberNameForType(type);
            return GetComment(typeMemberName, xPath, humanize);
        }
        /// <summary>
        /// 读取字段或者属性的注释
        /// </summary>
        /// <param name="fieldOrPropertyInfo">字段或者属性</param>
        /// <param name="xPath">注释路径</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetFieldOrPropertyComment(MemberInfo fieldOrPropertyInfo, string xPath = "summary", bool humanize = true)
        {
            var fieldOrPropertyMemberName = GetMemberNameForFieldOrProperty(fieldOrPropertyInfo);
            return GetComment(fieldOrPropertyMemberName, xPath, humanize);
        }
        /// <summary>
        /// 读取方法中的注释
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <param name="xPath">注释路径</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetMethodComment(MethodInfo methodInfo, string xPath = "summary", bool humanize = true)
        {
            var methodMemberName = GetMemberNameForMethod(methodInfo);
            return GetComment(methodMemberName, xPath, humanize);
        }
        /// <summary>
        /// 读取方法中的返回值注释
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetMethodReturnComment(MethodInfo methodInfo, bool humanize = true)
        {
            return GetMethodComment(methodInfo, "returns", humanize);
        }
        /// <summary>
        /// 读取参数的注释
        /// </summary>
        /// <param name="parameterInfo">参数</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetParameterComment(ParameterInfo parameterInfo, bool humanize = true)
        {
            if (!(parameterInfo.Member is MethodInfo methodInfo)) return string.Empty;

            var methodMemberName = GetMemberNameForMethod(methodInfo);
            return GetComment(methodMemberName, $"param[@name='{parameterInfo.Name}']", humanize);
        }
        /// <summary>
        /// 读取方法的所有参数的注释
        /// </summary>
        /// <param name="methodInfo">方法</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public Dictionary<string, string> GetParameterComments(MethodInfo methodInfo, bool humanize = true)
        {
            var parameterInfos = methodInfo.GetParameters();
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var parameterInfo in parameterInfos)
            {
                dict[parameterInfo.Name] = GetParameterComment(parameterInfo, humanize);
            }
            return dict;
        }
        /// <summary>
        /// 读取指定名称节点的注释
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="xPath">注释路径</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetComment(string name, string xPath, bool humanize = true)
        {
            foreach (var _xmlNavigator in navigators)
            {
                var typeSummaryNode = _xmlNavigator.SelectSingleNode($"/doc/members/member[@name='{name}']/{xPath.Trim('/', '\\')}");

                if (typeSummaryNode != null)
                {
                    return humanize ? Humanize(typeSummaryNode.InnerXml) : typeSummaryNode.InnerXml;
                }
            }

            return string.Empty;
        }
        /// <summary>
        /// 读取指定节点的summary注释
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetSummary(string name, bool humanize = true)
        {
            return GetComment(name, "summary", humanize);
        }
        /// <summary>
        /// 读取指定节点的example注释
        /// </summary>
        /// <param name="name">节点名称</param>
        /// <param name="humanize">可读性优化(比如：去掉xml标记)</param>
        /// <returns></returns>
        public string GetExample(string name, bool humanize = true)
        {
            return GetComment(name, "example", humanize);
        }
        /// <summary>
        /// 获取方法的节点名称
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public string GetMemberNameForMethod(MethodInfo method)
        {
            var builder = new StringBuilder("M:");

            builder.Append(QualifiedNameFor(method.DeclaringType));
            builder.Append($".{method.Name}");

            var parameters = method.GetParameters();
            if (parameters.Any())
            {
                var parametersNames = parameters.Select(p =>
                {
                    return p.ParameterType.IsGenericParameter
                        ? $"`{p.ParameterType.GenericParameterPosition}"
                        : QualifiedNameFor(p.ParameterType, expandGenericArgs: true);
                });
                builder.Append($"({string.Join(",", parametersNames)})");
            }

            return builder.ToString();
        }
        /// <summary>
        /// 获取类型的节点名称
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GetMemberNameForType(Type type)
        {
            var builder = new StringBuilder("T:");
            builder.Append(QualifiedNameFor(type));

            return builder.ToString();
        }
        /// <summary>
        /// 获取字段或者属性的节点名称
        /// </summary>
        /// <param name="fieldOrPropertyInfo"></param>
        /// <returns></returns>
        public string GetMemberNameForFieldOrProperty(MemberInfo fieldOrPropertyInfo)
        {
            var builder = new StringBuilder(((fieldOrPropertyInfo.MemberType & MemberTypes.Field) != 0) ? "F:" : "P:");
            builder.Append(QualifiedNameFor(fieldOrPropertyInfo.DeclaringType));
            builder.Append($".{fieldOrPropertyInfo.Name}");

            return builder.ToString();
        }

        private string QualifiedNameFor(Type type, bool expandGenericArgs = false)
        {
            if (type.IsArray)
                return $"{QualifiedNameFor(type.GetElementType(), expandGenericArgs)}[]";

            var builder = new StringBuilder();

            if (!string.IsNullOrEmpty(type.Namespace))
                builder.Append($"{type.Namespace}.");

            if (type.IsNested)
            {
                builder.Append($"{string.Join(".", GetNestedTypeNames(type))}.");
            }

            if (type.IsConstructedGenericType && expandGenericArgs)
            {
                var nameSansGenericArgs = type.Name.Split('`').First();
                builder.Append(nameSansGenericArgs);

                var genericArgsNames = type.GetGenericArguments().Select(t =>
                {
                    return t.IsGenericParameter
                        ? $"`{t.GenericParameterPosition}"
                        : QualifiedNameFor(t, true);
                });

                builder.Append($"{{{string.Join(",", genericArgsNames)}}}");
            }
            else
            {
                builder.Append(type.Name);
            }

            return builder.ToString();
        }
        private IEnumerable<string> GetNestedTypeNames(Type type)
        {
            if (!type.IsNested || type.DeclaringType == null) yield break;

            foreach (var nestedTypeName in GetNestedTypeNames(type.DeclaringType))
            {
                yield return nestedTypeName;
            }

            yield return type.DeclaringType.Name;
        }
        private string Humanize(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            //Call DecodeXml at last to avoid entities like &lt and &gt to break valid xml      
            text = NormalizeIndentation(text);
            text = HumanizeRefTags(text);
            text = HumanizeCodeTags(text);
            text = HumanizeParaTags(text);
            text = DecodeXml(text);
            return text;
        }
        private string NormalizeIndentation(string text)
        {
            string[] lines = text.Split('\n');
            string padding = GetCommonLeadingWhitespace(lines);

            int padLen = padding == null ? 0 : padding.Length;

            // remove leading padding from each line
            for (int i = 0, l = lines.Length; i < l; ++i)
            {
                string line = lines[i].TrimEnd('\r'); // remove trailing '\r'

                if (padLen != 0 && line.Length >= padLen && line.Substring(0, padLen) == padding)
                    line = line.Substring(padLen);

                lines[i] = line;
            }

            // remove leading empty lines, but not all leading padding
            // remove all trailing whitespace, regardless
            return string.Join("\r\n", lines.SkipWhile(x => string.IsNullOrWhiteSpace(x))).TrimEnd();
        }
        private string GetCommonLeadingWhitespace(string[] lines)
        {
            if (null == lines)
                throw new ArgumentException("lines");

            if (lines.Length == 0)
                return null;

            string[] nonEmptyLines = lines
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (nonEmptyLines.Length < 1)
                return null;

            int padLen = 0;

            // use the first line as a seed, and see what is shared over all nonEmptyLines
            string seed = nonEmptyLines[0];
            for (int i = 0, l = seed.Length; i < l; ++i)
            {
                if (!char.IsWhiteSpace(seed, i))
                    break;

                if (nonEmptyLines.Any(line => line[i] != seed[i]))
                    break;

                ++padLen;
            }

            if (padLen > 0)
                return seed.Substring(0, padLen);

            return null;
        }
        private string HumanizeRefTags(string text)
        {
            return RefTagPattern.Replace(text, (match) => match.Groups["display"].Value);
        }
        private string HumanizeCodeTags(string text)
        {
            return CodeTagPattern.Replace(text, (match) => "{" + match.Groups["display"].Value + "}");
        }
        private string HumanizeParaTags(string text)
        {
            return ParaTagPattern.Replace(text, (match) => "<br>" + match.Groups["display"].Value);
        }
        private string DecodeXml(string text)
        {
            return System.Net.WebUtility.HtmlDecode(text);
        }
    }
}
