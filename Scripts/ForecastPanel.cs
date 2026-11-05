using System.Collections.Generic;
using System.Linq;
using Timberborn.AssetSystem;
using Timberborn.CoreUI;
using Timberborn.GameFactionSystem;
using Timberborn.Goods;
using Timberborn.Localization;
using Timberborn.SettingsSystem;
using Timberborn.SingletonSystem;
using Timberborn.TooltipSystem;
using Timberborn.UILayoutSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mods.DroughtForecaster.Scripts {
  public class ForecastPanel : ILoadableSingleton, IUpdatableSingleton {

    private const float RefreshIntervalSeconds = 1f;
    private const string PanelVisibleKey = "WillItLast.PanelVisible";
    // Normal difficulty beaver needs, scaled by the game mode's consumption setting; the floor for daily use.
    private const float WaterPerBeaverPerDay = 2.13f;
    private const float FoodPerBeaverPerDay = 2.67f;
    private static readonly Color OkColor = new(0.45f, 0.85f, 0.4f);
    private static readonly Color MaybeColor = new(1f, 0.75f, 0.2f);
    private static readonly Color BadColor = new(0.95f, 0.3f, 0.25f);

    private readonly UILayout _uiLayout;
    private readonly EventBus _eventBus;
    private readonly VisualElementLoader _visualElementLoader;
    private readonly GoodForecaster _goodForecaster;
    private readonly HazardOutlookService _hazardOutlookService;
    private readonly PopulationProjector _populationProjector;
    private readonly IGoodService _goodService;
    private readonly NeedModificationService _needModificationService;
    private readonly IAssetLoader _assetLoader;
    private readonly ITooltipRegistrar _tooltipRegistrar;
    private readonly ISettings _settings;
    private readonly ILoc _loc;
    private readonly List<string> _waterGoods = new() { "Water" };
    private List<string> _foodGoods;
    private ResourceView _water;
    private ResourceView _food;
    private VisualElement _buttons;
    private VisualElement _panel;
    private float _nextRefreshTime;

    public ForecastPanel(UILayout uiLayout,
                         EventBus eventBus,
                         VisualElementLoader visualElementLoader,
                         GoodForecaster goodForecaster,
                         HazardOutlookService hazardOutlookService,
                         PopulationProjector populationProjector,
                         IGoodService goodService,
                         NeedModificationService needModificationService,
                         IAssetLoader assetLoader,
                         ITooltipRegistrar tooltipRegistrar,
                         ISettings settings,
                         ILoc loc) {
      _uiLayout = uiLayout;
      _eventBus = eventBus;
      _visualElementLoader = visualElementLoader;
      _goodForecaster = goodForecaster;
      _hazardOutlookService = hazardOutlookService;
      _populationProjector = populationProjector;
      _goodService = goodService;
      _needModificationService = needModificationService;
      _assetLoader = assetLoader;
      _tooltipRegistrar = tooltipRegistrar;
      _settings = settings;
      _loc = loc;
    }

    public void Load() {
      _foodGoods = _goodService.Goods.Where(IsFood).ToList();
      _water = new ResourceView("WillItLast.Water", _waterGoods,
                                WaterPerBeaverPerDay * _needModificationService._waterConsumption,
                                false, GetIcon("Water"));
      _food = new ResourceView("WillItLast.Food", _foodGoods,
                               FoodPerBeaverPerDay * _needModificationService._foodConsumption,
                               false, GetIcon(_foodGoods.FirstOrDefault()));

      _buttons = new VisualElement();
      _buttons.style.flexDirection = FlexDirection.Row;
      _buttons.Add(CreateStatusButton(_water));
      _buttons.Add(CreateStatusButton(_food));

      _panel = CreatePanel();
      SetPanelVisible(_settings.GetBool(PanelVisibleKey));
      _eventBus.Register(this);
    }

    [OnEvent]
    public void OnShowPrimaryUI(ShowPrimaryUIEvent showPrimaryUIEvent) {
      _uiLayout.AddTopRightButton(_buttons, 100);
      _uiLayout.AddTopRight(_panel, UILayout.TopRightButtonsOrder + 1);
      Refresh();
    }

    public void UpdateSingleton() {
      if (Time.unscaledTime < _nextRefreshTime) {
        return;
      }
      _nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
      Refresh();
    }

    private bool IsFood(string goodId) {
      var effects = _goodService.GetGood(goodId).ConsumptionEffects;
      return !effects.IsDefault && effects.Any(e => e.NeedId == "Hunger");
    }

    private Sprite GetIcon(string goodId) {
      var spec = goodId != null ? _goodService.GetGoodOrNull(goodId) : null;
      return spec?.Icon.Asset;
    }

    private VisualElement CreateStatusButton(ResourceView view) {
      var root = _visualElementLoader.LoadVisualElement("Common/SquareToggle");
      var toggle = root.Q<Toggle>("Toggle");
      var checkmark = toggle.Q(className: "unity-toggle__checkmark");
      checkmark.style.backgroundImage = new StyleBackground(view.Icon);
      checkmark.style.width = 18;
      checkmark.style.height = 18;
      checkmark.style.marginLeft = 4;
      checkmark.style.marginTop = 4;

      var dot = new VisualElement { pickingMode = PickingMode.Ignore };
      dot.style.position = Position.Absolute;
      dot.style.right = 1;
      dot.style.bottom = 1;
      dot.style.width = 9;
      dot.style.height = 9;
      SetRound(dot, 4.5f);
      dot.style.borderTopWidth = dot.style.borderBottomWidth = 1;
      dot.style.borderLeftWidth = dot.style.borderRightWidth = 1;
      var border = new Color(0.1f, 0.1f, 0.1f);
      dot.style.borderTopColor = dot.style.borderBottomColor = border;
      dot.style.borderLeftColor = dot.style.borderRightColor = border;
      toggle.Add(dot);

      toggle.RegisterValueChangedCallback(evt => SetPanelVisible(evt.newValue));
      _tooltipRegistrar.RegisterUpdatable(root, () => GetTooltip(view));
      view.Toggle = toggle;
      view.Dot = dot;
      return root;
    }

    private VisualElement CreatePanel() {
      var panel = new NineSliceVisualElement();
      var commonStyle = _assetLoader.LoadSafe<StyleSheet>("UI/Views/Common/CommonStyle");
      if (commonStyle) {
        panel.styleSheets.Add(commonStyle);
        panel.AddToClassList("square-large--green");
      } else {
        panel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
      }
      panel.style.marginTop = 4;
      panel.style.marginRight = 4;
      panel.style.paddingLeft = 10;
      panel.style.paddingRight = 10;
      panel.style.paddingTop = 6;
      panel.style.paddingBottom = 6;
      panel.style.alignSelf = Align.FlexEnd;
      panel.Add(CreateRow(_water));
      panel.Add(CreateRow(_food));
      return panel;
    }

    private VisualElement CreateRow(ResourceView view) {
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      row.style.alignItems = Align.Center;
      row.style.marginTop = 1;
      row.style.marginBottom = 1;

      var icon = new Image { sprite = view.Icon };
      icon.style.width = 20;
      icon.style.height = 20;
      icon.style.marginRight = 6;
      row.Add(icon);

      view.RowLabel = new Label();
      view.RowLabel.AddToClassList("game-text-normal");
      row.Add(view.RowLabel);

      _tooltipRegistrar.RegisterUpdatable(row, () => GetTooltip(view));
      return row;
    }

    private void SetPanelVisible(bool visible) {
      _panel.ToggleDisplayStyle(visible);
      _water.Toggle?.SetValueWithoutNotify(visible);
      _food.Toggle?.SetValueWithoutNotify(visible);
      _settings.SetBool(PanelVisibleKey, visible);
    }

    private void Refresh() {
      var outlook = _hazardOutlookService.Calculate();
      var daysToSeasonEnd = outlook.IsHazard ? outlook.DaysLeft : outlook.MaxDaysUntil + outlook.MaxDuration;
      var projectedBeavers = _populationProjector.Project(daysToSeasonEnd);
      Show(_water, outlook, projectedBeavers);
      Show(_food, outlook, projectedBeavers);
    }

    private void Show(ResourceView view, HazardOutlook outlook, int projectedBeavers) {
      view.Outlook = outlook;
      view.Current = _goodForecaster.Calculate(view.GoodIds, view.PerBeaverPerDay, outlook,
                                               view.ProductionContinues);
      view.Projected = _goodForecaster.Calculate(view.GoodIds, view.PerBeaverPerDay, outlook,
                                                 view.ProductionContinues, projectedBeavers);
      var (status, color) = GetStatus(view.Current, outlook);
      view.RowLabel.text = status;
      view.RowLabel.style.color = color;
      view.Dot.style.backgroundColor = color;
    }

    private (string text, Color color) GetStatus(GoodForecast f, HazardOutlook outlook) {
      if (f.IsOut) {
        return (_loc.T("WillItLast.Out"), BadColor);
      }
      if (f.RunsOutInDays > 0) {
        return (_loc.T("WillItLast.RunsOutInDays", f.RunsOutInDays), BadColor);
      }
      if (f.RunsOutOnHazardDay == 0) {
        return (_loc.T("WillItLast.Enough"), OkColor);
      }
      if (f.RunsOutOnHazardDay <= outlook.MinDuration) {
        return (_loc.T("WillItLast.RunsOut"), BadColor);
      }
      return (_loc.T("WillItLast.MayRunOut"), MaybeColor);
    }

    private string GetTooltip(ResourceView view) {
      var f = view.Current;
      var outlook = view.Outlook;
      if (f == null) {
        return "";
      }
      var lines = new List<string> {
          $"{_loc.T(view.TitleLocKey)}: {GetStatus(f, outlook).text}",
          _loc.T("WillItLast.TooltipStock", f.Stock, f.Capacity),
          f.IsMeasured
              ? _loc.T("WillItLast.TooltipRatesMeasured", f.DailyProduction.ToString("0.#"),
                       f.DailyUse.ToString("0.#"))
              : _loc.T("WillItLast.TooltipRatesEstimated", f.DailyUse.ToString("0.#"), f.Beavers)
      };
      if (outlook.MaxDuration > 0) {
        if (outlook.IsHazard) {
          lines.Add(_loc.T("WillItLast.TooltipSeasonLeft", outlook.DaysLeft));
        } else if (f.RunsOutInDays == 0) {
          lines.Add(_loc.T("WillItLast.TooltipAtSeasonStart", f.StockAtHazardStart));
        }
      }
      if (view.Projected.Beavers > f.Beavers) {
        var key = _populationProjector.IsGrowthBased
            ? "WillItLast.TooltipGrowthContinues"
            : "WillItLast.TooltipHousingFull";
        lines.Add(_loc.T(key, view.Projected.Beavers, GetStatus(view.Projected, outlook).text));
      }
      lines.Add(_loc.T("WillItLast.TooltipClick"));
      return string.Join("\n", lines);
    }

    private static void SetRound(VisualElement element, float radius) {
      element.style.borderTopLeftRadius = radius;
      element.style.borderTopRightRadius = radius;
      element.style.borderBottomLeftRadius = radius;
      element.style.borderBottomRightRadius = radius;
    }

    private class ResourceView {

      public readonly string TitleLocKey;
      public readonly IReadOnlyList<string> GoodIds;
      public readonly float PerBeaverPerDay;
      public readonly bool ProductionContinues;
      public readonly Sprite Icon;
      public Toggle Toggle;
      public VisualElement Dot;
      public Label RowLabel;
      public HazardOutlook Outlook;
      public GoodForecast Current;
      public GoodForecast Projected;

      public ResourceView(string titleLocKey, IReadOnlyList<string> goodIds, float perBeaverPerDay,
                          bool productionContinues, Sprite icon) {
        TitleLocKey = titleLocKey;
        GoodIds = goodIds;
        PerBeaverPerDay = perBeaverPerDay;
        ProductionContinues = productionContinues;
        Icon = icon;
      }

    }

  }
}
