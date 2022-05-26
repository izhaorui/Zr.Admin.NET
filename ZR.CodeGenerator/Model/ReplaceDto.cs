using System;

namespace ZR.CodeGenerator.Model
{
    public class ReplaceDto
    {
        /// <summary>
        /// 主键字段
        /// </summary>
        public string PKName { get; set; }
        /// <summary>
        /// 首字母小写主键
        /// </summary>
        public string FistLowerPk{ get; set; }
        /// <summary>
        /// 主键类型
        /// </summary>
        public string PKType { get; set; }
        /// <summary>
        /// 控制器权限
        /// </summary>
        public string PermissionPrefix { get; set; }
        /// <summary>
        /// C#类名
        /// </summary>
        public string ModelTypeName { get; set; }
        //vue、api
        //public string VueViewFormResetHtml { get; set; }
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
        /// 查询条件
        /// </summary>
        public string QueryCondition { get; set; } = "";
        public bool ShowBtnExport { get; set; }
        public bool ShowBtnAdd { get; set; }
        public bool ShowBtnEdit { get; set; }
        public bool ShowBtnDelete { get; set; }
        public bool ShowBtnView { get; set; }
        /// <summary>
        /// 上传URL data
        /// </summary>
        //public string VueUploadUrl { get; set; }
        public int UploadFile { get; set; } = 0;
        public string Author { get; set; }
        public string AddTime { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }
}
