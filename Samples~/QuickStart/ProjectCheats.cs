using System;
using System.Collections.Generic;
using Dreamy.Debugger.Options;

namespace Dreamy.Debugger.Sample
{
    public enum DemoDifficulty { Easy, Normal, Hard }
    /// <summary>Editable game-side options. These only mutate this sample's in-memory state.</summary>
    public sealed class ProjectCheats
    {
        public bool GodMode { get; private set; }
        public int Coins { get; private set; }
        public float Speed { get; private set; } = 1;
        public DemoDifficulty Difficulty { get; private set; } = DemoDifficulty.Normal;
        public string Note { get; private set; } = "QA";
        public IEnumerable<CheatOption> CreateOptions()
        {
            yield return CheatOption.Value("player.god-mode", "Player", "God mode", () => GodMode, v => GodMode = v);
            yield return CheatOption.Value("economy.coins", "Economy", "Coins", () => Coins, v => Coins = v, 0, 1000000);
            yield return CheatOption.Button("economy.grant", "Economy", "Grant 100 coins", () => Coins = Math.Min(1000000, Coins + 100));
            yield return CheatOption.Value("game.speed", "Gameplay", "Speed", () => Speed, v => Speed = v, .1, 5);
            yield return CheatOption.Value("game.difficulty", "Gameplay", "Difficulty", () => Difficulty, v => Difficulty = v);
            yield return CheatOption.Value("qa.note", "QA", "Note", () => Note, v => Note = v);
            yield return CheatOption.Value("qa.version", "QA", "Version (read only)", () => "demo-1");
            yield return CheatOption.Button("save.reset", "Save", "Reset sample state", Reset, true);
        }
        private void Reset() { GodMode = false; Coins = 0; Speed = 1; Difficulty = DemoDifficulty.Normal; Note = "QA"; }
    }
}
