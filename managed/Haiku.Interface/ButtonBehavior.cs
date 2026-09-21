namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors BButton::BBehavior (headers/os/interface/Button.h) -- a
	/// plain sequential C++ enum (B_BUTTON_BEHAVIOR=0, B_TOGGLE_BEHAVIOR=1,
	/// B_POP_UP_BEHAVIOR=2 by ordinary declaration-order semantics, NOT
	/// macro-computed like ViewResizingMode's B_FOLLOW_* constants), values
	/// verified against the actual installed Button.h, not assumed.
	/// Deliberately NOT a [Flags] enum -- these are mutually exclusive
	/// behaviors, not independent bits, same shape as MouseTransit.
	/// </summary>
	public enum ButtonBehavior
	{
		/// <summary>The default: an ordinary push button. Clicking it fires <see cref="Button.OnClick"/> once.</summary>
		PushButton = 0,

		/// <summary>Clicking toggles <see cref="Control.Value"/> between B_CONTROL_ON/B_CONTROL_OFF -- BeAPI's own toggle-button behavior.</summary>
		Toggle = 1,

		/// <summary>
		/// Real BeAPI shows a pop-up menu on click instead of invoking
		/// directly, using whatever BMessage SetPopUpMessage() configured.
		/// This binding does NOT expose SetPopUpMessage()/PopUpMessage()
		/// -- see hs_button.h's "NO BMessage/BInvoker/TARGET PLUMBING"
		/// note, the same scope decision that shaped OnClick. Setting
		/// this value is included here for BButton enum parity, but
		/// without a pop-up message configured natively there is nothing
		/// for app_server to show -- not a currently useful behavior on
		/// its own, and not exercised by this binding's tests or sample.
		/// </summary>
		PopUpMenu = 2,
	}
}
