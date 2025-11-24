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
};

using var cancellationSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
	args.Cancel = true;
	cancellationSource.Cancel();
};

var baseDirectory = AppContext.BaseDirectory;
var outputPaths = BuildOutputPathMap(generators, args, baseDirectory);
var context = new CodeGenerationContext(baseDirectory, DateTime.UtcNow, outputPaths);

foreach (var generator in generators)
{
	cancellationSource.Token.ThrowIfCancellationRequested();
	await generator.GenerateAsync(context, cancellationSource.Token);
}

static IReadOnlyDictionary<string, string> BuildOutputPathMap(
	IReadOnlyList<ICodeGenerationTask> tasks,
	string[] arguments,
	string baseDirectory)
{
	var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

	foreach (var task in tasks)
	{
		var defaultPath = task.GetDefaultOutputPath(baseDirectory);
		map[task.Id] = Path.GetFullPath(defaultPath, baseDirectory);
	}

	if (arguments.Length == 0)
	{
		return map;
	}

	var positionalIndex = 0;
	foreach (var rawArg in arguments)
	{
		if (string.IsNullOrWhiteSpace(rawArg))
		{
			continue;
		}

		var argument = rawArg.Trim();
		var separatorIndex = argument.IndexOf('=', StringComparison.Ordinal);
		if (separatorIndex > 0)
		{
			var key = argument[..separatorIndex].Trim();
			var value = argument[(separatorIndex + 1)..].Trim();

			if (!map.ContainsKey(key))
			{
				throw new InvalidOperationException($"Unknown generator id '{key}'.");
			}

			map[key] = Path.GetFullPath(value, baseDirectory);
			continue;
		}

		if (positionalIndex >= tasks.Count)
		{
			throw new InvalidOperationException("Too many positional arguments were supplied.");
		}

		var taskId = tasks[positionalIndex].Id;
		map[taskId] = Path.GetFullPath(argument, baseDirectory);
		positionalIndex++;
	}

	return map;
}
