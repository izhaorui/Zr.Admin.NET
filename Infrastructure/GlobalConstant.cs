using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    /// <summary>
    /// 全局静态常量
    /// </summary>
    public class GlobalConstant
    {
        /// <summary>
        /// 管理员权限
        /// </summary>
        public static string AdminPerm = "*:*:*";
        /// <summary>
        /// 管理员角色
        /// </summary>
        public static string AdminRole = "admin";
        /// <summary>
        /// 开发版本API映射路径
        /// </summary>
        public static string DevApiProxy = "/dev-api/";
        /// <summary>
        /// 用户权限缓存key
        /// </summary>
        public static string UserPermKEY = "CACHE-USER-PERM";

        /// <summary>
        /// 欢迎语
        /// </summary>
        public static string[] WelcomeMessages = new string[] {
            "祝你开心每一天！",
            "忙碌了一周，停一停脚步！",
            "世间美好，与你环环相扣！",
            "永远相信美好的事情即将发生！",
            "每一天，遇见更好的自己！",
            "保持热爱，奔赴山海！",
            "生活明朗，万物可爱！",
            "愿每一天醒来都是美好的开始！",
            "没有希望的地方，就没有奋斗！",
            "我最珍贵的时光都行走在路上！",
            "成功，往往住在失败的隔壁！",
            "人只要不失去方向，就不会失去自己！",
            "每条堵住的路，都有一个出口！",
            "没有谁能击垮你，除非你自甘堕落！",
            "微笑着的人并非没有痛苦！",
            "生活变的再糟糕，也不妨碍我变得更好！",
            "你要悄悄努力，然后惊艳众人！",
            "人与人之间最大的信任是精诚相见",
            "人生就像爬坡，要一步一步来。",
            "今天的目标完成了吗？",
            "高效工作，告别996",
            "销售是从别人拒绝开始的！"
        };
    }
}
