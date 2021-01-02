using System;
using System.IO;
using System.Reflection;

namespace NetRevisionTask
{
	/// <summary>
	/// Provides public API methods.
	/// </summary>
	public class Api
	{
		#region Simple API

		public static string GetVersion(
			string projectDir = null,
			string requiredVcs = null,
			string revisionFormat = null,
			string tagMatch = "v[0-9]*",
			bool removeTagV = true,
			string copyright = null,
			string configurationName = null,
			string errorOnModifiedRepoPattern = null)
		{
			if (string.IsNullOrEmpty(projectDir))
				projectDir = Directory.GetCurrentDirectory();
			var logger = new ConsoleLogger();

			var result = Common.GetVersion(projectDir, requiredVcs, revisionFormat, tagMatch, removeTagV, copyright ?? "", logger, true,
				configurationName, errorOnModifiedRepoPattern);
			if (!result.success)
			{
				return null;
			}
			return result.informationalVersion;
		}

		public static string GetShortVersion(
			string projectDir = null,
			string requiredVcs = null,
			string revisionFormat = null,
			string tagMatch = "v[0-9]*",
			bool removeTagV = true,
			string copyright = null,
			string configurationName = null,
			string errorOnModifiedRepoPattern = null)
		{
			if (string.IsNullOrEmpty(projectDir))
				projectDir = Directory.GetCurrentDirectory();
			var logger = new ConsoleLogger();

			var result = Common.GetVersion(projectDir, requiredVcs, revisionFormat, tagMatch, removeTagV, copyright ?? "", logger, true,
				configurationName, errorOnModifiedRepoPattern);
			if (!result.success)
			{
				return null;
			}
			return result.version;
		}

		#endregion Simple API

		#region Interactive API

		/// <summary>
		/// The instance that contains data about a revision of the project directory.
		/// </summary>
		public RevisionData RevisionData = null;

		/// <summary>
		/// The instance of the logger used by the Interactive API.
		/// </summary>
		private ILogger logger = null;

		/// <summary>
		/// The revision format template.
		/// </summary>
		private string revisionFormat = null;

		/// <summary>
		/// The instance that resolves a revision format with placeholders to a revision ID from the
		/// specified revision data.
		/// </summary>
		private RevisionFormatter revisionFormatter = null;

		/// <summary>
		/// Create an instance of the Interactive API.
		/// </summary>
		/// <param name="projectDir">
		/// The project directory to process by the version control system.
		/// </param>
		/// <param name="requiredVcs">
		/// The required <see cref="IVcsProvider.Name"/>, or null if any VCS is acceptable.
		/// </param>
		/// <param name="revisionFormat">The revision format template.</param>
		/// <param name="tagMatch">
		/// The global pattern of tag names to match. If empty or "*", all tags are accepted.
		/// </param>
		/// <param name="removeTagV">
		/// The value indicating whether a leading "v" followed by a digit will be removed from the
		/// tag name.
		/// </param>
		/// <param name="configurationName">The value of the build configuration name.</param>
		/// <returns>True if initialization was successful, false otherwise.</returns>
		public Api(
			ILogger logger = null,
			string projectDir = null,
			string requiredVcs = null,
			string revisionFormat = null,
			string tagMatch = "v[0-9]*",
			bool removeTagV = true,
			string configurationName = null)
		{
			// instantiate the console logger if no custom logger was provided
			if (logger == null)
			{
				logger = new ConsoleLogger();
			}
			this.logger = logger;
			logger.Success(typeof(Api).GetTypeInfo().Assembly
				.GetCustomAttribute<AssemblyTitleAttribute>().Title + " v" +
				typeof(Api).GetTypeInfo().Assembly
				.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
				.InformationalVersion);
			logger.Trace($"Constructing {typeof(Api).FullName} instance");
			logger.Trace($"Assigned logger '{logger.GetType().FullName}'");

			// analyze the working directory
			RevisionData = Common.ProcessDirectory(projectDir, requiredVcs, tagMatch, logger);
			if (!string.IsNullOrEmpty(requiredVcs) && RevisionData.VcsProvider == null)
			{
				string message = $"The required version control system '{requiredVcs}' is not " +
					"available or not used in the project directory";
				logger.Error(message);

				throw new Exception(message);
			}

			// initialize the revision format
			if (string.IsNullOrEmpty(revisionFormat))
			{
				revisionFormat = Common.GetRevisionFormat(projectDir, logger, true);
			}
			if (string.IsNullOrEmpty(revisionFormat))
			{
				revisionFormat = RevisionData.GetDefaultRevisionFormat(logger);
			}
			this.revisionFormat = revisionFormat;

			// initialize the RevisionFormatter
			revisionFormatter = new RevisionFormatter
			{
				RevisionData = RevisionData,
				RemoveTagV = removeTagV,
				BuildTime = DateTimeOffset.Now,
				ConfigurationName = configurationName
			};

			// initialization successfully completed
			logger.Trace($"Instantiation of {typeof(Api).FullName} successfully completed");
		}

		/// <summary>
		/// Destroy the instance of the Interactive API.
		/// </summary>
		~Api()
		{
			// initialization successfully completed
			if (logger != null)
			{
				logger.Trace($"Destructing {typeof(Api).FullName} instance");
			}
		}

		/// <summary>
		/// Get the short version.
		/// </summary>
		/// <returns>The short version on success, null otherwise.</returns>
		public string GetShortVersion()
		{
			return revisionFormatter.ResolveShort(revisionFormat);
		}

		/// <summary>
		/// Get the full (informational) version.
		/// </summary>
		/// <returns>The full (informational) version on success, null otherwise.</returns>
		public string GetVersion()
		{
			return revisionFormatter.Resolve(revisionFormat);
		}

		/// <summary>
		/// Resolves placeholders in a revision format string using the current data.
		/// </summary>
		/// <param name="str">The revision format string to resolve.</param>
		/// <returns>The resolved revision string.</returns>
		public string Resolve(string str)
		{
			if (string.IsNullOrEmpty(str))
			{
				return str;
			}
			else
			{
				return revisionFormatter.Resolve(str);
			}
		}

		#endregion Interactive API
	}
}
