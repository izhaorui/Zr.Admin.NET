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
        //public string[] QueryColumn { get; set; }
        /// <summary>
        /// 是否预览代码
        /// </summary>
        public int IsPreview { get; set; }
        /// <summary>
        /// 要生成的文件
        /// </summary>
        public int[] GenCodeFiles { get; set; }
        /// <summary>
        /// 如果目标文件存在，是否覆盖。默认为false
        /// </summary>
        public bool Coverd { get; set; } = true;
        /// <summary>
        /// 生成代码的数据库类型 0、mysql 1、sqlserver
        /// </summary>
        public int DbType { get; set; }
        public GenTable GenTable { get; set; }
        #region 存储路径
        /// <summary>
        /// 代码模板预览存储路径存放
        /// </summary>
        public List<GenCode> GenCodes { get; set; } = new List<GenCode>();
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

    public class GenCode
    {
        public int Type { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public string Content { get; set; }

        public GenCode(int type, string title, string path, string content)
        {
            Type = type;
            Title = title;
            Path = path;
            Content = content;
        }
    }
}
