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
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;
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
            var unlimited = (ChallengeContract)canonical.ToMutable();
            unlimited.ShopSchemaVersion = 1;
            unlimited.CharacterRole = "ironclad";
            unlimited.ContractData = "{\"shop:ironclad:squad:SQ-07\":4,\"shop:ironclad:item:IT-08\":1}";
            var unlimitedPlayer = Player.CreateForNewRun<Ironclad>(UnlockState.all, 5uL);
            var unlimitedRun = RunState.CreateForTest(new[] { unlimitedPlayer }, modifiers: new ModifierModel[] { unlimited });
            unlimited.OnRunCreated(unlimitedRun);
            var wave = unlimitedPlayer.Deck.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.IronWave>().First();
            if (wave.CurrentUpgradeLevel != 1 || wave.MaxUpgradeLevel != int.MaxValue)
                throw new InvalidOperationException("SQ-07/IT-08 upgrade synergy did not initialize");
            var defeated = new Creature(ModelDb.Monster<SpinyToad>().ToMutable(), CombatSide.Enemy, null);
            await unlimited.AfterDeath(new ThrowingPlayerChoiceContext(), defeated, false, 0);
            await unlimited.AfterDeath(new ThrowingPlayerChoiceContext(), defeated, false, 0);
            if (wave.CurrentUpgradeLevel != 3)
                throw new InvalidOperationException($"SQ-07 kill upgrades expected 3, got {wave.CurrentUpgradeLevel}");
            CardModel reloadedWave = CardModel.FromSerializable(wave.ToSerializable());
            if (reloadedWave.CurrentUpgradeLevel != 3)
                throw new InvalidOperationException("SQ-07 upgraded Iron Wave could not round-trip through card save");
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
        string mode = System.Environment.GetEnvironmentVariable("CHALLENGE_POINTS_INTEGRATION") ?? "";
        var contract = (ChallengeContract)ModelDb.Modifier<ChallengeContract>().ToMutable();
        contract.CharacterRole = mode switch
        {
            "soul" or "spirit" => "necrobinder",
            "status" => "defect",
            _ => "ironclad"
        };
        contract.ShopSchemaVersion = 1;
        bool choiceTest = mode == "choice";
        bool battleTest = mode is "battle" or "light" or "soul" or "spirit" or "status";
        contract.ContractData = mode switch
        {
            "choice" => "{\"shop:ironclad:item:IT-03\":1}",
            "battle" => "{\"shop:ironclad:squad:SQ-07\":4,\"shop:ironclad:item:IT-08\":1}",
            "light" => "{\"shop:ironclad:squad:SQ-02\":2}",
            "soul" => "{\"shop:necrobinder:squad:SQ-03\":4}",
            "spirit" => "{\"shop:necrobinder:squad:SQ-09\":1}",
            "status" => "{\"shop:defect:squad:SQ-05\":3}",
            _ => "{\"shop:ironclad:squad:SQ-07\":3}"
        };
        NGame game = NGame.Instance ?? throw new InvalidOperationException("NGame not ready for PC integration");
        CharacterModel character = mode switch
        {
            "soul" or "spirit" => ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Necrobinder>(),
            "status" => ModelDb.Character<MegaCrit.Sts2.Core.Models.Characters.Defect>(),
            _ => ModelDb.Character<Ironclad>()
        };
        Task<RunState> start = game.StartNewSingleplayerRun(
            character, false, ActModel.GetDefaultList(),
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
        if (battleTest)
        {
            var console = new DevConsole(shouldAllowDebugCommands: true);
            var fight = console.ProcessCommand("fight SPINY_TOAD_NORMAL");
            if (!fight.success) throw new InvalidOperationException($"PC fight command failed: {fight.msg}");
            for (int i = 0; i < 200 && !CombatManager.Instance.IsInProgress; i++)
                await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
            if (!CombatManager.Instance.IsInProgress) throw new TimeoutException("PC combat did not start");
            Player player = run.Players.Single();
            ICombatState combat = player.Creature.CombatState ?? throw new InvalidOperationException("player combat state missing");
            if (mode == "light")
            {
                for (int i = 0; i < 100 && !PileType.Hand.GetPile(player).Cards.Any(c => c is ChallengeLightVoucher); i++)
                    await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
                CardModel voucher = PileType.Hand.GetPile(player).Cards.First(c => c is ChallengeLightVoucher);
                if (!ReferenceEquals(voucher.Owner, player)) throw new InvalidOperationException("SQ-02 voucher has no owner");
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), voucher, null).WaitAsync(TimeSpan.FromSeconds(20));
                if (voucher.Pile?.Type == PileType.Hand) throw new InvalidOperationException("SQ-02 voucher did not play");
                MainFile.Logger.Info("[ChallengePointsIntegration] PASS: SQ-02 generated voucher played in PC combat.");
                GetTree().Quit(0);
                return;
            }
            if (mode == "soul")
            {
                await contract.BeforeHandDraw(player, new ThrowingPlayerChoiceContext(), combat).WaitAsync(TimeSpan.FromSeconds(20));
                CardModel[] soulCards = new[] { PileType.Draw, PileType.Hand, PileType.Discard }
                    .SelectMany(p => p.GetPile(player).Cards).Where(c => c is MegaCrit.Sts2.Core.Models.Cards.Soul
                        || c.Id.Entry.Contains("WISP", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (soulCards.Length < 2 || soulCards.Any(c => !ReferenceEquals(c.Owner, player)))
                    throw new InvalidOperationException("SQ-03 soul/wisp generation or owner binding failed");
                MainFile.Logger.Info("[ChallengePointsIntegration] PASS: SQ-03 generated soul/wisp in PC combat.");
                GetTree().Quit(0);
                return;
            }
            if (mode == "status")
            {
                CardModel wound = combat.CreateCard(ModelDb.Card<MegaCrit.Sts2.Core.Models.Cards.Wound>(), player);
                await CardPileCmd.AddGeneratedCardToCombat(wound, PileType.Hand, player).WaitAsync(TimeSpan.FromSeconds(20));
                if (contract.ShopStatusesThisTurn < 1 || !PileType.Hand.GetPile(player).Cards.Any(c => c.Id.Entry == "FUEL"))
                    throw new InvalidOperationException("SQ-05 status-generated fuel did not appear");
                MainFile.Logger.Info("[ChallengePointsIntegration] PASS: SQ-05 generated fuel after status in PC combat.");
                GetTree().Quit(0);
                return;
            }
            if (mode == "spirit")
            {
                CardModel spiritMaker = combat.CreateCard(ModelDb.Card<ChallengeSpiritMaker>(), player);
                await CardPileCmd.AddGeneratedCardToCombat(spiritMaker, PileType.Hand, player);
                await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), spiritMaker, null).WaitAsync(TimeSpan.FromSeconds(20));
                CardModel? spirit = PileType.Draw.GetPile(player).Cards.FirstOrDefault(c => c is MegaCrit.Sts2.Core.Models.Cards.Apparition);
                if (spirit is null || !ReferenceEquals(spirit.Owner, player))
                    throw new InvalidOperationException("SQ-09 Apparition did not generate with an owner");
                MainFile.Logger.Info("[ChallengePointsIntegration] PASS: SQ-09 Spirit Maker played and generated Apparition.");
                GetTree().Quit(0);
                return;
            }
            CardModel wave = player.Deck.Cards.OfType<MegaCrit.Sts2.Core.Models.Cards.IronWave>().First();
            if (wave.CurrentUpgradeLevel != 1) throw new InvalidOperationException("IT-08 starter upgrade missing in live run");
            CardModel generated = combat.CreateCard(ModelDb.Card<ChallengeMeatCleaver>(), player);
            await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, player);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            CardModel cleaver = PileType.Hand.GetPile(player).Cards.Last(c => c is ChallengeMeatCleaver);
            Creature target = combat.HittableEnemies.First(c => c.IsAlive && c.IsHittable);
            int hp = target.CurrentHp;
            int block = player.Creature.Block;
            await CardCmd.AutoPlay(new BlockingPlayerChoiceContext(), cleaver, target).WaitAsync(TimeSpan.FromSeconds(20));
            if (target.CurrentHp >= hp || player.Creature.Block <= block || cleaver.Pile?.Type == PileType.Hand)
                throw new InvalidOperationException("SQ-07 Meat Cleaver failed to play damage/block in PC combat");
            await CreatureCmd.Damage(new BlockingPlayerChoiceContext(), target, 999, ValueProp.Unpowered, player.Creature);
            if (wave.CurrentUpgradeLevel != 2 || CardModel.FromSerializable(wave.ToSerializable()).CurrentUpgradeLevel != 2)
                throw new InvalidOperationException("SQ-07 enemy kill did not persist unlimited Iron Wave upgrade");
            MainFile.Logger.Info("[ChallengePointsIntegration] PASS: PC combat Meat Cleaver damage/block and SQ-07 kill upgrade/save.");
            GetTree().Quit(0);
            return;
        }
        string capture = Path.Combine(Path.GetDirectoryName(typeof(MainFile).Assembly.Location)!, "integration-neow.png");
        GetViewport().GetTexture().GetImage().SavePng(capture);
        MainFile.Logger.Info($"[ChallengePointsIntegration] PASS: PC new-run and Neow loaded; capture={capture}");
        GetTree().Quit(0);
    }
}
