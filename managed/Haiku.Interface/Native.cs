using System;
using System.Runtime.InteropServices;

namespace Haiku.Interface
{
	/* Delegate shapes matching hs_window.h's callback typedefs exactly.
	 * Declared here at namespace level (not nested in Native), matching
	 * Haiku.App.Native's own convention -- that project already got bitten
	 * once by a delegate nested inside a static class colliding with a
	 * same-named type elsewhere (see the top-level README). Prefixed with
	 * "Window" so a future BView (or anything else in this namespace) can
	 * have its own MessageReceived-shaped delegate without a name clash. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void WindowMessageReceivedCallback(IntPtr userData, IntPtr message);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate int WindowQuitRequestedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void WindowDestroyedCallback(IntPtr userData);

	/* Mirrors hs_rect (see native/include/hs_types.h) exactly. Haiku.App.Native
	 * already has its own internal HsRect for the same purpose, but that one
	 * is `internal` to the Haiku.App assembly and invisible here. Rather than
	 * reach for InternalsVisibleTo to share a six-line plain-data struct
	 * (whose field layout can only ever change if hs_types.h itself does),
	 * this duplicates it locally -- simpler than taking on an assembly-
	 * linking dependency for something this small. (Message, in Window.cs's
	 * borrowed-handle case below, is the one place in this kit where that
	 * tradeoff comes out the other way -- see Haiku.App's AssemblyInfo.cs.) */
	[StructLayout(LayoutKind.Sequential)]
	internal struct HsRect
	{
		internal float Left;
		internal float Top;
		internal float Right;
		internal float Bottom;
	}

	/* Raw P/Invoke surface for hs_window.h. Nothing here is meant to be
	 * called directly by binding consumers -- Window.cs wraps every one of
	 * these in a safe, idiomatic method/property. See Haiku.App.Native's own
	 * header comment for why every DllImport for a kit lives in one file. */
	internal static class Native
	{
		private const string Lib = "haikusharp";

		[DllImport(Lib)]
		internal static extern IntPtr hs_window_create(HsRect frame, string title,
			int look, int feel, uint flags);

		[DllImport(Lib)]
		internal static extern void hs_window_destroy(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_set_message_received_callback(IntPtr window,
			WindowMessageReceivedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_set_quit_requested_callback(IntPtr window,
			WindowQuitRequestedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_set_destroyed_callback(IntPtr window,
			WindowDestroyedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_window_show(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_hide(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_is_hidden(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_quit(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_lock(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_unlock(IntPtr window);

		[DllImport(Lib)]
		[return: MarshalAs(UnmanagedType.I1)]
		internal static extern bool hs_window_is_locked(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_set_title(IntPtr window, string title);

		/* Returns a pointer into BWindow's own internal storage (its BLooper
		 * name), same borrowed-pointer situation as hs_message_find_string --
		 * declared as IntPtr (not string) so Window.cs can copy it into a
		 * managed string immediately with Marshal.PtrToStringAnsi, rather
		 * than let the marshaler treat it as an owned allocation it should
		 * free (which it is not). */
		[DllImport(Lib)]
		internal static extern IntPtr hs_window_title(IntPtr window);

		[DllImport(Lib)]
		internal static extern void hs_window_get_frame(IntPtr window, out HsRect outFrame);

		[DllImport(Lib)]
		internal static extern void hs_window_move_to(IntPtr window, float x, float y);

		[DllImport(Lib)]
		internal static extern void hs_window_resize_to(IntPtr window, float width, float height);
	}
}
