using Domain.Shared.Models;

namespace Domain.Authorization.ValueObjects;

/// <summary>
/// Represents a permission request for use with HasPermission checks.
/// Provides fluent parameter construction and implicit string conversion.
/// Format: "path" or "path;key=value;key2=value2"
/// </summary>
public readonly struct PermissionRequest
{
    private readonly string _path;
    private readonly string? _params;

    /// <summary>
    /// Creates a permission request for the specified path without parameters.
    /// </summary>
    /// <param name="path">The permission path.</param>
    internal PermissionRequest(string path)
    {
        _path = path;
        _params = null;
    }

    /// <summary>
    /// Creates a permission request for the specified path with an initial parameter.
    /// </summary>
    /// <param name="path">The permission path.</param>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The parameter value.</param>
    internal PermissionRequest(string path, string key, string value)
    {
        _path = path;
        _params = $"{key}={value}";
    }

    /// <summary>
    /// Creates a permission request from existing path and parameters with an additional parameter.
    /// </summary>
    private PermissionRequest(string path, string existingParams, string key, string value)
    {
        _path = path;
        _params = $"{existingParams};{key}={value}";
    }

    /// <summary>
    /// Gets the permission path.
    /// </summary>
    public string Path => _path;

    /// <summary>
    /// Adds a parameter to the permission request.
    /// </summary>
    /// <param name="key">The parameter key.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A new permission request with the added parameter.</returns>
    public PermissionRequest WithParameter(string key, string value) =>
        string.IsNullOrEmpty(_params)
            ? new PermissionRequest(_path, key, value)
            : new PermissionRequest(_path, _params, key, value);

    /// <summary>
    /// Returns the string representation of this permission request.
    /// Format: "path" or "path;key=value;key2=value2"
    /// </summary>
    public override string ToString() =>
        string.IsNullOrEmpty(_params) ? _path : $"{_path};{_params}";

    /// <summary>
    /// Implicit conversion to string for seamless use with HasPermission methods.
    /// </summary>
    /// <param name="request">The permission request to convert.</param>
    public static implicit operator string(PermissionRequest request) => request.ToString();
}
