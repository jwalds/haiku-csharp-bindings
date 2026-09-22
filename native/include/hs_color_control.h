/*
 * hs_color_control.h -- C shim over BColorControl
 * (headers/os/interface/ColorControl.h), layered on top of BControl
 * (headers/os/interface/Control.h).
 *
 * Same trampoline shape as hs_button.h -- read that file first if you
 * haven't. This comment covers only what's actually different about
 * BColorControl.
 *
 * NO BRect FRAME -- A BPoint START POSITION INSTEAD
 * --------------------------------------------------------------------------
 * Every other concrete control this binding wraps (BButton, BTextControl,
 * BCheckBox, BRadioButton, BSlider) takes a BRect frame in its
 * constructor. Real BColorControl does not: its constructor takes a
 * BPoint start (top-left corner only) plus a color_control_layout and a
 * cellSize, and computes its own width/height internally from those --
 * confirmed on hardware (BPoint(10,10) + B_CELLS_32x8 + cellSize=6.0
 * produced Frame()=(10,10,298,82), not something this shim chose).
 * hs_color_control_create() therefore takes an hs_point, not an hs_rect,
 * and there is no separate width/height parameter to pass -- callers
 * read the real, BeAPI-computed Frame() afterward via the existing
 * hs_view_get_frame() (see below), exactly like every other control.
 *
 * "name" DOES NOT DOUBLE AS Label -- HARDWARE-VERIFIED, NOT ASSUMED
 * --------------------------------------------------------------------------
 * Real BColorControl's constructor has no separate `label` parameter the
 * way BButton/BTextControl/BCheckBox/BRadioButton/BSlider's constructors
 * all do. A probe confirmed Label() reads back NULL immediately after
 * construction with a non-NULL "name" -- BControl's label is a genuinely
 * separate, unset attribute here, not silently populated from "name".
 * Set it explicitly afterward via hs_control_set_label() (ColorControl.cs
 * does this automatically in its constructor, passing through whatever
 * label string -- possibly null -- the caller gave, for parity with every
 * other control's constructor shape) if a label is wanted.
 *
 * A SIXTH VERIFIED BApplication REQUIREMENT
 * --------------------------------------------------------------------------
 * Constructing a BColorControl with no live BApplication anywhere in the
 * process hangs indefinitely -- confirmed with the same isolated,
 * flush-per-step probe technique used for every other widget in this
 * binding, matching TextControl/RadioButton/Slider, not CheckBox.
 *
 * VALUE AS A COLOR: REAL SetValue(rgb_color)/ValueAsColor(), NOT A
 * REIMPLEMENTED BIT-PACKING FORMULA
 * --------------------------------------------------------------------------
 * Real BColorControl stores its color packed into BControl's own int32
 * Value (already exposed generically by hs_control.h/Control.cs -- no
 * new code needed for that half) via an inline, non-virtual
 * SetValue(rgb_color) that packs as
 * (red<<24)+(green<<16)+(blue<<8) (alpha is NOT encoded), and a real
 * ValueAsColor() that unpacks the same way, always returning alpha=255
 * regardless of what alpha was ever passed to SetValue(rgb_color) --
 * confirmed exactly matching this formula on hardware, including the
 * expected int32 sign wraparound once red >= 128 (BControl's Value is a
 * signed int32; e.g. SetValue(rgb_color{200,150,100,255}) reads back as
 * Value()==-929668096, which round-trips back through ValueAsColor()
 * correctly regardless). Rather than reimplement that packing/unpacking
 * arithmetic a second time in C#, hs_color_control_set_value_color() and
 * hs_color_control_value_as_color() below call the real BColorControl
 * methods directly -- both verified on hardware to produce results
 * identical to the formula above, so this is a safety/simplicity choice
 * (one implementation of the packing, not a divergence risk), not a
 * behavior difference from what BControl.Value already gives you.
 *
 * REUSING hs_view_get_frame() AND hs_control_* -- SAME ABI FACT, VERIFIED
 * AGAIN FOR THIS WIDGET
 * --------------------------------------------------------------------------
 * There is no hs_color_control_get_frame()/move_to()/resize_to(), and
 * Label/IsEnabled/the raw int32 Value are NOT declared here -- see
 * hs_control.h and hs_view.h, shared with every other concrete control.
 * The blind cast to BView* / BControl* those files' functions perform is
 * safe here too: BColorControl's single, non-virtual inheritance chain
 * (`class BColorControl : public BControl`) places BControl (and BView
 * beneath it) at offset 0, the same as every other control -- confirmed
 * on hardware for THIS chain specifically (calling BControl::SetValue()/
 * Value() through a BControl* cast of a live BColorControl* correctly
 * reaches BColorControl's own override), not merely assumed to carry
 * over from Button/TextControl/CheckBox/RadioButton/Slider's own,
 * separately-verified probes.
 *
 * NO BMessage/BInvoker/TARGET PLUMBING -- A DIRECT VALUE-CHANGED CALLBACK
 * --------------------------------------------------------------------------
 * Same scope decision as every other control in this binding (see
 * hs_button.h's own "NO BMessage/BInvoker/TARGET PLUMBING" note).
 * HSColorControl overrides Invoke() to fire a direct callback instead of
 * posting anywhere; hs_color_control_create() always constructs the
 * underlying BColorControl with a NULL BMessage*. Real BColorControl
 * invokes on every color pick (there is no separate drag-vs-commit split
 * the way BSlider has -- a click on a ramp or the palette is a single,
 * atomic value change), so there is exactly one callback here, matching
 * Button's shape, not Slider's/TextControl's two-hook shape.
 *
 * SCOPE
 * --------------------------------------------------------------------------
 * Covered: construction (BPoint/layout/cellSize/name/useOffscreen),
 * Label/Value/IsEnabled (via hs_control.h), the color-typed
 * SetValue(rgb_color)/ValueAsColor() pair, CellSize/SetCellSize,
 * Layout/SetLayout(color_control_layout), and the value-changed callback
 * (Invoke()). NOT covered, a deliberate scope decision: SetIcon() (needs
 * a BBitmap binding this project doesn't have, same reason Slider defers
 * its own SetIcon), the BMessage-based constructor/Archive/Instantiate
 * (BArchivable persistence is out of scope everywhere in this binding),
 * SetLayout(BLayout*) (the newer layout API, out of scope everywhere
 * else too), and the raw drawing internals (_DrawColorArea/
 * _DrawSelectors/_DrawColorRamp/etc. -- private anyway, and withheld
 * from every BControl subclass in this binding on principle, same as
 * Slider's own raw drawing internals).
 */
#ifndef HS_COLOR_CONTROL_H
#define HS_COLOR_CONTROL_H

#include "hs_types.h"

#ifdef __cplusplus
extern "C" {
#endif

typedef void (*hs_color_control_value_changed_callback)(void* user_data);
typedef void (*hs_color_control_destroyed_callback)(void* user_data);

/* Create a new HSColorControl (a BColorControl subclass) at the given
 * top-left position, with the given layout, cell size, and name. Has no
 * parent and is not attached to any window yet; safe to configure from
 * whatever thread created it, same threading rule as hs_view.h's.
 * `layout` is the raw color_control_layout value (B_CELLS_4x64=4,
 * B_CELLS_8x32=8, B_CELLS_16x16=16, B_CELLS_32x8=32, B_CELLS_64x4=64 --
 * plain, non-sequential values, verified against the actual installed
 * ColorControl.h, not guessed). Its real, BeAPI-computed frame (there is
 * no frame parameter to pass -- see this file's own header comment) is
 * read afterward via hs_view_get_frame(). Label is NOT set from `name`
 * (see this file's header comment) -- call hs_control_set_label()
 * afterward if a label is wanted. */
hs_handle hs_color_control_create(hs_point start, uint32_t layout,
	float cell_size, const char* name, bool use_offscreen);

/* ONLY safe on a color control that is not currently attached to a
 * parent -- same OWNERSHIP rule as hs_view_destroy(). Fires the
 * destroyed callback, same as any other path to this control's
 * destruction. */
void hs_color_control_destroy(hs_handle color_control);

void hs_color_control_set_value_changed_callback(hs_handle color_control,
	hs_color_control_value_changed_callback callback, void* user_data);

void hs_color_control_set_destroyed_callback(hs_handle color_control,
	hs_color_control_destroyed_callback callback, void* user_data);

/* BControl-level state (Label/the raw int32 Value/IsEnabled) is NOT
 * declared here -- see hs_control.h, shared with every other concrete
 * control this binding wraps. Call hs_control_set_label()/
 * hs_control_value()/hs_control_set_enabled()/etc. directly against a
 * color control handle; safe, see hs_control.h's own rationale for why.
 *
 * BColorControl-specific state -- the color-typed half of Value, and
 * CellSize/Layout. Colors cross the P/Invoke boundary as four raw
 * uint8_t/byte parameters, not an hs_rgb_color struct -- the same
 * convention hs_view.h's SetHighColor/SetLowColor/SetViewColor and
 * hs_slider.h's BarColor/FillColor already established. */
void hs_color_control_set_value_color(hs_handle color_control, uint8_t red,
	uint8_t green, uint8_t blue, uint8_t alpha);
void hs_color_control_value_as_color(hs_handle color_control,
	uint8_t* out_red, uint8_t* out_green, uint8_t* out_blue,
	uint8_t* out_alpha);

void hs_color_control_set_cell_size(hs_handle color_control, float size);
float hs_color_control_cell_size(hs_handle color_control);

void hs_color_control_set_layout(hs_handle color_control, uint32_t layout);
uint32_t hs_color_control_layout(hs_handle color_control);

#ifdef __cplusplus
}
#endif

#endif /* HS_COLOR_CONTROL_H */
