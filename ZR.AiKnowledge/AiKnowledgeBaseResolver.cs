using System;
using System.Collections.Generic;
using System.Linq;
using Infrastructure.AI.Knowledge;

namespace ZR.AiKnowledge
{
    /// <summary>
    /// 多知识库的选库、连接构造与可用性判断。
    /// <para>工具（模型调用）与管理端接口共用同一套规则，避免两处各写一份导致"测试通过但线上选错库"。</para>
    /// </summary>
    public static class AiKnowledgeBaseResolver
    {
        /// <summary>
        /// 按 kbId 选库：未指定用默认库；指定时先按库 Id 匹配，
        /// 再兼容按上游 KbId 匹配（模型有时会从文档里拿到上游库标识）。
        /// </summary>
        public static AiKnowledgeBaseOptions Pick(IReadOnlyList<AiKnowledgeBaseOptions> bases, string requestedBaseId)
        {
            if (bases == null || bases.Count == 0)
            {
                return null;
            }
            if (string.IsNullOrWhiteSpace(requestedBaseId))
            {
                return bases.FirstOrDefault(b => b.IsDefault) ?? bases[0];
            }

            var id = requestedBaseId.Trim();
            return bases.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? bases.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.KbId)
                    && string.Equals(b.KbId, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>把生效库配置转成渠道连接信息（字段已在 BuildBases 中回落，直接透传）。</summary>
        public static KnowledgeConnection ToConnection(AiKnowledgeBaseOptions selected) => new()
        {
            Provider = selected.Provider,
            KbName = selected.Name,
            Endpoint = selected.Endpoint,
            ApiKey = selected.ApiKey,
            DatasetId = selected.DatasetId,
            TimeoutSeconds = selected.TimeoutSeconds ?? 15
        };

        /// <summary>
        /// 基础配置是否完整：generic 只看地址；dify 还需要数据集与密钥。
        /// 用于管理端把"配了但一直报错"的库直接标出来，而不是等用户试到报错才知道。
        /// </summary>
        public static bool IsConfigured(AiKnowledgeBaseOptions baseItem)
        {
            if (string.IsNullOrWhiteSpace(baseItem?.Endpoint))
            {
                return false;
            }
            if (string.Equals(baseItem.Provider, AiKnowledgeOptions.DifyProvider, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(baseItem.DatasetId) && !string.IsNullOrWhiteSpace(baseItem.ApiKey);
            }
            return true;
        }

        /// <summary>当前用户能否使用该库：库未配权限码=不做限制；配了则按权限码判断（管理员放行）。</summary>
        public static bool CanUse(AiKnowledgeBaseOptions baseItem, List<string> perms)
        {
            if (string.IsNullOrWhiteSpace(baseItem?.Permission))
            {
                return true;
            }
            return AiPermissionHelper.HasPerm(perms, baseItem.Permission);
        }
    }
}