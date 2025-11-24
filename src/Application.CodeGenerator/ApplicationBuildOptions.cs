using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Application.CodeGenerator;

internal sealed record ApplicationBuildOptions(
    string OutputPath,
    string AppName,
    string AppTitle,
    string AppDescription,
    string Version,
    string AppTag,
    string BuildPayload)
{
    public static ApplicationBuildOptions Parse(IReadOnlyList<string> arguments, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < arguments.Count; index++)
        {
            var raw = arguments[index];

            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (raw == "--")
            {
                break;
            }

            if (raw.StartsWith("--", StringComparison.Ordinal))
            {
                var trimmed = raw[2..];
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex >= 0)
                {
                    var key = trimmed[..separatorIndex];
                    var value = trimmed[(separatorIndex + 1)..];
                    options[key] = value;
                    continue;
                }

                string valueCandidate = string.Empty;
                if (index + 1 < arguments.Count)
                {
                    var next = arguments[index + 1];
                    if (!next.StartsWith("--", StringComparison.Ordinal))
                    {
                        valueCandidate = next;
                        index++;
                    }
                }

                options[trimmed] = valueCandidate;
                continue;
            }
        }

        static string GetRequired(IDictionary<string, string> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (source.TryGetValue(key, out var value) && value is not null)
                {
                    return value;
                }
            }

            throw new InvalidOperationException($"Missing required command line option '{string.Join("|", keys)}'.");
        }

        static string GetOptional(IDictionary<string, string> source, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (source.TryGetValue(key, out var value) && value is not null)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        var outputPath = GetRequired(options, "output", "o");
        var absoluteOutputPath = Path.GetFullPath(outputPath, baseDirectory);

        var inlinePayload = GetOptional(options, "build-payload", "buildpayload");
        var payloadPath = GetOptional(options, "build-payload-path", "buildpayloadpath");

        return new ApplicationBuildOptions(
            absoluteOutputPath,
            GetRequired(options, "app-name", "appname"),
            GetRequired(options, "app-title", "apptitle"),
            GetOptional(options, "app-description", "appdescription"),
            GetRequired(options, "version"),
            GetRequired(options, "app-tag", "apptag"),
            ResolveBuildPayload(inlinePayload, payloadPath, baseDirectory));
    }

    private static string ResolveBuildPayload(string inlineValue, string payloadPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            return inlineValue;
        }

        if (!string.IsNullOrWhiteSpace(payloadPath))
        {
            try
            {
                var absolutePath = Path.GetFullPath(payloadPath, baseDirectory);
                if (File.Exists(absolutePath))
                {
                    var raw = File.ReadAllText(absolutePath);
                    if (string.IsNullOrEmpty(raw))
                    {
                        raw = "{}";
                    }

                    var bytes = Encoding.UTF8.GetBytes(raw);
                    return Convert.ToBase64String(bytes);
                }
            }
            catch (IOException)
            {
                // Fallback to default payload when the configured file is unavailable.
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to default payload when the configured file is inaccessible.
            }
        }

        var defaultBytes = Encoding.UTF8.GetBytes("{}");
        return Convert.ToBase64String(defaultBytes);
    }
}
