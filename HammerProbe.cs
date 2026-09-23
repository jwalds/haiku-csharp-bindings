// HammerProbe.cs -- standalone stress-test tool for KNOWN_ISSUES.md #3/#5
// (Mono thread-attach/detach around a BWindow's message-loop thread).
// Not part of the normal build (build.sh doesn't compile it); compile and
// run it manually when touching hs_mono_thread_attach.h/hs_mono_thread_
// detach.h or either destructor that uses them:
//
//   mcs -r:Haiku.App.dll -r:Haiku.Interface.dll -out:HammerProbe.exe HammerProbe.cs
//   LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH" \
//     mono HammerProbe.exe 50
//
// Repeatedly creates, shows, and quits plain (non-drawing) windows in a
// tight loop, well beyond anything Tests.exe exercises, and reports any
// OnDestroyed timeout (a hang) immediately with a nonzero exit code. Kept
// around because the destructor-thread-detach investigation needed high
// iteration counts to build confidence beyond Tests.exe's single pass
// through each code path -- see KNOWN_ISSUES.md #3's fix-attempt-4 writeup.

using System;
using System.Threading;
using Haiku.App;
using Haiku.Interface;

public class HammerProbe
{
	private class ProbeWindow : Window
	{
		public volatile bool DestroyedFired;

		public ProbeWindow(int n)
			: base(new Rect(50, 50, 250, 150), "HammerProbe " + n)
		{
		}

		protected override void OnDestroyed()
		{
			DestroyedFired = true;
		}
	}

	public static void Main(string[] args)
	{
		int iterations = 30;
		if (args.Length > 0)
			iterations = int.Parse(args[0]);

		Console.WriteLine("HammerProbe starting: " + iterations + " show/quit cycles");

		using (new Application("application/x-vnd.HaikuSharp-HammerProbe")) {
			for (int i = 0; i < iterations; i++) {
				ProbeWindow window = new ProbeWindow(i);
				window.Show();
				window.Quit();

				const int timeoutMs = 3000;
				const int pollIntervalMs = 5;
				int waited = 0;
				while (!window.DestroyedFired && waited < timeoutMs) {
					Thread.Sleep(pollIntervalMs);
					waited += pollIntervalMs;
				}

				if (!window.DestroyedFired) {
					Console.WriteLine("[" + i + "] TIMEOUT waiting for OnDestroyed -- stopping");
					Environment.Exit(2);
				}

				Console.WriteLine("[" + i + "] destroyed OK");
			}
		}

		Console.WriteLine("HammerProbe: ALL " + iterations + " CYCLES DESTROYED CLEANLY");
	}
}
