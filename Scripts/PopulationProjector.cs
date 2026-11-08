using System.Collections.Generic;
using Timberborn.GameFactionSystem;
using Timberborn.Population;
using Timberborn.PopulationStatisticsSampling;
using UnityEngine;

namespace Mods.DroughtForecaster.Scripts {
  public class PopulationProjector {

    // Iron Teeth breed in pods, so housing doesn't cap them; other factions fill free beds.
    private const string PodBreedingFactionId = "IronTeeth";
    private const int MaxSampleDays = 5;

    private readonly FactionService _factionService;
    private readonly PopulationService _populationService;
    private readonly GlobalPopulationSamplesRegistry _globalPopulationSamplesRegistry;

    public PopulationProjector(FactionService factionService,
                               PopulationService populationService,
                               GlobalPopulationSamplesRegistry globalPopulationSamplesRegistry) {
      _factionService = factionService;
      _populationService = populationService;
      _globalPopulationSamplesRegistry = globalPopulationSamplesRegistry;
    }

    public bool IsGrowthBased => _factionService.Current?.Id == PodBreedingFactionId;

    public int Project(float daysAhead) {
      var population = _populationService.GlobalPopulationData;
      if (!IsGrowthBased) {
        return population.NumberOfBeavers + population.BedData.FreeBeds;
      }
      var growth = Mathf.Max(0f, MedianDailyGrowth());
      return population.NumberOfBeavers + Mathf.RoundToInt(growth * daysAhead);
    }

    private float MedianDailyGrowth() {
      var samples = _globalPopulationSamplesRegistry.PopulationSampleHistory.PopulationSamples;
      var growth = new List<float>();
      for (var i = samples.Count - 1; i >= 0 && growth.Count < MaxSampleDays; i--) {
        growth.Add(samples[i].Births - samples[i].Deaths);
      }
      return growth.Count > 0 ? GoodForecaster.Median(growth) : 0f;
    }

  }
}
