using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSCheckBox (see native/include/
	 * hs_checkbox.h for the full design rationale -- read it before
	 * changing anything here). Same shape as Button.cs, which this
	 * mirrors almost exactly: a direct OnClick hook (no BMessage/
	 * BInvoker/target plumbing), Label/Value/IsEnabled inherited from
	 * Control, plus a friendlier IsChecked bool on top of the raw int
	 * Value -- matching how Button added IsDefault/IsFlat beyond raw
	 * Control state.
	 *
	 * LIFECYCLE: same as Button -- a freshly-constructed CheckBox has no
	 * parent and is safe to configure from whatever thread created it.
	 * Unlike TextControl/RadioButton, constructing a CheckBox does NOT
	 * require a live BApplication in a process where none has EVER
	 * existed yet -- confirmed on real hardware, see hs_checkbox.h's own
	 * note. That guarantee does NOT extend to a process that has already
	 * constructed and disposed an Application earlier (even one that
	 * never called Run()) -- see hs_checkbox.h's caveat, found the hard
	 * way via CheckBoxTests.cs. A real app that constructs one
	 * Application and keeps it for the app's lifetime (the normal
	 * pattern) never hits this. Once added to a Window or View, a
	 * CheckBox draws and handles input entirely on its own.
	 */
	public class CheckBox : Control
	{
		private readonly CheckBoxClickCallback _clickThunk;
		private readonly CheckBoxDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain checkbox
		/// with real BeAPI's own default flags
		/// (B_WILL_DRAW | B_NAVIGABLE | B_FULL_UPDATE_ON_RESIZE, matching
		/// Button's own convenience overload) and no special resizing
		/// mode.
		/// </summary>
		public CheckBox(Rect frame, string name, string label)
			: this(frame, name, label, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FullUpdateOnResize)
		{
		}

		public CheckBox(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeCheckBox(frame, name, label, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_clickThunk = ClickThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_checkbox_set_click_callback(_handle, _clickThunk, userData);
			Native.hs_checkbox_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / Button.cs's CreateNativeButton
		// -- same "base(...) needs an expression" reason for pulling
		// hs_checkbox_create() out here.
		private static IntPtr CreateNativeCheckBox(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_checkbox_create(nativeFrame, name, label,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>
		/// Called on the owning window's thread when this checkbox is
		/// clicked (MouseDown followed by MouseUp still over it, or
		/// Space/Enter while it has focus). By the time this fires,
		/// <see cref="Control.Value"/> / <see cref="IsChecked"/> already
		/// reflect the NEW state -- BCheckBox toggles Value itself before
		/// Invoke() runs (verified against the real BCheckBox API docs).
		/// No BMessage/target is involved -- see hs_checkbox.h's "NO
		/// BMessage/BInvoker/TARGET PLUMBING" note. Default: does
		/// nothing.
		/// </summary>
		protected virtual void OnClick() { }

		/// <summary>
		/// Friendlier bool view of the inherited <see cref="Control.Value"/>
		/// (B_CONTROL_ON=1 / B_CONTROL_OFF=0) -- matches how Button adds
		/// IsDefault/IsFlat beyond raw Control state. Setting this calls
		/// straight through to Value; it does not fire <see cref="OnClick"/>
		/// (matching real BeAPI: SetValue() alone never invokes a
		/// control, only an actual click/key or an explicit Invoke() does).
		/// </summary>
		public bool IsChecked
		{
			get { return Value != 0; }
			set { Value = value ? 1 : 0; }
		}

		private static CheckBox FromUserData(IntPtr userData)
		{
			return (CheckBox)GCHandle.FromIntPtr(userData).Target;
		}

		private static void ClickThunk(IntPtr userData)
		{
			FromUserData(userData).OnClick();
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
			Native.hs_checkbox_destroy(handle);
		}
	}
}
