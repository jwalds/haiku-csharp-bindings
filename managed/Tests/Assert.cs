using System;

namespace Haiku.Testing
{
	/// <summary>
	/// Minimal assertion helpers -- deliberately small and dependency-free
	/// (see the top-level README's "Testing" section for why this project
	/// hand-rolls this instead of pulling in a real test framework: nothing
	/// compatible with this exact Mono 6.14.1-on-Haiku build is available
	/// without depending on an incidental leftover file outside this
	/// project). Add more here as tests need them; every one follows the
	/// same one-line-throw shape.
	/// </summary>
	public static class Assert
	{
		public static void IsTrue(bool condition, string message)
		{
			if (!condition)
				throw new AssertionException(message);
		}

		public static void IsFalse(bool condition, string message)
		{
			if (condition)
				throw new AssertionException(message);
		}

		public static void IsNull(object value, string message)
		{
			if (value != null)
				throw new AssertionException(message + " (expected null, was " + Describe(value) + ")");
		}

		public static void IsNotNull(object value, string message)
		{
			if (value == null)
				throw new AssertionException(message + " (expected non-null)");
		}

		/// <summary>
		/// Works for reference types, boxed value types (int, float,
		/// structs with a proper Equals override such as this binding's
		/// own Point/Rect/Size/RgbColor/Alignment -- see Geometry.cs), and
		/// nullable value types alike, since all of those route through
		/// object.Equals once boxed.
		/// </summary>
		public static void AreEqual(object expected, object actual, string message)
		{
			bool equal = expected == null ? actual == null : expected.Equals(actual);
			if (!equal) {
				throw new AssertionException(message + " (expected " + Describe(expected)
					+ ", was " + Describe(actual) + ")");
			}
		}

		public static void Fail(string message)
		{
			throw new AssertionException(message);
		}

		private static string Describe(object value)
		{
			return value == null ? "null" : "\"" + value + "\"";
		}
	}
}
