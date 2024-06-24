# Haven Vintage Story Mod

This mod generates a haven structure at world spawn. The player's basic needs are met in the haven, while still encouraging the player to venture out of the haven after some preparation.

The [design doc](https://docs.google.com/document/d/1L6l8S_KnaE7IbpYp8SXqnl9lG38k-CenrQp7xAH97u8/edit?usp=sharing) has more information on the design and purpose of the mod.

## Building

The `VINTAGE_STORY` environment variable must be set before loading the
project. It should be set to the install location of Vintage Story (the
directory that contains VintagestoryLib.dll).

A Visual Studio Code workspace is included. The mod can be built through it or
from the command line.

### Release build from command line

This will produce a zip file in a subfolder of `bin/Release`.
```
dotnet build -c Release
```

### Debug build from command line

This will produce a zip file in a subfolder of `bin/Debug`.
```
dotnet build -c Debug
```
