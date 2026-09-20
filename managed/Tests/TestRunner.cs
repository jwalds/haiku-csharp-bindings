using System;
using System.Collections.Generic;
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
	/// Output is grouped by module: each test class gets a header (its
	/// [TestModule("...")] name -- see TestModuleAttribute.cs -- or its
	/// bare class name if untagged), and each test's name is printed
	/// BEFORE it runs, flushed immediately, with the PASS/FAIL/ERROR
	/// result appended once it's known. That ordering matters for a test
	/// like ApplicationTests' -- which blocks on a real native message
	/// loop for a moment -- so a slow or hung test shows up as its own
	/// name sitting on screen without a result, rather than nothing at
	/// all until it finishes.
	///
	/// Pass a substring as the one command-line argument to only run test
	/// classes whose module name or class name contains it, e.g.
	/// `mono Tests.exe BMessage` or `mono Tests.exe Message` (both match
	/// MessageTests). No argument runs everything.
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

				string moduleName = ModuleNameOf(type);
				if (filter != null
						&& type.Name.ToLower().IndexOf(filter) < 0
						&& moduleName.ToLower().IndexOf(filter) < 0)
					continue;

				List<MethodInfo> tests = new List<MethodInfo>();
				foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
					if (Attribute.IsDefined(method, typeof(TestAttribute)))
						tests.Add(method);
				}
				if (tests.Count == 0)
					continue;

				Console.WriteLine();
				Console.WriteLine("== " + moduleName + " ==");

				foreach (MethodInfo method in tests) {
					Console.Write("  " + method.Name + " ... ");
					Console.Out.Flush();

					object instance;
					try {
						instance = Activator.CreateInstance(type);
					} catch (Exception ex) {
						errored++;
						Console.WriteLine("ERROR (could not construct " + type.Name + ": " + Unwrap(ex).Message + ")");
						continue;
					}

					try {
						method.Invoke(instance, null);
						passed++;
						Console.WriteLine("PASS");
					} catch (Exception ex) {
						Exception real = Unwrap(ex);
						if (real is AssertionException) {
							failed++;
							Console.WriteLine("FAIL: " + real.Message);
						} else {
							errored++;
							Console.WriteLine("ERROR: " + real.GetType().Name + ": " + real.Message);
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

		private static string ModuleNameOf(Type type)
		{
			TestModuleAttribute module = (TestModuleAttribute)Attribute.GetCustomAttribute(type, typeof(TestModuleAttribute));
			return module != null ? module.Name : type.Name;
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
