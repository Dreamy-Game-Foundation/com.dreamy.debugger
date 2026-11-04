using System.Text.RegularExpressions;
namespace Dreamy.Debugger {
    public static class ReportRedactor {
        private static readonly Regex Secrets=new Regex(@"(?i)(authorization|password|passwd|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|bot[_-]?token)(\s*[=:]\s*)([^\s,;""}]+)",RegexOptions.Compiled);
        private static readonly Regex Email=new Regex(@"\b[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}\b",RegexOptions.Compiled);
        public static string Clean(string value,int max=16000){if(string.IsNullOrEmpty(value))return "";var text=value.Length>max?value.Substring(0,max):value;return Email.Replace(Secrets.Replace(text,"$1$2[REDACTED]"),"[EMAIL]");}
    }
}
