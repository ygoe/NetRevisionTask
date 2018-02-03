using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace NetRevisionTask.VcsProviders
{
	internal class GitProvider : IVcsProvider
	{
		#region Private data

		private string gitExeName;
		private string gitExec;
		private bool isWindows;

		#endregion Private data

		#region Constructors

		public GitProvider()
		{
#if NETFULL
			isWindows = Environment.OSVersion.Platform != PlatformID.Unix;
#else
			isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
#endif
			gitExeName = isWindows ? "git.exe" : "git";
		}

		#endregion Constructors

		#region Properties

		public ILogger Logger { get; set; }

		#endregion Properties

		#region Overridden methods

		public override string ToString() => "Git VCS provider";

		#endregion Overridden methods

		#region IVcsProvider members

		public string Name => "git";

		public bool CheckEnvironment()
		{
			Logger?.Trace("Git environment check...");
			gitExec = FindGitBinary();
			if (gitExec == null)
			{
				Logger?.Warning("  git executable not found.");
				return false;
			}
			return true;
		}

		public bool CheckDirectory(string path, out string rootPath)
		{
			// Scan directory tree upwards for the .git directory
			Logger?.Trace("Checking directory tree for Git working directory...");
			do
			{
				Logger?.Trace($"  Testing: {path}");
				if (Directory.Exists(Path.Combine(path, ".git")))
				{
					Logger?.Success($"  Found {path}");
					rootPath = path;
					return true;
				}
				path = Path.GetDirectoryName(path);
			}
			while (!string.IsNullOrEmpty(path));

			// Nothing found
			Logger?.Trace("Not a Git working directory.");
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

			// Queries the commit hash and time from the latest log entry
			string gitLogFormat = "%H %ci %ai%n%cN%n%cE%n%aN%n%aE";
			Logger?.Trace($@"Executing: git log -n 1 --format=format:""{gitLogFormat}""");
			Logger?.Trace("  WorkingDirectory: " + path);
			var psi = new ProcessStartInfo(gitExec, $@"log -n 1 --format=format:""{gitLogFormat}""")
			{
				WorkingDirectory = path,
				RedirectStandardOutput = true,
				//StandardOutputEncoding = Encoding.Default,   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
				UseShellExecute = false
			};
			var process = Process.Start(psi);
			string line = null;
			int lineCount = 0;
			while (!process.StandardOutput.EndOfStream)
			{
				line = process.StandardOutput.ReadLine();
				lineCount++;
				Logger?.RawOutput(line);
				if (lineCount == 1)
				{
					Match m = Regex.Match(line, @"^([0-9a-fA-F]{40}) ([0-9-]{10} [0-9:]{8} [0-9+-]{5}) ([0-9-]{10} [0-9:]{8} [0-9+-]{5})");
					if (m.Success)
					{
						data.CommitHash = m.Groups[1].Value;
						data.CommitTime = DateTimeOffset.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
						data.AuthorTime = DateTimeOffset.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
					}
				}
				else if (lineCount == 2)
				{
					data.CommitterName = line.Trim();
				}
				else if (lineCount == 3)
				{
					data.CommitterEMail = line.Trim();
				}
				else if (lineCount == 4)
				{
					data.AuthorName = line.Trim();
				}
				else if (lineCount == 5)
				{
					data.AuthorEMail = line.Trim();
				}
			}
			if (!process.WaitForExit(1000))
			{
				process.Kill();
			}

			if (!string.IsNullOrEmpty(data.CommitHash))
			{
				// Query the working directory state
				Logger?.Trace("Executing: git status --porcelain");
				Logger?.Trace($"  WorkingDirectory: {path}");
				psi = new ProcessStartInfo(gitExec, "status --porcelain")
				{
					WorkingDirectory = path,
					RedirectStandardOutput = true,
					//StandardOutputEncoding = Encoding.Default,   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
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

				// Query the current branch
				Logger?.Trace("Executing: git rev-parse --abbrev-ref HEAD");
				Logger?.Trace($"  WorkingDirectory: {path}");
				psi = new ProcessStartInfo(gitExec, "rev-parse --abbrev-ref HEAD")
				{
					WorkingDirectory = path,
					RedirectStandardOutput = true,
					//StandardOutputEncoding = Encoding.Default,   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
					UseShellExecute = false
				};
				process = Process.Start(psi);
				line = null;
				if (!process.StandardOutput.EndOfStream)
				{
					line = process.StandardOutput.ReadLine();
					Logger?.RawOutput(line);
					data.Branch = line.Trim();
				}
				process.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
				if (!process.WaitForExit(1000))
				{
					process.Kill();
				}

				if ((data.Branch == "HEAD" || data.Branch.StartsWith("heads/")) &&
					Environment.GetEnvironmentVariable("CI_SERVER") == "yes")
				{
					// GitLab runner uses detached HEAD so the normal Git command will always return
					// "HEAD" instead of the actual branch name.

					// "HEAD" is reported by default with GitLab CI runner.
					// "heads/*" is reported if an explicit 'git checkout -B' command has been issued.

					// Use GitLab CI provided environment variables instead if the available data is
					// plausible.
					if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME")) &&
						string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_COMMIT_TAG")))
					{
						// GitLab v9
						Logger?.Trace("Reading branch name from CI environment variable: CI_COMMIT_REF_NAME");
						data.Branch = Environment.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
					}
					else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_BUILD_REF_NAME")) &&
						string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI_BUILD_TAG")))
					{
						// GitLab v8
						Logger?.Trace("Reading branch name from CI environment variable: CI_BUILD_REF_NAME");
						data.Branch = Environment.GetEnvironmentVariable("CI_BUILD_REF_NAME");
					}
					else
					{
						Logger?.Trace("No branch name available in CI environment");
						data.Branch = "";
					}
				}

				// Query the most recent matching tag
				string tagMatchOption = "";
				if (!string.IsNullOrWhiteSpace(tagMatch) && tagMatch != "*")
				{
					tagMatchOption = $@" --match ""{tagMatch}""";
				}
				Logger?.Trace("Executing: git describe --tags --first-parent --long" + tagMatchOption);
				Logger?.Trace($"  WorkingDirectory: {path}");
				psi = new ProcessStartInfo(gitExec, "describe --tags --first-parent --long" + tagMatchOption)
				{
					WorkingDirectory = path,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					//StandardOutputEncoding = Encoding.Default,   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
					UseShellExecute = false
				};
				process = Process.Start(psi);
				line = null;
				if (!process.StandardOutput.EndOfStream)
				{
					line = process.StandardOutput.ReadLine();
					Logger?.RawOutput(line);
					line = line.Trim();
					Match m = Regex.Match(line, @"^(.*)-([0-9]+)-g[0-9a-fA-F]+$");
					if (m.Success)
					{
						data.Tag = m.Groups[1].Value.Trim();
						data.CommitsAfterTag = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
					}
				}
				process.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
				if (!process.WaitForExit(1000))
				{
					process.Kill();
				}

				// Query the linear revision number of the current branch (first parent)
				Logger?.Trace("Executing: git rev-list --first-parent --count HEAD");
				Logger?.Trace($"  WorkingDirectory: {path}");
				psi = new ProcessStartInfo(gitExec, "rev-list --first-parent --count HEAD")
				{
					WorkingDirectory = path,
					RedirectStandardOutput = true,
					//StandardOutputEncoding = Encoding.Default,   // TODO: Test if it's necessary (Encoding.Default is not supported in .NET Standard 1.6)
					UseShellExecute = false
				};
				process = Process.Start(psi);
				line = null;
				if (!process.StandardOutput.EndOfStream)
				{
					line = process.StandardOutput.ReadLine();
					Logger?.RawOutput(line);
					if (int.TryParse(line.Trim(), out int revNum))
					{
						data.RevisionNumber = revNum;
					}
					else
					{
						Logger?.Warning("Revision count could not be parsed");
					}
				}
				process.StandardOutput.ReadToEnd();   // Kindly eat up the remaining output
				if (!process.WaitForExit(1000))
				{
					process.Kill();
				}
			}
			return data;
		}

		#endregion IVcsProvider members

		#region Private helper methods

		private string FindGitBinary()
		{
			string git = null;

			// Try the PATH environment variable
			if (git == null)
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
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{dir}"" via %PATH%");
						break;
					}
				}
			}

			var registry = new WindowsRegistry();

			// Read registry uninstaller key
			if (git == null && isWindows)
			{
				string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				string loc = registry.GetStringValue("HKLM", keyPath, "InstallLocation");
				if (loc != null)
				{
					string testPath = Path.Combine(loc, Path.Combine("bin", gitExeName));
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{loc}"" via HKLM\\{keyPath}\\InstallLocation");
					}
				}
			}

			// Try 64-bit registry key
			if (git == null && Is64Bit && isWindows)
			{
				string keyPath = @"Software\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				string loc = registry.GetStringValue("HKLM", keyPath, "InstallLocation");
				if (loc != null)
				{
					string testPath = Path.Combine(loc, Path.Combine("bin", gitExeName));
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{loc}"" via HKLM\\{keyPath}\\InstallLocation");
					}
				}
			}

			// Try user profile key (since Git for Windows 2.x)
			if (git == null && isWindows)
			{
				string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Git_is1";
				string loc = registry.GetStringValue("HKCU", keyPath, "InstallLocation");
				if (loc != null)
				{
					string testPath = Path.Combine(loc, Path.Combine("bin", gitExeName));
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{loc}"" via HKCU\\{keyPath}\\InstallLocation");
					}
				}
			}

			// Search program files directory
			if (git == null && isWindows)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFiles(), "git*"))
				{
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{dir}"" via %ProgramFiles%\\git*");
						break;
					}
					testPath = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{dir}"" via %ProgramFiles%\\git*\\bin");
						break;
					}
				}
			}

			// Try 32-bit program files directory
			if (git == null && Is64Bit && isWindows)
			{
				foreach (string dir in Directory.GetDirectories(ProgramFilesX86(), "git*"))
				{
					string testPath = Path.Combine(dir, gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{dir}"" via %ProgramFiles(x86)%\\git*");
						break;
					}
					testPath = Path.Combine(dir, "bin", gitExeName);
					if (File.Exists(testPath))
					{
						git = testPath;
						Logger?.Success($@"Found {gitExeName} in ""{dir}"" via %ProgramFiles(x86)%\\git*\\bin");
						break;
					}
				}
			}

			// Try Atlassian SourceTree local directory
			if (git == null && isWindows)
			{
				string dir = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Local\Atlassian\SourceTree\git_local\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Logger?.Success($@"Found {gitExeName} in ""{dir}""");
				}
			}

			// Try Tower local directory
			if (git == null && isWindows)
			{
				string dir = Environment.ExpandEnvironmentVariables(ProgramFilesX86() + @"\fournova\Tower\vendor\Git\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Logger?.Success($@"Found {gitExeName} in ""{dir}""");
				}
			}

			// Try SmartGit local directory
			if (git == null && isWindows)
			{
				string dir = Environment.ExpandEnvironmentVariables(ProgramFilesX86() + @"\SmartGit\git\bin");
				string testPath = Path.Combine(dir, gitExeName);
				if (File.Exists(testPath))
				{
					git = testPath;
					Logger?.Success($@"Found {gitExeName} in ""{dir}""");
				}
			}
			return git;
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
