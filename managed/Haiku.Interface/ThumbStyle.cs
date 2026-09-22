namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors BSlider's own <c>thumb_style</c> enum (headers/os/interface/
	/// Slider.h) -- a plain sequential C++ enum (B_BLOCK_THUMB=0,
	/// B_TRIANGLE_THUMB=1 by ordinary declaration-order semantics), values
	/// verified against the actual installed Slider.h, not assumed.
	/// Deliberately NOT a [Flags] enum -- these are mutually exclusive
	/// thumb shapes, same spirit as <see cref="ButtonBehavior"/>.
	/// </summary>
	public enum ThumbStyle
	{
		/// <summary>The default: a solid rectangular block thumb.</summary>
		Block = 0,

		/// <summary>A triangular pointer/arrow-shaped thumb.</summary>
		Triangle = 1,
	}
}
