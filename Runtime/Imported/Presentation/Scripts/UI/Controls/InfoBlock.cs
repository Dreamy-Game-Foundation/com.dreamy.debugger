namespace Dreamy.Debugger.Presentation.UI.Controls
{
    using Dreamy.Debugger.Foundation;
    using UnityEngine.UI;

    public class InfoBlock : ComponentBehaviour
    {
        public UnityEngine.RectTransform Rows;
        public InfoRow RowPrefab;
        public Button CopyButton;
        private readonly System.Collections.Generic.Dictionary<string,InfoRow> rows = new System.Collections.Generic.Dictionary<string,InfoRow>();
        private string copyText;
        protected override void Start(){base.Start();if(CopyButton!=null)CopyButton.onClick.AddListener(()=>UnityEngine.GUIUtility.systemCopyBuffer=copyText??"");}
        public void SetRows(System.Collections.Generic.IList<Dreamy.Debugger.Presentation.InfoEntry> entries){
            Content.gameObject.SetActive(false);var text=new System.Text.StringBuilder();
            foreach(var entry in entries){
                if(!rows.TryGetValue(entry.Title,out var row)){row=UnityEngine.Object.Instantiate(RowPrefab,Rows,false);row.gameObject.SetActive(true);rows.Add(entry.Title,row);}
                string value;try{var raw=entry.Value;value=raw is bool flag?(flag?"Yes":"No"):System.Convert.ToString(raw,System.Globalization.CultureInfo.InvariantCulture);}catch(System.Exception){value="Unavailable";}
                if(value!=null && value.Length>4096)value=value.Substring(0,4096)+"…";
                row.Key.text=entry.Title;row.Value.text=value??"—";text.Append(entry.Title).Append(": ").AppendLine(value);
            }
            copyText=text.ToString();
        }
        [RequiredField] public Text Content;

        [RequiredField] public Text Title;
    }
}
