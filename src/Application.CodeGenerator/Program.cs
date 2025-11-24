using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Application.CodeGenerator;
using Application.CodeGenerator.Generators;

var generators = new ICodeGenerationTask[]
{
	new BuildConstantsGenerator(),
};

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
	args.Cancel = true;
	cancellationSource.Cancel();
};

var baseDirectory = AppContext.BaseDirectory;
var options = ApplicationBuildOptions.Parse(args, baseDirectory);
var outputPaths = BuildOutputPathMap(generators, options, baseDirectory);
var context = new CodeGenerationContext(baseDirectory, DateTime.UtcNow, options, outputPaths);

foreach (var generator in generators)
{
	cancellationSource.Token.ThrowIfCancellationRequested();
	await generator.GenerateAsync(context, cancellationSource.Token);
}

static IReadOnlyDictionary<string, string> BuildOutputPathMap(
	IReadOnlyList<ICodeGenerationTask> tasks,
	ApplicationBuildOptions options,
	string baseDirectory)
{
	var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	foreach (var task in tasks)
	{
		var defaultPath = task.GetDefaultOutputPath(baseDirectory);
		map[task.Id] = Path.GetFullPath(defaultPath, baseDirectory);
	}

	if (!string.IsNullOrWhiteSpace(options.OutputPath))
	{
		var overriddenPath = Path.GetFullPath(options.OutputPath, baseDirectory);

		foreach (var task in tasks)
		{
			if (string.Equals(task.Id, BuildConstantsGenerator.TaskId, StringComparison.Ordinal))
			{
				map[task.Id] = overriddenPath;
				break;
			}
		}
	}

	return map;
}
