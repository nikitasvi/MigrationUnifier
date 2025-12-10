using System.Globalization;

namespace MigrationUnifier.Utils
{
	public class PathUtil
	{
		public static (string codeOutputPath, string designerOutputPath, string migrationId)
			ResolveOutputPaths(
				string? outputPath,
				DateTime? until,
				string? untilRaw,
				string sourceDirectory,
				string className,
				string lastMigrationPath
			)
		{
			string lastFileName = Path.GetFileNameWithoutExtension(lastMigrationPath);

			string? idFromUntil =
				until?.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

			string? idFromUntilRaw = null;

			if (!string.IsNullOrEmpty(untilRaw) &&
				untilRaw!.Length == 14 &&
				untilRaw.All(char.IsDigit))
			{
				idFromUntilRaw = untilRaw;
			}

			string? idFromLastName = DateUtil.TryGetStringTimestampFromName(lastFileName);

			string migrationId =
				idFromUntil
				?? idFromUntilRaw
				?? idFromLastName
				?? DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

			string baseFileName = $"{migrationId}_{className}";

			string directory;

			if (string.IsNullOrWhiteSpace(outputPath))
			{
				directory = Path.GetFullPath(sourceDirectory);
			}
			else if (outputPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
			{
				directory = Path.GetDirectoryName(Path.GetFullPath(outputPath))!;
				baseFileName = Path.GetFileNameWithoutExtension(outputPath);
			}
			else
			{
				directory = Path.GetFullPath(outputPath);
			}

			string codeOutputPath = Path.Combine(directory, baseFileName + ".cs");
			string designerOutputPath = Path.Combine(directory, baseFileName + ".Designer.cs");

			return (codeOutputPath, designerOutputPath, migrationId);
		}
	}
}
