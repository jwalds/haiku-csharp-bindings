namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors real BeAPI's <c>border_style</c> enum (headers/os/interface/
	/// InterfaceDefs.h) exactly -- read directly from the real installed
	/// header, not assumed (see hs_scroll_view.h's BORDER_STYLE note).
	/// </summary>
	public enum ScrollViewBorder
	{
		/// <summary>B_PLAIN_BORDER.</summary>
		Plain = 0,

		/// <summary>B_FANCY_BORDER -- BScrollView's own default.</summary>
		Fancy = 1,

		/// <summary>B_NO_BORDER.</summary>
		None = 2,
	}
}
