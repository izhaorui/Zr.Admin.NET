using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MiniExcelLibs;
using ZR.Common;
using ZR.Mall.Common;
using ZR.Mall.Model;
using ZR.Mall.Model.Dto;
using ZR.Mall.Service.IService;

//创建时间：2025-05-30
namespace ZR.Mall.Controllers
{
    /// <summary>
    /// 订单管理
    /// </summary>
    [Route("shopping/Order")]
    [ApiExplorerSettings(GroupName = "shopping")]
    public class OrderController : BaseController
    {
        private NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        /// 后台订单运营：查询 / 导出 / 统计 / 修改
        /// </summary>
        private readonly IOMSOrderService _OMSOrderService;

        /// <summary>
        /// 订单状态内核：取消 / 批量取消 / 发货
        /// </summary>
        private readonly IOMSOrderStateService _orderStateService;

        public OrderController(IOMSOrderService OMSOrderService, IOMSOrderStateService orderStateService)
        {
            _OMSOrderService = OMSOrderService;
            _orderStateService = orderStateService;
        }

        /// <summary>
        /// 查询订单管理列表
        /// </summary>
        /// <param name="parm"></param>
        /// <returns></returns>
        [HttpGet("list")]
        [ActionPermissionFilter(Permission = "oms:order:list")]
        public IActionResult QueryOMSOrder([FromQuery] OMSOrderQueryDto parm)
        {
            var response = _OMSOrderService.GetList(parm);
            // 列表 + 概览共 2 条 SQL：概览（各状态计数 + 待发货未发货数）由 GetOrderOverview 一次 GROUP BY 得出，
            // 替代原先额外两次 COUNT。Extra 键名保持不变，前端仍读 data.extra.statusStats。
            var overview = _OMSOrderService.GetOrderOverview();
            response.Extra.Add("NotDelivereOrder", overview.NotDelivered);
            response.Extra.Add("StatusStats", overview.StatusStats);
            return SUCCESS(response);
        }

        /// <summary>
        /// 查询订单管理详情
        /// </summary>
        /// <param name="Id"></param>
        /// <returns></returns>
        [HttpGet("{Id}")]
        [ActionPermissionFilter(Permission = "oms:order:query")]
        public IActionResult GetOMSOrder(long Id)
        {
            var response = _OMSOrderService.GetInfo(Id);
            // GetInfo 对不存在的订单返回 null；不判空会导致 Adapt(null) 后返回 200 + data:null（或 NRE）
            if (response == null)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在");
            }

            var info = response.Adapt<OMSOrderDto>();
            return SUCCESS(info);
        }

        /// <summary>
        /// 查看订单中**敏感字段**的完整值（发货用）。
        ///
        /// 为什么要单独一个接口：订单详情里敏感字段默认脱敏，而虚拟商品发货要的恰恰是
        /// 接收手机号 / 接收账号这类值。三道约束缺一不可 ——
        /// ① 独立权限 <c>oms:order:sensitive</c>（普通订单查询权看不到）；
        /// ② 记操作日志（谁为哪单看过明文，可审计）；
        /// ③ **不保存请求与响应体**：否则明文会原样落进操作日志表，等于绕开①和②从日志里泄露。
        ///
        /// 只返回敏感字段：非敏感字段在详情里本就是明文，再传一遍只会白扩大数据面。
        /// </summary>
        [HttpGet("formValuesRaw/{id}")]
        [ActionPermissionFilter(Permission = "oms:order:sensitive")]
        [Log(Title = "查看买家敏感信息", BusinessType = BusinessType.OTHER, IsSaveRequestData = false, IsSaveResponseData = false)]
        public IActionResult GetOrderFormValuesRaw(long id)
        {
            return SUCCESS(_OMSOrderService.GetSensitiveFormValues(id));
        }

        /// <summary>
        /// 更新订单管理
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [ActionPermissionFilter(Permission = "oms:order:edit")]
        [Log(Title = "订单管理", BusinessType = BusinessType.UPDATE)]
        public IActionResult UpdateOMSOrder([FromBody] OMSOrderDto parm)
        {
            var modal = parm.Adapt<OMSOrder>().ToUpdate(HttpContext);
            var response = _OMSOrderService.UpdateOMSOrder(parm.OperType, modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 删除订单管理
        /// </summary>
        /// <returns></returns>
        [HttpPost("delete/{ids}")]
        [ActionPermissionFilter(Permission = "oms:order:delete")]
        [Log(Title = "订单管理", BusinessType = BusinessType.DELETE)]
        public IActionResult DeleteOMSOrder([FromRoute] string ids)
        {
            var idArr = Tools.SplitAndConvert<long>(ids);
            return ToResponse(_OMSOrderService.DeleteOrders(idArr));
        }

        /// <summary>
        /// 订单退款
        /// </summary>
        /// <returns></returns>
        [HttpPost("refund")]
        [ActionPermissionFilter(Permission = "oms:order:refund")]
        [Log(Title = "订单退款", BusinessType = BusinessType.UPDATE)]
        public IActionResult RefundOrder([FromBody] OMSOrderDto parm)
        {
            var modal = parm.Adapt<OMSOrder>().ToUpdate(HttpContext);
            var response = _OMSOrderService.UpdateOMSOrder((int)Enum.OrderUpdateOperType.Refund, modal);

            return ToResponse(response);
        }

        /// <summary>
        /// 取消订单（仅待发货可取消，回补库存）
        /// </summary>
        /// <returns></returns>
        [HttpPost("cancel/{id}")]
        [ActionPermissionFilter(Permission = "oms:order:cancel")]
        [Log(Title = "订单取消", BusinessType = BusinessType.UPDATE)]
        public IActionResult CancelOMSOrder(long id)
        {
            var response = _orderStateService.CancelOrder(id);

            return ToResponse(response);
        }

        /// <summary>
        /// 批量取消订单：仅待付款/待发货可取消，其余状态自动跳过；返回成功取消的条数
        /// </summary>
        [HttpPost("cancel/batch")]
        [ActionPermissionFilter(Permission = "oms:order:cancel")]
        [Log(Title = "订单批量取消", BusinessType = BusinessType.UPDATE)]
        public IActionResult CancelOMSOrders([FromBody] List<long> ids)
        {
            var response = _orderStateService.CancelOrders(ids);

            return ToResponse(response);
        }

        /// <summary>
        /// 导出订单管理
        /// </summary>
        /// <returns></returns>
        [Log(Title = "订单管理", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("export")]
        [ActionPermissionFilter(Permission = "oms:order:export")]
        public IActionResult Export([FromQuery] OMSOrderQueryDto parm)
        {
            var list = _OMSOrderService.ExportList(parm).Result;
            if (list == null || list.Count <= 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            var result = ExportExcelMini(list, "订单管理", "订单管理");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 订单发货
        /// </summary>
        /// <returns></returns>
        [HttpPut("delivery")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        [Log(Title = "订单发货", BusinessType = BusinessType.UPDATE)]
        public async Task<IActionResult> Delivery([FromBody] OMSOrderDto parm)
        {
            // 物流信息格式校验放在服务端：前端 ShipmentsForm 的 rules 只是提示性校验，
            // 绕过页面直接调接口就能写进超长/非法内容（列宽 50/64，超长会在 SQL 层报错，
            // 报错信息对运营毫无意义）。规则集中在 DeliveryValidator，与前端逐条对齐。
            var invalid = DeliveryValidator.Validate(parm.DeliveryCompany, parm.DeliveryNo);
            if (invalid != null)
            {
                return ToResponse(ResultCode.PARAM_ERROR, invalid);
            }

            var orderInfo = await _OMSOrderService.GetByIdAsync(parm.Id);
            if (orderInfo == null)
            {
                return ToResponse(ResultCode.FAIL, "订单不存在");
            }
            orderInfo.DeliveryCompany = parm.DeliveryCompany;
            orderInfo.DeliveryNo = parm.DeliveryNo;
            var response = _orderStateService.OrderDelivery(orderInfo);
            if (response > 0)
            {
                return ToResponse(ResultCode.SUCCESS, "发货成功");
            }
            // 不再把内部结果码（-1/-2/-3/-4）直接抛给用户，映射成可读原因
            return ToResponse(ResultCode.CUSTOM_ERROR, DeliveryFailReason(response));
        }

        /// <summary>
        /// 发货结果码 → 用户可读提示。
        /// 取值来自 <see cref="IOMSOrderStateService.OrderDelivery"/>：&gt;0 成功；
        /// -1 已发货；-2 收货信息不完整；-3 非待发货状态（已取消/关闭或并发变更）；-4 物流信息不合法。
        /// </summary>
        private static string DeliveryFailReason(int result) => result switch
        {
            -1 => "该订单已发货，请勿重复操作",
            -2 => "该订单收货信息不完整（收件人/手机号/地区/详细地址有缺失），无法发货",
            -3 => "订单当前状态不可发货（可能已取消/关闭或已被处理），请刷新后重试",
            -4 => "物流公司或物流单号不合法，请检查后重试",
            _ => "发货失败，请刷新后重试"
        };

        /// <summary>
        /// 导出待发货订单
        /// </summary>
        /// <returns></returns>
        [Log(Title = "导出待发货订单", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        [HttpGet("exportDelivery")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        public async Task<IActionResult> ExportExpress([FromQuery] OMSOrderQueryDto parm)
        {
            if (parm == null || parm.BeginCreateTime == null)
            {
                return ToResponse(ResultCode.CUSTOM_ERROR, "请选择时间");
            }
            var list = await _OMSOrderService.ExportWaitDeliveryList(parm);
            var result = await ExportExcelMiniAsync(list, "待发货", "待发货订单");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 批量发货
        /// </summary>
        /// <param name="file">表单字段名必须是 file（前端 el-upload name="file"），靠参数名绑定</param>
        /// <returns></returns>
        [HttpPost("importData")]
        [Consumes("multipart/form-data")]
        [Log(Title = "批量发货", BusinessType = BusinessType.IMPORT, IsSaveRequestData = false, IsSaveResponseData = true)]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        public async Task<IActionResult> ImportData(IFormFile file)
        {
            if (file == null || file.Length <= 0)
            {
                return ToResponse(ResultCode.FAIL, "请选择要导入的文件");
            }
            var resultList = new List<DeliveryExpressDto>();
            using var stream = file.OpenReadStream();
            var rows = await stream.QueryAsync<DeliveryExpressDto>(startCell: "A1");

            var orderNos = rows.Select(x => x.OrderNo).Distinct().ToList();
            // Includes 一次性把订单项带出来（拆成两条查询后内存关联，不是 N+1）：
            // 原先每单发货都会再查一次订单项，100 单就是 100 次重复查询。
            // 发货不新增/修改订单项，故预加载与逐单查询等价。
            var allOrders = await _OMSOrderService.Queryable()
                .Includes(x => x.Items)
                .In(x => x.OrderNo, orderNos)
                .ToListAsync();

            foreach (var item in rows)
            {
                // 物流信息格式与单个发货走同一套规则（DeliveryValidator）：批量入口绕过前端直接传文件，
                // 不校验就会把超长/带空格的内容整批写进库（列宽 50/64，超长在 SQL 层报错）
                var invalid = DeliveryValidator.Validate(item.DeliveryCompany, item.DeliveryNo);
                if (invalid != null)
                {
                    item.Status = invalid;
                    resultList.Add(item);
                    continue;
                }
                var orderInfo = allOrders.FirstOrDefault(f => f.OrderNo == item.OrderNo);
                if (orderInfo == null)
                {
                    item.Status = "订单号不存在";
                    resultList.Add(item);
                    continue;
                }
                if (orderInfo.DeliveryStatus != Enum.DeliveryStatusEnum.NotDelivered)
                {
                    item.Status = "已发货";
                    resultList.Add(item);
                    continue;
                }
                // 地址完整性：判字段而非只判 == null —— 字段全为 null 的空对象会通过 == null 检查，
                // 结果是"发货成功但买家收不到"。规则与单个发货共用 DeliveryValidator，
                // 避免两处各写一遍、日后改一处漏一处。
                if (!DeliveryValidator.IsAddressComplete(orderInfo.AddressSnapshot))
                {
                    item.Status = DeliveryValidator.AddressIncompleteMessage;
                    resultList.Add(item);
                    continue;
                }
                // 用数据库订单实体发货（保留 Id/AddressSnapshot/OrderStatus 等），仅覆盖 Excel 里的快递信息
                orderInfo.DeliveryCompany = item.DeliveryCompany;
                orderInfo.DeliveryNo = item.DeliveryNo;

                // 订单项已随 allOrders 一次性带出（Includes），传入避免每单再查一次；
                // notifyBuyer=false：批量发货不发短信 —— 逐单发是 100 次同步网络往返 + 100 条费用，
                // 且批量场景买家本就靠订单状态看进度（与 NotifyBuyer 只发发货/退款两类的策略一致）。
                var result = _orderStateService.OrderDelivery(orderInfo, orderInfo.Items, notifyBuyer: false);
                item.Status = result > 0 ? "发货成功" : DeliveryFailReason(result);
                resultList.Add(item);
            }

            return SUCCESS(new
            {
                total = resultList.Count,
                successCount = resultList.Count(x => x.Status == "发货成功"),
                failCount = resultList.Count(x => x.Status != "发货成功"),
                // 批量发货按行业惯例不发短信（见 OrderDelivery 的 notifyBuyer 参数），
                // 提示文案放在前端导入弹窗上，避免依赖后端字段被组件透传
                result = resultList
            });
        }

        /// <summary>
        /// 批量发货导入模板下载（仅表头：订单号/物流公司/物流单号）
        /// </summary>
        [HttpGet("importTemplate")]
        [ActionPermissionFilter(Permission = "oms:order:ship")]
        [Log(Title = "订单批量发货模板", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public IActionResult ImportTemplate()
        {
            var result = DownloadImportTemplate(new List<DeliveryExpressDto>() { }, "order");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 查询销售总
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSales")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSales(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetTotalSales(dto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 经营概览（客单价 / 退款率 / 售后率 / 形态占比 / 复购率）
        /// </summary>
        [HttpGet("getBusinessOverview")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetBusinessOverview([FromQuery] OMSOrderQueryDto dto)
        {
            return SUCCESS(await _OMSOrderService.GetBusinessOverview(dto));
        }

        /// <summary>
        /// 退款 Top10 商品
        /// </summary>
        [HttpGet("getRefundTopProduct")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetRefundTopProduct([FromQuery] OMSOrderQueryDto dto)
        {
            return SUCCESS(await _OMSOrderService.GetRefundTopProduct(dto));
        }

        /// <summary>
        /// 查询销售趋势
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSalesTrade")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSalesTrade(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetSaleTreandByDay(dto);

            return SUCCESS(response);
        }

        /// <summary>
        /// 查询销售前10的商品
        /// </summary>
        /// <returns></returns>
        [HttpGet("getSaleTopProduct")]
        [ActionPermissionFilter(Permission = "oms:sale:query")]
        public async Task<IActionResult> GetSaleTopProduct(OMSOrderQueryDto dto)
        {
            var response = await _OMSOrderService.GetSaleTopProduct(dto);

            return SUCCESS(response);
        }

        #region 销售统计图表导出 Excel

        /// <summary>
        /// 导出销售趋势（按天）为 Excel。口径与 getSalesTrade 一致（按支付时间的有效订单）。
        /// </summary>
        [HttpGet("exportSalesTrade")]
        [ActionPermissionFilter(Permission = "oms:sale:export")]
        [Log(Title = "销售趋势导出", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public async Task<IActionResult> ExportSalesTrade([FromQuery] OMSOrderQueryDto dto)
        {
            var data = await _OMSOrderService.GetSaleTreandByDay(dto);
            var rows = ToChartRows(data);
            if (rows.Count == 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }

            var columns = new List<(string, string)>
            {
                ("Date", "日期"), ("TotalSales", "销售额"), ("OrderCount", "订单数")
            };
            var result = ExportChartExcel(rows, columns, "销售趋势", "销售趋势");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 导出销售排名前 10 商品为 Excel。口径与 getSaleTopProduct 一致。
        /// </summary>
        [HttpGet("exportSaleTopProduct")]
        [ActionPermissionFilter(Permission = "oms:sale:export")]
        [Log(Title = "销售排名导出", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public async Task<IActionResult> ExportSaleTopProduct([FromQuery] OMSOrderQueryDto dto)
        {
            var data = await _OMSOrderService.GetSaleTopProduct(dto);
            var rows = ToChartRows(data);
            if (rows.Count == 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }

            var columns = new List<(string, string)>
            {
                ("ProductName", "商品名"), ("TotalSold", "销量"), ("TotalSales", "销售额")
            };
            var result = ExportChartExcel(rows, columns, "销售排名", "销售排名");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 导出退款 Top10 商品为 Excel。口径与 getRefundTopProduct 一致。
        /// </summary>
        [HttpGet("exportRefundTopProduct")]
        [ActionPermissionFilter(Permission = "oms:sale:export")]
        [Log(Title = "退款排名导出", BusinessType = BusinessType.EXPORT, IsSaveResponseData = false)]
        public async Task<IActionResult> ExportRefundTopProduct([FromQuery] OMSOrderQueryDto dto)
        {
            var data = await _OMSOrderService.GetRefundTopProduct(dto);
            var rows = ToChartRows(data);
            if (rows.Count == 0)
            {
                return ToResponse(ResultCode.FAIL, "没有要导出的数据");
            }
            FormatPercentColumn(rows, "RefundRate");

            var columns = new List<(string, string)>
            {
                ("ProductName", "商品名"), ("RefundOrderCount", "退款订单数"),
                ("RefundSales", "退款销售额"), ("TotalSales", "总销售额"), ("RefundRate", "退款占比")
            };
            var result = ExportChartExcel(rows, columns, "退款排名", "退款排名");
            return ExportExcel(result.Item2, result.Item1);
        }

        /// <summary>
        /// 把 Service 返回的匿名类型/强类型图表数据转成「字段→值」字典行，交给 ExportChartExcel。
        /// 走 JSON 中转而非反射：匿名类型属性名固定、序列化稳定，且能一并处理强类型 DTO。
        /// </summary>
        private static List<Dictionary<string, object>> ToChartRows(object data)
        {
            if (data == null)
            {
                return new List<Dictionary<string, object>>();
            }
            return JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(JsonConvert.SerializeObject(data))
                ?? new List<Dictionary<string, object>>();
        }

        /// <summary>把小数比率列就地格式化为百分比字符串（0.1234 → 12.34%），失败则原样保留。</summary>
        private static void FormatPercentColumn(List<Dictionary<string, object>> rows, string field)
        {
            foreach (var row in rows)
            {
                if (row.TryGetValue(field, out var value) && value != null)
                {
                    try
                    {
                        row[field] = Convert.ToDecimal(value).ToString("P2");
                    }
                    catch (FormatException) { }
                    catch (InvalidCastException) { }
                    catch (OverflowException) { }
                }
            }
        }

        #endregion

    }
}