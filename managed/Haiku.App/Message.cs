using System;
using System.Runtime.InteropServices;

namespace Haiku.App
{
	/* Wraps a native BMessage. See hs_message.h for the ownership rule this
	 * class enforces: a Message you construct yourself owns its native
	 * BMessage and must be Dispose()d (or used in a `using` block); a
	 * Message handed to your Application.OnMessageReceived override is
	 * BORROWED from the looper's message queue and must NOT be destroyed --
	 * the internal constructor below is how Application creates that
	 * non-owning wrapper. */
	public sealed class Message : IDisposable
	{
		private IntPtr _handle;
		private readonly bool _owns;

		public Message(uint what)
		{
			_handle = Native.hs_message_create(what);
			_owns = true;
		}

		internal Message(IntPtr borrowedHandle)
		{
			_handle = borrowedHandle;
			_owns = false;
		}

		internal IntPtr Handle { get { return _handle; } }

		public uint What
		{
			get { return Native.hs_message_what(_handle); }
			set { Native.hs_message_set_what(_handle, value); }
		}

		public void AddInt32(string name, int value)
		{
			Check(Native.hs_message_add_int32(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as an int32.</summary>
		public int? FindInt32(string name)
		{
			int value;
			return Native.hs_message_find_int32(_handle, name, out value) == 0 ? (int?)value : null;
		}

		public void AddBool(string name, bool value)
		{
			Check(Native.hs_message_add_bool(_handle, name, value));
		}

		public bool? FindBool(string name)
		{
			bool value;
			return Native.hs_message_find_bool(_handle, name, out value) == 0 ? (bool?)value : null;
		}

		public void AddString(string name, string value)
		{
			Check(Native.hs_message_add_string(_handle, name, value));
		}

		/// <summary>
		/// Returns null if <paramref name="name"/> isn't present. The native BMessage
		/// hands back a pointer into its own internal storage (see hs_message.h) --
		/// this copies it into a managed string immediately, so the result stays
		/// valid even after the message is later mutated or destroyed.
		/// </summary>
		public string FindString(string name)
		{
			IntPtr ptr;
			if (Native.hs_message_find_string(_handle, name, out ptr) != 0 || ptr == IntPtr.Zero)
				return null;
			return Marshal.PtrToStringAnsi(ptr);
		}

		private static void Check(int status)
		{
			if (status != 0)
				throw new HaikuException(status);
		}

		public void Dispose()
		{
			if (_owns && _handle != IntPtr.Zero) {
				Native.hs_message_destroy(_handle);
				_handle = IntPtr.Zero;
			}
		}
	}
}
