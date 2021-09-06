using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ZR.Model
{
    /// <summary>
    /// 数据表的信息
    /// </summary>
    public class DbTableInfo
    {
        /// <summary>
        /// 表格ID，表的名称。
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// 表的别称，或者描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 字段列表
        /// </summary>
        public List<DbFieldInfo> Fileds { get; set; }
        /// <summary>
        /// 初始化
        /// </summary>
        public DbTableInfo()
        {
            Fileds = new List<DbFieldInfo>();
        }

        /// <summary>
        /// 获取主键的名称列表。
        /// </summary>
        /// <returns></returns>
        public List<string> GetIdentityList()
        {
            var list = Fileds.Where(x => x.IsIdentity);
            if (list == null) return null;
            return list.Select(x => x.FieldName).ToList();
        }
        /// <summary>
        /// 获取主键字段列表
        /// </summary>
        /// <returns></returns>
        public List<DbFieldInfo> GetIdentityFields()
        {
            var list = Fileds.Where(x => x.IsIdentity);
            if (list == null) return null;
            return list.ToList();
        }
        /// <summary>
        /// 获取可空字段。
        /// </summary>
        /// <returns></returns>
        public List<DbFieldInfo> GetIsNullableFields()
        {
            var list = Fileds.Where(x => x.IsNullable);
            if (list == null) return null;
            return list.ToList();
        }
        /// <summary>
        /// 获取不可空字段。
        /// </summary>
        /// <returns></returns>
        public List<DbFieldInfo> GetNotNullableFields()
        {
            var list = Fileds.Where(x => !x.IsNullable);
            if (list == null) return null;
            return list.ToList();
        }
    }
}
