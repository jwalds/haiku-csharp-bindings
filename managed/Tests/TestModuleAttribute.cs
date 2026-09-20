using System;

namespace Haiku.Testing
{
	/// <summary>
	/// Labels a test class with the human-readable module/kit name it
	/// covers (e.g. "BMessage", "Application Kit") -- TestRunner prints
	/// this as a section header instead of the raw C# class name, and
	/// matches it against the runner's optional command-line filter
	/// alongside the class name itself. A class with no [TestModule]
	/// falls back to its own class name for both purposes.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class TestModuleAttribute : Attribute
	{
		public string Name { get; }

		public TestModuleAttribute(string name)
		{
			Name = name;
		}
	}
}
