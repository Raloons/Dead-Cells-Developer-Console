# Pull request

## Summary

What changed and why?

## Tests

- [ ] `dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false`
- [ ] `dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Debug -p:AutoInstallMod=false`
- [ ] `dotnet run --project tests/DeveloperConsole.Tests -c Release`
- [ ] Privacy audit
- [ ] Documentation updated

## Native verification

- [ ] This change is offline-only and makes no native-behavior claim.
- [ ] Any required live test is described below and remains pending until an
      authorized session.

## Safety and privacy

- [ ] No save files, Steam Cloud data, game assets, raw databases, private
      expansion content, credentials, or absolute local paths are included.
- [ ] Command safety metadata is declared.
- [ ] New state-changing behavior is documented and tested.
