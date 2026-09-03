using System;
using System.Reflection;
using UnityEngine;

namespace MSC.Lighting.Production
{
    /// <summary>
    /// Read-only adapter for the established vehicle authorities. NWH is
    /// accessed through its public serialized object graph so this assembly
    /// does not take a compile-time dependency on vendor code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleLightingElectricalAdapter : MonoBehaviour,
        IVehicleElectricalLightingSource
    {
        [SerializeField] private MonoBehaviour vendorVehicleController;
        [SerializeField] private MonoBehaviour projectSimulationHost;
        [SerializeField] private GameLightFixture[] fixtures =
            Array.Empty<GameLightFixture>();

        private object lightsManager;
        private Func<int> readLightState;
        private Func<float> readBatteryVoltage;

        public bool HasUsablePower
        {
            get
            {
                if (TryReadBatteryVoltage(out float voltage))
                {
                    return voltage >= 9f;
                }

                return vendorVehicleController != null &&
                       vendorVehicleController.isActiveAndEnabled;
            }
        }

        private void Awake()
        {
            CachePublicApis();
            BindFixtures();
        }

        private void Start()
        {
            if (readLightState == null || readBatteryVoltage == null)
            {
                CachePublicApis();
                BindFixtures();
            }
        }

        public bool IsLightingChannelOn(string channelId)
        {
            if (!HasUsablePower || readLightState == null)
            {
                return false;
            }

            int state = readLightState();
            int mask = channelId switch
            {
                "brake" => 1 << 0,
                "tail" => 1 << 1,
                "license-plate" => 1 << 1,
                "dashboard" => (1 << 1) | (1 << 3) | (1 << 4),
                "interior" => (1 << 1) | (1 << 7),
                "reverse" => 1 << 2,
                "low-beam" => 1 << 3,
                "high-beam" => 1 << 4,
                "indicator" => (1 << 5) | (1 << 6),
                _ => 0,
            };
            return mask != 0 && (state & mask) != 0;
        }

        private void CachePublicApis()
        {
            if (vendorVehicleController != null)
            {
                FieldInfo effectsField = vendorVehicleController.GetType()
                    .GetField("effectsManager", BindingFlags.Public |
                        BindingFlags.Instance);
                object effects = effectsField?.GetValue(vendorVehicleController);
                FieldInfo lightsField = effects?.GetType().GetField(
                    "lightsManager",
                    BindingFlags.Public | BindingFlags.Instance);
                lightsManager = lightsField?.GetValue(effects);
                MethodInfo getIntState = lightsManager?.GetType().GetMethod(
                    "GetIntState",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getIntState != null)
                {
                    readLightState = (Func<int>)Delegate.CreateDelegate(
                        typeof(Func<int>),
                        lightsManager,
                        getIntState);
                }
            }

            if (projectSimulationHost != null)
            {
                PropertyInfo rootProperty = projectSimulationHost.GetType().GetProperty(
                    "Root",
                    BindingFlags.Public | BindingFlags.Instance);
                object root = rootProperty?.GetValue(projectSimulationHost);
                PropertyInfo stateProperty = root?.GetType().GetProperty(
                    "State",
                    BindingFlags.Public | BindingFlags.Instance);
                object state = stateProperty?.GetValue(root);
                PropertyInfo batteryVoltageProperty = state?.GetType().GetProperty(
                    "BatteryVoltage",
                    BindingFlags.Public | BindingFlags.Instance);
                MethodInfo getter = batteryVoltageProperty?.GetGetMethod();
                if (state != null && getter != null)
                {
                    readBatteryVoltage = (Func<float>)Delegate.CreateDelegate(
                        typeof(Func<float>),
                        state,
                        getter);
                }
            }
        }

        private bool TryReadBatteryVoltage(out float voltage)
        {
            if (readBatteryVoltage != null)
            {
                voltage = readBatteryVoltage();
                return true;
            }

            voltage = 0f;
            return false;
        }

        private void BindFixtures()
        {
            for (int index = 0; index < fixtures.Length; index++)
            {
                fixtures[index]?.BindVehicleElectricalSource(this);
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            MonoBehaviour configuredVendorVehicle,
            MonoBehaviour configuredSimulationHost,
            GameLightFixture[] configuredFixtures)
        {
            vendorVehicleController = configuredVendorVehicle;
            projectSimulationHost = configuredSimulationHost;
            fixtures = configuredFixtures ?? Array.Empty<GameLightFixture>();
        }
#endif
    }
}
