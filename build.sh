#!/bin/bash
# Builds the native shim and the managed assemblies, in order.
# Run this ON Haiku (needs g++, libbe, and the mono port's mcs/mono).
set -e

cd "$(dirname "$0")"

echo "== building native/libhaikusharp.so =="
make -C native

echo "== building managed/Haiku.App/Haiku.App.dll =="
mcs -target:library -out:Haiku.App.dll managed/Haiku.App/*.cs

echo "== building managed/Haiku.Interface/Haiku.Interface.dll =="
mcs -target:library -out:Haiku.Interface.dll -reference:Haiku.App.dll managed/Haiku.Interface/*.cs

echo "== building managed/Sample/Sample.exe =="
mcs -target:exe -out:Sample.exe -reference:Haiku.App.dll -reference:Haiku.Interface.dll managed/Sample/*.cs

echo "== building managed/Tests/Tests.exe =="
mcs -target:exe -out:Tests.exe -reference:Haiku.App.dll -reference:Haiku.Interface.dll managed/Tests/*.cs

echo
echo "Build complete. Run the sample with:"
echo "  ./run_sample.sh"
echo
echo "Run the test suite with:"
echo "  ./run_tests.sh"
echo "(pass a substring, e.g. \"./run_tests.sh Message\", to run just one test class)"
echo
echo "(Those wrapper scripts set LIBRARY_PATH -- Haiku's runtime_loader uses that, not"
echo " LD_LIBRARY_PATH, see README.md -- and MONO_THREADS_SUSPEND=preemptive, which"
echo " KNOWN_ISSUES.md #3 verified silences Mono's benign 'Failed aborting id' warning"
echo " on quit. To run mono directly instead:"
echo "  MONO_THREADS_SUSPEND=preemptive LIBRARY_PATH=\"\$(pwd)/native:\$HOME/config/non-packaged/lib:\$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:\$LIBRARY_PATH\" mono Sample.exe"
echo ")"
