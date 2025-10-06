This is the VR Sketch Revit plugin.
It was tested with Revit 2022 and partially 2023.

For PC VR, you need to install a binary of VR Sketch itself.  For usage on a standalone
Quest or Pico Neo device, you simply run VR Sketch on the Quest/PicoNeo headset.

Instructions:

* starting from the sources, you need first to build the solution with Visual Studio.
  You need to either fix the path to a few DLLs, or make a folder called "RevitDLLs" in
  the directory containing this README, and copy the required DLLs there.  Fish them
  around from the Revit directories.  The DLLs are:

    NewtonSoft.Json.dll
    RevitAPI.dll
    RevitAPI.xml
    RevitAPIUI.dll
    RevitAPIUI.xml

* edit the file VRSketch.addin to fix the path to VRSketch.dll: that file should have
  a line that says:

      <Assembly>Y:\VRSketch\bin\Debug\VRSketch.dll</Assembly>

  and you must replace it with the correct path, something like:

      <Assembly>C:\Some\Path\VRSketch.dll</Assembly>

* then copy the .addin file to C:\ProgramData\Autodesk\Revit\Addins\20##\


The VRSketch plugin in Revit tries to connect to 127.0.0.1:17353, and it will start
the executable VRSketch\VRSketch##.#.#.exe by itself if necessary, like it does
in Sketchup.  The version number is hard-coded by VRSKETCH_EXE_VERSION in
VRSketch/VRSketchPlugin.cs; you can generally change this to any more recent version.

NOTE: you can make it connect to 10.0.2.2:17353 for use from a VM.  See the start of
VRSketch/VRSketchConnect.cs.

Generally useful if you are going to tinker: set DUMP_FILE in VRSketchCommand.cs.

There are a few commented-out references and code related to editing DirectShapes,
but that was never fully implemented.


How to build the minimal installer
==================================

First ensure that VRSketchApp.VRSKETCH_EXE_VERSION contains the correct string!

Make sure the Release mode is selected (not Debug) in VS.

Go to VRSketch\VRSketch\bin\Release.  Make sure that the subdirectory VRSketch
there contains the latest version of VRSketchX.Y.Z.exe and the files that go
with it.  Then remake vrsketch.zip.

Then, regenerate the "Installer" project.  It should output
VRSketch\Installer\bin\Release\Installer.exe.
Go and rename it to VRSketch-0.#.#-Installer.exe.
