using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger {
    internal static class SessionJournal {
        public static Task<bool> BeginAsync(string path,CancellationToken token)=>Task.Run(()=>{token.ThrowIfCancellationRequested();var folder=Path.Combine(path,"DreamyDebugger");Directory.CreateDirectory(folder);var file=Path.Combine(folder,"session.running");var previous=File.Exists(file);File.WriteAllText(file,DateTime.UtcNow.ToString("O"));return previous;},token);
        public static async Task EndAsync(string path){try{await Task.Run(()=>{var file=Path.Combine(path,"DreamyDebugger/session.running");if(File.Exists(file))File.Delete(file);});}catch(IOException){}catch(UnauthorizedAccessException){}}
    }
}
