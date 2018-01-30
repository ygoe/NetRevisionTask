using System;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace NetRevisionTask.Tasks
{
	/// <summary>
	/// Implements the RestoreAssemblyInfo MSBuild task.
	/// </summary>
	public class RestoreAssemblyInfo : MSBuildTask
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

		#endregion Properties

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
			logger.Trace("NetRevisionTask: RestoreAssemblyInfo");

			var aih = new AssemblyInfoHelper(ProjectDir, true, logger);
			aih.RestoreFile();

			return true;
		}

		#endregion Main task execution
	}
}
