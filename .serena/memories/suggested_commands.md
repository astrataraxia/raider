# Suggested Commands

- Windows workspace inspection: `Get-ChildItem -Force`.
- Search files and text with `rg --files` and `rg`.
- Run PowerShell spike tests with `powershell -ExecutionPolicy Bypass -File <script>`.
- Use `dotnet test`, `dotnet format --verify-no-changes`, and `dotnet build --no-restore -warnaserror`.
- .NET 10 SDK `10.0.301` is installed at `C:\Users\astra\.dotnet` and registered in the user `PATH` and `DOTNET_ROOT`.
- `C:\Users\astra\.local\bin\dotnet.cmd` ensures the normal `dotnet` command selects the user-installed SDK before the older system runtime.
- Playwright for .NET `1.60.0` and its Chromium `1223` browser are verified. After the test project build, install its browser with `pwsh -ExecutionPolicy Bypass -File <test-bin>\playwright.ps1 install chromium`.
- The default branch is `main`.
