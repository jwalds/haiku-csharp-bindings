using System;

namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's view flags bitmask (headers/os/interface/View.h, the
	/// B_WILL_DRAW/B_FRAME_EVENTS/... constants), every value copied
	/// verbatim from that header (they're given there as plain hex
	/// literals, not macro-computed -- see ViewResizingMode.cs for the
	/// one group of Interface Kit constants that needed hardware
	/// verification instead). Combine with bitwise-or, e.g.
	/// <c>ViewFlags.WillDraw | ViewFlags.FrameEvents</c>.
	/// </summary>
	[Flags]
	public enum ViewFlags : uint
	{
		None = 0,
		FullUpdateOnResize = 0x80000000,
		WillDraw = 0x20000000,
		PulseNeeded = 0x10000000,
		NavigableJump = 0x08000000,
		FrameEvents = 0x04000000,
		Navigable = 0x02000000,
		SubpixelPrecise = 0x01000000,
		DrawOnChildren = 0x00800000,
		InputMethodAware = 0x00400000,
		ScrollViewAware = 0x00200000,
		SupportsLayout = 0x00100000,
		InvalidateAfterLayout = 0x00080000,
		TransparentBackground = 0x00040000,
	}
}
