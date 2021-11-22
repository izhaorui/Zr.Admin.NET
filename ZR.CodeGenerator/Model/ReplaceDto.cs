using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZR.CodeGenerator.Model
{
    public class ReplaceDto
    {
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
        /// 修改列
        /// </summary>
        public string UpdateColumn { get; set; }
        /// <summary>
        /// 插入列
        /// </summary>
        public string InsertColumn { get; set; }


        /// <summary>
        /// 实体属性模板
        /// </summary>
        public string ModelProperty { get; set; }
        /// <summary>
        /// 输入Dto模板
        /// </summary>
        public string InputDtoProperty { get; set; }

        //vue、api
        public string VueViewFormResetHtml { get; set; }
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
        /// <summary>
        /// vue js方法
        /// </summary>
        public string VueJsMethod { get; set; }
        /// <summary>
        /// vue 添加、编辑表单规则
        /// </summary>
        public string VueViewEditFormRuleContent { get; set; }
        /// <summary>
        /// 查询条件
        /// </summary>
        public string QueryCondition { get; set; }
        /// <summary>
        /// 查询属性
        /// </summary>
        public string QueryProperty { get; set; }
        /// <summary>
        /// vue data内容
        /// </summary>
        public string VueDataContent { get; set; }
        /// <summary>
        /// vue mounted 方法
        /// </summary>
        public string MountedMethod { get; set; }
        /// <summary>
        /// views、js文件名
        /// </summary>
        public string ViewsFileName { get; set; }
        /// <summary>
        /// vue组件引用
        /// </summary>
        public string VueComponent { get; set; } = "";
        /// <summary>
        /// vue组件导入
        /// </summary>
        public string VueComponentImport { get; set; } = "";
        public string Author { get; set; }
        public string AddTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}
