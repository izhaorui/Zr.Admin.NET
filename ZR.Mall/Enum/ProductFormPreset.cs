namespace ZR.Mall.Enum
{
    /// <summary>
    /// 预设的下单字段（后台「表单配置」页勾选这些即可，不做拖拽式表单设计器）。
    ///
    /// 为什么有预设：虚拟商品要填的东西高度雷同（接收手机号 / 接收账号 / 备注…），
    /// 让运营从零配字段既费劲又容易配错（类型、必填、长度全要自己想）。
    /// 勾选预设 = 一次点选就得到一份可用配置，改个标题、加个候选项即可。
    ///
    /// 预设只是**快捷方式**，不构成限制：后台仍可自由增删字段（校验见 ProductFormValidator）。
    /// 预设值随 <c>shopping/product/formMeta</c> 一起下发，与服务端校验口径同源。
    ///
    /// Key 一经使用就不要再改：它是下单回传与订单项快照的匹配依据（改了等于换了个字段）。
    /// </summary>
    public static class ProductFormPreset
    {
        /// <summary>接收手机号（虚拟商品最常见：充值/发卡发到哪个号）</summary>
        public const string ReceiverPhone = "receiverPhone";

        /// <summary>接收账号（充值类：游戏账号 / 会员号 / 银行卡号）</summary>
        public const string ReceiverAccount = "receiverAccount";

        /// <summary>接收人姓名</summary>
        public const string ReceiverName = "receiverName";

        /// <summary>微信号</summary>
        public const string Wechat = "wechat";

        /// <summary>接收邮箱（发兑换码、下载链接常用）</summary>
        public const string Email = "email";

        /// <summary>QQ 号</summary>
        public const string Qq = "qq";

        /// <summary>预约日期（到店服务、预约配送）</summary>
        public const string AppointmentDate = "appointmentDate";

        /// <summary>买家备注（长文本）</summary>
        public const string Remark = "remark";

        /// <summary>
        /// 预设清单：key、显示名、字段类型、默认是否必填、输入框占位提示。
        /// 默认必填按"业务上确实需要"来定（如接收手机号 / 账号），备注类默认选填。
        /// </summary>
        public static readonly Preset[] Presets =
        {
            new(ReceiverPhone, "接收手机号", ProductFormType.Phone, true, "请输入接收手机号"),
            new(ReceiverAccount, "接收账号", ProductFormType.Text, true, "请输入接收账号"),
            new(ReceiverName, "接收人姓名", ProductFormType.Text, true, "请输入接收人姓名"),
            new(Wechat, "微信号", ProductFormType.Text, false, "请输入微信号"),
            new(Email, "接收邮箱", ProductFormType.Email, false, "请输入接收邮箱"),
            new(Qq, "QQ 号", ProductFormType.Number, false, "请输入 QQ 号"),
            new(AppointmentDate, "预约日期", ProductFormType.Date, false, ""),
            new(Remark, "买家备注", ProductFormType.Textarea, false, "如有特殊要求请在此说明"),
        };

        /// <summary>
        /// 预设定义（<see cref="Presets"/> 的元素类型）。
        /// </summary>
        /// <param name="Key">字段键，下单回传与快照匹配用，创建后不建议再改</param>
        /// <param name="Label">默认显示名，勾选后运营可改</param>
        /// <param name="Type">字段类型，取值见 <see cref="ProductFormType"/></param>
        /// <param name="Required">默认是否必填，勾选后运营可改</param>
        /// <param name="Placeholder">输入框占位提示，可改</param>
        public readonly record struct Preset(
            string Key,
            string Label,
            string Type,
            bool Required,
            string Placeholder);
    }
}
