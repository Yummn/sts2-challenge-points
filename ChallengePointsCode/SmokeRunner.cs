using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Unlocks;
using System.Reflection;

namespace ChallengePoints;

internal sealed partial class SmokeRunner : Node
{
    public override void _Ready() => _ = Run();

    private async Task Run()
    {
        try
        {
            await ToSignal(GetTree().CreateTimer(5), SceneTreeTimer.SignalName.Timeout);
            if (ChallengeArt.Load("contract_panel.png") is null || ChallengeArt.MalleableIcon is null
                || ChallengeArt.AmbergrisIcon is null)
                throw new InvalidOperationException("embedded hand-painted art failed to load");
            var ui = new ChallengeUi(null!);
            GetTree().Root.AddChild(ui);
            if (ui.GetChildCount() < 2)
                throw new InvalidOperationException("challenge UI did not create its controls");
            if (System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_VISUAL_TEST") == "1")
            {
                ui.OpenForSmokeTest();
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                string capture = Path.Combine(Path.GetDirectoryName(typeof(MainFile).Assembly.Location)!, "smoke-ui.png");
                GetViewport().GetTexture().GetImage().SavePng(capture);
                MainFile.Logger.Info($"[ChallengePointsSmoke] UI captured: {capture}");
            }
            ui.QueueFree();
            ChallengeContract canonical = ModelDb.Modifier<ChallengeContract>();
#if !STS2_V110
            var ambergris = ModelDb.Potion<ChallengeAmbergris>();
            if (ambergris.Image is null || ambergris.Pool.GetType().Name != "EventPotionPool")
                throw new InvalidOperationException("PC v107.1 Ambergris compatibility potion was not registered");
#endif
            if (ModelDb.Power<ChallengeMalleablePower>().Icon is null || ModelDb.Power<ChallengeMalleablePower>().BigIcon is null)
                throw new InvalidOperationException("custom challenge power icon was not installed");
            var selection = (Dictionary<string, int>)typeof(ChallengeSelection)
                .GetField("Ranks", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            var originalSelection = new Dictionary<string, int>(selection);
            try
            {
                selection.Clear();
                selection["G-02"] = 1;
                selection["IC-02"] = 1;
                var selectedPlayer = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL);
                var selectedRun = RunState.CreateForNewRun(new[] { selectedPlayer }, ActModel.GetDefaultList().Select(a => a.ToMutable()).ToArray(),
                    Array.Empty<ModifierModel>(), GameMode.Standard, 0, "CHALLENGEPOINTS_SMOKE");
                var attached = selectedRun.Modifiers.OfType<ChallengeContract>().SingleOrDefault();
                if (attached is null || attached.CommonCp != 1 || attached.RoleCp != 8)
                    throw new InvalidOperationException("new-run patch did not attach the selected contract");
            }
            finally
            {
                selection.Clear();
                foreach (var pair in originalSelection) selection[pair.Key] = pair.Value;
            }
            MainFile.Logger.Info($"[ChallengePointsSmoke] reward audit: {ChallengeContract.AuditRewardNames()}");
            string dump = Path.Combine(Path.GetDirectoryName(typeof(MainFile).Assembly.Location)!, "smoke-cards.tsv");
            File.WriteAllLines(dump, ModelDb.AllCards.Select(x => $"{x.Id.Entry}\t{x.Title}"));
            File.WriteAllLines(Path.Combine(Path.GetDirectoryName(dump)!, "smoke-potions.tsv"), ModelDb.AllPotions.Select(x => $"{x.Id.Entry}\t{x.Title.GetFormattedText()}"));
            MainFile.Logger.Info("[ChallengePointsSmoke] relics=" + string.Join(" | ", ModelDb.AllRelics.Where(x => x.Title.GetFormattedText().Contains("护喉") || x.Title.GetFormattedText().Contains("扭蛋")).Select(x => $"{x.Id.Entry}:{x.Title.GetFormattedText()}")));
            MainFile.Logger.Info("[ChallengePointsSmoke] potions=" + string.Join(" | ", ModelDb.AllPotions.Where(x => x.Title.GetFormattedText().Contains("龙涎")).Select(x => $"{x.Id.Entry}:{x.Title.GetFormattedText()}")));
            ChallengeContract contract = (ChallengeContract)canonical.ToMutable();
            contract.CharacterRole = "ironclad";
            contract.ContractData = "{\"G-02\":2,\"G-03\":1,\"G-06\":2,\"IC-02\":1}";
            if (contract.CommonCp != 9 || contract.RoleCp != 8)
                throw new InvalidOperationException($"scores {contract.CommonCp}/{contract.RoleCp}, expected 9/8");
            Player player = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL);
            int hp = player.Creature.MaxHp;
            int gold = player.Gold;
            var run = RunState.CreateForTest(new[] { player }, modifiers: new ModifierModel[] { contract });
            contract.OnRunCreated(run);
            if (player.Creature.MaxHp != hp - 6 || player.Gold != Math.Max(0, gold - 25))
                throw new InvalidOperationException($"run start HP/gold {player.Creature.MaxHp}/{player.Gold}, expected {hp-6}/{Math.Max(0,gold-25)}");
            var saved = contract.ToSerializable();
            MainFile.Logger.Info($"[ChallengePointsSmoke] savedProps={saved.Props?.strings?.Count ?? -1}");
            var restored = (ChallengeContract)ModifierModel.FromSerializable(saved);
            MainFile.Logger.Info($"[ChallengePointsSmoke] save state: source={contract.ContractData}, restored={restored.ContractData}, role={restored.CharacterRole}, scores={restored.CommonCp}/{restored.RoleCp}");
            if (restored.CommonCp != 9 || restored.RoleCp != 8)
                throw new InvalidOperationException("saved challenge points did not round-trip");
            var monsterContract = (ChallengeContract)canonical.ToMutable();
            monsterContract.ContractData = "{\"G-01\":3}";
            var monsterRun = RunState.CreateForTest(new[] { Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL) }, modifiers: new ModifierModel[] { monsterContract });
            monsterContract.OnRunCreated(monsterRun);
            var monster = new Creature(ModelDb.Monster<SpinyToad>().ToMutable(), CombatSide.Enemy, null);
            int originalEnemyHp = monster.MaxHp;
            await monsterContract.AfterCreatureAddedToCombat(monster);
            if (monster.MaxHp != (int)Math.Ceiling(originalEnemyHp * 1.3m))
                throw new InvalidOperationException($"enemy HP hook {monster.MaxHp}, expected {(int)Math.Ceiling(originalEnemyHp * 1.3m)}");
            var richRanks = ChallengeCatalog.All.Where(x => x.Role == "common")
                .ToDictionary(x => x.Id, x => x.MaxRank);
            richRanks["IC-02"] = richRanks["IC-03"] = richRanks["IC-04"] = 1;
            var rewardContract = (ChallengeContract)canonical.ToMutable();
            rewardContract.ContractData = System.Text.Json.JsonSerializer.Serialize(richRanks);
            rewardContract.CharacterRole = "ironclad";
            var rewardPlayer = Player.CreateForNewRun<Ironclad>(UnlockState.all, 1uL);
            var rewardRun = RunState.CreateForTest(new[] { rewardPlayer }, modifiers: new ModifierModel[] { rewardContract });
            rewardContract.OnRunCreated(rewardRun);
            var twin = rewardPlayer.Deck.Cards.SingleOrDefault(x => x.Id.Entry == "TWIN_STRIKE");
            var shrug = rewardPlayer.Deck.Cards.SingleOrDefault(x => x.Id.Entry == "SHRUG_IT_OFF");
            if (rewardContract.CommonCp < 70 || rewardContract.RoleCp < 20 || twin is null || shrug is null
                || !twin.IsUpgraded || !shrug.IsUpgraded)
                throw new InvalidOperationException("20/70-CP starting deck transformation or upgrade failed");
            MainFile.Logger.Info("[ChallengePointsSmoke] PASS: model registration, two independent CP tracks, run-start penalties, save round trip.");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[ChallengePointsSmoke] FAIL: {ex}");
            GetTree().Quit(1);
        }
    }
}
