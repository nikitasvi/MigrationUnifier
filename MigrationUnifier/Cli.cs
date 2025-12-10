using MigrationUnifier.Models;

namespace MigrationUnifier
{
	public class Cli
	{
		public static bool TryParseArguments(string[] args, out Request? request)
		{
			request = null;

			string? sourceDirectory = null;
			string? @class = null;
			string? @namespace = null;
			string? from = null;
			string? until = null;
			string? outputDirectory = null;

			int i = 0;
			while (i < args.Length)
			{
				string arg = args[i];

				if (!arg.StartsWith("-"))
				{
					Console.Error.WriteLine($"Unexpected positional argument: {arg}");
					PrintUsage(Console.Error);
					return false;
				}

				string flag;
				string? value = null;

				int eq = arg.IndexOf('=');
				if (eq >= 0)
				{
					flag = arg[..eq];
					value = arg[(eq + 1)..];
				}
				else
				{
					flag = arg;
					if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
					{
						value = args[++i];
					}
				}

				if (string.Equals(flag, "--help", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(flag, "-h", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(flag, "-?", StringComparison.OrdinalIgnoreCase))
				{
					PrintUsage(Console.Out);
					return false;
				}

				if (value is null)
				{
					Console.Error.WriteLine($"Missing value for option '{flag}'.");
					PrintUsage(Console.Error);
					return false;
				}

				switch (flag.ToLowerInvariant())
				{
					case "-s":
					case "--source":
						sourceDirectory = value;
						break;

					case "-c":
					case "--class":
						@class = value;
						break;

					case "-n":
					case "--namespace":
						@namespace = value;
						break;

					case "-f":
					case "--from":
						from = value;
						break;

					case "-u":
					case "--until":
						until = value;
						break;

					case "-o":
					case "--out":
						outputDirectory = value;
						break;

					default:
						Console.Error.WriteLine($"Unknown option: {flag}");
						PrintUsage(Console.Error);
						return false;
				}

				i++;
			}

			if (string.IsNullOrWhiteSpace(sourceDirectory) ||
				string.IsNullOrWhiteSpace(@class) ||
				string.IsNullOrWhiteSpace(@namespace))
			{
				Console.Error.WriteLine("Missing required options: --source, --class, --namespace are mandatory.");
				PrintUsage(Console.Error);
				return false;
			}

			request = new Request
			{
				SourceDirectory = sourceDirectory,
				ClassName = @class,
				TargetNamespace = @namespace,
				From = from,
				Until = until,
				OutputDirectory = outputDirectory
			};

			return true;
		}

		public static void PrintUsage(TextWriter writer)
		{
			writer.WriteLine("Usage:");
			writer.WriteLine("  MigrationUnifier.exe --source <dir> --class <name> --namespace <ns> [options]");
			writer.WriteLine();
			writer.WriteLine("Arguments:");
			writer.WriteLine("  *required \t-s, \t--source <dir>     \t Path to the directory with migrations");
			writer.WriteLine("  *required \t-c, \t--class <name>     \t The base name of the class");
			writer.WriteLine("  *required \t-n, \t--namespace <ns>   \t Final migration namespace");
			writer.WriteLine("	\t-f,  \t--from <filename>      \t Filename 'from' (inclusive)");
			writer.WriteLine("	\t-u,  \t--until <filename>     \t Filename 'to' (inclusive)");
			writer.WriteLine("	\t-o,  \t--out <path>       \t Output path for .cs and Designer.cs files");
			writer.WriteLine();
			writer.WriteLine("Examples:");
			writer.WriteLine("  MigrationUnifier \\");
			writer.WriteLine("    --source ./Migrations \\");
			writer.WriteLine("    --class FeatureX \\");
			writer.WriteLine("    --namespace MyProject.Data.Migrations \\");
			writer.WriteLine("    --from 20240101000000_NameFrom \\");
			writer.WriteLine("    --until 20241201000000_NameTo \\");
			writer.WriteLine("    --out ./Migrations/Combined");
			//writer.WriteLine("  dotnet script <gist-url>.csx -- \\");
			//writer.WriteLine("    --source ./Migrations --class FeatureX --namespace MyProject.Data.Migrations");
		}
	}
}
