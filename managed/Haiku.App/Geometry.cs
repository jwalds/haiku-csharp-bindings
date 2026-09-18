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
}
