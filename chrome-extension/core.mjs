export const HOST = "com.flexhours.chrome_sync";
export const ALARM = "flexhours-refresh";
export const INTERVALS = [1, 5, 10, 15, 30, 60];

export function bambooUrl(value) {
  const url = new URL(value);
  if (url.protocol !== "https:" || url.port || url.username || url.password ||
      !/^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.bamboohr\.com$/i.test(url.hostname)) {
    throw new Error("Open your company's HTTPS BambooHR Timesheet first.");
  }
  if ([...url.searchParams.keys()].some(key => /token|password|session|^code$|^auth$/i.test(key)) ||
      /(?:access_token|id_token|password|session)=/i.test(url.hash)) {
    throw new Error("Finish signing in, then use the Timesheet page rather than a login callback.");
  }
  return url;
}

export function configFromUrl(url, interval = 5, month = null) {
  const parsed = bambooUrl(url);
  const minutes = Number(interval);
  if (!INTERVALS.includes(minutes)) throw new Error("Choose a refresh interval between 1 and 60 minutes.");
  const today = new Date();
  month = month || `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}`;
  if (!/^(?:[2-9]\d{3}|1[7-9]\d{2})-(0[1-9]|1[0-2])$/.test(month)) throw new Error("Choose the month shown in this Timesheet.");
  return { url: parsed.href, origin: parsed.origin, interval: minutes, month, enabled: true };
}

// Runs in an isolated script world on the permitted company page. No cookie API.
export async function waitForSummary(expectedOrigin) {
  if (location.origin !== expectedOrigin) throw new Error("Sign in to BambooHR in your normal Chrome session, then retry.");
  return await new Promise((resolve, reject) => {
    let debounce, settle, previousText;
    let readableCache = new WeakMap();
    const readable = element => {
      if (!element) return true;
      if (readableCache.has(element)) return readableCache.get(element);
      let result;
      if (typeof getComputedStyle === "function") {
        const style = getComputedStyle(element);
        result = !element.hidden && style.display !== "none" && style.visibility !== "hidden" && style.visibility !== "collapse"
          && (!element.parentElement || readable(element.parentElement));
      } else result = element.getClientRects?.().length > 0;
      readableCache.set(element, result);
      return result;
    };
    const textOf = element => {
      const rendered = element?.innerText;
      if (typeof rendered === "string" && rendered.trim()) return rendered;
      let count = 0, length = 0;
      const walk = node => {
        if (!node) return "";
        if (++count > 12000 || length > 200000) throw new Error("Timesheet HTML is too large to read safely.");
        if (node.nodeType === 3) { const value = node.nodeValue ?? ""; length += value.length; return value; }
        if (node.nodeType !== 1 || /^(SCRIPT|STYLE|NOSCRIPT|TEMPLATE)$/i.test(node.tagName) || !readable(node)) return "";
        const block = /^(DIV|P|BR|TR|LI|SECTION|ARTICLE|HEADER|FOOTER|H[1-6])$/i.test(node.tagName);
        return (block ? "\n" : "") + [...node.childNodes].map(walk).join("") + (block ? "\n" : "");
      };
      return walk(element);
    };
    const cleanup = () => { observer.disconnect(); clearTimeout(timeout); clearTimeout(debounce); clearTimeout(settle); };
    const inspect = () => {
      try {
      readableCache = new WeakMap();
      const text = textOf(document.body);
      if (/(?:\d{1,4}\s*h(?:\s*\d{1,2}\s*m)?\s*Pay\s*Period|Pay\s*Period\s*\d{1,4}\s*h)/i.test(text)) {
        const stableText = text.replace(/\b\d{1,2}:\d{2}:\d{2}\b/g, "[running clock]").replace(/\b\d{1,2}\s*s(?:ec(?:onds?)?)?\b/gi, "[seconds]");
        if (stableText !== previousText) {
          previousText = stableText;
          clearTimeout(settle);
          settle = setTimeout(inspect, 750);
          return;
        }
        cleanup();
        if (text.length > 200000) { reject(new Error("Timesheet page is too large to read safely; use the app's paste fallback.")); return; }
        const tables = [...document.querySelectorAll('table,[role="table"],[role="grid"]')]
          .filter(readable).slice(0, 12).map(table => ({
            Headers: [...table.querySelectorAll('thead th,[role="columnheader"]')].slice(0, 24).map(cell => textOf(cell).trim()),
            Rows: [...table.querySelectorAll('tbody tr,[role="row"]')].slice(0, 100).map(row =>
              [...row.querySelectorAll('td,[role="cell"],[role="gridcell"]')].slice(0, 24).map(cell => textOf(cell).slice(0, 500)))
          }));
        // Identify each date's enclosing card rather than depending on CSS text order.
        const datePattern = /^(?:(?:Mon(?:day)?|Tue(?:sday)?|Wed(?:nesday)?|Thu(?:rsday)?|Fri(?:day)?|Sat(?:urday)?|Sun(?:day)?)[,\s]+)?(Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s*(\d{1,2})(?:,?\s+(\d{4}))?$/i;
        const markers = [...document.querySelectorAll('time,span,div,p,td,[role="cell"]')].slice(0, 6000)
          .filter(readable)
          .map(element => ({ element, match: textOf(element).replace(/\s+/g, " ").trim().match(datePattern) }))
          .filter(item => item.match).slice(0, 300)
          .map(item => ({ ...item, key: `${item.match[1].slice(0,3).toLowerCase()}-${Number(item.match[2])}-${item.match[3] ?? ""}` }));
        const byDate = new Map();
        for (const marker of markers) {
          let card = marker.element;
          for (let parent = card.parentElement, depth = 0; parent && parent !== document.body && depth < 8; parent = parent.parentElement, depth++) {
            const keys = new Set(markers.filter(item => parent.contains(item.element)).map(item => item.key));
            if (keys.size !== 1 || textOf(parent).length > 12000) break;
            card = parent;
          }
          const candidate = { DateLabel: `${marker.match[1]} ${marker.match[2]}${marker.match[3] ? " " + marker.match[3] : ""}`, Text: textOf(card) };
          if (!byDate.has(marker.key) || candidate.Text.length > byDate.get(marker.key).Text.length) byDate.set(marker.key, candidate);
        }
        const cards = [...byDate.values()];
        resolve({ text, pageUrl: location.href, tables, cards });
      }
      } catch (error) { cleanup(); reject(error); }
    };
    const observer = new MutationObserver(() => { clearTimeout(debounce); debounce = setTimeout(inspect, 250); });
    const timeout = setTimeout(() => {
      cleanup();
      reject(new Error("No timesheet summary appeared. Check your login and the saved current-month Timesheet page."));
    }, 20000);
    observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    inspect();
  });
}
