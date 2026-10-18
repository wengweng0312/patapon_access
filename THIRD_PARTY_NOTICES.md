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
