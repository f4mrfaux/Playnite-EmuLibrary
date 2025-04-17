using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.FileCopier
{
    public interface IFileCopier
    {
        FileSystemInfo Source { get; set; }
        DirectoryInfo Destination { get; set; }
        Task CopyAsync(CancellationToken cancellationToken);
        Task CopyWithProgressAsync(CancellationToken cancellationToken, IProgress<FileCopyProgress> progress);
    }
    
    public class FileCopyProgress
    {
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public long BytesPerSecond { get; set; }
        public double SecondsRemaining { get; set; }
        public double ProgressPercentage { get; set; }
    }
}
