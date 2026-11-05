# Public-release privacy audit — DC-M4.1

**Result: PASS for the locally qualified DC-M4.1 source/export/package.**
Private-content terms were supplied from `DC_PRIVATE_DENYLIST`, outside the tree.
No private names/IDs, machine paths or credentials belong in public artifacts.

## Scope and method

`tools/Privacy-Audit.ps1` scans tracked and nonignored new source, project,
documentation, config, fixture/tool text and generated SVG/HTML previews.
It checks generic machine paths/credential markers, the external denylist and
private milestone markers. Drive prefixes must begin a path token; words ending
in `ID:` followed by an escaped newline are not drive paths.

Excluded: Git/build/output/staged data, this report/scanner, and specified
audit/admission/release scripts containing their own detection terms. Generated
comparison PNGs are original synthetic UI rasterizations, not copied game art.
Local fixture/game data and raw development logs are never exported or packaged.

Manual checks:

- Only the supported MDK package and .NET dependencies are referenced.
- `modinfo.json` has no private dependencies; the deployable DLL is standalone.
- English names are read at runtime from installed data; no extracted POT,
  translation table or generated stock/private catalog is distributed.
- Tests use small synthetic contracts and public stock examples, not an
  expansion dataset. The actual starting-sword identity is `StartSword`.
- The release contains only the console DLL/modinfo and public documentation/
  original previews; no framework/game binaries, PDBs, saves or fixture evidence.
- R13 admission includes the external denylist and empty isolated save/data roots;
  all prior fixtures and native evidence folders remain preserved.
- Compiler debug records use generic `/_/` path mapping, verified in a PE debug
  directory contract. Release DLL ASCII/UTF-16 strings are scanned for local
  machine paths and external private terms as well as source/package names.
- Public source uses a fresh one-commit export with no inherited private history
  or remote. The old development history is not the publication artifact.

The local validation outputs are under `dist/`, not tracked. Re-run this audit
on the exact approved publication artifact. Publication is not authorized.
