using Timberborn.SettingsSystem;

namespace Mods.DroughtForecaster.Scripts {
  public class WillItLastOptions {

    // Keys written by Mod Settings for ModSettingsSupport~/WillItLastSettingsOwner.cs.
    private const string SettingsKeyPrefix = "ModSetting.acasperw.WillItLast.WillItLastSettingsOwner.";
    private const string ShowWaterKey = SettingsKeyPrefix + "ShowWater";
    private const string ShowFoodKey = SettingsKeyPrefix + "ShowFood";

    private readonly ISettings _settings;

    public WillItLastOptions(ISettings settings) {
      _settings = settings;
    }

    public bool ShowWater => _settings.GetBool(ShowWaterKey, true);
    public bool ShowFood => _settings.GetBool(ShowFoodKey, true);
    public int SafetyMarginDays => 0;

  }
}
