using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace NetRevisionTask
{
	/// <summary>
	/// An implementation of <see cref="ILogger"/> that logs to MSBuild.
	/// </summary>
	internal class TaskLogger : ILogger
	{
		/// <summary>
		/// Initialises a new instance of the <see cref="TaskLogger"/> class.
		/// </summary>
		/// <param name="log">The MSBuild task logging object.</param>
		public TaskLogger(TaskLoggingHelper log)
		{
			Log = log;
		}

		private TaskLoggingHelper Log { get; }

		public void RawOutput(string message)
		{
			Log.LogMessage(MessageImportance.Low, message);
		}

		public void Trace(string message)
		{
			Log.LogMessage(MessageImportance.Low, message);
		}

		public void Success(string message)
		{
			Log.LogMessage(MessageImportance.Normal, message);
		}

		public void Info(string message)
		{
			Log.LogMessage(MessageImportance.High, message);
		}

		public void Warning(string message)
		{
			Log.LogWarning(message);
		}

		public void Error(string message)
		{
			Log.LogError(message);
		}
	}
}
