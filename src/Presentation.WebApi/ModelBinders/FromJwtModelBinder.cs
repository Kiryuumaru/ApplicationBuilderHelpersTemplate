using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Presentation.WebApi.Attributes;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;

namespace Presentation.WebApi.ModelBinders;

/// <summary>
/// Model binder that extracts values from JWT claims using <see cref="FromJwtAttribute"/>.
/// </summary>
public sealed class FromJwtModelBinder : IModelBinder
{
    /// <inheritdoc />
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var parameter = bindingContext.ActionContext.ActionDescriptor.Parameters
            .FirstOrDefault(p => p.Name == bindingContext.FieldName);

        if (parameter is not Microsoft.AspNetCore.Mvc.Controllers.ControllerParameterDescriptor controllerParameter)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        var fromJwtAttribute = controllerParameter.ParameterInfo
            .GetCustomAttributes(typeof(FromJwtAttribute), true)
            .OfType<FromJwtAttribute>()
            .FirstOrDefault();

        if (fromJwtAttribute is null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        var user = bindingContext.HttpContext.User;
        var claim = user.FindFirst(fromJwtAttribute.ClaimType);

        if (claim is null)
        {
            bindingContext.Result = ModelBindingResult.Failed();
            return Task.CompletedTask;
        }

        var targetType = bindingContext.ModelType;
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        try
        {
            object? convertedValue;

            if (underlyingType == typeof(Guid))
            {
                convertedValue = Guid.TryParse(claim.Value, out var guidValue) ? guidValue : null;
            }
            else if (underlyingType == typeof(string))
            {
                convertedValue = claim.Value;
            }
            else
            {
                var converter = TypeDescriptor.GetConverter(underlyingType);
                convertedValue = converter.CanConvertFrom(typeof(string))
                    ? converter.ConvertFromInvariantString(claim.Value)
                    : Convert.ChangeType(claim.Value, underlyingType, CultureInfo.InvariantCulture);
            }

            if (convertedValue is null && Nullable.GetUnderlyingType(targetType) is null)
            {
                bindingContext.Result = ModelBindingResult.Failed();
                return Task.CompletedTask;
            }

            bindingContext.Result = ModelBindingResult.Success(convertedValue);
        }
        catch (Exception)
        {
            bindingContext.Result = ModelBindingResult.Failed();
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Model binder provider for <see cref="FromJwtAttribute"/>.
/// </summary>
public sealed class FromJwtModelBinderProvider : IModelBinderProvider
{
    /// <inheritdoc />
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Check if the parameter has FromJwtAttribute
        if (context.BindingInfo.BindingSource == BindingSource.Custom)
        {
            // We need to check metadata for FromJwtAttribute
            // This provider will be used when BindingSource is Custom
            return new FromJwtModelBinder();
        }

        return null;
    }
}
