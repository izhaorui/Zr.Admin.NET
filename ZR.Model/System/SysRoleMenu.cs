using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 角色菜单
    /// </summary>
    [SqlSugar.SugarTable("sys_role_menu")]
    public class SysRoleMenu
    {
        [JsonProperty("roleId")]
        [SqlSugar.SugarColumn(IsPrimaryKey = true)]
        public long Role_id { get; set; }
        [JsonProperty("menuId")]
        [SqlSugar.SugarColumn(IsPrimaryKey = true)]
        public long Menu_id { get; set; }
        public DateTime Create_time { get; set; }
        public string Create_by { get; set; }
    }
}
