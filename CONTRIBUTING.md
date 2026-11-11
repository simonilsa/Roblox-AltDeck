# Contributing

Thanks for helping! The easiest and most useful contribution is a **translation**.

## Translations
1. Copy `src/Languages/en.json` and name the copy after the language code, for example `th.json` (Thai), `ko.json` (Korean) or `pl.json` (Polish).
2. Set `"_name"` to the language's own name, such as `"ไทย"` or `"한국어"`.
3. Translate the values only. Keep the keys, and keep `{0}`, `{1}` and `\n` exactly as they are.
4. Optional: add a flag picture as `src/Flags/<code>.png` (3:2, for example 90 × 60).
5. Open a pull request. The language appears in the app's Language menu automatically.

Fixing an existing translation works the same way: edit the file and open a pull request.

## Bugs and ideas
Open an issue and describe what happened, what you expected, and your Windows version. **Never paste cookies, passwords or your `accounts.json` file.**

## Code
- C# / WinForms on .NET 10. Open `src/AltDeck.csproj` in Visual Studio or Rider.
- Keep the rules in SECURITY.md. Pull requests that send data anywhere other than `roblox.com`, or that touch the Roblox process, won't be accepted.
