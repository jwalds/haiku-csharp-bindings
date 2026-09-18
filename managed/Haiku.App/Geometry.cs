using System;

namespace Haiku.App
{
	/// <summary>
	/// Mirrors BPoint's two public floats (see headers/os/interface/Point.h
	/// upstream). This exists only so BMessage's Add/FindPoint (see
	/// Message.cs) have something better than four loose floats to hand
	/// back -- it is a deliberately minimal placeholder for a real BPoint
	/// wrapper, which belongs to the Interface Kit once that kit starts.
	/// When it does, that wrapper should absorb this role rather than
	/// duplicate it.
	/// </summary>
	public struct Point : IEquatable<Point>
	{
		public float X;
		public float Y;

		public Point(float x, float y)
		{
			X = x;
			Y = y;
		}

		public bool Equals(Point other)
		{
			return X == other.X && Y == other.Y;
		}

		public override bool Equals(object obj)
		{
			return obj is Point && Equals((Point)obj);
		}

		public override int GetHashCode()
		{
			return X.GetHashCode() ^ Y.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + X + ", " + Y + ")";
		}

		public static bool operator ==(Point a, Point b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Point a, Point b)
		{
			return !a.Equals(b);
		}
	}

	/// <summary>
	/// Mirrors BRect's four public floats (see headers/os/interface/Rect.h
	/// upstream). Same placeholder rationale as <see cref="Point"/> above.
	/// </summary>
	public struct Rect : IEquatable<Rect>
	{
		public float Left;
		public float Top;
		public float Right;
		public float Bottom;

		public Rect(float left, float top, float right, float bottom)
		{
			Left = left;
			Top = top;
			Right = right;
			Bottom = bottom;
		}

		public bool Equals(Rect other)
		{
			return Left == other.Left && Top == other.Top
				&& Right == other.Right && Bottom == other.Bottom;
		}

		public override bool Equals(object obj)
		{
			return obj is Rect && Equals((Rect)obj);
		}

		public override int GetHashCode()
		{
			return Left.GetHashCode() ^ Top.GetHashCode() ^ Right.GetHashCode() ^ Bottom.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + Left + ", " + Top + ", " + Right + ", " + Bottom + ")";
		}

		public static bool operator ==(Rect a, Rect b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Rect a, Rect b)
		{
			return !a.Equals(b);
		}
	}

	/// <summary>
	/// Mirrors BSize's two public floats (see headers/os/interface/Size.h
	/// upstream). Same placeholder rationale as <see cref="Point"/>.
	/// </summary>
	public struct Size : IEquatable<Size>
	{
		public float Width;
		public float Height;

		public Size(float width, float height)
		{
			Width = width;
			Height = height;
		}

		public bool Equals(Size other)
		{
			return Width == other.Width && Height == other.Height;
		}

		public override bool Equals(object obj)
		{
			return obj is Size && Equals((Size)obj);
		}

		public override int GetHashCode()
		{
			return Width.GetHashCode() ^ Height.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + Width + ", " + Height + ")";
		}

		public static bool operator ==(Size a, Size b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Size a, Size b)
		{
			return !a.Equals(b);
		}
	}

	/// <summary>
	/// Mirrors rgb_color's four public bytes (see headers/os/interface/
	/// GraphicsDefs.h upstream). Named RgbColor rather than Color to keep
	/// it unambiguous next to System.Drawing.Color, should this project
	/// ever end up referencing that assembly too.
	/// </summary>
	public struct RgbColor : IEquatable<RgbColor>
	{
		public byte Red;
		public byte Green;
		public byte Blue;
		public byte Alpha;

		public RgbColor(byte red, byte green, byte blue, byte alpha)
		{
			Red = red;
			Green = green;
			Blue = blue;
			Alpha = alpha;
		}

		public RgbColor(byte red, byte green, byte blue)
		{
			Red = red;
			Green = green;
			Blue = blue;
			Alpha = 255;
		}

		public bool Equals(RgbColor other)
		{
			return Red == other.Red && Green == other.Green
				&& Blue == other.Blue && Alpha == other.Alpha;
		}

		public override bool Equals(object obj)
		{
			return obj is RgbColor && Equals((RgbColor)obj);
		}

		public override int GetHashCode()
		{
			return Red.GetHashCode() ^ Green.GetHashCode() ^ Blue.GetHashCode() ^ Alpha.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + Red + ", " + Green + ", " + Blue + ", " + Alpha + ")";
		}

		public static bool operator ==(RgbColor a, RgbColor b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(RgbColor a, RgbColor b)
		{
			return !a.Equals(b);
		}
	}

	/// <summary>
	/// Mirrors Haiku's `alignment` enum (headers/os/interface/
	/// InterfaceDefs.h) exactly, numeric values included -- these cross
	/// the P/Invoke boundary as plain int32s inside HsAlignment (see
	/// Native.cs), so the values here must stay in sync with Haiku's own.
	/// </summary>
	public enum HorizontalAlignment
	{
		Left = 0,
		Right = 1,
		Center = 2,
		Unset = -1,
		UseFullWidth = -2
	}

	/// <summary>
	/// Mirrors Haiku's `vertical_alignment` enum (headers/os/interface/
	/// InterfaceDefs.h) exactly, numeric values included -- see the note
	/// on <see cref="HorizontalAlignment"/>.
	/// </summary>
	public enum VerticalAlignment
	{
		Top = 0x10,
		Middle = 0x20,
		Bottom = 0x30,
		Unset = -1,
		UseFullHeight = -2
	}

	/// <summary>
	/// Mirrors BAlignment's two enum fields (see headers/os/interface/
	/// Alignment.h upstream). Same placeholder rationale as
	/// <see cref="Point"/>.
	/// </summary>
	public struct Alignment : IEquatable<Alignment>
	{
		public HorizontalAlignment Horizontal;
		public VerticalAlignment Vertical;

		public Alignment(HorizontalAlignment horizontal, VerticalAlignment vertical)
		{
			Horizontal = horizontal;
			Vertical = vertical;
		}

		public bool Equals(Alignment other)
		{
			return Horizontal == other.Horizontal && Vertical == other.Vertical;
		}

		public override bool Equals(object obj)
		{
			return obj is Alignment && Equals((Alignment)obj);
		}

		public override int GetHashCode()
		{
			return Horizontal.GetHashCode() ^ Vertical.GetHashCode();
		}

		public override string ToString()
		{
			return "(" + Horizontal + ", " + Vertical + ")";
		}

		public static bool operator ==(Alignment a, Alignment b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(Alignment a, Alignment b)
		{
			return !a.Equals(b);
		}
	}
}
