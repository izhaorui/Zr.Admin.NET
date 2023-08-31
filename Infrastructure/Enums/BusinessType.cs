namespace Infrastructure.Enums
{
    /// <summary>
    /// 业务操作类型 0=其它,1=新增,2=修改,3=删除,4=授权,5=导出,6=导入,7=强退,8=生成代码,9=清空数据
    /// </summary>
    public enum BusinessType
    {
        /// <summary>
        /// 其它
        /// </summary>
        OTHER = 0,

        /// <summary>
        /// 新增
        /// </summary>
        INSERT = 1,

        /// <summary>
        /// 修改
        /// </summary>
        UPDATE = 2,

        /// <summary>
        /// 删除
        /// </summary>
        DELETE = 3,

        /// <summary>
        /// 授权
        /// </summary>
        GRANT = 4,

        /// <summary>
        /// 导出
        /// </summary>
        EXPORT = 5,

        /// <summary>
        /// 导入
        /// </summary>
        IMPORT = 6,

        /// <summary>
        /// 强退
        /// </summary>
        FORCE = 7,

        /// <summary>
        /// 生成代码
        /// </summary>
        GENCODE = 8,

        /// <summary>
        /// 清空数据
        /// </summary>
        CLEAN = 9,

        /// <summary>
        /// 下载
        /// </summary>
        DOWNLOAD = 10,
    }
}
