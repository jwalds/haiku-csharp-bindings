namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors the real BeAPI <c>orientation</c> enum (headers/os/interface/
	/// InterfaceDefs.h) -- a plain sequential C++ enum (B_HORIZONTAL=0,
	/// B_VERTICAL=1 by ordinary declaration-order semantics), values
	/// verified against the actual installed InterfaceDefs.h, not assumed.
	/// Used by <see cref="Slider"/> only, for now -- real BeAPI reuses this
	/// same enum for other layout-related APIs this binding doesn't wrap
	/// yet.
	/// </summary>
	public enum SliderOrientation
	{
		/// <summary>The default: the thumb moves left/right, the bar runs horizontally.</summary>
		Horizontal = 0,

		/// <summary>The thumb moves up/down, the bar runs vertically.</summary>
		Vertical = 1,
	}
}
