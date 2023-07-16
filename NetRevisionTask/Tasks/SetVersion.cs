﻿using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace NetRevisionTask.Tasks
{
	/// <summary>
	/// Implements the SetVersion MSBuild task.
	/// </summary>
	public class SetVersion : MSBuildTask
	{
		#region Private data

		private ILogger logger;

		#endregion Private data

		#region Properties

		/// <summary>
		/// Gets or sets the project directory.
		/// </summary>
		public string ProjectDir { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether MSBuild generates the assembly info data.
		/// </summary>
		/// <remarks>
		/// If this is true, we can provide the revision number through the <see cref="Version"/>
		/// output parameter. This is the default for the new VS2017 project style. Otherwise, the
		/// AssemblyInfo file needs to be patched and restored like the classic NetRevisionTool
		/// application does.
		/// </remarks>
		public bool GenerateAssemblyInfo { get; set; }

		/// <summary>
		/// Gets or sets the revision format template.
		/// </summary>
		public string RevisionFormat { get; set; }

		/// <summary>
		/// Gets or sets the <see cref="IVcsProvider.Name"/> of the version control system that
		/// needs to be present at build time. No VCS is required if this is empty.
		/// </summary>
		public string RequiredVcs { get; set; }

		/// <summary>
		/// Gets or sets the glob pattern of tag names to match. If empty or "*", all tags are
		/// accepted.
		/// </summary>
		/// <example>
		/// The pattern <code>v[0-9]*</code> matches the common version tag names.
		/// </example>
		public string TagMatch { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a leading "v" followed by a digit will be
		/// removed from the tag name.
		/// </summary>
		public bool RemoveTagV { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the copyright value should be resolved.
		/// </summary>
		public bool ResolveCopyright { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the determined revision ID is printed during the
		/// build with higher importance than normal, so it can be seen more easily.
		/// </summary>
		public bool ShowRevision { get; set; }

		#endregion Properties

		#region Task output properties

		/// <summary>
		/// Gets or sets the resolved version string of the version number only.
		/// </summary>
		[Output]
		public string Version { get; set; }

		/// <summary>
		/// Gets or sets the resolved informational version string with additional details.
		/// </summary>
		[Output]
		public string InformationalVersion { get; set; }

		/// <summary>
		/// Gets or sets the resolved copyright string.
		/// </summary>
		[Output]
		public string Copyright { get; set; }

		#endregion Task output properties

		#region Main task execution

		/// <summary>
		/// Executes the task.
		/// </summary>
		/// <returns>true if the execution was successful; otherwise, false.</returns>
		public override bool Execute()
		{
#if NETFULL
			string targetFramework = ".NET Framework";
#else
			string targetFramework = ".NET Standard";
#endif

			logger = new TaskLogger(Log);
			logger.Trace($"NetRevisionTask: SetVersion ({targetFramework})");

			var result = Common.GetVersion(ProjectDir, RequiredVcs, RevisionFormat, TagMatch, RemoveTagV, Copyright ?? "", logger, !GenerateAssemblyInfo);
			if (!result.success)
			{
				return false;
			}
			Version = result.version;
			InformationalVersion = result.informationalVersion;
			if (ResolveCopyright)
			{
				Copyright = result.copyright;
			}

			if (ShowRevision)
			{
				logger.Info($"Version: {Version}");
				if (InformationalVersion != Version)
				{
					logger.Info($"InformationalVersion: {InformationalVersion}");
				}
			}
			else
			{
				logger.Success($"Version: {Version}");
				if (InformationalVersion != Version)
				{
					logger.Success($"InformationalVersion: {InformationalVersion}");
				}
			}
			return true;
		}

		#endregion Main task execution
	}
}
