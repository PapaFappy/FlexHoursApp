import test from 'node:test';
import assert from 'node:assert/strict';
import { bambooUrl, configFromUrl, HOST } from '../chrome-extension/core.mjs';

function mockChrome(overrides = {}) {
  const local = {}, session = {};
  const alarmState = new Map();
  const calls = { native: 0, removed: [], alarms: [], created: [], requests: [], readTabs: [] };
  const tabs = new Map([[1, { id: 1, url: 'https://example.bamboohr.com/employees/timesheet', active: true, status: 'complete' }]]);
  const area = object => ({
    async get(keys) { if (Array.isArray(keys)) return Object.fromEntries(keys.map(key => [key, object[key]])); return { [keys]: object[keys] }; },
    async set(value) { Object.assign(object, value); }, async remove(key) { delete object[key]; }
  });
  const event = { addListener() {} };
  const chrome = {
    storage: { local: area(local), session: area(session) },
    permissions: { async contains() { return overrides.permission !== false; } },
    action: { async setBadgeText() {}, async setBadgeBackgroundColor() {} },
    tabs: {
      async query() { return overrides.reuseTabs ? [...tabs.values()] : []; },
      async create(options) { const tab = { id: 2, url: overrides.redirectUrl ?? options.url, active: Boolean(overrides.activated), status: 'complete' }; tabs.set(2, tab); calls.created.push(options); return tab; },
      async get(id) { if (!tabs.has(id)) throw Error('No such tab'); return tabs.get(id); },
      async remove(id) { calls.removed.push(id); tabs.delete(id); }
    },
    scripting: { async executeScript(options) { calls.readTabs.push(options.target.tabId); if (overrides.scriptError || (overrides.failExisting && options.target.tabId === 1)) throw Error('Simulated page failure'); return [{ result: { text: 'fixture', pageUrl: overrides.pageUrl ?? 'https://example.bamboohr.com/employees/timesheet', tables: [] } }]; } },
    runtime: {
      id: 'a'.repeat(32), onMessage: event, onStartup: event, onInstalled: event,
      async sendNativeMessage(host, request) {
        calls.native++;
        calls.requests.push(request);
        assert.equal(host, HOST);
        assert.equal(request.Command, 'capture');
        assert.equal(request.CompanyUrl, 'https://example.bamboohr.com');
        assert.equal(request.SummaryText, 'fixture');
        assert.ok(!('cookies' in request));
        if (overrides.nativeError) throw Error('Host not found');
        if (overrides.onNative) await overrides.onNative();
        return overrides.reply ?? { ok: true, month: '2026-10', total: '133h 34m', dailyVerified: true };
      }
    },
    alarms: { onAlarm: event, async get(name) { if(overrides.onAlarmGet) await overrides.onAlarmGet(); return alarmState.get(name); }, async create(name, options) { if(overrides.onAlarmCreate) await overrides.onAlarmCreate(options); calls.alarms.push({ name, ...options }); alarmState.set(name, { name, ...options, scheduledTime: Date.now() + options.periodInMinutes * 60000 }); }, async clear(name) { alarmState.delete(name); } }
  };
  globalThis.chrome = chrome;
  return { chrome, calls, local, session, tabs, alarmState };
}
mockChrome();
const { syncWithConfig, handleMessage, refreshWatches, ensureAlarm } = await import('../chrome-extension/worker.mjs');
const config = configFromUrl('https://example.bamboohr.com/employees/timesheet', 5);

test('Only HTTPS company domains and approved refresh intervals are accepted', () => {
  assert.equal(bambooUrl(config.url).hostname, 'example.bamboohr.com');
  for (const bad of ['http://example.bamboohr.com/', 'https://example.bamboohr.com.evil.test/', 'https://user:pass@example.bamboohr.com/', 'https://example.bamboohr.com:444/', 'https://example-.bamboohr.com/', 'https://example.bamboohr.com/?access_token=secret']) {
    assert.throws(() => bambooUrl(bad));
  }
  assert.throws(() => configFromUrl(config.url, 0.1));
});

test('Existing signed-in tab capture does not create, reload or close user tabs', async () => {
  const state = mockChrome();
  await syncWithConfig(config, 1);
  assert.equal(state.calls.native, 1);
  assert.deepEqual(state.calls.created, []);
  assert.deepEqual(state.calls.removed, []);
  assert.match(state.local.status.message, /Daily.*verified/);
});

test('Automatic refresh uses an inactive temporary tab and cleans it up', async () => {
  const state = mockChrome();
  await syncWithConfig(config);
  assert.equal(state.calls.created[0].active, false);
  assert.deepEqual(state.calls.removed, [2]);
  assert.equal(state.session.temporaryTab, undefined);
});

test('Page failure closes temporary tab without sending unverified data', async () => {
  const state = mockChrome({ scriptError: true });
  await assert.rejects(syncWithConfig(config), /Simulated/);
  assert.equal(state.calls.native, 0);
  assert.deepEqual(state.calls.removed, [2]);
});

test('A temporary tab activated by the user is left open', async () => {
  const state = mockChrome({ activated: true });
  await syncWithConfig(config);
  assert.deepEqual(state.calls.removed, []);
});

test('Expired-session SSO redirects do not accumulate unattended tabs', async () => {
  const state = mockChrome({ redirectUrl: 'https://login.microsoftonline.com/' });
  await assert.rejects(syncWithConfig(config), /BambooHR/);
  assert.deepEqual(state.calls.removed, [2]);
  assert.equal(state.calls.native, 0);
});

test('Missing permission and wrong-origin capture are rejected', async () => {
  let state = mockChrome({ permission: false });
  await assert.rejects(syncWithConfig(config), /permission/);
  assert.equal(state.calls.native, 0);
  state = mockChrome({ pageUrl: 'https://other.bamboohr.com/timesheet' });
  await assert.rejects(syncWithConfig(config), /page changed/);
  assert.equal(state.calls.native, 0);
});

test('Missing native helper gives setup instructions and does not enable automation', async () => {
  const state = mockChrome({ nativeError: true });
  await assert.rejects(handleMessage({ type: 'connect', tabId: 1, interval: 5 }), /setup-chrome/);
  assert.equal(state.local.config, undefined);
});

test('Successful first capture saves configuration and creates a bounded alarm', async () => {
  const state = mockChrome();
  await handleMessage({ type: 'connect', tabId: 1, interval: 10 });
  assert.equal(state.local.config.enabled, true);
  assert.equal(state.calls.alarms[0].periodInMinutes, 10);
  await handleMessage({ type: 'pause' });
  assert.equal(state.local.config.enabled, false);
  await assert.rejects(handleMessage({ type: 'refresh' }), /Connect/);
});

test('Summary-only imports report that dated leave still needs attention', async () => {
  const state = mockChrome({ reply: { ok: true, total: '133h 34m', month: '2026-10', dailyVerified: false, dailyWarning: 'Missing weekday' } });
  await syncWithConfig(config, 1);
  assert.match(state.local.status.message, /Missing weekday/);
});

test('A page read waits for stable text, extracts visible tables and never reads cookies', async () => {
  const { waitForSummary } = await import('../chrome-extension/core.mjs');
  let disconnected = false;
  globalThis.location = { origin: 'https://example.bamboohr.com', href: config.url };
  globalThis.document = {
    body: { innerText: '133h 34m Pay Period Oct 1-31' }, documentElement: {},
    querySelectorAll() { return []; }
  };
  globalThis.MutationObserver = class { observe() {} disconnect() { disconnected = true; } };
  const capture = await waitForSummary('https://example.bamboohr.com');
  assert.equal(capture.text, document.body.innerText);
  assert.deepEqual(capture.tables, []);
  assert.equal(disconnected, true);
  assert.ok(!('cookie' in document));
  await assert.rejects(waitForSummary('https://other.bamboohr.com'), /Sign in/);
});

test('Adding December preserves the existing current-month watch and refreshes both', async () => {
  const state = mockChrome();
  await handleMessage({ type: 'connect', tabId: 1, interval: 5, month: '2026-10' });
  state.tabs.get(1).url = 'https://example.bamboohr.com/timesheet?month=2026-12';
  await handleMessage({ type: 'connect', tabId: 1, interval: 5, month: '2026-12' });
  assert.deepEqual(state.local.config.watches.map(watch => watch.month), ['2026-10', '2026-12']);
  const before = state.calls.requests.length;
  await refreshWatches(state.local.config);
  assert.deepEqual(state.calls.requests.slice(before).map(request => request.Month), ['2026-10', '2026-12']);
  await handleMessage({ type: 'forget' });
  assert.equal(state.local.config, undefined);
});

test('A future month sharing the same URL is captured once without replacing current sync', async () => {
  const state=mockChrome();
  await handleMessage({type:'connect',tabId:1,interval:5,month:'2026-10'});
  const original=structuredClone(state.local.config);
  await handleMessage({type:'connect',tabId:1,interval:5,month:'2026-12'});
  assert.deepEqual(state.local.config,original);
  assert.equal(state.calls.requests.at(-1).Month,'2026-12');
  assert.match(state.local.status.message,/Existing automatic sync is unchanged/);
});

test('DOM card boundaries keep a worked total before its date with the correct day', async () => {
  const { waitForSummary } = await import('../chrome-extension/core.mjs');
  function element(text, children = []) {
    const node = { innerText: text, children, parentElement: null, getClientRects: () => [{}] };
    node.contains = target => node === target || children.some(child => child.contains(target));
    children.forEach(child => { child.parentElement = node; });
    return node;
  }
  const date1 = element('Oct 1'), date2 = element('Oct 2');
  const row1 = element('9h 57m\nThu\nOct 1\n7:00 AM - 5:11 PM', [date1]);
  const row2 = element('2h 23m\nFri\nOct 2\n5 hours Malta Sick Leave', [date2]);
  const period = element(row1.innerText + '\n' + row2.innerText, [row1, row2]);
  const body = element('133h 34m Pay Period Oct 1-31\n' + period.innerText, [period]);
  globalThis.location = { origin: 'https://example.bamboohr.com', href: config.url };
  globalThis.document = { body, documentElement: {}, querySelectorAll(selector) { return selector.startsWith('table') ? [] : [period, row1, date1, row2, date2]; } };
  globalThis.MutationObserver = class { observe() {} disconnect() {} };
  const capture = await waitForSummary(location.origin);
  assert.equal(capture.cards.length, 2);
  assert.match(capture.cards.find(card => card.DateLabel === 'Oct 1').Text, /9h 57m/);
  assert.doesNotMatch(capture.cards.find(card => card.DateLabel === 'Oct 1').Text, /Sick Leave/);
  assert.match(capture.cards.find(card => card.DateLabel === 'Oct 2').Text, /5 hours Malta Sick Leave/);
});

test('Pause during connection cannot be undone by the pending capture', async () => {
  const state = mockChrome({ onNative: () => handleMessage({ type: 'pause' }) });
  state.local.config = { ...config, enabled: true };
  await handleMessage({ type: 'connect', tabId: 1, interval: 5 });
  assert.equal(state.local.config.enabled, false);
  assert.match(state.local.status.message, /paused/);
  assert.equal(state.calls.alarms.length, 0);
});

test('Forgetting during connection cannot restore the old watches', async () => {
  const state = mockChrome({ onNative: () => handleMessage({ type: 'forget' }) });
  state.local.config = { ...config, enabled: true };
  await handleMessage({ type: 'connect', tabId: 1, interval: 5 });
  assert.equal(state.local.config, undefined);
  assert.match(state.local.status.message, /cleared/);
});

test('Pause stops later watched months after the current capture finishes', async () => {
  const state = mockChrome({ onNative: () => handleMessage({ type: 'pause' }) });
  state.local.config = { ...config, watches: [{ ...config, month: '2026-10' }, { ...config, month: '2026-12' }] };
  await refreshWatches(state.local.config);
  assert.equal(state.calls.native, 1);
  assert.equal(state.local.config.enabled, false);
  assert.match(state.local.status.message, /paused/);
});

test('Multi-month refresh retains partial-read warnings', async () => {
  const state = mockChrome({ reply: { ok: true, month: '2026-10', total: '10h', dailyVerified: false, dailyWarning: 'Missing weekday' } });
  await refreshWatches({ ...config, watches: [{ ...config, month: '2026-10' }, { ...config, month: '2026-12' }] });
  assert.match(state.local.status.message, /Missing weekday/);
  assert.equal(state.local.status.ok, false);
});

test('A changing seconds clock does not prevent capturing the Pay Period summary', { timeout: 4000 }, async () => {
  const { waitForSummary } = await import('../chrome-extension/core.mjs');
  let ticks = 0, disconnected = false;
  globalThis.location = { origin: config.origin, href: config.url };
  globalThis.document = { body: { get innerText() { return `136h 56m Pay Period Oct 1-31\n03:22:${String(++ticks % 60).padStart(2, '0')}`; } },
    documentElement: {}, querySelectorAll() { return []; } };
  globalThis.MutationObserver = class { observe() {} disconnect() { disconnected = true; } };
  const capture = await waitForSummary(config.origin);
  assert.match(capture.text, /136h 56m Pay Period/);
  assert.ok(ticks >= 2);
  assert.equal(disconnected, true);
});

test('A changing Pay Period total still waits for the new summary to settle', { timeout: 4000 }, async () => {
  const { waitForSummary } = await import('../chrome-extension/core.mjs');
  let reads = 0;
  globalThis.location = { origin: config.origin, href: config.url };
  globalThis.document = { body: { get innerText() { return `${++reads === 1 ? '133h 34m' : '136h 56m'} Pay Period Oct 1-31`; } },
    documentElement: {}, querySelectorAll() { return []; } };
  globalThis.MutationObserver = class { observe() {} disconnect() {} };
  const capture = await waitForSummary(config.origin);
  assert.match(capture.text, /136h 56m/);
  assert.ok(reads >= 3);
});

test('One-minute refresh is supported without replacing the company connection', async () => {
  const state = mockChrome();
  await handleMessage({ type: 'connect', tabId: 1, interval: 1, month: '2026-10' });
  assert.equal(state.local.config.interval, 1);
  assert.equal(state.calls.alarms[0].periodInMinutes, 1);
  assert.equal(state.local.config.origin, config.origin);
});

test('A verified future deduction reports that the summary balance is available', async () => {
  const state = mockChrome({ reply: { ok: true, total: '136h 56m', month: '2026-10', dailyVerified: false, futureVerified: true, dailyWarning: 'Active day differs' } });
  await syncWithConfig(config, 1);
  assert.match(state.local.status.message, /today's balance is available/);
  assert.match(state.local.status.message, /Active day differs/);
  assert.equal(state.local.status.ok, true);
});

test('Manual selected-month refresh reads the existing active Timesheet immediately', async () => {
  const state = mockChrome();
  state.local.config = { ...config, watches: [{ url: config.url, month: '2026-10' }, { url: config.url + '?month=2026-12', month: '2026-12' }] };
  await handleMessage({ type: 'refresh', month: '2026-10', tabId: 1 });
  assert.deepEqual(state.calls.created, []);
  assert.equal(state.calls.requests.length, 1);
  assert.equal(state.calls.requests[0].Month, '2026-10');
  await assert.rejects(handleMessage({ type: 'refresh', month: '2027-01' }), /not connected/);
});

test('Desktop refresh link starts capture automatically and the popup can refresh its selected month', async () => {
  const state = mockChrome(), messages = [];
  state.local.config = { ...config };
  let storageChanged;
  state.chrome.storage.onChanged = { addListener(callback) { storageChanged = callback; } };
  state.chrome.tabs.query = async () => [{ id: 1 }];
  state.chrome.runtime.sendMessage = async message => { messages.push(message); return message.type === "setInterval" ? await handleMessage(message) : { ok: true }; };
  const elements = Object.fromEntries(['status','interval','month','timesheet','open','apply','schedule','watched','connect','refresh','pause','forget'].map(id => [id,
    { value: '', textContent: '', disabled: false, events: {}, addEventListener(event, callback) { this.events[event] = callback; } }]));
  globalThis.document = { querySelector(selector) { return elements[selector.slice(1)]; }, querySelectorAll() { return ['connect','refresh','pause','forget'].map(id => elements[id]); } };
  globalThis.location = { hash: '#refresh=2026-10' };
  await import('../chrome-extension/popup.mjs?desktop-refresh-test');
  await new Promise(resolve => setTimeout(resolve, 0));
  assert.deepEqual(messages[0], { type: 'refresh', month: '2026-10' });
  elements.month.value = '2026-12';
  await elements.refresh.events.click();
  assert.deepEqual(messages[1], { type: 'refresh', month: '2026-12', tabId: 1 });
  assert.equal(elements.refresh.disabled, false);
  elements.interval.value = '1';
  elements.interval.events.change();
  await elements.apply.events.click();
  assert.deepEqual(messages[2], {type:'setInterval', interval:1});
  assert.equal(Number(elements.interval.value), 1);
  assert.match(elements.schedule.textContent, /Every 1 min/);
  assert.match(elements.status.textContent, /Saved: every 1/);
  // An older status read must not replace the latest timer display.
  let releaseOld;
  state.chrome.alarms.get = () => new Promise(resolve => { releaseOld = resolve; });
  storageChanged();
  await new Promise(resolve => setTimeout(resolve,0));
  state.local.config.interval = 10;
  state.chrome.alarms.get = async () => ({periodInMinutes:10,scheduledTime:Date.now()+600000});
  storageChanged();
  await new Promise(resolve => setTimeout(resolve,0));
  releaseOld({periodInMinutes:1,scheduledTime:Date.now()+60000});
  await new Promise(resolve => setTimeout(resolve,0));
  assert.equal(Number(elements.interval.value),10);
  assert.match(elements.schedule.textContent,/Every 10 min/);
});

test('An existing connection without a saved month can refresh without repeating setup', async () => {
  const state = mockChrome();
  state.local.config = { url: config.url, origin: config.origin, interval: 5, enabled: true };
  const current = configFromUrl(config.url).month;
  await handleMessage({ type: 'refresh', month: current, tabId: 1 });
  assert.equal(state.calls.native, 1);
  assert.equal(state.calls.requests[0].Month, current);
  assert.equal(state.local.config.enabled, true);
});


test('Automatic capture reuses an inactive matching tab', async () => {
 const state=mockChrome({reuseTabs:true});state.tabs.get(1).active=false;
 await syncWithConfig(config);assert.deepEqual(state.calls.readTabs,[1]);assert.deepEqual(state.calls.created,[]);assert.deepEqual(state.calls.removed,[]);
});
test('Unavailable or different Timesheet tabs use a background helper', async () => {
 for(const change of [{frozen:true},{discarded:true},{url:config.url+'?id=99'}]) {
 const state=mockChrome({reuseTabs:true});Object.assign(state.tabs.get(1),change);await syncWithConfig(config);
 assert.deepEqual(state.calls.readTabs,[2]);assert.equal(state.calls.created[0].active,false);assert.ok(state.tabs.has(1));}
});
test('Failed existing read retries once without closing the user tab',async()=>{
 const state=mockChrome({reuseTabs:true,failExisting:true});await syncWithConfig(config);
 assert.deepEqual(state.calls.readTabs,[1,2]);assert.deepEqual(state.calls.removed,[2]);assert.equal(state.calls.native,1);
});
test('An exact link connects without an active BambooHR tab',async()=>{
 const state=mockChrome();await handleMessage({type:'connect',url:config.url,interval:5,month:'2026-10'});
 assert.equal(state.local.config.url,config.url);assert.equal(state.calls.created[0].url,config.url);
});
test('Background HTML can be read with no rendered rectangles', {timeout:4000},async()=>{
 const {waitForSummary}=await import('../chrome-extension/core.mjs');
 const text=value=>({nodeType:3,nodeValue:value});
 const el=(tagName,childNodes,hidden=false)=>({nodeType:1,tagName,childNodes,hidden,innerText:'',getClientRects:()=>[]});
 globalThis.getComputedStyle=()=>({display:'block',visibility:'visible'});
 globalThis.location={origin:config.origin,href:config.url};
 globalThis.document={body:el('BODY',[el('DIV',[text('136h 56m Pay Period Oct 1-31')]),el('SCRIPT',[text('SECRET')]),el('DIV',[text('HIDDEN')],true)]),documentElement:{},querySelectorAll:()=>[]};
 globalThis.MutationObserver=class{observe(){} disconnect(){}};
 try {const capture=await waitForSummary(config.origin);assert.match(capture.text,/136h 56m/);assert.doesNotMatch(capture.text,/SECRET|HIDDEN/);} finally {delete globalThis.getComputedStyle;}
});


test('Interval setter updates the alarm without reading or reconnecting the page', async()=>{
 const state=mockChrome();state.local.config={...config,watches:[{url:config.url,month:'2026-10'}]};
 await handleMessage({type:'setInterval',interval:1});
 assert.equal(state.local.config.interval,1);assert.equal(state.calls.alarms.at(-1).periodInMinutes,1);
 assert.equal(state.calls.alarms.at(-1).delayInMinutes,1);
 assert.deepEqual(state.local.config.watches,[{url:config.url,month:'2026-10'}]);
 assert.equal(state.calls.native,0);assert.deepEqual(state.calls.created,[]);
});
test('Invalid intervals leave the saved connection unchanged',async()=>{
 const state=mockChrome();state.local.config={...config};
 await assert.rejects(handleMessage({type:'setInterval',interval:0}),/interval/);
 assert.equal(state.local.config.interval,5);assert.equal(state.calls.alarms.length,0);
});
test('Interval changes preserve pause and Resume restarts the saved timer',async()=>{
 const state=mockChrome();state.local.config={...config,enabled:false};
 await handleMessage({type:'setInterval',interval:10});assert.equal(state.local.config.enabled,false);
 assert.equal(state.calls.alarms.length,0);
 await handleMessage({type:'resume'});assert.equal(state.local.config.enabled,true);
 assert.equal(state.calls.alarms.at(-1).periodInMinutes,10);assert.equal(state.calls.native,0);
});
test('Saved link connects while a different website is active',async()=>{
 const state=mockChrome();state.tabs.get(1).url='https://example.org/';
 await handleMessage({type:'connect',tabId:1,url:config.url,interval:5,month:'2026-10'});
 assert.equal(state.calls.created[0].url,config.url);assert.deepEqual(state.calls.readTabs,[2]);
});


test('An overlapping old timer restoration cannot overwrite Apply one minute',async()=>{
 let started, release;
 const entered=new Promise(resolve=>{started=resolve;});
 const blocked=new Promise(resolve=>{release=resolve;});
 const state=mockChrome({onAlarmCreate:async options=>{if(options.periodInMinutes===60){started();await blocked;}}});
 state.local.config={...config,interval:60};
 const restore=ensureAlarm();await entered;
 const applied=handleMessage({type:'setInterval',interval:1});
 await new Promise(resolve=>setTimeout(resolve,0));
 release();await restore;const reply=await applied;
 assert.equal(reply.interval,1);assert.equal(reply.alarm.periodInMinutes,1);
 assert.equal(state.local.config.interval,1);
 assert.equal((await state.chrome.alarms.get('flexhours-refresh')).periodInMinutes,1);
});

test('Reapplying the same interval resets its next alarm without a page read',async()=>{
 const state=mockChrome();state.local.config={...config,interval:1};
 await handleMessage({type:'setInterval',interval:1});
 const count=state.calls.alarms.length;
 await handleMessage({type:'setInterval',interval:1});
 assert.equal(state.calls.alarms.length,count+1);assert.equal(state.calls.native,0);
});

test('Apply reports failure when Chrome does not retain its timer',async()=>{
 const state=mockChrome();state.local.config={...config};
 state.chrome.alarms.create=async()=>{};
 await assert.rejects(handleMessage({type:'setInterval',interval:1}),/did not confirm/);
});
