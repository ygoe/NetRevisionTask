using System;
using System.IO;

namespace Unclassified.Util
{
	public class PathUtil
	{
		/// <summary>
		/// Determines the exact path on the file system with correct casing.
		/// </summary>
		/// <param name="path">The path to check.</param>
		/// <returns>The actual path if it exists, otherwise <paramref name="path"/>.</returns>
		public static string GetExactPath(string path)
		{
			// Source: http://stackoverflow.com/a/326153
			if (!(File.Exists(path) || Directory.Exists(path)))
				return path;

			var di = new DirectoryInfo(path);
			if (di.Parent != null)
			{
				return Path.Combine(
					GetExactPath(di.Parent.FullName),
					di.Parent.GetFileSystemInfos(di.Name)[0].Name);
			}
			else
			{
				return di.FullName.ToUpper();
			}
		}
	}
}
