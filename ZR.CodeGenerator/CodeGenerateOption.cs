namespace ZR.CodeGenerator
{
    public class CodeGenerateOption
    {
        /// <summary>
        /// 项目命名空间
        /// </summary>
        public string BaseNamespace { get; set; }
        /// <summary>
        /// 下级命名空间
        /// </summary>
        public string SubNamespace { get; set; }
        /// <summary>
        /// 数据实体命名空间
        /// </summary>
        public string ModelsNamespace { get; set; }
        /// <summary>
        /// 输入输出数据实体名称空间
        /// </summary>
        public string DtosNamespace { get; set; }
        /// <summary>
        /// 仓储接口命名空间
        /// </summary>
        public string IRepositoriesNamespace { get; set; }
        /// <summary>
        /// 仓储实现名称空间
        /// </summary>
        public string RepositoriesNamespace { get; set; }
        /// <summary>
        /// 服务接口命名空间
        /// </summary>
        public string IServicsNamespace { get; set; }
        /// <summary>
        /// 服务接口实现命名空间
        /// </summary>
        public string ServicesNamespace { get; set; }

        /// <summary>
        /// Api控制器命名空间
        /// </summary>
        public string ApiControllerNamespace { get; set; }

        /// <summary>
        /// 去掉的表头字符
        /// </summary>
        public string ReplaceTableNameStr { get; set; }
        /// <summary>
        /// 要生数据的表，用“，”分割
        /// </summary>
        //public string TableList { get; set; }
    }
}
