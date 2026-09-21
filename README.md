# haiku-csharp-bindings

C# bindings for Haiku OS's native BeAPI, for use from the [Mono 6.14.1 port
to Haiku](https://github.com/jwalds/haikuports) this project
grew out of. Not affiliated with or endorsed by the Haiku project.

See [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) for open bugs found on real
hardware that aren't fixed yet, what's been ruled out for each, and what's
been tried already -- check there before re-investigating one from
scratch.

## Why a hand-written C shim

BeAPI (`libbe.so`) is a pure C++ API: ordinary C++ classes with virtual
methods, no `extern "C"` surface at all. P/Invoke can only call C-linkage,
C-ABI-compatible functions, so there is no way to call into `libbe` directly
from C# — every binding attempt at this (see "Prior art" below) has needed
either a hand-written C/C++ shim or a C++-parsing generator (CppSharp,
SWIG, pybind11) to produce one automatically. This project writes the shim
by hand: slower to cover a lot of API surface, but every line is
understood, debuggable, and not dependent on a generator's own bug surface.

The one place Haiku *does* expose a real C ABI already is the low-level
kernel layer (`headers/os/kernel/OS.h` — threads, semaphores, ports, areas),
which is directly P/Invoke-able with no shim needed. This project uses one
piece of it directly (`wait_for_thread`) rather than re-wrapping it.

## Prior art (read before extending this)

- **[trungnt2910/dotnet-haiku](https://github.com/trungnt2910/dotnet-haiku)**
  — a 2023 Google Summer of Code project that ported .NET 8 itself to Haiku
  *and* auto-generated bindings for the Application, Interface, Kernel,
  Storage, and Support kits using CppSharp. The most relevant prior work;
  worth reading for the specific bugs it hit (missing constructors on
  generated layout classes, a `BLooper::Quit()` double-free needing a hand
  patch, C++ templates CppSharp couldn't parse). Targets a custom Haiku
  .NET runtime, not stock Mono, so its generated code isn't directly
  reusable here, but its writeup is.
- **[HaikuArchives/Habid](https://github.com/HaikuArchives/Habid)** — a D
  and C hand-written shim over parts of the Support, Storage, Interface,
  and Application kits. The closest existing analog to this project's
  approach. Incomplete, and the author notes trouble with constructors/
  destructors and operator overloads — worth a look for what NOT to do.
- Also relevant: [coolcoder613eb/Haiku-PyAPI](https://github.com/coolcoder613eb/Haiku-PyAPI)
  (pybind11), [return/haiku-api-js](https://github.com/return/haiku-api-js)
  (nbind), and the forum thread
  ["Haiku API bindings for other languages"](https://discuss.haiku-os.org/t/haiku-api-bindings-for-other-languages/7849),
  which is a good primer on why this is hard in general (C++ ownership not
  matching a GC'd/refcounted target language, and letting the target
  language override native virtuals like `MessageReceived()`).

## Current scope

### Application Kit

Covers just enough of `BApplication`/`BLooper`/`BHandler`/`BMessage` to
prove the hardest architectural question works at all on this specific
stack — see "The open question" below.

- `Haiku.App.Application` — wraps a native `BApplication` subclass
  (`HSApplication`, in `native/`). Override `OnMessageReceived`,
  `OnQuitRequested`, `OnReadyToRun`.
- `Haiku.App.Message` -- wraps `BMessage`. Covers the full scalar Add/Find
  set (`int8`/`int16`/`int32`/`int64`/`uint8`/`uint16`/`uint32`/`uint64`/
  `float`/`double`/`bool`/`string`), `Point`/`Rect`/`Size`/`RgbColor`/
  `Alignment` (see `Haiku.App.Geometry` -- minimal placeholders ahead of a
  real Interface Kit wrapper), `pointer` (a raw `IntPtr`, meaningful only
  within your own process), nested `Message`s (`AddMessage`/`FindMessage`),
  a generic `Data` escape hatch for any Haiku `type_code`, the `What`
  field, and the whole-message operations `Has*`/`RemoveName`/`RemoveData`/
  `MakeEmpty`/`IsEmpty`/`CountNames`/`Rename`/`Append`, plus `Replace*` for
  every type above.
- `Haiku.App.SystemMessages` -- a couple of Haiku's own `AppDefs.h`
  constants (`B_QUIT_REQUESTED`, `B_READY_TO_RUN`), packed the same way
  Haiku's own C++ headers pack them.

Not yet covered: `BMessenger`, `BInvoker`, `BMessageFilter`/
`BMessageQueue`/`BMessageRunner`, `BRoster`, filesystem references
(`entry_ref`/`node_ref` -- `AddRef`/`FindRef`/`AddNodeRef`/`FindNodeRef`,
which fit more naturally with a future Storage Kit wrapper), `BMessage`'s
flattened-object Add/Find pair (`AddFlat`/`FindFlat`, which needs
`BFlattenable`), archiving (`BArchivable`), scripting specifiers, delivery/
reply plumbing (`SendReply`, `WasDelivered`, etc. -- these belong more with
`BMessenger`/`BLooper`), and the `Get*`/`Set*` convenience-with-defaults
sugar and indexed (multiple-values-per-name) overloads real `BMessage` also
has.

### Interface Kit (BWindow, BView's "shell + drawing + basic input" slices, and Button/Control)

Five slices so far: `BWindow` (the first), a second, deliberately
scoped-down slice of `BView` -- construction/geometry, being added to and
removed from a window's view hierarchy, the `AttachedToWindow`/
`DetachedFromWindow`/`Draw` hooks, and enough drawing primitives to prove
the round trip (colors, `FillRect`/`StrokeRect`/`StrokeLine`, `DrawString`)
-- a third: basic mouse/keyboard input on top of that same `BView`
(`MouseDown`/`MouseUp`/`MouseMoved`, `KeyDown`/`KeyUp`, `MakeFocus`/
`IsFocus`) -- a fourth: modifier-key state (Shift/Ctrl/Option/Command)
delivered alongside `OnMouseDown`/`OnMouseUp`/`OnKeyDown`/`OnKeyUp`, plus a
standalone `Modifiers.Current` accessor for everywhere else (`OnMouseMoved`
included -- see "BView input" below for why that hook doesn't get one of
its own) -- and now a fifth: `Button`/`Control`, this binding's first
`BControl`-derived widget, with a direct `OnClick` hook rather than
BeAPI's own `BMessage`/`BInvoker`/target plumbing (see "Button/Control"
below for that scope decision, the shared `ViewBase` refactor it required,
and the ABI-offset fact that refactor rests on). `GetMouse()` polling,
drag & drop, layout/`FrameResized`/`FrameMoved`, and every other
`BControl`-derived widget (checkbox, radio button, ...) are deferred to a
follow-up slice -- see "Not yet covered" below. This kit lives in its own
assembly, `Haiku.Interface.dll` (referencing `Haiku.App.dll` for
`Rect`/`Point`/`Message`/`HaikuException`), mirroring how Haiku itself
splits the Application and Interface Kits -- and setting the pattern for
future kits (Storage, etc.) to also get their own assembly.

- `Haiku.Interface.Window` -- wraps a native `BWindow` subclass
  (`HSWindow`, in `native/`). Override `OnMessageReceived`,
  `OnQuitRequested`, `OnDestroyed`. `Show`/`Hide`/`IsHidden`, `Quit`,
  `Lock`/`Unlock`/`IsLocked`, `Title`, `Frame`, `MoveTo`, `ResizeTo`,
  `AddChild`/`RemoveChild` (a `BWindow` is the root of its own view
  hierarchy, exactly like a `BView` is the root of its children's -- see
  `hs_window.h`'s own `hs_window_add_child()` doc). See "BWindow:
  threading, quitting, and destruction" below before touching `Window.cs`
  or `hs_window.cpp` -- its lifecycle is shaped differently from
  `Application`'s in ways that matter.
- `Haiku.Interface.WindowLook`/`WindowFeel`/`WindowFlags` -- Haiku's own
  `window_look`/`window_feel`/flags enums, values copied verbatim from
  `headers/os/interface/Window.h`.
- `Haiku.Interface.ViewBase` -- new abstract base as of the Button/Control
  slice, shared by `View` and `Control` (see "Button/Control" below for
  why this split exists and what it does and doesn't cover). Holds
  `Frame`/`MoveTo`/`ResizeTo`, `Dispose()`/ownership enforcement, and the
  parenting bookkeeping `Window.AddChild`/`RemoveChild` and
  `View.AddChild`/`RemoveChild` both now accept (`ViewBase`, not `View`
  -- a `Control`/`Button` can be added anywhere a plain `View` could be).
- `Haiku.Interface.View` -- wraps a native `BView` subclass (`HSView`, in
  `native/`), deriving from `ViewBase`. Override `OnAttachedToWindow`,
  `OnDetachedFromWindow`, `OnDraw`, `OnDestroyed`, `OnMouseDown`/
  `OnMouseUp`/`OnMouseMoved`, `OnKeyDown`/`OnKeyUp`. `AddChild`/
  `RemoveChild` (nested views/controls), `Bounds`, `SetHighColor`/
  `SetLowColor`/`SetViewColor`, `FillRect`/`StrokeRect`/`StrokeLine`/
  `DrawString`, `MakeFocus`/`IsFocus`, `Invalidate` (on top of `ViewBase`'s
  shared `Frame`/`MoveTo`/`ResizeTo`). See "BView: no thread of its own,
  and stricter ownership" and "BView input" below before touching
  `View.cs` or `hs_view.cpp` -- and see
  [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #4 before adding an automated
  test that polls for `Draw()` (or, by the same reasoning, a mouse/
  keyboard hook) firing from a thread other than the one running
  `Application.Run()`.
- `Haiku.Interface.ViewFlags` -- Haiku's `B_WILL_DRAW`/`B_FRAME_EVENTS`/...
  bitmask, values copied verbatim from `headers/os/interface/View.h` (a
  real `[Flags]` enum -- these are independent bits).
- `Haiku.Interface.ViewResizingMode` -- Haiku's `B_FOLLOW_*` constants.
  Deliberately NOT a `[Flags]` enum, unlike `ViewFlags` above: these are
  macro-computed in `View.h` (a `_rule_(...)` macro), not independent bits
  -- `B_FOLLOW_ALL` is not simply `Left | Right | Top | Bottom` OR'd
  together. Every value here was verified against a small native scratch
  program compiled and run on real Haiku hardware, not hand-computed from
  the macro, per this project's "verify, don't assume" rule.
- `Haiku.Interface.MouseButtons` -- Haiku's `B_PRIMARY_MOUSE_BUTTON`/
  `B_SECONDARY_MOUSE_BUTTON`/`B_TERTIARY_MOUSE_BUTTON` bitmask, values
  copied verbatim from `View.h`'s `B_MOUSE_BUTTON(n) = 1 << (n-1)` (a real
  `[Flags]` enum -- independent bits). Delivered by `OnMouseDown`/
  `OnMouseMoved`; see "BView input" below for why `OnMouseUp` does NOT get
  one.
- `Haiku.Interface.MouseTransit` -- Haiku's `B_ENTERED_VIEW`/`B_INSIDE_VIEW`/
  `B_EXITED_VIEW`/`B_OUTSIDE_VIEW` constants, delivered as `OnMouseMoved`'s
  second parameter. Deliberately NOT a `[Flags]` enum, unlike
  `MouseButtons` above -- sequential and mutually exclusive.
- `Haiku.Interface.KeyBytes` -- single-byte control-character constants
  (`Escape`, `Backspace`, the arrow keys, ...) copied verbatim from
  `headers/os/interface/InterfaceDefs.h`, for comparing against
  `OnKeyDown`/`OnKeyUp`'s raw `byte[]`. Does not cover function keys
  (F1-F12); see "BView input" below.
- `Haiku.Interface.ModifierKeys` -- Haiku's `B_SHIFT_KEY`/`B_COMMAND_KEY`/
  `B_CONTROL_KEY`/... bitmask, values copied verbatim from
  `headers/os/interface/InterfaceDefs.h` (a real `[Flags]` enum --
  independent bits). Delivered by `OnMouseDown`/`OnMouseUp`/`OnKeyDown`/
  `OnKeyUp`; see "BView input" below for why `OnMouseMoved` does NOT get
  one.
- `Haiku.Interface.Modifiers.Current` -- wraps Haiku's standalone
  `modifiers()` global function, for reading modifier-key state from
  `OnMouseMoved` or anywhere else outside the four hooks above (a timer,
  `OnDraw`, ...). Safe to call from any thread; see "BView input" below.
- `Haiku.Interface.Control` -- new abstract base for `BControl`-derived
  widgets, deriving from `ViewBase` (NOT `View` -- see "Button/Control"
  below for why). `Label`/`Value`/`IsEnabled`, all plain C# properties
  (matching `Window.Title`'s precedent, not `View.MakeFocus`/`IsFocus`'s
  two-separate-members shape -- see the doc comment atop `Control.cs`).
- `Haiku.Interface.Button` -- wraps a native `BButton` subclass
  (`HSButton`, in `native/`), deriving from `Control`. `OnClick` (a direct
  hook, no `BMessage`/target involved), `MakeDefault`/`IsDefault`,
  `IsFlat`, `Behavior`. See "Button/Control" below before touching
  `Button.cs`, `Control.cs`, or `hs_button.cpp`.
- `Haiku.Interface.ButtonBehavior` -- Haiku's `BButton::BBehavior`
  (`B_BUTTON_BEHAVIOR`/`B_TOGGLE_BEHAVIOR`/`B_POP_UP_BEHAVIOR`), a plain
  sequential enum (0/1/2), verified against the actual installed
  `Button.h`. Deliberately NOT a `[Flags]` enum -- mutually exclusive
  behaviors, same shape as `MouseTransit`. `PopUpMenu` is included for
  enum parity but isn't fully usable through this binding yet -- see its
  own doc comment.

Not yet covered: `GetMouse()` polling, drag & drop, function-key
identification, `FrameResized`/`FrameMoved`, layout, scrolling, fonts
beyond the current default, custom drawing patterns (`FillRect`/
`StrokeRect`/`StrokeLine` always use `B_SOLID_HIGH`; see `hs_view.h`'s
DRAWING note), `AddChild`'s `before` (insert position) parameter, any
`BControl`-derived widget other than `Button` (checkbox, radio button,
slider, ...), and any real `BMessage`/`BInvoker`/target-based invocation
(`Button`'s `OnClick` is a direct callback instead -- see "Button/Control"
below) -- deliberately deferred to a follow-up slice rather than folded
into this one. Also not yet covered: everything else in Interface Kit
(~50 other classes), `BScreen`, `BDirectWindow`.

## The open question this slice exists to answer

`BLooper::Run()` spawns a **new native OS thread** for the message loop and
returns immediately — every callback (`OnReadyToRun`, `OnMessageReceived`,
`OnQuitRequested`) fires **on that thread**, which Mono never created and
knows nothing about. In practice, calling a marshaled managed delegate
through a native function pointer should cause Mono's embedding layer to
auto-attach an unrecognized calling thread on first entry — but that's an
expectation carried over from how Mono's embedding API generally behaves,
**not something verified on this exact Mono 6.14.1-on-Haiku build yet**.

`managed/Sample/Program.cs` exists to demonstrate this, and
`managed/Tests/ApplicationTests.cs`'s
`ReadyToRunMessageAndQuitRequestedAllFireInOrder` is the same scenario as an
actual regression test (see "Testing" below): if it passes, the cross-thread
callback story holds up here and the rest of this plan (Interface Kit, etc.)
can proceed on solid ground. If it hangs or crashes instead, that's the very
first thing to debug — everything else in this binding depends on it.

That question came back in a different shape once `BWindow` entered the
picture -- see the next section.

## BWindow: threading, quitting, and destruction

Three things about `BWindow`'s lifecycle are different enough from
`BApplication`'s that they shaped `Window.cs`/`hs_window.h`/`hs_window.cpp`'s
whole design, each one verified against real Haiku source/docs rather than
assumed (this project has been burned by assuming before -- see the git
history):

1. **The thread doesn't exist until `Show()`.** Unlike `BApplication`,
   whose `Run()` spawns its message-loop thread immediately, a freshly
   constructed `BWindow` has no running thread at all -- every constructor
   delegates to a private `_InitData()` that never spawns one. The Haiku
   Book is explicit: "windows are hidden by default, you must call `Show()`
   ... If this is the first time `Show()` has been called ... the message
   loop is started." So it's safe to configure a `Window` (`SetTitle`,
   `MoveTo`, a future `AddChild`, ...) right after construction, on
   whatever thread created it, with no locking needed -- there's no other
   thread yet to race with. `Show()`'s first call is the one moment that
   changes, exactly like `BApplication.Run()`'s threading note above.
2. **`Quit()` posts a message; it doesn't call `BWindow::Quit()`
   directly.** `BWindow::Quit()` (overriding `BLooper::Quit()`) requires
   the caller to already hold the window's lock -- verified against
   `src/kits/interface/Window.cpp`, which logs an error and only survives
   via a defensive fallback `Lock()` that can still fail if called
   unlocked. Rather than take on that locking protocol from arbitrary
   calling threads, `hs_window_quit()` posts `B_QUIT_REQUESTED` through the
   normal message queue instead -- exactly what a window's own close box
   does internally, and documented thread-safe from any thread, including
   the window's own. `Window.Quit()` is this: async, safe to call from
   anywhere, and it doesn't block waiting for the window to actually die.
3. **There's no `Application.Run()`-style blocking call to learn a window
   died, so there's a `Destroyed` callback instead.** A shown window lives
   and dies independently of whatever thread created it -- nothing blocks
   waiting for it the way `Run()` blocks for the app. `HSWindow` overrides
   its own C++ destructor (not a `BWindow` virtual -- none exists for
   "about to be deleted") and fires a registered callback unconditionally,
   right there, covering both ways an `HSWindow` can die with one hook: the
   normal quit flow (`QuitRequested()` returns true, `BLooper`'s own
   thread-exit machinery does `delete this` **on the window's own thread**
   some time later) and the "never shown, changed my mind" cleanup path
   (`Window.Dispose()` calling `hs_window_destroy()`, which deletes
   synchronously on whatever thread called it). `Window.OnDestroyed()` is
   this callback; by the time it fires, no other method on that `Window`
   is safe to call.

**Ownership rule that falls out of all three:** `Window.Dispose()` is only
safe to call synchronously (`hs_window_destroy`) if `Show()` was never
called -- no thread exists yet. Once shown, the only safe teardown is
`Quit()` -- calling the synchronous destroy on a shown window would race
with its own live thread. `Window.cs` tracks whether `Show()` was ever
called and picks the right one automatically; you don't need to.

**Two more facts, found empirically while building `WindowTests.cs`, that
don't yet have a full explanation but are real and worth knowing before you
hit them yourself:**

- **A `BApplication` that has been `Run()` all the way through a
  `QuitRequested()`-returns-true cycle is a one-shot event for the *whole
  process*, not just for that object.** Once that's happened, no further
  `BApplication` can ever be constructed again in the same process --
  not even a plain one you never intend to `Run()`. Constructing and
  disposing a `BApplication` that's never been `Run()` has no such effect
  and can be repeated freely. See `Application.cs`'s "ONE-SHOT PER
  PROCESS" remarks for the full writeup and how it was pinned down. This
  is why `managed/Tests/ApplicationTests.cs` is tagged `[TestOrder(100)]`
  -- it has to run dead last in `Tests.exe`, after anything else in the
  suite that needs a `BApplication` of its own.
- **Overriding `Window.OnQuitRequested()` in a test whose process later
  runs a real `Application.Run()` cycle reliably triggers the same
  one-shot poisoning, even though the underlying native callback fires
  either way** (`Window.cs` always wires up the native quit-requested
  callback, whether or not a subclass overrides the C# hook). Bisected
  against the real files, not guessed: removing just the override (and
  keeping everything else identical, including a same-shaped `OnDestroyed`
  override that does *not* cause the problem) fixes it. See
  `managed/Tests/WindowTests.cs`'s remarks for the detail and what was
  ruled out. The likely mechanism is some interaction between Mono's
  embedding layer and a foreign (Haiku-spawned, not Mono-created) thread
  that calls into managed code and then exits without an explicit Mono
  detach -- the same category of "thread Mono never created" concern this
  README has flagged since the Application Kit slice -- but it isn't
  nailed down yet. Treat this as an open item, not a closed one.

**None of this section's two "found empirically" facts are fixed yet --
see [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issues #1 and #2 for the full
writeup, what's been ruled out, and (for issue #2's suspected root cause)
a related, also-unresolved investigation in issue #3.**

## BView: no thread of its own, and stricter ownership

`BView` looked at first like it would need the same threading analysis as
`BWindow` above. It doesn't, for a simpler reason: **a `BView` never spawns
anything of its own.** Every hook (`AttachedToWindow`, `DetachedFromWindow`,
`Draw`) fires on whichever thread is running the OWNING WINDOW's message
loop -- the same thread `Window`'s own callbacks already fire on -- and the
Haiku Book says app_server automatically locks that `BWindow` before
calling any hook method, so there's no separate locking story to work out
here. A view that hasn't been added to a window yet (or whose window has
never been shown) has no thread delivering anything to it at all, exactly
like a freshly-constructed, not-yet-shown `Window` -- safe to configure
(`MoveTo`, `SetHighColor`, ...) right after construction, on whatever
thread created it.

Two things ARE specific to `BView`, both shaped by the same "no hook for
about-to-be-deleted" gap `BWindow`'s own destroyed-callback works around:

1. **`AddChild()`/`RemoveChild()` exist on both `Window` and `View`** -- a
   window is the root of its own view hierarchy, exactly like a view is the
   root of its nested children's (confirmed directly from `Window.h`/
   `View.h`, not assumed). `AttachedToWindow()` fires **synchronously**, on
   whatever thread calls `AddChild()`, even before the window has ever been
   shown -- see `hs_window.h`'s own `hs_window_add_child()` doc for why
   that's safe pre-`Show()` specifically (no thread running yet to race
   with).
2. **`Dispose()` is stricter than `Window.Dispose()`.** A `BWindow` shown or
   not, `Window.cs` can always pick a safe teardown path automatically. A
   still-attached `View` has no such fallback -- deleting it directly while
   its parent still references it would leave a dangling pointer in that
   parent's child list, and unlike `Window`, there's no native-side
   rejection of the mistake to lean on. `View.Dispose()` tracks whether it's
   currently attached and **throws `InvalidOperationException`** rather
   than risk it, instead of silently doing the wrong thing. `RemoveChild()`
   it from its parent first, or just let the parent's own destruction
   cascade-destroy it (see `hs_view.h`'s OWNERSHIP note for the three death
   paths this covers with one `OnDestroyed` hook: explicit `Dispose()`,
   explicit detach-then-dispose, and the implicit cascade).

**A real threading question about `Draw()` was found while building this
slice, and has since been fixed -- see [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)
issue #4.** Short version: one specific automated-test shape (showing a
window from a thread other than the one running `Application.Run()`, with
`Application` never `Run()` at all, then polling for `Draw()` from that
same outside thread) reliably hung the whole test process the first time
`Draw()` fired, for reasons never root-caused. Real app usage -- window
and view built inside `OnReadyToRun()`, same thread that calls `Run()` --
was separately confirmed NOT to hit this: see `managed/Sample/Program.cs`'s
`DemoView`, verified both by console output and by an actual screenshot of
its drawn content. That real-usage pattern turned out to be the fix, not
just a workaround: `managed/Tests/ApplicationTests.cs` now builds and
`Show()`s a probe window the same way, and `Draw()` firing with a sane
update rect is real, automated, `[TestOrder(100)]` coverage today --
`ViewTests.cs` itself still can't host it directly (see its own class
remarks and issue #4's "Fixed" write-up for why: only one test in the
whole process may ever run a full `Run()`-to-quit cycle, per issue #1, and
`ApplicationTests.cs` already owns that slot).

## BView input: threading, two field asymmetries, and what's not covered

Mouse and keyboard hooks (`OnMouseDown`/`OnMouseUp`/`OnMouseMoved`/
`OnKeyDown`/`OnKeyUp`) fire on exactly the same thread as `Draw()` and the
attach/detach hooks -- the owning window's message-loop thread -- for
exactly the same reason: a `BView` has no thread of its own (see "BView:
no thread of its own" above). Nothing new to work out there.

Three things ARE specific to this slice, all found by reading the actual
installed headers and the Be Book rather than assumed from BeOS-era memory:

1. **`B_MOUSE_UP` does not carry a `buttons` field.** `B_MOUSE_DOWN` and
   `B_MOUSE_MOVED` both do (confirmed against the Be Book's
   message-constants documentation), so `OnMouseDown`/`OnMouseMoved` both
   take a `MouseButtons buttons` parameter -- but `OnMouseUp` does not take
   one at all. Watch for this if you're used to another toolkit that hands
   every mouse hook the same signature.
2. **`MakeFocus`/`IsFocus` work regardless of `ViewFlags`.** `B_NAVIGABLE`
   (already in `ViewFlags`) only affects Tab-key auto-cycling between
   views, not whether a direct `MakeFocus(true)` call takes effect --
   verified against `View.h`, not assumed. A view must have focus
   (`IsFocus == true`) to receive `OnKeyDown`/`OnKeyUp` at all.
3. **`B_MOUSE_MOVED` does not carry a `modifiers` field -- the mirror
   image of the asymmetry above.** `B_MOUSE_DOWN`, `B_MOUSE_UP`,
   `B_KEY_DOWN`, and `B_KEY_UP` all document one (confirmed against the
   Be Book), so `OnMouseDown`/`OnMouseUp`/`OnKeyDown`/`OnKeyUp` all take a
   `ModifierKeys modifiers` parameter -- but `OnMouseMoved` does not take
   one at all. Use the standalone `Modifiers.Current` accessor (wraps
   Haiku's `modifiers()` global function, safe to call from any thread)
   from `OnMouseMoved`, or from anywhere else that isn't one of those
   four hooks. This binding could not cross-check this specific asymmetry
   against real Haiku source on this machine (no source checkout present,
   and scanning the whole filesystem for one timed out) -- it rests on the
   Be Book documentation plus the same coherent rationale BeOS itself
   likely had (`MouseMoved` fires at high frequency; modifier state is
   already cheap to read on demand via the standalone accessor, so there's
   no need to pay for it on every move event).

`Invalidate()` was also added to `View` in the mouse/keyboard slice, even
though it's not part of the minimal BeAPI surface this binding otherwise
sticks to -- without some way to ask app_server for a redraw, a
mouse/keyboard-driven view (that slice's whole point) would have no way to
make what it last drew stale. It's a thin wrapper over
`BView::Invalidate()`: schedules a redraw on the owning window's thread,
does not draw synchronously.

**Verifying that the hooks actually fire, and that the modifiers they
carry (or that `Modifiers.Current` returns) reflect the keys really being
held, has no automated or synthetic path in this project's current
environment**, for the same reason `Draw()`'s own regression coverage
stops where it does (see [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #4 and
`ViewInputTests.cs`'s own class remarks): there is no supported way from
inside the same process to synthesize a real mouse/keyboard input event
without either driving actual hardware or hand-constructing and posting
raw `BMessage`s to a specific view from a second process -- both out of
scope here, and this development environment has no input-injection tool
(no `xdotool` or equivalent) to reach for either. What IS automated is
everything that doesn't depend on an actual input event arriving --
`MakeFocus`/`IsFocus` round-tripping, `Invalidate()` not throwing, the
`MouseButtons`/`MouseTransit`/`KeyBytes`/`ModifierKeys` values themselves,
and `Modifiers.Current` not throwing (see `ViewInputTests.cs`, module
`BView Input`). Real hook-firing -- and real modifier state -- is verified
the same way `Draw()` was: by running `Sample.exe` and watching it happen
-- move the mouse over its window, click, type, and hold Shift/Ctrl/
Option/Command, and both the console (`[6]` lines) and the view's own
on-screen text (including a live "Modifiers held" line) update. If you
have physical or remote-desktop access to the Haiku machine, `Sample.exe`'s
`DemoView` is written so that just interacting with it IS the
verification.

## Button/Control: a direct OnClick hook, a shared ViewBase, and an ABI fact verified on hardware

`Button` is this binding's first `BControl`-derived widget, and it forced
two real design decisions plus one thing that had to be verified on real
hardware rather than assumed, all covered here.

**Why `OnClick` instead of BeAPI's own `BMessage`/`BInvoker`/target
plumbing.** Real BeAPI delivers a click by having `BControl` (via its
second base, `BInvoker`) post a `BMessage` to a target `Handler`/`Looper`
(the owning `BWindow`, by default) when `Invoke()` runs -- the same
message-passing shape `Window.OnMessageReceived` already exists for. This
binding does not expose any of that for `Button`: `hs_button_create()`
always constructs the underlying `BButton` with a `NULL` `BMessage*`, and
`HSButton` overrides `Invoke()` itself to fire a plain callback directly
instead (see `hs_button.h`'s "NO BMessage/BInvoker/TARGET PLUMBING" note).
`Button.OnClick()` is a direct virtual hook, matching the C#-idiomatic
style `View`'s own hooks (`OnMouseDown`/`OnDraw`/...) already established,
rather than requiring a `BMessage` and a `Window.OnMessageReceived`
override for the common one-button case. This was a deliberate scope
decision, made before writing any code (see this project's own history
for how "which hooks get which parameters" questions get resolved) --
the tradeoff is that `ButtonBehavior.PopUpMenu` (which needs a configured
pop-up `BMessage` to have anything to show) isn't fully usable through
this binding yet; it's included in the enum for parity, not because it
works end to end.

**Why `ViewBase` exists.** `View`'s constructor always calls
`hs_view_create()` and its `Dispose()` always calls `hs_view_destroy()`
-- neither can be reused as-is for a `Button`, whose native handle comes
from `hs_button_create()`/`hs_button_destroy()` instead. Calling
`hs_view_destroy()` on a button handle would `delete` through a
mismatched static type -- genuine undefined behavior, not just a style
mistake, since `HSView` and `HSButton` are unrelated concrete classes
that just happen to share `BView` as a common ancestor. `ViewBase` is the
new shared abstract root that holds exactly what's safe and meaningful
for ANY concrete native view/control handle -- `Frame`/`MoveTo`/
`ResizeTo`, the destroyed-callback/`GCHandle` bookkeeping, and
`Dispose()`'s ownership enforcement, built around a `protected abstract
DestroyNativeHandle(IntPtr)` seam each subclass fills in (`View.cs` calls
`hs_view_destroy()`, `Control.cs` calls `hs_button_destroy()`). `View`
keeps everything `ViewBase` doesn't cover (`Draw`, the mouse/keyboard
hooks, `FillRect`/`StrokeRect`/`StrokeLine`/`DrawString`, `MakeFocus`/
`IsFocus`, `Invalidate`, `Bounds`) -- `Control` never inherits any of
that, because the native shim never wires up `hs_view_set_*_callback()`
for a button's handle in the first place (a native `BButton` draws and
handles input entirely on its own; there is nothing for those callbacks
to report). `Window.AddChild`/`RemoveChild` and `View.AddChild`/
`RemoveChild` now all accept `ViewBase`, so a `Control`/`Button` can be
added anywhere a plain `View` could be -- verified by
`ButtonTests.AddChildUnderWindowSucceeds` AND
`AddChildUnderPlainViewSucceeds` (a `Button` nested inside a plain `View`,
not just directly under a `Window`).

**The ABI fact that reuse rests on, verified on hardware.** `Frame`/
`MoveTo`/`ResizeTo` (in `ViewBase`) and `AddChild`/`RemoveChild`'s child
parameter (in `hs_view.cpp`) all cast the opaque handle straight to
`BView*`, regardless of whether the real object behind it is an `HSView`
or an `HSButton` -- safe only because every class in both chains inherits
`BView` as its first, non-virtual base, which the Itanium C++ ABI this
binding builds under places at offset 0. That's true for `HSView`
(documented in `hs_window.cpp` since the mouse/keyboard slice), but
`BControl` -- `BButton`'s parent -- uses MULTIPLE inheritance
(`class BControl : public BView, public BInvoker`), so it needed its own
check rather than assuming the same reasoning carried over. A small
native probe (construct a real `HSButtonProbe`, `static_cast` it to each
base, print the resulting addresses) was compiled and run on the actual
Haiku box before any of this binding's own code relied on the answer:

```
HSButtonProbe*  = 0x7707dc9380
as BView*       = 0x7707dc9380 (offset 0)
as BControl*    = 0x7707dc9380 (offset 0)
as BButton*     = 0x7707dc9380 (offset 0)
as BInvoker*    = 0x7707dc9490 (offset 272)
reinterpret_cast<BView*>(void*) == static_cast<BView*>(button): MATCH
```

`BView` (and `BControl`, and `BButton`) all sit at offset 0 -- `BInvoker`,
`BControl`'s OTHER base, sits at a nonzero offset instead, which is
exactly why this binding never touches `BInvoker` through a blind handle
cast (there is no safe way to reach it that way, and nothing in this
binding needs to -- see the `OnClick` note above). The last line confirms
the actual pattern `hs_view_add_child()`/`hs_window_add_child()`/
`ViewBase`'s geometry functions all use -- a blind `reinterpret_cast` on
the raw opaque handle -- lands on exactly the same address a properly
computed `static_cast` would. `hs_view_get_bounds()` was deliberately
NOT extended this way (nothing needs a control's own `Bounds()` yet), and
neither were `FillRect`/`StrokeRect`/`StrokeLine`/`DrawString`/any
`hs_view_set_*_callback()` -- the latter genuinely would be unsafe against
an `HSButton` handle (they write to `HSView`-specific fields that don't
exist at that layout on a real `HSButton` object), not just meaningless.

**Verification.** `ButtonTests.cs` (module `BButton`) covers everything
that doesn't need a real click: construction/geometry (through
`ViewBase`), `Label`/`Value`/`IsEnabled`/`IsDefault`/`IsFlat`/`Behavior`
round-tripping, the `ButtonBehavior` enum's exact values, and the same
ownership/parenting rules `ViewTests.cs` already covers for `View`
(`AddChild`/`RemoveChild`, `Dispose()`-while-attached throwing, cascade-
on-parent-destroy) -- re-verified here since `Button` now goes through
`ViewBase` instead of duplicating `View`'s own implementation. Real click-
firing has no automated coverage, same reasoning as every other input
hook in this binding (see `ButtonTests.cs`'s own class remarks) --
verified instead by running `Sample.exe`'s `DemoButton` (a real "Click
Me" button beneath `DemoView` that updates its own label with a running
click count and disables itself after three clicks, to make `IsEnabled`'s
effect visible on screen) and confirmed with a real screenshot on the
Haiku box (`screenshot -s -f png`, run silently to avoid the interactive
save dialog blocking over SSH) showing the button rendered correctly,
positioned beneath `DemoView` with the expected native 3D-bevel look.

## Building and running (on Haiku)

Needs `g++` (or another Haiku-supported C++ compiler), the Mono 6.14.1 port
(`mcs`/`mono` from the `haiku-port-6.14.1` mono recipe), and `libbe`'s
development headers (part of `haiku_devel`, already a build requirement of
the mono port).

```
./build.sh
LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH" mono Sample.exe
```

(Haiku's own dynamic loader does not use `LD_LIBRARY_PATH` the way Linux does --
it is a BeOS-derived system and its runtime_loader looks at `LIBRARY_PATH`
instead. Setting `LD_LIBRARY_PATH` here is silently ignored, which is why an
otherwise-correct build can still fail to find `libhaikusharp.so` with a
`DllNotFoundException` at run time. Using an absolute path for the `native`
directory, rather than a bare relative `native`, avoids any ambiguity about
what the loader resolves a relative `LIBRARY_PATH` entry against.

The explicit system lib directories in that command (rather than just
appending the ambient `$LIBRARY_PATH`) matter more than they look: Haiku's
`SetupEnvironment` boot script, which normally populates `LIBRARY_PATH` with
those same paths, only runs for a desktop session. An SSH login shell does
not get it, so `$LIBRARY_PATH` there starts out empty, and appending an
empty variable to your own native directory finds *only* your own directory
-- silently missing system libraries like `libbsd.so` that `libnetwork.so`
needs. Spelling out the full path explicitly works the same whether you are
sitting at Haiku's own Terminal or running this over SSH.)

`Sample.exe` is now a small windowed app (see `managed/Sample/Program.cs`):
it shows a real `BWindow` containing a `DemoView` that actually draws
something (a filled/stroked rect and a line of text, via real `BeAPI`
calls), takes keyboard focus and tracks mouse/keyboard input live (see
"BView input" above), a real `DemoButton` ("Click Me") beneath it that
updates its own label with a running click count and disables itself
after three clicks (see "Button/Control" above), and waits for you to
close it (its title bar's close box), at which point
`WindowFlags.QuitOnWindowClose` signals the owning `BApplication` to quit
too. Expected output:

```
[1] OnReadyToRun fired -- creating and showing the demo window.
[2] DemoView attached to its window.
[2b] Window shown -- move/click the mouse over it, type, or click the button, close it (its title bar's close box) to quit.
[3] DemoView.OnDraw fired, updateRect=(0, 0, 360, 190)
[5] Application OnQuitRequested fired -- allowing shutdown.
App exited cleanly.
```

Moving the mouse over the window, clicking, and typing each print an
additional `[6] ...` line (e.g.
`[6] MouseDown at (123, 45), buttons=Primary, modifiers=Shift`,
`[6] KeyDown: Escape (0x1b), modifiers=None`) and redraw the view's own
on-screen "Last event"/"Mouse position"/"Last key down"/"Last key up"/
"Modifiers held" text live -- the last of those is refreshed from
`Modifiers.Current` on every `OnMouseMoved`, since (per "BView input"
above) that hook doesn't get a `modifiers` parameter of its own. Try
holding Shift/Ctrl/Option/Command while moving the mouse or typing -- that
interaction is the actual verification for this slice's mouse/keyboard/
modifier hooks, since (per "BView input" above) there's no automated or
synthetic way to fire them in this project's environment.

Clicking the "Click Me" button prints `[7] DemoButton clicked, count=N`
and updates the button's own label to "Clicked N times" -- after the
third click it prints an additional `[7] DemoButton disabled itself
(IsEnabled = false) after the 3rd click.` line, changes its label to
"Disabled after 3 clicks", and visibly grays out (a real `BButton`'s own
disabled rendering, not anything this binding draws itself). That's the
actual verification for `OnClick`/`IsEnabled`, since (per "Button/Control"
above) there's no automated or synthetic way to fire a real click either.

(`[4]`, printed from `DemoWindow.OnDestroyed()`, only appears if the window
itself gets torn down as part of that shutdown -- which happens when you
close it via its own close box, but not necessarily if the application is
asked to quit some other way, e.g. `hey <signature> QUIT` from a shell,
since that quits the app directly rather than going through the window's
own close-box path. Either way `[1]`/`[2]`/`[2b]`/`[3]`/`[5]`/"App exited
cleanly" is the proof that matters: a real window was created and shown, a
real view inside it actually drew something -- confirmed not just from this
log but from an actual `screenshot -s` of the running app during this
slice's development -- and the app shut down cleanly afterward.

If the window appears but stays entirely black (no fill color, no text) and
`[3]` never prints, that's very likely Haiku's own `screen_blanker`
(screensaver) covering the window rather than a bug in this binding -- see
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #4 for how that red herring was
found and ruled out. Move the mouse or kill `screen_blanker` and try again
before assuming `Draw()` itself is broken.)

You may also sometimes see a line like this print *after* "App exited
cleanly.":

```
abort_threads: Failed aborting id: 0x9ed12ed000, mono_thread_manage will ignore it
```

This is a known, non-fatal Mono runtime warning printed during process
shutdown -- it does not indicate a hang or a failed run. See
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #3 for the full root-cause
writeup (confirmed against the actual mono source) and why the seemingly
obvious fix for it crashes instead.

## Testing

`managed/Tests/` builds to `Tests.exe`, a small console app that runs every
`[Test]`-tagged method it finds and prints a pass/fail/error summary,
exiting nonzero if anything didn't pass:

```
./build.sh
LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH" mono Tests.exe
```

Output is grouped by module and printed as it happens -- each test class's
`[TestModule("...")]` name (see `managed/Tests/TestModuleAttribute.cs`)
gets its own header, and every test's name is written out, flushed, and
left on screen BEFORE that test runs, with its result appended once known:

```
== BMessage ==
  Int8RoundTrips ... PASS
  Int16RoundTrips ... PASS
  ...

== BView Input ==
  IsFocusDefaultsFalseBeforeMakeFocusIsCalled ... PASS
  FocusRoundTripsWhileAttachedToUnshownWindow ... PASS
  ...

== BView ==
  ConstructionAndGeometryRoundTrip ... PASS
  AddChildFiresAttachedToWindowSynchronously ... PASS
  ...

== BButton ==
  ConstructionAndGeometryRoundTrip ... PASS
  LabelRoundTrips ... PASS
  ...

== Interface Kit ==
  QuitPostsRequestAndFiresDestroyedCallback ... PASS

== Application Kit ==
  ReadyToRunMessageAndQuitRequestedAllFireInOrder ... PASS

64 passed, 0 failed, 0 errored
```

That ordering is deliberate, and no longer just a convenience: test
classes run in ascending `[TestOrder(n)]` order (default `0`, see
`managed/Tests/TestOrderAttribute.cs`), NOT whatever order reflection
happens to hand them back. `ApplicationTests` is tagged `[TestOrder(100)]`
specifically so it always runs dead last -- see "BWindow: threading,
quitting, and destruction" above for why running a real `Application.Run()`
cycle anywhere but last would silently break every `BApplication`-needing
test after it, for the rest of the process. Independent tests should never
need `[TestOrder]`; it exists for that one real constraint, not as a
general-purpose knob.

Separately, `ApplicationTests` blocks for a moment on a real native message
loop, so if a future change ever made it hang, you'd see its name sitting
on screen with no result yet, telling you exactly which test to look at,
rather than silence until it either finishes or you give up waiting.

Pass a substring to run just one module, matched against either the
`[TestModule]` name or the bare class name -- `mono Tests.exe BMessage` and
`mono Tests.exe Message` both run only `MessageTests`. Six modules exist
today:

- `BMessage` (class `MessageTests`) -- one small, fast, isolated test per
  `BMessage` Add/Find pair or whole-message operation (see
  `managed/Tests/MessageTests.cs`). Add a new one here alongside every new
  `hs_message.h` function.
- `BView` (class `ViewTests`) -- construction/geometry, `AddChild`/
  `RemoveChild` and the attach/detach hooks they fire, and the ownership
  rules `Dispose()` enforces (see `managed/Tests/ViewTests.cs`). Every test
  here uses an unshown `Window` (see "BView" above for why that's enough to
  exercise `AttachedToWindow`/`DetachedFromWindow`). Does NOT include a
  `Draw()`-firing test -- that lives in `Application Kit` below instead
  (see [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #4's "Fixed" write-up and
  this file's own class remarks for why it has to live there).
- `BView Input` (class `ViewInputTests`) -- `MakeFocus`/`IsFocus`
  round-tripping, `Invalidate()` not throwing, the `MouseButtons`/
  `MouseTransit`/`KeyBytes`/`ModifierKeys` values themselves, and
  `Modifiers.Current` not throwing (see `managed/Tests/ViewInputTests.cs`).
  Deliberately does NOT include a test that actually fires `OnMouseDown`/
  `OnMouseUp`/`OnMouseMoved`/`OnKeyDown`/`OnKeyUp`, or one that asserts a
  particular `ModifierKeys`/`Modifiers.Current` bit is set -- read that
  file's class remarks and "BView input" above before trying to add one.
- `BButton` (class `ButtonTests`) -- construction/geometry (through
  `ViewBase`), `Label`/`Value`/`IsEnabled`/`IsDefault`/`IsFlat`/`Behavior`
  round-tripping, the `ButtonBehavior` enum's exact values, and the same
  `AddChild`/`RemoveChild`/`Dispose()`-while-attached/cascade-on-destroy
  ownership rules `BView` covers for `View` -- re-verified here for
  `Button` specifically, including a `Button` nested under a plain `View`
  (not just directly under a `Window`), since `Button` now goes through
  the shared `ViewBase` rather than duplicating `View`'s own
  implementation (see "Button/Control" above). Deliberately does NOT
  include a test that actually fires `OnClick` -- read
  `managed/Tests/ButtonTests.cs`'s own class remarks and "Button/Control"
  above before trying to add one.
- `Interface Kit` (class `WindowTests`) -- `BWindow`'s `Quit()`/
  `OnDestroyed()` lifecycle (see `managed/Tests/WindowTests.cs`), including
  the poll-with-timeout pattern needed because there's no blocking
  "wait for this window" call. Read its class remarks before adding a
  second test here or overriding `OnQuitRequested` in a probe window --
  see "BWindow" above.
- `Application Kit` (class `ApplicationTests`) -- the threading proof from
  "The open question" above, as an actual regression test rather than
  something you verify by eye, PLUS this project's only automated `Draw()`
  -firing test (see [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) issue #4): a probe
  window and view are built inside `OnReadyToRun()`, on the same thread
  that calls `Run()`, and the view's own `OnDraw()` is what ends the test
  by calling `Window.Quit()` on a window built with
  `WindowFlags.QuitOnWindowClose`. Slower and less isolated than a
  `MessageTests` case (it spins up a real `BApplication` and blocks on a
  real native message loop), and -- per the one-shot constraint above --
  must stay `[TestOrder(100)]` (last); this is also why the `Draw()` check
  had to be folded into this one test rather than added as a second one:
  issue #1 means only one full `Run()`-to-quit cycle may ever happen in
  this process.

There is no NUnit (or any test framework) anywhere in this Mono 6.14.1
port's actual installed GAC, and no realistic way to get one: modern
NUnit/xUnit target newer .NET than this Mono build implements, and the
only NUnit on this machine at all is old 2.6.2 copies buried inside an
unrelated leftover full Mono source checkout (vendored there just to build
*Mono's own* Newtonsoft.Json/Cecil test suites) -- not something this
project should depend on, since it isn't ours and could disappear. Instead,
`managed/Tests/TestAttribute.cs`/`TestModuleAttribute.cs`/
`TestOrderAttribute.cs`/`Assert.cs`/`TestRunner.cs` are a deliberately tiny,
dependency-free framework of our own: a `[Test]` attribute, a
`[TestModule("...")]` class-level attribute for the header/filter name, an
optional `[TestOrder(n)]` class-level attribute for the rare case a test
can't be fully isolated (see above), reflection-based discovery, a handful
of `Assert.AreEqual`/`IsTrue`/`IsNull`/`Fail` helpers, and a runner that
constructs a fresh instance of the test class per test (so one test can't
see state another left behind) and reports `PASS`/`FAIL`/`ERROR` per test
plus a final count. `FAIL` means an `Assert.*` call didn't hold; `ERROR`
means the test threw something else entirely (a null reference, a native
crash surfacing as an exception, ...) -- worth keeping visually distinct,
since those call for different next steps. Same rationale as the
hand-written shim itself: small enough that every line is understood, and
guaranteed to compile with `mcs` and run on this exact Mono build since we
control every line of it.

To add a test: write a public, parameterless, `void`-returning instance
method tagged `[Test]` on a public class anywhere under `managed/Tests/`
(a new file per kit, following `MessageTests.cs`/`WindowTests.cs`/
`ApplicationTests.cs`), tag the class itself with `[TestModule("...")]`
naming the kit it covers, throw via one of the `Assert.*` helpers (or
`Assert.Fail(...)` directly) to report a failure, and rebuild. Only reach
for `[TestOrder(n)]` if your test genuinely can't coexist with another
regardless of what order they'd otherwise run in (see above) -- it's an
escape hatch, not a default.

## Ownership rules (read before touching `Application.cs` or `hs_application.cpp`)

1. **A `Message` you construct owns its native `BMessage`** — `Dispose()`
   it (or use `using`). A `Message` your `OnMessageReceived` override
   receives is **borrowed** from the looper's queue and must never be
   destroyed — Haiku deletes it right after your callback returns.
2. **`Application.Run()` consumes the native handle, but only once it
   returns.** `BLooper`'s own message loop deletes the `BApplication`
   object itself once `QuitRequested()` returns true and the loop exits —
   this is normal Haiku behavior, not a bug, but it means the handle is
   dangling the instant `Run()` returns. `dotnet-haiku`'s GSoC author hit
   exactly this as a double-free and had to patch around it. This binding
   avoids it by having `Run()` null out its own handle field right after
   the blocking native call returns — not before it starts. That ordering
   matters: `OnReadyToRun`/`OnMessageReceived`/`OnQuitRequested` all fire
   on the looper thread *while* `Run()` is still blocked, and a callback
   that calls `PostMessage()` (as the sample's `OnReadyToRun` does) needs
   the handle to still be live at that point. Nulling it any earlier — e.g.
   before the blocking call — breaks exactly that case with a spurious
   `ObjectDisposedException`, which is a real mistake this project's first
   draft made and had to fix.
3. **No C++ exception may ever cross an `extern "C"` shim function.**
   P/Invoke has no concept of a C++ exception — one escaping the shim
   boundary is a hard crash, not a catchable managed exception. Nothing in
   the current API surface throws in practice (`BMessage`'s Add/Find family
   returns `status_t`), but this is a hard rule for every future addition.
4. **A fully-`Run()` `BApplication` is a one-shot event for the whole
   process, not just for that object** -- see "BWindow: threading,
   quitting, and destruction" above. If you're writing something (a test,
   a tool) that might construct more than one `Application` over its
   lifetime, this is not optional background reading.

For `Window.cs`/`hs_window.cpp`'s own ownership rules (when `Dispose()` is
safe to call synchronously vs. when it has to go through `Quit()` instead),
see "BWindow: threading, quitting, and destruction" above rather than
duplicating it here.

## Adding more BMessage fields

Mechanical and low-risk once the shape above is proven -- which is now
demonstrated across the full core round-trip (every scalar type, the
geometry-ish struct types, nested messages, generic data, and
`Has*`/`Replace*`/`RemoveName`/`CountNames`/etc. for all of them; see
`managed/Tests/MessageTests.cs` for a working, run-on-every-change example
of each one -- see "Testing" above). For a new `Add<Type>`/`Find<Type>`
pair: one `extern "C"`
function to `hs_message.h`/`.cpp` following the existing functions
exactly, one matching `DllImport` in `Native.cs`, one public method in
`Message.cs`. `Has<Type>`/`Replace<Type>` follow that exact same shape
once `Add`/`Find` exist. Anything that hands back a pointer into
BMessage's own storage (like `FindString`/`FindData` do) needs the same
"copy into managed memory immediately" treatment those already do.

What's NOT mechanical, and needs real design first: `BMessenger` (a
lightweight cross-team handle, not a plain struct), `entry_ref`/
`node_ref` (filesystem references -- a natural fit for a future Storage
Kit wrapper instead), and `AddFlat`/`FindFlat` (needs a `BFlattenable`
design of its own).

## Adding a new kit

Follow `hs_application.h`/`.cpp` (or `hs_window.h`/`.cpp`, which followed
that same template while adding its own destroyed-callback and posted-quit
patterns -- see "BWindow" above; or now `hs_view.h`/`.cpp`, which reused
`hs_window.h`'s destroyed-callback pattern again while adding its own
stricter ownership rule -- see "BView" above) for any class you need to let
C# subclass/override: one native C++ subclass per base class, one
callback-typedef + setter per virtual you expose, name the delegates with
a prefix specific to that class (`Window*Callback`, `View*Callback`, not
just `*Callback`) so two kits' similarly-named virtuals never collide in
the same namespace. When two classes in the same kit both need the same
operation (e.g. `Window`/`View` both needing `AddChild`/`RemoveChild`,
since a window is the root of its own view hierarchy just like a view is
the root of its nested children's), give each its own
`hs_<class>_add_child()`/`hs_<class>_remove_child()` pair rather than
trying to share one -- see `hs_window.h`/`hs_view.h`'s own pair for the
template. For classes nobody needs to override (most of Interface Kit's
~50 remaining classes, like most of Application Kit's, are plain data or
widget wrappers), a much simpler shim — direct property/method wrapping
with no callback machinery — is all that's needed; `hs_message.cpp` is the
template for that simpler shape.

A kit that needs to let C# subclass/override anything gets its own
managed assembly (see `Haiku.Interface.dll`, referencing `Haiku.App.dll`
for the types it reuses, like `Rect`/`Message`) rather than growing
`Haiku.App.dll` indefinitely -- this mirrors Haiku's own kit boundaries and
keeps each assembly's native surface reviewable on its own. A small
plain-data struct used for P/Invoke marshaling only (like `hs_rect`) is
cheap enough to duplicate locally in the new assembly's own `Native.cs`
rather than share via `InternalsVisibleTo`; a real class with meaningful
behavior (like `Message`, which `Haiku.Interface.Window` needs to hand
back a borrowed instance of) is worth an `InternalsVisibleTo` grant on the
one constructor it needs instead -- see `Haiku.App/AssemblyInfo.cs`'s
comment for that tradeoff spelled out.

## License

MIT — see [LICENSE](LICENSE). This matches both Haiku's own convention and
this project's dependency on Haiku's MIT-licensed headers.
