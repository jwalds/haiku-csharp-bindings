using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Haiku.App;

namespace Haiku.Interface
{
	/*
	 * Managed wrapper over the native HSListView (see native/include/
	 * hs_list_view.h for the full design rationale -- read it before
	 * changing anything here, especially its ABI note on BView+BInvoker
	 * multiple inheritance and its OWNERSHIP note on BListItem cleanup).
	 *
	 * Derives from ViewBase DIRECTLY, not Control -- real BListView is
	 * not a BControl (see hs_list_view.h's own note), and not View --
	 * like Button/Slider/every other widget with its own native drawing
	 * and input handling, the native shim never wires up
	 * hs_view_set_draw_callback/mouse/key callbacks against an
	 * HSListView handle, so there is no OnDraw/OnMouseDown/OnKeyDown
	 * here (see Control.cs's own remarks for the identical reasoning).
	 * Frame/MoveTo/ResizeTo and AddChild/RemoveChild interop (a ListView
	 * can be added to a Window or a View) still work via ViewBase, since
	 * those only touch plain inherited BView state -- verified safe for
	 * THIS multiple-inheritance layout specifically (see hs_list_view.h).
	 *
	 * TEXT-ONLY ITEMS, NO ITEM HANDLES -- a deliberate scope decision.
	 * Every item is a plain string, addressed by its index, backed by a
	 * BStringItem this binding's native shim creates and owns
	 * internally (see hs_list_view.h's OWNERSHIP note on the real,
	 * hardware-verified BListItem leak this closes) -- there is no
	 * managed ListItem class and no way to plug in custom drawing.
	 *
	 * SCOPE: add/insert/remove/read/replace text items, CountItems,
	 * ListType, single- and multi-selection (Select/Deselect/
	 * DeselectAll/IsItemSelected/CurrentSelection), the
	 * SelectionChanged()/Invoke() callback pair, and -- from a later
	 * completeness pass -- SwapItems/MoveItem/bulk RemoveItems, a
	 * fixed ascending/descending text Sort, range Select/
	 * DeselectExcept, ItemFrame/IndexOfPoint hit-testing, IsEmpty,
	 * ScrollTo(index)/ScrollToSelection, and a pure-managed AddItems
	 * convenience -- see hs_list_view.h's own SCOPE note for what's
	 * still deliberately not covered (custom BListItem subclasses,
	 * BOutlineListView, drag-and-drop reordering, ReplaceItem, real
	 * AddList, DoForEach, a managed SortItems comparator callback).
	 *
	 * TWO EVENTS: OnSelectionChanged fires whenever the current
	 * selection changes -- confirmed on hardware to fire for a plain
	 * programmatic Select()/Deselect()/DeselectAll() call, not just a
	 * real click (see hs_list_view.h's own note) -- OnInvoked fires on
	 * double-click, or Enter/Return while a row has keyboard focus, real
	 * BeAPI's own Invoke() (see Button's OnClick for the same shape with
	 * a different trigger). No BMessage/BInvoker/target plumbing is
	 * involved in either.
	 *
	 * LIFECYCLE: unlike TextControl/RadioButton/Slider/ColorControl, a
	 * ListView does NOT need a live BApplication to construct --
	 * verified on hardware, matching CheckBox's own exception (see
	 * hs_list_view.h's note) -- so, unlike those widgets, it's safe to
	 * construct a ListView before Application.Run() as well as after.
	 * Otherwise the same shape as every other widget: no parent
	 * initially, safe to configure from whatever thread created it;
	 * once added, its parent owns it and deletes it automatically,
	 * recursively, on that parent's own destruction -- OnDestroyed still
	 * fires when that happens.
	 */
	public class ListView : ViewBase
	{
		private readonly ListViewSelectionChangedCallback _selectionChangedThunk;
		private readonly ListViewInvokedCallback _invokedThunk;
		private readonly ListViewDestroyedCallback _destroyedThunk;

		/// <summary>
		/// Convenience overload for the common case: a single-selection
		/// list with real BeAPI's own default flags (B_WILL_DRAW |
		/// B_FRAME_EVENTS | B_NAVIGABLE, matching ListView.h's own
		/// default) and no special resizing mode.
		/// </summary>
		public ListView(Rect frame, string name)
			: this(frame, name, ListViewType.SingleSelection, ViewResizingMode.None,
				ViewFlags.WillDraw | ViewFlags.FrameEvents | ViewFlags.Navigable)
		{
		}

		public ListView(Rect frame, string name, ListViewType type,
			ViewResizingMode resizingMode, ViewFlags flags)
			: base(CreateNativeListView(frame, name, type, resizingMode, flags))
		{
			IntPtr userData = SelfHandleUserData;

			_selectionChangedThunk = SelectionChangedThunk;
			_invokedThunk = InvokedThunk;
			_destroyedThunk = DestroyedThunk;

			Native.hs_list_view_set_selection_changed_callback(_handle, _selectionChangedThunk, userData);
			Native.hs_list_view_set_invoked_callback(_handle, _invokedThunk, userData);
			Native.hs_list_view_set_destroyed_callback(_handle, _destroyedThunk, userData);
		}

		// See View.cs's CreateNativeView / Slider.cs's CreateNativeSlider
		// -- same "base(...) needs an expression" reason for pulling
		// hs_list_view_create() out here.
		private static IntPtr CreateNativeListView(Rect frame, string name, ListViewType type,
			ViewResizingMode resizingMode, ViewFlags flags)
		{
			HsRect nativeFrame = new HsRect {
				Left = frame.Left,
				Top = frame.Top,
				Right = frame.Right,
				Bottom = frame.Bottom,
			};
			return Native.hs_list_view_create(nativeFrame, name, (uint)type,
				(uint)resizingMode, (uint)flags);
		}

		/// <summary>Appends a new text item to the end of the list.</summary>
		public void AddItem(string text)
		{
			CheckNotConsumed();
			Native.hs_list_view_add_item(_handle, text);
		}

		/// <summary>Inserts a new text item at index (0 = the very top; CountItems is equivalent to appending).</summary>
		public void AddItem(string text, int index)
		{
			CheckNotConsumed();
			Native.hs_list_view_add_item_at(_handle, text, index);
		}

		/// <summary>
		/// Removes the item at index. Returns false if index is out of
		/// range. See ListView.cs's own class comment / hs_list_view.h's
		/// OWNERSHIP note -- this binding deletes the underlying item
		/// itself, closing a real leak plain BeAPI would otherwise have.
		/// </summary>
		public bool RemoveItemAt(int index)
		{
			CheckNotConsumed();
			return Native.hs_list_view_remove_item_at(_handle, index);
		}

		/// <summary>Removes every item. See RemoveItemAt's own remarks on ownership.</summary>
		public void MakeEmpty()
		{
			CheckNotConsumed();
			Native.hs_list_view_make_empty(_handle);
		}

		public int CountItems
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_list_view_count_items(_handle);
			}
		}

		/// <summary>The text of the item at index, or null if index is out of range.</summary>
		public string ItemTextAt(int index)
		{
			CheckNotConsumed();
			return Marshal.PtrToStringAnsi(Native.hs_list_view_item_text(_handle, index));
		}

		/// <summary>Replaces the text of the item already at index in place -- does nothing if index is out of range.</summary>
		public void SetItemTextAt(int index, string text)
		{
			CheckNotConsumed();
			Native.hs_list_view_set_item_text(_handle, index, text);
		}

		/// <summary>
		/// Selects index, extending the current selection instead of
		/// replacing it when extend is true (only meaningful when
		/// <see cref="ListType"/> is <see cref="ListViewType.MultipleSelection"/>).
		/// Fires <see cref="OnSelectionChanged"/> -- confirmed on
		/// hardware to fire for a programmatic call exactly like a real
		/// click.
		/// </summary>
		public void Select(int index, bool extend = false)
		{
			CheckNotConsumed();
			Native.hs_list_view_select(_handle, index, extend);
		}

		public void Deselect(int index)
		{
			CheckNotConsumed();
			Native.hs_list_view_deselect(_handle, index);
		}

		public void DeselectAll()
		{
			CheckNotConsumed();
			Native.hs_list_view_deselect_all(_handle);
		}

		public bool IsItemSelected(int index)
		{
			CheckNotConsumed();
			return Native.hs_list_view_is_item_selected(_handle, index);
		}

		/// <summary>
		/// The selectionIndex-th selected row's index (0 for the
		/// first-selected row, 1 for the second, ...), or -1 once
		/// selectionIndex runs past the number of currently-selected
		/// rows -- real BeAPI's own CurrentSelection(index) shape,
		/// which already covers both <see cref="ListViewType"/> values.
		/// </summary>
		public int CurrentSelection(int selectionIndex = 0)
		{
			CheckNotConsumed();
			return Native.hs_list_view_current_selection(_handle, selectionIndex);
		}

		public ListViewType ListType
		{
			get
			{
				CheckNotConsumed();
				return (ListViewType)Native.hs_list_view_list_type(_handle);
			}
			set
			{
				CheckNotConsumed();
				Native.hs_list_view_set_list_type(_handle, (uint)value);
			}
		}

		/*
		 * --- Completeness pass below -- see this class's own header
		 * comment and hs_list_view.h's for the full design rationale. ---
		 */

		/// <summary>
		/// Swaps the items at indices a and b in place. Returns false if
		/// either index is out of range.
		/// </summary>
		public bool SwapItems(int a, int b)
		{
			CheckNotConsumed();
			return Native.hs_list_view_swap_items(_handle, a, b);
		}

		/// <summary>
		/// Moves the item at index from to index to, shifting every item
		/// between them to make room. Returns false if either index is
		/// out of range.
		/// </summary>
		public bool MoveItem(int from, int to)
		{
			CheckNotConsumed();
			return Native.hs_list_view_move_item(_handle, from, to);
		}

		/// <summary>
		/// Removes and deletes up to count items starting at index
		/// (fewer if the list is shorter than index + count). Returns
		/// the number of items actually removed. See
		/// <see cref="RemoveItemAt"/>'s own remarks on ownership -- this
		/// binding deletes the underlying items itself.
		/// </summary>
		public int RemoveItems(int index, int count)
		{
			CheckNotConsumed();
			return Native.hs_list_view_remove_items(_handle, index, count);
		}

		/// <summary>
		/// Sorts every item in place by its text -- a plain, non-locale-
		/// aware ordinal comparison, ascending unless
		/// <paramref name="ascending"/> is false. Does not fire
		/// <see cref="OnSelectionChanged"/> -- matches real BeAPI, whose
		/// own SortItems() clears the selection without invoking
		/// SelectionChanged().
		/// </summary>
		public void Sort(bool ascending = true)
		{
			CheckNotConsumed();
			Native.hs_list_view_sort(_handle, ascending);
		}

		/// <summary>
		/// Selects every index from <paramref name="from"/> to
		/// <paramref name="to"/> inclusive (either order), extending the
		/// current selection instead of replacing it when extend is true
		/// (only meaningful when <see cref="ListType"/> is
		/// <see cref="ListViewType.MultipleSelection"/>). Fires
		/// <see cref="OnSelectionChanged"/>, same as the single-index
		/// <see cref="Select(int, bool)"/> overload above.
		/// </summary>
		public void Select(int from, int to, bool extend = false)
		{
			CheckNotConsumed();
			Native.hs_list_view_select_range(_handle, from, to, extend);
		}

		/// <summary>
		/// Deselects every currently-selected index except those from
		/// exceptFrom to exceptTo inclusive.
		/// </summary>
		public void DeselectExcept(int exceptFrom, int exceptTo)
		{
			CheckNotConsumed();
			Native.hs_list_view_deselect_except(_handle, exceptFrom, exceptTo);
		}

		/// <summary>
		/// The on-screen frame (in this list view's own coordinate
		/// space) of the item at index. Returns an all-zero Rect if
		/// index is out of range -- also all-zero (hardware-confirmed)
		/// if this list view has not yet been added to a window, since
		/// item height is only measured against a real owner once
		/// attached. Call this after <see cref="ViewBase.AddChild"/>
		/// (or after the window that will contain it is showing), not
		/// before.
		/// </summary>
		public Rect ItemFrame(int index)
		{
			CheckNotConsumed();
			HsRect native;
			Native.hs_list_view_item_frame(_handle, index, out native);
			return new Rect(native.Left, native.Top, native.Right, native.Bottom);
		}

		/// <summary>
		/// The index of the item whose frame contains point (in this
		/// list view's own coordinate space), or -1 if point falls
		/// outside every item's frame -- real BeAPI's own IndexOf(BPoint)
		/// hit-test. Same attachment requirement as
		/// <see cref="ItemFrame"/> above (always returns -1 before this
		/// list view is added to a window). This ListView type has no
		/// mouse callbacks of its own (see this class's own comment), so
		/// point must come from elsewhere -- e.g. a coordinate derived
		/// from <see cref="ItemFrame"/>, or supplied by the caller some
		/// other way.
		/// </summary>
		public int IndexOfPoint(Point point)
		{
			CheckNotConsumed();
			HsPoint native = new HsPoint { X = point.X, Y = point.Y };
			return Native.hs_list_view_index_of_point(_handle, native);
		}

		public bool IsEmpty
		{
			get
			{
				CheckNotConsumed();
				return Native.hs_list_view_is_empty(_handle);
			}
		}

		/// <summary>Scrolls so the item at index is visible.</summary>
		public void ScrollTo(int index)
		{
			CheckNotConsumed();
			Native.hs_list_view_scroll_to_index(_handle, index);
		}

		/// <summary>Scrolls so the current selection is visible.</summary>
		public void ScrollToSelection()
		{
			CheckNotConsumed();
			Native.hs_list_view_scroll_to_selection(_handle);
		}

		/// <summary>
		/// Pure-managed convenience, not a separate native call: appends
		/// every string in items to the end of the list, in order, by
		/// looping <see cref="AddItem(string)"/>. Real BeAPI's own bulk
		/// AddList(BList*) is out of scope for this binding (see
		/// hs_list_view.h's own SCOPE note) -- this achieves the same
		/// net effect for the common case without needing a BList
		/// marshaling story.
		/// </summary>
		public void AddItems(IEnumerable<string> items)
		{
			CheckNotConsumed();
			foreach (string item in items)
				AddItem(item);
		}

		/// <summary>
		/// Called on the owning window's thread whenever the current
		/// selection changes -- including a plain programmatic
		/// <see cref="Select"/>/<see cref="Deselect"/>/
		/// <see cref="DeselectAll"/> call, not just a real click (see
		/// this class's own comment). Default: does nothing.
		/// </summary>
		protected virtual void OnSelectionChanged() { }

		/// <summary>
		/// Called on the owning window's thread on double-click, or
		/// Enter/Return while a row has keyboard focus. No BMessage/
		/// target is involved -- see this class's own comment. Default:
		/// does nothing.
		/// </summary>
		protected virtual void OnInvoked() { }

		private static ListView FromUserData(IntPtr userData)
		{
			return (ListView)GCHandle.FromIntPtr(userData).Target;
		}

		private static void SelectionChangedThunk(IntPtr userData)
		{
			FromUserData(userData).OnSelectionChanged();
		}

		private static void InvokedThunk(IntPtr userData)
		{
			FromUserData(userData).OnInvoked();
		}

		private static void DestroyedThunk(IntPtr userData)
		{
			// Same "consumed-before-notified" ordering as View.cs's/
			// Slider.cs's own DestroyedThunk -- see
			// ViewBase.MarkDestroyed()'s comment.
			FromUserData(userData).MarkDestroyed();
		}

		protected override void DestroyNativeHandle(IntPtr handle)
		{
			Native.hs_list_view_destroy(handle);
		}
	}
}
