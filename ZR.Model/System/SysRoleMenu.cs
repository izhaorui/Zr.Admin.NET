using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Text;

namespace ZR.Model.System
{
    /// <summary>
    /// 角色菜单
    /// </summary>
    [SugarTable("sys_role_menu")]
    [Tenant("0")]
    public class SysRoleMenu
    {
        [JsonProperty("roleId")]
        [SugarColumn(IsPrimaryKey = true)]
        public long Role_id { get; set; }
        [JsonProperty("menuId")]
        [SugarColumn(IsPrimaryKey = true)]
        public long Menu_id { get; set; }
        public DateTime Create_time { get; set; }
        public string Create_by { get; set; }
    }
}
