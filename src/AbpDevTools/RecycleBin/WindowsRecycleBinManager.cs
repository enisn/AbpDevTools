using System.Runtime.InteropServices;
using System.Text;

namespace AbpDevTools.RecycleBin;

public class WindowsRecycleBinManager : IRecycleBinManager
{
    public Task SendToRecycleBinAsync(string filePath)
    {
        return SendToRecycleBinAsync(new[] { filePath });
    }

    public Task SendToRecycleBinAsync(IEnumerable<string> filePaths)
    {
        return Task.Run(() =>
        {
            var pathList = filePaths.ToList();
            if (!pathList.Any())
                return;

            // First attempt: Try batch operation for best performance
            var batchResult = TryBatchOperation(pathList);
            if (batchResult.Success)
                return;

            // Fallback: Process individually to identify problematic files
            var failedFiles = ProcessIndividually(pathList);

            if (failedFiles.Any())
            {
                var errorDetails = string.Join("; ", failedFiles.Take(3).Select(f => $"'{Path.GetFileName(f.Path)}': {f.ErrorMessage} (Code: {f.ErrorCode})"));
                if (failedFiles.Count > 3)
                    errorDetails += $" and {failedFiles.Count - 3} more files";
                    
                throw new InvalidOperationException($"Failed to move {failedFiles.Count} of {pathList.Count} files to recycle bin: {errorDetails}. You can try --force-delete option to permanently delete files instead.");
            }
        });
    }

    private (bool Success, int ErrorCode) TryBatchOperation(List<string> pathList)
    {
        StringBuilder builder = new StringBuilder();
        foreach (string filePath in pathList)
        {
            builder.Append(Path.GetFullPath(filePath) + '\0');
        }
        builder.Append('\0');

        SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT
        {
            wFunc = FO_DELETE,
            pFrom = builder.ToString(),
            fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
        };

        int result = SHFileOperation(ref shf);
        return (result == 0, result);
    }

    private List<(string Path, int ErrorCode, string ErrorMessage)> ProcessIndividually(List<string> pathList)
    {
        var failedFiles = new List<(string Path, int ErrorCode, string ErrorMessage)>();

        foreach (string filePath in pathList)
        {
            var fullPath = Path.GetFullPath(filePath);
            var pathForOperation = fullPath + '\0' + '\0';

            SHFILEOPSTRUCT shf = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = pathForOperation,
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT
            };

            int result = SHFileOperation(ref shf);
            
            if (result != 0)
            {
                string errorMessage = GetErrorMessage(result);
                failedFiles.Add((filePath, result, errorMessage));
            }
        }

        return failedFiles;
    }

    private const int FO_DELETE = 0x0003;
    private const int FOF_ALLOWUNDO = 0x40;
    private const int FOF_NOCONFIRMATION = 0x10;
    private const int FOF_SILENT = 0x0004;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private static string GetErrorMessage(int errorCode)
    {
        return errorCode switch
        {
            120 => "This function is not supported on this system",
            2 => "The system cannot find the file specified",
            3 => "The system cannot find the path specified",
            5 => "Access is denied",
            32 => "The process cannot access the file because it is being used by another process",
            1223 => "The operation was cancelled by the user",
            _ => $"Unknown error"
        };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.U4)]
        public int wFunc;
        public string pFrom;
        public string pTo;
        public short fFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string lpszProgressTitle;
    }
} 