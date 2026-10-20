# Third-party components

The root MIT license applies to this project's original mod source, installer scripts,
tests, documentation and generated accessibility cue audio. It does not relicense
third-party components or PATAPON game materials.

## NVDA Controller Client

The installer includes nvdaControllerClient64.dll, which retains its own license.
The supplied GNU Lesser General Public License version 2.1 text is preserved at:

- Repository: assets/NVDA-controller-LICENSE.txt
- Installed package: BepInEx/plugins/PataponAccess/NVDA-controller-LICENSE.txt

Upstream source: https://github.com/nvaccess/nvda

## Runtime and game dependencies

The release installer also bundles third-party loader/runtime dependencies such as
BepInEx and .NET. They remain governed by their respective upstream licenses.
Unity dependencies and generated game interoperability bindings are not covered by
this project's MIT license. The game itself is not included.

## Tolk distribution

This package includes the user-supplied extended Tolk x64 Release build, unchanged,
and its vendor client modules under BepInEx/plugins/PataponAccess/Tolk.
Tolk retains LGPLv3; the included NVDA client retains its separate LGPL license.
License texts and the supplied documentation are preserved in that directory.
Vendor client modules retain their respective terms; this project's MIT license does not relicense them.
Original Tolk project: https://github.com/dkager/tolk
The exact source revision of this extended supplied build has not been identified;
the original-project URL is not a claim that these binaries match its current build.
