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
                path: path,
                mode: FileMode.Open,
                access: FileAccess.Read,
                share: FileShare.Read,
                bufferSize: 4096, 
                useAsync: true))
            {
                byte[] buffer = new byte[stream.Length];
                int bytesRead = 0;
                int totalBytesRead = 0;
                
                // Ensure we read the exact number of bytes
                while (totalBytesRead < buffer.Length)
                {
                    bytesRead = await stream.ReadAsync(buffer, totalBytesRead, buffer.Length - totalBytesRead);
                    if (bytesRead == 0)
                        break; // End of stream reached prematurely
                    totalBytesRead += bytesRead;
                }
                
                return buffer;
            }
        }
    }
}