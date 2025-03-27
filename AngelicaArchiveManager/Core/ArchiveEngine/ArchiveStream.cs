using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AngelicaArchiveManager.Core.ArchiveEngine
{
    public class ArchiveStream : IDisposable
    {
        protected BufferedStream pck = null;
        protected BufferedStream pkx = null;
        protected BufferedStream pkx1 = null;
        protected BufferedStream pkx2 = null;
        private string path = "";
        public long Position = 0;
        const uint PCK_MAX_SIZE = 2147483392;
        const uint PKX_MAX_SIZE = 4294966784;
        const int BUFFER_SIZE = 16777216; // 33554432

        public ArchiveStream(string path)
        {
            this.path = path;
        }

        public void Reopen(bool ro)
        {
            Close();
            pck = OpenStream(path, ro);
            if (File.Exists(path.Replace(".pck", ".pkx")) && Path.GetExtension(path) != ".cup")
            {
                pkx = OpenStream(path.Replace(".pck", ".pkx"), ro);

                if (File.Exists(path.Replace(".pck", ".pkx1")) && Path.GetExtension(path) != ".cup")
                {
                    pkx1 = OpenStream(path.Replace(".pck", ".pkx1"), ro);
                    if (File.Exists(path.Replace(".pck", ".pkx2")) && Path.GetExtension(path) != ".cup")
                    {
                        pkx2 = OpenStream(path.Replace(".pck", ".pkx2"), ro);
                    }
                }     
            }
        }

        private BufferedStream OpenStream(string path, bool ro = true)
        {
            FileAccess fa = ro ? FileAccess.Read : FileAccess.ReadWrite;
            FileShare fs = ro ? FileShare.Read : FileShare.ReadWrite;
            return new BufferedStream(new FileStream(path, FileMode.OpenOrCreate, fa, fs), BUFFER_SIZE);
        }

        public void Seek(long offset, SeekOrigin origin)
        {
            if (pck == null)
                throw new InvalidOperationException("O arquivo principal (PCK) não está disponível.");
            
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = GetLenght() + offset;
                    break;
            }
            
            if (Position < 0)
                Position = 0;
            
            if (Position <= pck.Length)
            {
                pck.Seek(Position, SeekOrigin.Begin);
            }
            else if (pkx != null && Position <= pck.Length + pkx.Length)
            {
                pkx.Seek(Position - pck.Length, SeekOrigin.Begin);
            }
            else if (pkx1 != null && Position <= pck.Length + (pkx != null ? pkx.Length : 0) + pkx1.Length)
            {
                pkx1.Seek(Position - pck.Length - (pkx != null ? pkx.Length : 0), SeekOrigin.Begin);
            }
            else if (pkx2 != null)
            {
                pkx2.Seek(Position - pck.Length - (pkx != null ? pkx.Length : 0) - (pkx1 != null ? pkx1.Length : 0), SeekOrigin.Begin);
            }
            else
            {
                throw new IOException($"Posição {Position} está além do tamanho do arquivo.");
            }
        }

        public long GetLenght()
        {
            if (pck == null)
                return 0;
            
            long length = pck.Length;
            
            if (pkx != null)
                length += pkx.Length;
            
            if (pkx1 != null)
                length += pkx1.Length;
            
            if (pkx2 != null)
                length += pkx2.Length;
            
            return length;
        }

        public void Cut(long len)
        {
            if (len < PCK_MAX_SIZE)
            {
                pck.SetLength(len);
            }
            else if(len < (long)PCK_MAX_SIZE + PKX_MAX_SIZE)
            {
                pkx.SetLength(PCK_MAX_SIZE - len);
            }
            else if(len < (long)PCK_MAX_SIZE + PKX_MAX_SIZE + PKX_MAX_SIZE)
            {
                pkx1.SetLength((long)PCK_MAX_SIZE + PKX_MAX_SIZE - len);
            }
            else
            {
                pkx2.SetLength((long)PCK_MAX_SIZE + PKX_MAX_SIZE + PKX_MAX_SIZE - len);
            }
        }

        public byte[] ReadBytes(int count)
        {
            if (count <= 0)
                return new byte[0];
            
            if (pck == null)
                throw new InvalidOperationException("O arquivo principal (PCK) não está disponível.");
            
            byte[] array = new byte[count];
            int BytesRead = 0;
            
            try
            {
                if (Position < pck.Length)
                {
                    BytesRead = pck.Read(array, 0, count);
                    if (BytesRead < count && pkx != null)
                    {
                        pkx.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx.Read(array, BytesRead, count - BytesRead);
                    }
                    if (BytesRead < count && pkx1 != null)
                    {
                        pkx1.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx1.Read(array, BytesRead, count - BytesRead);
                    }
                    if (BytesRead < count && pkx2 != null)
                    {
                        pkx2.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx2.Read(array, BytesRead, count - BytesRead);
                    }
                }
                else if (pkx != null && Position < pck.Length + pkx.Length)
                {
                    BytesRead = pkx.Read(array, 0, count);
                    if (BytesRead < count && pkx1 != null)
                    {
                        pkx1.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx1.Read(array, BytesRead, count - BytesRead);
                    }
                    if (BytesRead < count && pkx2 != null)
                    {
                        pkx2.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx2.Read(array, BytesRead, count - BytesRead);
                    }
                }
                else if (pkx1 != null && Position < pck.Length + (pkx != null ? pkx.Length : 0) + pkx1.Length)
                {
                    BytesRead = pkx1.Read(array, 0, count);
                    if (BytesRead < count && pkx2 != null)
                    {
                        pkx2.Seek(0, SeekOrigin.Begin);
                        BytesRead += pkx2.Read(array, BytesRead, count - BytesRead);
                    }
                }
                else if (pkx2 != null)
                {
                    BytesRead = pkx2.Read(array, 0, count);
                }
                else
                {
                    throw new IOException($"Posição {Position} está além do tamanho do arquivo.");
                }
                
                Position += BytesRead;
                
                // Se não conseguimos ler todos os bytes solicitados
                if (BytesRead < count)
                {
                    // Cria um novo array com o tamanho exato dos bytes lidos
                    byte[] result = new byte[BytesRead];
                    Array.Copy(array, result, BytesRead);
                    return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao ler bytes: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
            
            return array;
        }

        public void WriteBytes(byte[] array)
        {
            long totalSize = (long)PCK_MAX_SIZE + PKX_MAX_SIZE;
            long totalSize2 = (long)PCK_MAX_SIZE + PKX_MAX_SIZE + PKX_MAX_SIZE;
            long positionAfterWrite = Position + array.Length;

            if (positionAfterWrite < PCK_MAX_SIZE)
            {
                pck.Write(array, 0, array.Length);
            }
            else if (positionAfterWrite < totalSize)
            {
                if (pkx == null)
                {
                    pkx = OpenStream(path.Replace(".pck", ".pkx"), false);
                }
                if (Position >= PCK_MAX_SIZE)
                {
                    pkx.Write(array, 0, array.Length);
                }
                else
                {
                    int pckWriteLength = (int)(PCK_MAX_SIZE - Position);
                    pck.Write(array, 0, pckWriteLength);
                    pkx.Write(array, pckWriteLength, array.Length - pckWriteLength);
                }
            }
            else if (positionAfterWrite < totalSize2)
            {
                if (pkx1 == null)
                {
                    pkx1 = OpenStream(path.Replace(".pck", ".pkx1"), false);
                }
                if (Position >= totalSize)
                {
                    pkx1.Write(array, 0, array.Length);
                }
                else
                {
                    if (pkx == null)
                    {
                        pkx = OpenStream(path.Replace(".pck", ".pkx"), false);
                    }
                    long pkxPositionStart = PCK_MAX_SIZE;
                    long pkx1PositionStart = totalSize;
                    int pkxBytes = (int)(pkx1PositionStart - Position);

                    if (Position < pkxPositionStart)
                    {
                        int pckWriteLength = (int)(pkxPositionStart - Position);
                        pck.Write(array, 0, pckWriteLength);
                        pkx.Write(array, pckWriteLength, pkxBytes - pckWriteLength);
                        pkx1.Write(array, pkxBytes, array.Length - pkxBytes);
                    }
                    else
                    {
                        pkx.Write(array, 0, pkxBytes);
                        pkx1.Write(array, pkxBytes, array.Length - pkxBytes);
                    }
                }
            }
            else
            {
                if (pkx2 == null)
                {
                    pkx2 = OpenStream(path.Replace(".pck", ".pkx2"), false);
                }
                if (Position >= totalSize2)
                {
                    pkx2.Write(array, 0, array.Length);
                }
                else
                {
                    if (pkx1 == null)
                    {
                        pkx1 = OpenStream(path.Replace(".pck", ".pkx1"), false);
                    }
                    if (pkx == null)
                    {
                        pkx = OpenStream(path.Replace(".pck", ".pkx"), false);
                    }
                    long pkx1PositionStart = totalSize;
                    long pkx2PositionStart = totalSize2;
                    int pkx1Bytes = (int)(pkx2PositionStart - Position);

                    if (Position < pkx1PositionStart)
                    {
                        long pkxPositionStart = PCK_MAX_SIZE;
                        int pkxBytes = (int)(pkx1PositionStart - Position);

                        if (Position < pkxPositionStart)
                        {
                            int pckWriteLength = (int)(pkxPositionStart - Position);
                            pck.Write(array, 0, pckWriteLength);
                            pkx.Write(array, pckWriteLength, pkxBytes - pckWriteLength);
                            pkx1.Write(array, pkxBytes, pkx1Bytes - pkxBytes);
                            pkx2.Write(array, pkx1Bytes, array.Length - pkx1Bytes);
                        }
                        else
                        {
                            pkx.Write(array, 0, pkxBytes);
                            pkx1.Write(array, pkxBytes, pkx1Bytes - pkxBytes);
                            pkx2.Write(array, pkx1Bytes, array.Length - pkx1Bytes);
                        }
                    }
                    else
                    {
                        pkx1.Write(array, 0, pkx1Bytes);
                        pkx2.Write(array, pkx1Bytes, array.Length - pkx1Bytes);
                    }
                }
            }
            Position += array.Length;
        }


        public short ReadInt16() => BitConverter.ToInt16(ReadBytes(2), 0);
        public ushort ReadUInt16() => BitConverter.ToUInt16(ReadBytes(2), 0);
        public int ReadInt32() => BitConverter.ToInt32(ReadBytes(4), 0);
        public uint ReadUInt32() => BitConverter.ToUInt32(ReadBytes(4), 0);
        public long ReadInt64() => BitConverter.ToInt64(ReadBytes(8), 0);
        public ulong ReadUInt64() => BitConverter.ToUInt64(ReadBytes(8), 0);

        public void WriteInt16(short value) => WriteBytes(BitConverter.GetBytes(value));
        public void WriteUInt16(ushort value) => WriteBytes(BitConverter.GetBytes(value));
        public void WriteInt32(int value) => WriteBytes(BitConverter.GetBytes(value));
        public void WriteUInt32(uint value) => WriteBytes(BitConverter.GetBytes(value));
        public void WriteInt64(long value) => WriteBytes(BitConverter.GetBytes(value));
        public void WriteUInt64(ulong value) => WriteBytes(BitConverter.GetBytes(value));

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            try
            {
                pck?.Close();
                pck = null;
            }
            catch { /* Ignora exceções ao fechar */ }
            
            try
            {
                pkx?.Close();
                pkx = null;
            }
            catch { /* Ignora exceções ao fechar */ }
            
            try
            {
                pkx1?.Close();
                pkx1 = null;
            }
            catch { /* Ignora exceções ao fechar */ }
            
            try
            {
                pkx2?.Close();
                pkx2 = null;
            }
            catch { /* Ignora exceções ao fechar */ }
        }
    }
}