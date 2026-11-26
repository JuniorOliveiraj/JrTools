using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BennerSmartInstaller.Services
{
    /// <summary>
    /// Cache persistente de MD5 por arquivo, invalidado por (tamanho, última escrita UTC).
    /// O hash tem o mesmo formato da plataforma Benner (BitConverter.ToString => "AB-CD-...").
    /// Arquivo: %LOCALAPPDATA%\BennerSmartInstaller\hashcache_&lt;id do WebApp&gt;.tsv
    /// </summary>
    public class LocalHashCache
    {
        private class Entry
        {
            public long Size;
            public long Ticks;
            public string Hash;
        }

        private readonly string _cacheFile;
        private readonly ConcurrentDictionary<string, Entry> _entries =
            new ConcurrentDictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, byte> _seen =
            new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        private int _hits;
        private int _misses;
        private int _dirty;

        public int Hits { get { return _hits; } }
        public int Misses { get { return _misses; } }

        public LocalHashCache(string webAppPath)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BennerSmartInstaller");
            string id = HashText(Path.GetFullPath(webAppPath).TrimEnd('\\').ToLowerInvariant());
            _cacheFile = Path.Combine(dir, "hashcache_" + id + ".tsv");
            Load();
        }

        public int Count { get { return _entries.Count; } }

        public string GetHash(string fullPath)
        {
            var fi = new FileInfo(fullPath);
            long size = fi.Length;
            long ticks = fi.LastWriteTimeUtc.Ticks;
            _seen[fullPath] = 0;

            Entry e;
            if (_entries.TryGetValue(fullPath, out e) && e.Size == size && e.Ticks == ticks)
            {
                Interlocked.Increment(ref _hits);
                return e.Hash;
            }

            string hash = ComputeMd5(fullPath);
            _entries[fullPath] = new Entry { Size = size, Ticks = ticks, Hash = hash };
            Interlocked.Increment(ref _misses);
            Interlocked.Exchange(ref _dirty, 1);
            return hash;
        }

        /// <summary>MD5 em streaming, no mesmo formato usado pela plataforma.</summary>
        public static string ComputeMd5(string path)
        {
            using (var md5 = MD5.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536, FileOptions.SequentialScan))
            {
                return BitConverter.ToString(md5.ComputeHash(fs));
            }
        }

        /// <summary>Grava o cache. Com prune=true remove entradas de arquivos que não foram vistos nesta execução.</summary>
        public void Save(bool prune)
        {
            try
            {
                if (prune)
                {
                    foreach (var key in new List<string>(_entries.Keys))
                    {
                        if (!_seen.ContainsKey(key))
                        {
                            Entry removed;
                            if (_entries.TryRemove(key, out removed)) Interlocked.Exchange(ref _dirty, 1);
                        }
                    }
                }

                if (Interlocked.CompareExchange(ref _dirty, 0, 1) == 0) return;

                Directory.CreateDirectory(Path.GetDirectoryName(_cacheFile));
                string tmp = _cacheFile + ".tmp";
                using (var w = new StreamWriter(tmp, false, new UTF8Encoding(false)))
                {
                    foreach (var kv in _entries)
                    {
                        w.Write(kv.Key); w.Write('\t');
                        w.Write(kv.Value.Size); w.Write('\t');
                        w.Write(kv.Value.Ticks); w.Write('\t');
                        w.WriteLine(kv.Value.Hash);
                    }
                }
                if (File.Exists(_cacheFile)) File.Delete(_cacheFile);
                File.Move(tmp, _cacheFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO CACHE] Não foi possível gravar o cache de hash: {ex.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_cacheFile)) return;
                foreach (string line in File.ReadLines(_cacheFile, Encoding.UTF8))
                {
                    var p = line.Split('\t');
                    long size, ticks;
                    if (p.Length != 4 || !long.TryParse(p[1], out size) || !long.TryParse(p[2], out ticks)) continue;
                    _entries[p[0]] = new Entry { Size = size, Ticks = ticks, Hash = p[3] };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AVISO CACHE] Cache de hash ignorado (inválido): {ex.Message}");
                _entries.Clear();
            }
        }

        private static string HashText(string text)
        {
            using (var md5 = MD5.Create())
            {
                var b = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                return BitConverter.ToString(b, 0, 6).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
