using System.ComponentModel;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    public sealed partial class ProjectCheats
    {
        private DemoDifficulty difficulty = DemoDifficulty.Normal;
        private float speed = 1;
        private bool godMode = false;
        [Category("Player"), CheatId("player.god-mode"), DisplayName("God mode")]
        public bool GodMode { get => godMode; set => SetValue(ref godMode, value); }

        [Category("Gameplay"), CheatId("game.speed"), DisplayName("Speed")]
        [Dreamy.Debugger.Presentation.NumberRange(0.1, 5)]
        public float Speed { get => speed; set => SetValue(ref speed, value); }

        [Category("Gameplay"), CheatId("game.difficulty"), DisplayName("Difficulty")]
        public DemoDifficulty Difficulty { get => difficulty; set => SetValue(ref difficulty, value); }
    }
}
