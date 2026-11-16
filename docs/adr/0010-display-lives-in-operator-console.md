# Display lives in OperatorConsole

The on-site technician also needs to place the app's window (mode, size, position, monitor) on the installed machine, so the Runtime Panel gains a Display tab, Windows builds only. OC does the `Screen` and Win32 work itself instead of calling an adapter around the host app's `ResolutionInitializer`: the package stays free of host code and needs no seam, and any overlap with the host's own startup resolution code is fixed in the host later. The Display Settings are saved under fixed package keys as ad-hoc Overrides, not as Entries defined in the Config, because the package owns them and they never appear among Exposed Entries.

## Consequences

- OC carries Win32 window code (and its borderless mouse-offset workaround); it compiles to nothing off Windows.
- Display Settings do not show in the Config manager and are not covered by a Settings-tab Reset; the Display tab has its own Reset.
- A host that also sets the window at startup can fight OC's stored Overrides; that conflict is the host's to resolve.
