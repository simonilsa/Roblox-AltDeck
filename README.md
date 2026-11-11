# AltDeck – multi-account launcher (unofficial)

> **Unofficial third-party tool.** AltDeck is not affiliated with, endorsed by or sponsored by Roblox Corporation. "Roblox" is a trademark of Roblox Corporation and is used here only to describe what the tool works with.

Launch any number of your own Roblox accounts at once: into the same game, into one specific server, or straight into a friend's game.

## Download
**[Download the latest AltDeck.exe](https://github.com/YOUR-GITHUB-NAME/AltDeck/releases/latest)** for Windows 10/11 (64-bit). It's a single file with no installer: double-click it to run.

- Only download AltDeck from this repository's Releases page. See [SECURITY.md](SECURITY.md) for how to verify the file.
- Windows may show **"Windows protected your PC"** because the exe isn't code-signed yet. Click **More info → Run anyway**. Every release is built from this source code by GitHub Actions, and the checksum is listed next to the download.

### Build it yourself (optional)
1. Download or clone this repository.
2. Double-click **Build.bat**. It needs the .NET 10 SDK; if that's missing, the download page opens for you.
3. **AltDeck.exe** appears in the folder.

## Using it
1. **Open AltDeck and follow the three numbered steps at the top of the window.** Each step turns green once it's done.
   - **Step 1 – Close Roblox:** if Roblox is already open, a yellow bar explains what to do. Click **Close Roblox for me**, or close the Roblox windows yourself. Multi-instance turns on by itself as soon as they're closed, so you don't need to restart the app. Keep AltDeck open while you play.
2. **Add accounts:** click **+ Add account** and type the username, password and an optional nickname.
   - A login window shows the official Roblox login page and fills the details in for you.
   - If Roblox shows a captcha or 2-step check, complete it there. The window closes by itself once the account is logged in.
   - Advanced: you can also add an account by pasting its `.ROBLOSECURITY` cookie.
3. **Pick accounts:** tick the boxes next to the accounts you want. Your selection is remembered.
4. **Pick where to go:**
   - **Join an experience:** paste a game link (`roblox.com/games/...`) or a Place ID. Click **Look up** or press Enter to see the game's name; it is saved in the dropdown for next time.
     - Optional: add a **Job ID** to put every account in the same server.
     - Private server links (`...privateServerLinkCode=...` or `roblox.com/share?code=...&type=Server`) also work.
   - **Join a friend:** click **Load friends**. Friends who are in a game are listed first. Pick one, or type any username or profile link.
5. Click **▶ Launch**. Accounts start one after another, using the delay you set (3–5 seconds works well).

Right-click an account to launch only that one, rename it, re-login, reorder it or remove it.

## Languages
On first start, AltDeck shows a list of languages with flags, with your Windows language preselected. You can switch any time with the **Language** menu (globe icon and flags) in the top-right corner. The change applies instantly and is remembered.

Available languages: English, Español, Português (Brasil), Filipino (Tagalog), Bahasa Indonesia, Français, Deutsch, Русский, Türkçe, Tiếng Việt.

**Add or fix a language:**
1. Copy `src/Languages/en.json` to a new file named with the language code, for example `th.json` or `ko.json`.
2. Translate the values, and keep `{0}`, `{1}` exactly as they are. Optionally add a flag picture `src/Flags/<code>.png` (3:2, for example 90 × 60). Without one, the globe is shown.
3. Run Build.bat again. The new language appears in the menu automatically.

Any text that's missing from a translation falls back to English.

## How it stays on the safe side
- **No client tampering.** AltDeck never opens, reads, writes or injects into the Roblox process or its memory, and it doesn't touch the anti-cheat. Multi-instance works by creating and holding two named Windows objects (`ROBLOX_singletonMutex` / `ROBLOX_singletonEvent`) *before* Roblox starts, inside AltDeck's own process. Clients are started through the normal `roblox-player:` link, exactly like pressing Play on the website.
- **Logins stay on your PC.**
  - Cookies and passwords are stored only in `%AppData%\AltDeck\accounts.json`, encrypted with Windows DPAPI. Only your Windows user on this PC can decrypt them.
  - There is no AltDeck server, no telemetry and no updater.
  - The code refuses to send any request that isn't HTTPS to a `roblox.com` address (see `SendAsync` in `RobloxApi.cs`).
- **Official login page.** You log in on Roblox's own page, and you solve any captcha or 2-step check yourself. Nothing is bypassed.
- **Gentle on Roblox's servers.**
  - All requests go through a throttle (at most about 4 per second).
  - If Roblox answers "too many requests" (HTTP 429), the app waits as long as Roblox asks before trying once more.
  - There is no background polling: requests only happen when you click something.
- **Branding.** The app uses an original name and icon, no Roblox logos, and shows an "unofficial / not affiliated" notice in its window.

## Your responsibility
- Only use accounts that are yours.
- Don't use alts for ban evasion, harassment, or faking engagement (visits, likes, favorites).
- Follow the rules of each experience: some games forbid alt or AFK farming.
- Roblox doesn't officially support running multiple clients. Using this tool is at your own risk.

## Good to know
- **Expired login:** if an account says "login expired", right-click it and choose **Re-login**. If you saved its password, it fills in automatically.
- **Joining a friend:** this works when the friend is in a game and their privacy settings allow you to join them.
- **Multi-instance OFF:** Roblox was already open when the app started. Close all Roblox windows (there's a button for it), then restart the app.
- **Login window doesn't open:** install the Microsoft Edge WebView2 Runtime (built into Windows 10/11).

## Website
The `website/` folder is AltDeck's homepage. GitHub builds and publishes it automatically, with one page per language.

- **One-time setup:** go to **Settings → Pages**, and under "Build and deployment" set **Source** to **GitHub Actions**. Then on the **Actions** tab, open **Website** and click **Run workflow**. The address appears in Settings → Pages; it looks like `https://YOUR-NAME.github.io/AltDeck/`.
- **Texts:** `website/i18n/<language>.json`. Edit, commit and push, and the site updates by itself.
- **Screenshots:** save them as `website/assets/screenshot-1.png`, `screenshot-2.png` and so on. The Screenshots section appears automatically once at least one exists.
- **Google Search Console:** paste the code Google gives you into `google_site_verification` in `website/config.json`. Submit `sitemap.xml` there.
- **Try it locally:** run `python website/build.py --out _site`, then open `_site/index.html`.

## Project layout
- `Build.bat`: one-click build to a standalone .exe
- `src/`: C# / WinForms source (.NET 10 LTS). Open `src/AltDeck.csproj` in Visual Studio or Rider.
  - `MainForm.cs`: main window
  - `StepCard.cs`: the numbered step cards
  - `Loc.cs`, `Languages/*.json`: translations
  - `Flags.cs`, `Flags/*.png`: flags in the language menu and the first-start language picker
  - `LoginWindow.cs`: embedded Roblox login
  - `RobloxApi.cs`: web API calls, throttle, domain guard, launch links
  - `MultiInstance.cs`: singleton lock
  - `AccountStore.cs`, `Dpapi.cs`: encrypted local storage
- `website/`: the homepage (template, styles, texts per language, build script)
