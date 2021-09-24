using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZR.Model.System.Generate;

namespace ZR.CodeGenerator.Model
{
    public class GenerateDto
    {
        public long TableId { get; set; }
        public string[] QueryColumn { get; set; }

        /// <summary>
        /// 要生成的文件
        /// </summary>
        public int[] genFiles { get; set; }
        /// <summary>
        /// 如果目标文件存在，是否覆盖。默认为false
        /// </summary>
        public bool coverd { get; set; } = true;
        /// <summary>
        /// 生成代码的数据库类型 0、mysql 1、sqlserver
        /// </summary>
        public int DbType { get; set; }
        public GenTable GenTable { get; set; }
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
