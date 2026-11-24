using System.Text.RegularExpressions;
namespace Dreamy.Debugger {
    public static class ReportRedactor {
        private static readonly Regex Secrets=new Regex(@"(?i)(\b(?:authorization|password|passwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|bot[_-]?token|user[_-]?id|account[_-]?id)\b""?\s*[=:]\s*)(?:""[^""\r\n]*""|(?:bearer|basic)\s+[^\s,;""}]+|[^\s,;""}]+)",RegexOptions.Compiled);
        private static readonly Regex ProviderSecrets=new Regex(@"(?i)https://(?:discord(?:app)?\.com/api/(?:v\d+/)?webhooks/\d+/[^\s""?#]+|api\.telegram\.org/bot[^/\s""?#]+)",RegexOptions.Compiled);
        private static readonly Regex HomeDirectory=new Regex(@"(?i)(?:[A-Z]:[\\/]Users[\\/]|/(?:home|Users)/)[^\\/\s""<>]+",RegexOptions.Compiled);
        private static readonly Regex Email=new Regex(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b",RegexOptions.Compiled);
        public static string Clean(string value,int max=16000){if(string.IsNullOrEmpty(value))return "";var text=value.Length>max?value.Substring(0,max):value;return HomeDirectory.Replace(Email.Replace(Secrets.Replace(ProviderSecrets.Replace(text,"[REDACTED URL]"),"$1[REDACTED]"),"[EMAIL]"),"[HOME]");}
    }
}
