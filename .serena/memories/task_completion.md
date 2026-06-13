# Task Completion

- Run the smallest relevant test first, then broaden checks according to change risk.
- For C# changes, run `dotnet test`, `dotnet format --verify-no-changes`, and `dotnet build --no-restore -warnaserror`.
- Keep real external API smoke tests separate from repeatable fixture and local HTTP tests.
- Review scope, unrelated changes, missing tests, and secret exposure before completion.
- Update the active plan when one exists. Compress completed plans into `RELEASES.md`.
