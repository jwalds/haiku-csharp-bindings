namespace Haiku.Interface
{
	/// <summary>
	/// Mirrors BColorControl's own <c>color_control_layout</c> enum
	/// (headers/os/interface/ColorControl.h) -- plain, non-sequential
	/// values (4/8/16/32/64, each the number of columns in that layout's
	/// grid of color cells), verified against the actual installed
	/// ColorControl.h, not assumed to be sequential the way
	/// <see cref="ThumbStyle"/>'s or <see cref="SliderOrientation"/>'s
	/// values are.
	/// </summary>
	public enum ColorControlLayout
	{
		/// <summary>A 4-column by 64-row grid.</summary>
		Cells4x64 = 4,

		/// <summary>An 8-column by 32-row grid.</summary>
		Cells8x32 = 8,

		/// <summary>A 16-column by 16-row grid.</summary>
		Cells16x16 = 16,

		/// <summary>A 32-column by 8-row grid -- the layout this binding's own probes and Sample.exe's DemoColorControl use.</summary>
		Cells32x8 = 32,

		/// <summary>A 64-column by 4-row grid.</summary>
		Cells64x4 = 64,
	}
}
