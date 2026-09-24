# haiku-csharp-bindings

C# bindings for Haiku OS's native BeAPI, for use from the [Mono 6.14.1 port
to Haiku](https://github.com/jwalds/haikuports) this project grew out of.
Not affiliated with or endorsed by the Haiku project.

A hand-written C shim (not a generator) exposes BeAPI's C++ classes to
P/Invoke, with managed wrappers over the Application Kit (`BApplication`,
`BMessage`) and a growing slice of the Interface Kit (`BWindow`, `BView`,
and the `Button`/`TextControl`/`CheckBox`/`RadioButton`/`Slider`/
`ColorControl`/`ListView` controls). See [`details.md`](details.md) for
the full design rationale -- why a hand-written shim, prior art, the
current API surface class by class, and a deep-dive section per widget
covering every fact verified on real Haiku hardware before being relied
on. See [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) for open bugs found on real
hardware that aren't fixed yet.

![Sample.exe running on real Haiku hardware, showing DemoView's live input readout, the DemoButton "Click Me" button, the DemoTextControl "Type here:" field, the DemoCheckBox "Enable the text field above", the DemoRadioButton group "Option A"/"Option B"/"Option C", the DemoSlider "Volume:" control with its custom steel-blue bar color, hash marks, and "Quiet"/"Loud" limit labels, and the DemoColorControl "Color:" grid with its RGB ramps and numeric fields, and the DemoListView showing its four items with
"Alpha" selected](screenshots/sample-demo.png)

## Building and running (on Haiku)

Needs `g++` (or another Haiku-supported C++ compiler), the Mono 6.14.1 port
(`mcs`/`mono` from the `haiku-port-6.14.1` mono recipe), and `libbe`'s
development headers (part of `haiku_devel`, already a build requirement of
the mono port).

```
git clone https://github.com/jwalds/haiku-csharp-bindings.git
cd haiku-csharp-bindings
./build.sh
./run_sample.sh
```

`run_sample.sh` (and `run_tests.sh`, for the test suite) are thin wrappers
around `mono` that set two things every invocation needs:

- `LIBRARY_PATH` -- Haiku's own dynamic loader does not use
  `LD_LIBRARY_PATH` the way Linux does -- it is a BeOS-derived system and
  its runtime_loader looks at `LIBRARY_PATH` instead. Setting
  `LD_LIBRARY_PATH` is silently ignored, which is why an otherwise-correct
  build can still fail to find `libhaikusharp.so` with a
  `DllNotFoundException` at run time. The scripts use an absolute path for
  the `native` directory, rather than a bare relative `native`, to avoid
  any ambiguity about what the loader resolves a relative `LIBRARY_PATH`
  entry against, and include Haiku's own system lib directories explicitly
  rather than just appending the ambient `$LIBRARY_PATH` -- Haiku's
  `SetupEnvironment` boot script, which normally populates `LIBRARY_PATH`
  with those same paths, only runs for a desktop session, so an SSH login
  shell starts with it empty, and appending an empty variable to your own
  native directory finds *only* your own directory, silently missing
  system libraries like `libbsd.so` that `libnetwork.so` needs.
- `MONO_THREADS_SUSPEND=preemptive` -- must be set before `mono` starts
  (Mono's own launcher reads it once at process bootstrap, so setting it
  from inside managed code is too late). This is what gives every window/
  application teardown a real `mono_thread_detach()` instead of this
  binding's safe-but-cosmetic fallback, which in turn is what silences
  Mono's benign "Failed aborting id" warning on quit -- hardware-verified
  safe (6+ full `Tests.exe` runs and 150+ `HammerProbe.exe` create/show/
  close cycles, zero crashes, zero hangs, warning never appeared once).
  See [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md#3-benign-failed-aborting-id-mono-warning-on-window-quit-two-crash-prone-obvious-fixes-and-the-real-one)
  for the full investigation.

To run `mono` directly instead of using the wrapper scripts (e.g. to pass
extra `mono` flags), set both by hand:

```
MONO_THREADS_SUSPEND=preemptive LIBRARY_PATH="$(pwd)/native:$HOME/config/non-packaged/lib:$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:$LIBRARY_PATH" mono Sample.exe
```

Run the test suite the same way, with `./run_tests.sh` in place of
`./run_sample.sh` (`./run_tests.sh <substring>`, e.g. `./run_tests.sh
Button`, to run just one module -- same as `mono Tests.exe <substring>`
directly). See [details.md](details.md#running-sampleexe-what-it-demonstrates-and-expected-output)
for what `Sample.exe` actually demonstrates and its expected console
output, and [details.md](details.md#testing) for the test framework
`Tests.exe` runs on.

## License

MIT — see [LICENSE](LICENSE). This matches both Haiku's own convention and
this project's dependency on Haiku's MIT-licensed headers.
