using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.CodeGenerator;

interface ICodeGenerationTask
{
    string Id { get; }

    string GetDefaultOutputPath(string applicationBaseDirectory);

    Task GenerateAsync(CodeGenerationContext context, CancellationToken cancellationToken);
}

sealed class CodeGenerationContext(
    string applicationBaseDirectory,
    DateTime utcNow,
    ApplicationBuildOptions? options,
    IReadOnlyDictionary<string, string> outputPaths)
{
    private readonly IReadOnlyDictionary<string, string> _outputPaths = outputPaths;

    public string ApplicationBaseDirectory { get; } = applicationBaseDirectory;

    public DateTime UtcNow { get; } = utcNow;

    public ApplicationBuildOptions? Options { get; } = options;

    public string GetOutputPath(string generatorId)
    {
        if (!_outputPaths.TryGetValue(generatorId, out var path))
        {
            throw new InvalidOperationException($"No output path configured for generator '{generatorId}'.");
        }

        return path;
    }
}
