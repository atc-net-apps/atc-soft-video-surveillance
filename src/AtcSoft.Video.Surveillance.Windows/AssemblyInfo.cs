// Pin DllImport search to System32 — these P/Invokes target Windows
// system DLLs (mfplat.dll, mf.dll, ole32.dll). Reduces the search-path
// attack surface and silences CA5392.
[assembly: System.Runtime.InteropServices.DefaultDllImportSearchPaths(
    System.Runtime.InteropServices.DllImportSearchPath.System32)]