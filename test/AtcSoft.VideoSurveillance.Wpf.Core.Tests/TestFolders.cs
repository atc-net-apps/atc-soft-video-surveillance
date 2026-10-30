namespace AtcSoft.VideoSurveillance.Wpf.Core;

/// <summary>
/// Removes a test's temp folder. A file just written (a test .mkv) can be held for a moment
/// by something outside the test, such as the virus scanner, in a full parallel run; the
/// delete is tried again a few times, and a folder left behind in the temp folder is harmless.
/// Clean-up never fails a test that passed.
/// </summary>
internal static class TestFolders
{
    public static void Delete(string path)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                if (System.IO.Directory.Exists(path))
                {
                    System.IO.Directory.Delete(path, recursive: true);
                }

                return;
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(100 * attempt);
            }
        }
    }
}