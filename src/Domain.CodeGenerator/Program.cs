using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Domain.CodeGenerator;
using Domain.CodeGenerator.Generators;

var generators = new ICodeGenerationTask[]
{
	new PermissionIdsGenerator(),
	new RoleIdsGenerator(),
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

	// Override output paths if specified
	if (!string.IsNullOrWhiteSpace(options.OutputPath))
	{
		map[BuildConstantsGenerator.TaskId] = options.OutputPath;
	}

	if (!string.IsNullOrWhiteSpace(options.PermissionIdsOutputPath))
	{
		map["PermissionIds"] = options.PermissionIdsOutputPath;
	}

	if (!string.IsNullOrWhiteSpace(options.RoleIdsOutputPath))
	{
		map["RoleIds"] = options.RoleIdsOutputPath;
	}

	return map;
}
