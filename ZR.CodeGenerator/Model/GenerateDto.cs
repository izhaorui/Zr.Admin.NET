using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator.Model
{
    public class GenerateDto
    {
        public long TableId { get; set; }
        public string[] QueryColumn { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public string dbName { get; set; }
        /// <summary>
        /// 项目命名空间
        /// </summary>
        //public string baseSpace { get; set; }
        /// <summary>
        /// 要生成代码的表
        /// </summary>
        public string tableName { get; set; }
        /// <summary>
        /// 要删除表名的字符串用
        /// </summary>
        //public string replaceTableNameStr { get; set; }
        /// <summary>
        /// 要生成的文件
        /// </summary>
        public int[] genFiles { get; set; }
        /// <summary>
        /// 如果目标文件存在，是否覆盖。默认为false
        /// </summary>
        public bool coverd { get; set; } = true;

        #region 存储路径
        //public string ModelPath { get; set; }
        //public string ServicePath { get; set; }
        //public string RepositoryPath { get; set; }
        //public string ApiPath { get; set; }
        //public string VuePath { get; set; }
        //public string VueApiPath { get; set; }

        //public string ParentPath { get; set; } = "..";
        /// <summary>
        /// 代码生成路径
        /// </summary>
        public string GenCodePath { get; set; }
        /// <summary>
        /// 代码生成压缩包路径
        /// </summary>
        public string ZipPath { get; set; }
        /// <summary>
        /// 代码生成压缩包名称
        /// </summary>
        public string ZipFileName { get; set; }
        #endregion
    }
}
