using System;

namespace Haiku.Testing
{
	/// <summary>
	/// Optional class-level hint telling TestRunner to run this test class
	/// at a specific point relative to others, ascending, with untagged
	/// classes defaulting to 0. Independent, isolated tests should never
	/// need this -- it exists for the rare case where true isolation isn't
	/// possible because some piece of native state outlives any one test
	/// class and is scoped to the whole process instead. See
	/// ApplicationTests' own [TestOrder] for the concrete reason this
	/// attribute was added, rather than assuming you need it for a new
	/// test class.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class TestOrderAttribute : Attribute
	{
		public int Order { get; }

		public TestOrderAttribute(int order)
		{
			Order = order;
		}
	}
}
