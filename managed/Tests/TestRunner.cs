using System;
using System.Reflection;

namespace Haiku.Testing
{
	/// <summary>
	/// Finds every public method tagged [Test] on every public class in
	/// this assembly, runs each one in a fresh instance of its class (so
	/// no test can see state another test left behind), and prints a
	/// pass/fail/error summary. Exits 0 only if every test passed, and
	/// nonzero otherwise -- so a script (or a habit of running this after
	/// any change) can rely on the exit code alone without parsing output.
	///
	/// Pass a substring as the one command-line argument to only run test
	/// classes whose name contains it, e.g. `mono Tests.exe Message` to
	/// run just MessageTests. No argument runs everything.
	/// </summary>
	public static class TestRunner
	{
		public static int Main(string[] args)
		{
			string filter = args.Length > 0 ? args[0].ToLower() : null;

			int passed = 0;
			int failed = 0;
			int errored = 0;

			foreach (Type type in Assembly.GetExecutingAssembly().GetTypes()) {
				if (!type.IsClass || !type.IsPublic)
					continue;
				if (filter != null && type.Name.ToLower().IndexOf(filter) < 0)
					continue;

				foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
					if (!Attribute.IsDefined(method, typeof(TestAttribute)))
						continue;

					string testName = type.Name + "." + method.Name;
					object instance;
					try {
						instance = Activator.CreateInstance(type);
					} catch (Exception ex) {
						errored++;
						Console.WriteLine("ERROR  " + testName + " (could not construct "
							+ type.Name + ": " + Unwrap(ex).Message + ")");
						continue;
					}

					try {
						method.Invoke(instance, null);
						passed++;
						Console.WriteLine("PASS   " + testName);
					} catch (Exception ex) {
						Exception real = Unwrap(ex);
						if (real is AssertionException) {
							failed++;
							Console.WriteLine("FAIL   " + testName + ": " + real.Message);
						} else {
							errored++;
							Console.WriteLine("ERROR  " + testName + ": " + real.GetType().Name + ": " + real.Message);
						}
					} finally {
						IDisposable disposable = instance as IDisposable;
						if (disposable != null)
							disposable.Dispose();
					}
				}
			}

			Console.WriteLine();
			Console.WriteLine(passed + " passed, " + failed + " failed, " + errored + " errored");

			return (failed == 0 && errored == 0) ? 0 : 1;
		}

		// Reflection's MethodInfo.Invoke wraps whatever a test method
		// throws in a TargetInvocationException -- unwrap it so PASS/FAIL
		// output shows the real exception, not a layer of reflection
		// plumbing around it.
		private static Exception Unwrap(Exception ex)
		{
			return ex.InnerException != null ? ex.InnerException : ex;
		}
	}
}
