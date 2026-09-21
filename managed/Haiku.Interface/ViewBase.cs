using System;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Shared root for View and Control -- the managed mirror of the one
	 * real fact that makes this refactor necessary: BControl (and so
	 * BButton) genuinely IS-A BView in real BeAPI, but has its own native
	 * HSButton object created by its own hs_button_create() rather than
	 * hs_view_create() (see hs_button.h), and its own destroy function
	 * (hs_button_destroy(), NOT hs_view_destroy() -- calling the wrong
	 * one would delete through a mismatched static type, genuine
	 * undefined behavior, not just a style mistake). This class holds
	 * exactly the handle/parenting/geometry/lifecycle surface that is
	 * both safe and meaningful for ANY concrete HSView-family object,
	 * and nothing that is either HSView-specific (Draw/AttachedToWindow/
	 * mouse & key hooks, FillRect/StrokeRect/StrokeLine/DrawString --
	 * View.cs only) or that touches native memory a foreign concrete
	 * type doesn't actually have at that layout (registering any
	 * hs_view_set_*_callback against an HSButton handle would corrupt
	 * whatever field happens to sit at that offset in the real HSButton
	 * object -- View.cs's constructor is the only place those are ever
	 * called, and it always passes its own hs_view_create()'d handle).
	 *
	 * Frame/MoveTo/ResizeTo route through hs_view_get_frame()/
	 * hs_view_move_to()/hs_view_resize_to() even for a Control/Button --
	 * see hs_view.cpp's comments on those three functions for why that's
	 * safe (a verified-on-hardware ABI offset fact, not an assumption):
	 * every HSView-family class's BView subobject sits at offset 0, so
	 * casting the opaque handle straight to BView* reaches the right
	 * object no matter which concrete native class is actually behind
	 * it, for functions that only ever touch plain inherited BView
	 * state. Bounds is deliberately NOT included here -- hs_view_get_bounds()
	 * was never extended to the BView*-cast (nothing needs a control's
	 * own Bounds() yet), so it stays View-only.
	 *
	 * DestroyNativeHandle() is the seam a subclass MUST fill in: View.cs
	 * calls hs_view_destroy(), Control.cs calls hs_button_destroy(). Both
	 * end up going through this base class's Dispose(), so the ownership
	 * rule (only safe on a still-unattached object -- see hs_view.h's
	 * OWNERSHIP note) is enforced in exactly one place.
	 */
	public abstract class ViewBase : IDisposable
	{
		// internal (not private), same rationale as View.cs's own comment
		// used to give for these two fields: Window.AddChild(ViewBase)/
		// RemoveChild(ViewBase) and View.AddChild(ViewBase)/
		// RemoveChild(ViewBase) need to read/write _handle and _hasParent
		// on whatever concrete ViewBase they were handed.
		internal IntPtr _handle;
		private GCHandle _selfHandle;
		internal bool _hasParent;

		protected ViewBase(IntPtr handle)
		{
			_handle = handle;

			// A NORMAL (not weak) GCHandle -- same rationale as View's/
			// Window's own _selfHandle. Freed the instant MarkDestroyed()
			// runs (see below).
			_selfHandle = GCHandle.Alloc(this);
		}

		/// <summary>
		/// The GCHandle-derived user_data pointer a subclass's constructor
		/// passes to whichever hs_&lt;type&gt;_set_*_callback() functions
		/// it registers with its own native handle.
		/// </summary>
		protected IntPtr SelfHandleUserData
		{
			get { return GCHandle.ToIntPtr(_selfHandle); }
		}

		/// <summary>
		/// Called exactly once, right as the native object is being
		/// deleted. By the time this runs, no other method on this
		/// instance is safe to call. Default: does nothing.
		/// </summary>
		protected virtual void OnDestroyed() { }

		/// <summary>
		/// Called by a subclass's own destroyed-callback thunk (View's
		/// DestroyedThunk, Control's ControlDestroyedThunk, ...) --
		/// consumes the handle, frees the GCHandle, and fires
		/// <see cref="OnDestroyed"/>, in that order (consumed-before-
		/// notified, same as Window.cs's own DestroyedThunk).
		/// </summary>
		protected void MarkDestroyed()
		{
			_handle = IntPtr.Zero;
			if (_selfHandle.IsAllocated)
				_selfHandle.Free();
			OnDestroyed();
		}

		/// <summary>
		/// Destroys the underlying native object -- see hs_view.h's
		/// OWNERSHIP note. Overridden by each concrete subclass to call
		/// the matching hs_&lt;type&gt;_destroy() function for its own
		/// native handle (never a different one -- see this class's own
		/// remarks).
		/// </summary>
		protected abstract void DestroyNativeHandle(IntPtr handle);

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

		protected void CheckNotConsumed()
		{
			if (_handle == IntPtr.Zero)
				throw new ObjectDisposedException(GetType().Name,
					"This object has been destroyed and can no longer be used.");
		}

		/// <summary>
		/// Destroys this object. ONLY safe if it is not currently attached
		/// to a parent (never added via AddChild, or already
		/// RemoveChild()'d) -- see hs_view.h's OWNERSHIP note. Unlike
		/// Window.Dispose(), there is no "quit and let it happen later"
		/// alternative for an attached View/Control: throws rather than
		/// silently corrupting its parent's child list, since (unlike a
		/// shown Window) there is no native-side rejection of the mistake
		/// to fall back on.
		/// </summary>
		public void Dispose()
		{
			if (_handle == IntPtr.Zero)
				return;

			if (_hasParent)
				throw new InvalidOperationException(
					"Cannot Dispose() a " + GetType().Name + " that is still attached to a "
					+ "parent -- RemoveChild() it first (on its parent), or just let its "
					+ "parent's own destruction take care of it if you never needed to "
					+ "detach it.");

			IntPtr handle = _handle;
			_handle = IntPtr.Zero;
			DestroyNativeHandle(handle);
		}
	}
}
