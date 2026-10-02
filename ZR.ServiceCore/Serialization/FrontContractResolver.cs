using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using ZR.Model.System;

namespace ZR.ServiceCore.Serialization
{
    /// <summary>
    /// C 端（公网）序列化契约：移除 <see cref="SysBase"/> 上的审计字段
    /// </summary>
    /// <remarks>
    /// <para>移除的是创建人 / 创建时间 / 更新人 / 更新时间 / 备注 —— 平台管理元数据，
    /// C 端一处都用不到。返回它们既把后台的管理语义漏给公网，也白白撑大报文。</para>
    ///
    /// <para><b>必须按「声明类型」判断，不能按字段名一刀切</b>：实体自己声明的同名字段是
    /// **业务字段**，必须保留 —— 例如 <c>OMSOrder.CreateTime</c> 是订单自己的「下单时间」，
    /// C 端订单详情要用它；按名字过滤会把它一起删掉，而且是**静默失效**
    ///（页面只显示空白，不报错，测试断言不到就发现不了）。</para>
    ///
    /// <para><b>只在公网 C 端宿主启用</b>：启动时赋给 <c>App.ResponseContractResolver</c>
    ///（见 ZR.Front.WebApi/Program.cs）。后台宿主不启用 —— 后台的商品/分类/支付流水列表
    /// 都要展示「添加时间」这类审计字段。</para>
    ///
    /// <para>注意生效位置是 <c>BaseController.GetJsonStr</c>，不是 MVC 的 AddNewtonsoftJson：
    /// `SUCCESS` / `ToResponse` 都自建 settings 序列化后以字符串返回，不经过输出格式化器。</para>
    /// </remarks>
    public class FrontContractResolver : CamelCasePropertyNamesContractResolver
    {
        /// <summary><see cref="SysBase"/> 上的审计字段（CLR 属性名）</summary>
        private static readonly HashSet<string> AuditFields = new(StringComparer.Ordinal)
        {
            nameof(SysBase.Create_by),
            nameof(SysBase.Create_time),
            nameof(SysBase.Update_by),
            nameof(SysBase.Update_time),
            nameof(SysBase.Remark),
        };

        /// <inheritdoc />
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);

            // 不从 SysBase 继承的类型没有任何审计字段，直接跳过（也免去反射开销）
            return typeof(SysBase).IsAssignableFrom(type)
                ? properties.Where(p => !IsAuditField(type, p)).ToList()
                : properties;
        }

        /// <summary>
        /// 是否为 <see cref="SysBase"/> 声明的审计字段。
        /// </summary>
        /// <remarks>
        /// 先按名字快速排除，再回查**实际声明类型**。
        /// 注意不能用 <see cref="JsonProperty.DeclaringType"/>：Newtonsoft 在它上面填的是
        /// 「当前被序列化的类型」而不是「属性的声明类型」，拿它跟 SysBase 比较永远不成立，
        /// 整段过滤会变成静默空操作。
        /// </remarks>
        private static bool IsAuditField(Type type, JsonProperty property)
        {
            var name = property.UnderlyingName;
            if (name == null || !AuditFields.Contains(name))
            {
                return false;
            }

            var declared = type.GetProperty(name,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            return declared?.DeclaringType == typeof(SysBase);
        }
    }
}
