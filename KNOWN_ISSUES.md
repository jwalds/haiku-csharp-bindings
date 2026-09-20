# Known issues

Bugs and open questions found while building this binding that are real,
reproduced on actual Haiku hardware, and not yet fixed -- tracked here
instead of only in commit messages or scattered code comments so the next
person (or the next session) doesn't have to re-derive what's already been
ruled out. Each entry says what's confirmed, what's only suspected, and
what's been tried. When one of these gets fixed, move it to a "Fixed"
section at the bottom with the commit that fixed it, rather than deleting
it -- the investigation is worth keeping even after the bug isn't.

None of these three block using the binding today. All of them are corners
of the Application/Interface Kit threading model; nothing in BMessage is
affected.

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

## Fixed

*(nothing here yet)*
