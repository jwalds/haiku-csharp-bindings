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

		public void AddInt8(string name, sbyte value)
		{
			Check(Native.hs_message_add_int8(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as an int8.</summary>
		public sbyte? FindInt8(string name)
		{
			sbyte value;
			return Native.hs_message_find_int8(_handle, name, out value) == 0 ? (sbyte?)value : null;
		}

		public void AddInt16(string name, short value)
		{
			Check(Native.hs_message_add_int16(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as an int16.</summary>
		public short? FindInt16(string name)
		{
			short value;
			return Native.hs_message_find_int16(_handle, name, out value) == 0 ? (short?)value : null;
		}

		public void AddInt64(string name, long value)
		{
			Check(Native.hs_message_add_int64(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as an int64.</summary>
		public long? FindInt64(string name)
		{
			long value;
			return Native.hs_message_find_int64(_handle, name, out value) == 0 ? (long?)value : null;
		}

		public void AddFloat(string name, float value)
		{
			Check(Native.hs_message_add_float(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a float.</summary>
		public float? FindFloat(string name)
		{
			float value;
			return Native.hs_message_find_float(_handle, name, out value) == 0 ? (float?)value : null;
		}

		public void AddDouble(string name, double value)
		{
			Check(Native.hs_message_add_double(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a double.</summary>
		public double? FindDouble(string name)
		{
			double value;
			return Native.hs_message_find_double(_handle, name, out value) == 0 ? (double?)value : null;
		}

		public void AddPoint(string name, Point point)
		{
			HsPoint native;
			native.X = point.X;
			native.Y = point.Y;
			Check(Native.hs_message_add_point(_handle, name, native));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a point.</summary>
		public Point? FindPoint(string name)
		{
			HsPoint native;
			if (Native.hs_message_find_point(_handle, name, out native) != 0)
				return null;
			return new Point(native.X, native.Y);
		}

		public void AddRect(string name, Rect rect)
		{
			HsRect native;
			native.Left = rect.Left;
			native.Top = rect.Top;
			native.Right = rect.Right;
			native.Bottom = rect.Bottom;
			Check(Native.hs_message_add_rect(_handle, name, native));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a rect.</summary>
		public Rect? FindRect(string name)
		{
			HsRect native;
			if (Native.hs_message_find_rect(_handle, name, out native) != 0)
				return null;
			return new Rect(native.Left, native.Top, native.Right, native.Bottom);
		}

		/// <summary>
		/// Stores an opaque address verbatim -- BMessage never dereferences it or
		/// takes ownership of whatever it points to. Meaningful only for passing
		/// an address (such as another Message's <see cref="Handle"/>, or any
		/// value meaningful only within your own process) through a message.
		/// Never pass the address of managed memory here: the garbage collector
		/// can relocate it, which would leave this pointing at garbage.
		/// </summary>
		public void AddPointer(string name, IntPtr value)
		{
			Check(Native.hs_message_add_pointer(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a pointer.</summary>
		public IntPtr? FindPointer(string name)
		{
			IntPtr value;
			return Native.hs_message_find_pointer(_handle, name, out value) == 0 ? (IntPtr?)value : null;
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

		public void AddUInt8(string name, byte value)
		{
			Check(Native.hs_message_add_uint8(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a uint8.</summary>
		public byte? FindUInt8(string name)
		{
			byte value;
			return Native.hs_message_find_uint8(_handle, name, out value) == 0 ? (byte?)value : null;
		}

		public void AddUInt16(string name, ushort value)
		{
			Check(Native.hs_message_add_uint16(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a uint16.</summary>
		public ushort? FindUInt16(string name)
		{
			ushort value;
			return Native.hs_message_find_uint16(_handle, name, out value) == 0 ? (ushort?)value : null;
		}

		public void AddUInt32(string name, uint value)
		{
			Check(Native.hs_message_add_uint32(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a uint32.</summary>
		public uint? FindUInt32(string name)
		{
			uint value;
			return Native.hs_message_find_uint32(_handle, name, out value) == 0 ? (uint?)value : null;
		}

		public void AddUInt64(string name, ulong value)
		{
			Check(Native.hs_message_add_uint64(_handle, name, value));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a uint64.</summary>
		public ulong? FindUInt64(string name)
		{
			ulong value;
			return Native.hs_message_find_uint64(_handle, name, out value) == 0 ? (ulong?)value : null;
		}

		public void AddSize(string name, Size size)
		{
			HsSize native;
			native.Width = size.Width;
			native.Height = size.Height;
			Check(Native.hs_message_add_size(_handle, name, native));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a size.</summary>
		public Size? FindSize(string name)
		{
			HsSize native;
			if (Native.hs_message_find_size(_handle, name, out native) != 0)
				return null;
			return new Size(native.Width, native.Height);
		}

		public void AddColor(string name, RgbColor color)
		{
			HsRgbColor native;
			native.Red = color.Red;
			native.Green = color.Green;
			native.Blue = color.Blue;
			native.Alpha = color.Alpha;
			Check(Native.hs_message_add_color(_handle, name, native));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as a color.</summary>
		public RgbColor? FindColor(string name)
		{
			HsRgbColor native;
			if (Native.hs_message_find_color(_handle, name, out native) != 0)
				return null;
			return new RgbColor(native.Red, native.Green, native.Blue, native.Alpha);
		}

		public void AddAlignment(string name, Alignment alignment)
		{
			HsAlignment native;
			native.Horizontal = (int)alignment.Horizontal;
			native.Vertical = (int)alignment.Vertical;
			Check(Native.hs_message_add_alignment(_handle, name, native));
		}

		/// <summary>Returns null if <paramref name="name"/> isn't present as an alignment.</summary>
		public Alignment? FindAlignment(string name)
		{
			HsAlignment native;
			if (Native.hs_message_find_alignment(_handle, name, out native) != 0)
				return null;
			return new Alignment((HorizontalAlignment)native.Horizontal, (VerticalAlignment)native.Vertical);
		}

		/// <summary>
		/// Copies <paramref name="value"/> into this message under <paramref name="name"/> --
		/// you still own and must Dispose() <paramref name="value"/> yourself afterward.
		/// </summary>
		public void AddMessage(string name, Message value)
		{
			Check(Native.hs_message_add_message(_handle, name, value.Handle));
		}

		/// <summary>
		/// Returns null if <paramref name="name"/> isn't present as a nested message,
		/// otherwise a new, caller-OWNED Message you must Dispose() (or use in a
		/// `using` block) -- unlike FindString/FindData, this does not borrow anything
		/// from this message's own storage.
		/// </summary>
		public Message FindMessage(string name)
		{
			Message nested = new Message(0);
			if (Native.hs_message_find_message(_handle, name, nested.Handle) != 0) {
				nested.Dispose();
				return null;
			}
			return nested;
		}

		/// <summary>
		/// Generic escape hatch for any Haiku type_code not covered by a named
		/// Add method above (see TypeConstants.h for the standard B_*_TYPE
		/// values). <paramref name="data"/> is copied into the message, so it
		/// need not be kept alive afterward.
		/// </summary>
		public void AddData(string name, uint type, byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException("data");
			Check(Native.hs_message_add_data(_handle, name, type, data, data.Length));
		}

		/// <summary>
		/// Returns null if <paramref name="name"/> isn't present as
		/// <paramref name="type"/>. The native BMessage hands back a pointer into
		/// its own internal storage (see hs_message.h) -- this copies it into a
		/// new managed byte array immediately, so the result stays valid even
		/// after the message is later mutated or destroyed.
		/// </summary>
		public byte[] FindData(string name, uint type)
		{
			IntPtr ptr;
			int numBytes;
			if (Native.hs_message_find_data(_handle, name, type, out ptr, out numBytes) != 0)
				return null;
			byte[] data = new byte[numBytes];
			if (numBytes > 0)
				Marshal.Copy(ptr, data, 0, numBytes);
			return data;
		}

		public bool HasInt8(string name) { return Native.hs_message_has_int8(_handle, name); }
		public bool HasInt16(string name) { return Native.hs_message_has_int16(_handle, name); }
		public bool HasInt32(string name) { return Native.hs_message_has_int32(_handle, name); }
		public bool HasInt64(string name) { return Native.hs_message_has_int64(_handle, name); }
		public bool HasUInt8(string name) { return Native.hs_message_has_uint8(_handle, name); }
		public bool HasUInt16(string name) { return Native.hs_message_has_uint16(_handle, name); }
		public bool HasUInt32(string name) { return Native.hs_message_has_uint32(_handle, name); }
		public bool HasUInt64(string name) { return Native.hs_message_has_uint64(_handle, name); }
		public bool HasBool(string name) { return Native.hs_message_has_bool(_handle, name); }
		public bool HasFloat(string name) { return Native.hs_message_has_float(_handle, name); }
		public bool HasDouble(string name) { return Native.hs_message_has_double(_handle, name); }
		public bool HasString(string name) { return Native.hs_message_has_string(_handle, name); }
		public bool HasPoint(string name) { return Native.hs_message_has_point(_handle, name); }
		public bool HasRect(string name) { return Native.hs_message_has_rect(_handle, name); }
		public bool HasSize(string name) { return Native.hs_message_has_size(_handle, name); }
		public bool HasColor(string name) { return Native.hs_message_has_color(_handle, name); }
		public bool HasAlignment(string name) { return Native.hs_message_has_alignment(_handle, name); }
		public bool HasPointer(string name) { return Native.hs_message_has_pointer(_handle, name); }
		public bool HasMessage(string name) { return Native.hs_message_has_message(_handle, name); }
		public bool HasData(string name, uint type) { return Native.hs_message_has_data(_handle, name, type); }

		public void RemoveName(string name)
		{
			Check(Native.hs_message_remove_name(_handle, name));
		}

		public void RemoveData(string name)
		{
			RemoveData(name, 0);
		}

		public void RemoveData(string name, int index)
		{
			Check(Native.hs_message_remove_data(_handle, name, index));
		}

		public void MakeEmpty()
		{
			Check(Native.hs_message_make_empty(_handle));
		}

		public bool IsEmpty()
		{
			return Native.hs_message_is_empty(_handle);
		}

		/// <summary>Counts how many fields hold a value of the given Haiku type_code.</summary>
		public int CountNames(uint type)
		{
			return Native.hs_message_count_names(_handle, type);
		}

		public void Rename(string oldName, string newName)
		{
			Check(Native.hs_message_rename(_handle, oldName, newName));
		}

		/// <summary>Copies every field from <paramref name="source"/> into this message.</summary>
		public void Append(Message source)
		{
			Check(Native.hs_message_append(_handle, source.Handle));
		}

		public void ReplaceInt8(string name, sbyte value)
		{
			Check(Native.hs_message_replace_int8(_handle, name, value));
		}

		public void ReplaceInt16(string name, short value)
		{
			Check(Native.hs_message_replace_int16(_handle, name, value));
		}

		public void ReplaceInt32(string name, int value)
		{
			Check(Native.hs_message_replace_int32(_handle, name, value));
		}

		public void ReplaceInt64(string name, long value)
		{
			Check(Native.hs_message_replace_int64(_handle, name, value));
		}

		public void ReplaceUInt8(string name, byte value)
		{
			Check(Native.hs_message_replace_uint8(_handle, name, value));
		}

		public void ReplaceUInt16(string name, ushort value)
		{
			Check(Native.hs_message_replace_uint16(_handle, name, value));
		}

		public void ReplaceUInt32(string name, uint value)
		{
			Check(Native.hs_message_replace_uint32(_handle, name, value));
		}

		public void ReplaceUInt64(string name, ulong value)
		{
			Check(Native.hs_message_replace_uint64(_handle, name, value));
		}

		public void ReplaceBool(string name, bool value)
		{
			Check(Native.hs_message_replace_bool(_handle, name, value));
		}

		public void ReplaceFloat(string name, float value)
		{
			Check(Native.hs_message_replace_float(_handle, name, value));
		}

		public void ReplaceDouble(string name, double value)
		{
			Check(Native.hs_message_replace_double(_handle, name, value));
		}

		public void ReplaceString(string name, string value)
		{
			Check(Native.hs_message_replace_string(_handle, name, value));
		}

		public void ReplacePoint(string name, Point point)
		{
			HsPoint native;
			native.X = point.X;
			native.Y = point.Y;
			Check(Native.hs_message_replace_point(_handle, name, native));
		}

		public void ReplaceRect(string name, Rect rect)
		{
			HsRect native;
			native.Left = rect.Left;
			native.Top = rect.Top;
			native.Right = rect.Right;
			native.Bottom = rect.Bottom;
			Check(Native.hs_message_replace_rect(_handle, name, native));
		}

		public void ReplaceSize(string name, Size size)
		{
			HsSize native;
			native.Width = size.Width;
			native.Height = size.Height;
			Check(Native.hs_message_replace_size(_handle, name, native));
		}

		public void ReplaceColor(string name, RgbColor color)
		{
			HsRgbColor native;
			native.Red = color.Red;
			native.Green = color.Green;
			native.Blue = color.Blue;
			native.Alpha = color.Alpha;
			Check(Native.hs_message_replace_color(_handle, name, native));
		}

		public void ReplaceAlignment(string name, Alignment alignment)
		{
			HsAlignment native;
			native.Horizontal = (int)alignment.Horizontal;
			native.Vertical = (int)alignment.Vertical;
			Check(Native.hs_message_replace_alignment(_handle, name, native));
		}

		public void ReplacePointer(string name, IntPtr value)
		{
			Check(Native.hs_message_replace_pointer(_handle, name, value));
		}

		/// <summary>Replaces an existing nested message under <paramref name="name"/> by
		/// copying <paramref name="value"/> in -- you still own <paramref name="value"/>.</summary>
		public void ReplaceMessage(string name, Message value)
		{
			Check(Native.hs_message_replace_message(_handle, name, value.Handle));
		}

		public void ReplaceData(string name, uint type, byte[] data)
		{
			if (data == null)
				throw new ArgumentNullException("data");
			Check(Native.hs_message_replace_data(_handle, name, type, data, data.Length));
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
