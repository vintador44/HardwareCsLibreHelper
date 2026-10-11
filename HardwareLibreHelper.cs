using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LibreHardwareMonitor.Hardware;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var report = CollectAllMetrics();
            string jsonResult = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            
            
            Console.WriteLine(jsonResult);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(new { error = ex.Message }));
            Environment.Exit(1);
        }
    }

    static Dictionary<string, object> CollectAllMetrics()
    {
        var data = new Dictionary<string, object>();

        var computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true
        };

        computer.Open();

        var cpuList = new List<object>();
        var gpuList = new List<object>();
        var ramData = new Dictionary<string, object>();
        var storageList = new List<object>();
        var networkList = new List<object>();

        foreach (var hardware in computer.Hardware)
        {
            hardware.Update();

            switch (hardware.HardwareType)
            {
                case HardwareType.Cpu:
                    float? cpuTemp = null;
                    float? cpuLoad = null;

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && (sensor.Name.Contains("Package") || sensor.Name.Contains("Core (Max)")))
                            cpuTemp = sensor.Value;
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Total"))
                            cpuLoad = sensor.Value;
                    }

                    cpuList.Add(new { Name = hardware.Name, Temperature = cpuTemp, Load = cpuLoad });
                    break;

                case HardwareType.GpuNvidia:
                case HardwareType.GpuAmd:
                case HardwareType.GpuIntel:
                    float? gpuTemp = null;
                    float? gpuLoad = null;

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                            gpuTemp = sensor.Value;
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                            gpuLoad = sensor.Value;
                    }

                    gpuList.Add(new { Name = hardware.Name, Temperature = gpuTemp, Load = gpuLoad });
                    break;

                case HardwareType.Memory:
                    float? ramUsed = null;
                    float? ramAvailable = null;
                    float? ramLoad = null;

                    foreach (var sensor in hardware.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                            ramUsed = sensor.Value;
                        if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                            ramAvailable = sensor.Value;
                        if (sensor.SensorType == SensorType.Load)
                            ramLoad = sensor.Value;
                    }

                    ramData = new Dictionary<string, object> {
                        { "Name", hardware.Name },
                        { "UsedGB", ramUsed },
                        { "AvailableGB", ramAvailable },
                        { "LoadPercent", ramLoad }
                    };
                    break;
            }
        }

        
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (drive.IsReady)
            {
                storageList.Add(new
                {
                    Name = drive.Name,
                    VolumeLabel = drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    TotalSpaceGB = Math.Round((double)drive.TotalSize / 1024 / 1024 / 1024, 2),
                    FreeSpaceGB = Math.Round((double)drive.TotalFreeSpace / 1024 / 1024 / 1024, 2),
                    UsedSpaceGB = Math.Round((double)(drive.TotalSize - drive.TotalFreeSpace) / 1024 / 1024 / 1024, 2)
                });
            }
        }

        
        foreach (var hardware in computer.Hardware)
        {
            if (hardware.HardwareType == HardwareType.Network)
            {
                hardware.Update();
                float uploadSpeed = 0;
                float downloadSpeed = 0;

                foreach (var sensor in hardware.Sensors)
                {
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Upload"))
                        uploadSpeed = sensor.Value ?? 0;
                    if (sensor.SensorType == SensorType.Throughput && sensor.Name.Contains("Download"))
                        downloadSpeed = sensor.Value ?? 0;
                }

                networkList.Add(new
                {
                    Name = hardware.Name,
                    UploadBytesPerSec = uploadSpeed,
                    DownloadBytesPerSec = downloadSpeed
                });
            }
        }

        computer.Close();

        data["CPU"] = cpuList;
        data["GPU"] = gpuList;
        data["RAM"] = ramData;
        data["Storage"] = storageList;
        data["Network"] = networkList;

        return data;
    }
}
