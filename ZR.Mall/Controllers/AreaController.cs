using Microsoft.AspNetCore.Mvc;
using ZR.Mall.Service.IService;

namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 行政区划（只读字典接口）
    /// </summary>
    /// <remarks>
    /// 仅为运费模板配置「不配送地区」提供级联下拉数据，**不提供增删改**：
    /// 地区字典来自国家发布数据，由脚本统一生成（见 scripts/gen-area-data.mjs），
    /// 手工增删会与 C 端选择器口径脱节。
    /// </remarks>
    [Route("shopping/area")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class AreaController : BaseController
    {
        private readonly IAreaService _areaService;

        public AreaController(IAreaService areaService)
        {
            _areaService = areaService;
        }

        /// <summary>
        /// 按父级取下级地区（省 → 市 → 区县 懒加载）
        /// </summary>
        /// <param name="parentId">父级 Id；传 0 / 不传取省级</param>
        [HttpGet("tree")]
        // 用 freight 权限而非独立 area 权限：地区接口只服务于运费模板配置，
        // 单独设一套权限只会让运营多配一份无意义的白名单
        [ActionPermissionFilter(Permission = "shop:freight:list")]
        public IActionResult GetTree([FromQuery] long parentId = 0)
        {
            return SUCCESS(_areaService.GetChildren(parentId));
        }
    }
}
