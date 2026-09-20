using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSWindow (see native/include/hs_window.h
	 * for the full design rationale -- read it before changing anything
	 * here, especially the ownership rule this class's Dispose() enforces).
	 *
	 * LIFECYCLE, IN ONE PARAGRAPH: a freshly-constructed Window has no
	 * native thread yet and is safe to configure (SetTitle, MoveTo, ...)
	 * from whatever thread created it. The first call to Show() starts its
	 * message-loop thread; after that, OnMessageReceived/OnQuitRequested
	 * fire on THAT thread, not on the thread that created the Window --
	 * same rule as Application (see Application.cs). There is no blocking
	 * "wait for this window" call the way Application.Run() blocks for the
	 * app -- a shown window lives and dies independently, so OnDestroyed()
	 * is how you find out it's gone, whenever that happens to be.
	 */
	public class Window : IDisposable
	{
		private IntPtr _handle;
		private GCHandle _selfHandle;
		private bool _shown;

		// Kept as instance fields so they aren't GC'd while native code
		// might still call them -- see Haiku.App.Native's comment on this.
		private readonly WindowMessageReceivedCallback _messageReceivedThunk;
		private readonly WindowQuitRequestedCallback _quitRequestedThunk;
		private readonly WindowDestroyedCallback _destroyedThunk;

		/// <summary>Convenience overload for the common case: a normal, titled, resizable window.</summary>
		public Window(Rect frame, string title)
			: this(frame, title, WindowLook.Titled, WindowFeel.Normal, WindowFlags.None)
		{
		}

		public Window(Rect frame, string title, WindowLook look, WindowFeel feel, WindowFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			_handle = Native.hs_window_create(nativeFrame, title,
				(int)look, (int)feel, (uint)flags);

			// A NORMAL (not weak) GCHandle: keeps this managed Window instance
			// alive for as long as native code might call back into it,
			// independent of whether managed code still holds a reference --
			// same rationale as Application's _selfHandle. Freed the instant
			// OnDestroyed fires (see DestroyedThunk below), from whichever
			// thread that happens to be.
			_selfHandle = GCHandle.Alloc(this);
			IntPtr userData = GCHandle.ToIntPtr(_selfHandle);

			_messageReceivedThunk = MessageReceivedThunk;
			_quitRequestedThunk = QuitRequestedThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_window_set_message_received_callback(_handle, _messageReceivedThunk, userData);
			Native.hs_window_set_quit_requested_callback(_handle, _quitRequestedThunk, userData);
			Native.hs_window_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		/// <summary>Called on the window's own thread when a message arrives. Default: does nothing.</summary>
		protected virtual void OnMessageReceived(Message message) { }

		/// <summary>Called on the window's own thread when quit is requested. Return false to veto. Default: allows it.</summary>
		protected virtual bool OnQuitRequested() { return true; }

		/// <summary>
		/// Called exactly once, right as the native window object is being
		/// deleted -- from HSWindow's own C++ destructor, so it covers both
		/// the normal quit flow (on the window's own thread) and the
		/// never-shown cleanup path (synchronously, on whatever thread
		/// disposed this Window). By the time this runs, no other method on
		/// this instance is safe to call. Default: does nothing.
		/// </summary>
		protected virtual void OnDestroyed() { }

		/// <summary>
		/// Shows the window. The FIRST call also starts its message-loop
		/// thread (see the class remarks) -- from this point on,
		/// OnMessageReceived/OnQuitRequested run on that thread.
		/// </summary>
		public void Show()
		{
			CheckNotConsumed();
			_shown = true;
			Native.hs_window_show(_handle);
		}

		public void Hide()
		{
			CheckNotConsumed();
			Native.hs_window_hide(_handle);
		}

		public bool IsHidden
		{
			get { CheckNotConsumed(); return Native.hs_window_is_hidden(_handle); }
		}

		/// <summary>
		/// Requests that the window quit, by posting B_QUIT_REQUESTED through
		/// its own message queue -- safe to call from any thread, including
		/// the window's own (see hs_window.h's "QUITTING" note for why this
		/// doesn't call BWindow::Quit() directly). This is asynchronous:
		/// OnDestroyed() fires later, once the window's thread actually
		/// processes it, not before this call returns.
		/// </summary>
		public void Quit()
		{
			CheckNotConsumed();
			Native.hs_window_quit(_handle);
		}

		public bool Lock()
		{
			CheckNotConsumed();
			return Native.hs_window_lock(_handle);
		}

		public void Unlock()
		{
			CheckNotConsumed();
			Native.hs_window_unlock(_handle);
		}

		public bool IsLocked
		{
			get { CheckNotConsumed(); return Native.hs_window_is_locked(_handle); }
		}

		public string Title
		{
			get
			{
				CheckNotConsumed();
				// Borrowed pointer into the window's own storage -- copied
				// into a managed string immediately, same treatment as
				// Message.FindString. See Native.cs's comment on why this
				// isn't declared to return `string` directly.
				return Marshal.PtrToStringAnsi(Native.hs_window_title(_handle));
			}
			set
			{
				CheckNotConsumed();
				Native.hs_window_set_title(_handle, value);
			}
		}

		public Rect Frame
		{
			get
			{
				CheckNotConsumed();
				HsRect native;
				Native.hs_window_get_frame(_handle, out native);
				return new Rect(native.Left, native.Top, native.Right, native.Bottom);
			}
		}

		public void MoveTo(float x, float y)
		{
			CheckNotConsumed();
			Native.hs_window_move_to(_handle, x, y);
		}

		public void ResizeTo(float width, float height)
		{
			CheckNotConsumed();
			Native.hs_window_resize_to(_handle, width, height);
		}

		/// <summary>
		/// Adds child as a top-level child of this window's own view
		/// hierarchy -- a BWindow is the root of its own view tree, exactly
		/// like a BView is the root of its children's (see hs_window.h's own
		/// hs_window_add_child() doc). OnAttachedToWindow() fires on child
		/// (and its descendants, if any) synchronously, on whatever thread
		/// calls this -- even before this window has ever been shown, since
		/// (unlike a nested View-in-View attachment) a window never itself
		/// needs to "become attached" to anything first; see hs_window.h's
		/// own hs_window_add_child() doc for why this is safe pre-Show()
		/// specifically (no message-loop thread running yet to race with).
		/// </summary>
		public void AddChild(View child)
		{
			CheckNotConsumed();
			if (child == null)
				throw new ArgumentNullException("child");
			Native.hs_window_add_child(_handle, child._handle);
			child._hasParent = true;
		}

		/// <summary>
		/// Detaches child from this window's child list without deleting it
		/// -- see hs_view.h's OWNERSHIP note (the same rule applies whether
		/// a view's parent is a Window or another View). Returns false if
		/// child was not actually a direct child of this window.
		/// </summary>
		public bool RemoveChild(View child)
		{
			CheckNotConsumed();
			if (child == null)
				throw new ArgumentNullException("child");
			bool removed = Native.hs_window_remove_child(_handle, child._handle);
			if (removed)
				child._hasParent = false;
			return removed;
		}

		private void CheckNotConsumed()
		{
			if (_handle == IntPtr.Zero)
				throw new ObjectDisposedException(GetType().Name,
					"This Window has been destroyed (or quit was requested) and can no longer be used.");
		}

		private static Window FromUserData(IntPtr userData)
		{
			return (Window)GCHandle.FromIntPtr(userData).Target;
		}

		private static void MessageReceivedThunk(IntPtr userData, IntPtr messageHandle)
		{
			Window window = FromUserData(userData);
			// Borrowed, not owned -- do NOT Dispose() this (see Message's ctor doc).
			// Message's borrowed-handle constructor is `internal` to Haiku.App;
			// Haiku.App's AssemblyInfo.cs grants Haiku.Interface visibility to
			// it specifically so this line can exist without duplicating the
			// whole Message class here.
			Message message = new Message(messageHandle);
			window.OnMessageReceived(message);
		}

		private static int QuitRequestedThunk(IntPtr userData)
		{
			return FromUserData(userData).OnQuitRequested() ? 1 : 0;
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			Window window = FromUserData(userData);
			// The native object is gone (or, in the never-shown Dispose()
			// path below, is about to finish going right after this thunk
			// returns). Null the handle and free the GCHandle before firing
			// OnDestroyed, same "consumed-before-notified" ordering
			// Application.Run() uses -- so any call this override makes back
			// into `window` sees a cleanly consumed instance, not a
			// dangling handle.
			window._handle = IntPtr.Zero;
			window.FreeSelfHandle();
			window.OnDestroyed();
		}

		private void FreeSelfHandle()
		{
			if (_selfHandle.IsAllocated)
				_selfHandle.Free();
		}

		/// <summary>
		/// Ends this window's life. Which native call that means depends on
		/// whether Show() was ever called (see hs_window.h's ownership
		/// rule): never shown, no thread exists yet, so this deletes the
		/// native object synchronously, right here -- OnDestroyed() fires
		/// before this call returns. Once shown, the window's own thread is
		/// live, so this only requests a quit (exactly Quit() above); the
		/// actual destruction and OnDestroyed() happen later, off this call,
		/// once that thread processes it. Either way, the handle is marked
		/// consumed immediately so a second Dispose() call is a no-op rather
		/// than a duplicate native call racing the first.
		/// </summary>
		public void Dispose()
		{
			IntPtr handle = _handle;
			if (handle == IntPtr.Zero)
				return;

			_handle = IntPtr.Zero;

			if (_shown)
				Native.hs_window_quit(handle);
			else
				Native.hs_window_destroy(handle);
		}
	}
}
