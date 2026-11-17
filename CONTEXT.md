# OperatorConsole

A small persistence layer for tweakable settings of an installed app (e.g. values an on-site
technician adjusts), plus the on-site actions that technician runs. One save per app;
human-readable and hand-editable.

## Language

**Entry**:
A named, typed setting — identified by its **Key**, with a **Data Type**, a **Default**, and a display **Label**.
_Avoid_: field, variable, constant

**Entry Definition**:
The authored description of an **Entry** (key, data type, default, label, exposed flag), kept in the **Config**.
_Avoid_: DSMConstant, schema

**Config**:
The single asset holding every **Entry Definition** plus save behaviour (autosave, save location).

**Value**:
The current value of an **Entry** at runtime: its **Override** if one exists, otherwise its **Default**.

**Override**:
A **Value** explicitly set for an **Entry**. Only **Overrides** are persisted; an **Entry** never set keeps following its **Default**, even if the **Default** later changes.

**Save File**:
The one on-disk file holding the **Overrides** — a flat key→value document. There is exactly one per app.
_Avoid_: slot, save slot, envelope

**Exposed Entry**:
An **Entry** flagged to appear on the **Runtime Panel** for on-site adjustment.

**Range**:
An optional min/max on an **Exposed Entry** of type Int or Float. A **Range** tells the **Runtime Panel** to present the **Entry** as a slider; it is a presentation hint only and never clamps the **Value**.
_Avoid_: limit, bounds, constraint

**Option**:
A named choice (display text plus the int it stands for) in the **Options** of an **Exposed Entry** of type Int. **Options** tell the **Runtime Panel** to present the **Entry** as a dropdown; like a **Range**, they are a presentation hint only and never clamp the **Value**. Each **Option** keeps its own int, so reordering **Options** never changes what was saved.
_Avoid_: choice, item, enum value

**Header**:
An optional title on an **Exposed Entry**, shown on the **Runtime Panel** directly above that **Entry**'s row to start a visual group. Presentation only; it has no effect on the **Value**.
_Avoid_: section, category, group

**Operator**:
A person on site who uses a **Runtime Panel** to view and change **Entries** and run **Commands**.
_Avoid_: user, admin, player

**Tag**:
A name that decides on which **Runtime Panels** an **Exposed Entry** or a **Command** is shown (e.g. Admin, Technician, Profile4). An **Entry** may name several **Tags**; one naming none is shared and shown on every **Runtime Panel**. Every **Tag** is declared once in the **Config**.
_Avoid_: audience, group, category, label

**Default Tags**:
The **Tags** a **Runtime Panel** starts with, set in its **Panel Definition**.
_Avoid_: panel tags, initial tags

**Active Tags**:
The **Tags** a **Runtime Panel** filters by right now. They start as its **Default Tags**; code may replace them while the app runs and may return them to the **Default Tags**. They are never saved.
_Avoid_: current tags, runtime tags, selected tags

**Presentation Hint**:
A **Range**, set of **Options**, **Header**, or set of **Tags** on an **Exposed Entry**. It changes how and where the **Runtime Panel** shows the **Entry**, never its **Value**.
_Avoid_: widget setting, UI option

**Runtime Panel**:
The in-game UI where an operator views and edits **Exposed Entries** and runs **Commands** and **Entry Actions** while the app runs. An app may have more than one, each with its own **Active Tags**.
_Avoid_: config canvas

**Panel Definition**:
The authored description of one **Runtime Panel** (its **Default Tags**, **Open Triggers**, **Panel Lock** and layout), kept in the **Config**. The app creates each **Runtime Panel** from its **Panel Definition** when it starts.
_Avoid_: panel settings, panel profile

**Open Trigger**:
A way an operator opens a **Runtime Panel** by hand: a hotkey or a **Corner Tap**. Each **Runtime Panel** has its own **Open Triggers**; several can be on at once.
_Avoid_: toggle, shortcut, gesture

**Corner Tap**:
An **Open Trigger**: tapping or clicking one chosen corner of the screen a set number of times in quick succession. It only opens; it never closes the **Runtime Panel**.
_Avoid_: secret tap, hidden button

**Panel Lock**:
A **Runtime Panel** that asks for its **Passcode** every time an **Open Trigger** opens it.
_Avoid_: password protection, login

**Passcode**:
The numeric code of a **Panel Lock**, entered on an on-screen keypad. It belongs to one **Runtime Panel** and is set when the app is built; it is never an **Entry** and never in the **Save File**.
_Avoid_: password, PIN

**Lock Screen**:
The full-screen keypad a **Panel Lock** shows before its **Runtime Panel** opens. It checks the **Passcode** as soon as enough digits are entered; a wrong one is cleared for another try, and Cancel or the hotkey leaves the **Runtime Panel** closed.
_Avoid_: password dialog, login screen

**Command**:
A named action an operator runs from the **Runtime Panel**, with a **Title** and an optional **Description**. It takes no input; a **Command** that needs input reads it from **Entries**. It is code, not data: it is never saved and has no **Entry Definition**.
_Avoid_: action, button, function

**Registered Command**:
A **Command** that is live right now because the object that owns it has registered it. The **Runtime Panel** lists only **Registered Commands**; a **Command** whose owner is gone or disabled is not shown.
_Avoid_: available command

**Entry Action**:
A named action shown beside one **Exposed Entry** on the **Runtime Panel**, with a **Title**, whose result becomes that **Entry**'s **Value** (e.g. browse for a folder, fetch a token). Like a **Command** it is code, not data, and is live only while its owner has registered it; unlike a **Command** it belongs to exactly one **Entry** and exists to change its **Value**.
_Avoid_: entry command, value source, fill button

**Watch**:
Subscribing to an **Entry** to receive its **Value** now and every time it changes, whatever the cause (set, load, reset).

**Reset**:
Removing **Overrides** so **Values** fall back to their **Defaults**.

**Binding**:
A component that pushes one **Entry**'s **Value**, live and one way, into members of other components on the same GameObject.
_Avoid_: data binding, sync

**Link**:
One target member of a **Binding**. When the member takes text and the **Entry** is not a String, the **Value** goes through the **Link**'s **Format** first.
_Avoid_: target, connection

**Format**:
The .NET composite format string on a **Link** (e.g. `Speed: {0:0.0}`) that turns a non-text **Value** into text.

**Log**:
The messages the app wrote to Unity's log while it runs, kept so an **Operator** can read them on a **Runtime Panel**, including those written before the panel was opened.
_Avoid_: console, debug log

**Log Entry**:
One message in the **Log**, with its type (Info, Warning or Error) and, when there is one, its stack trace.
_Avoid_: log line, message

**Performance**:
Live measurements of how the running app is doing (frame rate, frame time, memory), shown to an **Operator** on a **Runtime Panel**.
_Avoid_: profiler, profiling, stats

**Metric**:
One measured quantity in **Performance** (e.g. frame time, GC allocation per frame). A **Metric** the running build cannot measure is shown as unavailable, not hidden.
_Avoid_: counter, stat

**Display**:
The app's own window on the screen (its **Display Mode**, size, position and monitor), shown to an **Operator** on a **Runtime Panel**. Only available on Windows builds.
_Avoid_: screen, window, resolution

**Display Mode**:
How the **Display** is presented: Windowed, Borderless or Full Screen. This "Full Screen" is unrelated to a **Runtime Panel** filling the screen.

**Display Setting**:
One value of the **Display** an **Operator** sets (mode, size, position, monitor). Persisted as an **Override** under a fixed key of the package; it is not an authored **Entry** and never appears among **Exposed Entries**.
_Avoid_: display option, resolution setting

**Graphics**:
How the app renders on the machine it is installed on (quality level, frame rate, image and shadow quality), tuned by an **Operator** on a **Runtime Panel** so that machine keeps its frame rate. Separate from the **Display**, which is only the app's window.
_Avoid_: display, video settings, quality settings

**Graphics Setting**:
One value of **Graphics** an **Operator** sets (e.g. quality level, VSync, render scale). Persisted as an **Override** under a fixed key of the package; like a **Display Setting**, it is not an authored **Entry** and never appears among **Exposed Entries**.
_Avoid_: graphics option, quality option

**Quality Level**:
The **Graphics Setting** that picks one of the app's authored quality presets. It is the baseline the other **Graphics Settings** fine-tune.
_Avoid_: preset, quality tier

**Custom**:
What a **Runtime Panel** shows for the **Quality Level** while any **Graphics Setting** that follows it has an **Override**: the baseline has been fine-tuned. Frame-rate settings do not make it **Custom**.

## Relationships

- The **Config** holds zero or more **Entry Definitions**; each defines exactly one **Entry**.
- The **Save File** stores **Values** by **Key**; a **Key** in the **Save File** with no **Entry Definition** is an _orphan_.
- An **Exposed Entry** is an **Entry** — not a separate kind of data.
- An **Exposed Entry** has at most one **Range**; with a **Range** it shows as a slider, without one as a plain input. A **Value** outside its **Range** is kept as-is.
- An **Exposed Entry** of type Int has at most one set of **Options**; with **Options** it shows as a dropdown. **Options** and a **Range** are mutually exclusive (**Options** win). A **Value** matching no **Option** is kept as-is and the dropdown shows it as unrecognised; nothing is written until the operator picks an **Option**.
- The **Default** of an **Entry** with **Options** is chosen from its **Options** at authoring time; at runtime it is not enforced.
- An **Exposed Entry** has at most one **Header**; an **Entry** that is not exposed shows no **Header**.
- An **Entry** that is not exposed is shown on no **Runtime Panel**, whatever **Tags** it names.
- A **Tag** cannot be removed from the **Config** while any **Entry** or **Panel Definition** still names it. Renaming a **Tag** changes no **Entry** or **Panel Definition**. Setting **Active Tags** that name an undeclared **Tag** is refused.
- Which **Presentation Hints** an **Entry** may carry depends on its **Data Type**: a **Range** needs Int or Float, **Options** need Int, a **Header** fits any type.
- A **Runtime Panel** shows an **Exposed Entry** when the **Entry** names no **Tag**, or shares at least one **Tag** with the panel's **Active Tags**. A **Runtime Panel** whose **Active Tags** are empty shows every **Exposed Entry**.
- **Active Tags** can be emptied only on purpose (show everything), never by setting an empty set of **Tags**.
- When the **Active Tags** change, the **Runtime Panel** shows the new set at once, even while it is open.
- **Tags** decide only where an **Entry** is shown. An **Entry** a **Runtime Panel** does not show still exists in that app: code can read and set it, and **Bindings** still follow it.
- Apps built from one **Config** share **Entry Definitions**, not **Values**: each app has its own **Save File**, so the same **Entry** can hold a different **Value** in each app.
- A **Reset** from a **Runtime Panel** covers only the **Exposed Entries** that **Runtime Panel** shows, and only after the operator confirms; **Overrides** of every other **Entry** and of _ad-hoc keys_ survive it.
- The **Config** holds zero or more **Panel Definitions**, each with a unique name. The app creates one **Runtime Panel** per enabled **Panel Definition** when it starts, and that **Runtime Panel** lasts until the app quits, across scene loads. With no enabled **Panel Definition** there is no **Runtime Panel**.
- Code finds a **Runtime Panel** by its **Panel Definition**'s name, so renaming a **Panel Definition** breaks code that used the old name.
- Two **Panel Definitions** may share an **Open Trigger**; one use of it then opens both **Runtime Panels**, except that at most one **Lock Screen** is shown at a time: of the **Runtime Panels** with a **Panel Lock**, only the first in the **Config**'s order opens.
- A **Panel Lock** guards only the **Open Triggers**; code that opens a **Runtime Panel** directly is not asked for the **Passcode**. A wrong **Passcode** can be retried without limit. A **Runtime Panel** with a **Panel Lock** but no **Lock Screen** refuses to open from its **Open Triggers**.
- While its **Lock Screen** is up, a **Runtime Panel** counts as open to the rest of the app, as it does once unlocked.
- A **Runtime Panel** with a **Panel Lock** and one without can have different **Active Tags** in the same app (e.g. a locked Admin panel beside an open Technician panel).
- A **Command** names zero or more **Tags** and is shown by the same rule as an **Exposed Entry**. Unlike an **Entry**, it names them by name, so renaming a **Tag** drops it from **Commands** that named the old name.
- A **Command** may require confirmation; the operator then confirms before it runs.
- Each owner registers its own **Commands**, so two owners of the same kind give two **Registered Commands** with the same **Title**; the **Runtime Panel** tells them apart by owner.
- A **Command** keeps running when the **Runtime Panel** closes. It is stopped when its owner unregisters. While it runs it cannot be run again; other **Commands** can.
- A **Command**'s last outcome (running, succeeded, failed) belongs to the **Registered Command**, not to the **Runtime Panel**, and is shown again when the **Runtime Panel** reopens.
- A **Command** shows a **Header** above it to start a visual group, as an **Exposed Entry** does; when the **Command** listed just before it has the same **Header**, the **Header** is shown once for both.
- A **Runtime Panel** with no **Command** to show has no Commands tab and looks as it did before **Commands** existed.
- A **Command** never changes a **Value** by being run through the **Runtime Panel**; anything it changes, it changes through code like any other caller.
- A **Binding** has one or more **Links**. A **Link** whose member takes the **Entry**'s own **Data Type** receives the **Value** as-is; only when no such member exists does a text member receive it through the **Format**.
- A **Key** used in code without an **Entry Definition** is an _ad-hoc key_: it works and persists like any **Override**, but is never added to the **Config** automatically.
- An **Entry Action** names its **Entry** by **Key** and must produce a **Value** of that **Entry**'s **Data Type**. It is shown wherever its **Entry** is shown and nowhere else; it has no **Tags** or **Header** of its own.
- An **Entry Action** is given the **Entry**'s current **Value** when it starts. When it finishes, its result is set exactly as if the operator had typed it: it becomes an **Override** at once, and saving follows the usual rules. A cancelled **Entry Action** leaves the **Value** unchanged. Its result is set even if the **Value** changed while it ran: the last write wins.
- One **Entry** may have several **Entry Actions**. While any of them runs, that **Entry**'s row on the **Runtime Panel** is locked: no other of its **Entry Actions** and no typing.
- An **Entry Action** may require confirmation, like a **Command**. It keeps running when the **Runtime Panel** closes and is stopped when its owner unregisters, like a **Command**. Two owners giving one **Entry** an **Entry Action** with the same **Title** are told apart by owner, like **Commands**.

- The **Log** and **Performance** are kept from app start only when at least one enabled **Panel Definition** shows them (for **Performance**, shows it or shows **Graphics**); otherwise nothing is kept or measured.
- Each **Panel Definition** chooses whether its **Runtime Panel** shows the **Log** and whether it shows **Performance**.
- The **Log** and **Performance** are available in every build, not only development builds.
- There is one **Log** per app, shared by every **Runtime Panel**: clearing it from one **Runtime Panel** clears it for all.
- The **Log** holds a limited number of **Log Entries**; when full, the oldest is dropped.
- A **Log Entry** identical to the one just before it (same type, message and stack trace) is counted on that one instead of added; identical **Log Entries** further apart stay separate, so the **Log** keeps its order.
- A **Log Entry** may have no stack trace, when the build does not record one for its type.
- A **Runtime Panel** shows how many errors arrived since its **Operator** last looked at its **Log**.
- A **Runtime Panel** that shows **Graphics** also needs **Performance** kept, for the live frame rate shown above the **Graphics Settings**.
- **Performance** is measured continuously while it is kept, so a **Runtime Panel** opened later shows recent history at once.
- Each **Panel Definition** chooses whether its **Runtime Panel** shows the **Display**. On a Windows build the **Display** tab works; in the Editor it is shown but inert (read-only, with a note); on other platforms it is not shown.
- An **Operator** edits **Display Settings** and applies them together. After applying, the **Operator** must confirm within a short time or the previous **Display** is restored; **Overrides** are written only on confirmation.
- Windowed: size is typed, position is left to the OS. Borderless: size and position are typed. Full Screen: size is chosen from the resolutions the monitor supports, and the monitor is chosen instead of a position.
- At app start, if **Display Setting** **Overrides** exist, they are applied whether or not any **Runtime Panel** shows the **Display**. With none, the **Display** is left as the app starts it.
- A **Reset** from the **Display** tab removes every **Display Setting** **Override** (after confirmation) and restores the **Display** the app started with. A **Reset** from the **Settings** tab never touches them.
- Each **Panel Definition** chooses whether its **Runtime Panel** shows **Graphics**.
- A **Graphics Setting** takes effect and becomes an **Override** as soon as the **Operator** changes it; there is no apply-and-confirm step as for the **Display**.
- Some **Graphics Settings** exist only when the app renders with URP; in an app that does not, they are not shown.
- Effects and options specific to one app (post-processing, field of view) are not **Graphics Settings**; the app defines them as its own **Entries**.
- Changing the **Quality Level** removes the **Overrides** of every other **Graphics Setting** except the frame-rate ones (VSync, Target FPS), so they follow the new baseline; frame-rate settings keep their **Values** and **Overrides** across a **Quality Level** change.
- At app start, **Graphics Setting** **Overrides** are applied after the app's own startup values, whether or not any **Runtime Panel** shows **Graphics**. A **Graphics Setting** with no **Override** is left as the app set it (e.g. the app's own frame-rate cap stays the baseline).
- A **Reset** from the **Graphics** tab removes every **Graphics Setting** **Override** (after confirmation) and restores **Graphics** as the app started. A **Reset** from the **Settings** tab or the **Display** tab never touches them.
- **Graphics** works in the Editor as in a build, and changing it never changes the project's own assets.

## Flagged ambiguities

- "Full Screen" meant both a **Display Mode** and a **Runtime Panel** filling the screen — resolved: the **Display Mode** is Full Screen; the panel behaviour is a layout option of the panel and not a **Display Mode**.

- "Profiler" was used to mean the **Performance** tab — resolved: **Performance**, so it is not confused with the Unity Profiler window.

- "Slot" used to mean one of several named saves — resolved: slots are removed; there is one **Save File**.
- "Constant" (`DSMConstant`) used to mean both the default value and the key name — resolved: defaults live in **Entry Definitions**; there is no generated constant class.
