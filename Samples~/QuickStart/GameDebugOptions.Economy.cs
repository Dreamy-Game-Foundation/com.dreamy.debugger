using System.ComponentModel;
using Dreamy.Debugger.Presentation;
namespace Dreamy.Debugger.Sample {
    public partial class GameDebugOptions {
        [Category("Economy"),DisplayName("Coin balance"),Sort(0)]public long Coins=>readCoins();
        [Category("Economy"),NumberRange(1,1000000),Increment(100),Sort(1)]public long GrantAmount{get;set;}=100;
        [Category("Economy"),DisplayName("Add Coins"),Sort(2)]public void AddCoins()=>grantCoins(GrantAmount);
        [Browsable(false)]public string HiddenDiagnostic=>"Not rendered";
    }
}
