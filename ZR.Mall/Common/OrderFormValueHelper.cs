using System.Text.RegularExpressions;
using ZR.Infrastructure.Helper;
using ZR.Mall.Enum;
using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 买家填写的表单值（<see cref="OrderFormValue"/>）在后台的展示与导出处理。
    ///
    /// 为什么要单独一层：表单值是买家**自由输入**的，运营在后台配字段时无法保证填的
    /// 不是手机号 / 身份证 / 银行卡。而订单列表、详情、导出都会带上它，一旦明文出网或
    /// 落进 Excel，敏感数据就跟着扩散了。
    ///
    /// 四档口径（全项目只在这里定，别处不要再判一遍）：
    /// - 订单**列表**：不返回（<see cref="ClearItems"/>）——列表用不上，且是批量数据；
    /// - 订单**详情**：按字段敏感标记脱敏（<see cref="ToDisplay"/> 的 reveal=false）；
    /// - **揭示完整值**：走独立接口 + 独立权限 + 操作日志（见 OrderController.formValuesRaw），
    ///   脱敏规则与详情完全一致，只是把敏感字段换成明文，不做二次加工；
    /// - 订单**导出**：全部脱敏（<see cref="ToText"/>）——Excel 落地后会被转发、传附件。
    ///
    /// 敏感判定 = 字段标记 + **值兜底**，两者取或：
    /// 运营在商品表单里勾了「收集敏感信息」就脱敏；没勾也不安全 ——
    /// 只要值本身长得像身份证或银行卡，一律打码。否则"运营忘勾"就等于把敏感数据明文摆出来，
    /// 而这恰恰是最常见的出错方式。
    /// </summary>
    public static class OrderFormValueHelper
    {
        /// <summary>身份证（15 位 / 18 位，末位可为 X），与 MaskUtil 的识别口径一致</summary>
        private static readonly Regex IdCardRegex = new(@"(?<!\d)\d{15}(?!\d)|(?<!\d)\d{17}[\dXx](?!\d)", RegexOptions.Compiled);

        /// <summary>银行卡（16~19 位纯数字），与 MaskUtil 的识别口径一致</summary>
        private static readonly Regex BankCardRegex = new(@"(?<!\d)\d{16,19}(?!\d)", RegexOptions.Compiled);

        /// <summary>导出/文本化时分隔多个字段用的符号</summary>
        private const char FieldSeparator = '；';

        private const char PairSeparator = '：';

        /// <summary>
        /// 订单项的表单值**就地**按敏感标记改写（订单详情用；实体刚查出来、未被跟踪，改动安全）。
        ///
        /// 规则：敏感字段打码，非敏感字段原样 —— 但值命中身份证/银行卡格式的仍打码（忘勾时的兜底）。
        /// </summary>
        public static void ApplyDisplayPolicy(IEnumerable<OMSOrderItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item.FormValues == null || item.FormValues.Count == 0)
                {
                    continue;
                }

                foreach (var value in item.FormValues)
                {
                    if (value == null)
                    {
                        continue;
                    }

                    value.Value = MaskForDisplay(value);
                }
            }
        }

        /// <summary>
        /// 单个字段按展示策略取值：敏感 → 按类型打码；非敏感 → 只做身份证/银行卡兜底。
        /// </summary>
        public static string MaskForDisplay(OrderFormValue value)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.Value))
            {
                return value?.Value;
            }

            return value.Sensitive == true
                ? MaskAll(value.Type, value.Value)
                : MaskIdentity(value.Value);
        }

        /// <summary>
        /// 清空订单项的表单值：列表接口用。
        /// 列表一次 10~20 单，每行都带 20 个字段的 JSON 快照纯属浪费，且批量带明文风险更高。
        /// </summary>
        public static void ClearItems(IEnumerable<OMSOrderItem> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item.FormValues != null && item.FormValues.Count > 0)
                {
                    item.FormValues.Clear();
                }
            }
        }

        /// <summary>
        /// 清空订单项的表单值：<see cref="OMSOrderItemDto"/>（列表接口走 DTO，与实体是两套类型）。
        /// </summary>
        public static void ClearItems(IEnumerable<OMSOrderItemDto> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item.FormValues != null && item.FormValues.Count > 0)
                {
                    item.FormValues.Clear();
                }
            }
        }

        /// <summary>
        /// 取出需要揭示的**敏感字段明文**（揭示接口用）：非敏感字段不返回 ——
        /// 它的值在详情里本来就是明文，再传一遍只是白白扩大数据面。
        /// </summary>
        public static List<OrderFormValue> RevealSensitive(IEnumerable<OrderFormValue> values)
        {
            if (values == null)
            {
                return new List<OrderFormValue>();
            }

            return values
                .Where(x => x != null)
                .Where(x => x.Sensitive == true)
                .Select(x => new OrderFormValue
                {
                    Field = x.Field,
                    Label = x.Label,
                    Type = x.Type,
                    Sensitive = true,
                    // 揭示就是给运营拿去发货的（接收手机号、接收账号），这里不做任何打码
                    Value = x.Value
                })
                .ToList();
        }

        /// <summary>
        /// 拼成导出用的一行文本：<c>接收手机号：138****8000；接收账号：abc123</c>。
        /// 返回 null 表示没有任何可导出的值，Excel 里留空而不是写一串"无"。
        /// </summary>
        public static string ToText(IEnumerable<OrderFormValue> values)
        {
            if (values == null)
            {
                return null;
            }

            var parts = values
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Value))
                .Select(x => $"{x.Label}{PairSeparator}{MaskAll(x.Type, x.Value)}")
                .ToList();

            return parts.Count == 0 ? null : string.Join(FieldSeparator, parts);
        }

        /// <summary>
        /// 按类型脱敏单个值：手机号、邮箱打码，其余只打掉身份证与银行卡。
        ///
        /// 为什么不用 <see cref="MaskUtil.MaskSensitiveText"/>：它会连**金额**一起脱敏
        /// （"100元" → "****元"），而虚拟商品最典型的表单值就是充值面额、预约金额，
        /// 被打码后运营就没法发货了。
        /// </summary>
        public static string MaskAll(string type, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var normalized = ProductFormType.Normalize(type);
            return normalized switch
            {
                ProductFormType.Phone => MaskUtil.MaskPhone(value.Trim()),
                ProductFormType.Email => MaskUtil.MaskEmail(value.Trim()),
                _ => MaskIdentity(value)
            };
        }

        /// <summary>自由文本兜底：只打掉身份证与银行卡，其余原样保留</summary>
        private static string MaskIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            var text = IdCardRegex.Replace(value, m => MaskUtil.MaskIdCard(m.Value));
            return BankCardRegex.Replace(text, m => MaskUtil.MaskBankCard(m.Value));
        }
    }
}
