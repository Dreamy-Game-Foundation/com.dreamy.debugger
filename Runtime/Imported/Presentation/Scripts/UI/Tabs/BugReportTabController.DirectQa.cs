#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dreamy.Debugger.Presentation.Internal;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Reporting;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    public partial class BugReportTabController
    {
        private GameObject directTools;
        private IDebugPanelService directPanelService;
        private Text directStatus;
        private bool directBusy;
        private string uncertainReportId, unassignedReportId;
        private GameObject credentialForm;
        private Button deliveryTools;
        private InputField credentialField, chatField;
        private int credentialProvider;
        private Button discordDestination, telegramDestination;
        private readonly List<Button> directButtons = new List<Button>();

        private void BuildDirectUi()
        {
            var destinationRow=Row(column,"Report destination");
            destinationRow.SetSiblingIndex(0);
            discordDestination=DirectButton(destinationRow,"Discord",()=>SelectDirect(0));
            telegramDestination=DirectButton(destinationRow,"Telegram",()=>SelectDirect(1));
            directStatus=Label("Direct QA status");
            directStatus.transform.SetSiblingIndex(1);
            var toolsRow=Row(column,"Direct QA actions");
            toolsRow.SetSiblingIndex(2);
            deliveryTools=DirectButton(toolsRow,"Delivery tools",()=>{
                if(directTools.activeSelf)CloseDirectActions();
                else directTools.SetActive(true);
            });

            directTools=new GameObject("Direct QA diagnostics",typeof(RectTransform),typeof(VerticalLayoutGroup));
            directTools.transform.SetParent(column,false);
            directTools.transform.SetSiblingIndex(3);
            var layout=directTools.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=true;
            layout.childForceExpandHeight=false;
            var setup=Row(directTools.transform,"Direct QA destination setup");
            DirectButton(setup,"Set up Discord",()=>OpenCredentials(0));
            DirectButton(setup,"Set up Telegram",()=>OpenCredentials(1));
            var tests=Row(directTools.transform,"Direct tests");
            DirectButton(tests,"Test Connection",()=>_=RunDirect(controls=>controls.TestDirect()));
            DirectButton(tests,"Send Test Report",()=>_=RunDirect(async controls=>
            {
                var saved=await controls.TestDirectReport();
                await RefreshReports();
                return saved?controls.DirectStatus():"The test could not be queued.";
            }));
            DirectButton(Row(directTools.transform,"Unassigned report"),"Assign selected report",()=>{
                if(reports.Length==0 || !reports[selected].NeedsDirectDestination ||
                    reports[selected].State==LocalReportState.RelayAccepted)return;
                var controls=GetControls?.Invoke();
                if(controls?.UploadEnabled()!=true)
                {
                    directStatus.text="Test the destination before assigning reports.";
                    return;
                }
                var id=reports[selected].Bundle.Report.Id;
                if(unassignedReportId!=id)
                {
                    unassignedReportId=id;
                    directStatus.text="Tap again to assign this report to the active destination. It may have been sent before the update.";
                    return;
                }
                unassignedReportId=null;
                _=RunDirect(async current=>{await current.AssignDirectDestination(id);await RefreshReports();return current.DirectStatus();});
            });
            DirectButton(Row(directTools.transform,"Uncertain delivery"),"Retry uncertain selected report",()=>{
                if(reports.Length==0 || reports[selected].State!=LocalReportState.Failed || !reports[selected].DeliveryUncertain ||
                    reports[selected].Destination!=GetControls?.Invoke()?.ActiveDestination?.Invoke())return;
                var id=reports[selected].Bundle.Report.Id;
                if(uncertainReportId!=id)
                {
                    uncertainReportId=id;
                    directStatus.text="Provider may already have received this report. Tap again to accept possible duplication.";
                    return;
                }
                uncertainReportId=null;
                _=RunDirect(async controls=>{await controls.ConfirmDirectRetry(id);await RefreshReports();return controls.DirectStatus();});
            });
            directTools.SetActive(false);
            directPanelService=Service.Panel;
            directPanelService.VisibilityChanged+=OnDirectPanelVisibilityChanged;
            RenderDestinations();
        }

        private void OnDirectPanelVisibilityChanged(IDebugPanelService panel,bool visible)
        { if(!visible)CloseDirectActions(); }

        private Button DirectButton(Transform parent,string title,Action action)
        {
            var button=ActionButton(parent,title,()=>{if(!directBusy)action();});
            directButtons.Add(button);
            return button;
        }

        private void SelectDirect(int provider)
        {
            uncertainReportId=unassignedReportId=null;
            var controls=GetControls?.Invoke();
            if(controls?.SelectDirect?.Invoke(provider)!=true)
            {
                directStatus.text="Wait for the current send to finish, then choose a destination.";
                return;
            }
            RenderDestinations();
            Render();
        }

        private void RenderDestinations()
        {
            if(discordDestination==null)return;
            var controls=GetControls?.Invoke();
            var hasDiscord=controls?.HasDirectDestination?.Invoke(0)==true;
            var hasTelegram=controls?.HasDirectDestination?.Invoke(1)==true;
            foreach(var button in directButtons)button.interactable=!directBusy;
            deliveryTools.interactable=!directBusy;
            discordDestination.interactable=!directBusy && hasDiscord;
            telegramDestination.interactable=!directBusy && hasTelegram;
            var active=controls?.DirectProvider?.Invoke() ?? -1;
            discordDestination.GetComponentInChildren<Text>().text=hasDiscord && active==0?"✓ Discord":"Discord";
            telegramDestination.GetComponentInChildren<Text>().text=hasTelegram && active==1?"✓ Telegram":"Telegram";
            directStatus.text=active>=0
                ? "Send to "+(active==1?"Telegram":"Discord")
                : Application.isEditor
                    ? "Set up a destination in Telegram & Reports, then restart Play Mode."
                    : "Open Delivery tools to set up a destination.";
        }

        private void CloseDirectActions()
        {
            if(directTools!=null)directTools.SetActive(false);
            uncertainReportId=unassignedReportId=null;
            ClearCredentials();
        }

        private void OpenCredentials(int provider)
        {
            if(credentialForm==null)
            {
                credentialForm=new GameObject("Session credentials",typeof(RectTransform),typeof(VerticalLayoutGroup));
                credentialForm.transform.SetParent(directTools.transform,false);
                credentialForm.transform.SetSiblingIndex(1);
                var layout=credentialForm.GetComponent<VerticalLayoutGroup>();
                layout.childControlWidth=layout.childControlHeight=layout.childForceExpandWidth=true;
                layout.childForceExpandHeight=false;
                credentialField=CreateCredentialInput("Webhook or bot token",true);
                chatField=CreateCredentialInput("Chat ID",false);
                DirectButton(Row(credentialForm.transform,"Apply credentials"),"Connect",()=>_=RunDirect(async controls=>{
                    if(controls.ConfigureDirect?.Invoke(credentialProvider,credentialField.text.Trim(),chatField.text.Trim())!=true)
                        return "Check the provider fields or wait for the current send.";
                    ClearCredentials();
                    return await controls.TestDirect();
                }));
            }
            ClearCredentials();
            credentialProvider=provider;
            credentialForm.SetActive(true);
            chatField.transform.parent.gameObject.SetActive(provider==1);
        }

        private InputField CreateCredentialInput(string caption,bool secret)
        {
            var row=Row(credentialForm.transform,caption);
            var input=Instantiate(sheet.DescriptionField,row);
            input.name=caption;
            input.onValueChanged=new InputField.OnChangeEvent();
            input.onEndEdit=new InputField.EndEditEvent();
            input.lineType=InputField.LineType.SingleLine;
            input.contentType=secret?InputField.ContentType.Password:InputField.ContentType.Standard;
            input.characterLimit=secret?512:32;
            input.textComponent.supportRichText=false;
            if(input.placeholder is Text placeholder)placeholder.text=caption;
            input.SetTextWithoutNotify("");
            input.gameObject.SetActive(true);
            return input;
        }

        private void ClearCredentials()
        {
            if(credentialForm==null)return;
            credentialField.SetTextWithoutNotify("");
            chatField.SetTextWithoutNotify("");
            credentialForm.SetActive(false);
        }

        private async Task RunDirect(Func<Controls,Task<string>> action)
        {
            var controls=GetControls?.Invoke();
            if(directBusy || controls?.SelectDirect==null)return;
            directBusy=true;
            foreach(var button in directButtons)button.interactable=false;
            string message;
            try{message=await action(controls);}
            catch(OperationCanceledException){message="Canceled. Check the destination before retrying.";}
            catch(Exception){message="Diagnostic failed. Reports remain on this device.";}
            if(this==null)return;
            directBusy=false;
            RenderDestinations();
            Render();
            directStatus.text=message;status.text=message;
        }
    }
}
#endif
