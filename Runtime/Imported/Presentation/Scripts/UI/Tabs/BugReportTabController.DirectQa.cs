#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dreamy.Debugger.Presentation.Internal;
using Dreamy.Debugger.Presentation.Services;
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
        private string uncertainReportId;
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
            DirectButton(toolsRow,"Delivery tools",()=>directTools.SetActive(!directTools.activeSelf));

            directTools=new GameObject("Direct QA diagnostics",typeof(RectTransform),typeof(VerticalLayoutGroup));
            directTools.transform.SetParent(column,false);
            directTools.transform.SetSiblingIndex(3);
            var layout=directTools.GetComponent<VerticalLayoutGroup>();
            layout.childControlWidth=layout.childControlHeight=true;
            layout.childForceExpandWidth=true;
            layout.childForceExpandHeight=false;
            var tests=Row(directTools.transform,"Direct tests");
            DirectButton(tests,"Test Connection",()=>_=RunDirect(controls=>controls.TestDirect()));
            DirectButton(tests,"Send Test Report",()=>_=RunDirect(async controls=>
            {
                var saved=await controls.TestDirectReport();
                await RefreshReports();
                return saved?controls.DirectStatus():"The test could not be queued.";
            }));
            DirectButton(Row(directTools.transform,"Uncertain delivery"),"Retry uncertain selected report",()=>{
                if(reports.Length==0)return;
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
            uncertainReportId=null;
            var controls=GetControls?.Invoke();
            if(controls?.SelectDirect?.Invoke(provider)!=true)
            {
                directStatus.text="Wait for the current send to finish, then choose a destination.";
                return;
            }
            RenderDestinations();
        }

        private void RenderDestinations()
        {
            if(discordDestination==null)return;
            var controls=GetControls?.Invoke();
            var hasDiscord=controls?.HasDirectDestination?.Invoke(0)==true;
            var hasTelegram=controls?.HasDirectDestination?.Invoke(1)==true;
            foreach(var button in directButtons)button.interactable=!directBusy && (hasDiscord || hasTelegram);
            discordDestination.interactable=!directBusy && hasDiscord;
            telegramDestination.interactable=!directBusy && hasTelegram;
            var active=controls?.DirectProvider?.Invoke() ?? -1;
            discordDestination.GetComponentInChildren<Text>().text=hasDiscord && active==0?"✓ Discord":"Discord";
            telegramDestination.GetComponentInChildren<Text>().text=hasTelegram && active==1?"✓ Telegram":"Telegram";
            directStatus.text=hasDiscord || hasTelegram
                ? "Send to "+(active==1?"Telegram":"Discord")
                : "Configure destinations in Unity Editor and rebuild this QA app.";
        }

        private void CloseDirectActions()
        {
            if(directTools!=null)directTools.SetActive(false);
            uncertainReportId=null;
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
            directStatus.text=message;status.text=message;
        }
    }
}
#endif
