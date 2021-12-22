﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ZR.Common
{
    public class Tools
    {
        /// <summary>
        /// 要分割的字符串 eg: 1,3,10,00
        /// </summary>
        /// <param name="infoIds"></param>
        /// <returns></returns>
        public static long[] SpitLongArrary(string str)
        {
            if (string.IsNullOrEmpty(str)) { return Array.Empty<long>(); }
            str = str.TrimStart(',').TrimEnd(',');
            string[] strIds = str.Split(',', (char)StringSplitOptions.RemoveEmptyEntries);
            long[] infoIdss = Array.ConvertAll(strIds, s => long.Parse(s));
            return infoIdss;
        }

        public static int[] SpitIntArrary(string str)
        {
            if (string.IsNullOrEmpty(str)) { return Array.Empty<int>(); }
            string[] strIds = str.Split(',', (char)StringSplitOptions.RemoveEmptyEntries);
            int[] infoIdss = Array.ConvertAll(strIds, s => int.Parse(s));
            return infoIdss;
        }

        /// <summary>
        /// 根据日期获取星期几
        /// </summary>
        public static string GetWeekByDate(DateTime dt)
        {
            var day = new[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            return day[Convert.ToInt32(dt.DayOfWeek.ToString("d"))];
        }

        /// <summary>
        /// 得到这个月的第几周
        /// </summary>
        /// <param name="daytime">年月日</param>
        /// <returns>传递过来的时间是第几周</returns>
        public static int GetWeekNumInMonth(DateTime daytime)
        {
            int dayInMonth = daytime.Day;
            //本月第一天
            DateTime firstDay = daytime.AddDays(1 - daytime.Day);
            //本月第一天是周几
            int weekday = (int)firstDay.DayOfWeek == 0 ? 7 : (int)firstDay.DayOfWeek;
            //本月第一周有几天
            int firstWeekEndDay = 7 - (weekday - 1);
            //当前日期和第一周之差
            int diffday = dayInMonth - firstWeekEndDay;
            diffday = diffday > 0 ? diffday : 1;
            //当前是第几周,如果整除7就减一天
            int weekNumInMonth = ((diffday % 7) == 0
                ? (diffday / 7 - 1)
                : (diffday / 7)) + 1 + (dayInMonth > firstWeekEndDay ? 1 : 0);
            return weekNumInMonth;
        }
        /// <summary>
        /// 判断一个字符串是否为url
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsUrl(string str)
        {
            try
            {
                string Url = @"^http(s)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$";
                return Regex.IsMatch(str, Url);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
