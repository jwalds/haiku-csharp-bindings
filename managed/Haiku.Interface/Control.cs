using System;
using System.Runtime.InteropServices;

namespace Haiku.Interface
{
	/*
	 * Shared base for Button, TextControl (and any future BControl-
	 * derived widget) -- the managed mirror of real BeAPI's BControl.
	 * Wraps the BControl-level surface common to every control: Label,
	 * Value, IsEnabled. These properties call the native hs_control_*
	 * functions (see native/include/hs_control.h) -- a shared shim used
	 * by any concrete control's handle, cast straight to BControl*,
	 * verified safe by an ABI probe on real hardware for both the
	 * BButton and BTextControl chains. (Originally these called
	 * hs_button_* directly, back when Button was the only concrete
	 * control this binding had; that changed when TextControl arrived.)
	 *
	 * Derives from ViewBase, NOT View -- a native control (BButton,
	 * BTextControl, and any future BControl subclass) draws and handles
	 * input entirely on its own; there is no OnDraw/OnMouseDown/
	 * OnKeyDown/... here the way there is on View, because the native
	 * shim never wires up those callbacks for a control's handle in the
	 * first place (see hs_button.h's "WHY BUTTON DOESN'T GET ITS OWN
	 * DRAW/ATTACHED/MOUSE/KEY CALLBACKS" note). Frame/MoveTo/ResizeTo
	 * (for positioning) and AddChild/RemoveChild interop (a Control can
	 * be added to a Window or a View, see ViewBase.cs) still work, since
	 * those only ever touch plain inherited BView state.
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
				return Marshal.PtrToStringAnsi(Native.hs_control_label(_handle));
			}
			set
			{
				CheckNotConsumed();
				Native.hs_control_set_label(_handle, value);
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
				return Native.hs_control_value(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_control_set_value(_handle, value);
			}
		}

		public bool IsEnabled
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_control_is_enabled(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_control_set_enabled(_handle, value);
			}
		}
	}
}
