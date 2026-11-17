# Graphics with an optional URP part

The on-site technician needs to tune how the installed app renders so each machine holds its frame rate, so the Runtime Panel gains a Graphics tab beside Display. Half the settings a game normally offers (render scale, upscaling, MSAA, HDR, shadow distance, resolution and cascades) live on the URP pipeline asset, but the package has never depended on a render pipeline. We considered three options: only pipeline-agnostic `QualitySettings` (no dependency, but none of the settings that matter most for frame rate), leaving Graphics to each app as its own Entries (works today, but every project rebuilds the same tab), or an optional URP part compiled only when URP is installed. We chose the optional part: the base Graphics settings work in any pipeline, and the URP settings appear only in URP apps. To keep the Editor safe, URP settings are applied to a runtime copy of the active pipeline asset, never the project's asset.

## Consequences

- The runtime assembly gains a version define on the URP package; the URP code compiles to nothing without it, and the package still declares no URP dependency.
- Swapping in a copied pipeline asset means code that compares against the original asset reference sees a different object while Graphics Overrides are active.
- Each Quality Level has its own pipeline asset, so a Quality Level change re-copies its asset and clears the other Graphics Overrides.
- Post-processing and app-specific options stay out of the package; apps add them as Entries.
- HDRP and Built-in get only the base settings until someone needs more.
- Soft Shadows is left out: URP 17 keeps the asset's soft-shadow switch behind an internal setter, so it cannot be changed at runtime without reflection.
- In the Editor, Quality and pipeline settings changed in Play mode would persist into the project, so leaving Play mode restores Graphics as the app started.
