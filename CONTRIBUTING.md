# Contributing to OpenWindow

Thank you for your interest in contributing to **OpenWindow**!

## Development Guidelines

### Prerequisites
- Windows 10 (1903+) or Windows 11 (64-bit)
- .NET 8.0 SDK (`dotnet --version` >= 8.0.400)
- Visual Studio 2022, JetBrains Rider, or VS Code with C# DevKit

### Building the Solution
```powershell
dotnet build OpenWindow.sln -c Release
```

### Running Tests
All changes must pass unit and integration tests before submitting a Pull Request:
```powershell
dotnet test OpenWindow.sln -c Release
```

### Code Style & Architecture
- Follow Clean Code principles (Uncle Bob).
- Do not import WPF UI controls or classes into `CodeByatta.Core`.
- P/Invoke signatures must live in `CodeByatta.Core.Interop`.
- Always check `User32.IsWindow(hwnd)` before calling Win32 modification APIs.
- WinEvent hooks must dispatch safely to the WPF Dispatcher.

### Submitting Pull Requests
1. Fork the repository.
2. Create a feature branch: `git checkout -b feature/my-enhancement`.
3. Follow [Conventional Commits](https://www.conventionalcommits.org/):
   - `feat(...)`: New feature or capability.
   - `fix(...)`: Bug fix.
   - `refactor(...)`: Code cleanup without behavior change.
   - `test(...)`: Adding or updating test suites.
4. Ensure `dotnet test` passes with 0 failures.
5. Submit your PR with a concise description of the change and verification evidence.
