# Contributing

This is an unofficial community developer tool. Contributions are welcome, but
native integration and save safety take priority over feature quantity.

## Development environment

- Windows.
- .NET SDK 10.0.
- Dead Cells v35 and DCCM 35.13.8 for a full local build and native testing.
- The DCCM MDK package is restored through NuGet; the project also uses the
  installed DCCM reference/host assemblies.

The offline test project references the mod project, so a contributor normally
needs the supported DCCM installation to build and run the suite. CI in this
repository intentionally runs source hygiene only; it does not bundle or
download game data.

## Build and test

```text
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Debug -p:AutoInstallMod=false
dotnet run --project tests/DeveloperConsole.Tests -c Release
```

## Coding conventions

- Keep the pure command layer free of native game types where practical.
- Put game access behind `IGameContext` / `IHeroFacade`.
- Declare `CommandSafety` for every new command and subcommand.
- Keep command output concise; log internal details rather than exposing raw
  exceptions.
- Bound collections, history, search results, and numeric inputs.
- Do not add absolute local paths, credentials, save data, or proprietary game
  resources.
- Do not add private expansion content or identifiers.
- Use ASCII or proper UTF-8; do not introduce mojibake.
- Update `docs/COMMANDS.md` and tests with every public command change.

## Native API caution

Do not infer native behavior from method names. Verify hook signatures,
ownership, lifecycle, and state transitions against the installed DCCM proxies
and actual game bytecode. Prefer existing native paths over speculative field
writes. Do not add save-file patches or resume-time reapplication hooks without
strong evidence and a documented safety case.

## Pull requests

- Keep changes focused and testable.
- Include offline regression tests for parsing, validation, and failure paths.
- State any native behavior that remains unverified.
- Do not commit build outputs, package archives, save files, game assets, raw
  databases, private fixtures, or DCCM distributions.
- Update documentation when behavior or limits change.

## Bug reports

Include:

- Dead Cells version.
- DCCM version.
- Console version.
- Exact command and arguments.
- Steps to reproduce.
- Expected and observed behavior.
- Sanitized DCCM log excerpts.
- Other installed mods, if relevant.

Do not upload production saves, Steam Cloud data, or private personal data.

## License

By contributing, you agree that your contribution may be distributed under the
project's MIT license. Third-party dependencies retain their own licenses.
