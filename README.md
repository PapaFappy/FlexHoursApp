# FlexHours for Windows

A Windows desktop app for tracking working hours, monthly flex balances, and carry-over, with optional local Chrome sync from your own BambooHR timesheet.

## Features

- Monthly overview with a selectable daily calendar and an ahead/behind balance.
- Daily breakdowns for work, PTO, sick leave, holidays, other paid leave, and unpaid time.
- Flex history, month completion, carry-over, and quarterly reset calculations.
- Chrome extension with automatic refresh and support for multiple watched months.
- Manual entry and browser/pasted-summary import as fallbacks.
- Appearance settings, custom colours, and compact layouts.

Missing daily readings remain unknown. The monthly balance uses the BambooHR Pay Period summary; daily differences require reconciled data. Future-month leave stays separate from the current balance.

## Requirements

- Windows with the 64-bit .NET Framework 4.x compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe` and Windows Forms support.
- PowerShell to run the build, test, and setup scripts.
- Google Chrome 120 or later for optional Chrome sync.
- Node.js with the built-in `node --test` runner for extension development tests.

The desktop app does not require Node.js. This repository builds directly with the .NET Framework compiler; there is no Visual Studio solution or NuGet restore step.

## Build and run

Clone or download the repository, open PowerShell in its root directory, and run:

```powershell
.\build.ps1
.\bin\FlexHours-Browser.exe
```

The build produces two executables in `bin`. `FlexHours-Browser.exe` is the desktop app; `FlexHours-ChromeHost.exe` is its native messaging helper. Keep both together and launch only the desktop app.

If PowerShell blocks scripts, review your Windows execution policy and organisation's policy before running them.

## Set up Chrome sync

1. Build the app and keep the project in a stable folder.
2. Open `chrome://extensions`, enable **Developer mode**, choose **Load unpacked**, and select this project's `chrome-extension` folder.
3. Copy the extension ID displayed by Chrome and run:

   ```powershell
   .\setup-chrome.ps1 -ExtensionId <your-extension-id>
   ```

4. Sign in to your own BambooHR account in Chrome and open the current month's Timesheet with its daily list and monthly summary loaded.
5. Open the extension, select the matching month, choose **Connect & enable auto refresh**, and grant access to your company website.
6. In the desktop app's **Chrome sync** dialog, enter the same company, enable receiving updates, and save.

Setup writes a native-host manifest under `%LOCALAPPDATA%\FlexHours\Chrome` and registers it under your Windows account in `HKCU\Software\Google\Chrome\NativeMessagingHosts\com.flexhours.chrome_sync`. Administrator access is not required.

Chrome must be running and signed in for refreshes. The default interval is five minutes. Use **Refresh now** in the app or extension to request a new reading.

For an in-place update, rebuild, restart the app, and reload the extension in `chrome://extensions`. Keep the existing paths. If you move the project, rerun native-host setup and check the unpacked extension's location and ID.

See [Chrome sync details](docs/CHROME-SYNC.txt) for future-month watches, pause controls, reconciliation, and same-URL limitations.

## Data and limitations

App state is stored locally in `%LOCALAPPDATA%\FlexHours\months.json`; Chrome capture files and configuration are stored under the same FlexHours directory. Chrome sync uses your normal website session and does not require exporting passwords or cookies. Captured timesheet data is passed to the local helper rather than a remote FlexHours service.

Timesheet parsing depends on BambooHR's page layout. Automated tests use synthetic data; they do not verify a live authenticated account. Unread or unreconciled entries need review. Upcoming leave covers dates read from timesheets, not annual leave entitlement or a separate Time Off request list. Manual entry and import remain available as fallbacks.

## Development and tests

```powershell
# Run C# tests and require JavaScript tests (Node.js must be installed).
.\test.ps1 -RequireChromeTests

# Run C# tests; run JavaScript tests if Node.js is available.
.\test.ps1

# Test, build, and create FlexHours-Windows.zip beside the project folder.
.\package.ps1
```

Packaging runs JavaScript tests when Node.js is available. Run the strict test command above before a release to ensure they were not skipped.

Generated tests, binaries, previews, and reports are excluded from Git. Tests use isolated synthetic state. See [testing notes](docs/TESTING.txt) for coverage and limitations.

| Path | Purpose |
| --- | --- |
| `source/` | C# domain logic, models, persistence, imports, Chrome integration, and UI |
| `chrome-extension/` | Manifest V3 extension and local sync logic |
| `tests/` | C# suites and JavaScript extension tests |
| `assets/` | App icon and icon-generation script |
| `docs/` | Usage, design, and testing notes |
| `build.ps1` | Compile the app and native helper |
| `setup-chrome.ps1` | Register the helper for the current Windows account |
| `package.ps1` | Build a distributable ZIP |

## Upload to GitHub

Create an empty GitHub repository, then run these commands from this project folder (initialise Git first if needed):

```powershell
git init -b main
git add .
git diff --cached --stat
git commit -m "Prepare FlexHours for GitHub"
git remote add origin https://github.com/YOUR-USERNAME/YOUR-REPOSITORY.git
git push -u origin main
```

Review staged files before committing. Keep local timesheets, browser profiles, credentials, and generated outputs out of the repository. Distribute built ZIPs through GitHub Releases when needed.

No licence is currently declared. The project owner should choose and add a licence before offering the code for reuse.
