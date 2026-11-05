import { HOST, ALARM, configFromUrl, bambooUrl, waitForSummary } from "./core.mjs";

let inFlight;
let actionEpoch = 0;

async function status(message, ok = false) {
  await chrome.storage.local.set({ status: { message, ok, at: new Date().toISOString() } });
  await chrome.action.setBadgeText({ text: ok ? "" : "!" });
  if (!ok) await chrome.action.setBadgeBackgroundColor({ color: "#B93E39" });
}

async function closeTemporary(record) {
  if (!record) return;
  try {
    const tab = await chrome.tabs.get(record.id);
    // Also clean up inactive SSO redirects, otherwise expired sessions would accumulate tabs.
    // A temporary tab the user activated is theirs to keep.
    if (!tab.active) await chrome.tabs.remove(record.id);
  } catch { /* The user may have already closed it. */ }
  await chrome.storage.session.remove("temporaryTab");
}

export async function syncWithConfig(rawConfig, existingTabId = null) {
  const epoch = actionEpoch;
  const config = configFromUrl(rawConfig.url, rawConfig.interval, rawConfig.month);
  if (!await chrome.permissions.contains({ origins: [config.origin + "/*"] })) {
    throw new Error("Company permission is missing. Use Connect on the Timesheet tab again.");
  }
  let temporary;
  try {
    let id = existingTabId;
    let reused = false;
    if (id === null) {
      const tabs = await chrome.tabs.query({ url: config.origin + "/*" });
      const available = tabs.filter(tab => {
        if (tab.discarded || tab.frozen || tab.status === "loading") return false;
        try { return bambooUrl(tab.url).href === config.url; } catch { return false; }
      }).sort((a, b) => Number(b.active) - Number(a.active));
      if (available.length) { id = available[0].id; reused = true; }
    }
    const createTemporary = async () => {
      const tab = await chrome.tabs.create({ url: config.url, active: false });
      temporary = { id: tab.id, origin: config.origin };
      await chrome.storage.session.set({ temporaryTab: temporary });
      id = tab.id;
      // Wait for initial navigation before injecting. No reload of the user's tabs.
      const started = Date.now();
      while (Date.now() - started < 8000) {
        const current = await chrome.tabs.get(id);
        if (current.status === "complete") break;
        await new Promise(resolve => setTimeout(resolve, 200));
      }
    };
    if (id === null) await createTemporary();
    const read = async () => {
      const tab = await chrome.tabs.get(id);
      if (tab.discarded || tab.frozen) throw new Error("Chrome suspended this Timesheet. The page cannot currently execute a read.");
      if (bambooUrl(tab.url).origin !== config.origin) throw new Error("Sign in to BambooHR in your normal Chrome session, then retry.");
      const results = await chrome.scripting.executeScript({ target: { tabId: id }, func: waitForSummary, args: [config.origin] });
      const result = results[0]?.result;
      if (!result || typeof result.text !== "string" || bambooUrl(result.pageUrl).origin !== config.origin)
        throw new Error("The page changed while reading. Open your own Timesheet and reconnect.");
      return result;
    };
    let capture;
    try { capture = await read(); }
    catch (error) {
      if (!reused) throw error;
      await createTemporary();
      capture = await read();
    }
    let reply;
    const request = { Command: "capture", CompanyUrl: config.origin, PageUrl: capture.pageUrl,
      SummaryText: capture.text, Tables: capture.tables ?? [], Cards: capture.cards ?? [], Month: config.month };
    if (new TextEncoder().encode(JSON.stringify(request)).byteLength > 900000) { request.Tables = []; request.Cards = []; }
    try {
      reply = await chrome.runtime.sendNativeMessage(HOST, request);
    } catch {
      throw new Error("The FlexHours helper is unavailable. Run setup-chrome.ps1 with this extension's ID, then retry.");
    }
    if (!reply?.ok) throw new Error(reply?.error || "FlexHours rejected this timesheet.");
    const daily = reply.dailyVerified ? "Daily worked/PTO/sick/holiday rows verified." : (reply.futureVerified ? "Pay Period total and future deduction verified; today's balance is available. Daily details need review: " : "Summary captured; daily rows need attention: ") + (reply.dailyWarning || "unsupported layout");
    const source = temporary ? "Background page" : "Existing Timesheet HTML";
    if (epoch === actionEpoch) await status(`${reply.month}: ${reply.total}. ${daily} ${source} read without switching tabs.`, reply.dailyVerified === true || reply.futureVerified === true);
    return reply;
  } finally { if (temporary) await closeTemporary(temporary); }
}

export function sync(config, existingTabId = null) {
  if (inFlight) return Promise.reject(new Error("A refresh is already running. Try again shortly."));
  const epoch = actionEpoch;
  inFlight = syncWithConfig(config, existingTabId).catch(async error => {
    if (epoch === actionEpoch) await status(error.message);
    throw error;
  }).finally(() => { inFlight = undefined; });
  return inFlight;
}

export async function refreshWatches(config) {
  const epoch = actionEpoch;
  const watches = config.watches?.length ? config.watches : [config];
  const completed = [], errors = [], warnings = [];
  for (const watch of watches) {
    if (epoch !== actionEpoch) return;
    try {
      const reply = await sync({ ...watch, interval: config.interval });
      completed.push(watch.month ?? "current month");
      if (!reply.dailyVerified) warnings.push(`${watch.month ?? "current month"}: ${reply.dailyWarning || "daily rows need review"}`);
    }
    catch (error) { errors.push(`${watch.month ?? "current month"}: ${error.message}`); }
  }
  if (epoch !== actionEpoch) return;
  if (errors.length) { await status(`Updated: ${completed.join(", ") || "none"}. ${errors.join(" ")}`); throw new Error(errors.join(" ")); }
  if (completed.length > 1) await status(`Captured watched months: ${completed.join(", ")}. ${warnings.length ? "Needs review: " + warnings.join(" ") : "Daily rows verified."}`, warnings.length === 0);
}

// Startup, popup and settings updates must never restore an older timer over a newer one.
let alarmQueue = Promise.resolve();
export function ensureAlarm(reset = false) {
  const operation = alarmQueue.then(async () => {
    const { config } = await chrome.storage.local.get("config");
    if (!config?.enabled) { await chrome.alarms.clear(ALARM); return null; }
    const interval = configFromUrl(config.url, config.interval, config.month).interval;
    let alarm = await chrome.alarms.get(ALARM);
    if (reset || !alarm || alarm.periodInMinutes !== interval) {
      await chrome.alarms.create(ALARM, { delayInMinutes: interval, periodInMinutes: interval });
      alarm = await chrome.alarms.get(ALARM);
    }
    if (!alarm || alarm.periodInMinutes !== interval || !Number.isFinite(alarm.scheduledTime))
      throw new Error("Chrome did not confirm the refresh timer. Apply the interval again.");
    return alarm;
  });
  alarmQueue = operation.catch(() => {});
  return operation;
}

export async function handleMessage(message) {
  if (message.type === "setInterval" || message.type === "resume") {
    const { config } = await chrome.storage.local.get("config");
    if (!config) throw new Error("Save your Timesheet connection first.");
    const interval = configFromUrl(config.url, message.type === "setInterval" ? message.interval : config.interval, config.month).interval;
    const updated = { ...config, interval, enabled: message.type === "resume" ? true : config.enabled };
    await chrome.storage.local.set({ config: updated });
    const alarm = await ensureAlarm(true);
    return { ok: true, interval, enabled: updated.enabled, alarm };
  }
  if (message.type === "connect") {
    const epoch = actionEpoch;
    const tab = Number.isInteger(message.tabId) ? await chrome.tabs.get(message.tabId) : null;
    const config = configFromUrl(message.url || tab?.url, message.interval, message.month);
    const { config: previous } = await chrome.storage.local.get("config");
    const watches = previous?.origin === config.origin ? [...(previous.watches ?? [configFromUrl(previous.url,previous.interval,previous.month)])] : [];
    const retained = watches.filter(watch => watch.month !== config.month);
    if (retained.length >= 6) throw new Error("A maximum of six watched months is supported. Pause and use Forget watched months to start again.");
    let matchingId = null;
    try { if (tab && bambooUrl(tab.url).href === config.url) matchingId = tab.id; } catch { /* Read the saved link instead of this unrelated tab. */ }
    await sync(config, matchingId); // Do not enable automation until the first read succeeds.
    if (epoch !== actionEpoch) return { ok: true };
    if(retained.some(watch=>watch.url===config.url)) {
      await status(`${config.month} captured for review. Existing automatic sync is unchanged. These months share a URL, so this future month cannot be reopened automatically yet.`,true);
      return {ok:true};
    }
    config.watches = [...retained, { url: config.url, month: config.month }];
    await chrome.storage.local.set({ config });
    await ensureAlarm();
    return { ok: true };
  }
  if (message.type === "pause") {
    actionEpoch++;
    const { config } = await chrome.storage.local.get("config");
    if (config) await chrome.storage.local.set({ config: { ...config, enabled: false } });
    await ensureAlarm();
    await status("Automatic Chrome refresh paused.", true);
    return { ok: true };
  }
  if (message.type === "forget") {
    actionEpoch++;
    await chrome.storage.local.remove("config");
    await ensureAlarm();
    await status("Watched months cleared. Connect a Timesheet to start again.", true);
    return { ok: true };
  }
  if (message.type === "refresh") {
    const { config } = await chrome.storage.local.get("config");
    if (!config?.enabled) throw new Error("Connect a Timesheet tab first.");
    if (message.month) {
      const watch = (config.watches?.length ? config.watches : [config])
        .map(item => configFromUrl(item.url, config.interval, item.month)).find(item => item.month === message.month);
      if (!watch) throw new Error("This month is not connected. Open its Timesheet and connect that month once.");
      let tabId = null;
      if (Number.isInteger(message.tabId)) {
        try {
          const tab = await chrome.tabs.get(message.tabId);
          if (bambooUrl(tab.url).href === bambooUrl(watch.url).href) tabId = tab.id;
        } catch { /* Use the saved permitted Timesheet instead. */ }
      }
      await sync({ ...watch, interval: config.interval }, tabId);
      return { ok: true };
    }
    await refreshWatches(config);
    return { ok: true };
  }
  throw new Error("Unknown extension action.");
}

chrome.runtime.onMessage.addListener((message, sender, respond) => {
  if (sender.id !== chrome.runtime.id || sender.tab) return false;
  handleMessage(message).then(respond, error => respond({ ok: false, error: error.message }));
  return true;
});
chrome.alarms.onAlarm.addListener(async alarm => {
  if (alarm.name !== ALARM) return;
  const { config } = await chrome.storage.local.get("config");
  if (config?.enabled) await refreshWatches(config).catch(() => {});
});
chrome.runtime.onStartup.addListener(async () => {
  const { temporaryTab } = await chrome.storage.session.get("temporaryTab");
  await closeTemporary(temporaryTab);
  await ensureAlarm();
  const { config } = await chrome.storage.local.get("config");
  if (config?.enabled) await refreshWatches(config).catch(() => {});
});
chrome.runtime.onInstalled.addListener(() => { ensureAlarm().catch(() => {}); });
ensureAlarm().catch(() => {});
