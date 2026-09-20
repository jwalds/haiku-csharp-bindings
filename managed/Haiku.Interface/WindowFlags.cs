using System;

namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's window flags bitmask (headers/os/interface/Window.h,
	/// the B_NOT_MOVABLE/B_QUIT_ON_WINDOW_CLOSE/... constants), every value
	/// verified against that header directly. Combine with bitwise-or, e.g.
	/// <c>WindowFlags.QuitOnWindowClose | WindowFlags.NotResizable</c>.
	/// </summary>
	[Flags]
	public enum WindowFlags : uint
	{
		None = 0,
		NotMovable = 0x00000001,
		NotResizable = 0x00000002,
		NotHResizable = 0x00000004,
		NotVResizable = 0x00000008,
		WillAcceptFirstClick = 0x00000010,
		NotClosable = 0x00000020,
		NotZoomable = 0x00000040,
		AvoidFront = 0x00000080,
		NoWorkspaceActivation = 0x00000100,
		NoServerSideWindowModifiers = 0x00000200,
		OutlineResize = 0x00001000,
		AvoidFocus = 0x00002000,
		NotMinimizable = 0x00004000,
		NotAnchoredOnActivate = 0x00020000,
		AsynchronousControls = 0x00080000,
		QuitOnWindowClose = 0x00100000,
		SamePositionInAllWorkspaces = 0x00200000,
		AutoUpdateSizeLimits = 0x00400000,
		CloseOnEscape = 0x00800000,
	}
}
