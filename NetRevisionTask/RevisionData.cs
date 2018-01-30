using System;
using System.Text.RegularExpressions;
using NetRevisionTask.VcsProviders;

namespace NetRevisionTask
{
	/// <summary>
	/// Contains data about a revision of the source directory.
	/// </summary>
	internal class RevisionData
	{
		#region Management properties

		/// <summary>
		/// Gets or sets the VCS provider that provided the data in the current instance.
		/// </summary>
		public IVcsProvider VcsProvider { get; set; }

		#endregion Management properties

		#region Revision data properties

		/// <summary>
		/// Gets or sets the commit hash of the currently checked out revision.
		/// </summary>
		public string CommitHash { get; set; }

		/// <summary>
		/// Gets or sets the revision number of the currently checked out revision.
		/// </summary>
		public int RevisionNumber { get; set; }

		/// <summary>
		/// Gets or sets the commit time of the currently checked out revision.
		/// </summary>
		public DateTimeOffset CommitTime { get; set; }

		/// <summary>
		/// Gets or sets the author time of the currently checked out revision.
		/// </summary>
		public DateTimeOffset AuthorTime { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the working copy is modified.
		/// </summary>
		public bool IsModified { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the working copy contains mixed revisions.
		/// </summary>
		public bool IsMixed { get; set; }

		/// <summary>
		/// Gets or sets the repository URL of the working directory.
		/// </summary>
		public string RepositoryUrl { get; set; }

		/// <summary>
		/// Gets or sets the committer name of the currently checked out revision.
		/// </summary>
		public string CommitterName { get; set; }

		/// <summary>
		/// Gets or sets the committer e-mail address of the currently checked out revision.
		/// </summary>
		public string CommitterEMail { get; set; }

		/// <summary>
		/// Gets or sets the author name of the currently checked out revision.
		/// </summary>
		public string AuthorName { get; set; }

		/// <summary>
		/// Gets or sets the author e-mail address of the currently checked out revision.
		/// </summary>
		public string AuthorEMail { get; set; }

		/// <summary>
		/// Gets or sets the branch currently checked out in the working directory.
		/// </summary>
		public string Branch { get; set; }

		/// <summary>
		/// Gets or sets the name of the most recent matching tag.
		/// </summary>
		public string Tag { get; set; }

		/// <summary>
		/// Gets or sets the number of commits since the most recent matching tag.
		/// </summary>
		public int CommitsAfterTag { get; set; }

		#endregion Revision data properties

		#region Operations

		/// <summary>
		/// Normalizes all data properties to prevent null values.
		/// </summary>
		public void Normalize()
		{
			if (CommitHash == null) CommitHash = "";
			if (RepositoryUrl == null) RepositoryUrl = "";
			if (CommitterName == null) CommitterName = "";
			if (CommitterEMail == null) CommitterEMail = "";
			if (AuthorName == null) AuthorName = "";
			if (AuthorEMail == null) AuthorEMail = "";
			if (Branch == null) Branch = "";
			if (Tag == null) Tag = "";
		}

		/// <summary>
		/// Dumps the revision data is debug output is enabled.
		/// </summary>
		/// <param name="logger">A logger.</param>
		public void DumpData(ILogger logger)
		{
			logger.Trace("Revision data:");
			logger.Trace("  AuthorEMail: " + AuthorEMail);
			logger.Trace("  AuthorName: " + AuthorName);
			logger.Trace("  AuthorTime: " + AuthorTime.ToString("yyyy-MM-dd HH:mm:ss K"));
			logger.Trace("  Branch: " + Branch);
			logger.Trace("  CommitHash: " + CommitHash);
			logger.Trace("  CommitterEMail: " + CommitterEMail);
			logger.Trace("  CommitterName: " + CommitterName);
			logger.Trace("  CommitTime: " + CommitTime.ToString("yyyy-MM-dd HH:mm:ss K"));
			logger.Trace("  IsMixed: " + IsMixed);
			logger.Trace("  IsModified: " + IsModified);
			logger.Trace("  RepositoryUrl: " + RepositoryUrl);
			logger.Trace("  RevisionNumber: " + RevisionNumber);
			logger.Trace("  Tag: " + Tag + " + " + CommitsAfterTag);
			logger.Trace("  VcsProvider: " + VcsProvider);
		}

		/// <summary>
		/// Returns a default revision format based on the available data.
		/// </summary>
		/// <param name="logger">A logger.</param>
		/// <returns>The default revision format.</returns>
		public string GetDefaultRevisionFormat(ILogger logger)
		{
			if (!string.IsNullOrEmpty(CommitHash) && !Regex.IsMatch(CommitHash, "^0+$"))
			{
				logger?.Trace("No format available, using default format for commit hash.");
				return "{semvertag}+{chash:7}";
			}
			if (RevisionNumber > 0)
			{
				logger?.Trace("No format available, using default format for revision number.");
				return "0.0.{revnum}";
			}

			logger?.Trace("No format available, using empty format.");
			return "0.0.1";
		}

		#endregion Operations
	}
}
