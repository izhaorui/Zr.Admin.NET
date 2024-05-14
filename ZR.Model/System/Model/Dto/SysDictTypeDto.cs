using System.ComponentModel.DataAnnotations;

namespace ZR.Model.System.Dto
{
    public class SysDictTypeDto
    {
        public long DictId { get; set; }
        /// <summary>
        /// 字典名称
        /// </summary>
        public string DictName { get; set; }
        /// <summary>
        /// 字典类型
        /// </summary>
        [Required(ErrorMessage = "字典类型不能为空")]
        [RegularExpression(pattern: "^[a-z][a-z0-9_]*$", ErrorMessage = "字典类型必须以字母开头,且字典类型只能由小写字母或加下划线还有数字组成")]
        public string DictType { get; set; }
        public string Status { get; set; }
        /// <summary>
        /// 系统内置 Y是 N否
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// 自定义sql
        /// </summary>
        public string CustomSql { get; set; }
    }
}
