using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSView (see native/include/hs_view.h
	 * for the full design rationale -- read it before changing anything
	 * here, especially the ownership rule ViewBase's Dispose() enforces).
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
	 *
	 * See ViewBase.cs for what's now shared with Control/Button (handle/
	 * parenting bookkeeping, Frame/MoveTo/ResizeTo, Dispose() itself) --
	 * everything left here is either View-specific (Draw and the mouse/
	 * keyboard hooks, FillRect/StrokeRect/StrokeLine/DrawString,
	 * MakeFocus/IsFocus/Invalidate, Bounds) or the hs_view_create()-based
	 * construction path a Control/Button doesn't use.
	 */
	public class View : ViewBase
	{
		// Kept as instance fields so they aren't GC'd while native code
		// might still call them -- see Haiku.App.Native's comment on this.
		private readonly ViewAttachedToWindowCallback _attachedToWindowThunk;
		private readonly ViewDetachedFromWindowCallback _detachedFromWindowThunk;
		private readonly ViewDrawCallback _drawThunk;
		private readonly ViewDestroyedCallback _destroyedThunk;
		private readonly ViewMouseDownCallback _mouseDownThunk;
		private readonly ViewMouseUpCallback _mouseUpThunk;
		private readonly ViewMouseMovedCallback _mouseMovedThunk;
		private readonly ViewKeyDownCallback _keyDownThunk;
		private readonly ViewKeyUpCallback _keyUpThunk;

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
			: base(CreateNativeView(frame, name, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_attachedToWindowThunk = AttachedToWindowThunk;
			_detachedFromWindowThunk = DetachedFromWindowThunk;
			_drawThunk = DrawThunk;
			_destroyedThunk = DestroyedThunk;
			_mouseDownThunk = MouseDownThunk;
			_mouseUpThunk = MouseUpThunk;
			_mouseMovedThunk = MouseMovedThunk;
			_keyDownThunk = KeyDownThunk;
			_keyUpThunk = KeyUpThunk;

			Native.hs_view_set_attached_to_window_callback(_handle, _attachedToWindowThunk, userData);
			Native.hs_view_set_detached_from_window_callback(_handle, _detachedFromWindowThunk, userData);
			Native.hs_view_set_draw_callback(_handle, _drawThunk, userData);
			Native.hs_view_set_destroyed_callback(_handle, _destroyedThunk, userData);
			Native.hs_view_set_mouse_down_callback(_handle, _mouseDownThunk, userData);
			Native.hs_view_set_mouse_up_callback(_handle, _mouseUpThunk, userData);
			Native.hs_view_set_mouse_moved_callback(_handle, _mouseMovedThunk, userData);
			Native.hs_view_set_key_down_callback(_handle, _keyDownThunk, userData);
			Native.hs_view_set_key_up_callback(_handle, _keyUpThunk, userData);
		}

		// ViewBase's constructor needs an already-created handle (it's
		// the base of the : base(...) chain, so it has to be an
		// expression, not a statement) -- this is that expression,
		// pulled out to its own method purely so the constructor above
		// reads top-to-bottom instead of burying hs_view_create() inline.
		private static IntPtr CreateNativeView(Rect frame, string name,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_view_create(nativeFrame, name,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>Called on the owning window's thread once this view becomes part of a window's hierarchy. Default: does nothing.</summary>
		protected virtual void OnAttachedToWindow() { }

		/// <summary>Called on the owning window's thread when this view stops being part of a window's hierarchy. Default: does nothing.</summary>
		protected virtual void OnDetachedFromWindow() { }

		/// <summary>Called on the owning window's thread whenever the app_server needs (part of) this view redrawn. Default: does nothing (draws nothing).</summary>
		protected virtual void OnDraw(Rect updateRect) { }

		/// <summary>
		/// Called on the owning window's thread when a mouse button is
		/// pressed while the pointer is over this view. <paramref
		/// name="buttons"/> is the full set of buttons down at that instant
		/// (see <see cref="MouseButtons"/>), not just the one that triggered
		/// this event. <paramref name="modifiers"/> is the Shift/Control/
		/// Option/Command/... state at that instant (see
		/// <see cref="ModifierKeys"/>). Default: does nothing.
		/// </summary>
		protected virtual void OnMouseDown(Point where, MouseButtons buttons, ModifierKeys modifiers) { }

		/// <summary>
		/// Called on the owning window's thread when a mouse button is
		/// released while the pointer is over this view. Unlike <see
		/// cref="OnMouseDown"/> and <see cref="OnMouseMoved"/>, there is no
		/// buttons parameter here -- B_MOUSE_UP messages carry no "buttons"
		/// field at all (verified against the Be Book's message-constants
		/// documentation, not assumed; see hs_view.h's MOUSE AND KEYBOARD
		/// INPUT note). It DOES carry a modifiers field though -- the
		/// asymmetry runs the other way for that one (see hs_view.h's
		/// MODIFIERS note), so <paramref name="modifiers"/> is here same as
		/// <see cref="OnMouseDown"/>. Default: does nothing.
		/// </summary>
		protected virtual void OnMouseUp(Point where, ModifierKeys modifiers) { }

		/// <summary>
		/// Called on the owning window's thread whenever the mouse moves
		/// over this view, including entering/exiting it (see <paramref
		/// name="transit"/>). <paramref name="buttons"/> is whatever buttons
		/// are currently down (commonly none, for a plain hover). No
		/// modifiers parameter here -- B_MOUSE_MOVED genuinely carries no
		/// "modifiers" field, unlike OnMouseDown/OnMouseUp/OnKeyDown/
		/// OnKeyUp (see hs_view.h's MODIFIERS note); use
		/// <see cref="Modifiers.Current"/> instead if you need it here.
		/// Default: does nothing.
		/// </summary>
		protected virtual void OnMouseMoved(Point where, MouseTransit transit, MouseButtons buttons) { }

		/// <summary>
		/// Called on the owning window's thread when a key is pressed while
		/// this view <see cref="IsFocus"/>. <paramref name="bytes"/> is the
		/// raw byte sequence Haiku delivered (already copied out of the
		/// native borrowed pointer) -- see <see cref="KeyBytes"/> for the
		/// common single-byte control characters, e.g. Escape/Backspace/the
		/// arrow keys. <paramref name="modifiers"/> is the Shift/Control/
		/// Option/Command/... state at that instant. Default: does nothing.
		/// </summary>
		protected virtual void OnKeyDown(byte[] bytes, ModifierKeys modifiers) { }

		/// <summary>Called on the owning window's thread when a key is released while this view <see cref="IsFocus"/>. Same <paramref name="bytes"/>/<paramref name="modifiers"/> semantics as <see cref="OnKeyDown"/>. Default: does nothing.</summary>
		protected virtual void OnKeyUp(byte[] bytes, ModifierKeys modifiers) { }

		/// <summary>
		/// Adds child to the end of this view's child list. See
		/// hs_view.h's DRAWING note for why there's no `before` parameter
		/// yet. <paramref name="child"/> may be a plain View or a
		/// Control/Button.
		/// </summary>
		public void AddChild(ViewBase child)
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
		public bool RemoveChild(ViewBase child)
		{
			CheckNotConsumed();
			if (child == null)
				throw new ArgumentNullException("child");
			bool removed = Native.hs_view_remove_child(_handle, child._handle);
			if (removed)
				child._hasParent = false;
			return removed;
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

		/// <summary>
		/// Gives this view keyboard focus (or takes it away, if focus is
		/// false) so it starts (or stops) receiving <see cref="OnKeyDown"/>/
		/// <see cref="OnKeyUp"/>. Works regardless of <see cref="ViewFlags"/>
		/// -- B_NAVIGABLE only affects Tab-key auto-cycling between views,
		/// not whether MakeFocus(true) works when called directly (verified
		/// against View.h, not assumed; see hs_view.h's MOUSE AND KEYBOARD
		/// INPUT note). Only meaningful once this view is attached to a
		/// window.
		/// </summary>
		public void MakeFocus(bool focus = true)
		{
			CheckNotConsumed();
			Native.hs_view_make_focus(_handle, focus);
		}

		/// <summary>Whether this view currently has keyboard focus. See <see cref="MakeFocus"/>.</summary>
		public bool IsFocus
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_view_is_focus(_handle);
			}
		}

		/// <summary>
		/// Asks the app_server to redraw this view's entire bounds, causing
		/// <see cref="OnDraw"/> to fire again soon (on the owning window's
		/// thread, per the usual rule) -- not a synchronous, wrong on the
		/// spot repaint. Not part of the BeAPI's own minimal View slice, but
		/// added here so a mouse/keyboard-driven view (this input slice's
		/// whole point) has some way to make what it drew last time stale.
		/// </summary>
		public void Invalidate()
		{
			CheckNotConsumed();
			Native.hs_view_invalidate(_handle);
		}

		private static HsRect ToHsRect(Rect rect)
		{
			return new HsRect { Left = rect.Left, Top = rect.Top, Right = rect.Right, Bottom = rect.Bottom };
		}

		private static HsPoint ToHsPoint(Point point)
		{
			return new HsPoint { X = point.X, Y = point.Y };
		}

		private static Point FromHsPoint(HsPoint point)
		{
			return new Point(point.X, point.Y);
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

		private static void MouseDownThunk(IntPtr userData, HsPoint where, uint buttons, uint modifiers)
		{
			FromUserData(userData).OnMouseDown(FromHsPoint(where), (MouseButtons)buttons, (ModifierKeys)modifiers);
		}

		private static void MouseUpThunk(IntPtr userData, HsPoint where, uint modifiers)
		{
			FromUserData(userData).OnMouseUp(FromHsPoint(where), (ModifierKeys)modifiers);
		}

		private static void MouseMovedThunk(IntPtr userData, HsPoint where, uint transit, uint buttons)
		{
			FromUserData(userData).OnMouseMoved(FromHsPoint(where), (MouseTransit)transit, (MouseButtons)buttons);
		}

		// bytes is a borrowed pointer (see hs_view.h's MOUSE AND KEYBOARD
		// INPUT note) -- copied into a managed byte[] immediately via
		// Marshal.Copy, same borrowed-pointer-copy-immediately convention
		// used elsewhere in this binding (e.g. Message.FindString).
		private static void KeyDownThunk(IntPtr userData, IntPtr bytes, int numBytes, uint modifiers)
		{
			FromUserData(userData).OnKeyDown(CopyBytes(bytes, numBytes), (ModifierKeys)modifiers);
		}

		private static void KeyUpThunk(IntPtr userData, IntPtr bytes, int numBytes, uint modifiers)
		{
			FromUserData(userData).OnKeyUp(CopyBytes(bytes, numBytes), (ModifierKeys)modifiers);
		}

		private static byte[] CopyBytes(IntPtr source, int length)
		{
			byte[] result = new byte[length];
			if (length > 0)
				Marshal.Copy(source, result, 0, length);
			return result;
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as Window.cs's own
			// DestroyedThunk -- see ViewBase.MarkDestroyed()'s comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_view_destroy(handle);
		}
	}
}
