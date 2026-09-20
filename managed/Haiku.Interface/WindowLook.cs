namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's <c>window_look</c> enum (headers/os/interface/Window.h)
	/// exactly, verified against that header directly -- these values control
	/// how much of a window's border/title-tab chrome the app_server draws.
	/// </summary>
	public enum WindowLook
	{
		Bordered = 20,
		NoBorder = 19,
		Titled = 1,
		Document = 11,
		Modal = 3,
		Floating = 7,
	}
}
