using Infrastructure.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Infrastructure
{
    public class ComputerHelper
    {
        /// <summary>
        /// 内存使用情况
        /// </summary>
        /// <returns></returns>
        public static MemoryMetrics GetComputerInfo()
        {
            try
            {
                MemoryMetricsClient client = new();
                MemoryMetrics memoryMetrics = client.GetMetrics();
                memoryMetrics.FreeRam = Math.Ceiling(memoryMetrics.Free / 1024) + "GB";
                memoryMetrics.UsedRam = Math.Ceiling(memoryMetrics.Used / 1024) + "GB";
                memoryMetrics.TotalRAM = Math.Ceiling(memoryMetrics.Total / 1024).ToString() + " GB";
                memoryMetrics.RAMRate = Math.Ceiling(100 * memoryMetrics.Used / memoryMetrics.Total).ToString() + "%";
                memoryMetrics.CPURate = Math.Ceiling(GetCPURate().ParseToDouble()) + "%";
                return memoryMetrics;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return null;
        }

        /// <summary>
        /// 获取内存大小
        /// </summary>
        /// <returns></returns>
        public static List<DiskInfo> GetDiskInfos()
        {
            List<DiskInfo> diskInfos = new();

            var driv = DriveInfo.GetDrives();
            foreach (var item in driv)
            {
                var obj = new DiskInfo()
                {
                    DickName = item.Name,
                    TypeName = item.DriveType.ToString(),
                    TotalFree = item.TotalFreeSpace / 1024 / 1024 / 1024,
                    TotalSize = item.TotalSize / 1024 / 1024 / 1024,
                    AvailableFreeSpace = item.AvailableFreeSpace / 1024 / 1024 / 1024,
                };
                obj.AvailablePercent =  decimal.Ceiling(((decimal)(obj.TotalSize - obj.AvailableFreeSpace) /(decimal)obj.TotalSize) * 100);
                diskInfos.Add(obj);
            }
            return diskInfos;
        }

        public static bool IsUnix()
        {
            var isUnix = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            return isUnix;
        }

        public static string GetCPURate()
        {
            string cpuRate;
            if (IsUnix())
            {
                string output = ShellHelper.Bash("top -b -n1 | grep \"Cpu(s)\" | awk '{print $2 + $4}'");
                cpuRate = output.Trim();
            }
            else
            {
                string output = ShellHelper.Cmd("wmic", "cpu get LoadPercentage");
                cpuRate = output.Replace("LoadPercentage", string.Empty).Trim();
            }
            return cpuRate;
        }

        public static string GetRunTime()
        {
            string runTime = string.Empty;
            try
            {
                if (IsUnix())
                {
                    string output = ShellHelper.Bash("uptime -s");
                    output = output.Trim();
                    runTime = DateTimeHelper.FormatTime((DateTime.Now - output.ParseToDateTime()).TotalMilliseconds.ToString().Split('.')[0].ParseToLong());
                }
                else
                {
                    string output = ShellHelper.Cmd("wmic", "OS get LastBootUpTime/Value");
                    string[] outputArr = output.Split('=', (char)StringSplitOptions.RemoveEmptyEntries);
                    if (outputArr.Length == 2)
                    {
                        runTime = DateTimeHelper.FormatTime((DateTime.Now - outputArr[1].Split('.')[0].ParseToDateTime()).TotalMilliseconds.ToString().Split('.')[0].ParseToLong());
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return runTime;
        }
    }

    /// <summary>
    /// 内存信息
    /// </summary>
    public class MemoryMetrics
    {
        public double Total { get; set; }
        public double Used { get; set; }
        public double Free { get; set; }

        public string UsedRam { get; set; }
        /// <summary>
        /// CPU使用率%
        /// </summary>
        public string CPURate { get; set; }
        /// <summary>
        /// 总内存 GB
        /// </summary>
        public string TotalRAM { get; set; }
        /// <summary>
        /// 内存使用率 %
        /// </summary>
        public string RAMRate { get; set; }
        /// <summary>
        /// 空闲内存
        /// </summary>
        public string FreeRam { get; set; }
    }

    public class DiskInfo
    {
        /// <summary>
        /// 磁盘名
        /// </summary>
        public string DickName { get; set; }
        public string TypeName { get; set; }
        public long TotalFree { get; set; }
        public long TotalSize { get; set; }
        /// <summary>
        /// 可使用
        /// </summary>
        public long AvailableFreeSpace { get; set; }
        public decimal AvailablePercent { get; set; }
    }

    public class MemoryMetricsClient
    {
        public MemoryMetrics GetMetrics()
        {
            if (ComputerHelper.IsUnix())
            {
                return GetUnixMetrics();
            }
            return GetWindowsMetrics();
        }

        /// <summary>
        /// windows系统获取内存信息
        /// </summary>
        /// <returns></returns>
        private MemoryMetrics GetWindowsMetrics()
        {
            string output = ShellHelper.Cmd("wmic", "OS get FreePhysicalMemory,TotalVisibleMemorySize /Value");

            var lines = output.Trim().Split('\n');
            var freeMemoryParts = lines[0].Split('=', (char)StringSplitOptions.RemoveEmptyEntries);
            var totalMemoryParts = lines[1].Split('=', (char)StringSplitOptions.RemoveEmptyEntries);

            var metrics = new MemoryMetrics();
            metrics.Total = Math.Round(double.Parse(totalMemoryParts[1]) / 1024, 0);
            metrics.Free = Math.Round(double.Parse(freeMemoryParts[1]) / 1024, 0);
            metrics.Used = metrics.Total - metrics.Free;

            return metrics;
        }

        /// <summary>
        /// windows 获取磁盘信息
        /// </summary>
        private void GetWindowsDiskInfo()
        {
            string output = ShellHelper.Cmd("wmic", "diskdrive get Size /Value");

            Console.WriteLine(output);
        }

        /// <summary>
        /// Unix系统获取
        /// </summary>
        /// <returns></returns>
        private MemoryMetrics GetUnixMetrics()
        {
            string output = ShellHelper.Bash("free -m");

            var lines = output.Split('\n');
            var memory = lines[1].Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);

            var metrics = new MemoryMetrics();
            metrics.Total = double.Parse(memory[1]);
            metrics.Used = double.Parse(memory[2]);
            metrics.Free = double.Parse(memory[3]);

            return metrics;
        }
    }

}