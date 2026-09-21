using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSTextControl (see native/include/
	 * hs_text_control.h for the full design rationale -- read it before
	 * changing anything here, especially its "CRITICAL" note on
	 * BTextControl requiring a live BApplication before construction).
	 *
	 * SCOPE: Text/SetText plus the shared Label/Value/IsEnabled (via
	 * Control) -- Divider/Alignment are real BTextControl API this
	 * binding does not expose yet, a deliberate scope decision made up
	 * front (see AskUserQuestion history in this slice, and
	 * hs_text_control.h's own "SCOPE" note), not an oversight.
	 *
	 * TWO EVENTS, NOT ONE: OnTextChanged fires on every edit while
	 * focused (BeAPI's modification message); OnTextCommitted fires once,
	 * on Enter or focus-out-after-an-edit (BeAPI's Invoke()). See
	 * hs_text_control.h's "TWO DIFFERENT 'CHANGED' EVENTS" note for the
	 * exact native mechanism -- neither exposes any BMessage/BInvoker/
	 * target plumbing to managed code, matching Button's OnClick.
	 *
	 * LIFECYCLE: same shape as Button -- a freshly-constructed
	 * TextControl has no parent and is safe to configure (Text, MoveTo,
	 * ...) from whatever thread created it, PROVIDED a BApplication
	 * already exists in the process (see the CRITICAL note above --
	 * this is the one lifecycle rule that genuinely differs from every
	 * other View/Control this binding wraps). Once added to a Window or
	 * View, it draws and handles input entirely on its own. The most
	 * common real usage never calls Dispose() at all: once added, its
	 * parent owns it and deletes it automatically, recursively, whenever
	 * that parent itself is destroyed -- OnDestroyed still fires when
	 * that happens.
	 */
	public class TextControl : Control
	{
		private readonly TextControlTextChangedCallback _textChangedThunk;
		private readonly TextControlTextCommittedCallback _textCommittedThunk;
		private readonly TextControlDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain text field
		/// with real BeAPI's own default flags
		/// (B_WILL_DRAW | B_NAVIGABLE | B_FRAME_EVENTS, see
		/// TextControl.h) and no special resizing mode.
		/// </summary>
		public TextControl(Rect frame, string name, string label, string text)
			: this(frame, name, label, text, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FrameEvents)
		{
		}

		public TextControl(Rect frame, string name, string label, string text,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeTextControl(frame, name, label, text, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_textChangedThunk = TextChangedThunk;
			_textCommittedThunk = TextCommittedThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_text_control_set_text_changed_callback(_handle, _textChangedThunk, userData);
			Native.hs_text_control_set_text_committed_callback(_handle, _textCommittedThunk, userData);
			Native.hs_text_control_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / Button.cs's CreateNativeButton
		// -- same "base(...) needs an expression" reason for pulling
		// hs_text_control_create() out here. Callers must have a live
		// BApplication already constructed -- see hs_text_control.h's
		// CRITICAL note; this call hangs forever otherwise, it does not
		// throw or fail fast.
		private static IntPtr CreateNativeTextControl(Rect frame, string name,
			string label, string text, ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_text_control_create(nativeFrame, name, label, text,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>
		/// This text field's current contents. The getter always reflects
		/// what's on screen, including in-progress edits the user hasn't
		/// committed yet (see <see cref="OnTextCommitted"/>).
		/// </summary>
		public string Text
		{
			get
			{
				CheckNotConsumed();
				// Borrowed pointer into the control's own storage --
				// copied into a managed string immediately, same
				// treatment as Control.Label/Window.Title.
				return Marshal.PtrToStringAnsi(Native.hs_text_control_text(_handle));
			}
			set
			{
				CheckNotConsumed();
				Native.hs_text_control_set_text(_handle, value);
			}
		}

		/// <summary>
		/// Called on the owning window's thread whenever the user
		/// modifies the text while this control has keyboard focus --
		/// fires on every edit, not just on commit (see
		/// <see cref="OnTextCommitted"/> for that). No BMessage/target is
		/// involved -- see hs_text_control.h's "TWO DIFFERENT 'CHANGED'
		/// EVENTS" note. Default: does nothing.
		/// </summary>
		protected virtual void OnTextChanged() { }

		/// <summary>
		/// Called on the owning window's thread when an edit is
		/// committed: Enter/Return pressed while this control has focus,
		/// or focus lost after the text changed. Fires once per commit,
		/// unlike <see cref="OnTextChanged"/>. No BMessage/target is
		/// involved -- see hs_text_control.h's "TWO DIFFERENT 'CHANGED'
		/// EVENTS" note. Default: does nothing.
		/// </summary>
		protected virtual void OnTextCommitted() { }

		private static TextControl FromUserData(IntPtr userData)
		{
			return (TextControl)GCHandle.FromIntPtr(userData).Target;
		}

		private static void TextChangedThunk(IntPtr userData)
		{
			FromUserData(userData).OnTextChanged();
		}

		private static void TextCommittedThunk(IntPtr userData)
		{
			FromUserData(userData).OnTextCommitted();
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as View.cs's/
			// Button.cs's own DestroyedThunk -- see
			// ViewBase.MarkDestroyed()'s comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_text_control_destroy(handle);
		}
	}
}
