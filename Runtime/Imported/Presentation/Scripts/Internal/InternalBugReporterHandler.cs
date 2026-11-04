using System;
namespace Dreamy.Debugger.Presentation.Internal {
    internal sealed class InternalBugReporterHandler : IBugReporterHandler {
        public bool IsUsable => false;
        public void Submit(BugReport report,Action<BugReportSubmitResult> complete,IProgress<float> progress) {
            complete(BugReportSubmitResult.Error("Configure Dreamy report outbox first."));
        }
    }
}
