namespace NetRevisionTask
{
	/// <summary>
	/// An interface that is used for logging internal and error events.
	/// </summary>
	public interface ILogger
	{
		/// <summary>
		/// Logs a raw output line of an executed application.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void RawOutput(string message);

		/// <summary>
		/// Logs a message of Trace (lowest) priority.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void Trace(string message);

		/// <summary>
		/// Logs a message of Success priority.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void Success(string message);

		/// <summary>
		/// Logs a message of Information priority.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void Info(string message);

		/// <summary>
		/// Logs a message of Warning priority.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void Warning(string message);

		/// <summary>
		/// Logs a message of Error priority.
		/// </summary>
		/// <param name="message">The message to log.</param>
		void Error(string message);
	}
}
