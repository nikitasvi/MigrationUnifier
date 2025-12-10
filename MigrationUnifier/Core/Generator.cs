using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Options;
using MigrationUnifier.Models;
using System.Text;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace MigrationUnifier.Core
{
	public class Generator
	{
		public static string GenerateMigrationFile(
			List<Migration> migrations,
			string @namespace,
			string className,
			IReadOnlyList<UsingDirectiveSyntax> usingDirectives
		)
		{
			(string upBodyText, string downBodyText) = MergeBodies(migrations);

			MethodDeclarationSyntax upMethod = CreateOverrideMethodFromBody("Up", upBodyText);
			MethodDeclarationSyntax downMethod = CreateOverrideMethodFromBody("Down", downBodyText);

			ClassDeclarationSyntax classDeclaration =
				ClassDeclaration(className)
					.AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.PartialKeyword))
					.WithBaseList(BaseList(SingletonSeparatedList<BaseTypeSyntax>(SimpleBaseType(IdentifierName("Migration")))))
					.WithMembers(List<MemberDeclarationSyntax>(new MemberDeclarationSyntax[] { upMethod, downMethod }))
					.WithAdditionalAnnotations(Formatter.Annotation)
					.WithLeadingTrivia(ElasticCarriageReturnLineFeed);

			NamespaceDeclarationSyntax namespaceDeclaration =
				NamespaceDeclaration(ParseName(@namespace))
					.WithMembers(SingletonList<MemberDeclarationSyntax>(classDeclaration))
					.WithAdditionalAnnotations(Formatter.Annotation)
					.WithLeadingTrivia(ElasticCarriageReturnLineFeed);

			CompilationUnitSyntax compilationUnit =
				CompilationUnit()
					.WithUsings(List(usingDirectives))
					.WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDeclaration))
					.WithEndOfFileToken(Token(SyntaxKind.EndOfFileToken).WithLeadingTrivia(ElasticCarriageReturnLineFeed))
					.WithAdditionalAnnotations(Formatter.Annotation);

			AdhocWorkspace workspace = new AdhocWorkspace();
			OptionSet options = workspace.Options
				.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
				.WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4)
				.WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4)
				.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true)
				.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true)
				.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInObjectCollectionArrayInitializers, true)
				.WithChangedOption(CSharpFormattingOptions.NewLineForMembersInObjectInit, true)
				.WithChangedOption(CSharpFormattingOptions.NewLineForMembersInAnonymousTypes, true)
				.WithChangedOption(CSharpFormattingOptions.NewLineForClausesInQuery, true);

			SyntaxNode formatted = Formatter.Format(compilationUnit, workspace, options);

			string header = "#nullable disable\r\n#pragma warning disable 612, 618\r\n\r\n";
			return header + formatted.ToFullString();
		}

		// Designer from last included migration's designer — MigrationId contains timestamp
		public static string GenerateDesignerFromLast(
			string lastDesignerPath,
			string targetNamespace,
			string newClassName,
			string migrationId,
			IReadOnlyList<UsingDirectiveSyntax> usingDirectives
		)
		{
			string text = File.ReadAllText(lastDesignerPath);
			CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();

			ClassDeclarationSyntax originalClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
			SyntaxList<AttributeListSyntax> attributes = originalClass.AttributeLists;

			List<AttributeListSyntax> updated = new List<AttributeListSyntax>();
			foreach (AttributeListSyntax list in attributes)
			{
				var items = new List<AttributeSyntax>();
				foreach (AttributeSyntax attribute in list.Attributes)
				{
					string name = attribute.Name.ToString();
					if (name.EndsWith("Migration") || name == "Migration")
					{
						SeparatedSyntaxList<AttributeArgumentSyntax> args = SeparatedList(new[]
						{
							AttributeArgument(LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(migrationId + "_" + newClassName)))
						});
						items.Add(attribute.WithArgumentList(AttributeArgumentList(args)));
					}
					else
					{
						items.Add(attribute);
					}
				}

				updated.Add(list.WithAttributes(SeparatedList(items)));
			}

			MethodDeclarationSyntax buildMethod = originalClass.Members.OfType<MethodDeclarationSyntax>()
				.First(m => m.Identifier.Text == "BuildTargetModel");

			ClassDeclarationSyntax newClass =
				ClassDeclaration(newClassName)
					.AddModifiers(Token(SyntaxKind.PartialKeyword))
					.WithAttributeLists(List(updated))
					.WithMembers(SingletonList<MemberDeclarationSyntax>(buildMethod))
					.WithAdditionalAnnotations(Formatter.Annotation);

			NamespaceDeclarationSyntax namespaceDeclaration =
				NamespaceDeclaration(ParseName(targetNamespace))
					.WithMembers(SingletonList<MemberDeclarationSyntax>(newClass))
					.WithAdditionalAnnotations(Formatter.Annotation);

			CompilationUnitSyntax compilationUnit =
				CompilationUnit()
					.WithUsings(List(usingDirectives))
					.WithMembers(SingletonList<MemberDeclarationSyntax>(namespaceDeclaration))
					.WithEndOfFileToken(Token(SyntaxKind.EndOfFileToken).WithLeadingTrivia(ElasticCarriageReturnLineFeed))
					.WithAdditionalAnnotations(Formatter.Annotation);

			AdhocWorkspace workspace = new AdhocWorkspace();
			OptionSet options = workspace.Options
				.WithChangedOption(FormattingOptions.UseTabs, LanguageNames.CSharp, false)
				.WithChangedOption(FormattingOptions.IndentationSize, LanguageNames.CSharp, 4)
				.WithChangedOption(FormattingOptions.TabSize, LanguageNames.CSharp, 4)
				.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInTypes, true)
				.WithChangedOption(CSharpFormattingOptions.NewLinesForBracesInMethods, true);

			SyntaxNode formatted = Formatter.Format(compilationUnit, workspace, options);

			string header = "// <auto-generated />\r\n";
			return header + formatted.ToFullString();
		}

		private static MethodDeclarationSyntax CreateOverrideMethodFromBody(string name, string bodyText)
		{
			BlockSyntax body = ParseBodyToBlock(bodyText).WithAdditionalAnnotations(Formatter.Annotation);

			return MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(name))
				.AddModifiers(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword))
				.WithParameterList(
					ParameterList(
						SingletonSeparatedList(
							Parameter(Identifier("migrationBuilder"))
								.WithType(IdentifierName("MigrationBuilder"))
						)
					)
				)
				.WithBody(body)
				.WithLeadingTrivia(ElasticCarriageReturnLineFeed)
				.WithAdditionalAnnotations(Formatter.Annotation);
		}

		private static BlockSyntax ParseBodyToBlock(string rawBody)
		{
			if (string.IsNullOrWhiteSpace(rawBody))
			{
				return Block();
			}

			string wrapper =
			$@"class __TmpClass__
			{{
				void __TmpMethod__()
				{{
					{rawBody}
				}}
			}}";

			CompilationUnitSyntax root = CSharpSyntaxTree
				.ParseText(wrapper)
				.GetCompilationUnitRoot();

			return root.DescendantNodes().OfType<MethodDeclarationSyntax>().First().Body!;
		}

		#region Merge Bodies
		private static (string up, string down) MergeBodies(IReadOnlyList<Migration> orderedMigrations)
		{
			var up = new StringBuilder();
			foreach (Migration migration in orderedMigrations)
			{
				if (string.IsNullOrWhiteSpace(migration.UpBodyContent))
				{
					continue;
				}

				up.AppendLine($"// === Begin {DisplayName(migration)} ===");
				up.AppendLine(migration.UpBodyContent);

				if (!migration.UpBodyContent.TrimEnd().EndsWith(";"))
				{
					up.AppendLine(";");
				}

				up.AppendLine($"// === End {DisplayName(migration)} ===");
				up.AppendLine();
			}

			var down = new StringBuilder();
			foreach (Migration migration in orderedMigrations.Reverse())
			{
				if (string.IsNullOrWhiteSpace(migration.DownBodyContent))
				{
					continue;
				}

				down.AppendLine($"// === Begin {DisplayName(migration)} (reversed) ===");
				down.AppendLine(migration.DownBodyContent);

				if (!migration.DownBodyContent.TrimEnd().EndsWith(";"))
				{
					down.AppendLine(";");
				}

				down.AppendLine($"// === End {DisplayName(migration)} ===");
				down.AppendLine();
			}

			return (up.ToString(), down.ToString());
		}

		private static string DisplayName(Migration migration)
		{
			return $"{(migration.Timestamp is null
				? string.Empty
				: migration.Timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss") + " ")}{migration.ClassName}";
		}
		#endregion

		#region Helpers
		public static IReadOnlyList<UsingDirectiveSyntax> CollectUsingsFromFiles(IEnumerable<string> filePaths)
		{
			HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
			foreach (string path in filePaths)
			{
				string? text = File.ReadAllText(path);
				CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();

				foreach (UsingDirectiveSyntax @using in root.Usings)
				{
					if (@using.Name is not null)
					{
						set.Add(@using.Name.ToString());
					}
				}
			}

			return set.OrderBy(s => s, StringComparer.Ordinal)
					  .Select(s => UsingDirective(ParseName(s)))
					  .ToList();
		}
		#endregion
	}
}
