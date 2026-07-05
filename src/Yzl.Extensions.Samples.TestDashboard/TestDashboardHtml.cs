namespace Yzl.Extensions.Samples.TestDashboard;

/// <summary>
/// 测试面板的 HTML 模板页面。
/// 页面加载时通过 fetch('/dashboard/api/routes') 动态获取路由数据并渲染。
/// 支持路由参数输入、Query 参数、Form 参数、SSE 流接收。
/// </summary>
internal static class TestDashboardHtml
{
    public static string GetContent(string title) => $$""""
        <!DOCTYPE html>
        <html lang="zh-CN">
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>{{title}}</title>
        <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
        font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
        background: #f0f2f5; color: #333; padding: 20px;
        }
        .container { max-width: 1000px; margin: 0 auto; }
        h1 {
        text-align: center; font-size: 24px; margin-bottom: 8px;
        background: linear-gradient(135deg, #667eea, #764ba2);
        -webkit-background-clip: text; -webkit-text-fill-color: transparent;
        }
        .subtitle { text-align: center; color: #888; font-size: 13px; margin-bottom: 24px; }
        .loading { text-align: center; color: #999; padding: 40px; font-size: 14px; }
        .section {
        background: #fff; border-radius: 8px; box-shadow: 0 1px 3px rgba(0,0,0,.1);
        margin-bottom: 16px; overflow: hidden;
        }
        .section-header {
        padding: 12px 16px; font-size: 14px; font-weight: 600;
        cursor: pointer; user-select: none; display: flex; align-items: center; gap: 8px;
        border-bottom: 1px solid #eee;
        }
        .section-header:hover { background: #fafafa; }
        .section-header .arrow { transition: transform .2s; font-size: 12px; }
        .section-header.collapsed .arrow { transform: rotate(-90deg); }
        .section-body { padding: 12px 16px; }
        .section-header.collapsed + .section-body { display: none; }
        .route-row {
        padding: 8px 0;
        border-bottom: 1px solid #f5f5f5; font-size: 13px;
        }
        .route-row:last-child { border-bottom: none; }
        .route-top {
        display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
        }
        .method {
        display: inline-block; padding: 2px 8px; border-radius: 3px;
        font-size: 11px; font-weight: 700; color: #fff; min-width: 48px; text-align: center;
        }
        .method.get { background: #61affe; }
        .method.post { background: #49cc90; }
        .method.put { background: #fca130; }
        .method.delete { background: #f93e3e; }
        .method.head { background: #9012fe; }
        .method.patch { background: #50e3c2; }
        .method.options { background: #0d5aa7; }
        .route-path {
        font-family: "SF Mono", "Fira Code", "Cascadia Code", monospace;
        font-size: 12px; color: #0b5e7a; flex: 1;
        }
        .route-desc { color: #999; font-size: 12px; min-width: 100px; max-width: 200px; }
        .btn-test {
        padding: 3px 14px; border: none; border-radius: 4px; cursor: pointer;
        font-size: 12px; font-weight: 500; white-space: nowrap;
        background: #1890ff; color: #fff; transition: background .2s;
        }
        .btn-test:hover { background: #40a9ff; }
        .btn-test:disabled { background: #ccc; cursor: not-allowed; }
        .btn-test.loading { background: #faad14; pointer-events: none; }
        .btn-test.success { background: #52c41a; }
        .btn-test.error { background: #ff4d4f; }
        .response-box {
        margin-top: 6px; padding: 8px 12px; border-radius: 4px;
        font-family: "SF Mono", "Fira Code", monospace; font-size: 12px;
        white-space: pre-wrap; word-break: break-all; display: none;
        max-height: 300px; overflow: auto; cursor: pointer;
        }
        .response-box.success { display: block; background: #f6ffed; border: 1px solid #b7eb8f; color: #135200; }
        .response-box.error { display: block; background: #fff2f0; border: 1px solid #ffccc7; color: #820014; }
        .response-box:hover { filter: brightness(0.97); }
        .route-controls { display: flex; align-items: center; gap: 8px; flex-shrink: 0; }
        .params-area {
        margin-top: 8px; padding: 8px 12px; background: #fafafa;
        border-radius: 6px; border: 1px solid #eee;
        }
        .params-title {
        font-size: 12px; font-weight: 600; color: #666; margin-bottom: 6px;
        display: flex; align-items: center; gap: 8px;
        }
        .param-row {
        display: inline-flex; align-items: center; gap: 6px; margin: 3px 6px 3px 0;
        }
        .param-label {
        font-size: 12px; font-weight: 500; color: #555; font-family: monospace;
        white-space: nowrap;
        }
        .input-param {
        padding: 3px 8px; border: 1px solid #d9d9d9; border-radius: 3px;
        font-size: 12px; width: 120px; font-family: monospace; height: 26px;
        }
        .input-param:focus { outline: none; border-color: #40a9ff; box-shadow: 0 0 0 2px rgba(24,144,255,.2); }
        .kv-row {
        display: inline-flex; align-items: center; gap: 4px; margin: 3px 6px 3px 0;
        }
        .kv-row .input-param { width: 100px; }
        .btn-remove {
        padding: 0 6px; border: none; border-radius: 3px; cursor: pointer;
        background: #f5f5f5; color: #999; font-size: 14px; line-height: 22px; height: 26px;
        }
        .btn-remove:hover { background: #ff4d4f; color: #fff; }
        .btn-add-param {
        padding: 2px 10px; border: 1px dashed #d9d9d9; border-radius: 3px;
        background: transparent; color: #666; cursor: pointer; font-size: 11px;
        }
        .btn-add-param:hover { border-color: #40a9ff; color: #40a9ff; background: #f0f8ff; }
        .badge { display: inline-block; padding: 0 6px; font-size: 11px; border-radius: 3px; background: #f5f5f5; color: #666; }
        .log-panel {
        background: #1e1e2e; border-radius: 8px; margin-bottom: 16px; overflow: hidden;
        box-shadow: 0 2px 8px rgba(0,0,0,.15);
        position: sticky; top: 16px; z-index: 10;
        }
        .log-panel .log-header {
        padding: 10px 16px; font-size: 13px; font-weight: 600; cursor: pointer; user-select: none;
        display: flex; align-items: center; gap: 8px; color: #cdd6f4; background: #181825;
        border-bottom: 1px solid #313244;
        }
        .log-panel .log-header .arrow { transition: transform .2s; font-size: 12px; }
        .log-panel.collapsed .log-header .arrow { transform: rotate(-90deg); }
        .log-panel.collapsed .log-body { display: none; }
        .log-panel .log-body {
        padding: 8px 12px; max-height: 320px; overflow-y: auto; font-family: "SF Mono","Fira Code",monospace; font-size: 11px;
        }
        .log-panel .log-body::-webkit-scrollbar { width: 6px; }
        .log-panel .log-body::-webkit-scrollbar-thumb { background: #45475a; border-radius: 3px; }
        .log-entry {
        padding: 4px 6px; margin-bottom: 2px; border-radius: 3px; line-height: 1.5;
        border-left: 3px solid #585b70; cursor: pointer;
        }
        .log-entry:hover { background: #313244; }
        .log-entry .log-time { color: #6c7086; margin-right: 8px; }
        .log-entry .log-method { display: inline-block; padding: 0 4px; border-radius: 2px; font-weight: 700; font-size: 10px; color: #fff; margin-right: 6px; }
        .log-entry .log-method.get { background: #61affe; }
        .log-entry .log-method.post { background: #49cc90; }
        .log-entry .log-method.put { background: #fca130; }
        .log-entry .log-method.delete { background: #f93e3e; }
        .log-entry .log-path { color: #89b4fa; margin-right: 8px; }
        .log-entry .log-status { font-weight: 600; }
        .log-entry .log-status.ok { color: #a6e3a1; }
        .log-entry .log-status.err { color: #f38ba8; }
        .log-empty { color: #585b70; text-align: center; padding: 16px; font-style: italic; }
        .btn-clear {
        margin-left: auto; padding: 2px 10px; border: 1px solid #45475a; border-radius: 4px;
        background: transparent; color: #a6adc8; cursor: pointer; font-size: 11px;
        }
        .btn-clear:hover { background: #45475a; color: #cdd6f4; }
        .modal-overlay {
        display: none; position: fixed; inset: 0; z-index: 1000;
        background: rgba(0,0,0,.55); justify-content: center; align-items: center;
        }
        .modal-overlay.active { display: flex; }
        .modal-box {
        background: #fff; border-radius: 8px; width: 90vw; max-width: 900px;
        max-height: 85vh; display: flex; flex-direction: column; box-shadow: 0 8px 32px rgba(0,0,0,.3);
        }
        .modal-header {
        display: flex; align-items: center; gap: 12px; padding: 14px 20px;
        border-bottom: 1px solid #eee; font-size: 14px; font-weight: 600;
        }
        .modal-header .modal-path { font-family: "SF Mono","Fira Code",monospace; font-size: 12px; color: #0b5e7a; flex: 1; }
        .modal-close {
        padding: 4px 12px; border: none; border-radius: 4px; cursor: pointer;
        background: #f5f5f5; color: #666; font-size: 18px; line-height: 1;
        }
        .modal-close:hover { background: #ff4d4f; color: #fff; }
        .modal-body {
        padding: 16px 20px; overflow: auto; flex: 1;
        font-family: "SF Mono","Fira Code",monospace; font-size: 13px;
        white-space: pre-wrap; word-break: break-all; line-height: 1.6;
        }
        @media (max-width: 640px) {
        .route-top { flex-wrap: wrap; }
        .route-desc { min-width: auto; width: 100%; order: -1; }
        }
        </style>
        </head>
        <body>
        <div class="container">
        <h1>🔌 {{title}}</h1>
        <p class="subtitle" id="subtitle">加载中...</p>

        <div class="log-panel" id="logPanel">
        <div class="log-header" onclick="toggleCollapse('logPanel')">
        <span class="arrow">▼</span>
        <span>📋 测试日志</span>
        <span class="badge" id="logCount">0 条</span>
        <button class="btn-clear" onclick="event.stopPropagation();clearLogs()">清空</button>
        </div>
        <div class="log-body" id="logBody">
        <div class="log-empty">暂无测试记录</div>
        </div>
        </div>

        <div id="sectionsContainer">
        <div class="loading">⏳ 正在加载路由列表...</div>
        </div>
        </div>

        <div class="modal-overlay" id="modalOverlay" onclick="closeModal(event)">
        <div class="modal-box" onclick="event.stopPropagation()">
        <div class="modal-header">
        <span>📄 完整响应</span>
        <span class="modal-path" id="modalPath"></span>
        <button class="modal-close" onclick="closeModal()">✕</button>
        </div>
        <div class="modal-body" id="modalBody"></div>
        </div>
        </div>

        <script>
        // ============================================================
        // 参数默认值映射
        // ============================================================
        const PARAM_DEFAULTS = {
            id: '1', ids: '1', userId: '1', userid: '1',
            name: 'Alice', username: 'Alice', displayname: 'Alice',
            keyword: 'test', query: 'test', q: 'test',
            age: '25', minAge: '20', maxAge: '40',
            email: 'test@test.com', mail: 'test@test.com',
            page: '1', pageSize: '20', size: '20', pageindex: '1',
            slug: 'hello-world', tag: 'demo',
            category: 'general', status: 'active', sort: 'id',
            title: '测试标题', content: '测试内容',
        };

        function getDefaultValue(name) {
            return PARAM_DEFAULTS[name.toLowerCase()] ?? '';
        }

        // ============================================================
        // 从路由路径中解析 {paramName} 参数
        // ============================================================
        function parseRouteParams(path) {
            const params = [];
            const regex = /{(\w+)}/g;
            let match;
            while ((match = regex.exec(path)) !== null) {
                params.push({ name: match[1], defaultValue: getDefaultValue(match[1]) });
            }
            return params;
        }

        // ============================================================
        // 初始化
        // ============================================================
        async function init() {
            try {
                const res = await fetch('/dashboard/api/routes');
                if (!res.ok) throw new Error('HTTP ' + res.status);
                const data = await res.json();

                const container = document.getElementById('sectionsContainer');
                container.innerHTML = '';

                for (const group of data.groups) {
                    const section = createSection(group);
                    container.appendChild(section);

                    const routesForGroup = data.routes.filter(r => r.group === group.id);
                    const body = section.querySelector('.section-body');
                    for (const route of routesForGroup) {
                        body.appendChild(buildRow(route));
                    }
                }

                document.getElementById('subtitle').textContent =
                    `共 ${data.routes.length} 个测试端点，${data.groups.length} 个分组`;
            } catch (err) {
                document.getElementById('sectionsContainer').innerHTML =
                    `<div class="loading" style="color:#ff4d4f">❌ 加载失败: ${err.message}</div>`;
            }
        }

        function createSection(group) {
            const section = document.createElement('div');
            section.className = 'section';
            section.dataset.group = group.id;

            const header = document.createElement('div');
            header.className = 'section-header';
            header.innerHTML = `<span class="arrow">▼</span><span>${escapeHtml(group.title)}</span>` +
                (group.badge ? `<span class="badge">${escapeHtml(group.badge)}</span>` : '');
            header.addEventListener('click', function() { this.classList.toggle('collapsed'); });
            section.appendChild(header);

            const body = document.createElement('div');
            body.className = 'section-body';
            section.appendChild(body);

            return section;
        }

        // ============================================================
        // 构建行
        // ============================================================
        function buildRow(route) {
            const row = document.createElement('div');
            row.className = 'route-row';

            // ── 顶部栏：方法 + 路径 + 描述 + 按钮 ──
            const top = document.createElement('div');
            top.className = 'route-top';

            const method = document.createElement('span');
            method.className = `method ${route.method.toLowerCase()}`;
            method.textContent = route.method;
            top.appendChild(method);

            const pathSpan = document.createElement('span');
            pathSpan.className = 'route-path';
            pathSpan.textContent = route.path;
            top.appendChild(pathSpan);

            const desc = document.createElement('span');
            desc.className = 'route-desc';
            desc.textContent = route.desc;
            top.appendChild(desc);

            const controls = document.createElement('div');
            controls.className = 'route-controls';

            const btn = document.createElement('button');
            btn.className = 'btn-test';
            btn.textContent = '▶ 测试';
            controls.appendChild(btn);
            top.appendChild(controls);

            row.appendChild(top);

            // ── 响应区 ──
            const resp = document.createElement('div');
            resp.className = 'response-box';
            resp.addEventListener('click', () => {
                if (resp.textContent) {
                    openModal(route.path, resp.textContent);
                }
            });
            row.appendChild(resp);

            // ── 参数解析 ──
            const routeParams = parseRouteParams(route.path);
            const isPost = ['POST', 'PUT', 'PATCH', 'DELETE'].includes(route.method.toUpperCase());

            // ── 参数区域 ──
            let paramsArea = null;
            let routeParamsSection = null;
            let queryAddBtn = null;
            let formAddBtn = null;

            const hasRouteParams = routeParams.length > 0;

            if (hasRouteParams) {
                paramsArea = document.createElement('div');
                paramsArea.className = 'params-area';

                // 路由参数
                routeParamsSection = document.createElement('div');
                routeParamsSection.className = 'params-section';
                const title = document.createElement('div');
                title.className = 'params-title';
                title.textContent = 'URL 参数';
                routeParamsSection.appendChild(title);

                for (const p of routeParams) {
                    const pr = document.createElement('div');
                    pr.className = 'param-row';
                    const label = document.createElement('span');
                    label.className = 'param-label';
                    label.textContent = p.name + ':';
                    pr.appendChild(label);
                    const input = document.createElement('input');
                    input.className = 'input-param param-route';
                    input.dataset.paramName = p.name;
                    input.value = p.defaultValue;
                    pr.appendChild(input);
                    routeParamsSection.appendChild(pr);
                }
                paramsArea.appendChild(routeParamsSection);

                // Query 参数（非 POST）
                if (!isPost) {
                    const qSection = document.createElement('div');
                    qSection.className = 'params-section';
                    const qTitle = document.createElement('div');
                    qTitle.className = 'params-title';
                    qTitle.innerHTML = 'Query 参数 <button class="btn-add-param" onclick="addKvRow(this, \'query\')">+ 添加</button>';
                    qSection.appendChild(qTitle);
                    paramsArea.appendChild(qSection);
                }

                // Form 参数（POST）
                if (isPost) {
                    const fSection = document.createElement('div');
                    fSection.className = 'params-section';
                    const fTitle = document.createElement('div');
                    fTitle.className = 'params-title';
                    fTitle.innerHTML = 'Form 参数 <button class="btn-add-param" onclick="addKvRow(this, \'form\')">+ 添加</button>';
                    fSection.appendChild(fTitle);
                    paramsArea.appendChild(fSection);
                }

                row.appendChild(paramsArea);
            }

            // ── 点击测试按钮 ──
            btn.addEventListener('click', async () => {
                btn.disabled = true;
                btn.className = 'btn-test loading';
                btn.textContent = '⏳ 请求中...';
                resp.className = 'response-box';
                resp.textContent = '';
                resp.style.display = 'none';

                try {
                    // 构建 URL
                    let url = route.path;

                    // 替换路由参数
                    if (paramsArea) {
                        const inputs = paramsArea.querySelectorAll('.param-route');
                        inputs.forEach(inp => {
                            url = url.replace(
                                '{' + inp.dataset.paramName + '}',
                                encodeURIComponent(inp.value)
                            );
                        });
                    }

                    // 如果还有未替换的 {param}，用默认值替换
                    url = url.replace(/{(\w+)}/g, (_, name) => encodeURIComponent(getDefaultValue(name)));

                    // query string
                    let queryString = '';
                    if (paramsArea) {
                        const queryKvs = paramsArea.querySelectorAll('.params-section .kv-row[data-type="query"]');
                        const pairs = [];
                        queryKvs.forEach(kv => {
                            const k = kv.querySelector('.kv-key')?.value;
                            const v = kv.querySelector('.kv-value')?.value;
                            if (k) pairs.push(encodeURIComponent(k) + '=' + encodeURIComponent(v || ''));
                        });
                        queryString = pairs.join('&');
                    }

                    // form body
                    let formBody = null;
                    if (paramsArea) {
                        const formKvs = paramsArea.querySelectorAll('.params-section .kv-row[data-type="form"]');
                        const pairs = [];
                        formKvs.forEach(kv => {
                            const k = kv.querySelector('.kv-key')?.value;
                            const v = kv.querySelector('.kv-value')?.value;
                            if (k) pairs.push(encodeURIComponent(k) + '=' + encodeURIComponent(v || ''));
                        });
                        if (pairs.length > 0) formBody = pairs.join('&');
                    }

                    const fullUrl = url + (queryString ? '?' + queryString : '');

                    // 发送请求
                    let fetchOptions = {};
                    if (isPost) {
                        fetchOptions.method = route.method;
                        if (formBody) {
                            fetchOptions.headers = { 'Content-Type': 'application/x-www-form-urlencoded' };
                            fetchOptions.body = formBody;
                        }
                    }

                    const res = await fetch(fullUrl, fetchOptions);
                    let body;
                    const ct = res.headers.get('content-type') || '';

                    if (route.isSse || ct.includes('text/event-stream')) {
                        // ===== SSE 流式输出：逐块读取并实时显示 =====
                        resp.className = 'response-box success';
                        resp.style.display = 'block';
                        btn.textContent = '🔴 SSE 接收中...';
                        btn.className = 'btn-test loading';

                        const reader = res.body.getReader();
                        const decoder = new TextDecoder();
                        let displayText = `HTTP ${res.status} ${res.statusText}\n\n`;
                        let eventCount = 0;

                        while (true) {
                            const { done, value } = await reader.read();
                            if (done) break;

                            const chunk = decoder.decode(value, { stream: true });
                            displayText += chunk;
                            // 统计 data: 事件数
                            const matches = chunk.match(/^data: /gm);
                            if (matches) eventCount += matches.length;

                            resp.textContent = displayText;
                            resp.scrollTop = resp.scrollHeight;
                        }

                        body = `[SSE 完成，共 ${eventCount} 个事件]`;
                        addLog(route.method, fullUrl, `SSE OK (${eventCount})`, true, `收到 ${eventCount} 个 SSE 事件`, displayText);
                        btn.className = 'btn-test success';
                        btn.textContent = `✅ SSE 完成 (${eventCount})`;
                    } else {
                        if (ct.includes('application/json')) {
                            body = JSON.stringify(await res.json(), null, 2);
                        } else {
                            body = await res.text();
                        }

                        if (res.ok) {
                            resp.className = 'response-box success';
                            btn.className = 'btn-test success';
                            btn.textContent = '✅ 成功';
                        } else {
                            resp.className = 'response-box error';
                            btn.className = 'btn-test error';
                            btn.textContent = '❌ 失败';
                        }
                        const fullBody = `HTTP ${res.status} ${res.statusText}\n\n${body}`;
                        resp.textContent = fullBody;
                        addLog(route.method, fullUrl, `HTTP ${res.status}`, res.ok, body.substring(0, 100).replace(/\n/g, ' '), fullBody);
                    }

                } catch (err) {
                    resp.className = 'response-box error';
                    const errBody = `❌ 请求异常: ${err.message}`;
                    resp.textContent = errBody;
                    btn.className = 'btn-test error';
                    btn.textContent = '❌ 异常';
                    addLog(route.method, route.path, 'ERR', false, err.message, errBody);
                }

                btn.disabled = false;
            });

            return row;
        }

        // ============================================================
        // 添加键值对行（Query / Form）
        // ============================================================
        function addKvRow(btn, type) {
            const section = btn.parentElement.parentElement;
            const row = document.createElement('div');
            row.className = 'kv-row';
            row.dataset.type = type;
            const keyInput = document.createElement('input');
            keyInput.className = 'kv-key input-param';
            keyInput.placeholder = '参数名';
            const valInput = document.createElement('input');
            valInput.className = 'kv-value input-param';
            valInput.placeholder = '值';
            const removeBtn = document.createElement('button');
            removeBtn.className = 'btn-remove';
            removeBtn.textContent = '✕';
            removeBtn.addEventListener('click', () => row.remove());
            row.appendChild(keyInput);
            row.appendChild(valInput);
            row.appendChild(removeBtn);
            section.appendChild(row);
            keyInput.focus();
        }

        // ============================================================
        // 工具函数
        // ============================================================
        function escapeHtml(str) {
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        }

        function toggleCollapse(id) {
            document.getElementById(id).classList.toggle('collapsed');
        }

        // ============================================================
        // 日志
        // ============================================================
        function addLog(method, path, status, ok, summary, fullBody) {
            const body = document.getElementById('logBody');
            const empty = body.querySelector('.log-empty');
            if (empty) body.innerHTML = '';

            const entry = document.createElement('div');
            entry.className = 'log-entry';
            entry.dataset.path = path;
            entry.dataset.body = fullBody || summary;

            const time = new Date().toLocaleTimeString();
            entry.innerHTML = `
                <span class="log-time">${time}</span>
                <span class="log-method ${method.toLowerCase()}">${method}</span>
                <span class="log-path">${escapeHtml(path)}</span>
                <span class="log-status ${ok ? 'ok' : 'err'}">${escapeHtml(status)}</span>
                <span style="color:#a6adc8;margin-left:6px">${escapeHtml(summary)}</span>
            `;

            entry.addEventListener('click', () => {
                openModal(entry.dataset.path, entry.dataset.body);
            });

            body.appendChild(entry);
            body.scrollTop = body.scrollHeight;

            document.getElementById('logCount').textContent = body.querySelectorAll('.log-entry').length + ' 条';
        }

        function clearLogs() {
            const body = document.getElementById('logBody');
            body.innerHTML = '<div class="log-empty">暂无测试记录</div>';
            document.getElementById('logCount').textContent = '0 条';
        }

        // ============================================================
        // 模态框
        // ============================================================
        function openModal(path, content) {
            document.getElementById('modalPath').textContent = path;
            document.getElementById('modalBody').textContent = content;
            document.getElementById('modalOverlay').classList.add('active');
            document.body.style.overflow = 'hidden';
        }

        function closeModal(e) {
            if (e && e.target !== e.currentTarget) return;
            document.getElementById('modalOverlay').classList.remove('active');
            document.body.style.overflow = '';
        }

        document.addEventListener('DOMContentLoaded', init);

        document.addEventListener('keydown', e => {
            if (e.key === 'Escape') closeModal();
        });
        </script>
        </body>
        </html>
        """";
}
