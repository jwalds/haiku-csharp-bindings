using System;

namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's B_FOLLOW_* resizing-mode constants (headers/os/
	/// interface/View.h). Deliberately NOT a [Flags] enum, unlike
	/// <see cref="ViewFlags"/>: upstream builds most of these values with
	/// its own internal `_rule_(top, left, bottom, right)` macro rather
	/// than by OR-ing independent bits, so (for example) <c>All</c> is
	/// its own distinct encoded value, not <c>Top | Left | Bottom |
	/// Right</c> -- freely OR-ing arbitrary members here would not always
	/// produce a value Haiku actually recognizes. <see cref="LeftTop"/>
	/// happens to be the one upstream itself defines as a real OR
	/// (<c>Top | Left</c>), which still works if you write it that way,
	/// but the general rule is: use one of the named values below as-is,
	/// don't combine your own. Every value here was verified on real
	/// Haiku hardware (a tiny native program printing the actual compiled
	/// constants) rather than hand-computed from the macro, specifically
	/// because of that computed-not-literal risk.
	/// </summary>
	public enum ViewResizingMode : uint
	{
		/// <summary>Fixed position and size relative to its parent -- the default a new View uses if you don't say otherwise.</summary>
		None = 0x0000,
		FollowLeft = 0x0202,
		FollowRight = 0x0404,
		FollowLeftRight = 0x0204,
		FollowHCenter = 0x0505,
		FollowTop = 0x1010,
		FollowBottom = 0x3030,
		FollowTopBottom = 0x1030,
		FollowVCenter = 0x5050,
		FollowLeftTop = 0x1212,
		/// <summary>Resizes and moves with its parent on all four sides.</summary>
		All = 0x1234,
	}
}
