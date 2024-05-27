using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ZR.Common.DynamicApiSimple;

class ApiFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        Type type = typeInfo.AsType();
        // 不能是非公开的、值类型、抽象类、泛型类或基元类型
        if (!type.IsPublic || type.IsValueType || type.IsAbstract || type.IsGenericType || type.IsPrimitive || string.IsNullOrWhiteSpace(type.Namespace)) return false;

        // 原生层或者实现IDynamicApiController（类）,[DynamicApi](接口)
        if ((!typeof(Controller).IsAssignableFrom(type) && typeof(ControllerBase).IsAssignableFrom(type)) || type.IsDefined(typeof(DynamicApiAttribute), true) || typeof(IDynamicApi).IsAssignableFrom(type))
        {
            // 如果是忽略的则跳过自定义的接口在前面会报错，所以必须在后面
            if (type.IsDefined(typeof(ApiExplorerSettingsAttribute), true) && type.GetCustomAttribute<ApiExplorerSettingsAttribute>(true).IgnoreApi)
            {
                return false;
            }
            return true;
        }
        return false;
    }
}
