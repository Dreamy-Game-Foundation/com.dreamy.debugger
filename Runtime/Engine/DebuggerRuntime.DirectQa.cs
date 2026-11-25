using System;
using System.IO;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Dreamy.Debugger.Reporting.DirectQa;
#endif

namespace Dreamy.Debugger
{
    public sealed partial class DebuggerRuntime
    {
        internal bool DirectProvisioningVisible { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool CanUseDirectQa => Debug.isDebugBuild && CanCheat && runtimeConfig.ReportingMode==ReportingMode.DirectQa &&
            (runtimeConfig.Mode==DebuggerMode.Development || runtimeConfig.Mode==DebuggerMode.Qa);
        public string DirectQaStatus => transport is DirectQaTransport ? (LastUploadResult?.DiagnosticSummary ?? "Direct QA configured for this session.") : "Enter QA credentials. Nothing is saved to disk.";
        public bool ConfigureDirectQa(int provider, string credential, string chat)
        {
            if (!CanUseDirectQa || currentUpload!=null && !currentUpload.IsCompleted || manualReport!=null && !manualReport.IsCompleted) return false;
            try
            {
                var directory=Path.Combine(Application.persistentDataPath,"DreamyDebugger","DirectReceipts");
                DirectQaTransport next=provider==0 ? (DirectQaTransport)new DirectDiscordTransport(credential,directory) :
                    provider==1 ? new DirectTelegramTransport(credential,chat,directory) : throw new ArgumentException();
                SetReportUploadPermission(false);
                (transport as IDisposable)?.Dispose(); transport=next; pump=new OutboxPump(Outbox,next,true);
                // A text acknowledgement is required before the queue is allowed to send attachments.
                Status="Direct QA configured. Test text before sending reports."; return true;
            }
            catch (ArgumentException) { Status="Invalid Direct QA credentials. Check the provider fields."; return false; }
        }
        public void ForgetDirectQa()
        {
            if (!(transport is DirectQaTransport)) return;
            SetReportUploadPermission(false); (transport as IDisposable)?.Dispose(); transport=null; pump=null;
            Status="Direct credentials forgotten. Reports remain on this device.";
        }
        public async Task<string> TestDirectConnectionAsync()
        {
            if (!CanUseDirectQa || !(transport is DirectQaTransport direct)) return "Configure Direct QA first.";
            var result=await direct.TestConnectionAsync(lifetime.Token);
            if (ReferenceEquals(transport,direct) && result.Status==DeliveryStatus.Accepted) SetReportUploadPermission(runtimeConfig.UploadsEnabled);
            return result.DiagnosticSummary;
        }
        public async Task<bool> SendDirectTestReportAsync()
        {
            if (!CanUseDirectQa || !(transport is DirectQaTransport) || !ReportUploadsEnabled) return false;
            return await QueueReportAsync("[QA TEST] Direct report with JSON and optional screenshot",true,true,true);
        }
        public async Task ConfirmDirectRetryAsync(string id)
        {
            if (!CanUseDirectQa || !(transport is DirectQaTransport direct) || !ReportUploadsEnabled) return;
            await direct.AllowUncertainRetryAsync(id,lifetime.Token);
            await pump.RetryNowAsync(id,uploadLifetime.Token);
        }
#endif
    }
}
