//using Microsoft.AspNetCore.Http;
//using Snowflake.Core;
using System;

namespace ZR.Admin.WebApi.Extensions
{
    public static class EntityExtension
    {
        public static TSource ToCreate<TSource>(this TSource source, HttpContext? context = null)
        {
            var types = source?.GetType();
            if (types == null) return source;

            types.GetProperty("CreateTime")?.SetValue(source, DateTime.Now, null);
            types.GetProperty("AddTime")?.SetValue(source, DateTime.Now, null);
            types.GetProperty("UpdateTime")?.SetValue(source, DateTime.Now, null);
            if (types.GetProperty("Create_by") != null && context != null)
            {
                types.GetProperty("Create_by")?.SetValue(source, context.GetName(), null);
            }
            if (types.GetProperty("Create_By") != null && context != null)
            {
                types.GetProperty("Create_By")?.SetValue(source, context.GetName(), null);
            }
            if (types.GetProperty("CreateBy") != null && context != null)
            {
                types.GetProperty("CreateBy")?.SetValue(source, context.GetName(), null);
            }
            if (types.GetProperty("UserId") != null && context != null)
            {
                types.GetProperty("UserId")?.SetValue(source, context.GetUId(), null);
            }
            return source;
        }

        public static TSource ToUpdate<TSource>(this TSource source, HttpContext? context = null)
        {
            var types = source?.GetType();
            if (types == null) return source;

            types.GetProperty("UpdateTime")?.SetValue(source, DateTime.Now, null);
            types.GetProperty("Update_time")?.SetValue(source, DateTime.Now, null);

            types.GetProperty("UpdateBy")?.SetValue(source,context.GetName(), null);
            types.GetProperty("Update_by")?.SetValue(source, context.GetName(), null);

            return source;
        }

    }
}
