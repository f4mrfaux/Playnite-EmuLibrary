using Microsoft.VisualBasic.FileIO;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace EmuLibrary.Util.FileCopier
{
    public class WindowsFileCopier : BaseFileCopier, IFileCopier
    {
        public WindowsFileCopier(FileSystemInfo source, DirectoryInfo destination) : base(source, destination) { }

        protected override void Copy()
        {
            try
            {
                if (Source is DirectoryInfo)
                {
                    FileSystem.CopyDirectory(Source.FullName, Destination.FullName, UIOption.AllDialogs);
                    return;
                }
                FileSystem.CopyFile(Source.FullName, Path.Combine(Destination.FullName, Source.Name), UIOption.AllDialogs);
            }
            catch (Exception ex)
            {
                try
                {
                    // For directories, some child nodes may have been partially copied before cancellation. Clean these up.
                    if (Source is DirectoryInfo)
                    {
                        FileSystem.DeleteDirectory(Destination.FullName, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                    }
                    // Remove the file if for some reason it still exists after user cancellation.
                    else if (Source is FileInfo)
                    {
                        FileSystem.DeleteFile(Destination.FullName, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                    }
                }
                catch { }
                if (ex is OperationCanceledException)
                {
                    throw new WindowsCopyDialogClosedException("The user cancelled the copy request", ex);
                }
                throw new Exception($"Unable to copy source \"{Source.FullName}\" to destination \"{Destination.FullName}\"", ex);
            }
        }
        
        protected override void CopyWithProgress(IProgress<FileCopyProgress> progress)
        {
            try
            {
                // For Windows copy dialog, we'll still show the dialog but also track progress
                
                // First, start a separate thread to track progress while the Windows dialog is shown
                long totalBytes = CalculateTotalSize(Source);
                var targetPath = Source is DirectoryInfo
                    ? Destination.FullName
                    : Path.Combine(Destination.FullName, Source.Name);
                
                var progressThread = new Thread(() => 
                {
                    TrackCopyProgress(Source, targetPath, totalBytes, progress);
                });
                progressThread.IsBackground = true;
                progressThread.Start();
                
                // Now start the copy operation with Windows dialog
                if (Source is DirectoryInfo)
                {
                    FileSystem.CopyDirectory(Source.FullName, Destination.FullName, UIOption.AllDialogs);
                }
                else
                {
                    FileSystem.CopyFile(Source.FullName, Path.Combine(Destination.FullName, Source.Name), UIOption.AllDialogs);
                }
            }
            catch (Exception ex)
            {
                try
                {
                    // For directories, some child nodes may have been partially copied before cancellation. Clean these up.
                    if (Source is DirectoryInfo)
                    {
                        FileSystem.DeleteDirectory(Destination.FullName, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                    }
                    // Remove the file if for some reason it still exists after user cancellation.
                    else if (Source is FileInfo)
                    {
                        string destFile = Path.Combine(Destination.FullName, Source.Name);
                        if (File.Exists(destFile))
                        {
                            FileSystem.DeleteFile(destFile, UIOption.OnlyErrorDialogs, RecycleOption.DeletePermanently);
                        }
                    }
                }
                catch { }
                
                if (ex is OperationCanceledException)
                {
                    throw new WindowsCopyDialogClosedException("The user cancelled the copy request", ex);
                }
                throw new Exception($"Unable to copy source \"{Source.FullName}\" to destination \"{Destination.FullName}\"", ex);
            }
        }
        
        private void TrackCopyProgress(FileSystemInfo source, string destination, long totalBytes, IProgress<FileCopyProgress> progress)
        {
            var stopwatch = Stopwatch.StartNew();
            long lastReportTime = 0;
            
            try
            {
                bool isSourceFile = source is FileInfo;
                bool isComplete = false;
                
                // Report initial progress
                progress.Report(new FileCopyProgress
                {
                    BytesTransferred = 0,
                    TotalBytes = totalBytes,
                    ProgressPercentage = 0,
                    BytesPerSecond = 0,
                    SecondsRemaining = -1 // Unknown yet
                });
                
                while (!isComplete)
                {
                    // Pause between checks
                    Thread.Sleep(100);
                    
                    long currentBytes = 0;
                    
                    if (isSourceFile)
                    {
                        // For files, check if the destination file exists and get its size
                        if (File.Exists(destination))
                        {
                            currentBytes = new FileInfo(destination).Length;
                            
                            // Check if copy is complete
                            if (currentBytes >= totalBytes)
                            {
                                isComplete = true;
                                currentBytes = totalBytes; // Ensure we show 100%
                            }
                        }
                    }
                    else
                    {
                        // For directories, recursively calculate the size of the destination
                        if (Directory.Exists(destination))
                        {
                            currentBytes = CalculateDirectorySize(new DirectoryInfo(destination));
                            
                            // Check if copy might be complete
                            if (currentBytes >= totalBytes)
                            {
                                // Give it a bit more time to finalize
                                Thread.Sleep(500);
                                currentBytes = CalculateDirectorySize(new DirectoryInfo(destination));
                                
                                if (currentBytes >= totalBytes)
                                {
                                    isComplete = true;
                                    currentBytes = totalBytes; // Ensure we show 100%
                                }
                            }
                        }
                    }
                    
                    // Report progress periodically
                    if (stopwatch.ElapsedMilliseconds - lastReportTime > 100 || isComplete)
                    {
                        lastReportTime = stopwatch.ElapsedMilliseconds;
                        
                        double progressPercentage = totalBytes > 0 ? (double)currentBytes / totalBytes * 100 : 0;
                        long bytesPerSecond = stopwatch.ElapsedMilliseconds > 0 
                            ? currentBytes * 1000 / stopwatch.ElapsedMilliseconds 
                            : 0;
                        double secondsRemaining = bytesPerSecond > 0 && totalBytes > currentBytes 
                            ? (double)(totalBytes - currentBytes) / bytesPerSecond
                            : 0;
                        
                        progress.Report(new FileCopyProgress
                        {
                            BytesTransferred = currentBytes,
                            TotalBytes = totalBytes,
                            ProgressPercentage = progressPercentage,
                            BytesPerSecond = bytesPerSecond,
                            SecondsRemaining = secondsRemaining
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail, since this is just a tracking thread
            }
        }
        
        private long CalculateDirectorySize(DirectoryInfo directory)
        {
            long size = 0;
            
            try
            {
                // Add size of all files
                foreach (FileInfo file in directory.GetFiles())
                {
                    size += file.Length;
                }
                
                // Add size of all subdirectories
                foreach (DirectoryInfo dir in directory.GetDirectories())
                {
                    size += CalculateDirectorySize(dir);
                }
            }
            catch (Exception)
            {
                // Ignore exceptions during size calculation
            }
            
            return size;
        }
    }

    public class WindowsCopyDialogClosedException : Exception
    {
        public WindowsCopyDialogClosedException(string message, Exception ex) : base(message, ex) { }
    }
}
