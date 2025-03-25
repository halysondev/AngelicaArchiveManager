using System;
using System.IO;
using System.Threading.Tasks;

namespace AngelicaArchiveManager.Core
{
    public static class FileExtensions
    {
        /// <summary>
        /// Asynchronously reads all bytes from a file
        /// </summary>
        /// <param name="path">The file path</param>
        /// <returns>A task that represents the asynchronous read operation</returns>
        public static async Task<byte[]> ReadAllBytesAsync(string path)
        {
            using (FileStream stream = new FileStream(
                path, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize: 4096, 
                useAsync: true))
            {
                byte[] buffer = new byte[stream.Length];
                await Task.Run(() => 
                {
                    stream.Read(buffer, 0, buffer.Length);
                });
                return buffer;
            }
        }
    }
} 