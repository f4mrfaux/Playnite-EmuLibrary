using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmuLibrary.Util.FileCopier
{
    public abstract class BaseFileCopier : IFileCopier
    {
        public FileSystemInfo Source { get; set; }
        public DirectoryInfo Destination { get; set; }

        internal BaseFileCopier(FileSystemInfo source, DirectoryInfo destination)
        {
            Source = source;
            Destination = destination;
        }

        protected abstract void Copy();
        protected abstract void CopyWithProgress(IProgress<FileCopyProgress> progress);

        public async Task CopyAsync(CancellationToken cancellationToken)
        {
            if (Source.FullName.IsNullOrEmpty() || !Source.Exists)
            {
                throw new Exception($"source path \"{Source.FullName}\" does not exist or is invalid");
            }
            if (Destination.FullName.IsNullOrEmpty())
            {
                throw new Exception($"destination path \"{Destination.FullName}\" is not valid");
            }

            await Task.Run(() =>
                {
                    Copy();
                },
                cancellationToken
            );
        }
        
        public async Task CopyWithProgressAsync(CancellationToken cancellationToken, IProgress<FileCopyProgress> progress)
        {
            if (Source.FullName.IsNullOrEmpty() || !Source.Exists)
            {
                throw new Exception($"source path \"{Source.FullName}\" does not exist or is invalid");
            }
            if (Destination.FullName.IsNullOrEmpty())
            {
                throw new Exception($"destination path \"{Destination.FullName}\" is not valid");
            }

            await Task.Run(() =>
                {
                    CopyWithProgress(progress);
                },
                cancellationToken
            );
        }
        
        protected long CalculateTotalSize(FileSystemInfo source)
        {
            if (source is FileInfo file)
            {
                return file.Length;
            }
            else if (source is DirectoryInfo dir)
            {
                long size = 0;
                foreach (var fileInfo in dir.GetFiles("*", SearchOption.AllDirectories))
                {
                    size += fileInfo.Length;
                }
                return size;
            }
            
            return 0;
        }
    }
}
