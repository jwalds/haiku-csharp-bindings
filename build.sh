#!/bin/bash
# Builds the native shim and the managed assemblies, in order.
# Run this ON Haiku (needs g++, libbe, and the mono port's mcs/mono).
set -e

cd "$(dirname "$0")"

echo "== building native/libhaikusharp.so =="
make -C native

echo "== building managed/Haiku.App/Haiku.App.dll =="
mcs -target:library -out:Haiku.App.dll managed/Haiku.App/*.cs

echo "== building managed/Sample/Sample.exe =="
mcs -target:exe -out:Sample.exe -reference:Haiku.App.dll managed/Sample/*.cs

echo "== building managed/Tests/Tests.exe =="
mcs -target:exe -out:Tests.exe -reference:Haiku.App.dll managed/Tests/*.cs

echo
echo "Build complete. Run the sample with:"
echo "  LIBRARY_PATH=\"\$(pwd)/native:\$HOME/config/non-packaged/lib:\$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:\$LIBRARY_PATH\" mono Sample.exe"
echo
echo "Run the test suite with:"
echo "  LIBRARY_PATH=\"\$(pwd)/native:\$HOME/config/non-packaged/lib:\$HOME/config/lib:/boot/system/non-packaged/lib:/boot/system/lib:\$LIBRARY_PATH\" mono Tests.exe"
echo "(pass a substring, e.g. \"mono Tests.exe Message\", to run just one test class)"
echo
echo "(Haiku's runtime_loader uses LIBRARY_PATH, not LD_LIBRARY_PATH -- see README.md.)"
