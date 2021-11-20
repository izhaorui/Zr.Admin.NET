using Infrastructure.Attribute;
using Infrastructure.Enums;
using Infrastructure.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using ZR.Admin.WebApi.Filters;
using ZR.Model;
using ZR.Model.System;
using ZR.Model.Vo;
using ZR.Service.System.IService;

namespace ZR.Admin.WebApi.Controllers.System
{
    //[Verify]
    [Route("system/user")]
    public class SysUserController : BaseController
    {
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ISysUserService UserService;
        private readonly ISysRoleService RoleService;
        private readonly ISysPostService PostService;
        private readonly ISysUserPostService UserPostService;
        private IWebHostEnvironment WebHostEnvironment;

        public SysUserController(
            ISysUserService userService,
            ISysRoleService roleService,
            ISysPostService postService,
            ISysUserPostService userPostService,
            IWebHostEnvironment HostEnvironment)
        {
            UserService = userService;
            RoleService = roleService;
            PostService = postService;
            UserPostService = userPostService;
            WebHostEnvironment = HostEnvironment;
        }

        /// <summary>
        /// 用户管理 -> 获取用户
        /// /system/user/list
        /// </summary>
        /// <returns></returns>
        [ActionPermissionFilter(Permission = "system:user:list")]
        [HttpGet("list")]
        public IActionResult List([FromQuery] SysUser user, PagerInfo pager)
        {
            var list = UserService.SelectUserList(user, pager);

            var vm = new VMPageResult<SysUser>(list, pager);

            return SUCCESS(vm, TIME_FORMAT_FULL);
        }

        /// <summary>
        /// 用户管理 -> 编辑、添加用户获取用户，信息查询
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        [HttpGet("")]
        [HttpGet("{userId:int=0}")]
        public IActionResult GetInfo(int userId)
        {
            Dictionary<string, object> dic = new();
            var roles = RoleService.SelectRoleAll();
            dic.Add("roles", roles);
            dic.Add("posts", PostService.GetAll());

            //编辑
            if (userId > 0)
            {
                dic.Add("user", UserService.SelectUserById(userId));
                dic.Add("postIds", UserPostService.GetUserPostsByUserId(userId));
                dic.Add("roleIds", RoleService.SelectUserRoles(userId));
            }

            return ToResponse(ApiResult.Success(dic));
        }

        /// <summary>
        /// 添加用户
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPost("edit")]
        [Log(Title = "用户管理", BusinessType = BusinessType.INSERT)]
        [ActionPermissionFilter(Permission = "system:user:add")]
        public IActionResult AddUser([FromBody] SysUser user)
        {
            if (user == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }
            if (UserConstants.NOT_UNIQUE.Equals(UserService.CheckUserNameUnique(user.UserName)))
            {
                return ToResponse(ApiResult.Error($"新增用户 '{user.UserName}'失败，登录账号已存在"));
            }

            user.Create_by = User.Identity.Name;
            user.Password = NETCore.Encrypt.EncryptProvider.Md5(user.Password);

            return ToResponse(UserService.InsertUser(user));
        }

        /// <summary>
        /// 修改用户
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPut("edit")]
        [Log(Title = "用户管理", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:edit")]
        public IActionResult UpdateUser([FromBody] SysUser user)
        {
            if (user == null || user.UserId <= 0) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            user.Update_by = User.Identity.Name;
            int upResult = UserService.UpdateUser(user);

            return ToResponse(upResult);
        }

        /// <summary>
        /// 改变用户状态
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpPut("changeStatus")]
        [Log(Title = "修改用户状态", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:update")]
        public IActionResult ChangeStatus([FromBody] SysUser user)
        {
            if (user == null) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int result = UserService.ChangeUserStatus(user);
            return ToResponse(ToJson(result));
        }

        /// <summary>
        /// 删除用户
        /// </summary>
        /// <param name="userid"></param>
        /// <returns></returns>
        [HttpDelete("{userId}")]
        [Log(Title = "用户管理", BusinessType = BusinessType.DELETE)]
        [ActionPermissionFilter(Permission = "system:user:remove")]
        public IActionResult Remove(int userid = 0)
        {
            if (userid <= 0) { return ToResponse(ApiResult.Error(101, "请求参数错误")); }

            int result = UserService.DeleteUser(userid);

            return ToResponse(ToJson(result));
        }

        /// <summary>
        /// 重置密码
        /// </summary>
        /// <returns></returns>
        [HttpPut("resetPwd")]
        [Log(Title = "重置密码", BusinessType = BusinessType.UPDATE)]
        [ActionPermissionFilter(Permission = "system:user:update")]
        public IActionResult ResetPwd([FromBody] SysUser sysUser)
        {
            //密码md5
            sysUser.Password = NETCore.Encrypt.EncryptProvider.Md5(sysUser.Password);

            int result = UserService.ResetPwd(sysUser.UserId, sysUser.Password);
            return ToResponse(ToJson(result));
        }

        ///// <summary>
        ///// 导入 ok
        ///// </summary>
        ///// <param name="formFile">使用IFromFile必须使用name属性否则获取不到文件</param>
        ///// <returns></returns>
        //[HttpPost("importData")]
        //[Log(Title = "用户导入", BusinessType = BusinessType.IMPORT)]
        //[ActionPermissionFilter(Permission = "system:user:import")]
        //public IActionResult ImportData([FromForm(Name = "file")] IFormFile formFile)
        //{
        //    var mapper = new Mapper(formFile.OpenReadStream());// 从流获取
        //    //读取的sheet信息
        //    var rows = mapper.Take<SysUser>(0);
        //    foreach (var item in rows)
        //    {
        //        SysUser u = item.Value;
        //    }
        //    //TODO 业务逻辑
        //    return SUCCESS(1);
        //}

        ///// <summary>
        ///// 用户模板 ok
        ///// </summary>
        ///// <returns></returns>
        //[HttpGet("importTemplate")]
        //[Log(Title = "用户模板", BusinessType = BusinessType.EXPORT)]
        //[ActionPermissionFilter(Permission = "system:user:export")]
        //public IActionResult ImportTemplateExcel()
        //{
        //    List<SysUser> user = new List<SysUser>();
        //    var mapper = new Mapper();
        //    MemoryStream stream = new MemoryStream();
        //    mapper.Save(stream, user, "sheel1", overwrite: true, xlsx: true);
        //    //Response.Headers.Append("content-disposition", "attachment;filename=sysUser.xlsx");
        //    return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sysUser.xlsx");
        //}

        /// <summary>
        /// 用户导出
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        [HttpGet("export")]
        [Log(Title = "用户导出", BusinessType = BusinessType.EXPORT)]
        public IActionResult UserExport([FromQuery] SysUser user)
        {
            string sFileName = $"用户列表{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            string newFileName = Path.Combine(WebHostEnvironment.WebRootPath, "export", sFileName);
            var list = UserService.SelectUserList(user, new PagerInfo(1, 10000));

            //调试模式需要加上
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            Directory.CreateDirectory(Path.GetDirectoryName(newFileName));
            using (ExcelPackage package = new ExcelPackage(new FileInfo(newFileName)))
            {
                // 添加worksheet
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("sysuser");

                //添加头
                worksheet.Cells[1, 1].Value = "用户id";
                worksheet.Cells[1, 2].Value = "用户名称";
                worksheet.Cells[1, 3].Value = "用户昵称";
                worksheet.Cells[1, 4].Value = "部门";
                worksheet.Cells[1, 5].Value = "手机号码";
                worksheet.Cells[1, 6].Value = "性别";
                worksheet.Cells[1, 7].Value = "状态";
                worksheet.Cells[1, 8].Value = "添加时间";
                worksheet.Cells[1, 9].Value = "登录IP";
                worksheet.Cells[1, 10].Value = "最后登录时间";
                for (int i = 0; i < list.Count; i++)
                {
                    var item = list[i];
                    worksheet.Cells[$"A{i + 2}"].Value = item.UserId;
                    worksheet.Cells[$"B{i + 2}"].Value = item.UserName;
                    worksheet.Cells[$"C{i + 2}"].Value = item.NickName;
                    worksheet.Cells[$"D{i + 2}"].Value = item.DeptName;
                    worksheet.Cells[$"E{i + 2}"].Value = item.Phonenumber;                    
                    worksheet.Cells[$"F{i + 2}"].Value = item.Sex;
                    worksheet.Cells[$"G{i + 2}"].Value = item.Status;
                    worksheet.Cells[$"H{i + 2}"].Value = item.Create_time.ToString();
                    worksheet.Cells[$"I{i + 2}"].Value = item.LoginIP;
                    worksheet.Cells[$"J{i + 2}"].Value = item.LoginDate.ToString();
                }
                package.Save();
            }
            return SUCCESS(new { zipPath = "/export/" + sFileName, fileName = sFileName });
        }
    }
}
