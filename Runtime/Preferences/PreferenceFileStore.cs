using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dreamy.Debugger.Preferences
{
    public interface IPreferenceStorage
    {
        Task<string[]> ReadAsync();
        Task WriteAsync(string json);
    }

    /// <summary>Worker-only bounded I/O. Per-path sequencing prevents old sessions overwriting newer writes.</summary>
    public sealed class PreferenceFileStore : IPreferenceStorage
    {
        private sealed class Writer { public readonly object Gate = new object(); public long Revision; }
        private static readonly ConcurrentDictionary<string,Writer> Writers = new ConcurrentDictionary<string,Writer>();
        private readonly string path;
        private readonly Writer writer;
        public PreferenceFileStore(string path)
        {
            this.path = Path.GetFullPath(path ?? throw new ArgumentNullException(nameof(path)));
            writer = Writers.GetOrAdd(this.path, _ => new Writer());
        }
        public Task<string[]> ReadAsync() => Task.Run(() =>
        {
            lock (writer.Gate) return new[] { ReadBounded(path), ReadBounded(path + ".bak") };
        });
        private static string ReadBounded(string file)
        {
            if (!File.Exists(file)) return null;
            using (var stream = File.OpenRead(file))
            {
                if (stream.Length > PreferenceSnapshot.MaximumBytes) return null;
                var bytes = new byte[PreferenceSnapshot.MaximumBytes + 1];
                var count = 0;
                while (count < bytes.Length) { var read = stream.Read(bytes, count, bytes.Length - count); if (read == 0) break; count += read; }
                return count > PreferenceSnapshot.MaximumBytes ? null : Encoding.UTF8.GetString(bytes, 0, count);
            }
        }
        public Task WriteAsync(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > PreferenceSnapshot.MaximumBytes)
                throw new ArgumentException("Preferences exceed the bounded payload size.", nameof(json));
            var revision = System.Threading.Interlocked.Increment(ref writer.Revision);
            return Task.Run(() =>
            {
                lock (writer.Gate)
                {
                    if (revision != System.Threading.Interlocked.Read(ref writer.Revision)) return;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var temp = path + ".tmp";
                    File.WriteAllText(temp, json, new UTF8Encoding(false));
                    if (!File.Exists(path)) { File.Move(temp, path); return; }
                    // Do not retain an unbounded manually corrupted file as backup.
                    if (new FileInfo(path).Length > PreferenceSnapshot.MaximumBytes)
                    {
                        using (var input = File.OpenRead(path))
                        using (var output = File.Create(path + ".bak"))
                        { var bytes = new byte[PreferenceSnapshot.MaximumBytes]; var count = input.Read(bytes, 0, bytes.Length); output.Write(bytes, 0, count); }
                        File.Delete(path); File.Move(temp, path); return;
                    }
                    try { File.Replace(temp, path, path + ".bak"); }
                    catch (PlatformNotSupportedException)
                    { File.Copy(path, path + ".bak", true); File.Delete(path); File.Move(temp, path); }
                }
            });
        }
    }
}
