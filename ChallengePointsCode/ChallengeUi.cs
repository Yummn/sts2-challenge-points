using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace ChallengePoints;

internal sealed partial class ChallengeUi : CanvasLayer
{
    private readonly NCharacterSelectScreen _screen;
    private Control _shade = null!;
    private VBoxContainer _commonList = null!;
    private VBoxContainer _roleList = null!;
    private Label _commonHeading = null!;
    private Label _roleHeading = null!;
    private Label _summary = null!;
    private Label _commonRewards = null!;
    private Label _roleRewards = null!;
    private Control _frame = null!;
    private bool _showShop;

    internal ChallengeUi(NCharacterSelectScreen screen)
    {
        _screen = screen;
        Layer = 110;
        Name = "ChallengePointsUi";
    }

    public override void _Ready()
    {
        ChallengeLocalization.Ensure();
        var launcher = MakeButton("挑战点契约", new Color("d49a56"), 24);
        launcher.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        launcher.Position = new Vector2(-270, 18);
        launcher.CustomMinimumSize = new Vector2(250, 64);
        launcher.Pressed += Open;
        AddChild(launcher);

        _shade = new ColorRect { Color = new Color(0.08f, 0.04f, 0.04f, 0.81f), MouseFilter = Control.MouseFilterEnum.Stop };
        _shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _shade.Visible = false;
        AddChild(_shade);

        _frame = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _frame.SetAnchorsPreset(Control.LayoutPreset.Center);
        _frame.OffsetLeft = -740;
        _frame.OffsetTop = -430;
        _frame.OffsetRight = 740;
        _frame.OffsetBottom = 430;
        _frame.PivotOffset = new Vector2(740, 430);
        _shade.AddChild(_frame);

        Texture2D? art = ChallengeArt.ContractFrame;
        if (art is not null)
        {
            var backdrop = new TextureRect
            {
                Texture = art,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _frame.AddChild(backdrop);
        }
        else
        {
            var fallback = new Panel();
            fallback.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            fallback.AddThemeStyleboxOverride("panel", Style(new Color("e6c497"), new Color("372122"), 12, 28));
            _frame.AddChild(fallback);
        }

        var inset = new MarginContainer();
        inset.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        inset.AddThemeConstantOverride("margin_left", 110);
        inset.AddThemeConstantOverride("margin_right", 110);
        inset.AddThemeConstantOverride("margin_top", 72);
        inset.AddThemeConstantOverride("margin_bottom", 150);
        _frame.AddChild(inset);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        inset.AddChild(root);
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 24);
        root.AddChild(header);
        var title = MakeLabel("挑战点契约", 42, new Color("38232b"));
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(title);
        var challengesTab = MakeButton("挑战词条", new Color("6a8292"), 22);
        challengesTab.CustomMinimumSize = new Vector2(170, 62);
        challengesTab.Pressed += () => { _showShop = false; Refresh(); };
        header.AddChild(challengesTab);
        var shopTab = MakeButton("分队商店", new Color("9b7150"), 22);
        shopTab.CustomMinimumSize = new Vector2(170, 62);
        shopTab.Pressed += () => { _showShop = true; Refresh(); };
        header.AddChild(shopTab);
        var close = MakeButton("返回", new Color("bd6555"), 24);
        close.CustomMinimumSize = new Vector2(150, 62);
        close.Pressed += () => _shade.Visible = false;
        header.AddChild(close);

        _summary = MakeLabel("", 25, new Color("473a32"));
        root.AddChild(_summary);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 20);
        columns.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        root.AddChild(columns);
        _commonList = MakeColumn(columns, "通用挑战", new Color("52788d"), out _commonHeading);
        _roleList = MakeColumn(columns, "角色挑战", new Color("9b5a56"), out _roleHeading);

        _commonRewards = MakeLabel("", 19, new Color("334d5c"));
        _commonRewards.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_commonRewards);
        _roleRewards = MakeLabel("", 19, new Color("7e383e"));
        _roleRewards.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        root.AddChild(_roleRewards);
        root.AddChild(MakeLabel("先选挑战赚点，再到分队商店购买。开局后配置锁定；旧阈值不再自动发奖。", 18, new Color("6e6257")));
        GetViewport().SizeChanged += UpdateFrameScale;
        UpdateFrameScale();
    }

    private void UpdateFrameScale()
    {
        if (_frame is null) return;
        Vector2 size = GetViewport().GetVisibleRect().Size;
        float scale = Mathf.Min(1f, Mathf.Min((size.X - 32f) / 1480f, (size.Y - 32f) / 860f));
        _frame.Scale = Vector2.One * Mathf.Max(0.2f, scale);
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= UpdateFrameScale;
    }

    private static VBoxContainer MakeColumn(HBoxContainer host, string title, Color color, out Label heading)
    {
        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panel.AddThemeStyleboxOverride("panel", Style(new Color("f5dfbb"), new Color("694a40"), 6, 15));
        host.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        panel.AddChild(column);
        heading = MakeLabel(title, 30, color);
        column.AddChild(heading);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        scroll.CustomMinimumSize = new Vector2(0, 300);
        column.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 9);
        scroll.AddChild(list);
        return list;
    }

    private void Open()
    {
        ChallengeLocalization.Ensure();
        _shade.Visible = true;
        Refresh();
    }

    internal void OpenForSmokeTest() => Open();

    private string CurrentRole()
    {
        try { return ChallengeCatalog.NormalizeRole(_screen.Lobby.LocalPlayer.character.Id.Entry); }
        catch { return "ironclad"; }
    }

    private void Refresh()
    {
        string role = CurrentRole();
        int common = ChallengeSelection.Score("common");
        int character = ChallengeSelection.Score(role);
        int spent = ChallengeSelection.Spent(role);
        _summary.Text = $"通用 {common} ＋ 角色 {character} － 已花 {spent} ＝ 可用 {common + character - spent} 挑战点";
        if (_showShop)
        {
            _commonHeading.Text = "四级分队";
            _roleHeading.Text = "小商品";
            BuildSquads(_commonList, role);
            BuildItems(_roleList, role);
            _commonRewards.Text = "分队可同时购买；价格为当前级总价，升级仅补差价。";
            _roleRewards.Text = "不确定分队随机跨角色，结果在开局时固定。";
        }
        else
        {
            _commonHeading.Text = "通用挑战";
            _roleHeading.Text = "角色挑战";
            BuildRows(_commonList, "common");
            BuildRows(_roleList, role);
            _commonRewards.Text = "词条仅提供购买点数；旧阈值奖励已取消。";
            _roleRewards.Text = "减少词条等级时，不能使已购内容超出预算。";
        }
    }

    private static void ClearRows(VBoxContainer target)
    {
        foreach (Node child in target.GetChildren())
        {
            target.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void BuildSquads(VBoxContainer target, string role)
    {
        var scroll = target.GetParent() as ScrollContainer;
        int previousScroll = scroll?.ScrollVertical ?? 0;
        ClearRows(target);
        foreach (ChallengeSquad squad in ChallengeShopCatalog.Squads.Where(x => x.Role == "common" || x.Role == role))
        {
            int rank = ChallengeSelection.SquadRank(role, squad.Id);
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", Style(new Color("e9d1a6"), new Color("6d4a3a"), 4, 9));
            target.AddChild(panel);
            var row = new HBoxContainer();
            panel.AddChild(row);
            var texts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(texts);
            var heading = MakeLabel($"{squad.Name}  {rank}/4 · {squad.Price(rank)} 点", 20, new Color("443130"));
            texts.AddChild(heading);
            var description = MakeLabel(rank == 0
                ? $"I 级 {squad.TotalPrices[0]} 点 · {squad.TierDescriptions[0]}"
                : $"当前：{squad.TierDescriptions[rank - 1]}" + (rank < 4 ? $"\n下级：{squad.TierDescriptions[rank]}" : ""),
                17, new Color("695349"));
            description.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            texts.AddChild(description);
            var minus = MakeButton("－", new Color("b96f5c"), 22);
            minus.CustomMinimumSize = new Vector2(64, 58);
            minus.Disabled = rank == 0;
            minus.Pressed += () => { ChallengeSelection.SetSquadRank(role, squad.Id, rank - 1); Refresh(); };
            row.AddChild(minus);
            var plus = MakeButton("＋", new Color("77936e"), 22);
            plus.CustomMinimumSize = new Vector2(64, 58);
            plus.Disabled = rank >= 4 || ChallengeSelection.Available(role) < squad.Price(rank + 1) - squad.Price(rank);
            plus.Pressed += () => { ChallengeSelection.SetSquadRank(role, squad.Id, rank + 1); Refresh(); };
            row.AddChild(plus);
        }
        scroll?.SetDeferred("scroll_vertical", previousScroll);
    }

    private void BuildItems(VBoxContainer target, string role)
    {
        var scroll = target.GetParent() as ScrollContainer;
        int previousScroll = scroll?.ScrollVertical ?? 0;
        ClearRows(target);
        foreach (ChallengeShopItem item in ChallengeShopCatalog.Items)
        {
            bool owned = ChallengeSelection.HasItem(role, item.Id);
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", Style(new Color("e9d1a6"), new Color("6d4a3a"), 4, 9));
            target.AddChild(panel);
            var row = new HBoxContainer();
            panel.AddChild(row);
            var texts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(texts);
            texts.AddChild(MakeLabel($"{item.Name} · {item.Price} 点{(owned ? " · 已购" : "")}", 20, new Color("443130")));
            var description = MakeLabel(item.Description, 17, new Color("695349"));
            description.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            texts.AddChild(description);
            var button = MakeButton(owned ? "退" : "购", owned ? new Color("b96f5c") : new Color("77936e"), 22);
            button.CustomMinimumSize = new Vector2(74, 58);
            button.Disabled = !owned && ChallengeSelection.Available(role) < item.Price;
            button.Pressed += () => { ChallengeSelection.SetItem(role, item.Id, !owned); Refresh(); };
            row.AddChild(button);
        }
        scroll?.SetDeferred("scroll_vertical", previousScroll);
    }

    private void BuildRows(VBoxContainer target, string role)
    {
        var scroll = target.GetParent() as ScrollContainer;
        int previousScroll = scroll?.ScrollVertical ?? 0;
        ClearRows(target);
        foreach (ChallengeDefinition definition in ChallengeCatalog.All.Where(d => d.Role == role))
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", Style(
                definition.Selectable ? new Color("e9d1a6") : new Color("c4b6a0"),
                new Color("6d4a3a"), 4, 9));
            target.AddChild(panel);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            panel.AddChild(row);
            var texts = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(texts);
            int rank = ChallengeSelection.Rank(definition.Id);
            string suffix = definition.Selectable ? $"  {rank}/{definition.MaxRank}  ·  {rank*definition.CpPerRank} CP" : "  尚未开放";
            var heading = MakeLabel($"{definition.Id}  {definition.Name}{suffix}", 20, definition.Selectable ? new Color("443130") : new Color("706963"));
            heading.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            texts.AddChild(heading);
            var description = MakeLabel(definition.Description, 17, new Color("695349"));
            description.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
            texts.AddChild(description);
            var minus = MakeButton("－", new Color("b96f5c"), 22);
            minus.CustomMinimumSize = new Vector2(64, 58);
            minus.Disabled = !definition.Selectable || rank == 0;
            minus.Pressed += () => { ChallengeSelection.SetRank(definition.Id, ChallengeSelection.Rank(definition.Id) - 1); Refresh(); };
            row.AddChild(minus);
            var plus = MakeButton("＋", new Color("77936e"), 22);
            plus.CustomMinimumSize = new Vector2(64, 58);
            plus.Disabled = !definition.Selectable || rank >= definition.MaxRank;
            plus.Pressed += () => { ChallengeSelection.SetRank(definition.Id, ChallengeSelection.Rank(definition.Id) + 1); Refresh(); };
            row.AddChild(plus);
        }
        scroll?.SetDeferred("scroll_vertical", previousScroll);
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.2f, 0.1f, 0.08f, 0.17f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    private static Button MakeButton(string text, Color fill, int fontSize)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", fontSize);
        button.AddThemeColorOverride("font_color", new Color("fff2d5"));
        button.AddThemeStyleboxOverride("normal", Style(fill, new Color("35252c"), 6, 10));
        button.AddThemeStyleboxOverride("hover", Style(fill.Lightened(0.15f), new Color("35252c"), 6, 10));
        button.AddThemeStyleboxOverride("pressed", Style(fill.Darkened(0.17f), new Color("35252c"), 6, 10));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("9b9187"), new Color("655a54"), 5, 10));
        return button;
    }

    private static StyleBoxFlat Style(Color bg, Color border, int width, int radius)
    {
        var style = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = width,
            BorderWidthRight = width,
            BorderWidthTop = width,
            BorderWidthBottom = width,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ShadowColor = new Color(0.12f, 0.06f, 0.05f, 0.45f),
            ShadowSize = 5,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        return style;
    }
}
