using System;

namespace NetRevisionTask
{
	/// <summary>
	/// An implementation of <see cref="ILogger"/> that logs to the console.
	/// </summary>
	internal class ConsoleLogger : ILogger
	{
		public void RawOutput(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkMagenta;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}

		public void Trace(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGray;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}

		public void Success(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}

		public void Info(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Cyan;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}

		public void Warning(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}

		public void Error(string message)
		{
			var color = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.Error.WriteLine(message);
			Console.ForegroundColor = color;
		}
	}
}
