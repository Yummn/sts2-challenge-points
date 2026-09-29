using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
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
            await ToSignal(GetTree().CreateTimer(12), SceneTreeTimer.SignalName.Timeout);
            if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_INTEGRATION")))
            {
                await RunIntegration();
                return;
            }
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
            ChallengeShopCards.EnsurePools();
            foreach (CardModel card in new CardModel[]
            {
                ModelDb.Card<ChallengeLightVoucher>(), ModelDb.Card<ChallengeSpiritMaker>(),
                ModelDb.Card<ChallengeSpiritPrinter>(), ModelDb.Card<ChallengeMeatCleaver>()
            })
            {
                if (card.Pool is null || !ReferenceEquals(ModelDb.GetById<CardModel>(card.Id), card) ||
                    string.IsNullOrWhiteSpace(card.PortraitPath))
                    throw new InvalidOperationException($"shop card registration failed: {card.Id.Entry}");
            }
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
                if (attached is null || attached.ShopSchemaVersion != 1 || attached.CommonCp != 2 || attached.RoleCp != 8)
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
            var shopIron = (ChallengeContract)canonical.ToMutable();
            shopIron.ShopSchemaVersion = 1;
            shopIron.CharacterRole = "ironclad";
            shopIron.ContractData = "{\"shop:ironclad:squad:SQ-07\":3}";
            var ironPlayer = Player.CreateForNewRun<Ironclad>(UnlockState.all, 2uL);
            var ironRun = RunState.CreateForTest(new[] { ironPlayer }, modifiers: new ModifierModel[] { shopIron });
            shopIron.OnRunCreated(ironRun);
            if (ironPlayer.Deck.Cards.Count(c => c is MegaCrit.Sts2.Core.Models.Cards.IronWave) < 8 ||
                !ironPlayer.Deck.Cards.Any(c => c is ChallengeMeatCleaver) ||
                ironPlayer.Deck.Cards.Any(c => c.Id.Entry.Contains("STRIKE_IRONCLAD") || c.Id.Entry.Contains("DEFEND_IRONCLAD")))
                throw new InvalidOperationException("SQ-07 starter replacement/custom card failed");
            shopIron.MerchantCardPurchases = 3;
            ChallengeContract restoredShop = (ChallengeContract)ModifierModel.FromSerializable(shopIron.ToSerializable());
            if (restoredShop.ShopSchemaVersion != 1 || restoredShop.SquadRank("SQ-07") != 3 ||
                restoredShop.MerchantCardPurchases != 3)
                throw new InvalidOperationException("shop contract save round trip failed");
            var shopSoul = (ChallengeContract)canonical.ToMutable();
            shopSoul.ShopSchemaVersion = 1;
            shopSoul.CharacterRole = "necrobinder";
            shopSoul.ContractData = "{\"shop:necrobinder:squad:SQ-09\":3}";
            var soulPlayer = Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Necrobinder>(UnlockState.all, 3uL);
            var soulRun = RunState.CreateForTest(new[] { soulPlayer }, modifiers: new ModifierModel[] { shopSoul });
            shopSoul.OnRunCreated(soulRun);
            if (soulPlayer.Deck.Cards.SingleOrDefault(c => c is ChallengeSpiritPrinter) is not { IsUpgraded: true })
                throw new InvalidOperationException("SQ-09 spirit printer starter card failed");
            var shopRegent = (ChallengeContract)canonical.ToMutable();
            shopRegent.ShopSchemaVersion = 1;
            shopRegent.CharacterRole = "regent";
            shopRegent.ContractData = "{\"shop:regent:squad:SQ-10\":3}";
            var regentPlayer = Player.CreateForNewRun<MegaCrit.Sts2.Core.Models.Characters.Regent>(UnlockState.all, 4uL);
            var regentRun = RunState.CreateForTest(new[] { regentPlayer }, modifiers: new ModifierModel[] { shopRegent });
            shopRegent.OnRunCreated(regentRun);
            foreach (string id in new[] { "REFINE_BLADE", "BULWARK", "SUMMON_FORTH", "ARMAMENTS" })
                if (!regentPlayer.Deck.Cards.Any(c => c.Id.Entry == id))
                    throw new InvalidOperationException($"SQ-10 starting card unavailable: {id}");
            MainFile.Logger.Info("[ChallengePointsSmoke] PASS: model registration, two independent CP tracks, run-start penalties, save round trip.");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"[ChallengePointsSmoke] FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    private async Task RunIntegration()
    {
        var contract = (ChallengeContract)ModelDb.Modifier<ChallengeContract>().ToMutable();
        contract.CharacterRole = "ironclad";
        contract.ShopSchemaVersion = 1;
        bool choiceTest = System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_INTEGRATION") == "choice";
        contract.ContractData = choiceTest
            ? "{\"shop:ironclad:item:IT-03\":1}"
            : "{\"shop:ironclad:squad:SQ-07\":3}";
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame not ready for PC integration");
        Task<RunState> start = game.StartNewSingleplayerRun(
            ModelDb.Character<Ironclad>(), false, ActModel.GetDefaultList(),
            new ModifierModel[] { contract }, "CHALLENGEPOINTSINTEGRATION", GameMode.Standard);
        if (choiceTest)
        {
            await Task.Delay(18000);
            if (start.IsFaulted) await start;
            if (ChallengeContract.ShopChoiceOpenedCount == 0 || start.IsCompleted)
                throw new InvalidOperationException("IT-03 choice did not remain pending for selection");
            MainFile.Logger.Info("[ChallengePointsIntegration] PASS: IT-03 ability choice opened and awaits selection.");
            GetTree().Quit(0);
            return;
        }
        Task finished = await Task.WhenAny(start, Task.Delay(45000));
        if (!ReferenceEquals(finished, start))
            throw new TimeoutException("PC new-run startup did not complete within 45 seconds");
        RunState run = await start;
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        NRun? scene = NRun.Instance;
        if (run.CurrentRoom is not EventRoom || scene is null ||
            !run.Modifiers.OfType<ChallengeContract>().Any())
            throw new InvalidOperationException($"new-run UI/Neow not ready: room={run.CurrentRoom?.GetType().Name}");
        if (scene.GetNodeOrNull<ChallengeContractHud>("ChallengeContractHud") is null)
            throw new InvalidOperationException("contract HUD missing from PC run");
        string capture = Path.Combine(Path.GetDirectoryName(typeof(MainFile).Assembly.Location)!, "integration-neow.png");
        GetViewport().GetTexture().GetImage().SavePng(capture);
        MainFile.Logger.Info($"[ChallengePointsIntegration] PASS: PC new-run and Neow loaded; capture={capture}");
        GetTree().Quit(0);
    }
}
