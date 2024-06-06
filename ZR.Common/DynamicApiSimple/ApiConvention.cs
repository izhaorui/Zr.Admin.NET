using Infrastructure.Helper;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZR.Common.DynamicApiSimple;

class ApiConvention : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            var type = controller.ControllerType;
            if (typeof(IDynamicApi).IsAssignableFrom(type) || type.IsDefined(typeof(DynamicApiAttribute), true))
            {
                ClearAction(controller);
                ConfigureApiExplorer(controller);
                ConfigureSelector(controller);
            }
        }
    }

    private void ClearAction(ControllerModel controller)
    {
        Type genericBaseType = AssemblyUtils.GetGenericTypeByName("BaseService`1");
        var needRemoveAction = controller.Actions
            .Where(action => !action.ActionMethod.DeclaringType.IsDerivedFromGenericBaseRepository(genericBaseType))
            .ToList();

        foreach (var actionModel in needRemoveAction)
        {
            controller.Actions.Remove(actionModel);
        }
    }


    private static void ConfigureApiExplorer(ControllerModel controller)
    {
        if (!controller.ApiExplorer.IsVisible.HasValue)
            controller.ApiExplorer.IsVisible = true;

        foreach (var action in controller.Actions)
        {
            if (!action.ApiExplorer.IsVisible.HasValue)
            {
                action.ApiExplorer.IsVisible = true;
            }
        }
    }

    private void ConfigureSelector(ControllerModel controller)
    {
        RemoveEmptySelectors(controller.Selectors);

        if (controller.Selectors.Any(selector => selector.AttributeRouteModel != null))
            return;

        foreach (var action in controller.Actions)
        {
            ConfigureSelector(action);
        }
    }

    private static void RemoveEmptySelectors(IList<SelectorModel> selectors)
    {
        for (var i = selectors.Count - 1; i >= 0; i--)
        {
            var selector = selectors[i];
            if (selector.AttributeRouteModel == null &&
               (selector.ActionConstraints == null || selector.ActionConstraints.Count <= 0) &&
               (selector.EndpointMetadata == null || selector.EndpointMetadata.Count <= 0))
            {
                selectors.Remove(selector);
            }
        }
    }

    private void ConfigureSelector(ActionModel action)
    {
        RemoveEmptySelectors(action.Selectors);

        if (action.Selectors.Count <= 0)
            AddServiceSelector(action);
        else
            NormalizeSelectorRoutes(action);
    }

    private void AddServiceSelector(ActionModel action)
    {
        var template = new Microsoft.AspNetCore.Mvc.RouteAttribute(GetRouteTemplate(action));
        var selector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(template)
        };
        selector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { GetHttpMethod(action) }));
        action.Selectors.Add(selector);
    }

    private void NormalizeSelectorRoutes(ActionModel action)
    {
        foreach (var selector in action.Selectors)
        {
            var template = new Microsoft.AspNetCore.Mvc.RouteAttribute(GetRouteTemplate(action,selector));
            selector.AttributeRouteModel = new AttributeRouteModel(template);
            if (selector.ActionConstraints.OfType<HttpMethodActionConstraint>().FirstOrDefault()?.HttpMethods?.FirstOrDefault() == null)
                selector.ActionConstraints.Add(new HttpMethodActionConstraint(new[] { GetHttpMethod(action) }));

        }
    }

    private string GetRouteTemplate(ActionModel action,SelectorModel selectorModel=null)
    {
        var routeTemplate = new StringBuilder();
        var names = action.Controller.ControllerType.Namespace.Split('.');
        if (names.Length > 2)
        {
            routeTemplate.Append(names[^2]);
        }

        // Controller
        var controllerName = action.Controller.ControllerName;
        if (controllerName.EndsWith("Service"))
            controllerName = controllerName[0..^7];

        if (selectorModel is { AttributeRouteModel: not null })
        {
            if (!string.IsNullOrWhiteSpace(selectorModel.AttributeRouteModel?.Template))
            {
                if (selectorModel.AttributeRouteModel.Template.StartsWith("/"))
                {
                    routeTemplate.Append(selectorModel.AttributeRouteModel.Template);
                }
                else
                {
                    routeTemplate.Append($"{BaseRoute}/{controllerName}/{selectorModel.AttributeRouteModel.Template}");
                }
            }
        }
        else
        {
            routeTemplate.Append($"{BaseRoute}/{controllerName}");

            // Action
            var actionName = action.ActionName;
            if (actionName.EndsWith("Async") || actionName.EndsWith("async"))
                actionName = actionName[..^"Async".Length];

            if (!string.IsNullOrEmpty(actionName))
            {
                routeTemplate.Append($"/{RemoveHttpMethodPrefix(actionName)}");
            }
        }
        

        return routeTemplate.ToString();
    }

    private static string GetHttpMethod(ActionModel action)
    {
        var actionName = action.ActionName.ToLower();
        string Method = string.Empty;
        if (!string.IsNullOrEmpty(actionName))
        {
            Method = GetName(actionName);
        }
        return Method;
    }

    private static string GetName(string actionName)
    {
        string result = "POST";
        foreach (string key in Methods.Keys)
        {
            if (actionName.Contains(key))
            {
                result = Methods[key];
                break;
            }

        }
        return result;
    }
    internal static Dictionary<string, string> Methods { get; private set; }
    internal static string BaseRoute { get; private set; } = "api";
    static ApiConvention()
    {
        Methods = new Dictionary<string, string>()
        {

            ["get"] = "GET",
            ["find"] = "GET",
            ["fetch"] = "GET",
            ["query"] = "GET",
            ["post"] = "POST",
            ["add"] = "POST",
            ["create"] = "POST",
            ["insert"] = "POST",
            ["submit"] = "POST",
            ["put"] = "POST",
            ["update"] = "POST",
            ["delete"] = "DELETE",
            ["remove"] = "DELETE",
            ["clear"] = "DELETE",
            ["patch"] = "PATCH"
        };

    }
    private static string RemoveHttpMethodPrefix(string actionName)
    {
        foreach (var method in Methods.Keys)
        {
            if (actionName.StartsWith(method, StringComparison.OrdinalIgnoreCase))
            {
                // 移除前缀并返回结果
                return actionName.Substring(method.Length);
            }
        }

        return actionName; // 如果没有找到前缀，返回原始名称
    }
}
