using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    public enum DemoDifficulty { Easy, Normal, Hard }

    /// <summary>Editable partial sample; values are in-memory, never real game saves.</summary>
    public sealed partial class ProjectCheats : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void SetValue<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public IEnumerable<CheatOption> CreateOptions() => AttributedCheatOptions.From(this, "sample");
    }
}
