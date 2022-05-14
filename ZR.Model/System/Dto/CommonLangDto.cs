using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ZR.Model.Dto
{
    /// <summary>
    /// 多语言配置输入对象
    /// </summary>
    public class CommonLangDto
    {
        //[Required(ErrorMessage = "id不能为空")]
        public long Id { get; set; }
        //[Required(ErrorMessage = "语言code不能为空")]
        public string LangCode { get; set; }
        public string LangKey { get; set; }
        //[Required(ErrorMessage = "名称不能为空")]
        public string LangName { get; set; }
        public List<CommonLangDto> LangList { get; set; }
    }

    /// <summary>
    /// 多语言配置查询对象
    /// </summary>
    public class CommonLangQueryDto : PagerInfo 
    {
        public string LangCode { get; set; }
        public string LangKey { get; set; }
        public DateTime? BeginAddtime { get; set; }
        public DateTime? EndAddtime { get; set; }
        public int ShowMode { get; set; }
    }
}
