using System.Text.Json;
using CounterStrikeSharp.API.Core.Logging;
using Microsoft.Extensions.Logging;

namespace ZombieSharp
{
    public class WeaponModule : IWeaponModule
    {
        private ZombieSharp _Core;

        private ILogger _logger = CoreLogging.Factory.CreateLogger("WeaponConfgLog");

        public WeaponModule(ZombieSharp plugin)
        {
            _Core = plugin;
        }

        public WeaponConfig WeaponDatas { get; private set; }

        public void Initialize()
        {
            var configPath = Path.Combine(_Core.ModuleDirectory, "weapons.json");

            if (!File.Exists(configPath))
            {
                _logger.LogWarning("Cannot found weapons.json file!");
                return;
            }

            WeaponDatas = JsonSerializer.Deserialize<WeaponConfig>(File.ReadAllText(configPath));
        }
    }
}

public class WeaponConfig
{
    public Dictionary<string, WeaponData> WeaponConfigs { get; set; } = new Dictionary<string, WeaponData>();

    public float KnockbackMultiply { get; set; } = 1.0f;

    public WeaponConfig()
    {
        WeaponConfigs = new Dictionary<string, WeaponData>(StringComparer.OrdinalIgnoreCase)
        {
            { "glock", new WeaponData("Glock", "weapon_glock", 1.0f) },
        };
    }
}

public class WeaponData
{
    public WeaponData(string weaponName, string weaponEntity, float knockback)
    {
        WeaponName = weaponName;
        WeaponEntity = weaponEntity;
        Knockback = knockback;
    }

    public string WeaponName { get; set; }
    public string WeaponEntity { get; set; }
	public float Knockback { get; set; }
}