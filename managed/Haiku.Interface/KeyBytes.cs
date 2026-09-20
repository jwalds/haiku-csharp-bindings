namespace Haiku.Interface
{
	/// <summary>
	/// Control-character byte constants for View's OnKeyDown/OnKeyUp hooks,
	/// copied verbatim from headers/os/interface/InterfaceDefs.h (not
	/// hand-computed). Compare a single-byte key press against these,
	/// e.g. <c>if (bytes.Length == 1 &amp;&amp; bytes[0] == KeyBytes.Escape)</c>.
	/// This deliberately does NOT cover function keys (F1-F12) -- those
	/// aren't identifiable from the bytes/numBytes pair alone and need the
	/// raw "key"/"states" message fields, which are out of scope for this
	/// basic-hooks input slice (see native/include/hs_view.h's MOUSE AND
	/// KEYBOARD INPUT note).
	/// </summary>
	public static class KeyBytes
	{
		public const byte Home = 0x01;
		public const byte End = 0x04;
		public const byte Insert = 0x05;
		public const byte Backspace = 0x08;
		public const byte Tab = 0x09;
		public const byte Return = 0x0a;
		public const byte Enter = 0x0a;
		public const byte PageUp = 0x0b;
		public const byte PageDown = 0x0c;
		public const byte FunctionKey = 0x10;
		public const byte Escape = 0x1b;
		public const byte LeftArrow = 0x1c;
		public const byte RightArrow = 0x1d;
		public const byte UpArrow = 0x1e;
		public const byte DownArrow = 0x1f;
		public const byte Space = 0x20;
		public const byte Delete = 0x7f;
	}
}
