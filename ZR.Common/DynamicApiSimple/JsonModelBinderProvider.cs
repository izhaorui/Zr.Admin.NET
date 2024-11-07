using System;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace ZR.Common.DynamicApiSimple;

public class JsonModelBinderProvider : IModelBinderProvider
{
    public IModelBinder GetBinder(ModelBinderProviderContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Metadata is { IsComplexType: true, IsCollectionType: false })
        {
            var fallbackBinder = new ComplexObjectModelBinderProvider().GetBinder(context);
            return new JsonModelBinder(fallbackBinder);
        }

        return null;
    }
}
