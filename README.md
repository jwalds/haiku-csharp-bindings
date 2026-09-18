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
  set (`int8`/`int16`/`int32`/`int64`/`float`/`double`/`bool`/`string`),
  `Point`/`Rect` (see `Haiku.App.Geometry` -- minimal placeholders ahead of
  a real Interface Kit wrapper), `pointer` (a raw `IntPtr`, meaningful only
  within your own process), and the `What` field.
- `Haiku.App.SystemMessages` -- a couple of Haiku's own `AppDefs.h`
  constants (`B_QUIT_REQUESTED`, `B_READY_TO_RUN`), packed the same way
  Haiku's own C++ headers pack them.

Not yet covered: `BWindow`/`BView`/anything Interface Kit (no GUI yet --
this is deliberately windowless), `BMessenger`, `BInvoker`,
`BMessageFilter`/`BMessageQueue`/`BMessageRunner`, `BRoster`, `BMessage`'s
flattened-object Add/Find pair (`AddFlat`/`FindFlat`, which needs
`BFlattenable`), and archiving (`BArchivable`).

## The open question this slice exists to answer

`BLooper::Run()` spawns a **new native OS thread** for the message loop and
returns immediately — every callback (`OnReadyToRun`, `OnMessageReceived`,
`OnQuitRequested`) fires **on that thread**, which Mono never created and
knows nothing about. In practice, calling a marshaled managed delegate
through a native function pointer should cause Mono's embedding layer to
auto-attach an unrecognized calling thread on first entry — but that's an
expectation carried over from how Mono's embedding API generally behaves,
**not something verified on this exact Mono 6.14.1-on-Haiku build yet**.

`managed/Sample/Program.cs` exists specifically to test this: if you see
its four `[1]`–`[4]` `Console.WriteLine` calls in order followed by a clean
exit, the cross-thread callback story holds up here and the rest of this
plan (Interface Kit, etc.) can proceed on solid ground. If it hangs or
crashes instead, that's the very first thing to debug — everything else in
this binding depends on it.

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

Mechanical and low-risk once the shape above is proven: for each new
Add<Type>/Find<Type> pair you want, add one `extern "C"` function to
`hs_message.h`/`.cpp` following the existing `Int32`/`String`/`Bool`
functions exactly, then one matching `DllImport` in `Native.cs` and one
public method in `Message.cs`. `Point`/`Rect`/`float`/`double` are the
obvious next ones; anything that hands back a pointer into BMessage's own
storage (like `FindString` does) needs the same "copy into managed memory
immediately" treatment `Message.FindString` already does.

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
