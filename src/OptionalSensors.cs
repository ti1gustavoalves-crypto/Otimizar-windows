using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CodexPerformanceOptimizer
{
    internal static class OptionalSensorProvider
    {
        private const string LibraryName = "LibreHardwareMonitorLib.dll";

        public static List<TemperatureReading> ReadTemperatures()
        {
            string path = FindLibrary();
            if (string.IsNullOrEmpty(path)) return new List<TemperatureReading>();
            object computer = null;
            try
            {
                Assembly assembly = Assembly.LoadFrom(path);
                Type computerType = assembly.GetType("LibreHardwareMonitor.Hardware.Computer", true);
                computer = Activator.CreateInstance(computerType);
                Enable(computerType, computer, "IsCpuEnabled");
                Enable(computerType, computer, "IsGpuEnabled");
                Enable(computerType, computer, "IsMemoryEnabled");
                Enable(computerType, computer, "IsStorageEnabled");
                Enable(computerType, computer, "IsMotherboardEnabled");
                computerType.GetMethod("Open").Invoke(computer, null);
                var readings = new List<TemperatureReading>();
                IEnumerable hardware = computerType.GetProperty("Hardware").GetValue(computer, null) as IEnumerable;
                if (hardware != null) foreach (object item in hardware) ReadHardware(item, readings);
                return readings.Where(item => item.Celsius > 0 && item.Celsius < 150).OrderByDescending(item => item.Celsius).Take(12).ToList();
            }
            catch (Exception ex)
            {
                CrashLogger.Write(ex, "Provedor opcional de sensores");
                return new List<TemperatureReading>();
            }
            finally
            {
                if (computer != null)
                {
                    try { computer.GetType().GetMethod("Close").Invoke(computer, null); } catch { }
                    IDisposable disposable = computer as IDisposable;
                    if (disposable != null) disposable.Dispose();
                }
            }
        }

        public static string Status
        {
            get
            {
                string path = FindLibrary();
                return string.IsNullOrEmpty(path) ? "Provedor opcional não instalado" : "LibreHardwareMonitor integrado";
            }
        }

        private static void ReadHardware(object hardware, List<TemperatureReading> readings)
        {
            if (hardware == null) return;
            Type type = hardware.GetType();
            MethodInfo update = type.GetMethod("Update");
            if (update != null) update.Invoke(hardware, null);
            string hardwareName = Convert.ToString(type.GetProperty("Name").GetValue(hardware, null), CultureInfo.CurrentCulture);
            IEnumerable sensors = type.GetProperty("Sensors").GetValue(hardware, null) as IEnumerable;
            if (sensors != null)
            foreach (object sensor in sensors)
            {
                Type sensorType = sensor.GetType();
                string kind = Convert.ToString(sensorType.GetProperty("SensorType").GetValue(sensor, null), CultureInfo.InvariantCulture);
                if (!kind.Equals("Temperature", StringComparison.OrdinalIgnoreCase)) continue;
                object raw = sensorType.GetProperty("Value").GetValue(sensor, null);
                if (raw == null) continue;
                double value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                string name = Convert.ToString(sensorType.GetProperty("Name").GetValue(sensor, null), CultureInfo.CurrentCulture);
                readings.Add(new TemperatureReading { Name = string.IsNullOrWhiteSpace(hardwareName) ? name : hardwareName + " — " + name, Celsius = value, Source = "LibreHardwareMonitor integrado" });
            }
            PropertyInfo subHardwareProperty = type.GetProperty("SubHardware");
            IEnumerable subHardware = subHardwareProperty == null ? null : subHardwareProperty.GetValue(hardware, null) as IEnumerable;
            if (subHardware != null) foreach (object child in subHardware) ReadHardware(child, readings);
        }

        private static void Enable(Type type, object instance, string propertyName)
        {
            PropertyInfo property = type.GetProperty(propertyName);
            if (property != null && property.CanWrite) property.SetValue(instance, true, null);
        }

        private static string FindLibrary()
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string direct = Path.Combine(baseDirectory, LibraryName);
            if (File.Exists(direct)) return direct;
            string sensors = Path.Combine(baseDirectory, "Sensors", LibraryName);
            return File.Exists(sensors) ? sensors : string.Empty;
        }
    }
}
