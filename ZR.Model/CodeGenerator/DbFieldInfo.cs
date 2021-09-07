using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ZR.Model
{
    /// <summary>
    /// 表的字段
    /// </summary>
    public class DbFieldInfo
    {
        public string TableName { get; set; }
        /// <summary>
        /// 初始化
        /// </summary>
        public DbFieldInfo()
        {
            FieldName = string.Empty;
            Description = string.Empty;
        }
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; }
        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// 系统数据类型，如 int
        /// </summary>
        public string DataType
        {
            get;
            set;
        }

        /// <summary>
        /// 数据库里面存放的类型。
        /// </summary>
        public string FieldType { get; set; }

        /// <summary>
        /// 代表小数位精度。
        /// </summary>
        public long? FieldScale { get; set; }
        /// <summary>
        /// 数据精度，仅数字类型有效，总共多少位数字（10进制）。
        /// 在MySql里面代表了字段长度
        /// </summary>
        public long? FieldPrecision { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public long? FieldMaxLength { get; set; }

        /// <summary>
        /// 可空
        /// </summary>
        public bool IsNullable { get; set; }
        /// <summary>
        /// 是否为主键字段
        /// </summary>
        public bool IsIdentity { get; set; }
        /// <summary>
        /// 【未用上】该字段是否自增
        /// </summary>
        public bool Increment { get; set; }


        /// <summary>
        /// 默认值
        /// </summary>
        public string FieldDefaultValue { get; set; }

    }
}
