namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's mouse-transit codes (headers/os/interface/View.h,
	/// the B_ENTERED_VIEW/B_INSIDE_VIEW/B_EXITED_VIEW/B_OUTSIDE_VIEW
	/// constants), delivered as the second parameter to View's
	/// OnMouseMoved hook. Unlike MouseButtons, these are sequential and
	/// mutually exclusive (a single moment's transit is exactly one of
	/// the four), so this is deliberately NOT a [Flags] enum -- same
	/// precedent as ViewResizingMode next to the [Flags] ViewFlags.
	/// </summary>
	public enum MouseTransit
	{
		Entered = 0,
		Inside = 1,
		Exited = 2,
		Outside = 3,
	}
}
