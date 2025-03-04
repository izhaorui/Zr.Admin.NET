namespace ZR.Model.System.Vo
{
    /// <summary>
    /// 路由展示
    /// </summary>
    public class RouterVo
    {
        /// <summary>
        /// 当你一个路由下面的 children 声明的路由大于1个时，自动会变成嵌套的模式--如组件页面
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool AlwaysShow { get; set; }
        /// <summary>
        /// 组件地址
        /// </summary>
        private string component;
        /// <summary>
        /// 是否隐藏路由，当设置 true 的时候该路由不会再侧边栏出现
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool Hidden { get; set; }
        /// <summary>
        /// 路由名字
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 路由地址
        /// </summary>
        public string Path { get; set; }
        /// <summary>
        /// 重定向地址，当设置 noRedirect 的时候该路由在面包屑导航中不可被点击
        /// </summary>
        public string Redirect { get; set; }
        /// <summary>
        /// 路由参数: 如{ "key": "value"}
        /// </summary>
        public string Query { get; set; }
        /// <summary>
        /// 其他元素
        /// </summary>
        public Meta Meta { get; set; }
        /// <summary>
        /// 子路由
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<RouterVo> Children { get; set; }
        public string Component { get => component; set => component = value; }
    }

    public class Meta
    {
        /// <summary>
        /// 设置该路由在侧边栏和面包屑中展示的名字
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// 设置该路由的图标，对应路径src/assets/icons/svg
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// 设置为true，则不会被 <keep-alive>缓存
        /// </summary>
        public bool NoCache { get; set; }
        public string TitleKey { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public int IsNew { get; set; }
        /// <summary>
        /// icon颜色
        /// </summary>
        public string IconColor { get; set; }
        /// <summary>
        /// 权限字符
        /// </summary>
        public string Permi { get; set; }
        public Meta(string title, string icon)
        {
            Title = title;
            Icon = icon;
        }
        public Meta(string title, string icon, string path)
        {
            Title = title;
            Icon = icon;
            Link = path;
        }
        public Meta(string title, string icon, bool noCache, string titleKey, string path, DateTime addTime)
        {
            Title = title;
            Icon = icon;
            NoCache = noCache;
            TitleKey = titleKey;
            if (!string.IsNullOrEmpty(path) && (path.StartsWith(UserConstants.HTTP) || path.StartsWith(UserConstants.HTTPS)))
            {
                Link = path;
            }
            if (addTime != DateTime.MinValue)
            {
                TimeSpan ts = DateTime.Now - addTime;
                if (ts.Days < 7)
                {
                    IsNew = 1;
                }
            }
        }
    }
}
