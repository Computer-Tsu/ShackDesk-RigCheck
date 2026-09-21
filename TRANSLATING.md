# Translating RigCheck

Translations are community-contributed and always welcome. **Any language is
accepted** — there is no predefined list. You do not need to be a programmer:
the work is copying one file and translating the text in it.

---

## Current languages

This table is updated automatically whenever a string file changes.
"Machine translated" files were produced by translation software and need a
native-speaking ham to read them through — correcting one is faster than
starting from nothing, and it is the most useful thing you can do.

<!-- coverage:start -->
| Language | File | Coverage | Status |
| --- | --- | --- | --- |
| English (source) | `Strings.resx` | 100% | Source |
<!-- coverage:end -->

---

## How it works

All user-visible text lives in `Resources/Strings.resx` (English). A
translation is a copy of that file named for its language, for example
`Resources/Strings.de.resx` for German or `Resources/Strings.pt-BR.resx` for
Brazilian Portuguese.

RigCheck picks the file matching the Windows display language. Any key missing
from a translation falls back to English, so a partial translation is still
useful, and a translation made today stays valid as new strings are added.

Two special entries at the top of every translation file:

| Entry | Put here |
| --- | --- |
| `Meta_Translator` | Your name and callsign, e.g. `Hans Mueller, DL1ABC`. Shown in Help › About and in [TRANSLATORS.md](TRANSLATORS.md). |
| `Meta_Status` | `machine-translated` until a native speaker has reviewed the whole file, then `reviewed`. |

## Steps

1. **Check for existing work.** Look at the
   [open pull requests](https://github.com/Computer-Tsu/ShackDesk-RigCheck/pulls)
   and [translation issues](https://github.com/Computer-Tsu/ShackDesk-RigCheck/issues?q=label%3Atranslation)
   for your language before starting, so two people don't translate the same thing.
2. **Claim it.** Open an issue titled "Translation: <language>" so others know.
3. **Copy** `Resources/Strings.resx` to `Resources/Strings.<tag>.resx`, using the
   IETF language tag (`de`, `fr`, `ja`, `es`, `pt-BR`, `zh-Hans`, …).
4. **Fill in `Meta_Translator`** with your name and callsign, and set
   `Meta_Status` to `machine-translated` or `reviewed`. This is how you are
   credited, automatically, in the app and in this repository.
5. **Translate only the text inside `<value>` tags.** Do not change the
   `name="…"` keys — the app uses them to find each string. The `<comment>`
   on each entry explains where the text appears and what any placeholders mean.
6. **Keep placeholders exactly:** `{0}`, `{1}` are replaced at runtime with values
   such as the app name or a date.
7. **Keep keyboard shortcuts sensible:** an underscore before a letter
   (`_Help`) makes that letter the Alt-key shortcut. Choose a letter that exists
   in your translation and is not already used by another item in the same menu.
8. **Expect longer text.** German and French often run 30% longer than English.
   The layout allows for it, but very long strings may wrap.
9. **Submit** a pull request, or attach the file to your issue if you prefer not
   to use git. By submitting you agree to the [CLA](CLA.md).

Machine translation is a fine starting point. What makes a translation good is
a native-speaking ham reading it through and fixing the radio terms.

## Glossary

**Never translate** — these are names:

ShackDesk · RigCheck · PortPane · Hamlib · rigctl · rigctld · WSJT-X · Fldigi ·
Flrig · JS8Call · Winlink · OmniRig · radio model names (IC-7300, FT-991A, …) ·
callsigns

**Usually left in English by hams** — use whatever your local club says:

CAT · CI-V · PTT · VFO · COM port · baud · RTS · DTR · VOX · S-meter

The tagline "Know your rig is ready" is part of the brand and stays in English.

## Credit

Translators are listed by name and callsign in **Help › About** inside RigCheck
and in `TRANSLATORS.md` in this repository. Your GitHub contribution also
appears on the repository's contributors page.

## Questions

Ask in [Discussions](https://github.com/Computer-Tsu/ShackDesk-RigCheck/discussions).
