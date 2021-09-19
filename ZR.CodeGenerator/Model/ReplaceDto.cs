using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator.Model
{
    public class ReplaceDto
    {
        //public string ModelsNamespace { get; set; }
        //public string DtosNamespace { get; set; }
        //public string RepositoriesNamespace { get; set; }
        //public string IRepositoriesNamespace { get; set; }
        //public string IServicsNamespace { get; set; }
        //public string ServicesNamespace { get; set; }

        /// <summary>
        /// 主键字段
        /// </summary>
        public string PKName { get; set; }
        /// <summary>
        /// 主键类型
        /// </summary>
        public string PKType { get; set; }
        /// <summary>
        /// 控制器权限
        /// </summary>
        public string Permission { get; set; }
        /// <summary>
        /// C#类名
        /// </summary>
        public string ModelTypeName { get; set; }
        /// <summary>
        /// 数据库表名
        /// </summary>
        public string TableName { get; set; }
        /// <summary>
        /// 表描述、说明
        /// </summary>
        public string TableDesc { get; set; }
        public string updateColumn { get; set; }


        /// <summary>
        /// 实体属性模板
        /// </summary>
        public string ModelProperty { get; set; }
        /// <summary>
        /// 输入Dto模板
        /// </summary>
        public string InputDtoProperty { get; set; }

        //vue、api
        public string VueViewEditFormHtml { get; set; }
        /// <summary>
        /// 前端列表查询html
        /// </summary>
        public string VueViewListHtml { get; set; }
        /// <summary>
        /// 前端添加、编辑表格html
        /// </summary>
        public string VueViewFormHtml { get; set; }
        /// <summary>
        /// 前端搜索表单html
        /// </summary>
        public string VueQueryFormHtml { get; set; }
        public string VueJsMethod { get; set; }
        public string VueViewEditFormRuleContent { get; set; }
    }
}
