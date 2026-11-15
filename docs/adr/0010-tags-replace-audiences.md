# Tags replace Audiences; Runtime Panels switch Active Tags at runtime

ADR 0002 filtered each Runtime Panel by Audiences, defined as kinds of Operator and fixed in the
Panel Definition. A project then needed one Runtime Panel to follow what the app shows: the same
app runs one of several layouts (Profiles) chosen while it runs, each with its own Entries, while
apps built from one Config still need different sets. A kind of Operator could not name a layout,
and a fixed set could not follow it.

We renamed Audience to Tag, a neutral name for any reason to show an Entry or Command, and split a
Runtime Panel's set into Default Tags (in the Panel Definition) and Active Tags (what it filters
by now, replaceable from code while the app runs). We rejected a second filter axis beside
Audiences (twice the model for a need one axis covers) and widening "Audience" to mean layouts
too (one word with two meanings).

## Consequences

- Breaking rename with no aliases: `OCAudience`, `Audiences` on Commands and `AudienceIds` become
  their Tag equivalents. Serialized fields keep their data through `FormerlySerializedAs`.
- Active Tags are never saved, so editing the Save File cannot widen what a Runtime Panel shows.
  The Passcode still guards opening a Runtime Panel, whatever its Active Tags.
- An empty Active Tags set shows every Exposed Entry, so code must ask for that explicitly; setting
  an empty set, or a Tag the Config does not declare, is refused.
- One axis matches on any shared Tag, so "only Admin and only Profile4 together" cannot be
  expressed.
- Apps sharing a Config share Default Tags; an app that needs its own set replaces the Active Tags
  when it starts.
- A Runtime Panel open while its Active Tags change rebuilds at once.
