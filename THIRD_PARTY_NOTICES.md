# Third-party notices

## Project code

The source code and documentation in this repository are original work of the
maintainer, adapted from the maintainer's private development project. They are
licensed under the MIT License in `LICENSE`.

No source code from the private expansion, Dead Cells, DCCM, or another mod is
vendored into this repository.

## Runtime dependencies (not bundled)

The release package contains only the project's `DeveloperConsole` mod files
and public documentation. It does not redistribute any of the following:

- **Dead Cells** — proprietary game by Motion Twin / Evil Empire. You must own
  a legitimate copy. No game executables, assets, databases, or saves are
  included.
- **Dead Cells Core Modding (DCCM)** and the **DeadCellsCoreModding.MDK**
  package — the framework and build reference used by this mod. They are
  external dependencies from the user's DCCM installation or NuGet restore.
  Consult their upstream repositories and licenses for terms.
- **GameProxy, HaxeProxy, HashlinkSharp, ModCore, ModCore.Common** — runtime
  host components supplied by the DCCM installation. They are not distributed
  with this project.
- **Serilog** — logging dependency supplied by the DCCM host/runtime. It is
  not distributed with this project.
- **.NET 10** — the build/runtime platform. It is not distributed with this
  project.

## Distribution rule

Do not add Dead Cells binaries, raw game databases, extracted game assets,
save files, DCCM distributions, or private expansion content to this repository
or to a release package. The console reads live game data from the user's
installation at runtime.

DC-M4 UI ornaments are independently authored code-drawn geometry. The included
offline layout SVG/PNG previews use synthetic state and approximate system fonts.
No font files or game textures are bundled. Optional English aliases are read
locally from the user's installed template; the template and extracted name
database are not redistributed.
