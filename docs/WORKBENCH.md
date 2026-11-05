# Workbench — retired in DC-M4.4

The desktop-style Workbench (Fancy presentation, Explorer, section pages,
catalog builder, pins, Preferences pages) was removed in DC-M4.4 per director
decision. The console is a single traditional overlay titled `Console`.

- `workbench` (`desk`), `browse`, `pin`, `unpin` no longer exist; `search`,
  `history`, `errors` and `recent` cover those needs as plain text.
- `uimode` now only switches native input placement (`transplant`/`cohesive`).
- Retired config keys (`UiMode`, `Accent`, `Density`) in old files are ignored.

Historical design reports remain in the repository as archives
(`DC-M4.1_WORKBENCH_REPORT.md`, `DC-M4.1_OPAQUE_FIX.md`,
`DC-M4.2_HIGH_FIDELITY_UI_REPORT.md`, `DC-M4.3_TYPOGRAPHY_SHARPNESS_REPORT.md`)
but are not shipped in the public package. See
[DC-M4.4 simplification](DC-M4.4_SIMPLIFICATION_REPORT.md) for the removal record.
