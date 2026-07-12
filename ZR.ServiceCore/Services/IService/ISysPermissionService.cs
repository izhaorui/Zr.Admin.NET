using ZR.Model.System;
using ZR.Model.System.Dto;

namespace ZR.ServiceCore.Services
{
    public interface ISysPermissionService
    {
        public List<string> GetRolePermission(SysUserDto user);
        public List<string> GetMenuPermission(SysUserDto user);
    }
}

