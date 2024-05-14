using Microsoft.Extensions.DependencyModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Infrastructure.Helper
{
    public static class AssemblyUtils
    {
        /// <summary>
        /// 获取应用中的所有程序集
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Assembly> GetAssemblies()
        {
            var compilationLibrary = DependencyContext.Default
              .CompileLibraries
              .Where(x => !x.Serviceable && x.Type == "project")
              .ToList();
            return compilationLibrary.Select(p => Assembly.Load(new AssemblyName(p.Name)));
        }

        /// <summary>
        /// 获取应用中的所有Type
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Type> GetAllTypes()
        {
            var assemblies = GetAssemblies();
            return assemblies.SelectMany(p => p.GetTypes());
        }
        //获取泛型类名
        public static Type GetGenericTypeByName(string genericTypeName)
        {
            Type type = null;
            foreach (var assembly in GetAssemblies())
            {
                var baseType = assembly.GetTypes()
                    .FirstOrDefault(t => t.IsGenericType &&
                                         t.GetGenericTypeDefinition().Name.Equals(genericTypeName, StringComparison.Ordinal));
                if (baseType != null)
                {
                    return baseType?.GetGenericTypeDefinition();
                }
                

            }

            return type;
        }
        public static bool IsDerivedFromGenericBaseRepository(this Type? type, Type genericBase)
        {
            while (type != null && type != typeof(object))
            {
                var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
                if (genericBase == cur)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }
}
