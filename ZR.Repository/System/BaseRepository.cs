using Infrastructure.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZR.Repository.DbProvider;

namespace ZR.Repository.System
{
    public class BaseRepository : SugarDbContext
    {
        protected void PrintLog()
        {
            //调式代码 用来打印SQL 
            Db.Aop.OnLogExecuting = (sql, pars) =>
            {
                Console.WriteLine("【SQL语句】" + sql.ToLower() + "\r\n" + Db.Utilities.SerializeObject(pars.ToDictionary(it => it.ParameterName, it => it.Value)));
            };
        }
    }
}
