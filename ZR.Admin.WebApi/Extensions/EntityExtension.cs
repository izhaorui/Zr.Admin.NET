using Microsoft.AspNetCore.Http;
using Snowflake.Core;
using System;

namespace ZR.Admin.WebApi.Extensions
{
    public static class EntityExtension
    {
        public static TSource ToCreate<TSource>(this TSource source, HttpContext context = null)
        {
            var types = source.GetType();

            if (types.GetProperty("CreateTime") != null)
            {
                types.GetProperty("CreateTime").SetValue(source, DateTime.Now, null);
            }
            if (types.GetProperty("AddTime") != null)
            {
                types.GetProperty("AddTime").SetValue(source, DateTime.Now, null);
            }
            if (types.GetProperty("UpdateTime") != null)
            {
                types.GetProperty("UpdateTime").SetValue(source, DateTime.Now, null);
            }
            if (types.GetProperty("Create_by") != null && context != null)
            {
                types.GetProperty("Create_by").SetValue(source, context.GetName(), null);
            }
            if (types.GetProperty("UserId") != null && context != null)
            {
                types.GetProperty("UserId").SetValue(source, context.GetUId(), null);
            }
            return source;
        }

        public static TSource ToUpdate<TSource>(this TSource source, HttpContext context = null)
        {
            var types = source.GetType();

            if (types.GetProperty("UpdateTime") != null)
            {
                types.GetProperty("UpdateTime").SetValue(source, DateTime.Now, null);
            }
            if (types.GetProperty("Update_time") != null)
            {
                types.GetProperty("Update_time").SetValue(source, DateTime.Now, null);
            }

            if (types.GetProperty("UpdateBy") != null)
            {
                types.GetProperty("UpdateBy").SetValue(source,context.GetName(), null);
            }
            if (types.GetProperty("Update_by") != null)
            {
                types.GetProperty("Update_by").SetValue(source, context.GetName(), null);
            }

            return source;
        }

    }
}
