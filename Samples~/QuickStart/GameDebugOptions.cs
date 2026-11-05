using System;
using System.Collections.Generic;
using System.ComponentModel;
using Dreamy.Debugger.Options;
namespace Dreamy.Debugger.Sample {
    // All GameDebugOptions.* partial files must be imported into the SAME game assembly.
    public partial class GameDebugOptions {
        private readonly Func<long> readCoins;private readonly Action<long> grantCoins;
        public GameDebugOptions(Func<long> readCoins,Action<long> grantCoins){this.readCoins=readCoins;this.grantCoins=grantCoins;}
        public IEnumerable<CheatOption> CreateOptions()=>AttributedCheatOptions.From(this,"game");
    }
}
