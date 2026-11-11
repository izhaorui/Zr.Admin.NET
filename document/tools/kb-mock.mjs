// 知识库插件联调用 Mock 服务（零依赖，Node 18+）
// 用法：node document/tools/kb-mock.mjs [端口，默认 5099]
// 作用：按 generic 渠道的请求/响应契约返回两条固定片段，用于验证
//       「工具下发 → 模型调用 → 片段回灌 → 引用展示」整条链路，无需真实知识库。
// 配置：ai.json 的 AiKnowledge 填
//   { "Enable": true, "Provider": "generic", "Endpoint": "http://127.0.0.1:5099/search" }
import http from 'node:http';

const port = Number(process.argv[2] || 5099);

const server = http.createServer((req, res) => {
  if (req.method !== 'POST') {
    res.writeHead(405, { 'Content-Type': 'application/json' });
    res.end('{"error":"method not allowed"}');
    return;
  }

  let body = '';
  req.setEncoding('utf8');
  req.on('data', (chunk) => { body += chunk; });
  req.on('end', () => {
    let query = '';
    try {
      query = JSON.parse(body || '{}').query || '';
    } catch {
      // 请求体非法时按空查询处理，返回固定片段（仅用于联调）
    }

    const hits = [
      {
        content: `【示例】关于「${query}」的报销规定：费用发生后 30 天内提交报销单，超期需附书面说明并经部门负责人审批。`,
        score: 0.91,
        title: '财务报销制度.pdf',
        url: 'https://example.com/kb/finance#p2',
        page: 2
      },
      {
        content: '【示例】差旅住宿标准：一线城市上限 600 元/晚，二线城市 450 元/晚，其他城市 350 元/晚；超标部分自理。',
        score: 0.78,
        title: '差旅管理制度.pdf',
        page: 5
      }
    ];

    console.log(`[kb-mock] query=${query} -> ${hits.length} hits`);
    const payload = JSON.stringify({ hits });
    res.writeHead(200, {
      'Content-Type': 'application/json; charset=utf-8',
      'Content-Length': Buffer.byteLength(payload)
    });
    res.end(payload);
  });
});

server.listen(port, '127.0.0.1', () => {
  console.log(`[kb-mock] listening http://127.0.0.1:${port}/search`);
});
