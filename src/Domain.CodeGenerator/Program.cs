using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

// Only run generators that have output paths configured
foreach (var generator in generators.Where(g => outputPaths.ContainsKey(g.Id)))
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

	// BuildConstants always uses default path if not specified
	var buildConstantsGenerator = tasks.FirstOrDefault(t => t.Id == BuildConstantsGenerator.TaskId);
	if (buildConstantsGenerator is not null)
	{
		var defaultPath = buildConstantsGenerator.GetDefaultOutputPath(baseDirectory);
		map[BuildConstantsGenerator.TaskId] = Path.GetFullPath(defaultPath, baseDirectory);
	}

	if (!string.IsNullOrWhiteSpace(options.OutputPath))
	{
		map[BuildConstantsGenerator.TaskId] = options.OutputPath;
	}

	// PermissionIds and RoleIds are ONLY generated when explicitly requested
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
