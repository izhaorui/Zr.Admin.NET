/**
 * 通用js方法封装处理
 *
 */

const baseURL = process.env.VUE_APP_BASE_API

// 日期格式化
export function parseTime(time, pattern) {
  if (arguments.length === 0 || !time) {
    return null
  }
  const format = pattern || '{y}-{m}-{d} {h}:{i}:{s}'
  let date
  if (typeof time === 'object') {
    date = time
  } else {
    if (typeof time === 'string' && /^[0-9]+$/.test(time)) {
      time = parseInt(time)
    } else if (typeof time === 'string') {
      time = time.replace(new RegExp(/-/gm), '/')
    }
    if (typeof time === 'number' && time.toString().length === 10) {
      time = time * 1000
    }
    date = new Date(time)
  }
  const formatObj = {
    y: date.getFullYear(),
    m: date.getMonth() + 1,
    d: date.getDate(),
    h: date.getHours(),
    i: date.getMinutes(),
    s: date.getSeconds(),
    a: date.getDay(),
  }
  const time_str = format.replace(/{(y|m|d|h|i|s|a)+}/g, (result, key) => {
    let value = formatObj[key]
    // Note: getDay() returns 0 on Sunday
    if (key === 'a') {
      return ['日', '一', '二', '三', '四', '五', '六'][value]
    }
    if (result.length > 0 && value < 10) {
      value = '0' + value
    }
    return value || 0
  })
  return time_str
}

// 表单重置
export function resetForm(refName) {
  if (this.$refs[refName]) {
    this.$refs[refName].resetFields()
  }
}

/**
 * 添加日期范围
 * @param { beginTime: '', endTime: '', page: 1} params
 * @param {*} dateRange 日期范围数组
 * @param {*} propName C#属性名首字母大写
 * @returns
 */
// 添加日期范围
export function addDateRange(params, dateRange, propName) {
  let search = params
  search = typeof search === 'object' && search !== null && !Array.isArray(search) ? search : {}
  dateRange = Array.isArray(dateRange) ? dateRange : []
  if (typeof propName === 'undefined') {
    search['beginTime'] = dateRange[0]
    search['endTime'] = dateRange[1]
  } else {
    search['begin' + propName] = dateRange[0]
    search['end' + propName] = dateRange[1]
  }
  return search
}

export function addDateRange2(dateRange, index) {
  var time = undefined
  if (null != dateRange && '' != dateRange) {
    if (dateRange.length <= 2) {
      time = dateRange[index]
    }
  }
  return time
}

// 回显数据字典
export function selectDictLabel(datas, value) {
  if (value === undefined) {
    return ''
  }
  var actions = []
  Object.keys(datas).some((key) => {
    if (datas[key].dictValue == '' + value) {
      actions.push(datas[key].dictLabel)
      return true
    }
  })
  if (actions.length === 0) {
    actions.push(value)
  }
  return actions.join('')
}

// 回显数据字典（字符串数组）
export function selectDictLabels(datas, value, separator) {
  if (value === undefined) {
    return ''
  }
  var actions = []
  var currentSeparator = undefined === separator ? ',' : separator
  var temp = value.split(currentSeparator)
  Object.keys(value.split(currentSeparator)).some((val) => {
    var match = false
    Object.keys(datas).some((key) => {
      if (datas[key].value == '' + temp[val]) {
        actions.push(datas[key].label + currentSeparator)
        match = true
      }
    })
    if (!match) {
      actions.push(temp[val] + currentSeparator)
    }
  })
  return actions.join('').substring(0, actions.join('').length - 1)
}

// table是否显示当前列
export function showColumn(columns, value) {
  columns.filter((item, index) => {
    // console.log(item);
    return item.key == value
  })
}

// 通用下载方法
export function download(url, fileName) {
  // window.location.href = baseURL + "/common/download?fileName=" + encodeURI(fileName) + "&delete=" + true;
  // window.open(baseURL + "/common/download?fileName=" + encodeURI(fileName) + "&delete=" + true)
  window.open(baseURL + url)
}

// 字符串格式化(%s )
export function sprintf(str) {
  var args = arguments,
    flag = true,
    i = 1
  str = str.replace(/%s/g, function () {
    var arg = args[i++]
    if (typeof arg === 'undefined') {
      flag = false
      return ''
    }
    return arg
  })
  return flag ? str : ''
}

// 转换字符串，undefined,null等转化为""
export function praseStrEmpty(str) {
  if (!str || str == 'undefined' || str == 'null') {
    return ''
  }
  return str
}
export function praseStrZero(str) {
  if (!str || str == 'undefined' || str == 'null') {
    console.log('zero')
    return 0
  }
  return str
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;')
}

function formatImportRecord(record) {
  if (record == null || record === '') {
    return ''
  }

  const text = typeof record === 'string' ? record : JSON.stringify(record)
  return text.length > 160 ? text.slice(0, 160) + '...' : text
}

function normalizeImportData(result) {
  const data = result?.data ?? result ?? {}
  const summary = data.summary ?? data.item1 ?? '导入完成'
  const totalCount = Number(data.totalCount ?? 0)
  const successCount = Number(data.successCount ?? data.insertCount ?? 0)
  const failedCount = Number(data.failedCount ?? 0)
  const ignoreCount = Number(data.ignoreCount ?? 0)
  const errorDetails = Array.isArray(data.errorDetails) ? data.errorDetails : Array.isArray(data.item2) ? data.item2 : []
  const ignoreDetails = Array.isArray(data.ignoreDetails) ? data.ignoreDetails : []

  return {
    summary,
    totalCount,
    successCount,
    failedCount,
    ignoreCount,
    errorDetails,
    ignoreDetails,
  }
}

function renderImportDetail(item) {
  const message = escapeHtml(item?.storageMessage || '暂无说明')
  const record = escapeHtml(formatImportRecord(item?.record))
  return record
    ? '<li><div style="font-weight: 600; margin-bottom: 4px;">' +
        message +
        '</div><div style="color: var(--el-text-color-secondary); line-height: 1.6; word-break: break-all;">' +
        record +
        '</div></li>'
    : '<li>' + message + '</li>'
}

function renderImportSection(title, items) {
  if (!items.length) {
    return ''
  }

  const previewCount = 5
  const detailList = items.slice(0, previewCount).map(renderImportDetail).join('')
  const moreText =
    items.length > previewCount
      ? '<div style="margin-top: 8px; color: var(--el-text-color-secondary);">还有 ' + (items.length - previewCount) + ' 条未显示</div>'
      : ''

  return (
    '<section style="margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--el-border-color-lighter);">' +
    '<div style="font-weight: 600; margin-bottom: 8px;">' +
    escapeHtml(title) +
    '（' +
    items.length +
    '）</div>' +
    '<ul style="margin: 0; padding-left: 20px;">' +
    detailList +
    '</ul>' +
    moreText +
    '</section>'
  )
}

export function buildImportResultHtml(result) {
  const { summary, totalCount, successCount, failedCount, ignoreCount, errorDetails, ignoreDetails } = normalizeImportData(result)
  const overviewHtml =
    '<div style="margin-bottom: 12px; line-height: 1.8;">' +
    '<div style="font-size: 14px; font-weight: 600; margin-bottom: 6px;">' +
    escapeHtml(summary) +
    '</div>' +
    '<div style="color: var(--el-text-color-secondary);">总数：' +
    totalCount +
    '，成功：' +
    successCount +
    '，失败：' +
    failedCount +
    '，忽略：' +
    ignoreCount +
    '</div>' +
    '</div>'

  return overviewHtml + renderImportSection('错误明细', errorDetails) + renderImportSection('忽略明细', ignoreDetails)
}

/**
 * 构造树型结构数据
 * @param {*} data 数据源
 * @param {*} id id字段 默认 'id'
 * @param {*} parentId 父节点字段 默认 'parentId'
 * @param {*} children 孩子节点字段 默认 'children'
 * @param {*} rootId 根Id 默认 0
 */
export function handleTree(data, id, parentId, children) {
  let config = {
    id: id || 'id',
    parentId: parentId || 'parentId',
    childrenList: children || 'children',
  }

  var childrenListMap = {}
  var nodeIds = {}
  var tree = []

  for (let d of data) {
    let parentId = d[config.parentId]
    if (childrenListMap[parentId] == null) {
      childrenListMap[parentId] = []
    }
    nodeIds[d[config.id]] = d
    childrenListMap[parentId].push(d)
  }

  for (let d of data) {
    let parentId = d[config.parentId]
    if (nodeIds[parentId] == null) {
      tree.push(d)
    }
  }

  for (let t of tree) {
    adaptToChildrenList(t)
  }

  function adaptToChildrenList(o) {
    if (childrenListMap[o[config.id]] !== null) {
      o[config.childrenList] = childrenListMap[o[config.id]]
    }
    if (o[config.childrenList]) {
      for (let c of o[config.childrenList]) {
        adaptToChildrenList(c)
      }
    }
  }
  return tree
}

/**
 * 构造自定义字典数据
 * @param {*} data 数据源
 * @param {*} lableId id字段 默认 'lableId'
 * @param {*} labelName 名称 默认 'labelName'
 */
export function handleDict(data, lableId, labelName) {
  lableId = lableId || 'id'
  labelName = labelName || 'name'
  //循环所有项
  var dictList = []
  if (!Array.isArray(data)) {
    return []
  }
  data.forEach((element) => {
    dictList.push({
      dictLabel: element[labelName],
      dictValue: element[lableId].toString(),
    })
  })
  return dictList
}

// 验证是否为blob格式
export async function blobValidate(data) {
  try {
    const text = await data.text()
    JSON.parse(text)
    return false
  } catch (error) {
    return true
  }
}
