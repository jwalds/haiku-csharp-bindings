using Haiku.App;
using Haiku.Testing;

/// <summary>
/// Regression coverage for Haiku.App.Application/BLooper's threading model
/// (see managed/Haiku.App/Application.cs and native/include/hs_application.h) --
/// this used to be Sample/Program.cs's whole reason for existing, proven by
/// eye via four numbered Console.WriteLine calls. It's an integration test,
/// not a fast isolated unit test (it spins up a real BApplication and blocks
/// on a real native message loop), but it belongs here rather than nowhere:
/// this is the one test that would catch it if Mono's embedding layer ever
/// stopped auto-attaching BLooper's native message-loop thread on first
/// entry into managed code -- see hs_application.h's threading note for why
/// that was never a given.
///
/// [TestOrder(100)] -- MUST run after every other test class that needs a
/// live BApplication (currently just WindowTests -- see its own remarks):
/// empirically verified on real Haiku hardware, once a BApplication's
/// Run() has spawned its message-loop thread and that thread has quit and
/// self-deleted the object, NO further BApplication can EVER be
/// constructed again in that same process -- not even a plain, never-run
/// one. (Constructing and disposing a never-run BApplication doesn't have
/// this effect; only a full Run()-to-quit cycle does.) Since this test is
/// the only place in the whole suite that runs a BApplication through that
/// full cycle, it has to run dead last, or it silently poisons every test
/// after it for the rest of the process. This isn't a guess -- see the
/// commit that added [TestOrder] for the throwaway repro that pinned it
/// down (two BApplications, neither ever coexisting, second construction
/// still hangs indefinitely) before assuming it was a WindowTests bug.
/// </summary>
[Haiku.Testing.TestModule("Application Kit")]
[Haiku.Testing.TestOrder(100)]
public class ApplicationTests
{
	private const uint PingMessage = 0x50494E47; // 'PING'

	private class ProbeApplication : Application
	{
		public bool ReadyToRunFired;
		public bool MessageReceivedFired;
		public string ReceivedGreeting;
		public bool QuitRequestedFired;

		public ProbeApplication() : base("application/x-vnd.HaikuSharp-Tests")
		{
		}

		protected override void OnReadyToRun()
		{
			ReadyToRunFired = true;
			using (Message ping = new Message(PingMessage)) {
				ping.AddString("greeting", "probe");
				PostMessage(ping);
			}
		}

		protected override void OnMessageReceived(Message message)
		{
			if (message.What == PingMessage) {
				MessageReceivedFired = true;
				ReceivedGreeting = message.FindString("greeting");
				PostMessage(SystemMessages.QuitRequested);
			}
		}

		protected override bool OnQuitRequested()
		{
			QuitRequestedFired = true;
			return true;
		}
	}

	[Test]
	public void ReadyToRunMessageAndQuitRequestedAllFireInOrder()
	{
		ProbeApplication app = new ProbeApplication();
		try {
			app.Run(); // blocks until ProbeApplication posts QuitRequested and it's handled
		} catch (HaikuException ex) {
			Assert.Fail("Application.Run() threw: " + ex.Message);
		}

		Assert.IsTrue(app.ReadyToRunFired, "OnReadyToRun should have fired on the looper thread");
		Assert.IsTrue(app.MessageReceivedFired, "OnMessageReceived should have fired for our own PING");
		Assert.AreEqual("probe", app.ReceivedGreeting, "the message posted from OnReadyToRun should round-trip");
		Assert.IsTrue(app.QuitRequestedFired, "OnQuitRequested should have fired");
	}
}
