using System.ComponentModel.DataAnnotations;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 邮件模板查询对象
    /// </summary>
    public class EmailTplQueryDto : PagerInfo 
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// 邮件模板输入输出对象
    /// </summary>
    public class EmailTplDto
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "Name不能为空")]
        public string Name { get; set; }

        [Required(ErrorMessage = "模板内容不能为空")]
        public string Content { get; set; }

        public string CreateBy { get; set; }

        public DateTime? CreateTime { get; set; }

        public string UpdateBy { get; set; }

        public DateTime? UpdateTime { get; set; }

        public string Remark { get; set; }



    }
}