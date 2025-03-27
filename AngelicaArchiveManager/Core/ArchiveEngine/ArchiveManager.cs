using AngelicaArchiveManager.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Windows;
using static AngelicaArchiveManager.Core.Events;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;

namespace AngelicaArchiveManager.Core.ArchiveEngine
{
    public class ArchiveManager : IArchiveManager
    {
        private string Path { get; set; } = "";
        public List<IArchiveEntry> Files { get; set; } = new List<IArchiveEntry>();
        private ArchiveStream Stream { get; set; }
        private ArchiveKey Key { get; set; }
        public ArchiveVersion Version { get; set; }

        public event SetProgress SetProgress;
        public event SetProgressMax SetProgressMax;
        public event SetProgressNext SetProgressNext;
        public event LoadData LoadData;

        public ArchiveManager(string path, ArchiveKey key, bool detect_version = true)
        {
            Path = path;
            Key = key;
            Stream = new ArchiveStream(path);
            if (detect_version)
            {
                Stream.Reopen(true);
                Stream.Seek(-4, SeekOrigin.End);
                short version = Stream.ReadInt16();
                switch (version)
                {
                    case 2:
                        Version = ArchiveVersion.V2;
                        break;
                    case 3:
                        Version = ArchiveVersion.V3;
                        break;
                    default:
                        MessageBox.Show("Unknown archive type");
                        break;
                }
                Stream.Close();
            }
        }

        public ArchiveManager(string path, bool detect_version = true)
        {
            Path = path;
            Stream = new ArchiveStream(path);
            
            if (detect_version)
            {
                try
                {
                    Stream.Reopen(true);
                    
                    // Verificar se o arquivo tem tamanho mínimo necessário
                    long fileLength = Stream.GetLenght();
                    if (fileLength < 10) // Tamanho mínimo necessário para um arquivo PCK válido
                    {
                        MessageBox.Show("O arquivo é muito pequeno para ser um arquivo PCK válido.");
                        return;
                    }
                    
                    Stream.Seek(-4, SeekOrigin.End);
                    
                    short version = Stream.ReadInt16();
                    switch (version)
                    {
                        case 2:
                            Version = ArchiveVersion.V2;
                            break;
                        case 3:
                            Version = ArchiveVersion.V3;
                            break;
                        default:
                            MessageBox.Show("Versão de arquivo desconhecida.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Log detalhado apenas para debug, exibir mensagem simplificada para o usuário
                    System.Diagnostics.Debug.WriteLine($"Erro ao detectar versão: {ex.Message}\n{ex.StackTrace}");
                    MessageBox.Show($"Erro ao detectar versão do arquivo. O arquivo pode estar corrompido ou não ser um arquivo PCK válido.");
                }
                finally
                {
                    try
                    {
                        // Garantir que o stream seja sempre fechado
                        Stream.Close();
                    }
                    catch
                    {
                        // Ignora erros ao fechar o stream
                    }
                }
            }
            
            // Usar a primeira chave por padrão, mas o ReadFileTableAsync tentará todas as chaves
            if (Settings.Keys.Count > 0)
                Key = Settings.Keys[0];
        }

        public void ReadFileTable()
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    ReadFileTableV2();
                    break;
                case ArchiveVersion.V3:
                    ReadFileTableV3();
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }

        public async Task ReadFileTableAsync()
        {
            bool success = false;
            Exception lastException = null;
            List<ArchiveKey> keysToTry = new List<ArchiveKey>(Settings.Keys);
            
            // Se não houver chaves cadastradas, mostra aviso e retorna
            if (keysToTry.Count == 0)
            {
                MessageBox.Show("Não há chaves cadastradas para tentar abrir o arquivo.");
                return;
            }
            
            // Coloca a chave atual no início da lista para tentar primeiro
            if (Key != null && keysToTry.Contains(Key))
            {
                keysToTry.Remove(Key);
                keysToTry.Insert(0, Key);
            }
            
            // Silencia as exceções de todas as tentativas de chaves, exceto a última
            foreach (var key in keysToTry)
            {
                try
                {
                    Key = key;
                    // Limpa a lista de arquivos antes de tentar uma nova chave
                    Files.Clear();
                    
                    // Fecha o stream se já estiver aberto
                    try { Stream.Close(); } catch { }
                    
                    // Log de debug para saber qual chave está sendo tentada
                    System.Diagnostics.Debug.WriteLine($"Tentando abrir com a chave: {key.Name}");
                    
                    switch (Version)
                    {
                        case ArchiveVersion.V2:
                            await ReadFileTableV2AsyncWithKey();
                            success = true;
                            System.Diagnostics.Debug.WriteLine($"Arquivo aberto com sucesso usando a chave: {key.Name}");
                            break;
                        case ArchiveVersion.V3:
                            await ReadFileTableV3AsyncWithKey();
                            success = true;
                            System.Diagnostics.Debug.WriteLine($"Arquivo aberto com sucesso usando a chave: {key.Name}");
                            break;
                        default:
                            // Versão desconhecida, continue para a próxima chave
                            continue;
                    }
                    
                    // Se chegou aqui, significa que foi bem-sucedido
                    if (success)
                        break;
                }
                catch (Exception ex)
                {
                    // Armazena a última exceção para debugging
                    lastException = ex;
                    
                    // Log detalhado apenas para debug, não mostrar ao usuário
                    System.Diagnostics.Debug.WriteLine($"Falha ao abrir com a chave '{key.Name}': {ex.Message}");
                    
                    // Garante que o stream seja fechado após uma exceção
                    try { Stream.Close(); } catch { }
                    
                    // Continua para a próxima chave
                    continue;
                }
            }
            
            // Se nenhuma chave funcionou, mostre o erro
            if (!success)
            {
                MessageBox.Show("Não foi possível abrir o arquivo com nenhuma das chaves cadastradas.");
                
                // Apenas lança exceção em modo de debug para facilitar investigação
                #if DEBUG
                if (lastException != null)
                    System.Diagnostics.Debug.WriteLine($"Última exceção: {lastException.Message}\n{lastException.StackTrace}");
                #endif
            }
        }

        private async Task ReadFileTableV2AsyncWithKey()
        {
            // Certifique-se de que o Stream seja aberto corretamente
            Stream.Reopen(true);
            
            try
            {
                // Validar o arquivo e as posições
                Stream.Seek(-8, SeekOrigin.End);
                int filesCount = Stream.ReadInt32();
                
                // Validação básica do número de arquivos - mais tolerante
                if (filesCount < 0 || filesCount > 10000000) // muito mais tolerante
                    throw new InvalidDataException($"Número de arquivos inválido: {filesCount}");
                    
                SetProgressMax?.Invoke(filesCount);
                
                Stream.Seek(-272, SeekOrigin.End);
                long fileTableOffset = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
                
                // Validação mais tolerante do offset da tabela
                if (fileTableOffset < 0 || fileTableOffset >= Stream.GetLenght())
                    throw new InvalidDataException($"Offset da tabela de arquivos inválido: {fileTableOffset}");
                    
                Stream.Seek(fileTableOffset, SeekOrigin.Begin);
                
                // Calcula o tamanho dos dados da tabela de forma mais tolerante
                long dataSize = Stream.GetLenght() - fileTableOffset - 280;
                // Aceita qualquer tamanho positivo
                if (dataSize <= 0)
                    throw new InvalidDataException($"Tamanho da tabela de arquivos inválido: {dataSize}");
                    
                byte[] tableData = Stream.ReadBytes((int)dataSize);
                
                bool taskSucceeded = false;
                Exception taskException = null;
                
                await Task.Run(() => 
                {
                    try
                    {
                        using (BinaryReader tableStream = new BinaryReader(new MemoryStream(tableData)))
                        {
                            for (int i = 0; i < filesCount; ++i)
                            {
                                // Validação para evitar leituras inválidas
                                if (tableStream.BaseStream.Position >= tableStream.BaseStream.Length - 8)
                                    throw new InvalidDataException("Fim inesperado da tabela de arquivos");
                                    
                                int entrySize = tableStream.ReadInt32() ^ Key.KEY_1;
                                
                                // Validação básica do tamanho da entrada - com limite muito maior
                                if (entrySize <= 0 || entrySize > 1000000) // aumentado para 1MB
                                    throw new InvalidDataException($"Tamanho de entrada inválido: {entrySize}");
                                    
                                tableStream.ReadInt32(); // Skip
                                
                                // Validação para evitar leituras além do fim do stream
                                if (tableStream.BaseStream.Position + entrySize > tableStream.BaseStream.Length)
                                    throw new InvalidDataException("Tamanho de entrada excede os limites da tabela");
                                    
                                byte[] entryData = tableStream.ReadBytes(entrySize);
                                
                                var entry = new ArchiveEntryV2(entryData);
                                lock (Files)
                                {
                                    Files.Add(entry);
                                }
                                SetProgressNext?.Invoke();
                            }
                        }
                        taskSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        // Captura exceções dentro da Task mas não propaga
                        taskException = ex;
                        System.Diagnostics.Debug.WriteLine($"Erro ao processar tabela V2: {ex.Message}\n{ex.StackTrace}");
                    }
                });
                
                // Verificar se a task foi bem-sucedida
                if (!taskSucceeded)
                    throw new Exception("Falha ao processar a tabela de arquivos", taskException);
                    
                SetProgress?.Invoke(0);
                LoadData?.Invoke(0);
            }
            finally
            {
                // Garantir que o Stream seja fechado mesmo em caso de exceção
                Stream.Close();
            }
        }

        private async Task ReadFileTableV3AsyncWithKey()
        {
            // Certifique-se de que o Stream seja aberto corretamente
            Stream.Reopen(true);
            
            try
            {
                // Validar o arquivo e as posições
                Stream.Seek(-8, SeekOrigin.End);
                int filesCount = Stream.ReadInt32();
                
                // Validação básica do número de arquivos - mais tolerante
                if (filesCount < 0 || filesCount > 10000000) // muito mais tolerante
                    throw new InvalidDataException($"Número de arquivos inválido: {filesCount}");
                    
                SetProgressMax?.Invoke(filesCount);
                
                Stream.Seek(-280, SeekOrigin.End);
                long fileTableOffset = Stream.ReadInt64() ^ Key.KEY_1;
                
                // Validação mais tolerante do offset da tabela
                if (fileTableOffset < 0 || fileTableOffset >= Stream.GetLenght())
                    throw new InvalidDataException($"Offset da tabela de arquivos inválido: {fileTableOffset}");
                    
                Stream.Seek(fileTableOffset, SeekOrigin.Begin);
                
                // Calcula o tamanho dos dados da tabela de forma mais tolerante
                long dataSize = Stream.GetLenght() - fileTableOffset - 288;
                // Aceita qualquer tamanho positivo
                if (dataSize <= 0)
                    throw new InvalidDataException($"Tamanho da tabela de arquivos inválido: {dataSize}");
                    
                byte[] tableData = Stream.ReadBytes((int)dataSize);
                
                bool taskSucceeded = false;
                Exception taskException = null;
                
                await Task.Run(() => 
                {
                    try
                    {
                        using (BinaryReader tableStream = new BinaryReader(new MemoryStream(tableData)))
                        {
                            for (int i = 0; i < filesCount; ++i)
                            {
                                // Validação para evitar leituras inválidas
                                if (tableStream.BaseStream.Position >= tableStream.BaseStream.Length - 8)
                                    throw new InvalidDataException("Fim inesperado da tabela de arquivos");
                                    
                                int entrySize = tableStream.ReadInt32() ^ Key.KEY_1;
                                
                                // Validação básica do tamanho da entrada
                                // Aumentado para 1MB para ser extremamente tolerante com entradas grandes
                                if (entrySize <= 0 || entrySize > 1000000)
                                    throw new InvalidDataException($"Tamanho de entrada inválido: {entrySize}");
                                    
                                tableStream.ReadInt32(); // Skip
                                
                                // Validação para evitar leituras além do fim do stream
                                if (tableStream.BaseStream.Position + entrySize > tableStream.BaseStream.Length)
                                    throw new InvalidDataException("Tamanho de entrada excede os limites da tabela");
                                    
                                byte[] entryData = tableStream.ReadBytes(entrySize);
                                
                                var entry = new ArchiveEntryV3(entryData);
                                lock (Files)
                                {
                                    Files.Add(entry);
                                }
                                SetProgressNext?.Invoke();
                            }
                        }
                        taskSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        // Captura exceções dentro da Task e armazena para análise, mas não propaga
                        taskException = ex;
                        System.Diagnostics.Debug.WriteLine($"Erro ao processar tabela V3: {ex.Message}\n{ex.StackTrace}");
                    }
                });
                
                // Verificar se a task foi bem-sucedida
                if (!taskSucceeded)
                    throw new Exception("Falha ao processar a tabela de arquivos", taskException);
                    
                SetProgress?.Invoke(0);
                LoadData?.Invoke(0);
            }
            finally
            {
                // Garantir que o Stream seja fechado mesmo em caso de exceção
                Stream.Close();
            }
        }

        public void SaveFileTable(long filetable = -1)
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    SaveFileTableV2(filetable);
                    break;
                case ArchiveVersion.V3:
                    SaveFileTableV3(filetable);
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }

        public async Task SaveFileTableAsync(long filetable = -1)
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    await SaveFileTableV2Async(filetable);
                    break;
                case ArchiveVersion.V3:
                    await SaveFileTableV3Async(filetable);
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }

        public void AddFiles(List<string> files, string srcdir, string dstdir)
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    AddFilesV2(files, srcdir, dstdir);
                    break;
                case ArchiveVersion.V3:
                    AddFilesV3(files, srcdir, dstdir);
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }

        public void Defrag()
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    DefragV2();
                    break;
                case ArchiveVersion.V3:
                    DefragV3();
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }

        #region V2
        public void ReadFileTableV2()
        {
            Stream.Reopen(true);
            Stream.Seek(-8, SeekOrigin.End);
            int FilesCount = Stream.ReadInt32();
            SetProgressMax?.Invoke(FilesCount);
            Stream.Seek(-272, SeekOrigin.End);
            long FileTableOffset = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
            Stream.Seek(FileTableOffset, SeekOrigin.Begin);
            BinaryReader TableStream = new BinaryReader(new MemoryStream(Stream.ReadBytes((int)(Stream.GetLenght() - FileTableOffset - 280))));
            for (int i = 0; i < FilesCount; ++i)
            {
                SetProgressNext?.Invoke();
                int EntrySize = TableStream.ReadInt32() ^ Key.KEY_1;
                TableStream.ReadInt32();
                Files.Add(new ArchiveEntryV2(TableStream.ReadBytes(EntrySize)));
            }
            SetProgress?.Invoke(0);
            Stream.Close();
            LoadData?.Invoke(0);
        }

        public async Task ReadFileTableV2Async()
        {
            Stream.Reopen(true);
            Stream.Seek(-8, SeekOrigin.End);
            int FilesCount = Stream.ReadInt32();
            SetProgressMax?.Invoke(FilesCount);
            Stream.Seek(-272, SeekOrigin.End);
            long FileTableOffset = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
            Stream.Seek(FileTableOffset, SeekOrigin.Begin);
            byte[] tableData = Stream.ReadBytes((int)(Stream.GetLenght() - FileTableOffset - 280));
            
            await Task.Run(() => {
                BinaryReader TableStream = new BinaryReader(new MemoryStream(tableData));
                for (int i = 0; i < FilesCount; ++i)
                {
                    int EntrySize = TableStream.ReadInt32() ^ Key.KEY_1;
                    TableStream.ReadInt32();
                    byte[] entryData = TableStream.ReadBytes(EntrySize);
                    
                    var entry = new ArchiveEntryV2(entryData);
                    lock (Files)
                    {
                        Files.Add(entry);
                    }
                    SetProgressNext?.Invoke();
                }
            });
            
            SetProgress?.Invoke(0);
            Stream.Close();
            LoadData?.Invoke(0);
        }

        public void SaveFileTableV2(long filetable = -1)
        {
            try
            {
                Stream.Reopen(false);
                long FileTableOffset = filetable;
                if (FileTableOffset == -1)
                {
                    Stream.Seek(-272, SeekOrigin.End);
                    FileTableOffset = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
                    Stream.Cut(FileTableOffset);
                }
                Stream.Seek(FileTableOffset, SeekOrigin.Begin);
                SetProgressMax?.Invoke(Files.Count);
                int cl = Settings.CompressionLevel;
                foreach (IArchiveEntry entry in Files)
                {
                    SetProgressNext?.Invoke();
                    byte[] data = entry.Write(cl);
                    Stream.WriteInt32(data.Length ^ Key.KEY_1);
                    Stream.WriteInt32(data.Length ^ Key.KEY_2);
                    Stream.WriteBytes(data);
                }
                Stream.WriteInt32(Key.ASIG_1);
                Stream.WriteInt16(2);
                Stream.WriteInt16(2);
                Stream.WriteUInt32((uint)(FileTableOffset ^ Key.KEY_1));
                Stream.WriteInt32(0);
                Stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
                Stream.WriteBytes(new byte[215]);
                Stream.WriteInt32(Key.ASIG_2);
                Stream.WriteInt32(Files.Count);
                Stream.WriteInt16(2);
                Stream.WriteInt16(2);
                Stream.Seek(4, SeekOrigin.Begin);
                Stream.WriteUInt32((uint)Stream.GetLenght());
                SetProgress?.Invoke(0);
                Stream.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public async Task SaveFileTableV2Async(long filetable = -1)
        {
            try
            {
                Stream.Reopen(false);
                long FileTableOffset = filetable;
                if (FileTableOffset == -1)
                {
                    Stream.Seek(-272, SeekOrigin.End);
                    FileTableOffset = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
                    Stream.Cut(FileTableOffset);
                }
                Stream.Seek(FileTableOffset, SeekOrigin.Begin);
                SetProgressMax?.Invoke(Files.Count);
                int cl = Settings.CompressionLevel;
                foreach (IArchiveEntry entry in Files)
                {
                    SetProgressNext?.Invoke();
                    byte[] data = entry.Write(cl);
                    Stream.WriteInt32(data.Length ^ Key.KEY_1);
                    Stream.WriteInt32(data.Length ^ Key.KEY_2);
                    Stream.WriteBytes(data);
                }
                Stream.WriteInt32(Key.ASIG_1);
                Stream.WriteInt16(2);
                Stream.WriteInt16(2);
                Stream.WriteUInt32((uint)(FileTableOffset ^ Key.KEY_1));
                Stream.WriteInt32(0);
                Stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
                Stream.WriteBytes(new byte[215]);
                Stream.WriteInt32(Key.ASIG_2);
                Stream.WriteInt32(Files.Count);
                Stream.WriteInt16(2);
                Stream.WriteInt16(2);
                Stream.Seek(4, SeekOrigin.Begin);
                Stream.WriteUInt32((uint)Stream.GetLenght());
                SetProgress?.Invoke(0);
                Stream.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public void AddFilesV2(List<string> files, string srcdir, string dstdir)
        {
            Stream.Reopen(false);
            SetProgressMax?.Invoke(files.Count);
            int cl = Settings.CompressionLevel;
            Stream.Seek(-272, SeekOrigin.End);
            long current_end = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
            foreach (string file in files)
            {
                SetProgressNext?.Invoke();
                byte[] data = File.ReadAllBytes(file);
                int size = data.Length;
                byte[] compressed = Zlib.Compress(data, cl);
                if (compressed.Length < size)
                    data = compressed;
                string path = (dstdir + file.RemoveFirst(srcdir).RemoveFirstSeparator()).RemoveFirstSeparator();
                var entry = Files.Where(x => x.Path == path).ToList();
                if (entry.Count > 0)
                {
                    if (data.Length <= entry[0].CSize)
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        Stream.Seek(entry[0].Offset, SeekOrigin.Begin);
                        Stream.WriteBytes(data);
                    }
                    else
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        entry[0].Offset = current_end;
                        Stream.Seek(current_end, SeekOrigin.Begin);
                        current_end += data.Length;
                        Stream.WriteBytes(data);
                    }
                }
                else
                {
                    Files.Add(new ArchiveEntryV2()
                    {
                        Path = path,
                        Size = size,
                        CSize = data.Length,
                        Offset = current_end
                    });
                    Stream.Seek(current_end, SeekOrigin.Begin);
                    current_end += data.Length;
                    Stream.WriteBytes(data);
                }
            }
            SaveFileTable(current_end);
            SetProgress?.Invoke(0);
            LoadData?.Invoke(0);
            LoadData?.Invoke(1);
        }

        public async Task AddFilesV2Async(List<string> files, string srcdir, string dstdir)
        {
            Stream.Reopen(false);
            SetProgressMax?.Invoke(files.Count);
            int cl = Settings.CompressionLevel;
            Stream.Seek(-272, SeekOrigin.End);
            long current_end = (long)((ulong)((uint)(Stream.ReadUInt32() ^ (ulong)Key.KEY_1)));
            
            foreach (string file in files)
            {
                byte[] data = await FileExtensions.ReadAllBytesAsync(file);
                int size = data.Length;
                byte[] compressed = await Zlib.CompressAsync(data, cl);
                if (compressed.Length < size)
                    data = compressed;
                string path = (dstdir + file.RemoveFirst(srcdir).RemoveFirstSeparator()).RemoveFirstSeparator();
                var entry = Files.Where(x => x.Path == path).ToList();
                if (entry.Count > 0)
                {
                    if (data.Length <= entry[0].CSize)
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        Stream.Seek(entry[0].Offset, SeekOrigin.Begin);
                        Stream.WriteBytes(data);
                    }
                    else
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        entry[0].Offset = current_end;
                        Stream.Seek(current_end, SeekOrigin.Begin);
                        current_end += data.Length;
                        Stream.WriteBytes(data);
                    }
                }
                else
                {
                    Files.Add(new ArchiveEntryV2()
                    {
                        Path = path,
                        Size = size,
                        CSize = data.Length,
                        Offset = current_end
                    });
                    Stream.Seek(current_end, SeekOrigin.Begin);
                    current_end += data.Length;
                    Stream.WriteBytes(data);
                }
                SetProgressNext?.Invoke();
            }
            
            await SaveFileTableV2Async(current_end);
            SetProgress?.Invoke(0);
            LoadData?.Invoke(0);
            LoadData?.Invoke(1);
        }

        public void DefragV2()
        {
            Stream.Reopen(true);
            long oldsize = Stream.GetLenght();
            ArchiveManager am = new ArchiveManager(Path + ".defrag", Key, false);
            am.Stream.Reopen(false);
            am.Stream.WriteInt32(Key.FSIG_1);
            am.Stream.WriteInt32(0);
            am.Stream.WriteInt32(Key.FSIG_2);
            int cl = Settings.CompressionLevel;
            SetProgressMax?.Invoke(Files.Count);
            foreach (IArchiveEntry file in Files)
            {
                SetProgressNext?.Invoke();
                byte[] data = GetFile(file, false);
                byte[] compressed = Zlib.Compress(data, cl);
                if (compressed.Length >= data.Length)
                    compressed = data;
                file.Offset = am.Stream.Position;
                file.Size = data.Length;
                file.CSize = compressed.Length;
                am.Stream.WriteBytes(compressed);
            }
            am.Files = Files;
            am.SaveFileTable(am.Stream.Position);
            am.Stream.Close();
            File.Delete(Path);
            File.Move(Path + ".defrag", Path);
            long newsize = Stream.GetLenght();
            MessageBox.Show($"Defragment Completed\nOld size: {oldsize}\nNew size: {newsize}");
            Stream.Close();
            ReadFileTable();
        }

        public async Task DefragV2Async()
        {
            Stream.Reopen(true);
            long oldsize = Stream.GetLenght();
            ArchiveManager am = new ArchiveManager(Path + ".defrag", Key, false);
            am.Stream.Reopen(false);
            am.Stream.WriteInt32(Key.FSIG_1);
            am.Stream.WriteInt32(0);
            am.Stream.WriteInt32(Key.FSIG_2);
            int cl = Settings.CompressionLevel;
            SetProgressMax?.Invoke(Files.Count);
            
            foreach (IArchiveEntry file in Files)
            {
                byte[] data = await GetFileAsync(file, false);
                byte[] compressed = await Zlib.CompressAsync(data, cl);
                if (compressed.Length >= data.Length)
                    compressed = data;
                file.Offset = am.Stream.Position;
                file.Size = data.Length;
                file.CSize = compressed.Length;
                am.Stream.WriteBytes(compressed);
                SetProgressNext?.Invoke();
            }
            
            am.Files = Files;
            await am.SaveFileTableV2Async(am.Stream.Position);
            am.Stream.Close();
            Stream.Close();
            File.Delete(Path);
            File.Move(Path + ".defrag", Path);
            long newsize = Stream.GetLenght();
            MessageBox.Show($"Defragment Completed\nOld size: {oldsize}\nNew size: {newsize}");
            Stream.Close();
            await ReadFileTableAsync();
        }
        #endregion

        #region V3
        public void ReadFileTableV3()
        {
            Stream.Reopen(true);
            Stream.Seek(-8, SeekOrigin.End);
            int FilesCount = Stream.ReadInt32();
            SetProgressMax?.Invoke(FilesCount);
            Stream.Seek(-280, SeekOrigin.End);
            long FileTableOffset = Stream.ReadInt64() ^ Key.KEY_1;
            Stream.Seek(FileTableOffset, SeekOrigin.Begin);
            BinaryReader TableStream = new BinaryReader(new MemoryStream(Stream.ReadBytes((int)(Stream.GetLenght() - FileTableOffset - 288))));
            for (int i = 0; i < FilesCount; ++i)
            {
                SetProgressNext?.Invoke();
                int EntrySize = TableStream.ReadInt32() ^ Key.KEY_1;
                TableStream.ReadInt32();
                Files.Add(new ArchiveEntryV3(TableStream.ReadBytes(EntrySize)));
            }
            SetProgress?.Invoke(0);
            Stream.Close();
            LoadData?.Invoke(0);
        }

        public async Task ReadFileTableV3Async()
        {
            Stream.Reopen(true);
            Stream.Seek(-8, SeekOrigin.End);
            int FilesCount = Stream.ReadInt32();
            SetProgressMax?.Invoke(FilesCount);
            Stream.Seek(-280, SeekOrigin.End);
            long FileTableOffset = Stream.ReadInt64() ^ Key.KEY_1;
            Stream.Seek(FileTableOffset, SeekOrigin.Begin);
            byte[] tableData = Stream.ReadBytes((int)(Stream.GetLenght() - FileTableOffset - 288));
            
            await Task.Run(() => {
                BinaryReader TableStream = new BinaryReader(new MemoryStream(tableData));
                for (int i = 0; i < FilesCount; ++i)
                {
                    int EntrySize = TableStream.ReadInt32() ^ Key.KEY_1;
                    TableStream.ReadInt32();
                    byte[] entryData = TableStream.ReadBytes(EntrySize);
                    
                    var entry = new ArchiveEntryV3(entryData);
                    lock (Files)
                    {
                        Files.Add(entry);
                    }
                    SetProgressNext?.Invoke();
                }
            });
            
            SetProgress?.Invoke(0);
            Stream.Close();
            LoadData?.Invoke(0);
        }

        public void SaveFileTableV3(long filetable = -1)
        {
            try
            {
                Stream.Reopen(false);
                long FileTableOffset = filetable;
                if (FileTableOffset == -1)
                {
                    Stream.Seek(-280, SeekOrigin.End);
                    FileTableOffset = Stream.ReadInt64() ^ Key.KEY_1;
                    Stream.Cut(FileTableOffset);
                }
                Stream.Seek(FileTableOffset, SeekOrigin.Begin);
                SetProgressMax?.Invoke(Files.Count);
                int cl = Settings.CompressionLevel;
                foreach (IArchiveEntry entry in Files)
                {
                    SetProgressNext?.Invoke();
                    byte[] data = entry.Write(cl);
                    Stream.WriteInt32(data.Length ^ Key.KEY_1);
                    Stream.WriteInt32(data.Length ^ Key.KEY_2);
                    Stream.WriteBytes(data);
                }
                Stream.WriteInt32(Key.ASIG_1);
                Stream.WriteInt16(3);
                Stream.WriteInt16(2);
                Stream.WriteInt64(FileTableOffset ^ Key.KEY_1);
                Stream.WriteInt32(0);
                Stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
                Stream.WriteBytes(new byte[215]);
                Stream.WriteInt32(Key.ASIG_2);
                Stream.WriteInt32(0);
                Stream.WriteInt32(Files.Count);
                Stream.WriteInt16(3);
                Stream.WriteInt16(2);
                Stream.Seek(4, SeekOrigin.Begin);
                Stream.WriteInt64(Stream.GetLenght());
                Stream.Close();
                SetProgress?.Invoke(0);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public async Task SaveFileTableV3Async(long filetable = -1)
        {
            try
            {
                Stream.Reopen(false);
                long FileTableOffset = filetable;
                if (FileTableOffset == -1)
                {
                    Stream.Seek(-280, SeekOrigin.End);
                    FileTableOffset = Stream.ReadInt64() ^ Key.KEY_1;
                    Stream.Cut(FileTableOffset);
                }
                Stream.Seek(FileTableOffset, SeekOrigin.Begin);
                SetProgressMax?.Invoke(Files.Count);
                int cl = Settings.CompressionLevel;
                foreach (IArchiveEntry entry in Files)
                {
                    SetProgressNext?.Invoke();
                    byte[] data = entry.Write(cl);
                    Stream.WriteInt32(data.Length ^ Key.KEY_1);
                    Stream.WriteInt32(data.Length ^ Key.KEY_2);
                    Stream.WriteBytes(data);
                }
                Stream.WriteInt32(Key.ASIG_1);
                Stream.WriteInt16(3);
                Stream.WriteInt16(2);
                Stream.WriteInt64(FileTableOffset ^ Key.KEY_1);
                Stream.WriteInt32(0);
                Stream.WriteBytes(Encoding.Default.GetBytes("Angelica File Package, Perfect World."));
                Stream.WriteBytes(new byte[215]);
                Stream.WriteInt32(Key.ASIG_2);
                Stream.WriteInt32(0);
                Stream.WriteInt32(Files.Count);
                Stream.WriteInt16(3);
                Stream.WriteInt16(2);
                Stream.Seek(4, SeekOrigin.Begin);
                Stream.WriteInt64(Stream.GetLenght());
                Stream.Close();
                SetProgress?.Invoke(0);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public void AddFilesV3(List<string> files, string srcdir, string dstdir)
        {
            Stream.Reopen(false);
            SetProgressMax?.Invoke(files.Count);
            int cl = Settings.CompressionLevel;
            Stream.Seek(-280, SeekOrigin.End);
            long current_end = Stream.ReadInt64() ^ Key.KEY_1;
            foreach (string file in files)
            {
                SetProgressNext?.Invoke();
                byte[] data = File.ReadAllBytes(file);
                int size = data.Length;
                byte[] compressed = Zlib.Compress(data, cl);
                if (compressed.Length < size)
                    data = compressed;
                string path = (dstdir + file.RemoveFirst(srcdir).RemoveFirstSeparator()).RemoveFirstSeparator();
                var entry = Files.Where(x => x.Path == path).ToList();
                if (entry.Count > 0)
                {
                    if (data.Length <= entry[0].CSize)
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        Stream.Seek(entry[0].Offset, SeekOrigin.Begin);
                        Stream.WriteBytes(data);
                    }
                    else
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        entry[0].Offset = current_end;
                        Stream.Seek(current_end, SeekOrigin.Begin);
                        current_end += data.Length;
                        Stream.WriteBytes(data);
                    }
                }
                else
                {
                    Files.Add(new ArchiveEntryV3()
                    {
                        Path = path,
                        Size = size,
                        CSize = data.Length,
                        Offset = current_end
                    });
                    Stream.Seek(current_end, SeekOrigin.Begin);
                    current_end += data.Length;
                    Stream.WriteBytes(data);
                }
            }
            SaveFileTable(current_end);
            SetProgress?.Invoke(0);
            LoadData?.Invoke(0);
            LoadData?.Invoke(1);
        }

        public async Task AddFilesV3Async(List<string> files, string srcdir, string dstdir)
        {
            Stream.Reopen(false);
            SetProgressMax?.Invoke(files.Count);
            int cl = Settings.CompressionLevel;
            Stream.Seek(-280, SeekOrigin.End);
            long current_end = Stream.ReadInt64() ^ Key.KEY_1;
            
            foreach (string file in files)
            {
                byte[] data = await FileExtensions.ReadAllBytesAsync(file);
                int size = data.Length;
                byte[] compressed = await Zlib.CompressAsync(data, cl);
                if (compressed.Length < size)
                    data = compressed;
                string path = (dstdir + file.RemoveFirst(srcdir).RemoveFirstSeparator()).RemoveFirstSeparator();
                var entry = Files.Where(x => x.Path == path).ToList();
                if (entry.Count > 0)
                {
                    if (data.Length <= entry[0].CSize)
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        Stream.Seek(entry[0].Offset, SeekOrigin.Begin);
                        Stream.WriteBytes(data);
                    }
                    else
                    {
                        entry[0].Size = size;
                        entry[0].CSize = data.Length;
                        entry[0].Offset = current_end;
                        Stream.Seek(current_end, SeekOrigin.Begin);
                        current_end += data.Length;
                        Stream.WriteBytes(data);
                    }
                }
                else
                {
                    Files.Add(new ArchiveEntryV3()
                    {
                        Path = path,
                        Size = size,
                        CSize = data.Length,
                        Offset = current_end
                    });
                    Stream.Seek(current_end, SeekOrigin.Begin);
                    current_end += data.Length;
                    Stream.WriteBytes(data);
                }
                SetProgressNext?.Invoke();
            }
            
            await SaveFileTableV3Async(current_end);
            SetProgress?.Invoke(0);
            LoadData?.Invoke(0);
            LoadData?.Invoke(1);
        }

        public void DefragV3()
        {
            Stream.Reopen(true);
            long oldsize = Stream.GetLenght();
            ArchiveManager am = new ArchiveManager(Path + ".defrag", Key, false)
            {
                Version = Version
            };
            am.Stream.Reopen(false);
            am.Stream.WriteInt32(Key.FSIG_1);
            am.Stream.WriteInt64(0);
            am.Stream.WriteInt32(Key.FSIG_2);
            int cl = Settings.CompressionLevel;
            SetProgressMax?.Invoke(Files.Count);
            foreach (IArchiveEntry file in Files)
            {
                SetProgressNext?.Invoke();
                byte[] data = GetFile(file, false);
                byte[] compressed = Zlib.Compress(data, cl);
                if (data.Length < compressed.Length)
                    compressed = data;
                file.Offset = am.Stream.Position;
                file.Size = data.Length;
                file.CSize = compressed.Length;
                am.Stream.WriteBytes(compressed);
            }
            am.Files = Files;
            am.SaveFileTable(am.Stream.Position);
            am.Stream.Close();
            Stream.Close();
            File.Delete(Path);
            File.Move(Path + ".defrag", Path);
            string pkx = Path.Replace(".pck", ".pkx");
            if (File.Exists(pkx))
            {
                File.Delete(pkx);
                File.Move(pkx + ".defrag", pkx);
            }
            string pkx1 = Path.Replace(".pck", ".pkx1");
            if (File.Exists(pkx1))
            {
                File.Delete(pkx1);
                File.Move(pkx1 + ".defrag", pkx1);
            }
            string pkx2 = Path.Replace(".pck", ".pkx2");
            if (File.Exists(pkx2))
            {
                File.Delete(pkx2);
                File.Move(pkx2 + ".defrag", pkx2);
            }
            ReadFileTable();
            Stream.Reopen(true);
            long newsize = Stream.GetLenght();
            MessageBox.Show($"Defragment Completed\nOld size: {oldsize}\nNew size: {newsize}");
        }

        public async Task DefragV3Async()
        {
            Stream.Reopen(true);
            long oldsize = Stream.GetLenght();
            ArchiveManager am = new ArchiveManager(Path + ".defrag", Key, false)
            {
                Version = Version
            };
            am.Stream.Reopen(false);
            am.Stream.WriteInt32(Key.FSIG_1);
            am.Stream.WriteInt64(0);
            am.Stream.WriteInt32(Key.FSIG_2);
            int cl = Settings.CompressionLevel;
            SetProgressMax?.Invoke(Files.Count);
            
            foreach (IArchiveEntry file in Files)
            {
                byte[] data = await GetFileAsync(file, false);
                byte[] compressed = await Zlib.CompressAsync(data, cl);
                if (data.Length < compressed.Length)
                    compressed = data;
                file.Offset = am.Stream.Position;
                file.Size = data.Length;
                file.CSize = compressed.Length;
                am.Stream.WriteBytes(compressed);
                SetProgressNext?.Invoke();
            }
            
            am.Files = Files;
            await am.SaveFileTableV3Async(am.Stream.Position);
            am.Stream.Close();
            Stream.Close();
            File.Delete(Path);
            File.Move(Path + ".defrag", Path);
            string pkx = Path.Replace(".pck", ".pkx");
            if (File.Exists(pkx))
            {
                File.Delete(pkx);
                File.Move(pkx + ".defrag", pkx);
            }
            string pkx1 = Path.Replace(".pck", ".pkx1");
            if (File.Exists(pkx1))
            {
                File.Delete(pkx1);
                File.Move(pkx1 + ".defrag", pkx1);
            }
            string pkx2 = Path.Replace(".pck", ".pkx2");
            if (File.Exists(pkx2))
            {
                File.Delete(pkx2);
                File.Move(pkx2 + ".defrag", pkx2);
            }
            await ReadFileTableAsync();
            Stream.Reopen(true);
            long newsize = Stream.GetLenght();
            MessageBox.Show($"Defragment Completed\nOld size: {oldsize}\nNew size: {newsize}");
        }
        #endregion

        public void UnpackFiles(string srcdir, List<IArchiveEntry> files, string dstdir)
        {
            try
            {
                Stream.Reopen(true);
                SetProgressMax?.Invoke(files.Count);
                foreach (IArchiveEntry entry in files)
                {
                    SetProgressNext?.Invoke();
                    byte[] file = GetFile(entry, false);
                    string path = System.IO.Path.Combine(dstdir,
                        srcdir.Length > 2 ? entry.Path.RemoveFirst(srcdir.RemoveFirstSeparator()) : entry.Path);
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    File.WriteAllBytes(path, file);
                }
                SetProgress?.Invoke(0);
                Stream.Close();
                MessageBox.Show("Extraction Completed");
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public byte[] GetFile(IArchiveEntry entry, bool reload = true)
        {
            if (reload)
                Stream.Reopen(true);
            Stream.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] file = Stream.ReadBytes(entry.CSize);
            if (entry.CSize < entry.Size)
                return Zlib.Decompress(file, entry.Size);
            else
                return file;
        }

        public List<byte[]> GetFiles(List<IArchiveEntry> files)
        {
            Stream.Reopen(true);
            SetProgressMax?.Invoke(files.Count);
            List<byte[]> fs = new List<byte[]>();
            foreach (IArchiveEntry entry in files)
            {
                SetProgressNext?.Invoke();
                fs.Add(GetFile(entry, false));
            }
            SetProgress?.Invoke(0);
            Stream.Close();
            return fs;
        }

        // Async implementations
        public async Task<byte[]> GetFileAsync(IArchiveEntry entry, bool reload = true)
        {
            if (reload)
                Stream.Reopen(true);
            Stream.Seek(entry.Offset, SeekOrigin.Begin);
            byte[] file = Stream.ReadBytes(entry.CSize);
            if (entry.CSize < entry.Size)
                return await Task.Run(() => Zlib.Decompress(file, entry.Size));
            else
                return file;
        }

        public async Task<List<byte[]>> GetFilesAsync(List<IArchiveEntry> files)
        {
            Stream.Reopen(true);
            SetProgressMax?.Invoke(files.Count);
            List<byte[]> fs = new List<byte[]>();
            List<Task<byte[]>> tasks = new List<Task<byte[]>>();
            
            foreach (IArchiveEntry entry in files)
            {
                tasks.Add(GetFileAsync(entry, false));
            }
            
            for (int i = 0; i < tasks.Count; i++)
            {
                fs.Add(await tasks[i]);
                SetProgressNext?.Invoke();
            }
            
            SetProgress?.Invoke(0);
            Stream.Close();
            return fs;
        }

        public async Task UnpackFilesAsync(string srcdir, List<IArchiveEntry> files, string dstdir)
        {
            try
            {
                Stream.Reopen(true);
                SetProgressMax?.Invoke(files.Count);
                List<Task> writeTasks = new List<Task>();
                
                foreach (IArchiveEntry entry in files)
                {
                    byte[] file = await GetFileAsync(entry, false);
                    string path = System.IO.Path.Combine(dstdir,
                        srcdir.Length > 2 ? entry.Path.RemoveFirst(srcdir.RemoveFirstSeparator()) : entry.Path);
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    
                    writeTasks.Add(Task.Run(() => {
                        File.WriteAllBytes(path, file);
                        SetProgressNext?.Invoke();
                    }));
                }
                
                await Task.WhenAll(writeTasks);
                SetProgress?.Invoke(0);
                Stream.Close();
                MessageBox.Show("Extraction Completed");
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
            }
        }

        public async Task AddFilesAsync(List<string> files, string srcdir, string dstdir)
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    await AddFilesV2Async(files, srcdir, dstdir);
                    break;
                case ArchiveVersion.V3:
                    await AddFilesV3Async(files, srcdir, dstdir);
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }
        
        public async Task DefragAsync()
        {
            switch (Version)
            {
                case ArchiveVersion.V2:
                    await DefragV2Async();
                    break;
                case ArchiveVersion.V3:
                    await DefragV3Async();
                    break;
                default:
                    MessageBox.Show("Unknown archive type");
                    break;
            }
        }
    }
}