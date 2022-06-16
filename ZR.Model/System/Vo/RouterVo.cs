using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System.Vo
{
    /// <summary>
    /// 路由展示
    /// </summary>
    public class RouterVo
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool AlwaysShow { get; set; }
        private string component;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool Hidden { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public string Redirect { get; set; }
        public Meta Meta { get; set; }
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
        public Meta(string title, string icon, bool noCache)
        {
            Title = title;
            Icon = icon;
            NoCache = noCache;
        }
        public Meta(string title, string icon, bool noCache, string titleKey, string path)
        {
            Title = title;
            Icon = icon;
            NoCache = noCache;
            TitleKey = titleKey;
            Link = path;
        }
    }
}
