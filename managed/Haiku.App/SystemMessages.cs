namespace Haiku.App
{
	/// <summary>
	/// A few of Haiku's own "what" constants from AppDefs.h, packed the same
	/// way Haiku's C++ headers do (a 4-character literal treated as a big-
	/// endian uint32 -- e.g. '_QRQ' becomes 0x5F515251). Posting
	/// SystemMessages.QuitRequested to your own Application is the normal,
	/// supported way to end it from within a message handler: it goes
	/// through the exact same OnQuitRequested() path as if the user had
	/// asked to quit, rather than trying to tear the app down directly.
	/// </summary>
	public static class SystemMessages
	{
		public const uint QuitRequested = 0x5F515251; // '_QRQ', AppDefs.h
		public const uint ReadyToRun = 0x5F525452;     // '_RTR', AppDefs.h
	}
}
