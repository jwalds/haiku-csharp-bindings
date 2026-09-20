using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSView (see native/include/hs_view.h
	 * for the full design rationale -- read it before changing anything
	 * here, especially the ownership rule this class's Dispose() enforces,
	 * which is stricter here than Window.cs's own).
	 *
	 * LIFECYCLE, IN ONE PARAGRAPH: a freshly-constructed View has no parent
	 * and is safe to configure (SetHighColor, MoveTo, ...) from whatever
	 * thread created it. Once added to a window (directly via
	 * Window.AddChild, or indirectly by being added to a View that's
	 * itself already in a window), OnAttachedToWindow/OnDraw/
	 * OnDetachedFromWindow fire on the OWNING WINDOW's own thread -- a
	 * View never has a thread of its own (see hs_view.h's threading note).
	 * The most common real usage never calls Dispose() on a child View at
	 * all: once added, its parent owns it and deletes it automatically,
	 * recursively, whenever that parent itself is destroyed -- OnDestroyed
	 * still fires when that happens, just later and from whatever thread
	 * is tearing the parent down. Dispose() is for a View you created but
	 * never added anywhere, or one you've since RemoveChild()'d.
	 */
	public class View : IDisposable
	{
		// Both internal (not private) so Window.AddChild(View)/RemoveChild(View)
		// can pass this view's native handle to hs_window_add_child/
		// hs_window_remove_child and toggle _hasParent -- same assembly,
		// same namespace, and Window needs exactly the same two things
		// AddChild/RemoveChild above already touch on View itself.
		internal IntPtr _handle;
		private GCHandle _selfHandle;
		internal bool _hasParent;

		// Kept as instance fields so they aren't GC'd while native code
		// might still call them -- see Haiku.App.Native's comment on this.
		private readonly ViewAttachedToWindowCallback _attachedToWindowThunk;
		private readonly ViewDetachedFromWindowCallback _detachedFromWindowThunk;
		private readonly ViewDrawCallback _drawThunk;
		private readonly ViewDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a plain view you're
		/// going to draw into. Includes <see cref="ViewFlags.WillDraw"/>
		/// by default -- without it, the app_server has no obligation to
		/// ever call <see cref="OnDraw"/> at all.
		/// </summary>
		public View(Rect frame, string name)
			: this(frame, name, ViewResizingMode.None, ViewFlags.WillDraw)
		{
		}

		public View(Rect frame, string name, ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			_handle = Native.hs_view_create(nativeFrame, name,
				(uint)resizingMode, (uint)flags);

			// A NORMAL (not weak) GCHandle -- same rationale as Window's
			// own _selfHandle. Freed the instant OnDestroyed fires (see
			// DestroyedThunk below).
			_selfHandle = GCHandle.Alloc(this);
			IntPtr userData = GCHandle.ToIntPtr(_selfHandle);

			_attachedToWindowThunk = AttachedToWindowThunk;
			_detachedFromWindowThunk = DetachedFromWindowThunk;
			_drawThunk = DrawThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_view_set_attached_to_window_callback(_handle, _attachedToWindowThunk, userData);
			Native.hs_view_set_detached_from_window_callback(_handle, _detachedFromWindowThunk, userData);
			Native.hs_view_set_draw_callback(_handle, _drawThunk, userData);
			Native.hs_view_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		/// <summary>Called on the owning window's thread once this view becomes part of a window's hierarchy. Default: does nothing.</summary>
		protected virtual void OnAttachedToWindow() { }

		/// <summary>Called on the owning window's thread when this view stops being part of a window's hierarchy. Default: does nothing.</summary>
		protected virtual void OnDetachedFromWindow() { }

		/// <summary>Called on the owning window's thread whenever the app_server needs (part of) this view redrawn. Default: does nothing (draws nothing).</summary>
		protected virtual void OnDraw(Rect updateRect) { }

		/// <summary>
		/// Called exactly once, right as the native view object is being
		/// deleted -- from HSView's own C++ destructor, so it covers an
		/// explicit Dispose(), and the implicit cascade when a still-
		/// attached parent is destroyed first. By the time this runs, no
		/// other method on this instance is safe to call. Default: does
		/// nothing.
		/// </summary>
		protected virtual void OnDestroyed() { }

		/// <summary>
		/// Adds child to the end of this view's child list. See
		/// hs_view.h's DRAWING note for why there's no `before` parameter
		/// yet.
		/// </summary>
		public void AddChild(View child)
		{
			CheckNotConsumed();
			if (child == null)
				throw new ArgumentNullException("child");
			Native.hs_view_add_child(_handle, child._handle);
			child._hasParent = true;
		}

		/// <summary>
		/// Detaches child from this view's child list without deleting it
		/// -- see hs_view.h's OWNERSHIP note. Returns false if child was
		/// not actually a child of this view.
		/// </summary>
		public bool RemoveChild(View child)
		{
			CheckNotConsumed();
			if (child == null)
				throw new ArgumentNullException("child");
			bool removed = Native.hs_view_remove_child(_handle, child._handle);
			if (removed)
				child._hasParent = false;
			return removed;
		}

		public Rect Frame
		{
			get
			{
				CheckNotConsumed();
				HsRect native;
				Native.hs_view_get_frame(_handle, out native);
				return new Rect(native.Left, native.Top, native.Right, native.Bottom);
			}
		}

		/// <summary>The view's own bounds, in its own coordinate system (always has Left=0, Top=0) -- what <see cref="OnDraw"/>'s updateRect is expressed in.</summary>
		public Rect Bounds
		{
			get
			{
				CheckNotConsumed();
				HsRect native;
				Native.hs_view_get_bounds(_handle, out native);
				return new Rect(native.Left, native.Top, native.Right, native.Bottom);
			}
		}

		public void MoveTo(float x, float y)
		{
			CheckNotConsumed();
			Native.hs_view_move_to(_handle, x, y);
		}

		public void ResizeTo(float width, float height)
		{
			CheckNotConsumed();
			Native.hs_view_resize_to(_handle, width, height);
		}

		public void SetHighColor(RgbColor color)
		{
			CheckNotConsumed();
			Native.hs_view_set_high_color(_handle, color.Red, color.Green, color.Blue, color.Alpha);
		}

		public void SetLowColor(RgbColor color)
		{
			CheckNotConsumed();
			Native.hs_view_set_low_color(_handle, color.Red, color.Green, color.Blue, color.Alpha);
		}

		public void SetViewColor(RgbColor color)
		{
			CheckNotConsumed();
			Native.hs_view_set_view_color(_handle, color.Red, color.Green, color.Blue, color.Alpha);
		}

		/// <summary>Fills rect with the current high color. Only meaningful called from inside <see cref="OnDraw"/> in this slice -- see hs_view.h's DRAWING note.</summary>
		public void FillRect(Rect rect)
		{
			CheckNotConsumed();
			Native.hs_view_fill_rect(_handle, ToHsRect(rect));
		}

		/// <summary>Strokes rect's outline with the current high color. Same restriction as <see cref="FillRect"/>.</summary>
		public void StrokeRect(Rect rect)
		{
			CheckNotConsumed();
			Native.hs_view_stroke_rect(_handle, ToHsRect(rect));
		}

		/// <summary>Strokes a line with the current high color. Same restriction as <see cref="FillRect"/>.</summary>
		public void StrokeLine(Point start, Point end)
		{
			CheckNotConsumed();
			Native.hs_view_stroke_line(_handle, ToHsPoint(start), ToHsPoint(end));
		}

		/// <summary>Draws string at location using the current high color and font. Same restriction as <see cref="FillRect"/>.</summary>
		public void DrawString(string text, Point location)
		{
			CheckNotConsumed();
			Native.hs_view_draw_string(_handle, text, ToHsPoint(location));
		}

		private static HsRect ToHsRect(Rect rect)
		{
			return new HsRect { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
		}

		private static HsPoint ToHsPoint(Point point)
		{
			return new HsPoint { X = point.X, Y = point.Y };
		}

		private void CheckNotConsumed()
		{
			if (_handle == IntPtr.Zero)
				throw new ObjectDisposedException(GetType().Name,
					"This View has been destroyed and can no longer be used.");
		}

		private static View FromUserData(IntPtr userData)
		{
			return (View)GCHandle.FromIntPtr(userData).Target;
		}

		private static void AttachedToWindowThunk(IntPtr userData)
		{
			FromUserData(userData).OnAttachedToWindow();
		}

		private static void DetachedFromWindowThunk(IntPtr userData)
		{
			FromUserData(userData).OnDetachedFromWindow();
		}

		private static void DrawThunk(IntPtr userData, HsRect updateRect)
		{
			View view = FromUserData(userData);
			view.OnDraw(new Rect(updateRect.Left, updateRect.Top, updateRect.Right, updateRect.Bottom));
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			View view = FromUserData(userData);
			// Same "consumed-before-notified" ordering as Window.cs's own
			// DestroyedThunk -- see its comment for why.
			view._handle = IntPtr.Zero;
			view.FreeSelfHandle();
			view.OnDestroyed();
		}

		private void FreeSelfHandle()
		{
			if (_selfHandle.IsAllocated)
				_selfHandle.Free();
		}

		/// <summary>
		/// Destroys this view. ONLY safe if it is not currently attached to
		/// a parent (never added via AddChild, or already RemoveChild()'d)
		/// -- see hs_view.h's OWNERSHIP note. Unlike Window.Dispose(),
		/// there is no "quit and let it happen later" alternative for an
		/// attached View: throws rather than silently corrupting its
		/// parent's child list, since (unlike a shown Window) there is no
		/// native-side rejection of the mistake to fall back on.
		/// </summary>
		public void Dispose()
		{
			if (_handle == IntPtr.Zero)
				return;

			if (_hasParent)
				throw new InvalidOperationException(
					"Cannot Dispose() a View that is still attached to a parent -- "
					+ "RemoveChild() it first (on its parent), or just let its parent's "
					+ "own destruction take care of it if you never needed to detach it.");

			IntPtr handle = _handle;
			_handle = IntPtr.Zero;
			Native.hs_view_destroy(handle);
		}
	}
}
