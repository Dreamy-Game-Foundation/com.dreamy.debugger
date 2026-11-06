using System.ComponentModel;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    public sealed partial class ProjectCheats
    {
        private string note = "QA";
        [Category("QA"), CheatId("qa.note"), DisplayName("Note")]
        public string Note { get => note; set => SetValue(ref note, value); }

        [Category("QA"), CheatId("qa.version"), DisplayName("Version (read only)")]
        public string Version => "demo-1";

        [Category("Save"), CheatId("save.reset"), DisplayName("Reset sample state")]
        [CheatConfirmation]
        public void Reset()
        {
            GodMode = false; Coins = 0; Speed = 1;
            Difficulty = DemoDifficulty.Normal; Note = "QA";
        }
    }
}
