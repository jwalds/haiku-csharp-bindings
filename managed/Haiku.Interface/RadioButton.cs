using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSRadioButton (see native/include/
	 * hs_radio_button.h for the full design rationale -- read it before
	 * changing anything here). Same shape as CheckBox.cs, which this
	 * mirrors closely: a direct OnClick hook, Label/Value/IsEnabled
	 * inherited from Control, and an IsChecked bool convenience property.
	 *
	 * GROUPING IS AUTOMATIC -- NO API HERE FOR IT. Real BeAPI groups
	 * radio buttons purely by shared parent View: add two or more
	 * RadioButtons to the same View (via AddChild, inherited from
	 * ViewBase -- see Window.cs/View.cs), and turning one on
	 * automatically turns its siblings off, with zero code in this
	 * class. Put RadioButtons meant to be independent choices under
	 * separate parent Views. Verified on real hardware -- see
	 * hs_radio_button.h's grouping note for the full writeup, including
	 * a caveat about calling SetValue()/IsChecked from a thread other
	 * than the one that will run Application.Run(), against a radio
	 * button that is a child of an already-Show()n Window.
	 *
	 * LIFECYCLE, AND A REAL DIFFERENCE FROM CHECKBOX: constructing a
	 * RadioButton REQUIRES a live BApplication to already exist in this
	 * process -- confirmed on real hardware (construction hangs
	 * indefinitely otherwise), matching TextControl but NOT CheckBox,
	 * despite RadioButton and CheckBox being structurally almost
	 * identical. See hs_radio_button.h's own note for the full
	 * (unexplained) story. Always construct an <see cref="Application"/>
	 * first, same rule as TextControl.
	 */
	public class RadioButton : Control
	{
		private readonly RadioButtonClickCallback _clickThunk;
		private readonly RadioButtonDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain radio
		/// button with real BeAPI's own default flags
		/// (B_WILL_DRAW | B_NAVIGABLE | B_FULL_UPDATE_ON_RESIZE, matching
		/// Button's/CheckBox's own convenience overloads) and no special
		/// resizing mode.
		/// </summary>
		public RadioButton(Rect frame, string name, string label)
			: this(frame, name, label, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.Navigable | ViewFlags.FullUpdateOnResize)
		{
		}

		public RadioButton(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeRadioButton(frame, name, label, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_clickThunk = ClickThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_radio_button_set_click_callback(_handle, _clickThunk, userData);
			Native.hs_radio_button_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / Button.cs's CreateNativeButton
		// -- same "base(...) needs an expression" reason for pulling
		// hs_radio_button_create() out here.
		//
		// REQUIRES a live BApplication to already exist -- see this
		// class's own remarks and hs_radio_button.h. Constructing a
		// RadioButton before any Application exists in the process
		// hangs, it does not throw -- there is nothing this managed
		// wrapper can do to turn that into a catchable exception, since
		// the hang happens inside the native constructor call itself.
		private static IntPtr CreateNativeRadioButton(Rect frame, string name, string label,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_radio_button_create(nativeFrame, name, label,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>
		/// Called on the owning window's thread when this radio button is
		/// clicked (MouseDown followed by MouseUp still over it, or
		/// Space/Enter while it has focus). By the time this fires,
		/// <see cref="Control.Value"/> / <see cref="IsChecked"/> already
		/// reflect the NEW state and any sibling radio buttons under the
		/// same parent have already been turned off -- BRadioButton does
		/// all of this itself before Invoke() runs. No BMessage/target is
		/// involved -- see hs_radio_button.h's "NO BMessage/BInvoker/
		/// TARGET PLUMBING" note. Default: does nothing.
		/// </summary>
		protected virtual void OnClick() { }

		/// <summary>
		/// Friendlier bool view of the inherited <see cref="Control.Value"/>
		/// (B_CONTROL_ON=1 / B_CONTROL_OFF=0) -- matches CheckBox.IsChecked.
		/// Setting this to true turns off any sibling radio buttons under
		/// the same parent View automatically (verified on real hardware
		/// -- see hs_radio_button.h's grouping note); setting it to false
		/// does not automatically turn any sibling on, matching real
		/// BeAPI's own SetValue(B_CONTROL_OFF) behavior. Does not fire
		/// <see cref="OnClick"/> (matching real BeAPI: SetValue() alone
		/// never invokes a control, only an actual click/key or an
		/// explicit Invoke() does).
		/// </summary>
		public bool IsChecked
		{
			get { return Value != 0; }
			set { Value = value ? 1 : 0; }
		}

		private static RadioButton FromUserData(IntPtr userData)
		{
			return (RadioButton)GCHandle.FromIntPtr(userData).Target;
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
			Native.hs_radio_button_destroy(handle);
		}
	}
}
