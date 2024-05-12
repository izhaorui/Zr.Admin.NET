using System;
using System.Collections.Generic;

namespace ZR.Model.System.Dto
{
    public class SysRoleMenuDto
    {
        public long RoleId { get; set; }
        /// <summary>
        /// 角色分配菜单
        /// </summary>
        public List<long> MenuIds { get; set; } = new List<long>();
        public string RoleName { get; set; }
        public string RoleKey { get; set; }
        public string Create_by { get; set; }
        public DateTime Create_time{ get; set; }
    }
}
