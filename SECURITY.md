# Security

AltDeck handles Roblox logins, so here is exactly what it does with them, and how to check for yourself.

## What happens to your login
- You log in on Roblox's own login page, inside a window in the app. Captchas and 2-step checks are yours to complete; nothing is bypassed.
- The session cookie, and your password if you choose to save it, are stored only in `%AppData%\AltDeck\accounts.json`. They are encrypted with Windows DPAPI, so only your Windows user on your PC can decrypt them.
- AltDeck has **no server, no telemetry and no auto-updater**.
- The code refuses to send any request that isn't HTTPS to a `roblox.com` address (`SendAsync` in `src/RobloxApi.cs`).
- AltDeck never reads, writes or injects into the Roblox process.

## How to verify a download
1. Only download `AltDeck.exe` from this repository's **Releases** page. Any other copy, such as a re-upload, a link shortener or a "cracked/premium" version, may be malware.
2. Compare the file's SHA-256 with the checksum in the release (`AltDeck.exe.sha256`). In PowerShell, run:
   ```
   Get-FileHash .\AltDeck.exe -Algorithm SHA256
   ```
3. Every release exe is built by GitHub Actions straight from this source code. The build log is public on the **Actions** tab.

AltDeck will never ask you to send your cookie, password or `accounts.json` to anyone. If someone asks for them, including someone claiming to be the developer, it's a scam.

## Reporting a problem
If you find a security issue, open a GitHub issue **without** any cookies, passwords or account details in it. If it needs to stay private, use GitHub's "Report a vulnerability" button on the Security tab.
