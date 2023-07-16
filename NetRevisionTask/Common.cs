using System;
using System.Collections.Generic;
using System.Reflection;
using NetRevisionTask.VcsProviders;

namespace NetRevisionTask
{
	internal class Common
	{
		public static (bool Success, string Version, string InformationalVersion, string Copyright)
			GetVersion(string projectDir, string requiredVcs, string revisionFormat, string tagMatch, bool removeTagV, string copyright, ILogger logger, bool warnOnMissing)
		{
			// Analyse working directory
			RevisionData data = ProcessDirectory(projectDir, requiredVcs, tagMatch, logger);
			if (!string.IsNullOrEmpty(requiredVcs) && data.VcsProvider == null)
			{
				logger.Error($@"The required version control system ""{requiredVcs}"" is not available or not used in the project directory.");
				return (false, null, null, null);
			}
			if (string.IsNullOrEmpty(revisionFormat))
			{
				revisionFormat = GetRevisionFormat(projectDir, logger, warnOnMissing);
			}
			if (string.IsNullOrEmpty(revisionFormat))
			{
				revisionFormat = data.GetDefaultRevisionFormat(logger);
			}

			var rf = new RevisionFormatter { RevisionData = data, RemoveTagV = removeTagV };
			try
			{
				return (true, rf.ResolveShort(revisionFormat), rf.Resolve(revisionFormat), rf.Resolve(copyright));
			}
			catch (FormatException ex)
			{
				logger.Error(ex.Message);
				return (false, null, null, null);
			}
		}

		/// <summary>
		/// Processes the specified directory with a suitable VCS provider.
		/// </summary>
		/// <param name="path">The directory to process.</param>
		/// <param name="requiredVcs">The required VCS name, or null if any VCS is acceptable.</param>
		/// <param name="tagMatch">The pattern of tag names to match. If empty, all tags are accepted.</param>
		/// <param name="logger">A logger.</param>
		/// <returns>Data about the revision. If no provider was able to process the directory,
		///   dummy data is returned.</returns>
		public static RevisionData ProcessDirectory(string path, string requiredVcs, string tagMatch, ILogger logger)
		{
			RevisionData data = null;

			// Try to process the directory with all available VCS providers
			logger.Trace("Processing directory...");
			foreach (var provider in GetVcsProviders(logger))
			{
				logger.Trace("Found VCS provider: " + provider);

				if (!string.IsNullOrEmpty(requiredVcs) &&
					!provider.Name.Equals(requiredVcs, StringComparison.OrdinalIgnoreCase))
				{
					logger.Trace("Provider is not what is required, skipping.");
					continue;
				}

				if (provider.CheckEnvironment())
				{
					logger.Success("Provider can be executed in this environment.");
					if (provider.CheckDirectory(path, out string rootPath))
					{
						logger.Success("Provider can process this directory.");
						data = provider.ProcessDirectory(path, tagMatch);
						break;
					}
				}
			}

			if (data == null)
			{
				// No provider could process the directory, return dummy data
				logger.Warning("No VCS provider used, using dummy revision data.");
				data = new RevisionData
				{
					CommitHash = "0000000000000000000000000000000000000000",
					CommitTime = DateTimeOffset.Now,
					IsModified = false,
					RevisionNumber = 0
				};
			}

			data.Normalize();
			data.DumpData(logger);
			return data;
		}

		/// <summary>
		/// Searches all VCS providers implemented in the current assembly and creates an instance
		/// of each type.
		/// </summary>
		/// <param name="logger">A logger.</param>
		/// <returns>An array containing all created VCS provider instances.</returns>
		private static IVcsProvider[] GetVcsProviders(ILogger logger)
		{
			var providers = new List<IVcsProvider>();
			var myAssembly = typeof(IVcsProvider).GetTypeInfo().Assembly;   // equivalent to GetExecutingAssembly()

			foreach (Type type in myAssembly.GetTypes())
			{
				if (!type.GetTypeInfo().IsInterface &&
					typeof(IVcsProvider).GetTypeInfo().IsAssignableFrom(type))
				{
					var provider = (IVcsProvider)Activator.CreateInstance(type);
					provider.Logger = logger;
					providers.Add(provider);
				}
			}
			return providers.ToArray();
		}

		/// <summary>
		/// Determines the revision format from source files, if present.
		/// </summary>
		/// <param name="projectDir">The project directory to look for the AssemblyInfo file in.</param>
		/// <param name="logger">A logger.</param>
		/// <param name="warnOnMissing">Logs a warning if the file is missing.</param>
		/// <returns>The format string if found; otherwise, null.</returns>
		public static string GetRevisionFormat(string projectDir, ILogger logger, bool warnOnMissing)
		{
			logger.Trace("No format specified, searching AssemblyInfo source file.");
			var aih = new AssemblyInfoHelper(projectDir, false, logger);
			if (!aih.FileExists)
			{
				if (warnOnMissing)
					logger.Warning("AssemblyInfo source file not found. Using default revision format.");
				else
					logger.Trace("AssemblyInfo source file not found. Using default revision format.");
				return null;
			}

			string revisionFormat = aih.GetRevisionFormat();
			if (!string.IsNullOrEmpty(revisionFormat))
			{
				logger.Trace("Found format: " + revisionFormat);
			}
			return revisionFormat;
		}
	}
}
