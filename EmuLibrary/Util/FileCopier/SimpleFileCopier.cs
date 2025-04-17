using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace EmuLibrary.Util.FileCopier
{
    public class SimpleFileCopier : BaseFileCopier, IFileCopier
    {
        public SimpleFileCopier(FileSystemInfo source, DirectoryInfo destination) : base(source, destination) { }

        protected override void Copy()
        {
            if (Source is DirectoryInfo)
            {
                CopyDirectoryContents(Source as DirectoryInfo, Destination);
                return;
            }

            File.Copy(Source.FullName, Path.Combine(Destination.FullName, Source.Name), true);
        }

        protected override void CopyWithProgress(IProgress<FileCopyProgress> progress)
        {
            if (Source is DirectoryInfo)
            {
                CopyDirectoryContentsWithProgress(Source as DirectoryInfo, Destination, progress);
                return;
            }
            
            // For single file, we can provide more detailed progress
            if (Source is FileInfo fileInfo)
            {
                long totalBytes = fileInfo.Length;
                string destPath = Path.Combine(Destination.FullName, Source.Name);
                
                // Use buffer for better performance
                const int bufferSize = 1024 * 1024; // 1MB buffer
                byte[] buffer = new byte[bufferSize];
                
                using (var sourceStream = new FileStream(Source.FullName, FileMode.Open, FileAccess.Read))
                using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    long totalBytesRead = 0;
                    int bytesRead;
                    
                    // Track copy speed
                    var stopwatch = Stopwatch.StartNew();
                    long lastReportTime = 0;
                    
                    while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        destStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        
                        // Report progress periodically (every 100ms)
                        if (stopwatch.ElapsedMilliseconds - lastReportTime > 100)
                        {
                            lastReportTime = stopwatch.ElapsedMilliseconds;
                            
                            double progressPercentage = (double)totalBytesRead / totalBytes * 100;
                            long bytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                                ? totalBytesRead * 1000 / stopwatch.ElapsedMilliseconds 
                                : 0;
                            double secondsRemaining = bytesPerSecond > 0 
                                ? (double)(totalBytes - totalBytesRead) / bytesPerSecond
                                : 0;
                            
                            progress.Report(new FileCopyProgress
                            {
                                BytesTransferred = totalBytesRead,
                                TotalBytes = totalBytes,
                                ProgressPercentage = progressPercentage,
                                BytesPerSecond = bytesPerSecond,
                                SecondsRemaining = secondsRemaining
                            });
                        }
                    }
                    
                    // Final progress report
                    progress.Report(new FileCopyProgress
                    {
                        BytesTransferred = totalBytes,
                        TotalBytes = totalBytes,
                        ProgressPercentage = 100,
                        BytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                            ? totalBytes * 1000 / stopwatch.ElapsedMilliseconds 
                            : 0,
                        SecondsRemaining = 0
                    });
                }
            }
        }

        private static void CopyDirectoryContents(DirectoryInfo source, DirectoryInfo destination)
        {
            Directory.CreateDirectory(destination.FullName);

            foreach (var file in source.GetFiles())
            {
                file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
            }

            foreach (var subDirectory in source.GetDirectories())
            {
                CopyDirectoryContents(subDirectory, destination.CreateSubdirectory(subDirectory.Name));
            }
        }
        
        private void CopyDirectoryContentsWithProgress(DirectoryInfo source, DirectoryInfo destination, IProgress<FileCopyProgress> progress)
        {
            // Calculate total size of the directory
            long totalBytes = CalculateTotalSize(source);
            long copiedBytes = 0;
            
            // Track copy speed
            var stopwatch = Stopwatch.StartNew();
            long lastReportTime = 0;
            
            Directory.CreateDirectory(destination.FullName);
            
            foreach (var file in source.GetFiles())
            {
                string destPath = Path.Combine(destination.FullName, file.Name);
                
                // Copy the file
                file.CopyTo(destPath, true);
                
                // Update progress
                copiedBytes += file.Length;
                
                // Report progress periodically
                if (stopwatch.ElapsedMilliseconds - lastReportTime > 100)
                {
                    lastReportTime = stopwatch.ElapsedMilliseconds;
                    
                    double progressPercentage = (double)copiedBytes / totalBytes * 100;
                    long bytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                        ? copiedBytes * 1000 / stopwatch.ElapsedMilliseconds 
                        : 0;
                    double secondsRemaining = bytesPerSecond > 0 
                        ? (double)(totalBytes - copiedBytes) / bytesPerSecond
                        : 0;
                    
                    progress.Report(new FileCopyProgress
                    {
                        BytesTransferred = copiedBytes,
                        TotalBytes = totalBytes,
                        ProgressPercentage = progressPercentage,
                        BytesPerSecond = bytesPerSecond,
                        SecondsRemaining = secondsRemaining
                    });
                }
            }
            
            foreach (var subDirectory in source.GetDirectories())
            {
                CopyDirectoryContentsWithProgress(subDirectory, destination.CreateSubdirectory(subDirectory.Name), progress);
            }
            
            // Final progress report at the end
            if (source.Parent == Source)
            {
                progress.Report(new FileCopyProgress
                {
                    BytesTransferred = totalBytes,
                    TotalBytes = totalBytes,
                    ProgressPercentage = 100,
                    BytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                        ? totalBytes * 1000 / stopwatch.ElapsedMilliseconds 
                        : 0,
                    SecondsRemaining = 0
                });
            }
        }
    }
}
