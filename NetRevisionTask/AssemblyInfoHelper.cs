using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetRevisionTask
{
	internal class AssemblyInfoHelper
	{
		#region Private data

		private string fileName;
		private string attrStart;
		private string attrEnd;
		private string[] lines;
		private string revisionFormat;
		private ILogger logger;

		#endregion Private data

		#region Constructor

		/// <summary>
		/// Initialises a new instance of the <see cref="AssemblyInfoHelper"/> class.
		/// </summary>
		/// <param name="path">The project directory to operate in.</param>
		/// <param name="throwOnMissingFile">Indicates whether an exception is thrown if the AssemblyInfo file was not found.</param>
		/// <param name="logger">A logger.</param>
		public AssemblyInfoHelper(string path, bool throwOnMissingFile, ILogger logger)
		{
			this.logger = logger;

			FindAssemblyInfoFile(path);
			if (fileName == null && throwOnMissingFile)
			{
				throw new FileNotFoundException($@"AssemblyInfo file not found in ""{path}"" or the usual subdirectories.");
			}
			if (fileName != null)
			{
				AnalyseFile();
			}
		}

		#endregion Constructor

		#region Public properties

		/// <summary>
		/// Gets a value indicating whether the AssemblyInfo file was found.
		/// </summary>
		public bool FileExists => fileName != null;

		#endregion Public properties

		#region Public methods

		/// <summary>
		/// Patches the file and injects the revision data.
		/// </summary>
		/// <param name="rf">The configured revision formatter instance.</param>
		/// <param name="fallbackFormat">The fallback format if none is defined in the file.</param>
		/// <param name="simpleAttributes">Indicates whether simple version attributes are processed.</param>
		/// <param name="informationalAttribute">Indicates whether the AssemblyInformationalVersion attribute is processed.</param>
		/// <param name="revOnly">Indicates whether only the last number is replaced by the revision number.</param>
		/// <param name="copyrightAttribute">Indicates whether the copyright year is replaced.</param>
		/// <param name="echo">Indicates whether the final informational version string is displayed.</param>
		public void PatchFile(RevisionFormatter rf, string fallbackFormat, bool simpleAttributes, bool informationalAttribute, bool revOnly, bool copyrightAttribute, bool echo)
		{
			logger?.Trace($@"Patching file ""{fileName}""...");
			string backupFileName = CreateBackup();

			// Read the backup file. If the backup was created earlier, it still contains the source
			// file while the regular file may have been resolved but not restored before. By
			// reading the former source file, we get the correct result and can heal the situation
			// with the next restore run.
			ReadFileLines(backupFileName);

			// Find the revision format for this file
			revisionFormat = FindRevisionFormat();
			if (revisionFormat == null)
			{
				// Nothing defined in this file. Use whatever was specified on the command line or
				// found in any of the projects in the solution.
				revisionFormat = fallbackFormat;
			}
			else
			{
				logger?.Trace("The file defines a revision format: " + revisionFormat);
			}
			if (revisionFormat == null)
			{
				// If we don't have a revision format, there's nothing to replace in this file.
				return;
			}

			// Process all lines in the file
			ResolveAllLines(rf, simpleAttributes, informationalAttribute, revOnly, copyrightAttribute, echo);

			// Write back all lines to the file
			WriteFileLines();
		}

		/// <summary>
		/// Restores the file from a backup.
		/// </summary>
		public void RestoreFile()
		{
			RestoreBackup();
		}

		/// <summary>
		/// Gets the revision format as defined in the file.
		/// </summary>
		/// <returns>The revision format, or null if none is defined.</returns>
		public string GetRevisionFormat()
		{
			// Prefer the backup file if it exists
			string fileToRead = GetBackupFileName();
			if (!File.Exists(fileToRead))
			{
				fileToRead = fileName;
			}

			ReadFileLines(fileToRead);
			return FindRevisionFormat();
		}

		#endregion Public methods

		#region Analysis

		/// <summary>
		/// Analyses the file and detects language-specific values.
		/// </summary>
		private void AnalyseFile()
		{
			switch (Path.GetExtension(fileName).ToLower())
			{
				case ".cs":
					attrStart = "[";
					attrEnd = "]";
					break;
				case ".vb":
					attrStart = "<";
					attrEnd = ">";
					break;
				default:
					throw new InvalidOperationException("Unsupported AssemblyInfo file extension: " + Path.GetExtension(fileName).ToLower());
			}
		}

		/// <summary>
		/// Finds the AssemblyInfo file in the specified directory.
		/// </summary>
		/// <param name="path">The directory to search in.</param>
		private void FindAssemblyInfoFile(string path)
		{
			fileName = Path.Combine(path, "Properties", "AssemblyInfo.cs");
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "My Project", "AssemblyInfo.vb");
			}
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "AssemblyInfo.cs");
			}
			if (!File.Exists(fileName))
			{
				fileName = Path.Combine(path, "AssemblyInfo.vb");
			}
			if (!File.Exists(fileName))
			{
				fileName = null;
			}
		}

		/// <summary>
		/// Finds the specified revision format from the file.
		/// </summary>
		/// <returns>The revision format, or null if it was not found.</returns>
		private string FindRevisionFormat()
		{
			foreach (string line in lines)
			{
				Match match = Regex.Match(
					line,
					@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyInformationalVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
					RegexOptions.IgnoreCase);
				if (match.Success)
				{
					return match.Groups[2].Value;
				}
			}
			return null;
		}

		#endregion Analysis

		#region File access

		/// <summary>
		/// Gets the name of the backup file for the current file.
		/// </summary>
		/// <returns>The backup file name.</returns>
		private string GetBackupFileName()
		{
			return fileName + ".bak";
		}

		/// <summary>
		/// Creates a backup of the file if it does not already exist.
		/// </summary>
		/// <returns>The name of the backup file.</returns>
		private string CreateBackup()
		{
			string backup = GetBackupFileName();
			if (!File.Exists(backup))
			{
				File.Copy(fileName, backup);
				logger?.Trace($@"Created backup to ""{Path.GetFileName(backup)}"".");
			}
			else
			{
				logger?.Warning($@"Backup ""{Path.GetFileName(backup)}"" already exists, skipping.");
			}
			return backup;
		}

		/// <summary>
		/// Restores the file from a backup if it exists.
		/// </summary>
		private void RestoreBackup()
		{
			string backup = GetBackupFileName();
			if (File.Exists(backup))
			{
				File.Delete(fileName);
				File.Move(backup, fileName);
				logger?.Trace($@"Restored backup file ""{backup}"".");
			}
			else
			{
				logger?.Warning($@"Backup file ""{backup}"" does not exist, skipping.");
			}
		}

		/// <summary>
		/// Reads all lines of a file.
		/// </summary>
		/// <param name="readFileName">The name of the file to read.</param>
		private void ReadFileLines(string readFileName)
		{
			lines = File.ReadAllLines(readFileName);
		}

		/// <summary>
		/// Writes all lines into the file.
		/// </summary>
		private void WriteFileLines()
		{
			int retryCounter = 20;
			while (true)
			{
				try
				{
					File.WriteAllLines(fileName, lines, Encoding.UTF8);
					break;
				}
				catch (IOException)
				{
					retryCounter--;
					if (retryCounter < 0) throw;
					logger?.Error("IOException when writing file, waiting for retry...");
					Task.Delay(100).Wait();
				}
			}
		}

		#endregion File access

		#region Resolving

		/// <summary>
		/// Resolves all attributes in the file.
		/// </summary>
		/// <param name="rf">The revision format for the file.</param>
		/// <param name="simpleAttributes">Indicates whether simple version attributes are processed.</param>
		/// <param name="informationalAttribute">Indicates whether the AssemblyInformationalVersion attribute is processed.</param>
		/// <param name="revOnly">Indicates whether only the last number is replaced by the revision number.</param>
		/// <param name="copyrightAttribute">Indicates whether the copyright year is replaced.</param>
		/// <param name="echo">Indicates whether the final informational version string is displayed.</param>
		private void ResolveAllLines(RevisionFormatter rf, bool simpleAttributes, bool informationalAttribute, bool revOnly, bool copyrightAttribute, bool echo)
		{
			// Preparing a truncated dotted-numeric version if we may need it
			string truncVersion = null;
			if (simpleAttributes && !revOnly)
			{
				truncVersion = rf.ResolveShort(revisionFormat);
			}

			// Checking the revision number if we may need it
			int revNum = rf.RevisionData.RevisionNumber;
			if (revOnly)
			{
				if (revNum == 0)
				{
					// No revision number available, try to use the format as a number
					string revisionId = rf.Resolve(revisionFormat);
					if (int.TryParse(revisionId, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out revNum))
					{
						logger?.Trace($"Using revision number {revNum} for /revonly from format.");
					}
				}
				if (revNum == 0)
				{
					// Still nothing useful available
					logger?.Warning("Revision number is 0. Did you really mean to use /revonly?");
				}
				if (revNum > ushort.MaxValue)
				{
					throw new ArgumentOutOfRangeException($"Revision number {revNum} is greater than {ushort.MaxValue} and cannot be used here. Consider using the offset option.");
				}
			}

			// Process all lines
			Match match;
			for (int i = 0; i < lines.Length; i++)
			{
				if (revOnly)
				{
					// Replace the fourth part of AssemblyVersion and AssemblyFileVersion with the
					// revision number. If less parts are currently specified, zeros are inserted.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyVersion\s*\(\s*""[0-9]+)(\.[0-9]+)?(\.[0-9]+)?(\.[0-9]+)?(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] =
							match.Groups[1].Value +
							(match.Groups[2].Success ? match.Groups[2].Value : ".0") +
							(match.Groups[3].Success ? match.Groups[3].Value : ".0") +
							"." + revNum +
							match.Groups[5].Value;
						logger?.Success("Found AssemblyVersion attribute for revision number only.");
					}
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyFileVersion\s*\(\s*""[0-9]+)(\.[0-9]+)?(\.[0-9]+)?(\.[0-9]+)?(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] =
							match.Groups[1].Value +
							(match.Groups[2].Success ? match.Groups[2].Value : ".0") +
							(match.Groups[3].Success ? match.Groups[3].Value : ".0") +
							"." + revNum +
							match.Groups[5].Value;
						logger?.Success("Found AssemblyFileVersion attribute for revision number only.");
					}
				}

				if (simpleAttributes && !revOnly)
				{
					// Replace the entire version in AssemblyVersion and AssemblyFileVersion with
					// the truncated dotted-numeric version.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] = match.Groups[1].Value + truncVersion + match.Groups[3].Value;
						logger?.Success("Found AssemblyVersion attribute.");
						logger?.Trace("  Replaced \"" + match.Groups[2].Value + "\" with \"" + truncVersion + "\".");
					}
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyFileVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						lines[i] = match.Groups[1].Value + truncVersion + match.Groups[3].Value;
						logger?.Success("Found AssemblyFileVersion attribute.");
						logger?.Trace("  Replaced \"" + match.Groups[2].Value + "\" with \"" + truncVersion + "\".");
					}
				}

				if (informationalAttribute && !revOnly)
				{
					// Replace the entire value of AssemblyInformationalVersion with the resolved
					// string of what was already there. This ignores the fallback format, should
					// one be given on the command line.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyInformationalVersion\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						string revisionId = rf.Resolve(match.Groups[2].Value);
						lines[i] = match.Groups[1].Value + revisionId + match.Groups[3].Value;
						logger?.Success("Found AssemblyInformationalVersion attribute.");
						logger?.Trace("  Replaced \"" + match.Groups[2].Value + "\" with \"" + revisionId + "\".");
						if (echo)
						{
							Console.WriteLine("Version: " + revisionId);
						}
					}
				}

				if (copyrightAttribute)
				{
					// Replace the entire value of Copyright with the resolved string of what was
					// already there.
					match = Regex.Match(
						lines[i],
						@"^(\s*\" + attrStart + @"\s*assembly\s*:\s*AssemblyCopyright\s*\(\s*"")(.*?)(""\s*\)\s*\" + attrEnd + @".*)$",
						RegexOptions.IgnoreCase);
					if (match.Success)
					{
						string copyrightText = rf.Resolve(match.Groups[2].Value);
						lines[i] = match.Groups[1].Value + copyrightText + match.Groups[3].Value;
						logger?.Success("Found AssemblyCopyright attribute.");
						logger?.Trace("  Replaced \"" + match.Groups[2].Value + "\" with \"" + copyrightText + "\".");
					}
				}
			}
		}

		#endregion Resolving
	}
}
