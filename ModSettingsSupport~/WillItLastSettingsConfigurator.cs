using Bindito.Core;

namespace Mods.DroughtForecaster.ModSettingsSupport {
  [Context("MainMenu")]
  [Context("Game")]
  public class WillItLastSettingsConfigurator : Configurator {

    protected override void Configure() {
      Bind<WillItLastSettingsOwner>().AsSingleton();
    }

  }
}
