using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Unclassified.Util;

namespace NetRevisionTask.VcsProviders
{
	internal class SubversionProvider : IVcsProvider
	{
		#region Private data

		private string svnExeName;
		private string svnversionExeName;
		private string svnExec;
		private string svnversionExec;
		private bool isWindows;

		#endregion Private data

		#region Constructors

		public SubversionProvider()
		{
#if NETFULL
			isWindows = Environment.OSVersion.Platform != PlatformID.Unix;
#else
			isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
			svnExeName = isWindows ? "svn.exe" : "svn";
			svnversionExeName = isWindows ? "svnversion.exe" : "svnversion";
		}

		#endregion Constructors

		#region Properties

		public ILogger Logger { get; set; }

		#endregion Properties

		#region Overridden methods

		public override string ToString() => "Subversion VCS provider";

		#endregion Overridden methods

		#region IVcsProvider members

		public string Name => "svn";

		public bool CheckEnvironment()
		{
			Logger?.Trace("Subversion environment check...");
			svnExec = FindSvnBinary();
			if (svnExec == null)
			{
				Logger?.Warning("  svn executable not found.");
				return false;
			}

			svnversionExec = Path.Combine(Path.GetDirectoryName(svnExec), svnversionExeName);
			if (!File.Exists(svnversionExec))
			{
				Logger?.Warning("  svnversion executable not found.");
				return false;
			}
			return true;
		}

		public bool CheckDirectory(string path, out string rootPath)
		{
			// Scan directory tree upwards for the .svn directory
			Logger?.Trace("Checking directory tree for Subversion working directory...");
			do
			{
				Logger?.Trace($"  Testing: {path}");
				if (Directory.Exists(Path.Combine(path, ".svn")))
				{
					Logger?.Success($"  Found {path}");
					rootPath = path;
					return true;
				}
				path = Path.GetDirectoryName(path);
			}
			while (!string.IsNullOrEmpty(path));

			// Nothing found
			Logger?.Trace("Not a Subversion working directory.");
			rootPath = null;
			return false;
		}

		public RevisionData ProcessDirectory(string path, string tagMatch)
		{
			// Initialise data
			var data = new RevisionData
			{
				VcsProvider = this
			};

			// svn assumes case-sensitive path names on Windows, which is... bad.
			string fixedPath = PathUtil.GetExactPath(path);
			if (fixedPath != path)
			{
				Logger?.Warning($"Corrected path to: {fixedPath}");
			}
			path = fixedPath;

			// Get revision number
			Logger?.Trace("Executing: svnversion");
			Logger?.Trace($"  WorkingDirectory: {path}");
			var psi = new ProcessStartInfo(svnversionExec)
			{
				WorkingDirectory = path,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			var process = Process.Start(psi);
			string line = null;
			while (!process.StandardOutput.EndOfStream)
			{
				line = process.StandardOutput.ReadLine();
				Logger?.RawOutput(line);
				// Possible output:
				// 1234          Revision 1234
				// 1100:1234     Mixed revisions 1100 to 1234
				// 1234M         Revision 1234, modified
				// 1100:1234MP   Mixed revisions 1100 to 1234, modified and partial
				Match m = Regex.Match(line, @"^([0-9]+:)?([0-9]+)");
				if (m.Success)
				{
					data.IsMixed = m.Groups[1].Success;
					data.RevisionNumber = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
					break;
				}
			}
			process.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
			if (!process.WaitForExit(1000))
			{
				process.Kill();
			}

			if (data.RevisionNumber == 0) return data;   // Try no more

			Logger?.Trace("Executing: svn status");
			Logger?.Trace($"  WorkingDirectory: {path}");
			psi = new ProcessStartInfo(svnExec, "status")
			{
				WorkingDirectory = path,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};
			process = Process.Start(psi);
			line = null;
			while (!process.StandardOutput.EndOfStream)
			{
				line = process.StandardOutput.ReadLine();
				Logger?.RawOutput(line);
			}
			if (!process.WaitForExit(1000))
			{
				process.Kill();
			}
			data.IsModified = !string.IsNullOrEmpty(line);

			Logger?.Trace($"Executing: svn info --revision {data.RevisionNumber}");
			Logger?.Trace($"  WorkingDirectory: {path}");
			psi = new ProcessStartInfo(svnExec, $"info --revision {data.RevisionNumber}")
			{
				WorkingDirectory = path,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				//StandardOutputEncoding = Encoding.Default   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
			};
			process = Process.Start(psi);
			line = null;
			string workingCopyRootPath = null;
			while (!process.StandardOutput.EndOfStream)
			{
				line = process.StandardOutput.ReadLine();
				Logger?.RawOutput(line);
				// WARNING: This is the info about the commit that has been *last updated to* in the
				//          specified *subdirectory* of the working directory. The revision number
				//          printed here belongs to that commit, but does not necessarily match the
				//          revision number determined above by 'svnversion'.
				//          If you need consistent data on the commit other than the revision
				//          number, be sure to always update the entire working directory and set
				//          the VCS path to its root.
				Match m = Regex.Match(line, @"^Working Copy Root Path: (.+)");
				if (m.Success)
				{
					workingCopyRootPath = m.Groups[1].Value.Trim();
				}
				// Try to be smart and detect the branch from the relative path. This should work
				// fine if the standard SVN repository tree is used.
				m = Regex.Match(line, @"^Relative URL: \^(.+)");
				if (m.Success)
				{
					data.Branch = m.Groups[1].Value.Trim().TrimStart('/');
					if (data.Branch.StartsWith("branches/", StringComparison.Ordinal))
					{
						data.Branch = data.Branch.Substring(9);
					}

					// Cut off the current subdirectory
					if (workingCopyRootPath != null &&
						path.StartsWith(workingCopyRootPath, StringComparison.OrdinalIgnoreCase))
					{
						int subdirLength = path.Length - workingCopyRootPath.Length;
						data.Branch = data.Branch.Substring(0, data.Branch.Length - subdirLength);
					}
				}
				// Use "Repository Root" because "URL" is only the URL where the working directory
				// was checked out from. This can be a subdirectory of the repository if only a part
				// of it was checked out, like "/trunk" or a branch.
				m = Regex.Match(line, @"^Repository Root: (.+)");
				if (m.Success)
				{
					data.RepositoryUrl = m.Groups[1].Value.Trim();
				}
				m = Regex.Match(line, @"^Last Changed Author: (.+)");
				if (m.Success)
				{
					data.CommitterName = m.Groups[1].Value.Trim();
				}
				m = Regex.Match(line, @"^Last Changed Date: ([0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} [+-][0-9]{4})");
				if (m.Success)
				{
					data.CommitTime = DateTimeOffset.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
				}
			}
			if (!process.WaitForExit(1000))
			{
				process.Kill();
			}
			return data;
		}

		#endregion IVcsProvider members

		#region Private helper methods

		private string FindSvnBinary()
		{
			string svn = null;

			// Try the PATH environment variable
			if (svn == null)
			{
				string pathEnv = Environment.GetEnvironmentVariable("PATH");
				foreach (string _dir in pathEnv.Split(Path.PathSeparator))
				{
					string dir = _dir;
					if (dir.StartsWith("\"") && dir.EndsWith("\""))
					{
						// Strip quotes (no Path.PathSeparator supported in quoted directories though)
						dir = dir.Substring(1, dir.Length - 2);
					}
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Logger?.Success($@"Found {svnExeName} in ""{dir}"" via %PATH%");
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
			}

			var registry = new WindowsRegistry();

			// If TortoiseSVN has been installed with command-line binaries
			if (svn == null && isWindows)
			{
				string keyPath = @"Software\TortoiseSVN";
				string loc = registry.GetStringValue("HKLM", keyPath, "Directory");
				if (loc != null)
				{
					svn = Path.Combine(loc, Path.Combine(@"bin", svnExeName));
					if (!File.Exists(svn))
					{
						svn = null;
					}
					else
					{
						Logger?.Success($@"Found {svnExeName} in ""{loc}"" via HKLM\\{keyPath}\\Directory");
					}
				}
			}

			// Read registry uninstaller key
			if (svn == null && isWindows)
			{
				string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client";
				string loc = registry.GetStringValue("HKLM", keyPath, "UninstallString");
				if (loc != null)
				{
					svn = Path.Combine(Path.GetDirectoryName(loc), svnExeName);
					if (!File.Exists(svn))
					{
						svn = null;
					}
					else
					{
						Logger?.Success($@"Found {svnExeName} in ""{loc}"" via HKLM\\{keyPath}\\UninstallString");
					}
				}
			}
			if (svn == null && isWindows)
			{
				string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1";
				string loc = registry.GetStringValue("HKLM", keyPath, "InstallLocation");
				if (loc != null)
				{
					svn = Path.Combine(loc, svnExeName);
					if (!File.Exists(svn))
					{
						svn = null;
					}
					else
					{
						Logger?.Success($@"Found {svnExeName} in ""{loc}"" via HKLM\\{keyPath}\\InstallLocation");
					}
				}
			}

			// Try 64-bit registry keys
			if (svn == null && Is64Bit && isWindows)
			{
				string keyPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\CollabNet Subversion Client";
				string loc = registry.GetStringValue("HKLM", keyPath, "UninstallString");
				if (loc != null)
				{
					svn = Path.Combine(Path.GetDirectoryName(loc), svnExeName);
					if (!File.Exists(svn))
					{
						svn = null;
					}
					else
					{
						Logger?.Success($@"Found {svnExeName} in ""{loc}"" via HKLM\\{keyPath}\\UninstallString");
					}
				}
			}
			if (svn == null && Is64Bit && isWindows)
			{
				string keyPath = @"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{F5980BF8-ED95-4742-A89F-CDAC202D53CF}_is1";
				string loc = registry.GetStringValue("HKLM", keyPath, "InstallLocation");
				if (loc != null)
				{
					svn = Path.Combine(loc, svnExeName);
					if (!File.Exists(svn))
					{
						svn = null;
					}
					else
					{
						Logger?.Success($@"Found {svnExeName} in ""{loc}"" via HKLM\\{keyPath}\\InstallLocation");
					}
				}
			}

			// Search program files directory
			if (svn == null && isWindows)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFiles(), "*subversion*"))
				{
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Logger?.Success($@"Found {svnExeName} in ""{dir}"" via %ProgramFiles%\\*subversion*");
						break;
					}
					svn = Path.Combine(dir, "bin", svnExeName);
					if (File.Exists(svn))
					{
						Logger?.Success($@"Found {svnExeName} in ""{dir}"" via %ProgramFiles%\\*subversion*\\bin");
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
			}

			// Try 32-bit program files directory
			if (svn == null && Is64Bit && isWindows)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFilesX86(), "*subversion*"))
				{
					svn = Path.Combine(dir, svnExeName);
					if (File.Exists(svn))
					{
						Logger?.Success($@"Found {svnExeName} in ""{dir}"" via %ProgramFiles(x86)%\\*subversion*");
						break;
					}
					svn = Path.Combine(dir, "bin", svnExeName);
					if (File.Exists(svn))
					{
						Logger?.Success($@"Found {svnExeName} in ""{dir}"" via %ProgramFiles(x86)%\\*subversion*\\bin");
						break;
					}
				}
				if (!File.Exists(svn)) svn = null;
			}
			return svn;
		}

		private static string ProgramFiles()
		{
			return Environment.GetEnvironmentVariable("ProgramFiles").Replace(" (x86)", "");
		}

		private static string ProgramFilesX86()
		{
			if (Is64Bit)
			{
				return Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			}
			return Environment.GetEnvironmentVariable("ProgramFiles");
		}

		private static bool Is64Bit
		{
			get
			{
				return IntPtr.Size == 8 ||
					!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"));
			}
		}

		#endregion Private helper methods
	}
}
