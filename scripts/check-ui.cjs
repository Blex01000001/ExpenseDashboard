#!/usr/bin/env node
// 一個指令驗證畫面：啟動 app（用暫存資料庫，不碰 App_Data 的真實資料）→ 匯入虛構資料 → 建一條關鍵字規則
// → 各頁面在桌面 1280 與手機 375 截圖 → 檢查水平捲動與瀏覽器錯誤 → 關閉 app。
// 有問題時結束碼為 1。需要 Playwright（雲端環境已預裝）與 python3。
//
// 用法：node scripts/check-ui.cjs [--out DIR] [--csv FILE] [--readme]
//   --out     截圖輸出資料夾（預設 artifacts/ui，已被 .gitignore 排除）
//   --csv     改用指定的 CSV（預設用 scripts/demo-data.py 產生）
//   --readme  另外更新 docs/images/dashboard.png（README 用的截圖）
const { spawn, execFileSync } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { chromium } = require('playwright');

const root = path.resolve(__dirname, '..');
const args = process.argv.slice(2);
const option = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : fallback;
};
const outDir = path.resolve(root, option('--out', 'artifacts/ui'));
const port = 5199;
const baseUrl = `http://localhost:${port}`;

const pages = [
  { name: 'dashboard', path: '/', ready: '.month-card .apexcharts-svg' },
  { name: 'import', path: '/import', ready: 'input[type=file]' },
  { name: 'keywords', path: '/keywords', ready: '.rule-row' },
  { name: 'tags', path: '/tags', ready: '.rule-row' },
];
const viewports = [
  { name: 'desktop', width: 1280, height: 900 },
  { name: 'phone', width: 375, height: 800 },
];

async function waitForServer(server) {
  for (let i = 0; i < 120; i++) {
    if (server.exitCode !== null) throw new Error('app 啟動失敗，見上方 dotnet 輸出');
    try {
      if ((await fetch(baseUrl)).ok) return;
    } catch { /* 還沒起來 */ }
    await new Promise(r => setTimeout(r, 1000));
  }
  throw new Error('等 app 啟動逾時（120 秒）');
}

async function main() {
  fs.mkdirSync(outDir, { recursive: true });
  const work = fs.mkdtempSync(path.join(os.tmpdir(), 'expense-check-'));
  const csv = option('--csv') ? path.resolve(option('--csv')) : path.join(work, 'demo.csv');
  if (!option('--csv')) {
    execFileSync('python3', [path.join(root, 'scripts/demo-data.py'), csv], { stdio: 'inherit' });
  }

  // detached：dotnet run 會再開子程序，結束時要整個程序群組一起關。
  const server = spawn('dotnet', ['run', '--project', 'src/ExpenseDashboard.Web', '--urls', baseUrl], {
    cwd: root,
    detached: true,
    stdio: ['ignore', 'ignore', 'inherit'],
    env: { ...process.env, ConnectionStrings__Expenses: `Data Source=${path.join(work, 'check.db')}` },
  });
  const problems = [];
  const browser = await chromium.launch();
  try {
    await waitForServer(server);
    const page = await browser.newPage({ viewport: viewports[0] });
    page.on('pageerror', e => problems.push(`頁面錯誤：${e.message}`));
    page.on('console', m => m.type() === 'error' && problems.push(`console 錯誤：${m.text()}`));

    await page.goto(`${baseUrl}/import`);
    await page.waitForTimeout(1500);   // 等 Blazor 連線
    await page.setInputFiles('input[type=file]', csv);
    console.log((await (await page.waitForSelector('.import-summary')).textContent()).trim());

    await page.goto(`${baseUrl}/keywords`);
    await page.waitForTimeout(1500);
    await page.fill('.rule-form input', '聚餐娛樂');
    await page.fill('.rule-form textarea', '聚餐\n威秀');
    await page.click('.rule-form .btn-primary');
    await page.waitForSelector('.rule-row');

    for (const viewport of viewports) {
      await page.setViewportSize(viewport);
      for (const target of pages) {
        await page.goto(baseUrl + target.path);
        await page.waitForSelector(target.ready, { timeout: 15000 });
        await page.waitForTimeout(1500);   // 等圖表動畫
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        if (scrollWidth > viewport.width) {
          problems.push(`${target.name} @${viewport.width}px 有水平捲動（寬 ${scrollWidth}px）`);
        }
        const file = path.join(outDir, `${target.name}-${viewport.name}.png`);
        await page.screenshot({ path: file, fullPage: true });
        console.log(`✓ ${path.relative(root, file)}`);
      }
    }

    if (args.includes('--readme')) {
      await page.setViewportSize(viewports[0]);
      await page.goto(baseUrl);
      await page.waitForSelector('.month-card .apexcharts-svg');
      await page.waitForTimeout(2500);
      const bottom = await page.$eval('.month-grid .month-card', e => e.getBoundingClientRect().bottom + 16);
      const file = path.join(root, 'docs/images/dashboard.png');
      await page.screenshot({ path: file, fullPage: true, clip: { x: 0, y: 0, width: 1280, height: Math.ceil(bottom) } });
      console.log(`✓ ${path.relative(root, file)}`);
    }
  } finally {
    await browser.close();
    try { process.kill(-server.pid, 'SIGTERM'); } catch { /* 已結束 */ }
    fs.rmSync(work, { recursive: true, force: true });
  }

  if (problems.length > 0) {
    console.error(`\n✗ ${problems.length} 個問題：\n- ${problems.join('\n- ')}`);
    process.exit(1);
  }
  console.log('\n✓ 沒有發現問題');
}

main().catch(e => {
  console.error(`✗ ${e.message}`);
  process.exit(1);
});
