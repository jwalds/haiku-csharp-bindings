namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors BSlider's own <c>hash_mark_location</c> enum (headers/os/
	/// interface/Slider.h), values verified against the actual installed
	/// header, not assumed. Deliberately NOT a [Flags] enum despite looking
	/// bitmask-shaped (<see cref="Both"/> == 3 == Top|Bottom) -- read
	/// carefully, this is a genuinely odd enum: <c>B_HASH_MARKS_TOP</c> and
	/// <c>B_HASH_MARKS_LEFT</c> are literally the same underlying value (1),
	/// and <c>B_HASH_MARKS_BOTTOM</c>/<c>B_HASH_MARKS_RIGHT</c> are both 2 --
	/// real BeAPI reuses the same two numbers for a horizontal slider's
	/// "above/below the bar" and a vertical slider's "left/right of the
	/// bar" depending on the slider's own <see cref="SliderOrientation"/>,
	/// rather than defining four independent locations. This C# enum
	/// mirrors that exactly (two pairs of same-valued members, which C#
	/// permits) rather than picking just one name per value, so
	/// <c>HashMarkLocation.Top</c> and <c>HashMarkLocation.Left</c> compare
	/// equal and either one round-trips through a vertical or horizontal
	/// slider correctly -- confirmed on hardware: setting
	/// <c>B_HASH_MARKS_TOP</c> and reading back gives the identical raw
	/// value <c>B_HASH_MARKS_LEFT</c> would.
	/// </summary>
	public enum HashMarkLocation
	{
		/// <summary>The default: no hash marks drawn at all.</summary>
		None = 0,

		/// <summary>Above the bar, for a horizontal slider.</summary>
		Top = 1,

		/// <summary>
		/// Left of the bar, for a vertical slider -- the exact same
		/// underlying value as <see cref="Top"/>; see this enum's own
		/// remarks above for why.
		/// </summary>
		Left = 1,

		/// <summary>Below the bar, for a horizontal slider.</summary>
		Bottom = 2,

		/// <summary>
		/// Right of the bar, for a vertical slider -- the exact same
		/// underlying value as <see cref="Bottom"/>; see this enum's own
		/// remarks above for why.
		/// </summary>
		Right = 2,

		/// <summary>Both sides -- equivalent to Top|Bottom or Left|Right.</summary>
		Both = 3,
	}
}
