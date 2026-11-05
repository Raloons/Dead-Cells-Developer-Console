# Workbench — retired in DC-M4.4

The desktop-style Workbench (Fancy presentation, Explorer, section pages,
catalog builder, pins, Preferences pages) was removed in DC-M4.4 per director
decision. The console is a single traditional overlay titled `Console`.

- `workbench` (`desk`), `browse`, `pin`, `unpin` no longer exist; `search`,
  `history`, `errors` and `recent` cover those needs as plain text.
- `uimode` (`ui`, `theme`) was removed in DC-M4.6; the transplant input
  layout stays on with no user-facing switch.
- Retired config keys (`UiMode`, `Accent`, `Density`) in old files are ignored.

Historical milestone reports were removed from the tree in the DC-M5 cleanup;
the evidence remains in git history. See
[DC-M5 public release](DC-M5_PUBLIC_RELEASE_REPORT.md) for the removal record.
