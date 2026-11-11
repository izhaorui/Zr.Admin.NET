using System.ComponentModel.DataAnnotations;

namespace ZR.Model.AI.Dto
{
    /// <summary>AI 运行时配置的当前状态（密钥已掩码，可直接回显到管理端）。</summary>
    public class AiConfigCurrentDto
    {
        /// <summary>配置段名（AiOptions / AiKnowledge）</summary>
        public string Section { get; set; }

        /// <summary>当前进程的环境名（Development / Production）</summary>
        public string Environment { get; set; }

        /// <summary>配置源策略：auto / file / db</summary>
        public string Source { get; set; }

        /// <summary>按当前环境与策略，数据库覆盖是否生效</summary>
        public bool DatabaseOverrideEnabled { get; set; }

        /// <summary>库中是否存在该段的配置</summary>
        public bool HasDbConfig { get; set; }

        /// <summary>库中配置版本（每次保存 +1）</summary>
        public long Version { get; set; }

        /// <summary>库中配置是否密文存储</summary>
        public bool Encrypted { get; set; }

        /// <summary>最近修改人</summary>
        public string UpdatedBy { get; set; }

        /// <summary>最近修改时间</summary>
        public DateTime? UpdatedTime { get; set; }

        /// <summary>配置 JSON：库中有值时为库中内容（密钥掩码），否则为文件默认值（密钥掩码）</summary>
        public string ConfigJson { get; set; }

        /// <summary>库表不可用等可提示问题（为空表示正常）</summary>
        public string Warning { get; set; }
    }

    /// <summary>保存 AI 运行时配置。ConfigJson 中的掩码密钥（****xxxx）会用库中现有值回填。</summary>
    public class AiConfigSaveDto
    {
        /// <summary>整段配置 JSON（AiOptions 结构）</summary>
        [Required(ErrorMessage = "配置内容不能为空")]
        public string ConfigJson { get; set; }

        /// <summary>变更说明（写入审计）</summary>
        [StringLength(200, ErrorMessage = "变更说明最多 200 字")]
        public string Remark { get; set; }

        /// <summary>
        /// 乐观锁版本号：填 config/current 返回的 Version。传入且与库中不一致时拒绝保存，
        /// 避免两位管理员同时编辑互相覆盖；不传（旧前端）时只在本次请求内做版本校验。
        /// </summary>
        public long? Version { get; set; }
    }
}
