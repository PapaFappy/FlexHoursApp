import { bambooUrl, ALARM } from "./core.mjs";
const status = document.querySelector("#status");
const interval = document.querySelector("#interval");
const month = document.querySelector("#month");
const timesheet = document.querySelector("#timesheet");
let intervalDirty = false;
let paused = false;
interval.addEventListener("change", () => { intervalDirty = true; });
const today = new Date();
const requested = location.hash.match(/^#refresh=(\d{4}-(?:0[1-9]|1[0-2]))$/);
month.value = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;
if (requested) month.value = requested[1];
const buttons = [...document.querySelectorAll("button")];
function busy(value) { buttons.forEach(button => { button.disabled = value; }); interval.disabled = value; }
let statusRevision = 0;
async function showStatus() {
  const revision = ++statusRevision;
  const state = await chrome.storage.local.get(["status", "config"]);
  const alarm = await chrome.alarms.get(ALARM);
  if (revision !== statusRevision) return;
  if (state.config) {
    if (!intervalDirty) interval.value = state.config.interval;
    if (!timesheet.value) timesheet.value = state.config.url;
  }
  paused = state.config?.enabled === false;
  document.querySelector("#pause").textContent = paused ? "Resume" : "Pause";
  const schedule = document.querySelector("#schedule");
  schedule.textContent = !state.config ? "Save a connection to start automatic refresh." : paused ? "Automatic refresh paused." :
    alarm?.periodInMinutes !== Number(state.config.interval) ? "Timer does not match your saved interval. Click Apply interval to repair it." :
    Number.isFinite(alarm?.scheduledTime) ? "Every " + alarm.periodInMinutes + " min - next refresh around " + new Date(alarm.scheduledTime).toLocaleTimeString() : "Automatic timer unavailable. Apply the interval to restore it.";
  document.querySelector("#watched").textContent = state.config ? "Watched: " + (state.config.watches ?? [state.config]).map(watch => watch.month ?? "current month").join(", ") : "No watched months.";
  status.textContent = state.status ? state.status.message + "\n" + new Date(state.status.at).toLocaleString() : "No capture yet. Refresh uses your saved Timesheet link.";
}

async function action(message) {
  busy(true);
  status.textContent = message.type === "setInterval" ? "Saving refresh interval..." : "Updating...";
  try {
    const reply = await chrome.runtime.sendMessage(message);
    if (!reply?.ok) throw new Error(reply?.error || "Could not contact the extension.");
    if (message.type === "setInterval") {
      if (reply.interval !== message.interval || (reply.enabled && reply.alarm?.periodInMinutes !== message.interval))
        throw new Error("The new interval was not confirmed. Reload the existing extension and try Apply again.");
      intervalDirty = false;
    }
    await showStatus();
    if (message.type === "setInterval") status.textContent = "Saved: every " + reply.interval + " minute(s). " + (paused ? "Automatic refresh remains paused." : "Automatic refresh is running.");
  } catch (error) { status.textContent = error.message; }
  finally { busy(false); }
}
document.querySelector("#connect").addEventListener("click", async () => {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    const url = bambooUrl(timesheet.value.trim() || tab?.url);
    const granted = await chrome.permissions.request({ origins: [url.origin + "/*"] });
    if (!granted) throw new Error("Company access was not granted.");
    await action({ type: "connect", tabId: tab?.id, url: url.href, interval: Number(interval.value), month: month.value });
  } catch (error) { status.textContent = error.message; }
});
document.querySelector("#open").addEventListener("click", async () => {
  try { await chrome.tabs.create({ url: bambooUrl(timesheet.value.trim()).href, active: true }); }
  catch (error) { status.textContent = error.message; }
});
document.querySelector("#refresh").addEventListener("click", async () => {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  await action({ type: "refresh", month: month.value, tabId: tab?.id });
});
document.querySelector("#apply").addEventListener("click", () => action({ type: "setInterval", interval: Number(interval.value) }));
document.querySelector("#pause").addEventListener("click", () => action({ type: paused ? "resume" : "pause" }));
document.querySelector("#forget").addEventListener("click", () => action({ type: "forget" }));
chrome.storage.onChanged.addListener(() => { showStatus().catch(() => {}); });
showStatus().then(() => requested ? action({ type: "refresh", month: requested[1] }) : undefined).catch(error => { status.textContent = error.message; });
