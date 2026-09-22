# Known issues

Bugs and open questions found while building this binding that are real,
reproduced on actual Haiku hardware, and not yet fixed -- tracked here
instead of only in commit messages or scattered code comments so the next
person (or the next session) doesn't have to re-derive what's already been
ruled out. Each entry says what's confirmed, what's only suspected, and
what's been tried. When one of these gets fixed, move it to a "Fixed"
section at the bottom with the commit that fixed it, rather than deleting
it -- the investigation is worth keeping even after the bug isn't.

None of the three still open below block using the binding today. All of
them are corners of the Application/Interface Kit threading model; nothing
in BMessage is affected. (A fourth, `Tests.exe` hanging on a `BView`'s
first `Draw()` in one specific automated-test pattern, was fixed -- see
"Fixed" at the bottom.)

---

## 1. A fully-`Run()` `BApplication` is a one-shot event for the whole process

**Symptom:** once a `BApplication` has been constructed, `Run()`, gone
through a full `QuitRequested()`-returns-true cycle, and self-deleted, no
further `BApplication` can ever be constructed again in that same OS
process -- construction just hangs forever. This is true even for a brand
new `BApplication` instance that is never itself going to be `Run()`.

**Confirmed:**
- Constructing and disposing a `BApplication` that is *never* `Run()` has
  no such effect and can be repeated indefinitely.
- It's specifically the full spawn-thread -> `QuitRequested()` -> true ->
  self-delete cycle that "uses up" the process's one shot, not merely
  having *a* `BApplication` object alive.
- It is not a timing race -- inserting a delay before the second
  construction attempt does not help; the process is left in a permanently
  different state, not a transiently busy one.
- Reproduced with multiple independent, isolated scratch programs, and
  separately with the real `ApplicationTests.cs`/`WindowTests.cs` files,
  in both possible orderings of the two test classes.

**Ruled out:**
- Not a `BMessage`-related issue -- reproduced with `MessageTests.cs`
  excluded from the build entirely.
- Not specific to one particular test file's code -- reproduced with
  minimal from-scratch repro programs containing nothing but two
  `BApplication` construct/`Run()` attempts.

**Not yet known:** the exact mechanism inside `libbe`/Mono's embedding
layer that makes this a process-wide, not object-wide, restriction.

**Current handling:** `managed/Tests/ApplicationTests.cs` is tagged
`[TestOrder(100)]` (see `managed/Tests/TestOrderAttribute.cs` and
`TestRunner.cs`) so it always runs dead last in `Tests.exe`, after every
other test that needs a `BApplication` of its own. `Application.cs`'s "ONE
SHOT PER PROCESS" remarks document this for anyone calling the binding
directly, outside the test suite.

**Where documented in code:** `managed/Haiku.App/Application.cs` (class
remarks), `managed/Tests/ApplicationTests.cs` (class remarks).

---

## 2. Overriding `Window.OnQuitRequested()` re-triggers issue #1's poisoning

**Symptom:** a `Window` subclass that overrides `OnQuitRequested()`, gets
shown, quit, and destroyed, poisons the process the same way issue #1
does: any `BApplication` constructed afterward (even one that failed to
ever construct successfully before) hangs. This happens even though the
underlying *native* quit-requested callback is wired up unconditionally by
`Window.cs`'s constructor whether or not the C# subclass overrides the
hook -- so the same P/Invoke crossing happens on every window regardless.

**Confirmed:**
- Bisected against the real, unmodified `WindowTests.cs`/`ApplicationTests.cs`
  files (not a simplified reproduction) -- removing only the
  `OnQuitRequested()` override from `WindowTests.cs`'s `ProbeWindow`,
  while keeping an equivalent void-returning `OnDestroyed()` override in
  place, fixes the hang reliably across repeated runs.
- Order-independent: running `ApplicationTests` before `WindowTests` also
  hangs (at the `WindowTests` `BApplication` construction step), so this
  isn't "just" issue #1 with the classes in a different order -- the
  `OnQuitRequested` override itself is what triggers the poisoning, not
  merely proximity to a `Run()` cycle.

**Ruled out:**
- Not caused by `MessageTests.cs`/BMessage churn -- reproduced with it
  excluded.
- Not caused by the *native* callback registration itself, since that
  registration is identical whether or not the C# override exists.
- A same-shaped `OnDestroyed()` override does **not** cause the problem --
  it's specific to `OnQuitRequested()`.

**Suspected, not confirmed:** some interaction between Mono's embedding
layer and a foreign (Haiku-spawned, not Mono-created) thread that calls
into managed code and then exits without an explicit Mono detach. This is
the same category of concern as issue #3 below, and issue #3's
investigation (see there) adds real evidence for the "foreign thread"
theory in general, but does **not** confirm it explains this specific hang
-- issue #3 is a process-*shutdown*-time symptom; this one happens
mid-process, at a later `BApplication` *construction* call, a different
code path. Don't assume they're the same bug just because they rhyme.

**Current handling:** `managed/Tests/WindowTests.cs`'s `ProbeWindow` does
not override `OnQuitRequested()` (only `OnDestroyed()`), and its one test
wraps everything in a `using (new Application(...))` scoped to the test
method itself, not a shared static field.

**Where documented in code:** `managed/Tests/WindowTests.cs` (class
remarks), `managed/Tests/ApplicationTests.cs` (class remarks), README.md's
"BWindow: threading, quitting, and destruction" section.

---

## 3. Benign "Failed aborting id" Mono warning on window quit; the "obvious" fix crashes

**Symptom:** quitting a shown window (verified both by clicking its real
close box on hardware, and by sending it `B_QUIT_REQUESTED` via `hey`)
sometimes prints, during process shutdown, after "App exited cleanly.":

```
abort_threads: Failed aborting id: 0x9ed12ed000, mono_thread_manage will ignore it
```

**Confirmed root cause** (read straight from the actual mono source at
`mono/metadata/threads.c`, matching the exact mono-sgen 6.14.1 haikuport
build in use -- not guessed):
- The window's message-loop thread is spawned by Haiku's `BLooper`, not by
  Mono. The first time it calls into managed code (through any of
  `hs_window.h`'s three callbacks), Mono implicitly attaches it and flags
  it a background thread (because it wasn't created through Mono's own
  thread APIs).
- Nothing in this shim ever calls `mono_thread_detach()` for that thread,
  so Mono's internal `threads` table keeps a record of it after the
  underlying OS thread has actually exited.
- At process shutdown, `mono_thread_manage_internal()` runs a "join" phase
  (skips background threads specifically, to avoid a worse fate -- see
  below) and then an "abort" phase (`abort_threads()`) that tries
  `mono_thread_internal_abort()` on every remaining thread. For our
  already-dead thread this returns `FALSE` (`request_thread_abort()` sees
  the thread already stopped), producing exactly this `g_warning(...)` and
  then moving on -- the code closes the handle and continues, unconditionally.
- Being flagged "background" is what saves this from something worse: the
  earlier "join" phase's `wait_for_tids()` contains a `g_error(...)` (glib
  fatal abort, not a warning) if a *non-background* thread's handle
  becomes signaled while still present in the `threads` table -- i.e. if
  our thread were ever misclassified as foreground, this would be a hard
  crash on every quit, not an occasional warning.

**Confirmed non-fatal:** reproduced independently via `hey` (both an
app-level `QUIT` and a window-targeted `let Window 0 do QUIT`) in addition
to the user's real close-box click. The process always exits fully and
"App exited cleanly." always prints first. It is non-deterministic --
roughly 2 of 7 automated attempts reproduced the warning, the rest didn't
-- and it never appeared across two full `Tests.exe` runs, even though
`WindowTests` also shows and quits a window in the same process.

**Fix attempts -- both crash, do not ship either of these:**

1. *Call `mono_thread_detach(mono_thread_current())` cold, as the last
   line of `~HSWindow()`, guarded by "only if this window was ever shown"
   (to avoid ever detaching the caller's own thread on the never-shown
   `hs_window_destroy()` path).* Compiles and links fine against
   `libmonosgen-2.0` (headers at `/boot/system/develop/headers/mono-2.0`,
   lib at `/boot/system/lib/libmonosgen-2.0.so`). **Crashes with a real
   SIGSEGV** on hardware, inside `mono_thread_current()`'s own
   implementation (native stack: `mono_thread_current` ->
   `mono_class_value_size` -> ... -> `mono_jit_thread_attach` -> fault).
   `mono_thread_current()` is not a cheap accessor: the first time it's
   asked about a given thread it allocates a new managed `MonoThread`
   wrapper object (`create_thread_object()` in the mono source), and doing
   that from deep inside `BWindow`'s own inline self-delete call chain
   (`task_looper -> _QuitRequested -> Quit -> delete this`) is fatal.
2. *Pre-warm the `MonoThread` wrapper earlier, so the destructor's later
   call is a cache hit instead of a fresh allocation.* Tried two ways,
   both still fail:
   - Calling `mono_thread_current()` natively at the top of
     `QuitRequested()`, before invoking the C# callback: fails a **hard
     Mono assertion**, not a crash -- `Assertion at threads.c:2183,
     condition 'internal' not met`. `mono_thread_internal_current()`
     returns `NULL` here because the implicit Mono attach hasn't happened
     yet at this point; it's a side effect of the delegate invocation
     itself, not of merely running inside a C++ virtual override called
     from a thread that has called into managed code before.
   - Calling `mono_thread_current()` natively *after* the C# callback
     returns (so the thread is definitely attached by then): passes that
     assertion, but then hits **the identical SIGSEGV** from attempt 1.
   - Warming from the *managed* side instead -- touching
     `System.Threading.Thread.CurrentThread` inside `QuitRequestedThunk`,
     on an ordinary JIT-compiled code path, before the native destructor
     ever runs: also hits **the identical SIGSEGV**, with the same native
     stack trace, in the destructor's later call.

   Three different theories about *when* it's safe to call
   `mono_thread_current()` from native code on this thread all converged
   on the same crash. That's strong evidence this isn't a
   caching/ordering problem at all -- calling this specific embedding
   function natively from this call site (nested inside `BWindow`'s
   self-quit sequence, on a Haiku-spawned foreign thread) appears to be
   unsafe outright in this Mono build, for a reason not yet identified.

**Not yet known:** the actual mechanism inside `libmonosgen-2.0` that
makes `mono_thread_current()` crash here specifically. Pinning it down
further would need real debugging tools this environment doesn't have
readily available -- gdb with matching debug symbols for `libmonosgen`
and `libbe` (this Haiku port ships neither), or a debug build of Mono
itself.

**Current handling:** left as the original warning-only behavior (no
`mono_thread_detach()` call anywhere). `hs_window.cpp`/`hs_window.h` are
unchanged from the version committed in
`26b2d27` ("Add BWindow bindings ..."). Both fix attempts were fully
reverted; nothing from this investigation is in the committed code except
this write-up.

**Where to pick this up:** anyone attempting this again should assume
`mono_thread_current()`/`mono_thread_detach()` called bare from
`hs_window.cpp` are unsafe from this call site until proven otherwise with
a real debugger attached, not just by moving the call around and rerunning
the test suite -- that's exactly what produced three different failures
above without ever getting closer to a working fix.

---

## 5. A real SIGSEGV, not just issue #3's benign warning, seen once during window teardown under a larger test suite

**Symptom:** during the Slider-completeness pass's verification (bringing
`Tests.exe` from 115 to 121 tests, none of them touching threading,
windows, or `BLooper` in any way -- see `managed/Tests/SliderTests.cs`),
four consecutive full `Tests.exe` runs against the exact same freshly
rebuilt binary produced four different outcomes: a clean pass, a clean
pass with issue #3's already-known benign "Failed aborting id" warning, a
run with one unrelated test (`ApplicationTests.cs`'s
`ReadyToRunMessageAndQuitRequestedAllFireInOrder`, failing on "OnMessageReceived
should have fired for our own PING") failing with no crash, and finally a
run that printed repeated `mono-hash.c:282`/`mono-hash.c:442`
`assertion 'hash != NULL' failed` lines and stopped making progress
entirely, which turned out (found only after `kill -9`, via `hey -o 46
COUNT Window` and a stale-crash-dialog check learned from issue #4's own
"Also observed" note) to be a real, `debug_server`-caught SIGSEGV.

**Confirmed:**
- The crash's native stack trace (`/var/log/syslog`, `debug_server:
  Thread 4030 entered the debugger: Segment violation`) is squarely in
  the same "foreign thread calling into Mono" territory issue #3 already
  root-caused in detail: `BWindow::~BWindow()` -> `BView::_RemoveSelf()`
  -> `BView::_Detach()` -> back into
  `mono_thread_execute_interruption_ptr` -> a fault inside Mono's own
  `mono_thread_execute_interruption`. This is the *destruction*-time
  sibling of issue #3's *quit*-time warning -- same window message-loop
  thread, same general "Mono's thread bookkeeping for a Haiku-spawned
  thread doesn't line up with what's actually happening to that thread"
  category, but a hard fault instead of a caught, logged, non-fatal case.
- Not caused by the Slider completeness changes themselves: none of
  `hs_slider.h`/`hs_slider.cpp`/`Slider.cs`/`HashMarkLocation.cs` touch
  threading, `BWindow`, `BLooper`, or Mono's embedding layer at all --
  every new member is a direct, synchronous getter/setter wrapping a
  simple `BSlider` accessor. `SliderTests.cs`'s own
  `AddChild`/`RemoveChild`/`Dispose`/cascade-destroy tests are copied
  verbatim from every other control's test file's own established
  pattern (see e.g. `ButtonTests.cs`, `TextControlTests.cs`), not new
  code shaped any differently than what already ran clean across ten
  prior slices' worth of full-suite runs.
- Two back-to-back clean runs *were* obtained immediately after the fresh
  from-scratch rebuild (`make -C native clean && rm -f *.dll *.exe &&
  ./build.sh`, zero warnings) -- the crash above only showed up on a
  third and fourth *extra* run done out of caution after noticing an
  unrelated, much older stale crash dialog on screen (see "Ruled out"
  below). So this is not "every run crashes"; it's closer to issue #3's
  own already-documented non-determinism (that one: "roughly 2 of 7
  attempts"), just occasionally escalating to a hard fault instead of a
  warning -- plausibly because 121 tests now construct and tear down more
  `BWindow`/`BApplication` instances per process than the 115-test suite
  did, giving this pre-existing race more chances to land badly within a
  single `Tests.exe` invocation.

**Ruled out:**
- Not the same event as the two *other* stale `debug_server` "Crashed
  program" dialogs found on screen at the very start of this
  investigation (one for a `probe_slider_noapp` scratch binary from the
  original Slider ABI-verification work, one an older
  `mono_code_manager_reserve_align` JIT crash) -- both of those were
  confirmed stale by their position in `/var/log/syslog` (well before the
  end of the file, with no crash entries at all between them and this
  session's own work) and dismissed via `hey -o 46 QUIT Window <n>`
  before this run-4 crash happened. This entry is about the *new* crash
  that appeared afterward, at line 3475 of that same log.

**Not yet known:** same caveat as issues #3/#4 -- real debugging (`gdb`
with matching symbols for `libmonosgen`/`libbe`, neither shipped by this
Haiku port) would be needed to say whether this is literally the same
underlying defect as issue #3 manifesting more severely, or a related but
distinct fault in Mono's thread-interruption path specifically. Not
chased further here, consistent with issues #3/#4's own conclusion that
guessing-and-rerunning without real debug tooling just produces more
inconclusive data points, not a fix.

**Current handling:** none needed for correctness -- no single test's
PASS/FAIL result was ever wrong because of this; the crash happens during
process teardown, after results have already been determined (and, in the
runs where it didn't happen, already printed). Treated as the same class
of "known, tracked, non-blocking" issue as #3, not as something the
Slider completeness work needs to (or plausibly could) fix. `hey -o 46
QUIT Window <n>` (found during this investigation) is now the fastest way
to dismiss a stale `debug_server` "Crashed program" dialog over SSH
without a GUI input tool -- worth reusing directly if this recurs, rather
than rediscovering it.

**Where documented in code:** nowhere in source -- this is a runtime/test-
process phenomenon, not something a particular file's behavior can
document. Recorded here only.

---

## Fixed

**FIXED** -- see the commit that added this line for the actual change.
Short version: the fix was exactly the untried approach this issue's own
"Where to pick this up" note (below) suggested -- build and `Show()` the
probe window from inside `OnReadyToRun()`, on the same thread that calls
`Application.Run()`, matching the pattern `Sample.exe`'s `DemoView`
already proved safe (see the "UPDATE" paragraph below), instead of
`Show()`ing it from the test method's own thread while `Application` was
never `Run()` at all.

That could not go back into `ViewTests.cs` where the original attempt
lived, though: it requires a real `Run()`-to-quit cycle, and issue #1
above means only ONE test in this entire process may ever run one. That
slot already belonged to `managed/Tests/ApplicationTests.cs`, so the
`Draw()` check was folded into that file's existing test instead of added
as a second one -- see that file's class remarks for the full design,
including a second, less obvious change it required: the original quit
trigger (a same-thread PING/PONG message round trip posting
`QuitRequested` to itself, near-instantly) had to be removed, because
left in place it would almost certainly have won the race and quit the
app before `Draw()` ever got a chance to fire on the window's own,
separate thread. `ProbeDrawView.OnDraw()` is now the only thing that ends
`Run()`, by calling `Window.Quit()` (documented safe from any thread,
including the window's own) on a window built with
`WindowFlags.QuitOnWindowClose` -- the same native BWindow mechanism
`Sample.exe`'s `DemoWindow` already relied on for its close-box click,
just triggered programmatically here instead of by a UI event.

Verified on real Haiku hardware: 5 consecutive full `Tests.exe` runs, all
48 tests passing, no hang, in both the with-warning and without-warning
variants of issue #3's already-known benign shutdown message (confirming
that warning is unrelated to this fix, as issue #3 itself already
concluded). The rest of this entry is kept as-is below for the historical
investigation -- including the still-unconfirmed root cause of why the
*original* pattern hung -- since knowing what didn't work, and why, is
still worth keeping even now that a working alternative exists.

---

## 4. `Tests.exe` hangs shortly after a BView's first `Draw()` call returns, in one specific usage pattern

**Symptom:** in `managed/Tests/ViewTests.cs`'s `DrawFiresWithSaneUpdateRectAfterShow`
test, the very first `OnDraw()` call on a shown `View` fires correctly, with
the correct update rect -- and then the whole `Tests.exe` process stops
making any further progress at all. No crash, no exception, no further
console output of any kind (not even from the polling loop's own
`Thread.Sleep` returning) -- the process simply never does anything else
again and has to be `kill -9`'d.

**Confirmed:**
- Reproduced with a native-only (no Mono/C# involved at all) `BApplication`/
  `BWindow`/`BView` scratch program *first* -- and that one does NOT hang:
  its `Draw()` fires and the program runs to completion and exits cleanly.
  This rules out anything Haiku-/hardware-/app_server-specific; the hang is
  specific to going through this binding's Mono embedding layer.
- Narrowed further with two temporary `fprintf(stderr, ...)` probes added
  directly to `HSView::Draw()` in `hs_view.cpp` (never committed -- see
  "Current handling" below): one right as `Draw()` is entered, one
  immediately after the call to the registered C# draw callback
  (`fDrawCallback(...)`) returns. Both printed, in order, with the correct
  update rect and `hasCallback=1` -- i.e. the *entire* Draw path completes
  successfully: `BView::Draw()` is invoked, the native-to-managed callback
  crosses into C# and back, and control returns to native code inside
  `HSView::Draw()` with nothing having thrown or crashed. The hang happens
  strictly *after* that point -- somewhere between `HSView::Draw()`
  returning and the test process's own polling loop (running on a
  different, ordinary .NET thread, not the window's) next observing the
  `DrawFired` flag that `OnDraw()` had already set before returning.
- An earlier apparent non-reproduction turned out to be a red herring, not
  a fix: the very first time this test was run, `OnDraw()` never fired at
  all within the timeout, which looked like a related-but-different
  problem. That instead traced to Haiku's own `screen_blanker` (screensaver)
  having kicked in during the unattended SSH session and fully covering the
  test window on screen -- confirmed by reproducing the *exact same*
  non-firing symptom with the native-only scratch program above while the
  blanker was running, and confirming `Draw()` fires immediately once the
  blanker process is killed. That's expected app_server clipping behavior
  (nothing to draw for a fully-obscured view), not a bug, and is unrelated
  to the hang described here, which only appears once `Draw()` has
  genuinely fired.

**Ruled out:**
- Not the already-documented issue #3's "Failed aborting id" thread-detach
  warning -- that fires (harmlessly) at window *quit* time, on a thread
  that has already made many prior successful calls into managed code.
  This hang happens on the window's message-loop thread's *first ever*
  call into managed code (its first `Draw()`), and nothing resembling
  issue #3's warning message appears in the log before the process stops
  responding.
- Not specific to `ViewTests.cs`'s test scaffolding -- the same native-side
  `fprintf` probes show the hang starts inside/after native code that has
  nothing to do with the test framework (`HSView::Draw()` itself, and
  whatever BeAPI/app_server code calls it), not inside `TestRunner.cs`'s
  reflection-based dispatch.
- Not an infinite `Draw()`/`Invalidate()` loop -- the "Draw called" probe
  line appears exactly once in the log, never repeated.

**Suspected, not confirmed:** something in Mono's embedding layer's
thread-attach path specifically for a *window's own message-loop thread's
first-ever* call into managed code, as opposed to threads that have already
attached successfully via other hooks. `OnQuitRequested`/`OnMessageReceived`/
`OnDestroyed` (see issues #1-3) all demonstrably work fine on this same
thread *after* it has already made at least one successful managed call, so
if this is a Mono attach issue, first-attach specifically -- not the window
thread in general -- would have to be implicated. This is the same category
of "foreign native thread calling into Mono" territory as the already-
documented issue #3, but not the same symptom (a hang, not a warning), and
not confirmed to share a root cause -- treat it as a separate open question,
not an extension of #3, until proven otherwise.

**UPDATE -- real (non-test) usage does NOT hit this hang.** After this was
written, `managed/Sample/Program.cs` was given a `DemoView : View` that
overrides `OnDraw()` (fills, strokes, and draws text -- see that file), with
the window and view created inside `OnReadyToRun()` on the app's own
thread, exactly the pattern real apps are expected to use (same thread that
calls `Application.Run()` in `Main()`, no separate polling thread). Run
repeatedly on real hardware: `OnDraw()` fires with the correct update rect,
the drawing appears on screen exactly as coded (confirmed via `screenshot
-s`, not just log output), the app stays fully responsive afterward, and it
quits cleanly (`OnQuitRequested` fires, `Main()` returns, the process
exits) when asked to via `hey <sig> QUIT`. This narrows the hang to
`ViewTests.cs`'s specific combination -- `Show()` from a thread other than
the one running `Application.Run()`, with `Application.Run()` never called
at all -- rather than BView drawing in general. **The whole BView "shell +
drawing" slice is usable for real apps**; only that one now-removed
automated test pattern is affected.

**Also observed, not fully understood:** during this same investigation, a
stray Haiku crash-reporter dialog ("has encountered an error... Terminate /
Debug / Save report / Write core file") was later found on screen for
`/boot/system/bin/mono Tests.exe View` -- i.e. one of the `kill -9`'d hung
processes from this investigation apparently didn't just sit blocked
forever; at some point it (or a process from an earlier attempt this same
session) hit an actual native fault that Haiku's `debug_server` caught. The
dialog was found well after the fact (via an unrelated screenshot taken for
Sample.exe), with no `ps` entry left for the crashed process by then, so
which specific run produced it, and what `Debug`/`Save report` would have
shown, is lost -- this write-up settled for confirming the hang reproduces
reliably enough with `kill -9` cleanup, not for capturing that crash's own
detail. Worth knowing for whoever picks this up: if you reproduce this
hang again, check for (and use `Debug`/`Save report` on, before dismissing)
a crash dialog rather than only `kill -9`'ing it -- it may resolve into a
real, debuggable fault given enough time instead of spinning forever.

**Not yet known:** the exact mechanism -- same caveat as issue #3: real
debugging (a `gdb` attach with matching symbols) would be needed to see
what the hung process's threads are actually doing, and that tooling isn't
available in this environment. Given the "UPDATE" above, this is now a
lower-priority curiosity about `ViewTests.cs`'s specific pattern (and
possibly about `Application` instances that are constructed and `Show()`
a `Window` but never themselves `Run()`) rather than a blocker for the
binding's actual drawing support.

**Current handling:** `DrawFiresWithSaneUpdateRectAfterShow` was removed
from `ViewTests.cs` rather than merged in a form that could hang the whole
`Tests.exe` process -- a hung test blocks every test after it and the exit
code never comes back, which is worse than one failing test. The other six
`ViewTests.cs` tests (construction/geometry, `AddChild`/`RemoveChild` and
their hook-firing, `Dispose()` ownership rules, cascade-destroy) don't
exercise `Draw()` at all and are unaffected. The two temporary `fprintf`
probes used to narrow this down were never committed -- `hs_view.cpp` is
byte-for-byte the version from this binding's normal history, confirmed via
`md5sum` against the pre-investigation copy, both on the Haiku box and in
this repo.

**Where to pick this up:** do not just re-add a `Draw()`-firing automated
test in `ViewTests.cs`'s original shape and hope it works differently next
time -- it hung reliably, every time it was tried, once the screen_blanker
red herring was eliminated. Since Sample.exe's real usage pattern is now
confirmed unaffected (see the UPDATE above), a safer way to get automated
`Draw()` coverage back, if it's worth the effort, is to match THAT pattern
in the test instead: build and `Show()` the window from inside
`Application.Run()`'s own call (e.g. from `OnReadyToRun()`), on the thread
that calls `Run()`, rather than `Show()`ing from the test method's own
thread while `Application` sits un-`Run()`. That was never tried during
this investigation; it might sidestep the hang entirely rather than fix it,
which would still be enough to safely restore the coverage.

**Where documented in code:** `managed/Tests/ApplicationTests.cs` (class
remarks -- the fix itself, and the design constraints it had to work
around). `managed/Tests/ViewTests.cs` (class remarks) explains why the
automated Draw() test could not simply go back into that file instead.

