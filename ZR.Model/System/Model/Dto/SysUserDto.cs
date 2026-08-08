namespace ZR.Model.System.Dto
{
    public class SysUserDto
    {
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Email { get; set; }
        public string Remark { get; set; }
        public string Phonenumber { get; set; }
        public string Avatar { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }
        public string Password { get; set; }

        /// <summary>
        /// 帐号状态（0正常 1停用）
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        public string LoginIP { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LoginDate { get; set; }

        /// <summary>
        /// 部门Id
        /// </summary>
        public long DeptId { get; set; }

        /// <summary>
        /// 上级主管用户Id
        /// </summary>
        public long? LeaderId { get; set; }

        /// <summary>
        /// 上级主管昵称（查询时实时回查填充，仅用于展示，不入库）
        /// </summary>
        public string LeaderName { get; set; }

        public string WelcomeContent { get; set; }

        /// <summary>
        /// 密码最后更新时间
        /// </summary>
        public DateTime? PwdUpdateTime { get; set; }
        
        public DateTime? Create_time { get; set; }
        public string Create_by { get; set; }
        public DateTime? Update_time { get; set; }
        public string Update_by { get; set; }

        #region 表额外字段

        [SugarColumn(IsIgnore = true)]
        public bool IsAdmin
        {
            get
            {
                return UserId == 1;
            }
        }
        /// <summary>
        /// 拥有角色个数
        /// </summary>
        //[SugarColumn(IsIgnore = true)]
        //public int RoleNum { get; set; }
        [SugarColumn(IsIgnore = true)]
        public string DeptName { get; set; }
        /// <summary>
        /// 角色id集合
        /// </summary>
        [ExcelIgnore]
        public long[] RoleIds { get; set; }
        /// <summary>
        /// 岗位集合
        /// </summary>
        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public int[] PostIds { get; set; }

        [SugarColumn(IsIgnore = true)]
        [ExcelIgnore]
        public List<SysRole> Roles { get; set; }
        [SugarColumn(IsIgnore = true)]
        public string WelcomeMessage
        {
            get
            {
                int now = DateTime.Now.Hour;

                if (now > 0 && now <= 6)
                {
                    return "午夜好";
                }
                else if (now > 6 && now <= 11)
                {
                    return "早上好";
                }
                else if (now > 11 && now <= 14)
                {
                    return "中午好";
                }
                else if (now > 14 && now <= 18)
                {
                    return "下午好";
                }
                else
                {
                    return "晚上好";
                }
            }
        }

        #endregion
    }

    public class SysUserQueryDto
    {
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public string NickName { get; set; }
        public string Email { get; set; }
        public string Remark { get; set; }
        public string Phonenumber { get; set; }
        /// <summary>
        /// 用户性别（0男 1女 2未知）
        /// </summary>
        public int Sex { get; set; }
        public DateTime? BeginTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Status { get; set; }
        public long DeptId { get; set; }
    }
}
