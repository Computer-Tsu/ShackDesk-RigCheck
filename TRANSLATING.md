# Translating RigCheck

Translations are community-contributed and always welcome. **Any language is
accepted** — there is no predefined list. You do not need to be a programmer:
the work is copying one file and translating the text in it.

**Status:** the translation mechanism is in place and working. RigCheck's text
is being moved into the string file in stages; the file grows with each build.
A translation made today stays valid — new keys simply fall back to English
until you add them.

---

## How it works

All user-visible text lives in `Resources/Strings.resx` (English). A
translation is a copy of that file named for its language:

| Language | File |
| --- | --- |
| English (source) | `Resources/Strings.resx` |
| German | `Resources/Strings.de.resx` |
| Japanese | `Resources/Strings.ja.resx` |
| Brazilian Portuguese | `Resources/Strings.pt-BR.resx` |

RigCheck picks the file matching the Windows display language. Any key missing
from a translation falls back to English, so a partial translation is still
useful.

## Steps

1. **Check for existing work.** Look at the
   [open pull requests](https://github.com/Computer-Tsu/ShackDesk-RigCheck/pulls)
   and [translation issues](https://github.com/Computer-Tsu/ShackDesk-RigCheck/issues?q=label%3Atranslation)
   for your language before starting, so two people don't translate the same thing.
2. **Claim it.** Open an issue titled "Translation: <language>" so others know.
3. **Copy** `Resources/Strings.resx` to `Resources/Strings.<tag>.resx`, using the
   IETF language tag (`de`, `fr`, `ja`, `es`, `pt-BR`, `zh-Hans`, …).
4. **Add a header comment** at the top of your file with the language, your
   name and callsign, and the date. This is how you are credited.
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
