using System;
using Microsoft.Build.Framework;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace NetRevisionTask.Tasks
{
	/// <summary>
	/// Implements the PatchAssemblyInfo MSBuild task.
	/// </summary>
	public class PatchAssemblyInfo : MSBuildTask
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
		/// Gets or sets the intermediate output path of the build process. The patched AssemblyInfo
		/// file will be saved there.
		/// </summary>
		public string IntermediateOutputPath { get; set; }

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
		/// Gets or sets a value indicating whether simple version attributes are processed.
		/// </summary>
		public bool ResolveSimpleAttributes { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the AssemblyInformationalVersion attribute is
		/// processed.
		/// </summary>
		public bool ResolveInformationalAttribute { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether only the last number is replaced by the revision
		/// number.
		/// </summary>
		public bool RevisionNumberOnly { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the copyright value should be resolved.
		/// </summary>
		public bool ResolveCopyright { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the final informational version string is
		/// displayed to the console.
		/// </summary>
		public bool ShowRevision { get; set; }

		#endregion Properties

		#region Task output properties

		/// <summary>
		/// Gets or sets the name of the source AssemblyInfo file.
		/// </summary>
		[Output]
		public string SourceAssemblyInfo { get; set; }

		/// <summary>
		/// Gets or sets the name of the patched AssemblyInfo file.
		/// </summary>
		[Output]
		public string PatchedAssemblyInfo { get; set; }

		#endregion Task output properties

		#region Main task execution

		/// <summary>
		/// Executes the task.
		/// </summary>
		/// <returns>true if the execution was successful; otherwise, false.</returns>
		public override bool Execute()
		{
			// This task is only required if MSBuild does not generate the assembly info data.
			if (GenerateAssemblyInfo)
			{
				return true;
			}

			logger = new TaskLogger(Log);
			logger.Trace("NetRevisionTask: PatchAssemblyInfo");

			// Analyse working directory
			RevisionData data = Common.ProcessDirectory(ProjectDir, RequiredVcs, TagMatch, logger);
			if (!string.IsNullOrEmpty(RequiredVcs) && data.VcsProvider == null)
			{
				logger.Error($@"Required VCS ""{RequiredVcs}"" not present.");
				return false;
			}
			if (string.IsNullOrEmpty(RevisionFormat))
			{
				RevisionFormat = Common.GetRevisionFormat(ProjectDir, logger, true);
			}
			if (string.IsNullOrEmpty(RevisionFormat))
			{
				RevisionFormat = data.GetDefaultRevisionFormat(logger);
			}

			var rf = new RevisionFormatter { RevisionData = data, RemoveTagV = RemoveTagV };
			try
			{
				var aih = new AssemblyInfoHelper(ProjectDir, true, logger);
				SourceAssemblyInfo = aih.FileName;
				PatchedAssemblyInfo = aih.PatchFile(
					IntermediateOutputPath,
					rf,
					RevisionFormat,
					ResolveSimpleAttributes,
					ResolveInformationalAttribute,
					RevisionNumberOnly,
					ResolveCopyright,
					ShowRevision);
			}
			catch (FormatException ex)
			{
				logger.Error(ex.Message);
				return false;
			}

			return true;
		}

		#endregion Main task execution
	}
}
