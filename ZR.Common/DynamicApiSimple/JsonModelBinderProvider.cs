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

        if (context.Metadata.IsComplexType)
        {
            var fallbackBinder = new ComplexTypeModelBinderProvider().GetBinder(context);
            return new JsonModelBinder(fallbackBinder);
        }

        return null;
    }
}