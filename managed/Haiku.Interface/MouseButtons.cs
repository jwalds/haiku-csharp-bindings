using System;

namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's mouse-button bitmask (headers/os/interface/View.h,
	/// the B_PRIMARY_MOUSE_BUTTON/B_SECONDARY_MOUSE_BUTTON/
	/// B_TERTIARY_MOUSE_BUTTON constants). These come from
	/// <c>B_MOUSE_BUTTON(n) = 1 &lt;&lt; (n-1)</c> in that header --
	/// genuinely independent bits (unlike ViewResizingMode's B_FOLLOW_*
	/// group), so this is a [Flags] enum and multiple buttons can be held
	/// down at once, e.g. <c>MouseButtons.Primary | MouseButtons.Secondary</c>.
	/// Delivered by View's OnMouseDown/OnMouseMoved hooks. OnMouseUp does
	/// NOT get a buttons value at all -- B_MOUSE_UP messages carry no
	/// "buttons" field, unlike B_MOUSE_DOWN and B_MOUSE_MOVED (verified
	/// against the Be Book's message-constants documentation, not assumed;
	/// see the MOUSE AND KEYBOARD INPUT note in native/include/hs_view.h).
	/// </summary>
	[Flags]
	public enum MouseButtons : uint
	{
		None = 0,
		Primary = 0x1,
		Secondary = 0x2,
		Tertiary = 0x4,
	}
}
