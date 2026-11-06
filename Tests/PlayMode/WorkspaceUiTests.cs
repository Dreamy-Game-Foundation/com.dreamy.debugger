#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Internal;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
using Dreamy.Debugger.Presentation.UI.Tabs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace Dreamy.Debugger.Tests
{
    public sealed class WorkspaceUiTests
    {
        private GameObject root;
        private DebuggerRuntime runtime;
        private DebuggerConfiguration config;
        private IEnumerator Create(string credential="0000",bool passphrase=false){
            if(DebuggerRuntime.Instance!=null)Object.Destroy(DebuggerRuntime.Instance.gameObject);yield return null;
            root=new GameObject("Offline workspace fixture");root.SetActive(false);
            config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.PersistRuntimePreferences=false;config.Credential=credential;config.Passphrase=passphrase;config.UploadsEnabled=false;config.AutoReportErrors=false;
            runtime=root.AddComponent<DebuggerRuntime>();runtime.ConfigurationAsset=config;
            runtime.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            root.SetActive(true);yield return null;yield return null;
        }
        private IEnumerator Unlock(){runtime.Open(DefaultTabs.Options);yield return null;var pin=Object.FindFirstObjectByType<PinEntryControl>();for(var i=0;i<4;i++)pin.PushNumber(0);yield return null;yield return null;}
        [UnityTearDown]public IEnumerator Cleanup(){if(root!=null)Object.Destroy(root);if(config!=null)Object.Destroy(config);yield return null;yield return null;}
        [UnityTest]public IEnumerator ResourceCardUsesSameRegistryAndSurvivesSearchCollapseAndPin(){
            yield return Create();long coins=9825,amount=100;
            using(var lease=runtime.Cheats.Register(new[]{
                CheatOption.Value("coins.balance","Economy","Coin balance",()=>coins).WithCard("coins","Coins",CheatCardKind.Resource,CheatCardRole.Balance),
                CheatOption.Value("coins.amount","Economy","Grant amount",()=>amount,v=>amount=v,1,10000).WithCard("coins","Coins",CheatCardKind.Resource,CheatCardRole.Amount),
                CheatOption.Button("coins.grant","Economy","Add coins",()=>coins+=amount,true).WithCard("coins","Coins",CheatCardKind.Resource,CheatCardRole.Action)})){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();
                var card=Object.FindFirstObjectByType<OptionCard>();Assert.That(card.CardId,Is.EqualTo("coins"));var cardId=card.GetInstanceID();
                var button=card.GetComponentInChildren<ActionControl>();button.Button.onClick.Invoke();yield return null;
                Assert.That(coins,Is.EqualTo(9825));var confirm=Object.FindFirstObjectByType<ConfirmationPrompt>();Assert.That(confirm,Is.Not.Null);
                confirm.Confirm.onClick.Invoke();Assert.That(coins,Is.EqualTo(9925));yield return null;
                var balance=card.BalanceSlot.GetComponentInChildren<NumberControl>();Assert.That(balance.NumberSpinner.text,Is.EqualTo("9925"));
                var count=runtime.Cheats.Snapshot().Count;tab.SetSearch("not found");Assert.That(tab.VisibleOptionCount,Is.Zero);Assert.That(runtime.Cheats.Snapshot().Count,Is.EqualTo(count));
                tab.SetSearch("Coins");Assert.That(tab.VisibleOptionCount,Is.EqualTo(3));Assert.That(card.GetInstanceID(),Is.EqualTo(cardId));
                tab.SetSearch("");tab.ToggleCategory("Economy");Assert.That(card.gameObject.activeInHierarchy,Is.False);tab.SetSearch("Coins");Assert.That(card.gameObject.activeInHierarchy,Is.True);tab.SetSearch("");Assert.That(card.gameObject.activeInHierarchy,Is.False);coins=10000;runtime.Cheats.NotifyValuesChanged();
                tab.ToggleCategory("Economy");yield return null;Assert.That(balance.NumberSpinner.text,Is.EqualTo("10000"));
                coins=long.MaxValue;runtime.Cheats.NotifyValuesChanged();Assert.That(balance.NumberSpinner.text,Is.EqualTo(long.MaxValue.ToString()));Assert.That(balance.NumberSpinner.textComponent.fontSize,Is.GreaterThanOrEqualTo(14));
                Service.PinnedUI.Pin(balance.Option);tab.SetPinnedOnly(true);Assert.That(tab.VisibleOptionCount,Is.EqualTo(1));
                runtime.Relock();Assert.Throws<System.UnauthorizedAccessException>(()=>runtime.Cheats.Execute("coins.grant",true));
            }
        }
        [UnityTest]public IEnumerator ConfirmationRejectsChangedAmountAndDisposedAction(){
            yield return Create();long balance=0,amount=100;
            var lease=runtime.Cheats.Register(new[]{CheatOption.Value("race.balance","Economy","Balance",()=>balance).WithCard("race","Coins",CheatCardKind.Resource,CheatCardRole.Balance),CheatOption.Value("race.amount","Economy","Amount",()=>amount,v=>amount=v).WithCard("race","Coins",CheatCardKind.Resource,CheatCardRole.Amount),CheatOption.Button("race.grant","Economy","Add coins",()=>balance+=amount,true).WithCard("race","Coins",CheatCardKind.Resource,CheatCardRole.Action)});
            try{
                yield return Unlock();var action=Object.FindFirstObjectByType<OptionCard>().GetComponentInChildren<ActionControl>();action.Button.onClick.Invoke();yield return null;
                var modal=Object.FindFirstObjectByType<ConfirmationPrompt>();Assert.That(modal.Title.text,Does.Contain("100"));amount=200;modal.Confirm.onClick.Invoke();Assert.That(balance,Is.Zero);Assert.That(modal.Title.text,Is.EqualTo("Values changed"));modal.Cancel.onClick.Invoke();yield return null;
                action.Button.onClick.Invoke();yield return null;modal=Object.FindFirstObjectByType<ConfirmationPrompt>();lease.Dispose();modal.Confirm.onClick.Invoke();Assert.That(balance,Is.Zero);Assert.That(modal.Title.text,Is.EqualTo("Values changed"));
            }finally{lease.Dispose();}
        }
        [UnityTest]public IEnumerator OwnershipCardIsGrantOnlyAndReflectsExternalRevocation(){
            yield return Create();bool owned=false;int grants=0;
            using(var lease=runtime.Cheats.Register(new[]{
                CheatOption.Value("ads.owned","Entitlements","No Ads owned",()=>owned).WithCard("ads","No Ads",CheatCardKind.Ownership,CheatCardRole.Owned),
                CheatOption.Button("ads.unlock","Entitlements","Grant No Ads",()=>{owned=true;grants++;},true).WithCard("ads","No Ads",CheatCardKind.Ownership,CheatCardRole.Action)})){
                yield return Unlock();var card=Object.FindFirstObjectByType<OptionCard>();var grant=card.GetComponentInChildren<ActionControl>();
                grant.Button.onClick.Invoke();yield return null;Object.FindFirstObjectByType<ConfirmationPrompt>().Confirm.onClick.Invoke();yield return null;
                Assert.That(grants,Is.EqualTo(1));Assert.That(card.OwnedLabel.text,Is.EqualTo("Owned"));Assert.That(card.ActionSlot.gameObject.activeSelf,Is.False);
                owned=false;runtime.Cheats.NotifyValuesChanged();Assert.That(card.OwnedLabel.text,Is.EqualTo("Not owned"));Assert.That(card.ActionSlot.gameObject.activeSelf,Is.True);
            }
        }
        [UnityTest]public IEnumerator IncompleteCardFallsBackWithoutLosingOptions(){
            yield return Create();using(var lease=runtime.Cheats.Register(new[]{CheatOption.Value("partial","Test","Incomplete",()=>1).WithCard("partial","Incomplete",CheatCardKind.Resource,CheatCardRole.Balance)})){
                yield return Unlock();Assert.That(Object.FindObjectsByType<OptionCard>(FindObjectsSortMode.None),Is.Empty);
                Assert.That(Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Any(c=>c.PropertyName=="Incomplete"),Is.True);
            }
        }
        private sealed class AttributedCardFixture
        {
            private long balance=100;
            private bool owned;
            [ResourceCheat("attribute.coins","Coins",CheatCardRole.Balance)] public long Balance=>balance;
            [ResourceCheat("attribute.coins","Coins",CheatCardRole.Amount)]
            [Dreamy.Debugger.Presentation.NumberRange(1,1000)] public long Amount{get;set;}=25;
            [ResourceCheat("attribute.coins","Coins",CheatCardRole.Action)] public void Add()=>balance+=Amount;
            [OwnershipCheat("attribute.ads","No Ads",CheatCardRole.Owned)] public bool Owned=>owned;
            [OwnershipCheat("attribute.ads","No Ads",CheatCardRole.Action)] public void Grant(){if(!owned)owned=true;}
        }
        [UnityTest]public IEnumerator AttributedCardsRenderConfirmAndRefreshThroughTheSameRegistry(){
            yield return Create();var model=new AttributedCardFixture();
            using(var lease=runtime.Cheats.Register(AttributedCheatOptions.From(model,"attribute"))){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();
                while(tab.IsBuilding)yield return null;
                var cards=Object.FindObjectsByType<OptionCard>(FindObjectsSortMode.None);
                Assert.That(cards.Length,Is.EqualTo(2));
                var resource=cards.Single(c=>c.CardId=="attribute.coins");
                resource.GetComponentInChildren<ActionControl>().Button.onClick.Invoke();yield return null;
                Assert.That(model.Balance,Is.EqualTo(100));
                var prompt=Object.FindFirstObjectByType<ConfirmationPrompt>();Assert.That(prompt.Title.text,Does.Contain("25"));
                prompt.Confirm.onClick.Invoke();yield return null;
                Assert.That(model.Balance,Is.EqualTo(125));
                Assert.That(resource.BalanceSlot.GetComponentInChildren<NumberControl>().NumberSpinner.text,Is.EqualTo("125"));
                var ownership=cards.Single(c=>c.CardId=="attribute.ads");
                ownership.GetComponentInChildren<ActionControl>().Button.onClick.Invoke();yield return null;
                Object.FindFirstObjectByType<ConfirmationPrompt>().Confirm.onClick.Invoke();yield return null;
                Assert.That(model.Owned,Is.True);Assert.That(ownership.ActionSlot.gameObject.activeSelf,Is.False);
            }
        }
        private sealed class NotifyingModel : System.ComponentModel.INotifyPropertyChanged
        {
            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            public int Value{get;private set;}=1;
            [System.ComponentModel.Browsable(false)]public void Change(int value){Value=value;PropertyChanged?.Invoke(this,new System.ComponentModel.PropertyChangedEventArgs(nameof(Value)));}
        }
        [UnityTest]public IEnumerator ConvenienceRegistrationOwnsNotificationsUntilRuntimeShutdown(){
            yield return Create();var model=new NotifyingModel();var lease=runtime.RegisterCheats(model,"notify");
            yield return Unlock();var control=Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Single(c=>c.Option.StableId=="notify.Value");
            model.Change(42);yield return null;yield return null;
            Assert.That(control.NumberSpinner.text,Is.EqualTo("42"));
            var registry=runtime.Cheats;Object.Destroy(root);yield return null;yield return null;
            Assert.That(registry.Snapshot(),Is.Empty);model.Change(99);Assert.That(registry.FlushValueNotifications(),Is.False);
            lease.Dispose();
        }
        [UnityTest]public IEnumerator DynamicChangesKeepUnrelatedControlCardAndPinIdentity(){
            yield return Create();var model=new AttributedCardFixture();string note="";
            using(var textOwner=runtime.Cheats.Register(new[]{CheatOption.Value("dynamic.note","Default","Note",()=>note,value=>note=value)}))
            using(var owner=runtime.RegisterCheats(model,"dynamic")){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
                var card=Object.FindObjectsByType<OptionCard>(FindObjectsSortMode.None).Single(c=>c.CardId=="attribute.coins");
                var amount=card.AmountSlot.GetComponentInChildren<NumberControl>();var definition=amount.Option;
                Service.PinnedUI.Pin(definition);var cardId=card.GetInstanceID();var controlId=amount.GetInstanceID();
                var text=Object.FindObjectsByType<StringControl>(FindObjectsSortMode.None).Single(c=>c.Option.StableId=="dynamic.note");
                text.InputField.text="in progress";text.InputField.ActivateInputField();yield return null;
                text.InputField.caretPosition=3;var textId=text.GetInstanceID();
                using(var extra=runtime.Cheats.Register(new[]{CheatOption.Value("dynamic.extra","Default","Extra",()=>5)})){
                    yield return null;yield return null;while(tab.IsBuilding)yield return null;
                    Assert.That(card.GetInstanceID(),Is.EqualTo(cardId));Assert.That(amount.GetInstanceID(),Is.EqualTo(controlId));
                    Assert.That(amount.Option,Is.SameAs(definition));Assert.That(Service.PinnedUI.HasPinned(definition),Is.True);
                    Assert.That(text.GetInstanceID(),Is.EqualTo(textId));Assert.That(text.InputField.text,Is.EqualTo("in progress"));
                    Assert.That(text.InputField.isFocused,Is.True);Assert.That(text.InputField.caretPosition,Is.EqualTo(3));
                }
                yield return null;yield return null;while(tab.IsBuilding)yield return null;
                Assert.That(card.GetInstanceID(),Is.EqualTo(cardId));Assert.That(Service.PinnedUI.HasPinned(definition),Is.True);
                var values=Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None);
                Assert.That(values.Any(c=>c.Option.StableId=="dynamic.extra"),Is.False);
            }
        }
        [UnityTest]public IEnumerator CompletingAndRemovingACardOnlyRebuildsThatGroup(){
            yield return Create();
            using(var stable=runtime.Cheats.Register(new[]{CheatOption.Value("stable","Same","Unrelated",()=>7)}))
            using(var partial=runtime.Cheats.Register(new[]{CheatOption.Value("parts.balance","Same","Balance",()=>10L).WithCard("parts","Coins",CheatCardKind.Resource,CheatCardRole.Balance),CheatOption.Value("parts.amount","Same","Amount",()=>1L,v=>{}).WithCard("parts","Coins",CheatCardKind.Resource,CheatCardRole.Amount)})){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
                var unrelated=Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Single(c=>c.Option.StableId=="stable");var id=unrelated.GetInstanceID();
                using(var action=runtime.Cheats.Register(new[]{CheatOption.Button("parts.grant","Same","Grant",()=>{},true).WithCard("parts","Coins",CheatCardKind.Resource,CheatCardRole.Action)})){
                    yield return null;yield return null;while(tab.IsBuilding)yield return null;
                    Assert.That(Object.FindObjectsByType<OptionCard>(FindObjectsSortMode.None).Any(c=>c.CardId=="parts"),Is.True);
                    Assert.That(unrelated.GetInstanceID(),Is.EqualTo(id));
                }
                yield return null;yield return null;while(tab.IsBuilding)yield return null;
                Assert.That(Object.FindObjectsByType<OptionCard>(FindObjectsSortMode.None).Any(c=>c.CardId=="parts"),Is.False);
                Assert.That(unrelated.GetInstanceID(),Is.EqualTo(id));
            }
        }
        [UnityTest]public IEnumerator RegistrationBurstPublishesOneStructuralBatchPerTick(){
            yield return Create();yield return Unlock();
            var events=0;System.EventHandler observer=(_,__)=>events++;
            Service.Options.OptionsUpdated+=observer;var leases=new System.Collections.Generic.List<System.IDisposable>();
            try{
                for(var i=0;i<20;i++){var value=i;leases.Add(runtime.Cheats.Register(new[]{CheatOption.Value("burst."+i,"Burst","Value "+i,()=>value)}));}
                Assert.That(events,Is.Zero,"Membership is immediate; presentation reconciliation waits for its owner tick");
                yield return null;yield return null;
                Assert.That(events,Is.EqualTo(1));
                var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
                Assert.That(tab.VisibleOptionCount,Is.GreaterThanOrEqualTo(20));
            }finally{Service.Options.OptionsUpdated-=observer;foreach(var lease in leases)lease.Dispose();}
        }
        [UnityTest]public IEnumerator NarrowDrawerOverlaysWithoutShrinkingContentAndBackKeepsPanel(){
            yield return Create();yield return Unlock();var menu=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.MobileMenuController>();
            var scaler=menu.Content.GetComponentInParent<CanvasScaler>();scaler.scaleFactor=Screen.width/390f;Canvas.ForceUpdateCanvases();yield return null;yield return null;Canvas.ForceUpdateCanvases();
            menu.Close();yield return new WaitForSecondsRealtime(.4f);var before=menu.Content.sizeDelta;menu.Open();yield return new WaitForSecondsRealtime(.4f);
            Assert.That(menu.IsNarrow,Is.True);Assert.That(menu.Scrim.gameObject.activeInHierarchy,Is.True);Assert.That(menu.Content.sizeDelta,Is.EqualTo(before));
            Assert.That(menu.Menu.anchoredPosition.x,Is.EqualTo(menu.Menu.rect.width).Within(3f),"Drawer must finish sliding fully onto the screen");
            var tabCanvas=Object.FindFirstObjectByType<OptionsTabController>().GetComponent<Canvas>();
            Assert.That(menu.Menu.GetComponent<Canvas>().sortingOrder,Is.GreaterThan(menu.Scrim.GetComponent<Canvas>().sortingOrder));
            Assert.That(menu.Scrim.GetComponent<Canvas>().sortingOrder,Is.GreaterThan(tabCanvas.sortingOrder));
            Assert.That(menu.Menu.GetComponent<GraphicRaycaster>(),Is.Not.Null);
            Assert.That(Dreamy.Debugger.Presentation.UI.MobileMenuController.DismissDrawer(),Is.True);Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.True);
        }
        [UnityTest]public IEnumerator LongPinUsesBoundedDotsWithoutEarlySubmission(){
            yield return Create(new string('0',32));runtime.Open(DefaultTabs.Options);yield return null;
            var pin=Object.FindFirstObjectByType<PinEntryControl>();Assert.That(pin.NumberDots.Length,Is.LessThanOrEqualTo(8));
            for(var i=0;i<31;i++)pin.PushNumber(0);Assert.That(runtime.Access.IsUnlocked,Is.False);Assert.That(pin.LengthStatus.text,Is.EqualTo("31 / 32 digits"));
            pin.PushNumber(0);yield return null;Assert.That(runtime.Access.IsUnlocked,Is.True);
        }
        [UnityTest]public IEnumerator PassphraseIgnoresNumericKeypadInput(){
            yield return Create("secret-1234",true);runtime.Open(DefaultTabs.Options);yield return null;
            var pin=Object.FindFirstObjectByType<PinEntryControl>();var old=InputBridge.Text;
            try {InputBridge.Text=()=>"1234";yield return null;Assert.That(pin.NumberDots.All(d=>!d.isOn),Is.True);}
            finally {InputBridge.Text=old;}
            pin.CredentialInput.text="secret-1234";pin.CredentialSubmit.onClick.Invoke();yield return null;Assert.That(runtime.Access.IsUnlocked,Is.True);
        }
        [UnityTest]public IEnumerator ConsoleKeepsSelectionAcrossAppendAndSupportsNarrowBack(){
            yield return Create();yield return Unlock();runtime.Open(DefaultTabs.Console);yield return null;yield return null;
            var service=Service.Console as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;service.Clear();
            Debug.Log("[FAKE UI] first");Debug.Log("[FAKE UI] first");yield return null;yield return null;
            Assert.That(service.Entries.Count,Is.EqualTo(1));var entry=service.Entries[0];Assert.That(entry.Count,Is.EqualTo(2));Assert.That(entry.TimestampUtc,Is.GreaterThan(System.DateTime.MinValue));
            var tab=Object.FindFirstObjectByType<ConsoleTabController>();
            var row=Object.FindFirstObjectByType<ConsoleEntryView>();
            Assert.That(((RectTransform)row.transform).rect.height,Is.GreaterThanOrEqualTo(55));
            Assert.That(row.Message.verticalOverflow,Is.EqualTo(VerticalWrapMode.Truncate));
            tab.ConsoleLogControl.SelectEntry(entry);
            Debug.Log("[FAKE UI] next");yield return null;yield return null;Assert.That(tab.ConsoleLogControl.SelectedEntry,Is.SameAs(entry));
            var scaler=tab.GetComponentInParent<CanvasScaler>();if(scaler==null)scaler=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.MobileMenuController>().Content.GetComponentInParent<CanvasScaler>();
            scaler.scaleFactor=Screen.width/390f;Canvas.ForceUpdateCanvases();yield return null;yield return null;Canvas.ForceUpdateCanvases();
            var toolbar=(RectTransform)tab.transform.Find("Layout/Console toolbar");
            Assert.That(toolbar.rect.height,Is.LessThan(220),"Opening log details must keep the search toolbar compact");
            Assert.That(tab.DetailPane.rect.height,Is.GreaterThan(((RectTransform)tab.transform).rect.height*.55f));
            Assert.That(tab.DetailBack.gameObject.activeInHierarchy,Is.True);tab.DetailBack.onClick.Invoke();Assert.That(tab.IsShowingDetails,Is.False);Assert.That(tab.ListPane.gameObject.activeInHierarchy,Is.True);
            tab.CollapseToggle.isOn=false;Assert.That(service.CollapseEnabled,Is.False);tab.Clear();yield return null;yield return null;Assert.That(tab.ConsoleLogControl.SelectedEntry,Is.Null);
        }
        [UnityTest,Timeout(60000)]public IEnumerator LargeCatalogFiltersWithoutRecreatingControls(){
            yield return Create();yield return Unlock();
            foreach(var count in new[]{20,200,1000}){
                var options=Enumerable.Range(0,count).Select(i=>CheatOption.Value("metric."+i,"Metrics","Metric "+i.ToString("D4"),()=>i)).ToArray();
                var watch=System.Diagnostics.Stopwatch.StartNew();
                using(var lease=runtime.Cheats.Register(options)){
                    watch.Stop();TestContext.WriteLine("Editor structural registration "+count+" options: "+watch.Elapsed.TotalMilliseconds.ToString("0.00")+" ms (not device acceptance)");
                    yield return null;yield return null;
                    var building=Object.FindFirstObjectByType<OptionsTabController>();while(building.IsBuilding)yield return null;
                    var target=Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Metric "+(count-1).ToString("D4"));var id=target.GetInstanceID();
                    var tab=Object.FindFirstObjectByType<OptionsTabController>();tab.SetSearch("Metric "+(count-1).ToString("D4"));Assert.That(tab.VisibleOptionCount,Is.EqualTo(1));Assert.That(target.GetInstanceID(),Is.EqualTo(id));
                    tab.SetSearch("");Assert.That(runtime.Cheats.Snapshot().Count(o=>!o.Id.StartsWith("debugger.")),Is.EqualTo(count));
                }
                yield return null;yield return null;
            }
        }
        [UnityTest,Timeout(60000)]public IEnumerator ChunkedPopulationCancelsOnTabSwitchAndOwnerDisposal(){
            yield return Create();yield return Unlock();bool dead=false;int staleReads=0;
            var options=Enumerable.Range(0,200).Select(i=>CheatOption.Value("temporary."+i,"Temporary","Temporary "+i,()=>{if(dead)staleReads++;return i;})).ToArray();
            var lease=runtime.Cheats.Register(options);var tab=Object.FindFirstObjectByType<OptionsTabController>();
            runtime.Open(DefaultTabs.Console);lease.Dispose();dead=true;yield return null;yield return null;
            runtime.Open(DefaultTabs.Options);yield return null;while(tab.IsBuilding)yield return null;
            Assert.That(staleReads,Is.Zero);Assert.That(Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Any(c=>c.PropertyName.StartsWith("Temporary ")),Is.False);
            Assert.That(runtime.Cheats.Snapshot().All(o=>o.Id.StartsWith("debugger.")),Is.True);
        }
        [UnityTest]public IEnumerator LongDetailsRetainEveryFrameAndCopyWithoutTruncation()
        {
            yield return Create();yield return Unlock();runtime.Open(DefaultTabs.Console);yield return null;yield return null;
            var service=(Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService)Service.Console;service.Clear();
            var message="[FAKE] "+new string('m',3000)+" MESSAGE_END";
            var trace=string.Join("\n",Enumerable.Range(0,800).Select(i=>"Game.Frame"+i+"() at Assets/Game.cs:"+i))+"\n<literal> TRACE_END";
            typeof(Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService).GetMethod("UnityLogCallback",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(service,new object[]{message,trace,LogType.Log});service.Drain();yield return null;yield return null;
            var entry=service.Entries.Last();Assert.That(entry.Message,Is.EqualTo(message));Assert.That(entry.StackTrace,Is.EqualTo(trace));
            var tab=Object.FindFirstObjectByType<ConsoleTabController>();tab.ConsoleLogControl.SelectEntry(entry);yield return null;
            var details=new System.Text.StringBuilder();Assert.That(tab.DetailPager.PageCount,Is.GreaterThan(1));
            for(var i=0;i<tab.DetailPager.PageCount;i++){tab.DetailPager.ShowPage(i);Assert.That(tab.StackTraceText.text.Length,Is.LessThanOrEqualTo(ConsoleDetailPager.PageCharacters));details.Append(tab.StackTraceText.text);}
            Assert.That(details.ToString(),Is.EqualTo(message+System.Environment.NewLine+trace));Assert.That(tab.StackTraceText.text,Does.Contain("TRACE_END"));
            var old=DreamyDebug.CopyConsoleItemCallback;Dreamy.Debugger.Presentation.Services.ConsoleEntry copied=null;
            try{DreamyDebug.CopyConsoleItemCallback=e=>copied=e;tab.CopyToClipboard();Assert.That(copied.StackTrace,Is.EqualTo(trace));}finally{DreamyDebug.CopyConsoleItemCallback=old;}
            tab.CloseDetails();Assert.That(tab.StackTraceText.text,Is.Empty);
        }
        [UnityTest]public IEnumerator FullDetailsStayWithinBudgetAfterClear()
        {
            yield return Create();var service=(Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService)Service.Console;service.Clear();service.CollapseEnabled=false;
            var callback=typeof(Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService).GetMethod("UnityLogCallback",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var trace=new string('x',240000);
            for(var i=0;i<8;i++){callback.Invoke(service,new object[]{"[FAKE] budget "+i,trace,LogType.Log});service.Drain();}
            Assert.That(service.Entries.Sum(e=>(long)(e.Message.Length+e.StackTrace.Length)*2),Is.LessThanOrEqualTo(2*1024*1024));
            Assert.That(service.DroppedCount,Is.GreaterThan(0));Assert.That(service.Entries.Last().StackTrace.Length,Is.EqualTo(trace.Length));
        }
        [UnityTest]public IEnumerator CategoryPinButtonPinsGroupWithoutSelectionWashOrValueChanges()
        {
            yield return Create();var amount=100;var actions=0;
            using(var lease=runtime.Cheats.Register(new[]{CheatOption.Value("pin.demo","Game","Pin demo",()=>amount,v=>amount=v),CheatOption.Button("pin.action","Game","Pin action",()=>actions++)})){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
                Assert.That(tab.PinButton.gameObject.activeInHierarchy,Is.False);
                var group=tab.GetComponentsInChildren<Dreamy.Debugger.Presentation.UI.Other.CategoryGroup>().Single(g=>g.Header.text=="Game");
                Assert.That(group.PinButton.gameObject.activeInHierarchy,Is.True);
                var options=Service.Options.Options.Where(o=>o.Category=="Game").ToArray();
                var background=group.CollapseButton.GetComponent<Image>().color;
                group.PinButton.onClick.Invoke();while(!group.PinButton.interactable)yield return null;
                Assert.That(options.All(Service.PinnedUI.HasPinned),Is.True);
                Assert.That(group.CollapseButton.GetComponent<Image>().color,Is.EqualTo(background));
                Assert.That(group.SelectionToggle.graphic.canvasRenderer.GetAlpha(),Is.Zero);
                Assert.That(tab.GetComponentsInChildren<OptionsControlBase>().All(c=>!c.SelectionModeEnabled && !c.SelectionModeToggle.gameObject.activeInHierarchy),Is.True);
                tab.PinnedOnly.isOn=true;Assert.That(tab.VisibleOptionCount,Is.EqualTo(2));
                group.PinButton.onClick.Invoke();while(!group.PinButton.interactable)yield return null;
                Assert.That(options.Any(Service.PinnedUI.HasPinned),Is.False);Assert.That(tab.VisibleOptionCount,Is.Zero);
                Assert.That(amount,Is.EqualTo(100));Assert.That(actions,Is.Zero);
            }
        }
        [UnityTest]public IEnumerator LeavingCheatsCancelsCategoryPinBatch()
        {
            yield return Create();
            using(var lease=runtime.Cheats.Register(Enumerable.Range(0,100).Select(i=>CheatOption.Value("pin.batch."+i,"Batch","Batch "+i,()=>i)))){
                yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
                var group=tab.GetComponentsInChildren<Dreamy.Debugger.Presentation.UI.Other.CategoryGroup>().Single(g=>g.Header.text=="Batch");
                group.PinButton.onClick.Invoke();runtime.Open(DefaultTabs.Console);yield return null;
                var definitions=Service.Options.Options.Where(o=>o.Category=="Batch").ToArray();
                var count=definitions.Count(Service.PinnedUI.HasPinned);yield return null;yield return null;
                Assert.That(definitions.Count(Service.PinnedUI.HasPinned),Is.EqualTo(count));
                runtime.Open(DefaultTabs.Options);yield return null;Assert.That(group.PinButton.interactable,Is.True);
            }
        }
        [UnityTest]public IEnumerator EmptyStateLivesInsideViewportBelowSearch()
        {
            yield return Create();yield return Unlock();var tab=Object.FindFirstObjectByType<OptionsTabController>();while(tab.IsBuilding)yield return null;
            tab.SetSearch("no-such-option-4e97");yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(tab.NoOptionsNotice.activeInHierarchy,Is.True);
            Assert.That(tab.NoOptionsNotice.transform.parent,Is.SameAs(tab.ContentContainer.parent));
            Assert.That(tab.NoOptionsNotice.GetComponent<Text>().alignment,Is.EqualTo(TextAnchor.MiddleCenter));
            var empty=new Vector3[4];var search=new Vector3[4];((RectTransform)tab.NoOptionsNotice.transform).GetWorldCorners(empty);((RectTransform)tab.SearchField.transform.parent).GetWorldCorners(search);
            Assert.That(empty[1].y,Is.LessThanOrEqualTo(search[0].y));tab.SetSearch("");Assert.That(tab.NoOptionsNotice.activeSelf,Is.False);
        }
        [UnityTest]public IEnumerator GripIgnoresOtherPointersAndCancelsCleanly(){
            yield return Create();runtime.SetHud(HudMode.Compact);yield return null;
            var grip=runtime.Hud.GetComponentInChildren<HudDragHandle>();var a=new PointerEventData(EventSystem.current){pointerId=1,position=grip.transform.position};
            var b=new PointerEventData(EventSystem.current){pointerId=2,position=grip.transform.position};
            grip.OnBeginDrag(a);var position=grip.transform.parent.position;grip.OnBeginDrag(b);b.position+=new Vector2(100,100);grip.OnDrag(b);grip.OnEndDrag(b);
            Assert.That(runtime.Hud.IsDragging,Is.True);Assert.That(grip.transform.parent.position,Is.EqualTo(position));grip.OnCancel(new BaseEventData(EventSystem.current));Assert.That(runtime.Hud.IsDragging,Is.False);
        }
    }
}
#endif
