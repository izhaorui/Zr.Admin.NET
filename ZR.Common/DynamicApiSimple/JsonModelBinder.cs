using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json;

namespace ZR.Common.DynamicApiSimple;

public class JsonModelBinder : IModelBinder
{
    private readonly IModelBinder _fallbackBinder;

    public JsonModelBinder(IModelBinder fallbackBinder)
    {
        _fallbackBinder = fallbackBinder;
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        var request = bindingContext.HttpContext.Request;
        if ((request.Method == "POST" || request.Method == "PUT") && request.ContentType != null && request.ContentType.Contains("application/json"))
        {
            using (var reader = new StreamReader(request.Body))
            {
                var body = await reader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(body))
                {
                    var result = JsonConvert.DeserializeObject(body, bindingContext.ModelType);
                    bindingContext.Result = ModelBindingResult.Success(result);
                    return;
                }
            }
        }

        if (_fallbackBinder != null)
        {
            await _fallbackBinder.BindModelAsync(bindingContext);
        }
        else
        {
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}