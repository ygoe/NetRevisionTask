using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace NetRevisionTask
{
	internal class WindowsRegistry
	{
		/// <summary>
		/// Reads a Windows registry value. This method does nothing if not running on Windows.
		/// </summary>
		/// <param name="root">The registry hive: "HKLM" or "HKCU".</param>
		/// <param name="key">The path of the key to read the value from.</param>
		/// <param name="value">The value to read.</param>
		/// <returns>The read value if it exists and is a string; otherwise, null.</returns>
		public string GetStringValue(string root, string key, string value)
		{
#if NETFULL
			RegistryKey rootKey;
			switch (root)
			{
				case "HKLM":
					rootKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
					break;
				case "HKCU":
					rootKey = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
					break;
				default:
					throw new InvalidOperationException($"Registry root key {root} unknown.");
			}
			var regKey = rootKey.OpenSubKey(key);
			if (regKey != null)
			{
				object obj = regKey.GetValue(value);
				regKey.Close();
				if (obj is string)
				{
					return (string)obj;
				}
			}
#else
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				UIntPtr rootKey;
				switch (root)
				{
					case "HKLM":
						rootKey = HKEY_LOCAL_MACHINE;
						break;
					case "HKCU":
						rootKey = HKEY_CURRENT_USER;
						break;
					default:
						throw new InvalidOperationException($"Registry root key {root} unknown.");
				}
				if (RegOpenKeyEx(rootKey, key, 0, KEY_READ | KEY_WOW64_64KEY, out UIntPtr keyHandle) == 0)
				{
					var sb = new StringBuilder(1000);
					int length = sb.Capacity;
					bool found = RegQueryValueEx(keyHandle, value, 0, out RegistryValueKind kind, sb, ref length) == 0;
					RegCloseKey(keyHandle);
					if (found)
						return sb.ToString();
				}
			}
#endif
			return null;
		}

		#region Registry interop

#if !NETFULL

		[DllImport("advapi32.dll")]
		private static extern int RegOpenKeyEx(
			UIntPtr hKey,
			string subKey,
			int ulOptions,
			int samDesired,
			out UIntPtr hkResult);

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern int RegCloseKey(
			UIntPtr hKey);

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern uint RegQueryValueEx(
			UIntPtr hKey,
			string lpValueName,
			int lpReserved,
			out RegistryValueKind lpType,
			IntPtr lpData,
			ref int lpcbData);

		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern uint RegQueryValueEx(
			UIntPtr hKey,
			string lpValueName,
			int lpReserved,
			out RegistryValueKind lpType,
			StringBuilder lpData,
			ref int lpcbData);

		private static UIntPtr HKEY_LOCAL_MACHINE = new UIntPtr(0x80000002u);
		private static UIntPtr HKEY_CURRENT_USER = new UIntPtr(0x80000001u);

		private const int KEY_READ = 0x20019;
		private const int KEY_WOW64_64KEY = 0x0100;

		private enum RegistryValueKind
		{
			None = 0,
			String = 1,
			ExpandString = 2,
			Binary = 3,
			DWord = 4,
			DWordBE = 5,
			Link = 6,
			MultiString = 7
		}

#endif

		#endregion Registry interop
	}
}
