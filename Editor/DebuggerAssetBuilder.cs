using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Foundation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Dreamy.Debugger.Editor {
    public static class DebuggerAssetBuilder {
        public const string Package="Packages/com.dreamy.debugger";
        public static string Root=Package+"/Runtime/Imported/Presentation";
        public static string PanelPath=>Root+"/Assets/DreamyDebugger/UI/Prefabs/DebugPanel.prefab";
        public static string CatalogPath=>PrefabsRoot+"/DebuggerAssets.asset";
        public static string HudPath=>PrefabsRoot+"/PerformanceHud.prefab";
        public static string ConfirmPath=>PrefabsRoot+"/ConfirmationPrompt.prefab";
        public static string BootstrapPath=>PrefabsRoot+"/DebuggerBootstrap.prefab";
        public static string DemoFolder {
            get {
                const string existing="Assets/Samples/Dreamy Debugger/0.1.0-preview.1/Production";
                if(Directory.Exists(existing))return existing;
                var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(Package+"/package.json");
                return "Assets/Samples/Dreamy Debugger/"+(package?.version??Dreamy.Debugger.Presentation.VersionInfo.Version)+"/Production";
            }
        }
        private static string PrefabsRoot=Package+"/Runtime/Prefabs";
        private static Color Accent=DebuggerPalette.Accent, Background=DebuggerPalette.Background, Surface=DebuggerPalette.Surface;
        private static Font Font=>Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace((char)92,'/');Folder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        public static string BuildPackageAssets(){
            var originalRoot=Root;var originalPrefabs=PrefabsRoot;
            const string staging="Assets/__DreamyDebuggerAuthoring";const string generated="Assets/__DreamyDebuggerGenerated";
            if(AssetDatabase.IsValidFolder(staging)||AssetDatabase.IsValidFolder(generated))throw new Exception("Authoring staging already exists; inspect before migration.");
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            try{
                var error=AssetDatabase.MoveAsset(Package+"/Runtime/Imported",staging);if(!string.IsNullOrEmpty(error))throw new Exception(error);
                error=AssetDatabase.MoveAsset(originalPrefabs,generated);if(!string.IsNullOrEmpty(error))throw new Exception(error);
                Root=staging+"/Presentation";PrefabsRoot=generated;
                BuildAssetsCore();
            }finally{
                Root=originalRoot;PrefabsRoot=originalPrefabs;
                if(AssetDatabase.IsValidFolder(generated))AssetDatabase.MoveAsset(generated,originalPrefabs);
                if(AssetDatabase.IsValidFolder(staging))AssetDatabase.MoveAsset(staging,Package+"/Runtime/Imported");
                AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies();
            }
            AssetDatabase.ImportAsset(Package+"/Runtime",ImportAssetOptions.ImportRecursive|ImportAssetOptions.ForceUpdate);
            return ValidatePrefabs();
        }
        private static string BuildAssetsCore(){
            Folder(PrefabsRoot);var entries=new List<CatalogEntry>();
            foreach(var source in new[]{Root+"/Resources",Root+"/usr/Resources"}){
                var destination=source.Substring(0,source.Length-"Resources".Length)+"Assets";
                if(AssetDatabase.IsValidFolder(source)){var error=AssetDatabase.MoveAsset(source,destination);if(!string.IsNullOrEmpty(error))throw new Exception(error);}
                foreach(var guid in AssetDatabase.FindAssets("",new[]{destination})){
                    var path=AssetDatabase.GUIDToAssetPath(guid);if(AssetDatabase.IsValidFolder(path))continue;
                    var key=path.Substring(destination.Length+1);key=key.Substring(0,key.LastIndexOf('.'));
                    entries.Add(new CatalogEntry{Key=key,Asset=AssetDatabase.LoadMainAssetAtPath(path)});
                }
            }
            var settings=entries.First(e=>e.Asset is Settings).Asset as Settings;
            var so=new SerializedObject(settings);so.FindProperty("_entryCode").arraySize=4;for(var n=0;n<4;n++)so.FindProperty("_entryCode").GetArrayElementAtIndex(n).intValue=0;
            so.FindProperty("_requireEntryCode").boolValue=true;so.FindProperty("_requiredTapCount").intValue=3;so.FindProperty("_triggerPosition").intValue=0;so.FindProperty("_triggerBehaviour").intValue=3;
            so.FindProperty("_enableBugReporter").boolValue=true;so.FindProperty("_enableBugReportScreenshot").boolValue=false;
            var welcome=so.FindProperty("_disableWelcomePopup");if(welcome!=null)welcome.boolValue=true;
            so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(settings);
            RestylePrefabs();RestyleSheets();DebuggerRedesignAssets.StylePrefabs(Root);MigrateAnimations();BuildHud();DebuggerRedesignAssets.BuildHud(HudPath);BuildConfirmation();
            entries.Add(new CatalogEntry{Key="DreamyDebugger/PerformanceHud",Asset=AssetDatabase.LoadAssetAtPath<GameObject>(HudPath)});
            entries.Add(new CatalogEntry{Key="DreamyDebugger/ConfirmationPrompt",Asset=AssetDatabase.LoadAssetAtPath<GameObject>(ConfirmPath)});
            var catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(CatalogPath);
            if(catalog==null){catalog=ScriptableObject.CreateInstance<DebuggerAssetCatalog>();AssetDatabase.CreateAsset(catalog,CatalogPath);}
            catalog.Entries=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            DebuggerInterfaceAssets.Install();
            ScreenshotTabAssets.Install();
            var bootstrap=new GameObject("Dreamy Debugger");try{var runtime=bootstrap.AddComponent<DebuggerRuntime>();runtime.Catalog=catalog;PrefabUtility.SaveAsPrefabAsset(bootstrap,BootstrapPath);}finally{UnityEngine.Object.DestroyImmediate(bootstrap);}
            return ValidatePrefabs();
        }
        private static void MigrateAnimations(){
            foreach(var guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{Root})){
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var original in AnimationUtility.GetCurveBindings(clip)){
                    var changed=original;changed.path=original.path.Replace("SR__","").Replace("SR_","");if(changed.path==original.path)continue;
                    var curve=AnimationUtility.GetEditorCurve(clip,original);AnimationUtility.SetEditorCurve(clip,original,null);AnimationUtility.SetEditorCurve(clip,changed,curve);
                }EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);
            }
        }
        private static void RestyleSheets()=>DebuggerRedesignAssets.StyleSheets(Root);
        private static void RestylePrefabs(){foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root})){
            var path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
            try{
                foreach(var old in root.GetComponentsInChildren<Dreamy.Debugger.Presentation.UI.Other.ScrollRectPatch>(true))UnityEngine.Object.DestroyImmediate(old);
                foreach(var t in root.GetComponentsInChildren<Transform>(true)){
                    if(t.name.EndsWith("ScrollView") && path.EndsWith("BugReportPopover.prefab"))GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    t.name=t.name.Replace("SR__","").Replace("SR_","").Replace("SRDebugger","DreamyDebugger");
                }
                foreach(var text in root.GetComponentsInChildren<Text>(true)){text.text=text.text.Replace("SRDebugger","Dreamy Debugger").Replace("Stompy Robot","Dreamy").Replace("Other","Frame time");text.raycastTarget=false;}
                foreach(var image in root.GetComponentsInChildren<Image>(true))if(image.gameObject.name.Contains("Logo") || image.GetComponent<StyleComponent>()!=null && image.GetComponent<StyleComponent>().StyleKey=="Icon_Stompy"){
                    var layout=image.GetComponent<LayoutElement>();if(layout==null)layout=image.gameObject.AddComponent<LayoutElement>();layout.minWidth=130;layout.preferredWidth=160;layout.minHeight=44;layout.preferredHeight=48;layout.flexibleWidth=1;
                    image.enabled=false;image.sprite=null;var style=image.GetComponent<StyleComponent>();if(style!=null)style.enabled=false;
                    if(image.transform.Find("DreamyBrand")==null){var label=Text("DreamyBrand",image.transform,"DREAMY\nDEBUGGER",16,Accent);label.alignment=TextAnchor.MiddleCenter;Stretch(label.rectTransform);}
                    else {var label=image.transform.Find("DreamyBrand").GetComponent<Text>();label.fontSize=image.gameObject.name.Contains("Logo")?16:26;label.text=image.gameObject.name.Contains("Logo")?"DREAMY\nDEBUGGER":"D";}
                }
                if(path.EndsWith("PinEntry.prefab")){
                    var control=root.GetComponentInChildren<PinEntryControl>(true);if(control!=null && control.CredentialInput==null){
                        var parent=control.NumberButtons[0].transform.parent;var inputObject=new GameObject("Passphrase",typeof(RectTransform),typeof(Image),typeof(InputField));inputObject.transform.SetParent(parent,false);
                        var rect=(RectTransform)inputObject.transform;rect.anchorMin=new Vector2(.05f,.4f);rect.anchorMax=new Vector2(.95f,.6f);rect.offsetMin=rect.offsetMax=Vector2.zero;inputObject.GetComponent<Image>().color=Surface;
                        var label=Text("Value",inputObject.transform,"",24,Color.white);Stretch(label.rectTransform);
                        var input=inputObject.GetComponent<InputField>();input.textComponent=label;input.contentType=InputField.ContentType.Password;input.characterLimit=64;control.CredentialInput=input;
                        var button=Button("Unlock",parent,"UNLOCK");var br=(RectTransform)button.transform;br.anchorMin=new Vector2(.15f,.2f);br.anchorMax=new Vector2(.85f,.35f);br.offsetMin=br.offsetMax=Vector2.zero;control.CredentialSubmit=button;
                        inputObject.SetActive(false);button.gameObject.SetActive(false);
                    }
                }
                foreach(var canvas in root.GetComponentsInChildren<Canvas>(true)){
                    if(canvas.GetComponentInChildren<SafeArea>(true)==null && canvas.GetComponentInChildren<Dreamy.Debugger.Presentation.UI.Other.SafeAreaSizer>(true)==null){var safe=new GameObject("SafeArea",typeof(RectTransform),typeof(SafeArea));safe.transform.SetParent(canvas.transform,false);Stretch((RectTransform)safe.transform);}
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }}
        private static GameObject CanvasRoot(string name){var root=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1080,1920);scaler.matchWidthOrHeight=.5f;return root;}
        private static Transform Safe(GameObject root){var go=new GameObject("SafeArea",typeof(RectTransform),typeof(SafeArea));go.transform.SetParent(root.transform,false);Stretch((RectTransform)go.transform);return go.transform;}
        private static void Stretch(RectTransform t){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.offsetMin=t.offsetMax=Vector2.zero;}
        private static Text Text(string name,Transform parent,string value,int size,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var t=go.GetComponent<Text>();t.font=Font;t.text=value;t.fontSize=size;t.color=color;t.raycastTarget=false;t.alignment=TextAnchor.MiddleLeft;return t;}
        private static Button Button(string name,Transform parent,string label){var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);go.GetComponent<Image>().color=Accent;var button=go.GetComponent<Button>();var t=Text("Label",go.transform,label,22,Background);t.alignment=TextAnchor.MiddleCenter;Stretch(t.rectTransform);return button;}
        private static void BuildHud(){var root=CanvasRoot("Dreamy Performance HUD");try{
            var safe=Safe(root);var card=new GameObject("Metric card",typeof(RectTransform),typeof(Image));card.transform.SetParent(safe,false);var rect=(RectTransform)card.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-20,-20);rect.sizeDelta=new Vector2(380,180);var bg=card.GetComponent<Image>();bg.color=new Color(.025f,.04f,.07f,.92f);bg.raycastTarget=false;
            var text=Text("Metrics",card.transform,"60 FPS  •  16.7 ms\n120 MB allocated",26,Accent);text.rectTransform.anchorMin=new Vector2(0,.45f);text.rectTransform.anchorMax=Vector2.one;text.rectTransform.offsetMin=new Vector2(18,0);text.rectTransform.offsetMax=new Vector2(-12,-8);
            var graphObject=new GameObject("Frame history",typeof(RectTransform),typeof(CanvasRenderer),typeof(Dreamy.Debugger.Presentation.UI.Controls.ProfilerGraphControl));graphObject.transform.SetParent(card.transform,false);var graph=graphObject.GetComponent<Dreamy.Debugger.Presentation.UI.Controls.ProfilerGraphControl>();graph.raycastTarget=false;graph.LineColours=new[]{DebuggerPalette.Ivory,DebuggerPalette.Warning,DebuggerPalette.Accent};var gr=(RectTransform)graphObject.transform;gr.anchorMin=Vector2.zero;gr.anchorMax=new Vector2(1,.45f);gr.offsetMin=new Vector2(14,12);gr.offsetMax=new Vector2(-14,0);
            var hud=root.AddComponent<PerformanceHud>();var so=new SerializedObject(hud);so.FindProperty("label").objectReferenceValue=text;so.FindProperty("graph").objectReferenceValue=graph;so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(root,HudPath);
        }finally{UnityEngine.Object.DestroyImmediate(root);}}
        private static void BuildConfirmation(){var root=CanvasRoot("Confirm debugger action");try{
            root.AddComponent<GraphicRaycaster>();var safe=Safe(root);var go=new GameObject("Dialog",typeof(RectTransform),typeof(Image));go.transform.SetParent(safe,false);var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(680,300);go.GetComponent<Image>().color=Surface;
            var title=Text("Title",go.transform,"Confirm action",28,Color.white);title.rectTransform.anchorMin=new Vector2(.06f,.5f);title.rectTransform.anchorMax=new Vector2(.94f,.92f);title.rectTransform.offsetMin=title.rectTransform.offsetMax=Vector2.zero;
            var confirm=Button("Confirm",go.transform,"CONFIRM");var cr=(RectTransform)confirm.transform;cr.anchorMin=new Vector2(.52f,.12f);cr.anchorMax=new Vector2(.94f,.4f);cr.offsetMin=cr.offsetMax=Vector2.zero;
            var cancel=Button("Cancel",go.transform,"CANCEL");var ar=(RectTransform)cancel.transform;ar.anchorMin=new Vector2(.06f,.12f);ar.anchorMax=new Vector2(.48f,.4f);ar.offsetMin=ar.offsetMax=Vector2.zero;cancel.GetComponent<Image>().color=new Color(.35f,.4f,.47f);
            var prompt=root.AddComponent<ConfirmationPrompt>();prompt.Title=title;prompt.Confirm=confirm;prompt.Cancel=cancel;PrefabUtility.SaveAsPrefabAsset(root,ConfirmPath);
        }finally{UnityEngine.Object.DestroyImmediate(root);}}
        public static string ValidatePrefabs(){var bad=new List<string>();var count=0;foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Package+"/Runtime"})){count++;var path=AssetDatabase.GUIDToAssetPath(guid);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab.GetComponentsInChildren<Component>(true).Any(c=>c==null))bad.Add(path);}if(bad.Count>0)throw new Exception("Missing scripts: "+string.Join(", ",bad));return count+" prefabs • no missing scripts";}
        public static DebuggerRuntime AddBootstrapToActiveScene(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before adding a scene bootstrap.");
            if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Close Prefab Mode and select a startup scene first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(!scene.IsValid() || !scene.isLoaded)throw new InvalidOperationException("Open a startup scene first.");
            var existing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DebuggerRuntime>(true)).FirstOrDefault();
            if(existing!=null){Selection.activeGameObject=existing.gameObject;return existing;}
            var path=EnsureProjectBootstrap();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),scene);
            Undo.RegisterCreatedObjectUndo(root,"Add Dreamy Debugger bootstrap");
            EditorSceneManager.MarkSceneDirty(scene);Selection.activeGameObject=root;
            return root.GetComponent<DebuggerRuntime>();
        }
        private static string EnsureProjectBootstrap(){
            Folder(DemoFolder);
            var configPath=DemoFolder+"/DebuggerConfiguration.asset";var config=AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(configPath);if(config==null){config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.InitialHud=HudMode.Off;AssetDatabase.CreateAsset(config,configPath);}
            var prefabPath=DemoFolder+"/DebuggerBootstrap.prefab";
            if(!File.Exists(prefabPath)){var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BootstrapPath));try{root.GetComponent<DebuggerRuntime>().ConfigurationAsset=config;var sampleType=Type.GetType("Dreamy.Debugger.Sample.ProjectCheatHost, Dreamy.Debugger.Sample");if(sampleType!=null)root.AddComponent(sampleType);PrefabUtility.SaveAsPrefabAsset(root,prefabPath);}finally{UnityEngine.Object.DestroyImmediate(root);}}
            return prefabPath;
        }
        public static void InstallSample(){
            var prefabPath=EnsureProjectBootstrap();
            var scenePath=DemoFolder+"/DebuggerDemo.unity";
            if(!File.Exists(scenePath)){var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var replaceEmpty=string.IsNullOrEmpty(previous.path) && !previous.isDirty;var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,replaceEmpty?NewSceneMode.Single:NewSceneMode.Additive);try{
                var camera=new GameObject("Main Camera",typeof(Camera));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera,scene);camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=Background;camera.tag="MainCamera";
                var light=new GameObject("Directional Light",typeof(Light));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light,scene);light.GetComponent<Light>().type=LightType.Directional;
                var bootstrap=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),scene);
                var canvas=CanvasRoot("Demo welcome");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas,scene);canvas.GetComponent<Canvas>().sortingOrder=0;
                var safe=Safe(canvas);var title=Text("Title",safe,"DREAMY\nDEBUGGER",88,Color.white);title.rectTransform.anchorMin=new Vector2(.1f,.54f);title.rectTransform.anchorMax=new Vector2(.9f,.8f);title.rectTransform.offsetMin=title.rectTransform.offsetMax=Vector2.zero;
                var subtitle=Text("Subtitle",safe,"INTERNAL DIAGNOSTICS / QA SANDBOX\n\nTap the top-left corner 5 times\nand enter 0000 to open the overlay.",25,Accent);subtitle.rectTransform.anchorMin=new Vector2(.1f,.24f);subtitle.rectTransform.anchorMax=new Vector2(.9f,.52f);subtitle.rectTransform.offsetMin=subtitle.rectTransform.offsetMax=Vector2.zero;
                EditorSceneManager.SaveScene(scene,scenePath);
            }finally{if(!replaceEmpty){EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}}}
            Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
        }
    }
}
