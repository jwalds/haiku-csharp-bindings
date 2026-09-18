using System;

namespace Haiku.Testing
{
	/// <summary>
	/// Marks a public, parameterless, no-return instance method as a test
	/// case. TestRunner (see TestRunner.cs) finds every method tagged with
	/// this across every public class in the assembly and runs each one in
	/// its own fresh instance of that class, so one test's state can never
	/// leak into another.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public sealed class TestAttribute : Attribute
	{
	}
}
