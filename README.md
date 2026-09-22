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

Run the test suite the same way, with `Tests.exe` in place of `Sample.exe`
(`mono Tests.exe`, or `mono Tests.exe <substring>` to run just one
module). See [details.md](details.md#running-sampleexe-what-it-demonstrates-and-expected-output)
for what `Sample.exe` actually demonstrates and its expected console
output, and [details.md](details.md#testing) for the test framework
`Tests.exe` runs on.

## License

MIT — see [LICENSE](LICENSE). This matches both Haiku's own convention and
this project's dependency on Haiku's MIT-licensed headers.
