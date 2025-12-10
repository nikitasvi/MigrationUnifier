namespace MigrationUnifier.Models
{
	public class Migration
	{
		public string FilePath { get; }
		public string ClassName { get; }
		public DateTime? Timestamp { get; }
		public string UpBodyContent { get; }
		public string DownBodyContent { get; }

		public Migration(
			string filePath, 
			string className, 
			DateTime? timestamp, 
			string upBodyContent, 
			string downBodyContent
		)
		{
			FilePath = filePath;
			ClassName = className;
			Timestamp = timestamp;
			UpBodyContent = upBodyContent;
			DownBodyContent = downBodyContent;
		}
	}
}
