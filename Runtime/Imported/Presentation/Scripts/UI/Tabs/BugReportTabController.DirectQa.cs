#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    public partial class BugReportTabController
    {
        private GameObject directPanel;
        private Action<bool> directVisibility;
        private InputField directSecret, directChat;
        private Text directStatus;
        private bool directBusy, confirmUncertain;
        private int directProvider;
        private readonly List<Button> directButtons = new List<Button>();
        private void BuildDirectUi()
        {
            directVisibility=GetControls?.Invoke()?.DirectVisible;
            var toggle=ActionButton(Row(column,"Direct QA setup"),"Direct QA setup",()=>{
                var visible=!directPanel.activeSelf;directPanel.SetActive(visible);directVisibility?.Invoke(visible);
            });
            toggle.transform.parent.SetSiblingIndex(0);
            directPanel=new GameObject("Direct QA session",typeof(RectTransform),typeof(VerticalLayoutGroup));
            directPanel.transform.SetParent(column,false);directPanel.transform.SetSiblingIndex(1);
            var layout=directPanel.GetComponent<VerticalLayoutGroup>();layout.spacing=8;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            directStatus=Label("Direct QA status");directStatus.transform.SetParent(directPanel.transform,false);
            var providers=Row(directPanel.transform,"Provider");
            DirectButton(providers,"Discord",()=>SelectDirect(0));DirectButton(providers,"Telegram",()=>SelectDirect(1));
            directSecret=DirectField("QA credential (hidden)",true);directChat=DirectField("Telegram chat ID",false);
            var setup=Row(directPanel.transform,"Session credentials");
            DirectButton(setup,"Apply session",()=>{
                var valid=GetControls?.Invoke()?.ConfigureDirect?.Invoke(directProvider,directSecret.text.Trim(),directChat.text.Trim())==true;
                directStatus.text=valid?"Session configured. Test text, then JSON + image.":"Check provider fields, upload policy and QA build.";
                directSecret.text="";directChat.text="";
            });
            DirectButton(setup,"Forget",()=>{GetControls?.Invoke()?.ForgetDirect?.Invoke();ClearDirectFields();status.text="Credentials forgotten; local reports retained.";});
            var tests=Row(directPanel.transform,"Direct tests");
            DirectButton(tests,"Test Connection",()=>_=RunDirect(async()=>await GetControls().TestDirect()));
            DirectButton(tests,"Send Test Report",()=>{
                directPanel.SetActive(false);directVisibility?.Invoke(false);
                _=RunDirect(async()=>{var saved=await GetControls().TestDirectReport();await RefreshReports();return saved?GetControls().DirectStatus():"Test not sent; configure Direct QA and enable uploads.";});
            });
            DirectButton(Row(directPanel.transform,"Uncertain delivery"),"Retry uncertain selected report",()=>{
                if(reports.Length==0)return;
                if(!confirmUncertain){confirmUncertain=true;directStatus.text="Provider may already have received this report. Click again to accept possible duplication.";return;}
                confirmUncertain=false;var id=reports[selected].Bundle.Report.Id;
                _=RunDirect(async()=>{await GetControls().ConfirmDirectRetry(id);await RefreshReports();return GetControls().DirectStatus();});
            });
            SelectDirect(0);directPanel.SetActive(false);
        }
        private InputField DirectField(string hint,bool password)
        {
            var field=Instantiate(sheet.DescriptionField,Row(directPanel.transform,hint));
            field.name=password?"QA credential":"QA chat ID";
            field.onValueChanged=new InputField.OnChangeEvent();field.onEndEdit=new InputField.EndEditEvent();
            field.text="";field.lineType=InputField.LineType.SingleLine;field.characterLimit=512;
            field.contentType=password?InputField.ContentType.Password:InputField.ContentType.Standard;
            field.shouldHideMobileInput=password;field.textComponent.supportRichText=false;
            if(field.placeholder is Text label)label.text=hint;
            var sizing=field.GetComponent<LayoutElement>()??field.gameObject.AddComponent<LayoutElement>();
            sizing.minWidth=0;sizing.minHeight=sizing.preferredHeight=48;sizing.flexibleWidth=1;
            return field;
        }
        private Button DirectButton(Transform parent,string title,Action action)
        {var button=ActionButton(parent,title,()=>{if(!directBusy)action();});directButtons.Add(button);return button;}
        private void SelectDirect(int provider)
        {
            GetControls?.Invoke()?.ForgetDirect?.Invoke();directProvider=provider;directSecret.text="";directChat.text="";
            directChat.transform.parent.gameObject.SetActive(provider==1);
            if(directSecret.placeholder is Text hint)hint.text=provider==0?"Discord webhook (hidden)":"Telegram bot token (hidden)";
            directStatus.text=(provider==0?"Discord":"Telegram")+": credentials stay in memory until Forget or app exit.";
        }
        private void ClearDirectFields()
        {
            if(directSecret!=null)directSecret.text="";if(directChat!=null)directChat.text="";
            if(directPanel!=null)directPanel.SetActive(false);
            directVisibility?.Invoke(false);confirmUncertain=false;
        }
        private async Task RunDirect(Func<Task<string>> action)
        {
            if(directBusy)return;directBusy=true;foreach(var button in directButtons)button.interactable=false;
            try{var message=await action();if(this!=null){directStatus.text=message;status.text=message;}}
            catch(OperationCanceledException){if(this!=null)directStatus.text="Canceled; check destination before retrying.";}
            catch(Exception){if(this!=null)directStatus.text="Diagnostic failed; credentials were not logged. Reports remain local.";}
            finally{if(this!=null){directBusy=false;foreach(var button in directButtons)button.interactable=true;}}
        }
    }
}
#endif
