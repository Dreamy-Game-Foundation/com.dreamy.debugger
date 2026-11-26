using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using Dreamy.Debugger.Reporting.DirectQa;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    public static class DirectQaBuildSetup
    {
        internal static string Key(DebuggerConfiguration configuration)
        {
            var guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(configuration));
            return string.IsNullOrEmpty(guid)?null:"Dreamy.DirectQaProfiles."+guid;
        }

        internal static DirectQaBuildProfiles Read(DebuggerConfiguration configuration)
        {
            var key=Key(configuration);
            if(key==null)return null;
            var text=SessionState.GetString(key,"");
            return text.Length==0?null:JsonUtility.FromJson<DirectQaBuildProfiles>(text);
        }

        public static void SetProfiles(DebuggerConfiguration configuration,DirectQaBuildProfiles profiles)
        {
            var key=Key(configuration)??throw new ArgumentException("Select a saved project configuration.");
            if(profiles==null)SessionState.EraseString(key);
            else SessionState.SetString(key,JsonUtility.ToJson(profiles));
        }

        public static void SetPreset(DebuggerConfiguration configuration,DirectQaBuildPreset preset) =>
            SetProfiles(configuration,preset==null?null:new DirectQaBuildProfiles
            {
                discord=preset.provider==0?preset:null,telegram=preset.provider==1?preset:null,defaultProvider=preset.provider
            });

        internal static bool FieldsReady(DirectQaBuildProfiles profiles) => profiles!=null &&
            (profiles.Has(0) || profiles.Has(1)) && (!HasInput(profiles.discord) || profiles.Has(0)) &&
            (!HasInput(profiles.telegram) || profiles.Has(1));

        private static bool HasInput(DirectQaBuildPreset preset) =>
            !string.IsNullOrEmpty(preset?.credential) || !string.IsNullOrEmpty(preset?.chatId);

        internal static void WriteForBuild(DebuggerConfiguration configuration,bool development,BuildTarget target,string resourcesPath)
        {
            if(!development || target!=BuildTarget.Android || !configuration.UploadsEnabled ||
               configuration.ReportingMode!=ReportingMode.DirectQa ||
               (configuration.Mode!=DebuggerMode.Development && configuration.Mode!=DebuggerMode.Qa))return;
            var profiles=Read(configuration);
            if(profiles==null)return;
            if(!FieldsReady(profiles))
                throw new BuildFailedException("Complete or clear each Direct QA destination in the Reports setup wizard before building.");
            File.WriteAllText(Path.Combine(resourcesPath,DirectQaBuildPreset.ResourcePath+".bytes"),JsonUtility.ToJson(profiles));
        }
    }

    internal sealed class DirectQaSetupWizard
    {
        private enum Destination { Discord, Telegram }
        private sealed class Check
        {
            public DirectQaBuildPreset Preset;
            public bool Busy, Accepted;
            public string Message;
        }
        private readonly Check[] checks={new Check(),new Check()};
        private bool loadingSaved;
        private string restoreMessage, restoreConfiguration;

        public void Draw(DebuggerConfiguration configuration,SerializedObject serialized,CancellationToken token,Action repaint)
        {
            if(DirectQaBuildSetup.Key(configuration)==null)
            {
                EditorGUILayout.HelpBox("Select a saved Project configuration to set up Direct QA.",MessageType.Info);
                return;
            }
            var mode=(DebuggerMode)serialized.FindProperty("Mode").enumValueIndex;
            var uploads=serialized.FindProperty("UploadsEnabled").boolValue;
            var profiles=DirectQaBuildSetup.Read(configuration);
            var readyMode=mode==DebuggerMode.Development || mode==DebuggerMode.Qa;
            var readyFields=DirectQaBuildSetup.FieldsReady(profiles);
            var configKey=DirectQaBuildSetup.Key(configuration);
            var loadedKey=configKey+".SavedProfileChecked";
            if(readyMode && !loadingSaved && !SessionState.GetBool(loadedKey,false))
            {
                SessionState.SetBool(loadedKey,true);
                if(profiles==null || !profiles.Has(0) || !profiles.Has(1))
                    _=RestoreSaved(configuration,token,repaint);
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Step("1  Enable QA delivery",readyMode && uploads);
            if(!readyMode)EditorGUILayout.LabelField("Set Mode to Development or Qa in the Access tab.",EditorStyles.wordWrappedMiniLabel);
            if(readyMode && !uploads && GUILayout.Button("Enable sending"))serialized.FindProperty("UploadsEnabled").boolValue=true;
            if(readyMode && uploads && GUILayout.Button("Pause sending"))serialized.FindProperty("UploadsEnabled").boolValue=false;

            Step("2  Choose destinations",readyFields);
            bool embed;
            using(new EditorGUI.DisabledScope(!readyMode || loadingSaved))
                embed=EditorGUILayout.ToggleLeft("Include in Android Development build",profiles!=null);
            if(embed && profiles==null)
            {
                profiles=new DirectQaBuildProfiles();
                DirectQaBuildSetup.SetProfiles(configuration,profiles);
            }
            if(!embed && profiles!=null)
            {
                DirectQaBuildSetup.SetProfiles(configuration,null);
                profiles=null;
            }

            using(new EditorGUI.DisabledScope(!readyMode || loadingSaved))
                if(GUILayout.Button(loadingSaved?"Restoring saved destinations…":"Restore saved Discord / Telegram"))
                    _=RestoreSaved(configuration,token,repaint);
            if(restoreConfiguration==configKey && !string.IsNullOrEmpty(restoreMessage))
                EditorGUILayout.LabelField(restoreMessage,EditorStyles.wordWrappedMiniLabel);

            if(profiles!=null)
            {
                using(new EditorGUI.DisabledScope(!readyMode))
                {
                    var before=JsonUtility.ToJson(profiles);
                    profiles.discord=DrawDestination(profiles.discord,0);
                    profiles.telegram=DrawDestination(profiles.telegram,1);
                    if(profiles.Has(0) && profiles.Has(1))
                        profiles.defaultProvider=(int)(Destination)EditorGUILayout.EnumPopup("Default destination",(Destination)profiles.defaultProvider);
                    if(before!=JsonUtility.ToJson(profiles))DirectQaBuildSetup.SetProfiles(configuration,profiles);
                }
                EditorGUILayout.LabelField("Use this APK internally. Its credentials can be extracted.",EditorStyles.wordWrappedMiniLabel);
            }

            readyFields=DirectQaBuildSetup.FieldsReady(profiles);
            var checkedAll=readyFields && (!profiles.Has(0) || Verified(0,profiles.discord)) &&
                (!profiles.Has(1) || Verified(1,profiles.telegram));
            Step("3  Verify each configured destination",checkedAll);
            using(new EditorGUI.DisabledScope(!readyMode))
            {
                if(profiles?.Has(0)==true)DrawTest(profiles,0,token,repaint);
                if(profiles?.Has(1)==true)DrawTest(profiles,1,token,repaint);
            }
            if(!readyFields)EditorGUILayout.LabelField("Add at least one destination to send a test.",EditorStyles.wordWrappedMiniLabel);
            var android=EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android && EditorUserBuildSettings.development;
            Step("4  Prepare Android build",readyMode && uploads && checkedAll && android);
            EditorGUILayout.LabelField("Select Android and Development Build. On the phone, choose a destination in Report.",EditorStyles.wordWrappedMiniLabel);
            if(GUILayout.Button("Open Build Profiles"))EditorApplication.ExecuteMenuItem("File/Build Profiles");
            EditorGUILayout.EndVertical();
        }

        private async Task RestoreSaved(DebuggerConfiguration configuration,CancellationToken token,Action repaint)
        {
            loadingSaved=true;
            restoreMessage=null;
            restoreConfiguration=DirectQaBuildSetup.Key(configuration);
            try
            {
                var saved=await Task.WhenAll(TelegramRelaySettings.ReadSavedProfileAsync(0,token),
                    TelegramRelaySettings.ReadSavedProfileAsync(1,token));
                if(token.IsCancellationRequested || configuration==null) return;
                var profiles=DirectQaBuildSetup.Read(configuration)??new DirectQaBuildProfiles();
                var added=MergeSaved(profiles,saved[0],saved[1]);
                if(added>0)DirectQaBuildSetup.SetProfiles(configuration,profiles);
                restoreMessage=added>0 ? "Restored from this computer's credential store." :
                    profiles.Has(0) || profiles.Has(1) ? "Your existing destinations are ready to test." :
                    "No saved destinations were available. Unlock your credential store or enter a destination below.";
            }
            finally
            {
                loadingSaved=false;
                if(!token.IsCancellationRequested)repaint();
            }
        }

        internal static int MergeSaved(DirectQaBuildProfiles profiles,DirectQaBuildPreset discord,DirectQaBuildPreset telegram)
        {
            var added=0;
            if(profiles.discord==null && discord?.IsValid==true){profiles.discord=discord;added++;}
            if(profiles.telegram==null && telegram?.IsValid==true){profiles.telegram=telegram;added++;}
            return added;
        }

        internal static void Step(string title,bool ready)
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                var icon=EditorGUIUtility.IconContent(ready?"TestPassed":"console.infoicon.sml");
                if(icon.image!=null)GUILayout.Label(icon,GUILayout.Width(18),GUILayout.Height(18));
                else GUILayout.Label(ready?"✓":"○",GUILayout.Width(18));
                GUILayout.Label(title,EditorStyles.boldLabel);
            }
        }

        private static DirectQaBuildPreset DrawDestination(DirectQaBuildPreset preset,int provider)
        {
            using(new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                Step(provider==0?"Discord":"Telegram",preset?.IsValid==true);
                var credential=EditorGUILayout.PasswordField(provider==0?"Webhook":"Bot token",preset?.credential??"").Trim();
                var chat=provider==1?EditorGUILayout.TextField("Chat ID",preset?.chatId??"").Trim():"";
                if(credential.Length==0 && chat.Length==0)return null;
                var input=new DirectQaBuildPreset{provider=provider,credential=credential,chatId=chat};
                if(!input.IsValid)EditorGUILayout.LabelField(provider==0?"Enter a Discord webhook URL." : "Enter a bot token and its chat ID.",EditorStyles.wordWrappedMiniLabel);
                return input;
            }
        }

        private bool Verified(int provider,DirectQaBuildPreset preset) => checks[provider].Accepted && Matches(checks[provider].Preset,preset);
        private static bool Matches(DirectQaBuildPreset a,DirectQaBuildPreset b) =>
            a!=null && b!=null && a.provider==b.provider && a.credential==b.credential && a.chatId==b.chatId;

        private void DrawTest(DirectQaBuildProfiles profiles,int provider,CancellationToken token,Action repaint)
        {
            var check=checks[provider];
            var preset=profiles?.Get(provider);
            using(new EditorGUI.DisabledScope(check.Busy || preset?.IsValid!=true))
                if(GUILayout.Button(check.Busy?"Testing…":"Send "+(provider==0?"Discord":"Telegram")+" test message"))
                    _=Test(check,preset,token,repaint);
            if(Matches(check.Preset,preset) && !string.IsNullOrEmpty(check.Message))
                EditorGUILayout.HelpBox(check.Message,check.Accepted?MessageType.Info:MessageType.Warning);
        }

        private static async Task Test(Check check,DirectQaBuildPreset preset,CancellationToken token,Action repaint)
        {
            check.Busy=true;
            check.Preset=preset;
            check.Accepted=false;
            check.Message="";
            try
            {
                using DirectQaTransport transport=preset.provider==0
                    ? (DirectQaTransport)new DirectDiscordTransport(preset.credential,Path.GetTempPath())
                    : new DirectTelegramTransport(preset.credential,preset.chatId,Path.GetTempPath());
                var result=await transport.TestConnectionAsync(token);
                check.Accepted=result.Status==DeliveryStatus.Accepted;
                check.Message=result.DiagnosticSummary;
            }
            catch(OperationCanceledException) { }
            catch(Exception) { check.Message="Could not test this destination. Check the connection and try again."; }
            finally
            {
                check.Busy=false;
                if(!token.IsCancellationRequested)repaint();
            }
        }
    }
}
