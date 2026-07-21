using System;
using System.Collections.Generic;

namespace ZR.Infrastructure.Helper
{
    /// <summary>
    /// 脱敏扩展方法：把“是否拥有敏感权限 + 循环/赋值 + 脱敏写回”统一收口，
    /// 消除各处重复的 foreach + if 样板代码。
    /// 当 hasPerm 为 true（拥有敏感权限）时不做任何处理，直接返回原值。
    /// </summary>
    public static class MaskExtensions
    {
        /// <summary>
        /// 对列表每个元素的指定字段脱敏
        /// </summary>
        /// <typeparam name="T">元素类型</typeparam>
        /// <param name="list">集合</param>
        /// <param name="hasPerm">是否拥有查看明文权限（true=不脱敏）</param>
        /// <param name="getter">读取字段</param>
        /// <param name="setter">写回字段</param>
        /// <param name="mask">脱敏方法</param>
        public static void MaskField<T>(this List<T> list, bool hasPerm,
            Func<T, string> getter, Action<T, string> setter, Func<string, string> mask)
        {
            if (hasPerm || list == null) return;
            foreach (var item in list)
            {
                setter(item, mask(getter(item)));
            }
        }

        /// <summary>
        /// 对单个对象的指定字段脱敏
        /// </summary>
        public static void MaskField<T>(this T item, bool hasPerm,
            Func<T, string> getter, Action<T, string> setter, Func<string, string> mask)
        {
            if (hasPerm || item == null) return;
            setter(item, mask(getter(item)));
        }
    }
}
