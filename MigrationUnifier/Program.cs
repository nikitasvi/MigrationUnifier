using Microsoft.CodeAnalysis.CSharp.Syntax;
using MigrationUnifier.Core;
using MigrationUnifier.Models;
using MigrationUnifier.Utils;
using System.Text;

namespace MigrationUnifier
{
	public static class Program
	{
		public static int Main(string[] args)
		{
			if (!Cli.TryParseArguments(args, out var request))
			{
				return 1;
			}

			if (request is null)
			{
				Console.WriteLine("Request is missing.");
				return 1;
			}

			DateTime? from = DateUtil.TryGetTimestampFromName(request.From);
			DateTime? until = DateUtil.TryGetTimestampFromName(request.Until);

			try
			{
				List<Migration> migrations = Scanner.Scan(request.SourceDirectory, from, until);

				if (migrations.Count == 0)
				{
					Console.WriteLine("No migrations found for the given filters.");
					return 2;
				}

				IReadOnlyList<UsingDirectiveSyntax> migrationUsings = Generator.CollectUsingsFromFiles(migrations.Select(m => m.FilePath));

				string lastMigrationPath = migrations[^1].FilePath;

				(string codeOutputPath, string designerOutputPath, string migrationId) =
					PathUtil.ResolveOutputPaths(
						request.OutputDirectory,
						until,
						request.Until,
						request.SourceDirectory,
						request.ClassName,
						lastMigrationPath
					);

				string code = Generator.GenerateMigrationFile(
					migrations: migrations,
					@namespace: request.TargetNamespace,
					className: request.ClassName,
					usingDirectives: migrationUsings
				);

				Directory.CreateDirectory(Path.GetDirectoryName(codeOutputPath)!);
				File.WriteAllText(codeOutputPath, code, Encoding.UTF8);

				string lastDesigner = Path.ChangeExtension(lastMigrationPath, ".Designer.cs");

				if (!File.Exists(lastDesigner))
				{
					var dir = Path.GetDirectoryName(lastMigrationPath)!;
					var stem = Path.GetFileNameWithoutExtension(lastMigrationPath);
					var probe = Directory.GetFiles(dir, $"{stem}*.Designer.cs").FirstOrDefault();
					if (probe != null)
					{
						lastDesigner = probe;
					}
				}

				if (File.Exists(lastDesigner))
				{
					var designersPaths = migrations
						.Select(m =>
						{
							var directory = Path.GetDirectoryName(m.FilePath)!;
							var stem = Path.GetFileNameWithoutExtension(m.FilePath);
							var designer = Path.Combine(directory, stem + ".Designer.cs");
							return designer;
						})
						.Where(File.Exists);

					IReadOnlyList<UsingDirectiveSyntax> designerUsings = Generator.CollectUsingsFromFiles(designersPaths);

					string designerText = Generator.GenerateDesignerFromLast(
						lastDesignerPath: lastDesigner,
						targetNamespace: request.TargetNamespace,
						newClassName: request.ClassName,
						migrationId: migrationId,
						usingDirectives: designerUsings);

					File.WriteAllText(designerOutputPath, designerText, Encoding.UTF8);
				}
				else
				{
					Console.WriteLine("Warning: last designer file not found; combined .Designer.cs was not created.");
				}

				Console.WriteLine();
				Console.WriteLine("Result files:");
				Console.WriteLine(" - " + codeOutputPath);
				Console.WriteLine(" - " + designerOutputPath);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.ToString());
				return 1;
			}

			return 0;
		}
	}
}