using Godot;

namespace ChallengePoints;

// A read-only relic-like contract marker. This intentionally does not create a
// RelicModel, use a relic slot, or fire relic-obtained hooks.
internal sealed partial class ChallengeContractHud : CanvasLayer
{
    private readonly ChallengeContract _contract;
    private Control _shade = null!;
    private VBoxContainer _list = null!;

    internal ChallengeContractHud(ChallengeContract contract)
    {
        _contract = contract;
        Name = "ChallengeContractHud";
        Layer = 105;
    }

    public override void _Ready()
    {
        var marker = Button("契约", new Color("d8ae75"), 23);
        marker.Position = new Vector2(22, 175);
        marker.CustomMinimumSize = new Vector2(92, 74);
        marker.TooltipText = "挑战契约：点按查看本局负面词条与分队";
        marker.Pressed += Open;
        AddChild(marker);

        _shade = new ColorRect
        {
            Color = new Color(0.07f, 0.04f, 0.04f, 0.8f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false
        };
        _shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_shade);

        var scroll = new ScrollContainer();
        scroll.SetAnchorsPreset(Control.LayoutPreset.Center);
        scroll.OffsetLeft = -510;
        scroll.OffsetRight = 510;
        scroll.OffsetTop = -360;
        scroll.OffsetBottom = 360;
        scroll.AddThemeStyleboxOverride("panel", Frame(new Color("f1d6a8"), new Color("4a302e"), 12));
        _shade.AddChild(scroll);

        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", Frame(new Color("f1d6a8"), new Color("4a302e"), 12));
        scroll.AddChild(panel);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        panel.AddChild(_list);
    }

    private void Open()
    {
        foreach (Node child in _list.GetChildren())
        {
            _list.RemoveChild(child);
            child.QueueFree();
        }
        var title = Label("挑战契约", 33, new Color("3c2926"));
        _list.AddChild(title);
        var close = Button("返回战斗", new Color("c66e58"), 22);
        close.CustomMinimumSize = new Vector2(190, 60);
        close.Pressed += () => _shade.Visible = false;
        _list.AddChild(close);

        _list.AddChild(Label($"通用 {_contract.CommonCp}  ·  角色 {_contract.RoleCp}  ·  合计 {_contract.CommonCp + _contract.RoleCp}",
            21, new Color("554038")));
        if (_contract.ShopSchemaVersion > 0)
        {
            _list.AddChild(Label($"商店购买 {_contract.MerchantCardPurchases}/10  ·  新增卡牌 {_contract.ShopCardsAcquired}/15  ·  不死图腾{(_contract.ShopTotemSpent ? "已触发" : "未触发")}",
                18, new Color("75503a")));
            foreach (ChallengeSquad squad in ChallengeShopCatalog.Squads)
            {
                int rank = _contract.SquadRank(squad.Id);
                if (rank > 0) _list.AddChild(Label($"{squad.Name}  {rank}级", 22, new Color("75503a")));
            }
            foreach (ChallengeShopItem item in ChallengeShopCatalog.Items)
                if (_contract.HasShopItem(item.Id)) _list.AddChild(Label($"已购：{item.Name}", 19, new Color("75503a")));
        }
        _list.AddChild(Label("负面词条", 26, new Color("8b4140")));
        foreach (ChallengeDefinition def in ChallengeCatalog.All.Where(x =>
            x.Role is "common" || x.Role == _contract.CharacterRole))
        {
            int rank = _contract.Rank(def.Id);
            if (rank <= 0) continue;
            var row = Label($"{def.Name}  {rank}级\n{def.Description}", 19, new Color("49382f"));
            row.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _list.AddChild(row);
        }
        _shade.Visible = true;
    }

    private static Godot.Label Label(string text, int size, Color color)
    {
        var label = new Godot.Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private static Godot.Button Button(string text, Color fill, int size)
    {
        var button = new Godot.Button { Text = text };
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeColorOverride("font_color", new Color("342523"));
        button.AddThemeStyleboxOverride("normal", Frame(fill, new Color("4a302e"), 8));
        button.AddThemeStyleboxOverride("hover", Frame(fill.Lightened(0.12f), new Color("4a302e"), 8));
        button.AddThemeStyleboxOverride("pressed", Frame(fill.Darkened(0.12f), new Color("4a302e"), 8));
        return button;
    }

    private static StyleBoxFlat Frame(Color fill, Color border, int width)
    {
        var style = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(9);
        style.ContentMarginLeft = style.ContentMarginRight = 18;
        style.ContentMarginTop = style.ContentMarginBottom = 12;
        return style;
    }
}
