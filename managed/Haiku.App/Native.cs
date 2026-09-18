using System;
using System.Runtime.InteropServices;

namespace Haiku.App
{
	/* Delegate shapes matching hs_application.h's callback typedefs exactly.
	 * Declared here (not nested in Native) so Application.cs can use them
	 * as field types without a forward reference. QuitRequestedCallback
	 * returns plain int (1/0), not bool -- marshaling a bool return value
	 * out of a native callback is one more place to get subtly wrong than
	 * marshaling a bool parameter, and there's no reason to take that risk
	 * when a plain int says exactly the same thing. */
	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void MessageReceivedCallback(IntPtr userData, IntPtr message);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate int QuitRequestedCallback(IntPtr userData);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	internal delegate void ReadyToRunCallback(IntPtr userData);

	/* Mirrors hs_point/hs_rect (see hs_types.h and hs_message.h) exactly --
	 * Sequential layout with no padding is what lets these cross the
	 * P/Invoke boundary by value as plain structs. Not meant to be used
	 * directly by binding consumers; Message.cs converts to/from the
	 * public Point/Rect types in Geometry.cs. */
	[StructLayout(LayoutKind.Sequential)]
	internal struct HsPoint
	{
		internal float X;
		internal float Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct HsRect
	{
		internal float Left;
		internal float Top;
		internal float Right;
		internal float Bottom;
	}

	/* Raw P/Invoke surface. Nothing here is meant to be called directly by
	 * binding consumers -- Message and Application wrap every one of these
	 * in a safe, idiomatic method. Keeping every DllImport in one file
	 * makes it easy to audit this binding's entire native surface area at
	 * a glance, which matters a lot for a hand-written shim: this file IS
	 * the contract between the C# and C++ halves of the project.
	 *
	 * "haikusharp" resolves to libhaikusharp.so via Mono's normal native
	 * library search (LD_LIBRARY_PATH, then standard system lib paths) --
	 * see the top-level README for how to point at it during development. */
	internal static class Native
	{
		private const string Lib = "haikusharp";

		// -- hs_message.h --

		[DllImport(Lib)]
		internal static extern IntPtr hs_message_create(uint what);

		[DllImport(Lib)]
		internal static extern void hs_message_destroy(IntPtr message);

		[DllImport(Lib)]
		internal static extern uint hs_message_what(IntPtr message);

		[DllImport(Lib)]
		internal static extern void hs_message_set_what(IntPtr message, uint what);

		[DllImport(Lib)]
		internal static extern int hs_message_add_int32(IntPtr message, string name, int value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_int32(IntPtr message, string name, out int outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_bool(IntPtr message, string name,
			[MarshalAs(UnmanagedType.I1)] bool value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_bool(IntPtr message, string name,
			[MarshalAs(UnmanagedType.I1)] out bool outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_int8(IntPtr message, string name, sbyte value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_int8(IntPtr message, string name, out sbyte outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_int16(IntPtr message, string name, short value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_int16(IntPtr message, string name, out short outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_int64(IntPtr message, string name, long value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_int64(IntPtr message, string name, out long outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_float(IntPtr message, string name, float value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_float(IntPtr message, string name, out float outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_double(IntPtr message, string name, double value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_double(IntPtr message, string name, out double outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_point(IntPtr message, string name, HsPoint point);

		[DllImport(Lib)]
		internal static extern int hs_message_find_point(IntPtr message, string name, out HsPoint outPoint);

		[DllImport(Lib)]
		internal static extern int hs_message_add_rect(IntPtr message, string name, HsRect rect);

		[DllImport(Lib)]
		internal static extern int hs_message_find_rect(IntPtr message, string name, out HsRect outRect);

		[DllImport(Lib)]
		internal static extern int hs_message_add_pointer(IntPtr message, string name, IntPtr value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_pointer(IntPtr message, string name, out IntPtr outValue);

		[DllImport(Lib)]
		internal static extern int hs_message_add_string(IntPtr message, string name, string value);

		[DllImport(Lib)]
		internal static extern int hs_message_find_string(IntPtr message, string name, out IntPtr outValue);

		// -- hs_application.h --

		[DllImport(Lib)]
		internal static extern IntPtr hs_application_create(string signature, out int outError);

		[DllImport(Lib)]
		internal static extern void hs_application_destroy(IntPtr app);

		[DllImport(Lib)]
		internal static extern void hs_application_set_message_received_callback(
			IntPtr app, MessageReceivedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_application_set_quit_requested_callback(
			IntPtr app, QuitRequestedCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern void hs_application_set_ready_to_run_callback(
			IntPtr app, ReadyToRunCallback callback, IntPtr userData);

		[DllImport(Lib)]
		internal static extern int hs_application_run_and_wait(IntPtr app);

		[DllImport(Lib)]
		internal static extern int hs_application_post_message(IntPtr app, IntPtr message);

		[DllImport(Lib)]
		internal static extern int hs_application_post_message_what(IntPtr app, uint what);
	}
}
