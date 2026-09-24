/*
 * hs_list_view.h -- C shim over BListView (headers/os/interface/
 * ListView.h), backed by BStringItem (headers/os/interface/StringItem.h)
 * for its items. Same trampoline shape as hs_button.h -- read that file
 * first if you haven't. This comment covers only what's actually
 * different about BListView.
 *
 * NOT A BControl -- BView PLUS BInvoker, MULTIPLE INHERITANCE, VERIFIED
 * FOR THE FIRST TIME IN THIS BINDING
 * --------------------------------------------------------------------------
 * Every widget this binding has wrapped so far (Button, TextControl,
 * CheckBox, RadioButton, Slider, ColorControl) derives from BControl,
 * itself a single, non-virtual BView subclass. Real BListView does not:
 * `class BListView : public BView, public BInvoker`. A probe confirmed
 * BView is still the first base and still sits at offset 0 (casting a
 * live ProbeListView* to BView* produced the identical pointer value,
 * and Frame() read through either path matched exactly) -- so
 * hs_view_get_frame()/move_to()/resize_to()/add_child()/remove_child()
 * are reused unchanged, same as every other widget. BInvoker itself is
 * NOT at offset 0 (a second base class never is under the Itanium C++
 * ABI this binding builds under) -- this shim never blind-casts to
 * BInvoker*, and never needs to: SelectionChanged()/Invoke() are called
 * through HSListView's own real vtable (see below), not through a
 * generic cast, so the BInvoker subobject's actual offset never matters
 * here.
 *
 * NO BApplication REQUIREMENT -- THE SECOND WIDGET TO NOT NEED ONE
 * --------------------------------------------------------------------------
 * Unlike TextControl/RadioButton/Slider/ColorControl (all of which hang
 * indefinitely without a live BApplication), a probe confirmed
 * BListView constructs fine with none -- matching BCheckBox's own
 * exception, not the majority pattern. Verified directly (a dedicated
 * probe constructed one, added nothing else, and returned normally),
 * not assumed to carry over from any other widget's own finding.
 *
 * BListItem OWNERSHIP: REAL BeAPI NEVER DELETES ITEMS -- THIS SHIM DOES,
 * SINCE IT'S THE ONLY THING THAT EVER CREATES ONE
 * --------------------------------------------------------------------------
 * A hardware probe with an instrumented BStringItem subclass (destructor
 * printed when it ran) confirmed that real BListView's RemoveItem(int32),
 * MakeEmpty(), AND ~BListView() itself never delete the BListItems they
 * stop referencing -- ownership passes back to the caller on RemoveItem,
 * and MakeEmpty()/~BListView() both leak silently if the caller doesn't
 * track and delete items separately. This is genuine, verified BeAPI
 * behavior, not a shim bug to work around later. Since this shim is the
 * ONLY thing that ever calls `new BStringItem(...)` for a list it
 * created (no function here ever exposes a raw BListItem* handle to the
 * managed side -- see the SCOPE note below), it can safely take on the
 * ownership BeAPI itself declines: hs_list_view_remove_item_at() deletes
 * the item after removing it, hs_list_view_make_empty() deletes every
 * item before clearing the list, hs_list_view_remove_items() (see the
 * completeness-pass note below) deletes every item it removes, and
 * HSListView's own destructor deletes whatever items remain before
 * letting ~BListView() run. Nothing above BStringItem's own text storage
 * is affected by this -- it only closes the real, hardware-confirmed
 * leak.
 *
 * SelectionChanged()/Invoke() -- A DIRECT CALLBACK PAIR, MATCHING
 * TextControl/Slider's TWO-HOOK SHAPE, NOT Button/ColorControl's ONE
 * --------------------------------------------------------------------------
 * Same "no BMessage/BInvoker/target plumbing" scope decision as every
 * other widget in this binding (see hs_button.h's own note) --
 * HSListView overrides both SelectionChanged() and Invoke() to fire
 * direct callbacks instead of posting anywhere; SetSelectionMessage()/
 * SetInvocationMessage() are simply never called, so both stay NULL
 * forever and neither override ever has anything to post regardless.
 * A probe confirmed SelectionChanged() fires correctly on ordinary
 * programmatic Select()/Select(..., extend=true)/DeselectAll() calls,
 * not merely on a real mouse click -- useful both for this shim's own
 * correctness and for ListViewTests.cs, which drives selection
 * programmatically (no synthetic mouse input available over this
 * project's SSH-only hardware harness). Invoke() (real BeAPI: fires on
 * double-click, or Enter/Return while a row has keyboard focus) was not
 * separately probed the same way -- like Button's OnClick, it's the
 * kind of input-driven behavior this binding demonstrates visually
 * (Sample.exe) rather than synthesizes programmatically.
 *
 * REUSING hs_view_get_frame()/move_to()/resize_to()/add_child()/
 * remove_child() -- SEE THE ABI NOTE ABOVE
 * --------------------------------------------------------------------------
 * There is no hs_list_view_get_frame()/move_to()/resize_to() -- see
 * hs_view.h, reused directly against a list view handle exactly like
 * every other HSView-family object (verified safe for THIS multiple-
 * inheritance layout specifically, not merely assumed -- see above).
 *
 * COMPLETENESS PASS: REORDERING, SORTING, RANGE SELECTION, HIT-TESTING
 * --------------------------------------------------------------------------
 * A later pass added hs_list_view_swap_items()/move_item() (thin wraps
 * of real BeAPI's own SwapItems()/MoveItem()), hs_list_view_remove_items()
 * (bulk removal -- NOT a thin wrap of real BeAPI's own RemoveItems(); see
 * that function's own comment below for why), hs_list_view_sort()
 * (ascending/descending by item text -- see its own comment below on
 * real BeAPI's actual SortItems() comparator shape, confirmed by
 * disassembling the installed libbe.so with gdb rather than guessed),
 * hs_list_view_select_range()/deselect_except() (completing the
 * selection API to match real BeAPI's own Select(from, to, extend)/
 * DeselectExcept() overloads), hs_list_view_item_frame()/index_of_point()
 * (hit-testing, pairing with hs_view.h's own mouse callbacks for custom
 * click handling), hs_list_view_is_empty(), and
 * hs_list_view_scroll_to_index()/scroll_to_selection(). Every one of
 * these is hardware-verified before being relied on, per this binding's
 * standing rule -- see details.md's own ListView completeness-pass
 * section for the verification writeup.
 *
 * SCOPE
 * --------------------------------------------------------------------------
 * Covered: construction (frame/name/list_view_type/resizing/flags),
 * text-only items (add/insert/remove/read/replace, all backed by a
 * BStringItem this shim owns), CountItems, list_view_type get/set,
 * single- and multi-selection (Select/Deselect/DeselectAll/
 * IsItemSelected/CurrentSelection -- BeAPI's own CurrentSelection(index)
 * already handles both list types, so both are supported for the same
 * cost), the SelectionChanged()/Invoke() callback pair, and -- from the
 * completeness pass above -- SwapItems/MoveItem/bulk RemoveItems, a
 * fixed ascending/descending text SortItems, range Select/DeselectExcept,
 * ItemFrame/IndexOf(point) hit-testing, IsEmpty, and ScrollTo(index)/
 * ScrollToSelection. NOT covered, a deliberate scope decision: custom
 * BListItem subclasses (anything beyond plain text -- would need a
 * DrawItem() callback into managed code, a meaningfully bigger design
 * than this slice), BOutlineListView (a related but separate class),
 * drag-and-drop reordering (InitiateDrag), ReplaceItem(BListItem*) (would
 * need an item handle this shim deliberately never exposes -- SetItemText
 * already covers replacing a row's displayed text without one), AddList
 * (bulk add from a raw BList* -- the managed side gets an equivalent
 * pure-managed convenience that loops the existing per-item add instead,
 * see ListView.cs), DoForEach (fully replaceable by a managed loop over
 * CountItems()/ItemTextAt(i), so it adds no real capability), a general
 * managed comparator callback for SortItems (see that function's own
 * comment below for why only a fixed text comparator is exposed), and the
 * BMessage-based constructor/Archive/Instantiate (out of scope everywhere
 * in this binding). Also out of scope: wrapping in a BScrollView -- a
 * plain BListView still works and scrolls a selection into view via
 * hs_list_view_scroll_to_selection() (now explicit, not merely internal),
 * it just has no visible scrollbar without one; Sample.exe's demo sizes
 * its frame to fit every row so this doesn't need to be solved to prove
 * the widget works.
 */
#ifndef HS_LIST_VIEW_H
#define HS_LIST_VIEW_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_list_view_selection_changed_callback)(void* user_data);
typedef void (*hs_list_view_invoked_callback)(void* user_data);
typedef void (*hs_list_view_destroyed_callback)(void* user_data);

/* Create a new HSListView (a BListView subclass) with the given frame,
 * name, list_view_type (the raw enum value: B_SINGLE_SELECTION_LIST=0,
 * B_MULTIPLE_SELECTION_LIST=1 -- checked against the actual installed
 * ListView.h, a plain unvalued C++ enum so these are guaranteed by the
 * language, not merely likely), resizing mode, and flags. Has no parent
 * and is not attached to any window yet -- safe to configure from
 * whatever thread created it, same threading rule as hs_view.h's. Unlike
 * most other widgets in this binding, does NOT need a live BApplication
 * to construct -- see this file's header comment. */
hs_handle hs_list_view_create(hs_rect frame, const char* name,
	uint32_t list_type, uint32_t resizing_mode, uint32_t flags);

/* ONLY safe on a list view that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy(). Deletes every item still in
 * the list first (see this file's header comment on BListItem
 * ownership), then fires the destroyed callback, same as any other path
 * to this list view's destruction. */
void hs_list_view_destroy(hs_handle list_view);

void hs_list_view_set_selection_changed_callback(hs_handle list_view,
	hs_list_view_selection_changed_callback callback, void* user_data);
void hs_list_view_set_invoked_callback(hs_handle list_view,
	hs_list_view_invoked_callback callback, void* user_data);
void hs_list_view_set_destroyed_callback(hs_handle list_view,
	hs_list_view_destroyed_callback callback, void* user_data);

/* Appends a new text item (backed by a BStringItem this shim owns -- see
 * the OWNERSHIP note above) to the end of the list. */
void hs_list_view_add_item(hs_handle list_view, const char* text);

/* Inserts a new text item at the given index (0 = the very top; passing
 * CountItems() is equivalent to hs_list_view_add_item()). */
void hs_list_view_add_item_at(hs_handle list_view, const char* text,
	int32_t index);

/* Removes and deletes the item at index (see the OWNERSHIP note above --
 * real BeAPI would merely hand the pointer back and leak it if the
 * caller didn't; this shim never lets a raw BListItem* escape, so it
 * deletes it here instead). Returns false if index is out of range. */
bool hs_list_view_remove_item_at(hs_handle list_view, int32_t index);

/* Removes and deletes every item (see the OWNERSHIP note above -- plain
 * BListView::MakeEmpty() alone would leak every item still present). */
void hs_list_view_make_empty(hs_handle list_view);

int32_t hs_list_view_count_items(hs_handle list_view);

/* Returns a pointer into the item's own internal text storage, same
 * borrowed-pointer situation as hs_control_label(). Copy it into a
 * managed string immediately, do not hold onto it or free it. Returns
 * NULL if index is out of range. */
const char* hs_list_view_item_text(hs_handle list_view, int32_t index);

/* Replaces the text of the item already at index in place (same
 * BStringItem, just a new string) -- does nothing if index is out of
 * range. */
void hs_list_view_set_item_text(hs_handle list_view, int32_t index,
	const char* text);

/* Selects index, extending the current selection instead of replacing it
 * when extend is true (only meaningful for a B_MULTIPLE_SELECTION_LIST --
 * see hs_list_view_set_list_type() below). Fires SelectionChanged() --
 * confirmed on hardware to fire for a programmatic call exactly like a
 * real click, see this file's header comment. */
void hs_list_view_select(hs_handle list_view, int32_t index, bool extend);
void hs_list_view_deselect(hs_handle list_view, int32_t index);
void hs_list_view_deselect_all(hs_handle list_view);

bool hs_list_view_is_item_selected(hs_handle list_view, int32_t index);

/* Returns the selection_index-th selected row's index (0 for the
 * first-selected row, 1 for the second, ...), or -1 once selection_index
 * runs past the number of currently-selected rows -- real BeAPI's own
 * CurrentSelection(int32 index = 0) shape, which already handles both
 * list_view_type values without this shim needing two different
 * functions. */
int32_t hs_list_view_current_selection(hs_handle list_view,
	int32_t selection_index);

void hs_list_view_set_list_type(hs_handle list_view, uint32_t type);
uint32_t hs_list_view_list_type(hs_handle list_view);

/*
 * --- Completeness pass below -- see this file's header comment ---
 */

/* Swaps the items at indices a and b in place. Returns false if either
 * index is out of range -- real BeAPI's own bool SwapItems(int32, int32)
 * return. */
bool hs_list_view_swap_items(hs_handle list_view, int32_t a, int32_t b);

/* Moves the item at from to index to, shifting every item between them
 * up or down by one to make room. Returns false if either index is out
 * of range -- real BeAPI's own bool MoveItem(int32, int32) return. */
bool hs_list_view_move_item(hs_handle list_view, int32_t from, int32_t to);

/* Removes and deletes up to count items starting at index (fewer if the
 * list is shorter than index + count -- a clamping "remove what's there"
 * shape, not a hard failure). Returns the number of items actually
 * removed (0 if index is already out of range). Unlike every other
 * function in this file, this is NOT a thin wrap of real BeAPI's own
 * RemoveItems(int32, int32): that call's underlying implementation was
 * not independently re-verified for the same not-deleted ownership
 * behavior already confirmed for RemoveItem(int32)/MakeEmpty()/
 * ~BListView() (see this file's OWNERSHIP note) -- rather than assume it
 * carries over, this shim implements the bulk removal as a loop over the
 * already-verified single-item RemoveItem(int32) (the same call
 * hs_list_view_remove_item_at() above already uses), deleting each
 * removed BStringItem itself exactly the same way. */
int32_t hs_list_view_remove_items(hs_handle list_view, int32_t index,
	int32_t count);

/* Sorts every item in place by its text, ascending if ascending is true,
 * descending otherwise -- a plain byte-wise strcmp() ordering, not
 * locale-aware. Real BeAPI's own SortItems() takes a raw comparator,
 * int (*cmp)(const void*, const void*); a gdb disassembly of the actual
 * installed libbe.so (BListView::SortItems -> BList::SortItems, which
 * bottlenecks straight into libc qsort() over the list's own internal
 * array of BListItem* with elemsize=sizeof(void*)) confirmed each
 * argument passed to cmp is a POINTER TO the BListItem* slot being
 * compared -- i.e. **(BListItem**)a, not *(BListItem*)a -- exactly the
 * usual qsort-over-an-array-of-pointers shape. This shim provides only a
 * fixed ascending/descending text comparator built on that confirmed
 * shape, rather than exposing it to managed code (which would need a
 * marshaled callback re-entering managed code from inside a native
 * qsort() -- a meaningfully bigger, riskier design, and unnecessary since
 * every item here is already known to be a plain BStringItem). Fires no
 * selection-change callback itself -- matches real BeAPI, whose own
 * SortItems() clears the selection without invoking SelectionChanged(). */
void hs_list_view_sort(hs_handle list_view, bool ascending);

/* Selects every index from from to to inclusive (either order),
 * extending the current selection instead of replacing it when extend is
 * true -- real BeAPI's own Select(int32 from, int32 to, bool extend)
 * overload, meaningful mainly for a B_MULTIPLE_SELECTION_LIST. Fires
 * SelectionChanged() the same as the single-index hs_list_view_select()
 * above. */
void hs_list_view_select_range(hs_handle list_view, int32_t from,
	int32_t to, bool extend);

/* Deselects every currently-selected index except those from
 * except_from to except_to inclusive -- real BeAPI's own
 * DeselectExcept(int32, int32). */
void hs_list_view_deselect_except(hs_handle list_view, int32_t except_from,
	int32_t except_to);

/* Writes the on-screen frame (in the list view's own coordinate space) of
 * the item at index into *out_frame -- same out-pointer convention as
 * hs_view_get_frame(). Does nothing if index is out of range or
 * out_frame is NULL. HARDWARE-CONFIRMED: only meaningful once this list
 * view is attached to a window -- a probe found every item's frame
 * comes back all-zero (0-height) beforehand, presumably because
 * BStringItem's height is only measured against a real owner/font once
 * attached. Not itself guarded against (returns whatever real BeAPI's
 * own ItemFrame() returns, all-zero or otherwise) -- callers should
 * attach first, same as ListViewTests.cs's own
 * ItemFrameReturnsIncreasingTopForEachRow. */
void hs_list_view_item_frame(hs_handle list_view, int32_t index,
	hs_rect* out_frame);

/* Returns the index of the item whose frame contains point (in the list
 * view's own coordinate space), or -1 if point falls outside every
 * item's frame -- real BeAPI's own IndexOf(BPoint) overload, useful for
 * turning a raw mouse-down location (see hs_view.h's own mouse callbacks)
 * into a row index for custom click handling. Same attachment
 * requirement as hs_list_view_item_frame() above (hit-tests against
 * the same per-item geometry) -- confirmed on hardware to always
 * return -1 on a never-attached list view, even for a point that
 * should land inside an item once real geometry exists. */
int32_t hs_list_view_index_of_point(hs_handle list_view, hs_point point);

bool hs_list_view_is_empty(hs_handle list_view);

/* Scrolls so the item at index is visible -- real BeAPI's own
 * ScrollTo(int32 index) overload on BListView itself, distinct from the
 * plain BView::ScrollTo(BPoint) that this binding does not expose for
 * any widget. */
void hs_list_view_scroll_to_index(hs_handle list_view, int32_t index);

/* Scrolls so the current selection is visible -- real BeAPI's own
 * ScrollToSelection(). A plain BListView already does this internally
 * whenever the selection changes (see this file's SCOPE note on
 * BScrollView wrapping), but it's exposed explicitly here too, e.g. to
 * re-scroll after a caller has since scrolled elsewhere with
 * hs_list_view_scroll_to_index(). */
void hs_list_view_scroll_to_selection(hs_handle list_view);

#ifdef __cplusplus
}
#endif

#endif /* HS_LIST_VIEW_H */
