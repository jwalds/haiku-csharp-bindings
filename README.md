# haiku-csharp-bindings

C# bindings for Haiku OS's native BeAPI, for use from the [Mono 6.14.1 port
to Haiku](https://github.com/jwalds/haikuports) this project
grew out of. Not affiliated with or endorsed by the Haiku project.

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

## Current scope: Application Kit only

This first slice covers just enough of `BApplication`/`BLooper`/`BHandler`/
`BMessage` to prove the hardest architectural question works at all on this
specific stack — see "The open question" below — before spending effort on
the much larger Interface Kit (~52 classes) or anything else. Concretely:

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

Not yet covered: `BWindow`/`BView`/anything Interface Kit (no GUI yet --
this is deliberately windowless), `BMessenger`, `BInvoker`,
`BMessageFilter`/`BMessageQueue`/`BMessageRunner`, `BRoster`, filesystem
references (`entry_ref`/`node_ref` -- `AddRef`/`FindRef`/`AddNodeRef`/
`FindNodeRef`, which fit more naturally with a future Storage Kit
wrapper), `BMessage`'s flattened-object Add/Find pair (`AddFlat`/
`FindFlat`, which needs `BFlattenable`), archiving (`BArchivable`),
scripting specifiers, delivery/reply plumbing (`SendReply`,
`WasDelivered`, etc. -- these belong more with `BMessenger`/`BLooper`),
and the `Get*`/`Set*` convenience-with-defaults sugar and indexed
(multiple-values-per-name) overloads real `BMessage` also has.

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

Expected output:

```
[1] OnReadyToRun fired -- native callback into managed code works.
[2] Received our own PING message back: "hello from the looper thread's own message"
[3] Requesting quit via SystemMessages.QuitRequested...
[4] OnQuitRequested fired -- allowing shutdown.
App exited cleanly.
```

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

== Application Kit ==
  ReadyToRunMessageAndQuitRequestedAllFireInOrder ... PASS

34 passed, 0 failed, 0 errored
```

That ordering is deliberate: `ApplicationTests` blocks for a moment on a
real native message loop, so if a future change ever made it hang, you'd
see its name sitting on screen with no result yet, telling you exactly
which test to look at, rather than silence until it either finishes or you
give up waiting.

Pass a substring to run just one module, matched against either the
`[TestModule]` name or the bare class name -- `mono Tests.exe BMessage` and
`mono Tests.exe Message` both run only `MessageTests`. Two modules exist
today:

- `BMessage` (class `MessageTests`) -- one small, fast, isolated test per
  `BMessage` Add/Find pair or whole-message operation (see
  `managed/Tests/MessageTests.cs`). Add a new one here alongside every new
  `hs_message.h` function.
- `Application Kit` (class `ApplicationTests`) -- the threading proof from
  "The open question" above, as an actual regression test rather than
  something you verify by eye. Slower and less isolated than a
  `MessageTests` case (it spins up a real `BApplication` and blocks on a
  real native message loop), but it belongs in the same suite rather than
  nowhere.

There is no NUnit (or any test framework) anywhere in this Mono 6.14.1
port's actual installed GAC, and no realistic way to get one: modern
NUnit/xUnit target newer .NET than this Mono build implements, and the
only NUnit on this machine at all is old 2.6.2 copies buried inside an
unrelated leftover full Mono source checkout (vendored there just to build
*Mono's own* Newtonsoft.Json/Cecil test suites) -- not something this
project should depend on, since it isn't ours and could disappear. Instead,
`managed/Tests/TestAttribute.cs`/`TestModuleAttribute.cs`/`Assert.cs`/
`TestRunner.cs` are a deliberately tiny (~180 line), dependency-free
framework of our own: a `[Test]` attribute, a `[TestModule("...")]`
class-level attribute for the header/filter name, reflection-based
discovery, a handful of `Assert.AreEqual`/`IsTrue`/`IsNull`/`Fail` helpers,
and a runner that constructs a fresh instance of the test class per test
(so one test can't see state another left behind) and reports
`PASS`/`FAIL`/`ERROR` per test plus a final count. `FAIL` means an
`Assert.*` call didn't hold; `ERROR` means the test threw something else
entirely (a null reference, a native crash surfacing as an exception,
...) -- worth keeping visually distinct, since those call for different
next steps. Same rationale as the hand-written shim itself: small enough
that every line is understood, and guaranteed to compile with `mcs` and
run on this exact Mono build since we control every line of it.

To add a test: write a public, parameterless, `void`-returning instance
method tagged `[Test]` on a public class anywhere under `managed/Tests/`
(a new file per kit, following `MessageTests.cs`/`ApplicationTests.cs`),
tag the class itself with `[TestModule("...")]` naming the kit it covers,
throw via one of the `Assert.*` helpers (or `Assert.Fail(...)` directly) to
report a failure, and rebuild.

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

## Adding more BMessage fields

Mechanical and low-risk once the shape above is proven -- which is now
demonstrated across the full core round-trip (every scalar type, the
geometry-ish struct types, nested messages, generic data, and
`Has*`/`Replace*`/`RemoveName`/`CountNames`/etc. for all of them; see
`managed/Tests/MessageTests.cs` for a working, run-on-every-change example
of each one -- see "Testing" below). For a new `Add<Type>`/`Find<Type>`
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

## Adding a new kit (e.g. Interface Kit next)

Follow `hs_application.h`/`.cpp` as the template for any class you need to
let C# subclass/override (one native C++ subclass per base class, one
callback-typedef + setter per virtual you expose). For classes nobody
needs to override (most of Interface Kit's ~52 classes are plain widget
wrappers), a much simpler shim — direct property/method wrapping with no
callback machinery — is all that's needed; `hs_message.cpp` is the
template for that simpler shape.

## License

MIT — see [LICENSE](LICENSE). This matches both Haiku's own convention and
this project's dependency on Haiku's MIT-licensed headers.
