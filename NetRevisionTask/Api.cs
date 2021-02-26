using System.IO;

namespace NetRevisionTask
{
	/// <summary>
	/// Provides public API methods.
	/// </summary>
	public class Api
	{
		public static string GetVersion(
			string projectDir = null,
			string requiredVcs = null,
			string revisionFormat = null,
			string tagMatch = "v[0-9]*",
			bool removeTagV = true,
			string copyright = null)
		{
			if (string.IsNullOrEmpty(projectDir))
				projectDir = Directory.GetCurrentDirectory();
			var logger = new ConsoleLogger();

			var (success, _, informationalVersion, _) = Common.GetVersion(projectDir, requiredVcs, revisionFormat, tagMatch, removeTagV, copyright ?? "", logger, true);
			if (!success)
			{
				return null;
			}
			return informationalVersion;
		}

		public static string GetShortVersion(
			string projectDir = null,
			string requiredVcs = null,
			string revisionFormat = null,
			string tagMatch = "v[0-9]*",
			bool removeTagV = true,
			string copyright = null)
		{
			if (string.IsNullOrEmpty(projectDir))
				projectDir = Directory.GetCurrentDirectory();
			var logger = new ConsoleLogger();

			var (success, version, _, _) = Common.GetVersion(projectDir, requiredVcs, revisionFormat, tagMatch, removeTagV, copyright ?? "", logger, true);
			if (!success)
			{
				return null;
			}
			return version;
		}
	}
}
