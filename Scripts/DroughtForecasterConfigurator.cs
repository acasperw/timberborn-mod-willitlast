using Bindito.Core;

namespace Mods.DroughtForecaster.Scripts {
  [Context("Game")]
  public class DroughtForecasterConfigurator : Configurator {

    protected override void Configure() {
      Bind<WillItLastOptions>().AsSingleton();
      Bind<GoodForecaster>().AsSingleton();
      Bind<HazardOutlookService>().AsSingleton();
      Bind<PopulationProjector>().AsSingleton();
      Bind<ForecastPanel>().AsSingleton();
    }

  }
}
