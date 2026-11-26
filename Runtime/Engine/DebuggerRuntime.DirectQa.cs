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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Serializable]
    public sealed class DirectQaBuildPreset
    {
        public const string ResourcePath = "DreamyDebugger/DirectQaPreset";
        public int provider;
        public string credential;
        public string chatId;

        public bool IsValid => provider == 0 ? DirectDiscordTransport.IsValidWebhook(credential) :
            provider == 1 && DirectTelegramTransport.IsValidDestination(credential, chatId);
    }

    [Serializable]
    public sealed class DirectQaBuildProfiles
    {
        public DirectQaBuildPreset discord, telegram;
        public int defaultProvider;
        public DirectQaBuildPreset Get(int provider) => provider == 0 ? discord : provider == 1 ? telegram : null;
        public bool Has(int provider)
        {
            var preset=Get(provider);
            return preset?.provider==provider && preset.IsValid;
        }
    }
#endif

    public sealed partial class DebuggerRuntime
    {
        private bool PreserveDirectQaReportKeyboard()
        {
#if UNITY_ANDROID && DEVELOPMENT_BUILD
            if (!CanUseDirectQa) return false;
            var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var field=selected==null?null:selected.GetComponent<UnityEngine.UI.InputField>();
            return field!=null && field.isFocused && field.touchScreenKeyboard!=null &&
                field.GetComponentInParent<Presentation.UI.Tabs.BugReportTabController>()!=null;
#else
            return false;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int directQaProvider;
        private bool usingDirectQaPreset;
        private DirectQaBuildProfiles directQaProfiles;
        private bool DirectQaTransportBusy => currentUpload!=null && !currentUpload.IsCompleted ||
            manualReport!=null && !manualReport.IsCompleted || pump?.SendingReportId!=null;
        public bool CanUseDirectQa => Debug.isDebugBuild && CanCheat && runtimeConfig.ReportingMode==ReportingMode.DirectQa &&
            (runtimeConfig.Mode==DebuggerMode.Development || runtimeConfig.Mode==DebuggerMode.Qa);
        public string DirectQaStatus => transport is DirectQaTransport
            ? LastUploadResult?.DiagnosticSummary ?? (usingDirectQaPreset ? "Direct QA ready with the APK destination." : "Direct QA configured for this session.")
            : "Set up the destinations in Unity Editor, then rebuild the Android QA APK.";

        private bool IsDirectQaDestinationAvailable(int provider) => directQaProfiles?.Has(provider)==true;

        private bool SelectDirectQaDestination(int provider)
        {
            if(!CanUseDirectQa || !IsDirectQaDestinationAvailable(provider) || DirectQaTransportBusy) return false;
            if(usingDirectQaPreset && directQaProvider==provider && transport is DirectQaTransport) return true;
            ApplyDirectQaDestination(provider);
            SetReportUploadPermission(runtimeConfig.UploadsEnabled);
            return true;
        }

        private void ApplyDirectQaBuildProfiles(DirectQaBuildProfiles profiles)
        {
            if(profiles==null || (!profiles.Has(0) && !profiles.Has(1))) return;
            directQaProfiles=profiles;
            ApplyDirectQaDestination(profiles.Has(profiles.defaultProvider)?profiles.defaultProvider:profiles.Has(0)?0:1);
        }

        private void ApplyDirectQaDestination(int provider)
        {
            var preset=directQaProfiles.Get(provider);
            SetDirectQaTransport(provider,preset.credential,preset.chatId);
            usingDirectQaPreset=true;
            Status=DirectQaStatus;
        }

#if UNITY_ANDROID && DEVELOPMENT_BUILD && !UNITY_EDITOR
        private void LoadDirectQaBuildPreset()
        {
            if (!Debug.isDebugBuild || catalog == null || !runtimeConfig.UploadsEnabled ||
                runtimeConfig.ReportingMode != ReportingMode.DirectQa ||
                (runtimeConfig.Mode != DebuggerMode.Qa && runtimeConfig.Mode != DebuggerMode.Development)) return;
            var asset = Resources.Load<TextAsset>(DirectQaBuildPreset.ResourcePath);
            if (asset == null) return;
            try
            {
                ApplyDirectQaBuildProfiles(JsonUtility.FromJson<DirectQaBuildProfiles>(asset.text));
            }
            catch (ArgumentException) { Status = "Invalid Direct QA APK destination."; }
            finally { Resources.UnloadAsset(asset); }
        }
#endif

        private void SetDirectQaTransport(int provider, string credential, string chat)
        {
            var directory=Path.Combine(Application.persistentDataPath,"DreamyDebugger","DirectReceipts");
            DirectQaTransport next=provider==0 ? (DirectQaTransport)new DirectDiscordTransport(credential,directory) :
                provider==1 ? new DirectTelegramTransport(credential,chat,directory) : throw new ArgumentException();
            SetReportUploadPermission(false);
            (transport as IDisposable)?.Dispose();
            transport=next;
            pump=new OutboxPump(Outbox,next,true);
            directQaProvider=provider;
            usingDirectQaPreset=false;
        }

        public bool ConfigureDirectQa(int provider, string credential, string chat)
        {
            if (!CanUseDirectQa || DirectQaTransportBusy) return false;
            try
            {
                SetDirectQaTransport(provider,credential,chat);
                // A text acknowledgement is required before the queue is allowed to send attachments.
                Status="Direct QA configured. Test text before sending reports."; return true;
            }
            catch (ArgumentException) { Status="Invalid Direct QA credentials. Check the provider fields."; return false; }
        }
        public void ForgetDirectQa()
        {
            if (!(transport is DirectQaTransport)) return;
            SetReportUploadPermission(false); (transport as IDisposable)?.Dispose(); transport=null; pump=null;
            usingDirectQaPreset=false;
            Status="Direct credentials forgotten. Reports remain on this device.";
        }
        public async Task<string> TestDirectConnectionAsync()
        {
            if (!CanUseDirectQa || !(transport is DirectQaTransport direct)) return "Configure Direct QA first.";
            if (!CanAttemptReportNetwork) return "No network connection. Connect to the internet and test again.";
            var result=await direct.TestConnectionAsync(lifetime.Token);
            if (ReferenceEquals(transport,direct) && CanUseDirectQa && result.Status==DeliveryStatus.Accepted)
                SetReportUploadPermission(runtimeConfig.UploadsEnabled);
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
            if (!CanAttemptReportNetwork) { Status="Report remains on this device; offline."; return; }
            var activePump=pump;
            var token=uploadLifetime.Token;
            await direct.AllowUncertainRetryAsync(id,token);
            if (ReferenceEquals(pump,activePump) && CanUseDirectQa)
                await activePump.RetryNowAsync(id,token);
        }
#endif
    }
}
