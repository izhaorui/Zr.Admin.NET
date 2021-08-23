using AspectCore.DynamicProxy;
using System.Threading.Tasks;
using ZR.Repository.DbProvider;

namespace ZR.Repository.Interceptor
{

    public class SqlLogInterceptorAttribute : AbstractInterceptorAttribute
    {
        public override Task Invoke(AspectContext context, AspectDelegate next)
        {
            global::System.Console.WriteLine("");
            return next(context);
        }
    }
}
