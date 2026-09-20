using System;
using System.Runtime.InteropServices;

namespace Haiku.App
{
	/*
	 * Managed wrapper over the native HSApplication (see hs_application.h
	 * for the full design rationale). To use it: subclass Application,
	 * override OnMessageReceived/OnQuitRequested/OnReadyToRun as needed,
	 * construct it, then call Run().
	 *
	 * THREADING: your overrides run on Haiku's own message-loop thread, NOT
	 * on the thread that called Run() -- see hs_application.h's threading
	 * note. Run() blocks the calling thread until the app quits; while it
	 * is blocked, PostMessage() and friends remain usable from inside your
	 * own overrides (that is how OnReadyToRun typically kicks the app off).
	 * Only once Run() actually RETURNS (per the native ownership rule) is
	 * this instance's native handle gone; calling any other method on this
	 * object after that point throws ObjectDisposedException.
	 *
	 * ONE-SHOT PER PROCESS, NOT JUST ONE AT A TIME
	 * ----------------------------------------------
	 * Real Haiku apps construct exactly one BApplication, Run() it, and let
	 * the process exit soon after it quits -- normal usage never needs more
	 * than that. It turns out that isn't just the normal pattern, it's a
	 * hard requirement: verified empirically on real Haiku hardware (while
	 * building this binding's test suite -- see managed/Tests/
	 * ApplicationTests.cs's own remarks for the full story), once a
	 * BApplication's Run() has spawned its message-loop thread and that
	 * thread has quit and deleted the object, NO further BApplication can
	 * ever be constructed again in that same process -- the attempt hangs
	 * indefinitely, not even a plain never-Run() one succeeds afterward.
	 * Constructing-and-disposing a BApplication that never called Run() has
	 * no such effect and can be repeated freely; it's specifically the
	 * spawn-thread-then-self-delete path that permanently uses up the
	 * process's one shot. If your process legitimately needs more than one
	 * BApplication's worth of app_server access over its lifetime, it
	 * can't get it by constructing a second Application after the first
	 * one's Run() returns -- that path is closed, not just discouraged.
	 */
	public class Application : IDisposable
	{
		private IntPtr _handle;
		private GCHandle _selfHandle;

		// Kept as instance fields so they aren't GC'd while native code
		// might still call them -- see Native.cs's comment on this.
		private readonly MessageReceivedCallback _messageReceivedThunk;
		private readonly QuitRequestedCallback _quitRequestedThunk;
		private readonly ReadyToRunCallback _readyToRunThunk;

		/// <param name="signature">
		/// A valid Haiku app signature MIME string, e.g. "application/x-vnd.YourName-AppName".
		/// </param>
		public Application(string signature)
		{
			int error;
			_handle = Native.hs_application_create(signature, out error);
			if (_handle == IntPtr.Zero)
				throw new HaikuException(error);

			// A NORMAL (not weak) GCHandle: this is what keeps the managed
			// Application instance alive for as long as native code might
			// call back into it, independent of whether managed code still
			// holds a reference. Freed once Run() returns or Dispose() runs.
			_selfHandle = GCHandle.Alloc(this);
			IntPtr userData = GCHandle.ToIntPtr(_selfHandle);

			_messageReceivedThunk = MessageReceivedThunk;
			_quitRequestedThunk = QuitRequestedThunk;
			_readyToRunThunk = ReadyToRunThunk;

			Native.hs_application_set_message_received_callback(_handle, _messageReceivedThunk, userData);
			Native.hs_application_set_quit_requested_callback(_handle, _quitRequestedThunk, userData);
			Native.hs_application_set_ready_to_run_callback(_handle, _readyToRunThunk, userData);
		}

		/// <summary>Called on the looper thread when a message arrives. Default: does nothing.</summary>
		protected virtual void OnMessageReceived(Message message) { }

		/// <summary>Called on the looper thread when quit is requested. Return false to veto. Default: allows it.</summary>
		protected virtual bool OnQuitRequested() { return true; }

		/// <summary>Called on the looper thread once the app has finished initializing. Default: does nothing.</summary>
		protected virtual void OnReadyToRun() { }

		public void PostMessage(uint what)
		{
			CheckNotConsumed();
			int status = Native.hs_application_post_message_what(_handle, what);
			if (status != 0)
				throw new HaikuException(status);
		}

		public void PostMessage(Message message)
		{
			CheckNotConsumed();
			int status = Native.hs_application_post_message(_handle, message.Handle);
			if (status != 0)
				throw new HaikuException(status);
		}

		/// <summary>
		/// Starts the message loop and blocks the calling thread until the app quits.
		/// After this returns, this Application instance is spent -- see the class
		/// remarks on why, and don't call any other method on it afterward.
		/// </summary>
		public void Run()
		{
			CheckNotConsumed();

			IntPtr handle = _handle;

			// _handle stays live for the duration of this call. OnReadyToRun,
			// OnMessageReceived, and OnQuitRequested all fire on the looper
			// thread WHILE this call is blocked here -- not after it returns
			// -- and a callback that calls PostMessage() (see the sample)
			// needs a valid _handle to do that. Only once
			// hs_application_run_and_wait() actually returns has BLooper's
			// own teardown deleted the native object (see the OWNERSHIP note
			// in hs_application.h), so that return -- not the start of this
			// call -- is the instant this wrapper must stop being usable.
			int status = Native.hs_application_run_and_wait(handle);

			// The native object is gone now. Null the field first, before
			// anything else below (including the throw), so any call that
			// lands here afterward -- from any thread -- sees a cleanly
			// consumed instance rather than a dangling handle.
			_handle = IntPtr.Zero;
			FreeSelfHandle();

			if (status != 0)
				throw new HaikuException(status);
		}

		private void CheckNotConsumed()
		{
			if (_handle == IntPtr.Zero)
				throw new ObjectDisposedException(GetType().Name,
					"This Application has already run to completion (or was disposed) and can no longer be used.");
		}

		private void FreeSelfHandle()
		{
			if (_selfHandle.IsAllocated)
				_selfHandle.Free();
		}

		private static Application FromUserData(IntPtr userData)
		{
			return (Application)GCHandle.FromIntPtr(userData).Target;
		}

		private static void MessageReceivedThunk(IntPtr userData, IntPtr messageHandle)
		{
			Application app = FromUserData(userData);
			// Borrowed, not owned -- do NOT Dispose() this (see Message's ctor doc).
			Message message = new Message(messageHandle);
			app.OnMessageReceived(message);
		}

		private static int QuitRequestedThunk(IntPtr userData)
		{
			return FromUserData(userData).OnQuitRequested() ? 1 : 0;
		}

		private static void ReadyToRunThunk(IntPtr userData)
		{
			FromUserData(userData).OnReadyToRun();
		}

		/// <summary>
		/// Only meaningful if construction succeeded but Run() was never called
		/// (e.g. you decided not to start the app after all). Do not call this
		/// from another thread while Run() is still blocked on this instance --
		/// that races with the still-live native object. Once Run() has
		/// returned, the native object is already gone and this is a no-op.
		/// </summary>
		public void Dispose()
		{
			if (_handle != IntPtr.Zero) {
				Native.hs_application_destroy(_handle);
				_handle = IntPtr.Zero;
			}
			FreeSelfHandle();
		}
	}
}
