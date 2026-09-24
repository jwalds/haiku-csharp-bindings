using System;
using System.Runtime.InteropServices;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSScrollView (see native/include/
	 * hs_scroll_view.h for the full design rationale -- read it before
	 * changing anything here).
	 *
	 * WHY THIS EXISTS: see hs_scroll_view.h's own "WHY THIS EXISTS" note
	 * -- a user compared this binding's ListView against a real BeOS
	 * screenshot and picked "Missing scrollbar" as the gap to close.
	 * ScrollView is not ListView-specific -- it wraps any ViewBase.
	 *
	 * REPARENTING: NO View.AddChild() CALL -- SEE hs_scroll_view.h
	 * --------------------------------------------------------------------
	 * hs_scroll_view_create() reparents `target` on the native side by
	 * itself (BScrollView's own constructor does this -- gdb-disassembly-
	 * confirmed, see hs_scroll_view.h's WRAPPING AND REPARENTING note).
	 * This constructor therefore does NOT call target's parent's
	 * AddChild() -- there is no separate parent-side call to make; the
	 * ScrollView itself becomes target's parent, natively, as a side
	 * effect of construction. What this constructor DOES do is set
	 * target._hasParent = true directly, the exact same internal-field
	 * write View.AddChild(ViewBase)/Window.AddChild(ViewBase) already
	 * perform after their own native AddChild() call -- see ViewBase.cs's
	 * own comment on why that field is `internal` rather than `private`.
	 * That alone is enough to make target.Dispose() throw while wrapped
	 * (ViewBase.Dispose() already checks it) and to make the ordinary
	 * destroyed-callback cascade fire target's own destroyed callback if
	 * this ScrollView (or something above it) is destroyed while target
	 * is still attached -- both fall out of mechanisms this binding
	 * already had, no new plumbing needed.
	 *
	 * `target` must not already be attached to a different parent when
	 * this constructor runs -- same rule real BeAPI's own AddChild()
	 * would apply. This constructor does not check that itself (neither
	 * does View.AddChild()); passing an already-attached target is a
	 * caller mistake, same class of mistake as elsewhere in this binding.
	 *
	 * The ScrollView itself is just another View as far as ITS OWN
	 * parent is concerned -- add it to a Window or View the ordinary way
	 * via AddChild(), inherited from ViewBase like every other concrete
	 * type in this binding.
	 *
	 * DELIBERATELY OUT OF SCOPE FOR THIS SLICE: see hs_scroll_view.h's own
	 * "DELIBERATELY OUT OF SCOPE" note -- no ScrollBar()/SetBorder()/
	 * Border()/SetTarget()/Target() accessors here yet.
	 */
	public class ScrollView : ViewBase
	{
		private readonly ScrollViewDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload: BScrollView's own defaults
		/// (resizing mode B_FOLLOW_LEFT_TOP, no extra flags,
		/// <see cref="ScrollViewBorder.Fancy"/> border) with just the
		/// scrollbar directions to show -- the common case (e.g. wrapping
		/// a ListView with a vertical scrollbar only:
		/// <c>new ScrollView("listScroll", myListView, false, true)</c>).
		/// </summary>
		public ScrollView(string name, ViewBase target, bool horizontal, bool vertical)
			: this(name, target, ViewResizingMode.FollowLeftTop, ViewFlags.None,
				horizontal, vertical, ScrollViewBorder.Fancy)
		{
		}

		public ScrollView(string name, ViewBase target, ViewResizingMode resizingMode,
			ViewFlags flags, bool horizontal, bool vertical, ScrollViewBorder border)
			: base(CreateNativeScrollView(name, target, resizingMode, flags,
				horizontal, vertical, border))
		{
			// See hs_scroll_view.h's WRAPPING AND REPARENTING note -- the
			// native call above already reparented target; this just
			// records that fact on the managed side, exactly the way
			// View.AddChild()/Window.AddChild() do after their own native
			// AddChild() call. No View.AddChild()/RemoveChild() call
			// belongs here.
			target._hasParent = true;

			IntPtr userData = SelfHandleUserData;
			_destroyedThunk = DestroyedThunk;
			Native.hs_scroll_view_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / CheckBox.cs's
		// CreateNativeCheckBox -- same "base(...) needs an expression"
		// reason for pulling hs_scroll_view_create() out here. Also
		// validates target up front, before base(...) can run with a
		// null handle.
		private static IntPtr CreateNativeScrollView(string name, ViewBase target,
			ViewResizingMode resizingMode, ViewFlags flags, bool horizontal, bool vertical,
			ScrollViewBorder border)
		{
			if (target == null)
				throw new ArgumentNullException("target");
			return Native.hs_scroll_view_create(target._handle, name,
				(uint)resizingMode, (uint)flags, horizontal, vertical, (uint)border);
		}

		private static ScrollView FromUserData(IntPtr userData)
		{
			return (ScrollView)GCHandle.FromIntPtr(userData).Target;
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as every other
			// HS*'s own DestroyedThunk -- see ViewBase.MarkDestroyed()'s
			// comment. Note this fires only for the ScrollView itself;
			// if target was still attached when this ScrollView was
			// destroyed, target's own destroyed callback already fired
			// first, as part of the same native cascade (see the class
			// remarks above) -- ~BView() deletes still-attached children
			// before finishing its own teardown.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_scroll_view_destroy(handle);
		}
	}
}
