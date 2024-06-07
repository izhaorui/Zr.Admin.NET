using IP2Region.Net.XDB;
using System;
using System.IO;
using ZR.Infrastructure.IPTools.Model;

namespace ZR.Infrastructure.IPTools
{
    public class IpTool
    {
        private static readonly string DbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ip2region.xdb");
        private static readonly Searcher Searcher;
        static IpTool()
        {
            if (!File.Exists(DbPath))
            {
                throw new Exception($"IP initialize failed. Can not find database file from {DbPath}. Please download the file to your application root directory, then set it can be copied to the output directory. Url: https://gitee.com/lionsoul/ip2region/blob/master/data/ip2region.xdb");
            }

            Searcher = new Searcher(CachePolicy.File, DbPath);
        }

        public static string GetRegion(string ip)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new ArgumentException("IP为空", nameof(ip));
            }

            try
            {
                var region = Searcher.Search(ip);
                return region;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception($"搜索IP异常IP={ip}", ex);
            }
        }

        public static IpInfo Search(string ip)
        {
            try
            {
                var region = GetRegion(ip);
                var array = region.Split("|");
                var info = new IpInfo()
                {
                    Country = array[0],
                    Province = array[2],
                    City = array[3],
                    NetworkOperator = array[4],
                    IpAddress = ip
                };
                return info;
            }
            catch (Exception e)
            {
                throw new Exception("Error converting ip address information to ipinfo object", e);
            }
        }
    }
}
