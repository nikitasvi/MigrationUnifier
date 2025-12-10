using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MigrationUnifier.Models;
using MigrationUnifier.Utils;
using System.Text;

namespace MigrationUnifier.Core
{
	public class Scanner
	{
		public static List<Migration> Scan(string sourceDirectory, DateTime? from = null, DateTime? until = null)
		{
			if (!Directory.Exists(sourceDirectory))
			{
				throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");
			}

			string[] files = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories);
			var migrations = new List<Migration>();

			foreach (string file in files)
			{
				string text = File.ReadAllText(file, Encoding.UTF8);
				SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
				CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

				foreach (ClassDeclarationSyntax @class in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
				{
					if (!IsMigrationClass(@class))
					{
						continue;
					}

					string? up = ExtractMethodBody(@class, "Up");
					string? down = ExtractMethodBody(@class, "Down");

					if (up is null && down is null)
					{
						continue;
					}

					string nameWithoutExtension = Path.GetFileNameWithoutExtension(file);
					DateTime? timestamp =
						DateUtil.TryGetTimestampFromName(nameWithoutExtension) ??
						DateUtil.TryGetTimestampFromName(@class.Identifier.Text);

					if (from.HasValue)
					{
						if (!timestamp.HasValue || timestamp.Value < from.Value)
						{
							continue;
						}
					}

					if (until.HasValue)
					{
						if (!timestamp.HasValue || timestamp.Value > until.Value)
						{
							continue;
						}
					}

					migrations.Add(
						new Migration(
							filePath: file,
							className: @class.Identifier.Text,
							timestamp: timestamp,
							upBodyContent: up ?? string.Empty,
							downBodyContent: down ?? string.Empty
						)
					);
				}
			}

			return migrations
				.OrderBy(m => m.Timestamp ?? DateTime.MaxValue)
				.ToList();
		}

		private static bool IsMigrationClass(ClassDeclarationSyntax @class)
		{
			return @class.BaseList?.Types
				.Select(t => t.Type).OfType<IdentifierNameSyntax>()
				.Any(id => id.Identifier.Text is "Migration") == true;
		}

		private static string? ExtractMethodBody(ClassDeclarationSyntax @class, string methodName)
		{
			MethodDeclarationSyntax? method = @class.Members.OfType<MethodDeclarationSyntax>()
				.FirstOrDefault(m => m.Identifier.Text == methodName
					&& m.ParameterList.Parameters.Count == 1
					&& m.ParameterList.Parameters[0].Type is IdentifierNameSyntax p
					&& p.Identifier.Text == "MigrationBuilder");

			if (method?.Body is null)
			{
				return null;
			}

			SyntaxList<StatementSyntax> statements = method.Body.Statements;

			if (statements.Count == 0)
			{
				return string.Empty;
			}

			IEnumerable<string> pieces = statements.Select(s => s.ToFullString());
			return string.Join(Environment.NewLine, pieces);
		}
	}
}
