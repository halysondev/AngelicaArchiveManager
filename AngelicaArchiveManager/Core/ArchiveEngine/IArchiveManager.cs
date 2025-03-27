using System.Collections.Generic;
using AngelicaArchiveManager.Interfaces;
using System.Threading.Tasks;

namespace AngelicaArchiveManager.Core.ArchiveEngine
{
    public interface IArchiveManager
    {
        List<IArchiveEntry> Files { get; set; }

        event Events.LoadData LoadData;
        event Events.SetProgress SetProgress;
        event Events.SetProgressMax SetProgressMax;
        event Events.SetProgressNext SetProgressNext;

        Task AddFiles(List<string> files, string srcdir, string dstdir);
        Task Defrag();
        byte[] GetFile(IArchiveEntry entry, bool reload = true);
        List<byte[]> GetFiles(List<IArchiveEntry> files);
        void ReadFileTable();
        Task SaveFileTable(long filetable = -1);
        void UnpackFiles(string srcdir, List<IArchiveEntry> files, string dstdir);
        
        // Async methods for improved performance
        Task<byte[]> GetFileAsync(IArchiveEntry entry, bool reload = true);
        Task<List<byte[]>> GetFilesAsync(List<IArchiveEntry> files);
        Task UnpackFilesAsync(string srcdir, List<IArchiveEntry> files, string dstdir);
        Task ReadFileTableAsync();
        Task SaveFileTableAsync(long filetable = -1);
        Task AddFilesAsync(List<string> files, string srcdir, string dstdir);
        Task DefragAsync();
    }
}