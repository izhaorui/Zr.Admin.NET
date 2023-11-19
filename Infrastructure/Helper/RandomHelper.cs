using System;
using System.Text;

namespace ZR.Infrastructure.Helper
{
    public class RandomHelper
    {
        /// <summary>
        /// 生成n为验证码
        /// </summary>
        /// <param name="Length"></param>
        /// <returns></returns>
        public static string GenerateNum(int Length)
        {
            char[] constant = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            StringBuilder newRandom = new(constant.Length);
            Random rd = new();
            for (int i = 0; i < Length; i++)
            {
                newRandom.Append(constant[rd.Next(constant.Length - 1)]);
            }
            return newRandom.ToString();
        }
    }
}
