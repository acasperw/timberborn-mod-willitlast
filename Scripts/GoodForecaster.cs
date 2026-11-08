using System.Collections.Generic;
using System.Linq;
using Timberborn.Common;
using Timberborn.GameCycleSystem;
using Timberborn.GoodsSampling;
using Timberborn.Population;
using Timberborn.ResourceCountingSystem;
using Timberborn.SingletonSystem;
using Timberborn.WeatherSystem;
using UnityEngine;

namespace Mods.DroughtForecaster.Scripts {
  public class GoodForecast {

    public int Stock;
    public int Capacity;
    public int Beavers;
    public float DailyProduction;
    public float DailyUse;
    public bool IsMeasured; // False when there aren't enough samples yet and use is estimated from beaver count.
    public int StockAtHazardStart;
    public int RunsOutInDays; // Days from now until stock runs out, if that could happen before the hazard; 0 otherwise.
    public int RunsOutOnHazardDay; // Hazard day (1-based) on which stock runs out; 0 if it lasts past the horizon.

    public bool IsOut => Stock <= 0 && DailyUse > DailyProduction;

  }

  public class GoodForecaster : ILoadableSingleton {

    private const int MaxSampleDays = 5;

    private readonly EventBus _eventBus;
    private readonly ResourceCountingService _resourceCountingService;
    private readonly PopulationService _populationService;
    private readonly WeatherService _weatherService;
    private readonly GameCycleService _gameCycleService;
    private readonly GlobalGoodSamplingRegistry _globalGoodSamplingRegistry;
    private readonly WillItLastOptions _options;
    private readonly Dictionary<(IReadOnlyList<string>, bool), Rates> _ratesCache = new();

    public GoodForecaster(EventBus eventBus,
                          ResourceCountingService resourceCountingService,
                          PopulationService populationService,
                          WeatherService weatherService,
                          GameCycleService gameCycleService,
                          GlobalGoodSamplingRegistry globalGoodSamplingRegistry,
                          WillItLastOptions options) {
      _eventBus = eventBus;
      _resourceCountingService = resourceCountingService;
      _populationService = populationService;
      _weatherService = weatherService;
      _gameCycleService = gameCycleService;
      _globalGoodSamplingRegistry = globalGoodSamplingRegistry;
      _options = options;
    }

    public void Load() {
      _eventBus.Register(this);
    }

    [OnEvent]
    public void OnGoodsSampled(GoodsSampledEvent goodsSampledEvent) {
      _ratesCache.Clear();
    }

    // perBeaverEstimate is the minimum demand; measured use can be lower when stock is empty.
    // projectedBeavers scales use to a larger population; 0 keeps the current one.
    // productionContinues: false assumes nothing is produced once the bad season starts (water).
    public GoodForecast Calculate(IReadOnlyList<string> goodIds, float perBeaverEstimate, HazardOutlook outlook,
                                  bool productionContinues, int projectedBeavers = 0) {
      // The safety margin extends the horizon so stock that only just lasts shows amber, not green.
      var horizonDays = outlook.MaxDuration + _options.SafetyMarginDays;
      var population = _populationService.GlobalPopulationData;

      var f = new GoodForecast {
          Beavers = population.NumberOfBeavers
      };
      foreach (var goodId in goodIds) {
        var resourceCount = _resourceCountingService.GetGlobalResourceCount(goodId);
        f.Stock += resourceCount.AvailableStock;
        f.Capacity += resourceCount.TotalCapacity;
      }
      var rates = GetRates(goodIds, outlook.IsHazard);
      f.IsMeasured = rates.IsMeasured;
      f.DailyProduction = rates.Production;
      f.DailyUse = Mathf.Max(rates.Use, f.Beavers * perBeaverEstimate);
      if (projectedBeavers > f.Beavers) {
        f.DailyUse = f.Beavers > 0
            ? f.DailyUse * projectedBeavers / f.Beavers
            : projectedBeavers * perBeaverEstimate;
        f.Beavers = projectedBeavers;
      }

      float hazardDay;
      float stockAtHazard;
      float hazardProduction;
      if (outlook.IsHazard) {
        hazardDay = outlook.HazardDay;
        stockAtHazard = f.Stock;
        hazardProduction = f.DailyProduction;
      } else {
        hazardDay = 1f;
        var net = f.DailyProduction - f.DailyUse;
        // Uses the latest possible start so "day 1 of the bad season" never hints at when it begins.
        if (net < 0f && f.Stock + net * outlook.MaxDaysUntil <= 0f) {
          f.RunsOutInDays = Mathf.Max(1, Mathf.CeilToInt(f.Stock / -net));
        }
        // Worst case over the possible start days: latest when draining, earliest when filling.
        stockAtHazard = f.Stock + net * (net < 0f ? outlook.MaxDaysUntil : outlook.MinDaysUntil);
        if (f.Capacity > 0) {
          stockAtHazard = Mathf.Min(stockAtHazard, f.Capacity);
        }
        stockAtHazard = Mathf.Max(0f, stockAtHazard);
        f.StockAtHazardStart = Mathf.RoundToInt(stockAtHazard);
        // Until the season is observed, production is only assumed to continue where the caller says so.
        hazardProduction = productionContinues ? f.DailyProduction : 0f;
      }

      var hazardNet = f.DailyUse - hazardProduction;
      var daysOfStock = hazardNet > 0f ? stockAtHazard / hazardNet : float.PositiveInfinity;
      var runsOutOn = Mathf.FloorToInt(hazardDay + Mathf.Min(daysOfStock, horizonDays + 1));
      f.RunsOutOnHazardDay = runsOutOn <= horizonDays ? Mathf.Max(runsOutOn, 1) : 0;
      return f;
    }

    private Rates GetRates(IReadOnlyList<string> goodIds, bool isHazard) {
      if (!_ratesCache.TryGetValue((goodIds, isHazard), out var rates)) {
        rates = MeasureRates(goodIds, isHazard);
        _ratesCache[(goodIds, isHazard)] = rates;
      }
      return rates;
    }

    // A sample holds the stock at dawn plus the previous day's production, so use = production - stock change.
    // Medians keep one-off stock jumps (e.g. demolished storage) from skewing the rates.
    private Rates MeasureRates(IReadOnlyList<string> goodIds, bool isHazard) {
      if (goodIds.Count == 0) {
        return default;
      }
      var registry = _globalGoodSamplingRegistry.GoodSamplingRegistry;
      var histories = goodIds.Select(id => registry.GetGoodSampleHistory(id).GoodSamples).ToList();
      var cycle = _gameCycleService.Cycle;
      var firstDay = isHazard ? _weatherService.TemperateWeatherDuration + 2 : 2;
      var minCount = histories.Min(h => h.Count);

      var productions = new List<float>();
      var uses = new List<float>();
      for (var back = 0; back < MaxSampleDays && back + 1 < minCount; back++) {
        var reference = Sample(histories[0], back);
        if (reference.Cycle != cycle || reference.Day < firstDay) {
          break;
        }
        var dayProduction = 0f;
        var stockChange = 0f;
        foreach (var history in histories) {
          var sample = Sample(history, back);
          dayProduction += sample.Production;
          stockChange += sample.Stock - Sample(history, back + 1).Stock;
        }
        productions.Add(dayProduction);
        uses.Add(Mathf.Max(0f, dayProduction - stockChange));
      }
      if (uses.Count == 0) {
        return default;
      }
      return new Rates(true, Median(productions), Median(uses));
    }

    private static GoodSample Sample(ReadOnlyList<GoodSample> history, int back) {
      return history[history.Count - 1 - back];
    }

    public static float Median(List<float> values) {
      values.Sort();
      var mid = values.Count / 2;
      return values.Count % 2 == 1 ? values[mid] : (values[mid - 1] + values[mid]) / 2f;
    }

    private readonly struct Rates {

      public readonly bool IsMeasured;
      public readonly float Production;
      public readonly float Use;

      public Rates(bool isMeasured, float production, float use) {
        IsMeasured = isMeasured;
        Production = production;
        Use = use;
      }

    }

  }
}
