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
remarks), `managed/Tests/ApplicationTests.cs` (class remarks), details.md's
"BWindow: threading, quitting, and destruction" section.

---

## 3. Benign "Failed aborting id" Mono warning on window quit; two crash-prone "obvious" fixes, and the real one

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
- Not window-specific: a later session (see fix attempt 3 below)
  confirmed `BApplication`'s own message-loop thread (spawned by
  `hs_application_run_and_wait()`'s `Run()` call) independently produces
  the identical warning -- `HSApplication` had no destructor at all
  before that investigation. `ApplicationTests.cs` is the only test that
  ever exercises a full `Run()` cycle (issue #1), so the already-
  documented ~2-in-7 rate below is plausibly explained by either
  thread, not only a window's.

**Confirmed non-fatal:** reproduced independently via `hey` (both an
app-level `QUIT` and a window-targeted `let Window 0 do QUIT`) in addition
to the user's real close-box click. The process always exits fully and
"App exited cleanly." always prints first. It is non-deterministic --
roughly 2 of 7 automated attempts reproduced the warning, the rest didn't
-- and it never appeared across two full `Tests.exe` runs, even though
`WindowTests` also shows and quits a window in the same process.

**Fix attempts -- the first two crash; do not ship either of these:**

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
   on the same crash at the time -- but see fix attempt 4 below for what
   this really meant: not "unsafe outright", but "unsafe on a thread that
   was never actually completely attached in the first place."

3. *Call `mono_thread_detach_if_exiting()` (`mono/metadata/threads.h`)
   instead of `mono_thread_detach(mono_thread_current())` -- a different
   embedding API that takes no `MonoThread*` argument at all, so it
   can't hit the `mono_thread_current()` allocation path that crashed
   attempts 1-2.* Verified against the real header (`/boot/system/
   develop/headers/mono-2.0/mono/metadata/threads.h`) and confirmed
   exported by the actual `libmonosgen-2.0.so` in use (`nm -D`) before
   writing any code. Tried in three call-site variants, on real
   hardware, in a later session -- none of them crash, but none of them
   fix anything either:
   - Called directly from `~HSWindow()`, guarded the same way as
     attempt 1 (`fShown`), and from a new `~HSApplication()` (added
     this session -- see the "not window-specific" bullet above)
     guarded by a new `fRun`. Survived a dedicated 30- and 50-
     iteration `HammerProbe.exe` (a standalone, non-`Tests.exe`
     scratch program that show/quit-cycles many windows in one
     process, to multiply this non-deterministic symptom's chances of
     appearing -- kept in the repo root, see its own header comment)
     across 4 separate runs -- 180 total show/quit/destroy
     cycles, zero crashes: a real, hardware-confirmed safety
     improvement over attempts 1-2. But a temporary `fprintf` on its
     return value showed it returns `FALSE` (a no-op) on every single
     call, every run: this destructor runs during `task_looper()`'s
     own `delete this`, before the underlying OS thread function has
     actually returned, and Mono doesn't consider the thread
     "exiting" yet at that point.
   - Suspecting the timing was the problem, registered a real
     `pthread_key_t` (`pthread_key_create`) whose destructor calls
     `mono_thread_detach_if_exiting()`, and had `~HSWindow()`/
     `~HSApplication()` only ever call `pthread_setspecific()` --
     never the Mono function directly. A dedicated, Mono-free scratch
     probe (`probe_tls_destructor.cpp` -- pure BeAPI + pthread, no
     C#/Mono involved at all) confirmed on hardware that Haiku's
     libroot *does* invoke a `pthread_key_t` destructor for a
     `BLooper`-spawned thread (created via `spawn_thread()`/
     `resume_thread()`, not `pthread_create()`) at that thread's
     genuine OS-level exit -- same thread ID, firing microseconds
     after `task_looper()` returns -- proving the mechanism itself
     works on this OS. Wired into the real shim and rerun: the TLS
     destructor reliably fires (confirmed via `fprintf`, both for a
     window's thread and, separately, `HSApplication`'s own thread) at
     what's unambiguously each thread's actual exit.
     `mono_thread_detach_if_exiting()` still returns `FALSE` every
     time, from inside this genuinely-correct call site.
   - Across 8 full `Tests.exe` runs with this variant in place, the
     "Failed aborting id" warning still appeared in roughly the same
     ~1-in-4 proportion this issue already documents below -- i.e.
     this had no measurable effect on the symptom at all, consistent
     with the function being a no-op here regardless of when it's
     called.

4. **THE REAL FIX (shipped) -- explicit `mono_thread_attach()`, found via
   `gdb` with real debug symbols.** This Haiku mono port turned out *not*
   to be stripped after all (see "Where to pick this up" in the version
   of this entry before this fix landed) -- a live `gdb` backtrace of
   attempt-1's exact SIGSEGV, reproduced fresh, showed real symbols and
   line numbers all the way down:

   ```
   mono_thread_current() [threads.c:2184]
   -> get_current_thread_ptr_for_domain(domain=0x0, ...) [threads.c:632]
   -> mono_class_vtable_checked(domain=0x0, klass=..., ...) [object.c:1943]
   -> crashes dereferencing a field at domain+0x7c -- domain is NULL.
   ```

   `mono_domain_get()` returns `NULL` on this thread. Mono's *implicit*
   attach (triggered by the JIT trampoline the first time this shim calls
   into managed code on a Haiku-spawned thread) gets the thread far
   enough to actually run JIT'd code, but never sets that thread's
   current-`MonoDomain` TLS the way an *explicit* `mono_thread_attach(
   domain)` call does. Every one of attempts 1-3 above assumed the
   problem was *when* to call `mono_thread_current()`/`mono_thread_
   detach()` on an already-implicitly-attached thread; none questioned
   whether that implicit attach was ever complete in the first place. It
   wasn't.

   The fix: `native/src/hs_mono_thread_attach.h` (new, committed) caches
   the root `MonoDomain*` once, cheaply, from `hs_application_create()`
   (`CacheRootDomainForAttach()` -- always called on a thread Mono
   already has valid domain state for, since it's running that very
   native call from managed code), and `EnsureThreadAttached()` calls
   `mono_thread_attach(domain)` explicitly, guarded by a `__thread` flag
   so it only does real work once per thread, from the very first line of
   every native->managed callback trampoline that might run on a
   Haiku-spawned thread (`hs_window.cpp`'s `MessageReceived()`/
   `QuitRequested()`, `hs_application.cpp`'s `MessageReceived()`/
   `QuitRequested()`/`ReadyToRun()`). `mono_thread_attach()` on an
   already-attached thread is cheap and safe (returns the existing
   `MonoThread*`), so even without the guard a redundant call would be
   harmless.

   **Verified on real hardware:** with this fix in place,
   `mono_thread_current()` no longer crashes anywhere it's called from
   these threads. 280 `HammerProbe.exe` show/quit cycles across 6 runs,
   zero crashes -- and, crucially, this is also what let testing get far
   enough to discover a second, previously-unreachable bug (see fix
   attempt 5 immediately below, and issue #5, which this fix also
   plausibly resolves -- see that issue's update).

5. *Having fixed attempt 1-3's crash, try a REAL `mono_thread_detach(
   mono_thread_current())` again in `~HSWindow()`/`~HSApplication()`,
   now that `mono_thread_current()` itself no longer crashes -- hoping to
   finally get a genuine, non-no-op fix for this issue's actual warning.*
   **Do not ship this either -- it trades a cosmetic warning for two
   different, worse, 100%-reproducible failures**, both found by A/B
   testing on real hardware (toggling only this one call, nothing else,
   between runs) and both root-caused with a real `gdb` backtrace:
   - **A window whose thread ever ran `BView::Draw()`:** a fatal
     `g_error` abort, every time, no exceptions. Backtrace:
     `mono_thread_detach()` -> `mono_threads_enter_gc_safe_region_
     unbalanced_with_info()` -> `mono_threads_transition_do_blocking()`
     -> `"Cannot transition thread ... from STATE_BLOCKING with
     DO_BLOCKING"`. `Draw()`'s own app_server IPC round-trip leaves the
     thread parked in Mono's cooperative-GC `STATE_BLOCKING`, unbalanced
     (nothing in this shim's `BView::Draw()` override wraps that IPC call
     with the matching `mono_threads_exit_gc_safe_region`-style pair Mono
     expects for anything that blocks) -- and `mono_thread_detach()`
     then tries to enter that same blocking state *again*, which Mono's
     state machine treats as a fatal double-transition, not a no-op.
   - **A window whose thread never drew:** no crash at the call site --
     `mono_thread_detach()` returns normally -- but a corrupted internal
     Mono hash table, surfacing only later, at process shutdown, as
     `mono-hash.c:282`/`mono-hash.c:442`'s `"assertion 'hash != NULL'
     failed"`, printed over and over, forever: the process never exits,
     its exit code never returns, and it has to be `kill -9`'d. This is
     the *exact* symptom issue #5 already documented as one of four
     non-deterministic outcomes of a stock `Tests.exe` run, well before
     this session's detach code existed -- i.e. this is very likely the
     same latent, pre-existing Mono/Haiku-port bug in the runtime's own
     per-thread bookkeeping, just one this specific "successful-looking"
     detach call turns from a rare, spontaneous occurrence into a
     reliably-triggered one.
   - Confirmed via direct A/B comparison, same build, same test suite,
     only this one call toggled: with the real detach call in place (via
     a temporary `ThreadUsedDrawing()` per-thread flag set from
     `HSView::Draw()`, so the destructor could take the crash-avoiding
     safe path for drawing windows and the real-detach path for
     non-drawing ones -- this scaffolding was fully removed again once
     the experiment concluded, see "Current handling" below), a full
     `Tests.exe` run hung at process shutdown with the `mono-hash.c`
     spam. Forcing the exact same build to *always* take the safe,
     no-op-capable path instead (no code changes other than that one
     branch) produced five consecutive clean full-suite runs (149
     passed, 0 failed each time), clean process exits every time (no
     lingering process, no hash-table spam), with the pre-existing
     benign warning appearing in roughly the same historical proportion
     (2 of 5 runs) and nothing else different. The real detach call is
     the only variable that changed between "hangs forever" and "exits
     clean" in this comparison.
   - A red herring hit twice during this same investigation, worth
     recording so it isn't rediscovered from scratch: `Tests.exe`
     appearing to hang with *no* diagnostic output at all past
     `ReadyToRunMessageAndQuitRequestedAllFireInOrder ...` turned out
     both times to be Haiku's own `screen_blanker` (screensaver) having
     kicked in during the unattended SSH session and fully covering the
     test window -- app_server has nothing to paint for a fully-obscured
     view, so `Draw()` (and everything downstream of it, including this
     fix's own diagnostics) never fires at all. This is the exact same
     false lead issue #4 already documents; `kill`ing `screen_blanker`
     (or otherwise keeping the display active) before an unattended test
     run resolves it immediately and is not evidence of any code bug.

6. **ADAPTIVE FIX (shipped, on top of fix attempt 4) -- a real
   `mono_thread_detach()`, but only when `MONO_THREADS_SUSPEND=preemptive`
   is set, detected at runtime via plain `getenv()`.** Fix attempt 5 ruled
   out a real detach *unconditionally*; it did not rule out a real detach
   *always*. Two more threads were pulled on this session, both confirmed
   on real hardware before any shipped code changed:

   - **Prior art:** a search of other Mono-embedding projects turned up
     `mono/mono#20283`, which documents a related macOS deadlock in
     Mono's cooperative-suspend GC worked around by setting
     `MONO_THREADS_SUSPEND=preemptive`. Both of fix attempt 5's failure
     modes (the `STATE_BLOCKING` double-transition `g_error`, and the
     mono-hash corruption) are specific to Mono's cooperative-suspend
     "blocking region" state machine -- the preemptive suspend
     implementation doesn't use that machinery the same way, making it a
     plausible fix for this port too, not just a coincidence from an
     unrelated platform.
   - **Tested directly, via A/B experiment (uncommitted scaffolding,
     fully reverted afterward):** with `MONO_THREADS_SUSPEND=preemptive`
     set and a real `mono_thread_detach(mono_thread_current())` call in
     place of `MarkThreadForMonoDetachOnExit()` in both destructors, 6
     consecutive full `Tests.exe` runs (149 passed, 0 failed, every
     time) plus 150 `HammerProbe.exe` show/quit cycles across both
     drawing and non-drawing windows -- zero crashes, zero hangs, and the
     "Failed aborting id" warning never appeared once. The identical
     build with the env var *unset* reproduced fix attempt 5's original
     failures exactly (same `g_error` for drawing windows, same
     mono-hash hang for non-drawing ones) -- confirming the env var,
     specifically, is what makes the real detach safe, not some other
     change.
   - **Auto-detecting preemptive mode from inside the shim, so consumers
     don't have to trust a hand-set env var to matter:** the obvious
     candidate, `mono_thread_get_coop_aware()`/`mono_thread_set_coop_
     aware()` (exported by `libmonosgen-2.0.so`, found via `nm -D`), was
     investigated as a way to *query* Mono's active suspend policy at
     runtime. `gdb` disassembly of the real binary (not source-guessing
     -- `mono-threads-coop.c`, lines 777/792 per its own DWARF line
     info) showed these are a **per-thread** atomic flag on the calling
     thread's `mono_thread_info` struct (offset `0x364`, read with a
     `lock xadd`, set with a `lock cmpxchg` loop) -- unrelated to the
     global, process-wide suspend-policy choice Mono makes once at
     startup. Confirmed dead end; there is no supported Mono API to
     query `MONO_THREADS_SUSPEND` after the fact.
   - **What shipped instead:** plain libc `getenv("MONO_THREADS_SUSPEND")`,
     called from inside this shim itself. A temporary on-hardware probe
     (a `getenv()` call plus an `fprintf` added to `hs_application_
     create()`, since reverted) confirmed it reliably and unmodified
     reflects whatever was set before `mono` launched -- `mono`'s own
     launcher parses this env var once, at process startup, before any
     embedded/loaded library code (including this shim) ever runs, so
     there's no race or staleness concern. `native/src/hs_mono_thread_
     attach.h`'s new `RealDetachIsSafe()` reads it exactly once per
     process (a C++11 function-local `static`, the same thread-safe
     exactly-once pattern already used by `CachedRootDomain()`) and
     matches only the exact string `"preemptive"` -- `"hybrid"` mode was
     not tested and is deliberately not treated as safe by this check.
     `~HSWindow()` and `~HSApplication()` now branch on it: a real
     `mono_thread_detach(mono_thread_current())` when true, and the
     existing safe `MarkThreadForMonoDetachOnExit()` fallback when false
     or unset.
   - **Verified on the actual shipped code** (not just the earlier
     scratch experiment): a full clean rebuild, then `Tests.exe` (3 runs)
     and `HammerProbe.exe` (50-100 cycles) under each of the two modes.
     Default/unset: 149/149 passing every run, the pre-existing benign
     warning appearing in roughly its usual proportion, zero crashes --
     i.e. bit-for-bit the same behavior as fix attempt 4 alone, no
     regression. `MONO_THREADS_SUSPEND=preemptive`: 149/149 passing every
     run, zero crashes, zero hangs, and the warning did not appear in
     any run. A temporary branch-taken probe (`fprintf` in each arm of
     the new `if`, reverted before commit) additionally confirmed the
     *correct* branch is actually taken in each mode -- the safe fallback
     ran under the default mode and never under preemptive mode, and the
     real detach ran under preemptive mode and never under the default
     mode -- ruling out the possibility that both arms just happen to
     behave identically for some unrelated reason.

**Not yet known:** why `mono_thread_detach_if_exiting()` (attempt 3)
always returns `FALSE` for these threads even when called from a
genuinely-correct, confirmed-at-real-exit call site, and the exact
mechanism behind fix attempt 5's mono-hash corruption under the *default*
cooperative suspend mode (only that it's real, reliably triggered by a
real detach call there, and matches a failure mode issue #5 already
observed occurring spontaneously). Understanding either in full would
need a real debugger stepping through `libmonosgen-2.0.so`'s thread/GC/
hash internals -- not pursued further, since fix attempt 6 gives every
consumer a proven-safe way to get a real detach today (by setting
`MONO_THREADS_SUSPEND=preemptive`), and the default mode's fallback path
is unconditionally safe regardless of the answer. `mono_thread_get_coop_
aware()`/`mono_thread_set_coop_aware()`'s real semantics (a per-thread
flag, not a suspend-policy query -- see fix attempt 6) are now fully
understood via `gdb` disassembly, closing that one open question from
earlier in this investigation.

**Current handling:** the domain-attach fix (fix attempt 4) is always
shipped -- `native/src/hs_mono_thread_attach.h` is wired into every
native->managed callback trampoline in `hs_window.cpp` and
`hs_application.cpp`, and `hs_application_create()` caches the root
domain. On top of that, `~HSWindow()`/`~HSApplication()` now call
`hs_internal::RealDetachIsSafe()` (fix attempt 6) to choose between a
real `mono_thread_detach(mono_thread_current())` and the safe
`hs_internal::MarkThreadForMonoDetachOnExit()` fallback
(`native/src/hs_mono_thread_detach.h`, the pthread-TLS-destructor-based
wrapper around `mono_thread_detach_if_exiting()` from fix attempt 3).
By default (no env var set, i.e. every consumer who doesn't opt in),
behavior is unchanged from fix attempt 4 alone: no crashes, no hangs, and
the benign "Failed aborting id" warning may still appear non-
deterministically. A consumer who sets `MONO_THREADS_SUSPEND=preemptive`
before launching `mono` gets a real detach on every window/application
teardown instead, and with it a fully silent shutdown -- verified across
multiple full test-suite and `HammerProbe.exe` runs, with zero
regressions found in either mode. The net effect versus where this
binding started: calling into managed code from any Haiku-spawned thread
no longer crashes in any tested configuration, and the one remaining
cosmetic symptom is now fully eliminable, opt-in, with a one-line env
var and no source change on the consumer's end.

**Made the default for this repo's own documented/scripted workflows.**
The opt-in above was real but easy to forget -- exactly what happened
here: a user re-reported the warning after this fix had already shipped,
simply because the command they ran (typed by hand, or an old habit)
didn't happen to include `MONO_THREADS_SUSPEND=preemptive`. Rather than
rely on everyone remembering a one-line env var, `run_sample.sh` and
`run_tests.sh` (repo root) now wrap `mono` and set it automatically,
alongside the `LIBRARY_PATH` every invocation already needed --
`./run_sample.sh`/`./run_tests.sh` are now what `build.sh`'s own printed
instructions and README.md's quick-start recommend, so anyone following
the documented path gets the fully-silent, hardware-verified-safe
behavior without having to know this issue exists. The underlying shim
logic is unchanged -- `RealDetachIsSafe()` still reads the env var
adaptively and still falls back safely when it's unset -- so a bare
`mono Sample.exe`/`mono Tests.exe` invocation (or any other consumer
embedding this binding directly) still defaults to the safe fallback
exactly as before; only this repo's own recommended entry points changed.

**Where to pick this up:** eliminating the warning under the *default*
suspend mode too (rather than requiring the `MONO_THREADS_SUSPEND=
preemptive` opt-in) would still require either (a) understanding why
`mono_thread_detach_if_exiting()` is always a no-op here well enough to
find a call site or condition where it isn't, or (b) understanding fix
attempt 5's mono-hash corruption well enough to fix *that* under
cooperative suspend specifically. Both need a real debugger session
stepping through `libmonosgen-2.0.so` itself (gdb is installed and DOES
have real symbols for this build -- see fix attempt 4's own discovery,
and fix attempt 6's coop-aware disassembly) rather than another
externally-observed-behavior experiment. Given the warning is confirmed
non-fatal, fully understood, and now fully eliminable for any consumer
willing to set one env var, this is a coloring-in problem, not a
blocker, for either the default-mode case or anything else in this area.

**Where documented in code:** `native/src/hs_mono_thread_attach.h` (the
domain-attach fix, fix attempt 5's ruled-out unconditional-real-detach
finding, and fix attempt 6's adaptive `RealDetachIsSafe()` plus the full
`getenv()`-vs-`mono_thread_get_coop_aware()` reasoning, all in detail)
and `native/src/hs_mono_thread_detach.h` (the safe fallback) -- both
files' own header comments. `hs_window.cpp`'s `~HSWindow()` and
`hs_application.cpp`'s `~HSApplication()` each carry a shorter version of
the same story at their own call sites. `HammerProbe.cs` (repo root) is
the standalone stress-test tool referenced throughout.

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
is actually installed on this Haiku box, per issue #3's later session,
but matching debug symbols for `libmonosgen`/`libbe` are not, and this
Haiku port ships neither package) would be needed to say whether this is
literally the same
underlying defect as issue #3 manifesting more severely, or a related but
distinct fault in Mono's thread-interruption path specifically. Not
chased further here, consistent with issues #3/#4's own conclusion that
guessing-and-rerunning without real debug tooling just produces more
inconclusive data points, not a fix.

**Update (later session) -- issue #3's fix attempt 4 plausibly fixes this
too.** That session's `gdb` work (see issue #3) root-caused a real,
reproducible SIGSEGV in the exact same territory this entry describes --
`mono_thread_current()` crashing on a Haiku-spawned thread because
`mono_domain_get()` returns `NULL` there, from an implicit-only Mono
attach that never completes -- and shipped a fix (explicit
`mono_thread_attach()` in every native->managed callback trampoline,
`native/src/hs_mono_thread_attach.h`). This entry's own crash (`BWindow::
~BWindow()` -> `BView::_Detach()` -> `mono_thread_execute_interruption`)
is squarely the same category -- a foreign thread's Mono bookkeeping
being incomplete -- though not confirmed to be byte-for-byte the same
fault without a real debugger attached to *this specific* crash (which
was never reproduced again to re-check). What's confirmed instead: with
the domain-attach fix in place, extensive later hardware testing (280+
`HammerProbe.exe` show/quit cycles across multiple sessions, several
full `Tests.exe` runs including ones that specifically exercise
`BView::Draw()` and window teardown together) produced zero SIGSEGVs of
any kind. That same investigation also found a *different*, real bug
living in this exact neighborhood -- calling a real `mono_thread_
detach()` (which this entry's crash is NOT calling; nothing in the
codebase does) can corrupt an internal Mono hash table, surfacing as
this entry's own already-documented `mono-hash.c` `"assertion 'hash !=
NULL' failed"` spam -- see issue #3's fix attempt 5. That confirms this
class of hash corruption is a real, latent phenomenon in this Mono/
Haiku-port combination (not a one-off fluke), which is independently
useful context for this entry even though the *trigger* found there
(a real detach call) is never exercised by any code path that could
have produced this entry's original run-4 crash.

A still-later session (issue #3's fix attempt 6) shipped a way to
make a real `mono_thread_detach()` safe after all, opt-in via
`MONO_THREADS_SUSPEND=preemptive` -- and re-ran the same class of
verification (`Tests.exe`, `HammerProbe.exe`) with that real detach
actually exercised, under that mode, with zero mono-hash corruption
and zero SIGSEGVs of any kind. That's additional, independent
evidence for this entry's own conclusion above -- the hash-corruption
phenomenon is specific to a real detach happening under the *default*
cooperative suspend mode, not to real detaches in general.

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
what the hung process's threads are actually doing; `gdb` itself is
installed on this Haiku box (see issue #3's later session), but matching
debug symbols for `libmonosgen`/`libbe` are not. Given the "UPDATE" above, this is now a
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

