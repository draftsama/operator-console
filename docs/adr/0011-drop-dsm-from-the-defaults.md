# Drop DSM from the default Config name and Save File folder

ADR 0006 kept `Resources/DSMConfig.asset` and `persistentDataPath/DSM/save.json` so the rename to
OperatorConsole touched nothing on disk. Those two names are now the only `DSM` a new user meets,
and they read as a different package. 7.0 makes the defaults `OperatorConsoleConfig` and
`persistentDataPath/OperatorConsole/save.json`, and keeps old installs working: the Config loader
falls back to `DSMConfig` with a warning, and a Save File in the old folder is moved to the new one
on first start. We rejected keeping the old names forever (the mismatch never goes away) and a
clean break (an on-site machine would silently lose its Overrides after an update).

## Consequences

- Supersedes the "default Save File location and `DSMConfig` Resources name stay" consequence of
  ADR 0006.
- Moving the Save File is a one-time side effect of the first `OC` access; it never overwrites an
  existing file in the new folder.
- The `DSMConfig` fallback is removed in 8.0.
