using System;
using System.Collections.Generic;
using System.Text;

namespace Infrastructure
{
    public class DateTimeHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static DateTime GetBeginTime(DateTime? dateTime, int days = 0)
        {
            if (dateTime == DateTime.MinValue || dateTime == null)
            {
                return DateTime.Now.AddDays(days);
            }
            return dateTime ?? DateTime.Now;
        }
        #region 时间戳转换

        /// <summary>
        ///  时间戳转本地时间-时间戳精确到秒
        /// </summary> 
        public static DateTime ToLocalTimeDateBySeconds(long unix)
        {
            var dto = DateTimeOffset.FromUnixTimeSeconds(unix);
            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        ///  时间转时间戳Unix-时间戳精确到秒
        /// </summary> 
        public static long ToUnixTimestampBySeconds(DateTime dt)
        {
            DateTimeOffset dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeSeconds();
        }

        /// <summary>
        ///  时间戳转本地时间-时间戳精确到毫秒
        /// </summary> 
        public static DateTime ToLocalTimeDateByMilliseconds(long unix)
        {
            var dto = DateTimeOffset.FromUnixTimeMilliseconds(unix);
            return dto.ToLocalTime().DateTime;
        }

        /// <summary>
        ///  时间转时间戳Unix-时间戳精确到毫秒
        /// </summary> 
        public static long ToUnixTimestampByMilliseconds(DateTime dt)
        {
            DateTimeOffset dto = new DateTimeOffset(dt);
            return dto.ToUnixTimeMilliseconds();
        }

        #endregion

        #region 毫秒转天时分秒
        /// <summary>
        /// 毫秒转天时分秒
        /// </summary>
        /// <param name="ms"></param>
        /// <returns></returns>
        public static string FormatTime(long ms)
        {
            int ss = 1000;
            int mi = ss * 60;
            int hh = mi * 60;
            int dd = hh * 24;

            long day = ms / dd;
            long hour = (ms - day * dd) / hh;
            long minute = (ms - day * dd - hour * hh) / mi;
            long second = (ms - day * dd - hour * hh - minute * mi) / ss;
            long milliSecond = ms - day * dd - hour * hh - minute * mi - second * ss;

            string sDay = day < 10 ? "0" + day : "" + day; //天
            string sHour = hour < 10 ? "0" + hour : "" + hour;//小时
            string sMinute = minute < 10 ? "0" + minute : "" + minute;//分钟
            string sSecond = second < 10 ? "0" + second : "" + second;//秒
            string sMilliSecond = milliSecond < 10 ? "0" + milliSecond : "" + milliSecond;//毫秒
            sMilliSecond = milliSecond < 100 ? "0" + sMilliSecond : "" + sMilliSecond;

            return string.Format("{0} 天 {1} 小时 {2} 分 {3} 秒", sDay, sHour, sMinute, sSecond);
        }
        #endregion

        #region 获取unix时间戳
        /// <summary>
        /// 获取unix时间戳
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long GetUnixTimeStamp(DateTime dt)
        {
            long unixTime = ((DateTimeOffset)dt).ToUnixTimeMilliseconds();
            return unixTime;
        }
        #endregion

        #region 获取日期天的最小时间
        public static DateTime GetDayMinDate(DateTime dt)
        {
            DateTime min = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0);
            return min;
        }
        #endregion

        #region 获取日期天的最大时间
        public static DateTime GetDayMaxDate(DateTime dt)
        {
            DateTime max = new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59);
            return max;
        }
        #endregion

        #region 获取日期天的最大时间
        public static string FormatDateTime(DateTime? dt)
        {
            if (dt != null)
            {
                if (dt.Value.Year == DateTime.Now.Year)
                {
                    return dt.Value.ToString("MM-dd HH:mm");
                }
                else
                {
                    return dt.Value.ToString("yyyy-MM-dd HH:mm");
                }
            }
            return string.Empty;
        }
        #endregion
    }
}
