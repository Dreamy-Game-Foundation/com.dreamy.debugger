using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dreamy.Debugger.Preferences;
using UnityEngine;

namespace Dreamy.Debugger
{
    /// <summary>Main-thread state/codec, worker file storage. Only explicit user intents mark dirty.</summary>
    public sealed class RuntimePreferences : IDisposable
    {
        private readonly IPreferenceStorage storage;
        private readonly Dictionary<string,string> values = new Dictionary<string,string>(StringComparer.Ordinal);
        private readonly HashSet<string> touched = new HashSet<string>(StringComparer.Ordinal);
        private bool disposed, dirty, resetAll;
        private double due;
        private Task write = Task.CompletedTask;
        public Task Ready { get; }
        public bool IsReady { get; private set; }
        public string LastError { get; private set; }
        public event Action<string> Changed;
        public RuntimePreferences(IPreferenceStorage storage)
        {
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            Ready = LoadAsync();
        }
        private async Task LoadAsync()
        {
            try
            {
                var candidates = await storage.ReadAsync();
                if (disposed) return;
                foreach (var json in candidates ?? new string[0])
                {
                    if (string.IsNullOrWhiteSpace(json)) continue;
                    PreferenceSnapshot snapshot;
                    try { snapshot = JsonUtility.FromJson<PreferenceSnapshot>(json); } catch (ArgumentException) { continue; }
                    if (snapshot == null || snapshot.schema != 1) continue;
                    foreach (var pair in snapshot.ValidEntries()) if (!resetAll && !touched.Contains(pair.Key) && (values.Count<160 || values.ContainsKey(pair.Key))) values[pair.Key] = pair.Value;
                    break;
                }
            }
            catch (Exception error) { LastError = error.GetType().Name; }
            if (disposed) return;
            IsReady = true; Changed?.Invoke(null);
        }
        public string Get(string key, string fallback) => values.TryGetValue(key, out var value) ? value : fallback;
        public bool Has(string key) => values.ContainsKey(key);
        public bool GetBool(string key, bool fallback) => Get(key, fallback ? "1" : "0") == "1" ? true : Get(key, "") == "0" ? false : fallback;
        public float GetNumber(string key, float fallback, float minimum, float maximum)
        {
            return float.TryParse(Get(key, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
                   !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum ? value : fallback;
        }
        public T GetEnum<T>(string key, T fallback) where T : struct
        { return Enum.TryParse<T>(Get(key, ""), out var value) && Enum.IsDefined(typeof(T), value) ? value : fallback; }
        public void Set(string key, string value)
        {
            if (disposed || !PreferenceSnapshot.Allowed(key) || value == null || value.Length > 256) return;
            if(!values.ContainsKey(key) && values.Count>=160){
                if(key.StartsWith("pin.") || key.StartsWith("category."))return;
                var discard=values.Keys.FirstOrDefault(k=>k.StartsWith("pin.") || k.StartsWith("category."));
                if(discard==null)return;values.Remove(discard);
            }
            touched.Add(key);
            if (values.TryGetValue(key, out var previous) && previous == value) return;
            values[key] = value; dirty = true; due = Time.realtimeSinceStartupAsDouble + .5;
            Changed?.Invoke(key);
        }
        public void Set(string key, bool value) => Set(key, value ? "1" : "0");
        public void Set(string key, float value) => Set(key, value.ToString("R", CultureInfo.InvariantCulture));
        public void Reset()
        {
            if (disposed) return;
            foreach (var key in values.Keys.ToArray()) touched.Add(key);
            resetAll = true; values.Clear(); dirty = true; due = 0; Changed?.Invoke(null);
        }
        public Dictionary<string,string> Export() => new Dictionary<string,string>(values, StringComparer.Ordinal);
        public void Seed(IEnumerable<KeyValuePair<string,string>> seed)
        { if (seed != null) foreach (var pair in seed) Set(pair.Key, pair.Value); }
        private string Encode()
        {
            var entries = values.Where(pair => PreferenceSnapshot.Allowed(pair.Key)).OrderBy(pair=>pair.Key.StartsWith("pin.") || pair.Key.StartsWith("category.")?1:0).ThenBy(pair=>pair.Key,StringComparer.Ordinal).Take(160).Select(pair => new PreferenceEntry(pair.Key, pair.Value)).ToList();
            while (true)
            {
                var json = JsonUtility.ToJson(new PreferenceSnapshot { entries = entries.ToArray() });
                if (Encoding.UTF8.GetByteCount(json) <= PreferenceSnapshot.MaximumBytes) return json;
                var index = entries.FindLastIndex(entry => entry.key.StartsWith("pin.") || entry.key.StartsWith("category."));
                if (index < 0) throw new InvalidOperationException("Preference payload exceeds its limit.");
                entries.RemoveAt(index);
            }
        }
        public void Tick(double now)
        {
            if (disposed || !IsReady || !dirty || now < due || !write.IsCompleted) return;
            _ = FlushAsync();
        }
        public async Task FlushAsync()
        {
            await Ready;
            if (disposed) return;
            if (!write.IsCompleted) { try { await write; } catch (Exception error) { LastError = error.GetType().Name; } }
            if (disposed || !dirty) return;
            var json = Encode(); dirty = false;
            try { write = storage.WriteAsync(json); await write; LastError = null; }
            catch (Exception error) { LastError = error.GetType().Name; if (!disposed) { dirty = true; due = Time.realtimeSinceStartupAsDouble + 5; } }
        }
        public void Dispose()
        {
            if (disposed) return;
            Changed = null;
            // Snapshot before owner destruction. The writer uses no Unity API or owner reference.
            if (dirty && IsReady) { var task = storage.WriteAsync(Encode()); _ = Observe(task); }
            disposed = true;
        }
        private static async Task Observe(Task task) { try { await task.ConfigureAwait(false); } catch (Exception) { } }
    }
}
