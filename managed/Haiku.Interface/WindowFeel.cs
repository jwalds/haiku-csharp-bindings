namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors Haiku's <c>window_feel</c> enum (headers/os/interface/Window.h)
	/// exactly, verified against that header directly -- these values control
	/// a window's stacking/activation relationship to the rest of the app
	/// and system (normal, modal, floating, and which windows they attach to).
	/// </summary>
	public enum WindowFeel
	{
		Normal = 0,
		ModalSubset = 2,
		ModalApp = 1,
		ModalAll = 3,
		FloatingSubset = 5,
		FloatingApp = 4,
		FloatingAll = 6,
	}
}
