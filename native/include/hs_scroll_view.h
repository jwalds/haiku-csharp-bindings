/*
 * hs_scroll_view.h -- C shim over BScrollView (headers/os/interface/
 * ScrollView.h), which wraps BScrollBar (headers/os/interface/
 * ScrollBar.h) around an existing target view.
 *
 * Same trampoline shape as hs_checkbox.h/hs_button.h -- read hs_view.h
 * first if you haven't, especially its "OWNERSHIP" note, which this file
 * both depends on and extends in a new direction (see WRAPPING AND
 * REPARENTING below). This comment covers only what's actually different
 * about BScrollView.
 *
 * WHY THIS EXISTS: A REAL, USER-REPORTED RENDERING GAP
 * --------------------------------------------------------------------------
 * A user compared this binding's `ListView` against a screenshot of the
 * real BeOS "Sliders, Tabs & Lists" demo app and pointed out it looked
 * "a little odd" -- offered four possible causes via AskUserQuestion, and
 * picked "Missing scrollbar" as the one to fix. A plain, unwrapped
 * BListView has no visible scrollbar of its own; real BeAPI applications
 * always wrap a scrolling view in a BScrollView to get one. This shim is
 * that wrapping mechanism, generic over any BView-family target -- not
 * ListView-specific, even though ListView is what prompted it.
 *
 * WRAPPING AND REPARENTING: NO SEPARATE hs_view_add_child() CALL NEEDED
 * --------------------------------------------------------------------------
 * Confirmed via `gdb` disassembly of the real installed `libbe.so` -- not
 * guessed. BScrollView's constructor calls a private `_Init(horizontal,
 * vertical)` helper, and disassembling `_Init()` shows it calls
 * `BView::AddChild()` internally, at least twice: once to attach the
 * wrapped target view, and once more per BScrollBar it constructs for
 * whichever of `horizontal`/`vertical` were requested. In other words,
 * constructing an HSScrollView around a target already reparents that
 * target exactly the way a real BeAPI application's own
 * `new BScrollView(...)` call would -- this shim's hs_scroll_view_create()
 * must NOT also call hs_view_add_child()/AddChild() on the target itself;
 * doing so would double-add it (BeAPI's own AddChild() does not guard
 * against re-adding an already-attached child gracefully -- this was not
 * tested here because there is no reason to: the real constructor already
 * does the right thing on its own, verified above).
 *
 * The managed side's job is therefore not to reparent target itself, but
 * to record that it now IS reparented -- see ScrollView.cs, which sets
 * the wrapped ViewBase's internal `_hasParent` field directly (the exact
 * same field/pattern View.AddChild()/Window.AddChild() already set),
 * immediately after a successful hs_scroll_view_create() call. That
 * alone is enough to make target.Dispose() correctly throw while
 * wrapped, and to make the ordinary destroyed-callback cascade (a real
 * `~BView()` recursively deletes still-attached children) correctly fire
 * target's own destroyed callback if the HSScrollView -- or something
 * further up its own parent chain -- is destroyed while still attached.
 * No new mechanism was needed for either of those; both already fall out
 * of mechanisms this binding built for View/Button/etc.
 *
 * The HSScrollView itself is just another BView (single, non-virtual
 * inheritance -- confirmed from the real header: `class BScrollView :
 * public BView`), so it gets added to ITS OWN parent the ordinary way,
 * through hs_view_add_child()/hs_window_add_child(), exactly like any
 * other HSView-family handle (see hs_view.h's own note on that). Nothing
 * about wrapping a target changes how the wrapper itself gets attached.
 *
 * BORDER_STYLE: RAW ENUM VALUES, VERIFIED AGAINST THE REAL HEADER
 * --------------------------------------------------------------------------
 * `border_style` (headers/os/interface/InterfaceDefs.h) is a plain,
 * non-flags enum: `B_PLAIN_BORDER` = 0, `B_FANCY_BORDER` = 1,
 * `B_NO_BORDER` = 2 -- read directly from the real installed header, not
 * assumed. hs_scroll_view_create()'s `border_style` parameter is this raw
 * numeric value, same convention as resizing_mode/flags elsewhere in this
 * binding; the managed ScrollViewBorder enum mirrors these three values
 * exactly (see ScrollViewBorder.cs).
 *
 * DELIBERATELY OUT OF SCOPE FOR THIS SLICE
 * --------------------------------------------------------------------------
 * `ScrollBar(orientation)` (getting the actual BScrollBar* to configure
 * its range/proportion/steps), `SetBorder()`/`Border()`,
 * `SetTarget()`/`Target()` (re-targeting an existing BScrollView after
 * construction), and `SetBorderHighlighted()` are all real BScrollView/
 * BScrollBar API not exposed here -- this slice's goal is just a visible,
 * correctly-behaving scrollbar around a wrapped view, matching this
 * binding's established pattern of shipping a minimal-but-real vertical
 * slice first and coming back for a completeness pass later (see the
 * Slider and ListView sections of details.md for two earlier examples of
 * exactly this two-pass shape). `BListView::TargetedByScrollView()`
 * (confirmed to exist via `nm -D`) suggests a plain BListView already
 * knows how to keep its selection in view once wrapped, with no extra
 * plumbing needed here for that to work -- if hardware testing shows
 * otherwise, that becomes a new, separately-scoped task rather than
 * silently expanding this one.
 *
 * NO BMessage/BInvoker PLUMBING -- THERE ISN'T ANY TO HAVE
 * --------------------------------------------------------------------------
 * Unlike BButton/BCheckBox, BScrollView is not a BControl/BInvoker at
 * all -- it never posts a BMessage anywhere, so there is no Invoke() to
 * override and no click/change callback to wire up here. HSScrollView
 * overrides only its destructor, for the destroyed-callback, same
 * minimal-override shape as every other HS* class in this binding.
 */
#ifndef HS_SCROLL_VIEW_H
#define HS_SCROLL_VIEW_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_scroll_view_destroyed_callback)(void* user_data);

/* Create a new HSScrollView (a BScrollView subclass) wrapping `target`,
 * an existing HSView-family handle (an HSView*, HSListView*, etc. -- same
 * "any HSView-family handle" note as hs_view_add_child()). This call
 * reparents target -- see the WRAPPING AND REPARENTING note above --
 * do NOT also call hs_view_add_child() on it. `target` must not already
 * be attached to a different parent (same rule real BeAPI's own AddChild()
 * would apply, per the OWNERSHIP note in hs_view.h). `border_style` is
 * the raw border_style value (see the BORDER_STYLE note above); pass 1
 * (B_FANCY_BORDER) to match BScrollView's own default. The returned
 * HSScrollView has no parent of its own yet -- add it to a window or
 * another view the ordinary way, via hs_view_add_child()/
 * hs_window_add_child(). */
hs_handle hs_scroll_view_create(hs_handle target, const char* name,
	uint32_t resizing_mode, uint32_t flags, bool horizontal, bool vertical,
	uint32_t border);

/* ONLY safe on a scroll view that is not currently attached to a parent --
 * same OWNERSHIP rule as hs_view_destroy(). Destroying an HSScrollView
 * that still has its target attached cascades into the target's own
 * destructor (real ~BView() deletes still-attached children recursively),
 * which fires the target's destroyed callback -- exactly as if the
 * target's own parent view had been destroyed directly. Fires this scroll
 * view's own destroyed callback too, same as any other path to its
 * destruction. */
void hs_scroll_view_destroy(hs_handle scroll_view);

void hs_scroll_view_set_destroyed_callback(hs_handle scroll_view,
	hs_scroll_view_destroyed_callback callback, void* user_data);

/* hs_view_get_frame()/hs_view_move_to()/hs_view_resize_to()/
 * hs_view_add_child()/hs_window_add_child() all work directly against an
 * HSScrollView handle too -- same ABI reasoning as hs_button.h/
 * hs_checkbox.h: BScrollView's BView subobject sits at offset 0, single
 * non-virtual inheritance, confirmed from the real header. Not
 * re-declared here. */

#ifdef __cplusplus
}
#endif

#endif /* HS_SCROLL_VIEW_H */
