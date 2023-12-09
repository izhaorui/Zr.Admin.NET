using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ZR.Infrastructure.ModelBinder
{
    public class CommaSeparatedArrayModelBinder<T> : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
            {
                return Task.CompletedTask;
            }

            var value = valueProviderResult.FirstValue;
            if (string.IsNullOrEmpty(value))
            {
                return Task.CompletedTask;
            }

            try
            {
                var array = value.Split(',').Select(x => (T)Convert.ChangeType(x, typeof(T))).ToArray();
                bindingContext.Result = ModelBindingResult.Success(array);
            }
            catch
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Invalid value format");
            }

            return Task.CompletedTask;
        }
    }

    public class CommaSeparatedArrayModelBinderProvider<T> : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(T[]))
            {
                return new BinderTypeModelBinder(typeof(CommaSeparatedArrayModelBinder<T>));
            }

            return null;
        }
    }
}
