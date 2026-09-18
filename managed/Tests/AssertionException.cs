using System;

namespace Haiku.Testing
{
	/// <summary>
	/// Thrown by every Assert.* helper (see Assert.cs) when a check fails.
	/// TestRunner treats this specially: a test that throws THIS is a
	/// FAILED test (something it checked wasn't true). A test that throws
	/// anything else is an ERRORED test (something went wrong that the
	/// test wasn't even checking for -- a null reference, a native crash
	/// surfacing as an exception, and so on). Keeping those two outcomes
	/// visually distinct in the summary is deliberate: they call for
	/// different next steps when you're staring at a red test.
	/// </summary>
	public class AssertionException : Exception
	{
		public AssertionException(string message) : base(message)
		{
		}
	}
}
