namespace MigrationUnifier.Models
{
	public class Request
	{
		public required string SourceDirectory { get; init; } = null!;
		public required string ClassName { get; init; } = null!;
		public required string TargetNamespace { get; init; } = null!;

		public string? From { get; init; }
		public string? Until { get; init; }
		public string? OutputDirectory { get; init; }
	}
}
