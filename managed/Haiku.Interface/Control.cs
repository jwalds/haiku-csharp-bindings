using System;
using System.Runtime.InteropServices;

namespace Haiku.Interface
{
	/*
	 * Shared base for Button (and future BControl-derived widgets --
	 * checkbox, radio button, ...) -- the managed mirror of real BeAPI's
	 * BControl. Wraps the BControl-level surface common to every control:
	 * Label, Value, IsEnabled. See hs_button.h's "BCONTROL-LEVEL STATE"
	 * note for why the underlying native functions are still named
	 * hs_button_* rather than hs_control_* today -- Button is the only
	 * concrete control this binding has, so there is nothing to
	 * generalize yet; revisit the native side (not this class) the day a
	 * second one is added.
	 *
	 * Derives from ViewBase, NOT View -- a native control (BButton, and
	 * any future BControl subclass) draws and handles input entirely on
	 * its own; there is no OnDraw/OnMouseDown/OnKeyDown/... here the way
	 * there is on View, because the native shim never wires up those
	 * callbacks for a control's handle in the first place (see
	 * hs_button.h's "WHY BUTTON DOESN'T GET ITS OWN DRAW/ATTACHED/
	 * MOUSE/KEY CALLBACKS" note) -- there is simply nothing that would
	 * ever call them. Frame/MoveTo/ResizeTo (for positioning) and
	 * AddChild/RemoveChild interop (a Control can be added to a Window or
	 * a View, see ViewBase.cs) still work, since those only ever touch
	 * plain inherited BView state.
	 *
	 * Label/Value/IsEnabled are plain C# read/write properties rather
	 * than BeAPI's separate SetXxx()/Xxx() method pairs, matching the
	 * precedent Window.Title already set (collapsing a pure data
	 * attribute's get/set pair into one property) rather than View's
	 * MakeFocus()/IsFocus() shape (kept as two members there because
	 * MakeFocus takes a defaulted parameter and reads more like an
	 * action than a plain attribute -- Button.MakeDefault()/IsDefault()
	 * follows that same action-like shape for the same reason).
	 */
	public abstract class Control : ViewBase
	{
		protected Control(IntPtr handle)
			: base(handle)
		{
		}

		public string Label
		{
			get
			{
				CheckNotConsumed();
				// Borrowed pointer into the control's own storage --
				// copied into a managed string immediately, same
				// treatment as Window.Title/Message.FindString.
				return Marshal.PtrToStringAnsi(Native.hs_button_label(_handle));
			}
			set
			{
				CheckNotConsumed();
				Native.hs_button_set_label(_handle, value);
			}
		}

		/// <summary>
		/// BControl's generic int32 "value" -- for a plain push-button
		/// this is rarely used directly (it matters more for toggle-style
		/// controls, e.g. B_CONTROL_ON/B_CONTROL_OFF), but it's part of
		/// the shared BControl surface this base class exists for.
		/// </summary>
		public int Value
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_button_value(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_button_set_value(_handle, value);
			}
		}

		public bool IsEnabled
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_button_is_enabled(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_button_set_enabled(_handle, value);
			}
		}
	}
}
