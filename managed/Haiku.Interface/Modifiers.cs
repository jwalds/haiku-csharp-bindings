using System;

namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's modifier-key bitmask (headers/os/interface/
	/// InterfaceDefs.h, the B_SHIFT_KEY/B_COMMAND_KEY/B_CONTROL_KEY/...
	/// constants), every value copied verbatim from that header -- a real
	/// [Flags] enum, same as MouseButtons (these are independent bits, not
	/// macro-computed like ViewResizingMode). Includes both the
	/// "either side" convenience bits (Shift, Command, Control, Option --
	/// set whenever the corresponding left OR right physical key is down)
	/// and the side-specific bits (LeftShift, RightShift, ...) for telling
	/// them apart, plus the three lock keys and NoCommandKey (set on
	/// keyboard layouts with no dedicated Command key). Delivered by
	/// View's OnMouseDown/OnMouseUp/OnKeyDown/OnKeyUp -- see
	/// <see cref="Modifiers.Current"/> for OnMouseMoved and anywhere else,
	/// and hs_view.h's MODIFIERS note for why OnMouseMoved doesn't get one
	/// of its own the way the other four do.
	/// </summary>
	[Flags]
	public enum ModifierKeys : uint
	{
		None = 0,
		Shift = 0x00000001,
		Command = 0x00000002,
		Control = 0x00000004,
		CapsLock = 0x00000008,
		ScrollLock = 0x00000010,
		NumLock = 0x00000020,
		Option = 0x00000040,
		Menu = 0x00000080,
		LeftShift = 0x00000100,
		RightShift = 0x00000200,
		LeftCommand = 0x00000400,
		RightCommand = 0x00000800,
		LeftControl = 0x00001000,
		RightControl = 0x00002000,
		LeftOption = 0x00004000,
		RightOption = 0x00008000,
		NoCommandKey = 0x00010000,
	}

	/// <summary>
	/// Wraps Haiku's standalone <c>modifiers()</c> global function
	/// (headers/os/interface/InterfaceDefs.h) -- the current modifier-key
	/// state, queryable at any time, not just from inside one of the four
	/// View hooks that get it delivered for free (OnMouseDown/OnMouseUp/
	/// OnKeyDown/OnKeyUp). This is the only way to read modifier state
	/// from <see cref="View.OnMouseMoved"/>, or from anywhere else outside
	/// those four hooks entirely (a timer, <see cref="View.OnDraw"/>,
	/// ...). Safe to call from any thread -- see hs_view.h's own doc
	/// comment on hs_modifiers() for why this needs no locking the way
	/// drawing calls do.
	/// </summary>
	public static class Modifiers
	{
		public static ModifierKeys Current
		{
			get { return (ModifierKeys)Native.hs_modifiers(); }
		}
	}
}
