# Continuous integration

The tracked workflow `.github/workflows/source-hygiene.yml` runs on
`windows-latest` and checks:

- the privacy/path audit (`tools/Privacy-Audit.ps1`);
- that no game/build binaries, databases, saves, image/audio assets or packages
  are tracked. Only the four exact `docs/previews/DC-M4/*-comparison.png` paths and
  the explicitly named historical workbench/sampler views are
  allowlisted original offline UI rasterizations, generated from the same layout
  plan as the SVG previews. No general artwork/image-directory exemption exists.

## Why CI does not build the mod

The mod targets the installed Dead Cells v35 / DCCM 35.13.8 reference and host
assemblies. A stock GitHub-hosted runner does not have a Dead Cells
installation or the DCCM host tree. The DCCM MDK NuGet package alone does not
provide the installed game reference assemblies.

Committing a workflow that attempts a full build on a stock runner would fail by
design. Full builds and the native/offline test suite are therefore local
developer steps:

```text
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Debug -p:AutoInstallMod=false
dotnet run --project tests/DeveloperConsole.Tests -c Release
```

A future self-hosted or dedicated test runner with the supported game/DCCM
installation can run the full build and the native authorization-gated matrix.
The CI workflow intentionally does not download or redistribute game files.

## Private-content checks

The public scanner does not embed a private-content denylist. An authorized
local audit can set `DC_PRIVATE_DENYLIST` to a file outside the repository; the
scanner and release package builder will then check those terms too. The file
is never committed.
