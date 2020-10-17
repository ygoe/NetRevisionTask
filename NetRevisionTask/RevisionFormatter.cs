using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NetRevisionTask
{
	/// <summary>
	/// Resolves a revision format with placeholders to a revision ID from the specified revision data.
	/// </summary>
	internal class RevisionFormatter
	{
		#region Static data

		private static DateTimeOffset buildTime = DateTimeOffset.Now;

		/// <summary>
		/// Alphabet for the base-28 encoding. This uses the digits 0–9 and all characters a–z that
		/// are no vowels and have a low chance of being confused with digits or each other when
		/// hand-written. Omitting vowels avoids generating profane words.
		/// </summary>
		private static readonly char[] base28Alphabet = new char[]
		{
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'b', 'c', 'd', 'f', 'g', 'h', 'j',
			'k', 'm', 'n', 'p', 'q', 'r', 't', 'v', 'w', 'x', 'y'
		};

		/// <summary>
		/// Alphabet for the base-x-encoding up to 36. This uses the digits 0–9 and all characters
		/// a–z as an extension to hexadecimal numbers.
		/// </summary>
		private static readonly char[] base36Alphabet = new char[]
		{
			'0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h',
			'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z'
		};

		#endregion Static data

		#region Data properties

		/// <summary>
		/// Gets or sets the revision data.
		/// </summary>
		public RevisionData RevisionData { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether a leading "v" followed by a digit will be
		/// removed from the tag name.
		/// </summary>
		public bool RemoveTagV { get; set; }

		/// <summary>
		/// Gets or sets the build time.
		/// </summary>
		public DateTimeOffset BuildTime
		{
			get => buildTime;
			set => buildTime = value;
		}

		#endregion Data properties

		#region Format resolving

		/// <summary>
		/// Shortcut for parsing a number from a Regex match.
		/// </summary>
		private static int ParseInt(Match match, int groupIndex = 1) =>
			int.Parse(match.Groups[groupIndex].Value, CultureInfo.InvariantCulture);

		/// <summary>
		/// Resolves placeholders in a revision format string using the current data.
		/// </summary>
		/// <param name="format">The revision format string to resolve.</param>
		/// <returns>The resolved revision string.</returns>
		public string Resolve(string format)
		{
			// Simple data fields
			format = format.Replace("{chash}", RevisionData.CommitHash);
			format = format.Replace("{CHASH}", RevisionData.CommitHash.ToUpperInvariant());
			format = Regex.Replace(format, @"\{chash:([0-9]+)\}", m => SafeSubstring(RevisionData.CommitHash, ParseInt(m)));
			format = Regex.Replace(format, @"\{CHASH:([0-9]+)\}", m => SafeSubstring(RevisionData.CommitHash.ToUpperInvariant(), ParseInt(m)));
			format = format.Replace("{revnum}", RevisionData.RevisionNumber.ToString());
			format = Regex.Replace(format, @"\{revnum\s*-\s*([0-9]+)\}", m => (RevisionData.RevisionNumber - ParseInt(m)).ToString());
			format = Regex.Replace(format, @"\{revnum\s*\+\s*([0-9]+)\}", m => (RevisionData.RevisionNumber + ParseInt(m)).ToString());
			format = format.Replace("{!}", RevisionData.IsModified ? "!" : "");
			format = Regex.Replace(format, @"\{!:(.*?)\}", m => RevisionData.IsModified ? m.Groups[1].Value : "");
			format = format.Replace("{tz}", RevisionData.CommitTime.ToString("%K"));
			format = format.Replace("{url}", RevisionData.RepositoryUrl);
			format = format.Replace("{cname}", RevisionData.CommitterName);
			format = format.Replace("{cmail}", RevisionData.CommitterEMail);
			format = format.Replace("{aname}", RevisionData.AuthorName);
			format = format.Replace("{amail}", RevisionData.AuthorEMail);
			format = format.Replace("{mname}", Environment.MachineName);

			if (!string.IsNullOrEmpty(RevisionData.Branch))
			{
				format = format.Replace("{branch}", RevisionData.Branch);
				format = Regex.Replace(format, @"\{branch:(.*?):(.+?)\}", m => RevisionData.Branch != m.Groups[2].Value ? m.Groups[1].Value + RevisionData.Branch : "");
			}
			else
			{
				// No branch name available, leave it away completely
				format = format.Replace("{branch}", "");
				format = Regex.Replace(format, @"\{branch:(.*?):(.+?)\}", "");
			}

			string tagName = RevisionData.Tag;
			if (RemoveTagV && Regex.IsMatch(tagName, "^v[0-9]"))
			{
				tagName = tagName.Substring(1);
			}
			format = format.Replace("{semvertag}", GetSemVerTagSpec(tagName));
			format = format.Replace("{semvertag+chash}", GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash, 7)));
			format = Regex.Replace(format, @"\{semvertag\+chash:([0-9]+)\}", m => GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash, ParseInt(m))));
			format = Regex.Replace(format, @"\{semvertag\+CHASH:([0-9]+)\}", m => GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash.ToUpperInvariant(), ParseInt(m))));
			format = Regex.Replace(format, @"\{semvertag:(.+?):\+chash:([0-9]+)\}", m => GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash, ParseInt(m, 2)), m.Groups[1].Value));
			format = Regex.Replace(format, @"\{semvertag:(.+?):\+CHASH:([0-9]+)\}", m => GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash.ToUpperInvariant(), ParseInt(m, 2)), m.Groups[1].Value));
			format = Regex.Replace(format, @"\{semvertag:(.+?):\+chash\}", m => GetSemVerTagSpec(tagName, SafeSubstring(RevisionData.CommitHash, 7), m.Groups[1].Value));
			format = Regex.Replace(format, @"\{semvertag:(.+?)\}", m => GetSemVerTagSpec(tagName, "", m.Groups[1].Value));

			format = format.Replace("{tag}", GetTagSpec(tagName));
			format = format.Replace("{tagname}", tagName);
			format = format.Replace("{tagadd}", RevisionData.CommitsAfterTag.ToString());
			format = Regex.Replace(format, @"\{tagadd:(.*?)\}", m => RevisionData.CommitsAfterTag > 0 ? m.Groups[1].Value + RevisionData.CommitsAfterTag : "");

			// Resolve time schemes
			format = Regex.Replace(format, @"\{[AaBbCc]:.+?\}", FormatTimeScheme);

			// Partial legacy format compatibility
			string commitString = RevisionData.CommitHash;
			if (string.IsNullOrEmpty(commitString))
			{
				commitString = RevisionData.RevisionNumber.ToString();
			}
			format = format.Replace("{commit}", commitString);
			format = Regex.Replace(format, @"\{commit:([0-9]+)\}", m => SafeSubstring(RevisionData.CommitHash, ParseInt(m)));
			format = Regex.Replace(format, @"\{(?:(?:[Xx]|[Bb](?:36)?|d2?)min):.+?\}", FormatTimeScheme);

			// Copyright year
			string copyright = BuildTime.Year.ToString();
			if (RevisionData.CommitTime.Year > 1)
			{
				copyright = RevisionData.CommitTime.Year.ToString();
			}
			format = format.Replace("{copyright}", copyright);
			format = Regex.Replace(format, @"\{copyright:([0-9]+?)-?\}", m => (m.Groups[1].Value != copyright ? m.Groups[1].Value + "–" : "") + copyright);

			// Return revision ID
			return format;
		}

		/// <summary>
		/// Resolves placeholders in a revision format string using the current data and returns a
		/// short dotted-numeric version number.
		/// </summary>
		/// <param name="format">The revision format string to resolve.</param>
		/// <returns>The resolved revision string.</returns>
		public string ResolveShort(string format)
		{
			string revisionId = Resolve(format);
			string truncVersion = Regex.Replace(revisionId, @"[^0-9.].*$", "").TrimEnd('.');
			if (!truncVersion.Contains("."))
				throw new FormatException("Revision ID cannot be truncated to dotted-numeric: " + revisionId);
			if (!Version.TryParse(truncVersion, out _))
				throw new FormatException("Revision ID cannot be truncated to dotted-numeric: " + revisionId);
			return truncVersion;
		}

		private static string SafeSubstring(string source, int length)
		{
			if (length >= source.Length)
			{
				return source;
			}
			return source.Substring(0, length);
		}

		private string GetSemVerTagSpec(string tagName, string preCommitHash = "", string defaultBranchName = "")
		{
			// If the current revision is tagged directly, return the tag name as-is
			if (RevisionData.CommitsAfterTag == 0 && !string.IsNullOrEmpty(tagName))
			{
				return tagName;
			}

			// The current revision is not tagged directly, make a pre-release version
			int count = RevisionData.CommitsAfterTag;
			string tagSuffix = "";
			var match = Regex.Match(tagName ?? "", @"^([0-9]+(?:\.[0-9]+(?:\.[0-9]+)?)?)(?:-(.+))?$");
			if (match.Success)
			{
				tagName = match.Groups[1].Value;
				tagSuffix = match.Groups[2].Value;
			}
			else
			{
				// The provided tag name is not SemVer-compliant and cannot be processed.
				// This is just handled as if no tag was set at all (which may be the case).
				tagName = "";
				count = RevisionData.RevisionNumber;
			}

			// Make sure we have three numbers in the tag name
			if (string.IsNullOrEmpty(tagName))
			{
				tagName = "0";
			}
			while (tagName.Split('.').Length < 3)
			{
				tagName += ".0";
			}

			string[] values = tagName.Split('.');
			int majorValue = int.Parse(values[0]);
			int minorValue = int.Parse(values[1]);
			int patchValue = int.Parse(values[2]);
			string preRelease = count.ToString();
			if (!string.IsNullOrEmpty(RevisionData.Branch) &&
				RevisionData.Branch != defaultBranchName)
			{
				preRelease = Regex.Replace(RevisionData.Branch, "[^0-9A-Za-z-]", "-") + "." + preRelease;
				// Example result: 1.0.1-master.4
				if (!string.IsNullOrEmpty(tagSuffix))
				{
					preRelease = tagSuffix + "-" + preRelease;
					// Example result: 1.0.1-beta-master.4
				}
			}
			else
			{
				// Example result: 1.0.1-4
				if (!string.IsNullOrEmpty(tagSuffix))
				{
					preRelease = tagSuffix + "." + preRelease;
					// Example result: 1.0.1-beta.4
				}
			}
			if (!string.IsNullOrEmpty(preCommitHash))
			{
				// The commit hash is always added to the end, no matter what we have until now
				preRelease += "+" + preCommitHash;
			}

			// Increment the patch value and append the branch name (if available) and commits after
			// the tag as pre-release suffix
			return majorValue + "." + minorValue + "." + (patchValue + 1) + "-" + preRelease;
		}

		private string GetTagSpec(string tagName)
		{
			if (RevisionData.CommitsAfterTag == 0)
			{
				return tagName;
			}

			// Only add the number of commits past the tag in the fourth number of the version, if
			// the tag doesn't already contain so many parts, otherwise append it with a plus.
			while (tagName.Split('.').Length < 3)
			{
				tagName += ".0";
			}
			if (tagName.Split('.').Length == 3)
			{
				return tagName + "." + RevisionData.CommitsAfterTag;
			}
			else
			{
				return tagName + "+" + RevisionData.CommitsAfterTag;
			}
		}

		/// <summary>
		/// Formats the current data for the specified time scheme.
		/// </summary>
		/// <param name="match"></param>
		/// <returns></returns>
		private string FormatTimeScheme(Match match)
		{
			string scheme = match.Value;
			SchemeData data = ParseTimeScheme(scheme);

			DateTimeOffset time;
			if (data.TimeSource == TimeSource.Build)
			{
				time = BuildTime;
			}
			else if (data.TimeSource == TimeSource.Author)
			{
				time = RevisionData.AuthorTime;
			}
			else
			{
				time = RevisionData.CommitTime;
			}

			switch (data.SchemeType)
			{
				case SchemeType.Readable:
					if (data.Utc)
					{
						time = time.UtcDateTime;
					}
					return time.ToString(data.TimeFormat, CultureInfo.InvariantCulture);

				case SchemeType.DottedDecimal:
					return EncodeDecimal(time, data.IntervalSeconds, data.BaseYear);

				case SchemeType.BaseEncoded:
					return EncodeBase(time, data.Alphabet, data.IntervalSeconds, data.BaseYear, data.MinLength, data.UpperCase);

				case SchemeType.Hours:
					return EncodeHours(time, data.BaseYear, data.BaseMonth);
			}

			// Return match unprocessed (should not happen)
			return scheme;
		}

		#endregion Format resolving

		#region Public time-based decoding

		/// <summary>
		/// Decodes a version value and writes the time to the console.
		/// </summary>
		/// <param name="scheme">The version scheme specification of the version value.</param>
		/// <param name="value">The version value to decode.</param>
		public static void ShowDecode(string scheme, string value)
		{
			SchemeData data = ParseTimeScheme(scheme);

			DateTime time = DateTime.MinValue;
			switch (data.SchemeType)
			{
				case SchemeType.Readable:
					time = DateTime.ParseExact(value, data.TimeFormat, CultureInfo.InvariantCulture).ToUniversalTime();
					break;
				case SchemeType.DottedDecimal:
					time = DecodeDecimal(value, data.IntervalSeconds, data.BaseYear);
					break;
				case SchemeType.BaseEncoded:
					time = DecodeBase(value, data.Alphabet, data.IntervalSeconds, data.BaseYear);
					break;
				case SchemeType.Hours:
					time = DecodeHours(value, data.BaseYear, data.BaseMonth);
					break;
			}
			if (time == DateTime.MinValue)
			{
				throw new FormatException("Invalid revision ID value: " + value);
			}

			Console.WriteLine(time.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
			Console.WriteLine(time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss K"));
		}

		#endregion Public time-based decoding

		#region Times-based scheme parsing

		/// <summary>
		/// Parses a time-based version scheme specification.
		/// </summary>
		/// <param name="scheme">The version scheme specification to parse.</param>
		/// <returns>The parsed scheme data.</returns>
		private static SchemeData ParseTimeScheme(string scheme)
		{
			var data = new SchemeData();

			Match match;
			string sourceStr = null;
			string timeComponents = null;
			string timeSeparator = null;
			int numberBase = 0;
			int intervalValue = 0;
			string intervalType = null;

			// Split scheme string
			if ((match = Regex.Match(scheme, @"^\{?([abc]):(u?)(ymd|hms|hm|h)([-.:]?)\}?$")).Success)
			{
				data.SchemeType = SchemeType.Readable;
				sourceStr = match.Groups[1].Value;
				data.Utc = match.Groups[2].Value == "u";
				timeComponents = match.Groups[3].Value;
				timeSeparator = match.Groups[4].Value;
			}
			else if ((match = Regex.Match(scheme, @"^\{?([abc]):([0-9]+)([smhd]):([0-9]+)\}?$")).Success)
			{
				data.SchemeType = SchemeType.DottedDecimal;
				sourceStr = match.Groups[1].Value;
				intervalValue = int.Parse(match.Groups[2].Value);
				intervalType = match.Groups[3].Value;
				data.BaseYear = int.Parse(match.Groups[4].Value);
			}
			else if ((match = Regex.Match(scheme, @"^\{?([AaBbCc]):([0-9]+):([0-9]+)([smhd]):([0-9]+)(?::([0-9]+))?\}?$")).Success)
			{
				data.SchemeType = SchemeType.BaseEncoded;
				sourceStr = match.Groups[1].Value.ToLowerInvariant();
				data.UpperCase = char.IsUpper(match.Groups[1].Value[0]);
				numberBase = int.Parse(match.Groups[2].Value);
				intervalValue = int.Parse(match.Groups[3].Value);
				intervalType = match.Groups[4].Value;
				data.BaseYear = int.Parse(match.Groups[5].Value);
				if (match.Groups[6].Success)
				{
					data.MinLength = int.Parse(match.Groups[6].Value);
				}
			}
			else if ((match = Regex.Match(scheme, @"^\{?([abc]):h:([0-9]{4})-([0-9]{2})\}?$")).Success)
			{
				data.SchemeType = SchemeType.Hours;
				sourceStr = match.Groups[1].Value;
				data.BaseYear = int.Parse(match.Groups[2].Value);
				data.BaseMonth = int.Parse(match.Groups[3].Value);
				data.IntervalSeconds = 3600;
			}

			#region Legacy formats

			else if ((match = Regex.Match(scheme, @"^\{?([Xx])min:([0-9]+)(?::([0-9]+))?\}?$")).Success)
			{
				data.SchemeType = SchemeType.BaseEncoded;
				sourceStr = "c";
				data.UpperCase = match.Groups[1].Value == "X";
				numberBase = 16;
				intervalValue = 1;
				intervalType = "m";
				data.BaseYear = int.Parse(match.Groups[2].Value);
				if (match.Groups[3].Success)
				{
					data.MinLength = int.Parse(match.Groups[3].Value);
				}
			}
			else if ((match = Regex.Match(scheme, @"^\{?([Bb])(36)?min:([0-9]+)(?::([0-9]+))?\}?$")).Success)
			{
				data.SchemeType = SchemeType.BaseEncoded;
				sourceStr = "c";
				data.UpperCase = match.Groups[1].Value == "B";
				numberBase = match.Groups[2].Success ? 36 : 28;
				intervalValue = match.Groups[2].Success ? 10 : 20;
				intervalType = "m";
				data.BaseYear = int.Parse(match.Groups[3].Value);
				if (match.Groups[4].Success)
				{
					data.MinLength = int.Parse(match.Groups[4].Value);
				}
			}
			else if ((match = Regex.Match(scheme, @"^\{?d(2)?min:([0-9]+)\}?$")).Success)
			{
				data.SchemeType = SchemeType.DottedDecimal;
				sourceStr = "c";
				intervalValue = match.Groups[1].Success ? 2 : 15;
				intervalType = "m";
				data.BaseYear = int.Parse(match.Groups[2].Value);
			}

			#endregion Legacy formats

			else
			{
				throw new FormatException("Invalid time scheme: " + scheme);
			}

			// Select time source
			switch (sourceStr)
			{
				case "a": data.TimeSource = TimeSource.Author; break;
				case "b": data.TimeSource = TimeSource.Build; break;
				case "c": data.TimeSource = TimeSource.Commit; break;
				default:
					throw new FormatException("Invalid time source specification: " + sourceStr);
			}

			if (data.SchemeType == SchemeType.Readable)
			{
				// Select time format from components and separator
				switch (timeComponents + timeSeparator)
				{
					case "ymd":
						data.TimeFormat = "yyyyMMdd";
						break;
					case "ymd-":
						data.TimeFormat = "yyyy-MM-dd";
						break;
					case "ymd.":
						data.TimeFormat = "yyyy.MM.dd";
						break;
					case "hms":
						data.TimeFormat = "HHmmss";
						break;
					case "hms-":
						data.TimeFormat = "HH-mm-ss";
						break;
					case "hms.":
						data.TimeFormat = "HH.mm.ss";
						break;
					case "hms:":
						data.TimeFormat = "HH:mm:ss";
						break;
					case "hm":
						data.TimeFormat = "HHmm";
						break;
					case "hm-":
						data.TimeFormat = "HH-mm";
						break;
					case "hm.":
						data.TimeFormat = "HH.mm";
						break;
					case "hm:":
						data.TimeFormat = "HH:mm";
						break;
					case "h":
						data.TimeFormat = "HH";
						break;
					default:
						throw new FormatException("Invalid time components and separator: " + timeComponents + timeSeparator);
				}
			}

			if (data.SchemeType == SchemeType.DottedDecimal ||
				data.SchemeType == SchemeType.BaseEncoded)
			{
				// Convert interval specification to seconds
				data.IntervalSeconds = intervalValue;
				switch (intervalType)
				{
					case "s": break;
					case "m": data.IntervalSeconds *= 60; break;
					case "h": data.IntervalSeconds *= 60 * 60; break;
					case "d": data.IntervalSeconds *= 24 * 60 * 60; break;
				}
			}

			if (data.SchemeType == SchemeType.BaseEncoded)
			{
				// Select alphabet for number base
				if (numberBase < 2 || numberBase > 36)
				{
					throw new FormatException("Invalid number base: " + numberBase);
				}
				else if (numberBase == 28)
				{
					data.Alphabet = base28Alphabet;
				}
				else
				{
					data.Alphabet = new char[numberBase];
					Array.Copy(base36Alphabet, data.Alphabet, numberBase);
				}
			}
			return data;
		}

		#endregion Times-based scheme parsing

		#region Time-based dotted-decimal coding

		/// <summary>
		/// Encodes a time using the dotted-decimal scheme.
		/// </summary>
		/// <param name="time">The time to encode.</param>
		/// <param name="intervalSeconds">The interval time in seconds.</param>
		/// <param name="baseYear">The base year.</param>
		/// <returns>The encoded time string.</returns>
		private static string EncodeDecimal(DateTimeOffset time, int intervalSeconds, int baseYear)
		{
			long ticks = time.UtcDateTime.Ticks - new DateTime(baseYear, 1, 1).Ticks;
			bool negative = false;
			if (ticks < 0)
			{
				negative = true;
				ticks = -ticks;
			}

			long dayTicks = TimeSpan.FromDays(1).Ticks;
			int days = (int)(ticks / dayTicks);
			long ticks2 = ticks % dayTicks;
			int intervalCount = (int)TimeSpan.FromTicks(ticks2).TotalSeconds / intervalSeconds;

			return (negative ? "-" : "") + days + "." + intervalCount;
		}

		/// <summary>
		/// Decodes a dotted-decimal-encoded time string.
		/// </summary>
		/// <param name="value">The dotted-decimal-encoded time string to decode.</param>
		/// <param name="intervalSeconds">The interval time in seconds.</param>
		/// <param name="baseYear">The base year.</param>
		/// <returns>The decoded time value in UTC.</returns>
		private static DateTime DecodeDecimal(string value, int intervalSeconds, int baseYear)
		{
			// Split the two numbers
			string[] parts = value.Split('.');
			if (parts.Length != 2)
				return DateTime.MinValue;

			// Parse and check the two numbers
			if (!int.TryParse(parts[0], out int days))
				return DateTime.MinValue;
			if (!int.TryParse(parts[1], out int intervalCount))
				return DateTime.MinValue;
			if (days < 0 || days >= ushort.MaxValue)
				return DateTime.MinValue;
			int maxIntervalCount = (int)TimeSpan.FromDays(1).TotalSeconds / intervalSeconds;
			if (intervalCount < 0 || intervalCount >= maxIntervalCount)
				return DateTime.MinValue;

			// Build the date and time
			try
			{
				return new DateTime(baseYear, 1, 1).AddDays(days).AddSeconds(intervalCount * intervalSeconds);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		#endregion Time-based dotted-decimal coding

		#region Time-based base-x coding

		/// <summary>
		/// Encodes a time using base-x-encoding.
		/// </summary>
		/// <param name="time">The time to encode.</param>
		/// <param name="alphabet">The alphabet to use for the base-x-encoding</param>
		/// <param name="intervalSeconds">The interval time in seconds.</param>
		/// <param name="baseYear">The base year.</param>
		/// <param name="minLength">The minimum length of the returned value.
		///   The returned value is left-padded with the first character of the alphabet.</param>
		/// <param name="upperCase">Indicates whether the returned string contains upper-case characters.</param>
		/// <returns>The encoded time string.</returns>
		private static string EncodeBase(DateTimeOffset time, char[] alphabet, int intervalSeconds, int baseYear, int minLength = 1, bool upperCase = false)
		{
			int intervalCount = (int)((time.UtcDateTime - new DateTime(baseYear, 1, 1)).TotalSeconds / intervalSeconds);
			bool negative = false;
			if (intervalCount < 0)
			{
				negative = true;
				intervalCount = -intervalCount;
			}
			string s = "";
			while (intervalCount > 0)
			{
				int digit = intervalCount % alphabet.Length;
				intervalCount = intervalCount / alphabet.Length;
				s = alphabet[digit] + s;
			}
			if (upperCase)
			{
				s = s.ToUpperInvariant();
			}
			return (negative ? "-" : "") + s.PadLeft(minLength, alphabet[0]);
		}

		/// <summary>
		/// Decodes a base-x-encoded time string.
		/// </summary>
		/// <param name="value">The base-x-encoded time string to decode.</param>
		/// <param name="alphabet">The alphabet to use for the base-x-encoding</param>
		/// <param name="intervalSeconds">The interval time in seconds.</param>
		/// <param name="baseYear">The base year.</param>
		/// <returns>The decoded time value in UTC.</returns>
		private static DateTime DecodeBase(string value, char[] alphabet, int intervalSeconds, int baseYear)
		{
			bool negative = false;
			value = value.Trim().ToLowerInvariant();
			if (value.StartsWith("-"))
			{
				negative = true;
				value = value.Substring(1);
			}
			int count = 0;
			while (value.Length > 0)
			{
				int digit = Array.IndexOf(alphabet, value[0]);
				if (digit == -1)
				{
					return DateTime.MinValue;
				}
				count = count * alphabet.Length + digit;
				value = value.Substring(1);
			}
			try
			{
				return new DateTime(baseYear, 1, 1).AddSeconds((negative ? -count : count) * intervalSeconds);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		#endregion Time-based base-x coding

		#region Time-based hours coding

		/// <summary>
		/// Encodes a time using hours encoding.
		/// </summary>
		/// <param name="time">The time to encode.</param>
		/// <param name="baseYear">The base year.</param>
		/// <param name="baseMonth">The base month.</param>
		/// <returns>The encoded time string.</returns>
		private static string EncodeHours(DateTimeOffset time, int baseYear, int baseMonth)
		{
			int intervalCount = (int)((time.UtcDateTime - new DateTime(baseYear, baseMonth, 1)).TotalHours);
			return intervalCount.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Decodes an hours-encoded time string.
		/// </summary>
		/// <param name="value">The hours-encoded time string to decode.</param>
		/// <param name="baseYear">The base year.</param>
		/// <param name="baseMonth">The base month.</param>
		/// <returns>The decoded time value in UTC.</returns>
		private static DateTime DecodeHours(string value, int baseYear, int baseMonth)
		{
			int intervalCount = int.Parse(value, CultureInfo.InvariantCulture);
			if (intervalCount < 0 || intervalCount >= ushort.MaxValue)
				return DateTime.MinValue;
			try
			{
				return new DateTime(baseYear, baseMonth, 1).AddHours(intervalCount);
			}
			catch
			{
				return DateTime.MinValue;
			}
		}

		#endregion Time-based hours coding
	}

	#region Scheme data classes and enums

	internal class SchemeData
	{
		/// <summary>
		/// The scheme type.
		/// </summary>
		public SchemeType SchemeType;

		/// <summary>
		/// The time source.
		/// </summary>
		public TimeSource TimeSource;

		/// <summary>
		/// Indicates whether the time source is UTC.
		/// </summary>
		public bool Utc;

		/// <summary>
		/// The .NET time format string. (Only for <see cref="SchemeType.Readable"/> type.)
		/// </summary>
		public string TimeFormat;

		/// <summary>
		/// The alphabet used for the number base. (Only for <see cref="SchemeType.BaseEncoded"/> type.)
		/// </summary>
		public char[] Alphabet;

		/// <summary>
		/// Indicates whether the characters are converter to upper case. (Only for <see cref="SchemeType.BaseEncoded"/> type.)
		/// </summary>
		public bool UpperCase;

		/// <summary>
		/// The interval time in seconds. (Not for <see cref="SchemeType.Readable"/> type.)
		/// </summary>
		public int IntervalSeconds;

		/// <summary>
		/// The base year. (Not for <see cref="SchemeType.Readable"/> type.)
		/// </summary>
		public int BaseYear;

		/// <summary>
		/// The base month. (Only for <see cref="SchemeType.Hours"/> type.)
		/// </summary>
		public int BaseMonth;

		/// <summary>
		/// The minimum length of the encoded value. (Only for <see cref="SchemeType.BaseEncoded"/> type.)
		/// </summary>
		public int MinLength;
	}

	internal enum TimeSource
	{
		/// <summary>
		/// The time of the current build, i.e. the .NET Revision Tool startup time.
		/// </summary>
		Build,
		/// <summary>
		/// The time when the currently checked-out revision was committed to the repository.
		/// </summary>
		Commit,
		/// <summary>
		/// The time when the currently checked-out revision was originally authored.
		/// </summary>
		Author
	}

	internal enum SchemeType
	{
		/// <summary>
		/// A human-readable date or time like "2015-03-31".
		/// </summary>
		Readable,
		/// <summary>
		/// A dotted-decimal version number like "23.45".
		/// </summary>
		DottedDecimal,
		/// <summary>
		/// A base-x-encoded comparable string like "17yn".
		/// </summary>
		BaseEncoded,
		/// <summary>
		/// A decimal number (UInt16) of hours since a base year and month.
		/// </summary>
		Hours
	}

	#endregion Scheme data classes and enums
}
