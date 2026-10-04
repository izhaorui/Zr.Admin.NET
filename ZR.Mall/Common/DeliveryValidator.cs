using System.Text.RegularExpressions;
using ZR.Mall.Model;

namespace ZR.Mall.Common
{
    /// <summary>
    /// 发货入参校验：物流信息格式 + 收货信息完整性。
    ///
    /// 为什么从 <c>OrderController</c> 抽到这里：① 校验规则是**业务约束**，不该埋在 HTTP 层；
    /// ② 单个发货与批量发货（Excel 导入）两条路径必须同一套规则，抽出来才做得到；
    /// ③ 纯函数、无依赖，可直接单元测试（见 <c>DeliveryValidatorTests</c>）。
    ///
    /// 规则与前端 <c>ShipmentsForm.vue</c> 的 rules 逐条对齐 —— 两边规则漂移会出现
    /// "前端放行、后端拒绝"的错位，运营完全无法理解。
    /// </summary>
    public static class DeliveryValidator
    {
        /// <summary>物流公司列宽（<c>oms_order.DeliveryCompany</c>）</summary>
        public const int MaxCompanyLength = 50;

        /// <summary>物流单号列宽（<c>oms_order.DeliveryNo</c>）</summary>
        public const int MaxNoLength = 64;

        /// <summary>单号最短长度：低于 5 位基本不可能是真实运单号</summary>
        public const int MinNoLength = 6;

        /// <summary>前端展示用的最长长度（比列宽短，与前端 maxlength 一致）</summary>
        public const int MaxNoDisplayLength = 40;

        /// <summary>快递单号只可能含字母、数字与短横线</summary>
        private static readonly Regex NoPattern = new("^[A-Za-z0-9-]+$", RegexOptions.Compiled);

        /// <summary>
        /// 校验发货时的物流信息。返回 null 表示通过，否则返回可读的拒绝原因。
        /// </summary>
        /// <param name="deliveryCompany">物流公司</param>
        /// <param name="deliveryNo">物流单号</param>
        public static string Validate(string deliveryCompany, string deliveryNo)
        {
            if (string.IsNullOrWhiteSpace(deliveryCompany))
            {
                return "请选择物流公司";
            }
            if (deliveryCompany.Length > MaxCompanyLength)
            {
                return "物流公司名称过长";
            }
            if (string.IsNullOrWhiteSpace(deliveryNo))
            {
                return "请输入物流单号";
            }
            if (deliveryNo.Length is < MinNoLength or > MaxNoDisplayLength)
            {
                return $"物流单号长度一般为 {MinNoLength}-{MaxNoDisplayLength} 位";
            }
            // 拦住空格/中文/引号等，避免写入脏数据（也挡住 SQL 注入面）
            if (!NoPattern.IsMatch(deliveryNo))
            {
                return "物流单号只能包含字母、数字和短横线";
            }
            return null;
        }

        /// <summary>
        /// 收货信息是否完整。
        ///
        /// 为什么不能只判 <c>AddressSnapshot == null</c>：下单能提交一个"字段全为 null 的空对象"，
        /// 那种情况能通过 null 检查 → 发货成功而买家根本收不到。必须逐项判空。
        /// </summary>
        public static bool IsAddressComplete(AddressSnapshot address)
        {
            return address != null
                && !string.IsNullOrWhiteSpace(address.UserName)
                && !string.IsNullOrWhiteSpace(address.Phone)
                && !string.IsNullOrWhiteSpace(address.Province)
                && !string.IsNullOrWhiteSpace(address.DetailAddress);
        }

        /// <summary>收货信息不完整时的统一提示（单个发货与批量发货共用，避免两处文案漂移）</summary>
        public const string AddressIncompleteMessage = "收货信息不完整（收件人/手机号/地区/详细地址有缺失）";
    }
}
