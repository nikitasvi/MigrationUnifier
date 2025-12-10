using System.Text.RegularExpressions;

namespace MigrationUnifier.Utils
{
	public static class DateUtil
	{
		private static readonly Regex TimestampPrefix = new(@"^(?<ts>\d{14})_", RegexOptions.Compiled);

		public static DateTime? TryGetTimestampFromName(string? name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return null;
			}

			Match match = TimestampPrefix.Match(name);

			if (!match.Success)
			{
				return null;
			}

			return DateTime.TryParseExact(match.Groups["ts"].Value, "yyyyMMddHHmmss", null, 0, out var dt)
				? dt
				: null;
		}

		public static string? TryGetStringTimestampFromName(string name)
		{
			Match match = TimestampPrefix.Match(name);

			if (!match.Success)
			{
				return null;
			}

			return match.Groups["ts"].Value;
		}
	}
}
