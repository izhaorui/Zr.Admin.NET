using System;
using System.Collections.Generic;

namespace ZR.Model.Models
{
    /// <summary>
    /// ，数据实体对象
    /// </summary>
    [SqlSugar.SugarTable("gen_demo")]
    public class Gendemo
    {
        /// <summary>
        /// 描述 :自增id
        /// 空值 :False
        /// 默认 :
        /// </summary>
        [SqlSugar.SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        public int? Id { get; set; }
        /// <summary>
        /// 描述 :名称
        /// 空值 :False
        /// 默认 :
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 描述 :图片
        /// 空值 :True
        /// 默认 :
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// 描述 :显示状态
        /// 空值 :False
        /// 默认 :
        /// </summary>
        public int? ShowStatus { get; set; }
        /// <summary>
        /// 描述 :添加时间
        /// 空值 :True
        /// 默认 :
        /// </summary>
        public DateTime? AddTime { get; set; }

    }
}
