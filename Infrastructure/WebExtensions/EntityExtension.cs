
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Reflection;

namespace Infrastructure
{
    public static class EntityExtension
    {
        public static TSource ToCreate<TSource>(this TSource source, HttpContext? context = null)
        {
            var types = source?.GetType();
            if (types == null || context == null) return source;
            BindingFlags flag = BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance;

            types.GetProperty("CreateTime", flag)?.SetValue(source, DateTime.Now, null);
            types.GetProperty("AddTime", flag)?.SetValue(source, DateTime.Now, null);
            types.GetProperty("CreateBy", flag)?.SetValue(source, context.GetName(), null);
            types.GetProperty("Create_by", flag)?.SetValue(source, context.GetName(), null);
            //types.GetProperty("UserId", flag)?.SetValue(source, context.GetUId(), null);
            // 安全设置 DeptId，处理可空类型及类型转换（context.GetDeptId() 返回 long）
            var deptProp = types.GetProperty("DeptId", flag);
            if (deptProp != null)
            {
                var deptVal = context.GetDeptId();
                // 如果属性是可空类型则取其底层类型
                var propType = deptProp.PropertyType;
                var targetType = Nullable.GetUnderlyingType(propType) ?? propType;

                // 如果是可空并且值为0，默认不设置（保持null）
                object? setValue = null;
                if (!(Nullable.GetUnderlyingType(propType) != null && deptVal == 0))
                {
                    try
                    {
                        setValue = Convert.ChangeType(deptVal, targetType);
                    }
                    catch
                    {
                        // 回退策略：如果目标是 int，尝试强转
                        if (targetType == typeof(int)) setValue = (int)deptVal;
                        else if (targetType == typeof(long)) setValue = deptVal;
                        else setValue = deptVal;
                    }
                }

                deptProp.SetValue(source, setValue, null);
            }

            return source;
        }

        public static TSource ToUpdate<TSource>(this TSource source, HttpContext? context = null)
        {
            var types = source?.GetType();
            if (types == null || context == null) return source;
            BindingFlags flag = BindingFlags.Public | BindingFlags.IgnoreCase | BindingFlags.Instance;

            types.GetProperty("UpdateTime", flag)?.SetValue(source, DateTime.Now, null);
            types.GetProperty("Update_time", flag)?.SetValue(source, DateTime.Now, null);
            types.GetProperty("UpdateBy", flag)?.SetValue(source, context.GetName(), null);
            types.GetProperty("Update_by", flag)?.SetValue(source, context.GetName(), null);

            return source;
        }

    }
}
