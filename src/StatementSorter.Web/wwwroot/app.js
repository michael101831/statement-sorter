const drop = document.querySelector("#drop");
const browse = document.querySelector("#browse");
const fileInput = document.querySelector("#file");
const sampleButton = document.querySelector("#sample");
const compareBrowse = document.querySelector("#compare-browse");
const compareSample = document.querySelector("#compare-sample");
const compareFile = document.querySelector("#compare-file");
const compareClear = document.querySelector("#compare-clear");
const resetButton = document.querySelector("#reset");
const error = document.querySelector("#error");
const results = document.querySelector("#results");
const categories = document.querySelector("#categories");

const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });

const goalForm = document.querySelector("#goal-form");
const goalName = document.querySelector("#goal-name");
const goalAmount = document.querySelector("#goal-amount");
const goalNote = document.querySelector("#goal-note");
const goalSave = document.querySelector("#goal-save");
const goalCancel = document.querySelector("#goal-cancel");
const goalError = document.querySelector("#goal-error");
const goalsElement = document.querySelector("#goals");
const goalsEmpty = document.querySelector("#goals-empty");
const savingsInput = document.querySelector("#savings-total");
const savingsLeft = document.querySelector("#savings-left");
const savingsError = document.querySelector("#savings-error");
const storageKey = "statement-sorter-goals";
const savingsKey = "statement-sorter-savings";
const capsKey = "statement-sorter-caps";
const oneOffKey = "statement-sorter-one-offs";
const accountBridge = {
  applying: false,
  timer: 0,
  signedIn: () => false,
  scheduleSave: () => {},
};

let current = null;
let previous = null;
let activeCategory = "all";
let goals = loadGoals();
let savingsTotal = loadSavings(goals);
let caps = loadCaps();
let oneOffs = loadOneOffs();
let editingId = null;
const hadLegacySavings = goals.some((goal) => goal.legacySaved > 0);
goals = goals.map(({ legacySaved, ...goal }) => goal);
if (hadLegacySavings) saveGoals();

browse.addEventListener("click", () => fileInput.click());
fileInput.addEventListener("change", () => {
  const file = fileInput.files && fileInput.files[0];
  fileInput.value = "";
  if (file) sortFile(file);
});

sampleButton.addEventListener("click", () => loadSample("/samples/messy-statement.csv", "messy-statement.csv", "current"));
compareSample.addEventListener("click", () => loadSample("/samples/earlier-statement.csv", "earlier-statement.csv", "previous"));
compareBrowse.addEventListener("click", () => compareFile.click());
compareFile.addEventListener("change", () => {
  const file = compareFile.files && compareFile.files[0];
  compareFile.value = "";
  if (file) sortFile(file, { role: current ? "previous" : "current" });
});
compareClear.addEventListener("click", () => {
  previous = null;
  if (current) render();
});

resetButton.addEventListener("click", () => {
  current = null;
  previous = null;
  activeCategory = "all";
  results.hidden = true;
  error.hidden = true;
  renderGoals();
});

categories.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-category]");
  if (!button || !current) return;
  activeCategory = button.dataset.category;
  renderCategories();
  renderTable();
});

window.addEventListener("dragover", (event) => event.preventDefault());
window.addEventListener("drop", (event) => {
  event.preventDefault();
  drop.classList.remove("hot");
  const file = event.dataTransfer && event.dataTransfer.files && event.dataTransfer.files[0];
  if (file) sortFile(file);
});
drop.addEventListener("dragenter", () => drop.classList.add("hot"));
drop.addEventListener("dragleave", (event) => {
  if (!drop.contains(event.relatedTarget)) drop.classList.remove("hot");
});

let sorterPromise = null;

function readySorter() {
  if (!sorterPromise) {
    sorterPromise = import("/_framework/dotnet.js").then(async ({ dotnet }) => {
      const api = await dotnet.create();
      const exports = await api.getAssemblyExports(api.getConfig().mainAssemblyName);
      const sorter = exports.StatementSorter.Wasm.SorterExports;
      if (!sorter || typeof sorter.SortCsv !== "function") throw new Error("The sorter runtime is missing.");
      return sorter;
    }).catch((err) => {
      sorterPromise = null;
      throw err;
    });
  }
  return sorterPromise;
}

async function sortCsv(text) {
  try {
    const sorter = await readySorter();
    const payload = JSON.parse(sorter.SortCsv(text));
    if (payload && payload.error && !payload.transactions) {
      return { ok: false, error: payload.error };
    }
    return { ok: true, payload };
  } catch {
    return null;
  }
}

async function loadSample(url, name, role) {
  setBusy(true);
  try {
    let text = "";
    try {
      const response = await fetch(url);
      if (response.ok) text = await response.text();
    } catch {
      text = "";
    }
    if (!text || text.trim().startsWith("<")) {
      const sorter = await readySorter();
      text = sorter.SampleCsv(name);
    }
    if (!text) throw new Error("The sample statement didn't load.");
    const file = new File([text], name, { type: "text/csv" });
    await sortFile(file, { alreadyBusy: true, role: role === "previous" && !current ? "current" : role });
  } catch (err) {
    setBusy(false);
    showError(err.message || "The sample statement didn't load.");
  }
}

async function sortFile(file, options = {}) {
  if (!options.alreadyBusy) setBusy(true);
  error.hidden = true;
  const role = options.role || "current";

  try {
    const text = await file.text();
    const sorted = await sortCsv(text);
    let payload = sorted && sorted.ok ? sorted.payload : null;
    if (sorted && !sorted.ok) {
      showError(sorted.error || "Couldn't read that file.");
      return;
    }

    if (!payload) {
      const body = new FormData();
      body.append("file", file, file.name);
      const response = await fetch("/api/sort", { method: "POST", body });
      payload = await response.json().catch(() => null);
      if (!response.ok) {
        showError((payload && payload.error) || "Couldn't read that file.");
        return;
      }
    }

    payload.fileName = file.name;
    if (role === "previous") previous = payload;
    else {
      current = payload;
      activeCategory = "all";
    }
    alignStatements();
    render();
  } catch {
    showError("Couldn't reach the sorter. Leave this page open and try again.");
  } finally {
    setBusy(false);
  }
}

function alignStatements() {
  if (!current || !previous) return;
  const currentEnd = endDate(current);
  const previousEnd = endDate(previous);
  if (currentEnd && previousEnd && previousEnd > currentEnd) {
    const swap = current;
    current = previous;
    previous = swap;
  }
}

function endDate(result) {
  const transactions = result.transactions;
  return transactions.length ? transactions[transactions.length - 1].date : "";
}

function render() {
  const transactions = current.transactions;
  document.querySelector("#file-name").textContent = previous
    ? `${current.fileName} · compared with ${previous.fileName}`
    : current.fileName;
  document.querySelector("#date-span").textContent = dateSpan(transactions);
  document.querySelector("#spent").textContent = money.format(current.totalSpent);
  document.querySelector("#received").textContent = money.format(current.totalReceived);
  const leftover = document.querySelector("#leftover");
  const leftOver = Number(current.habits.leftOver);
  leftover.textContent = leftOver < 0 ? formatSigned(leftOver) : money.format(leftOver);
  leftover.className = leftOver < 0 ? "negative" : "positive";
  document.querySelector("#txn-count").textContent = transactions.length ? ` · ${transactions.length}` : "";

  const aside = current.duplicates.length + current.issues.length;
  document.querySelector("#aside").textContent = String(aside);
  document.querySelector("#aside-note").textContent = asideNote(current);

  renderInsights();
  renderComparison();
  renderCategories();
  renderTable();
  renderGoals();
  renderList("#issues", "#issue-section", current.issues, (issue) => {
    const item = document.createElement("li");
    const title = document.createElement("p");
    title.textContent = `Row ${issue.lineNumber}`;
    const reason = document.createElement("span");
    reason.className = "reason";
    reason.textContent = ` — ${issue.reason}`;
    title.append(reason);
    const raw = document.createElement("code");
    raw.className = "raw";
    raw.textContent = issue.raw;
    item.append(title, raw);
    return item;
  });
  renderList("#dups", "#dup-section", current.duplicates, (row) => {
    const item = document.createElement("li");
    const title = document.createElement("p");
    title.textContent = `Row ${row.lineNumber} · ${row.description}`;
    const raw = document.createElement("span");
    raw.className = "raw";
    raw.textContent = formatSigned(row.amount);
    item.append(title, raw);
    return item;
  });

  results.hidden = false;
}

function renderInsights() {
  const list = document.querySelector("#insights");
  list.replaceChildren();
  const insights = current.habits.insights.map(adjustPaceInsight);
  for (const insight of [...insights, ...capInsights()]) {
    const card = document.createElement("article");
    const title = document.createElement("p");
    title.className = "insight-title";
    title.textContent = insight.title;
    const detail = document.createElement("p");
    detail.textContent = insight.detail;
    card.append(title, detail);
    list.append(card);
  }
}

function renderComparison() {
  const section = document.querySelector("#compare-section");
  if (!previous) {
    section.hidden = true;
    return;
  }

  section.hidden = false;
  document.querySelector("#compare-range").textContent =
    `${dateSpan(previous.transactions)}. Both files are stretched to 30 days, so a shorter export doesn't look cheaper.`;
  const nowPace = monthlyPace(recurringSpend(current), current.habits.periodDays);
  const thenPace = monthlyPace(recurringSpend(previous), previous.habits.periodDays);
  const overall = roundMoney(nowPace - thenPace);
  document.querySelector("#compare-overall").textContent = Math.abs(overall) < 1
    ? "Overall spending is about the same."
    : `Overall spending is ${overall > 0 ? "up" : "down"} ${money.format(Math.abs(overall))} every 30 days.`;

  const list = document.querySelector("#compare-list");
  list.replaceChildren();
  const changes = categoryChanges().slice(0, 6);
  if (changes.length === 0) {
    const item = document.createElement("li");
    item.textContent = "No category moved by a dollar or more.";
    list.append(item);
  } else {
    for (const change of changes) {
      const item = document.createElement("li");
      item.className = change.delta > 0 ? "up" : "down";
      const direction = change.delta > 0 ? "up" : "down";
      item.textContent = `${change.category} is ${direction} ${money.format(Math.abs(change.delta))} every 30 days.`;
      list.append(item);
    }
  }
  renderRepeats();
}

function renderRepeats() {
  const heading = document.querySelector("#repeat-heading");
  const list = document.querySelector("#repeat-list");
  const empty = document.querySelector("#repeat-empty");
  heading.hidden = false;
  list.replaceChildren();
  const repeats = repeatCharges();
  empty.hidden = repeats.length > 0;
  list.hidden = repeats.length === 0;
  for (const repeat of repeats) {
    const item = document.createElement("li");
    const closeness = repeat.exact ? "at" : "around";
    item.textContent = `${repeat.merchant} came back ${closeness} ${money.format(repeat.amount)}.`;
    list.append(item);
  }
}

function renderCategories() {
  categories.replaceChildren();
  const totals = current.totals;
  const max = Math.max(...totals.map((total) => Math.max(total.spent, total.received)), 0);
  const all = categoryButton("All transactions", `${current.transactions.length}`, "all", 0, false);
  categories.append(all);

  for (const total of totals) {
    const figure = total.spent > 0 ? money.format(total.spent) : formatSigned(total.received);
    const meta = total.count === 1 ? "1 transaction" : `${total.count} transactions`;
    const value = total.spent > 0 ? total.spent : total.received;
    const button = categoryButton(total.category, figure, total.category, max === 0 ? 0 : (value / max) * 100, total.spent === 0 && total.received > 0, meta);
    if (total.spent > 0) categories.append(categoryWithCap(button, total));
    else categories.append(button);
  }
}

function categoryWithCap(button, total) {
  const wrap = document.createElement("div");
  wrap.className = "category-wrap";
  const label = document.createElement("label");
  label.className = "cap-field";
  const caption = document.createElement("span");
  caption.textContent = "Cap";
  const input = document.createElement("input");
  input.type = "text";
  input.inputMode = "decimal";
  input.placeholder = "No cap";
  input.setAttribute("aria-label", `Monthly cap for ${total.category}`);
  if (Object.prototype.hasOwnProperty.call(caps, total.category)) input.value = String(caps[total.category]);
  input.addEventListener("click", (event) => event.stopPropagation());
  input.addEventListener("change", () => {
    const value = parseAmount(input.value);
    if (value === null || value < 0) delete caps[total.category];
    else caps[total.category] = value;
    saveCaps();
    renderInsights();
    renderGoals();
    const status = wrap.querySelector(".cap-status");
    if (status) status.remove();
    const next = capStatus(total);
    if (next) wrap.append(next);
  });
  label.append(caption, input);
  wrap.append(button, label);
  const status = capStatus(total);
  if (status) wrap.append(status);
  return wrap;
}

function capStatus(total) {
  const cap = caps[total.category];
  if (!(cap >= 0) || caps[total.category] === undefined) return null;
  const measured = measureCap(recurringCategorySpent(current, total.category), current.habits.periodDays, cap);
  const status = document.createElement("p");
  status.className = measured.overBy > 0 ? "cap-status over" : "cap-status under";
  status.textContent = measured.overBy > 0
    ? `${money.format(measured.monthly)} of a ${money.format(cap)} cap, over by ${money.format(measured.overBy)}`
    : measured.overBy < 0
      ? `${money.format(measured.monthly)} of a ${money.format(cap)} cap, under by ${money.format(Math.abs(measured.overBy))}`
      : `${money.format(measured.monthly)}, right on the ${money.format(cap)} cap`;
  return status;
}

function categoryButton(name, figure, id, width, received, meta) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "category";
  button.dataset.category = id;
  button.setAttribute("aria-pressed", String(activeCategory === id));
  if (activeCategory === id) button.classList.add("is-active");

  const label = document.createElement("span");
  label.className = "category-name";
  label.textContent = name;

  const amount = document.createElement("span");
  amount.className = "category-figure";
  amount.textContent = figure;

  button.append(label, amount);

  if (meta) {
    const note = document.createElement("span");
    note.className = "category-meta";
    note.textContent = meta;
    const bar = document.createElement("span");
    bar.className = received ? "bar received" : "bar";
    const fill = document.createElement("span");
    fill.style.width = `${width}%`;
    bar.append(fill);
    button.append(note, bar);
  }

  return button;
}

function renderTable() {
  const body = document.querySelector("#transactions");
  const rows = activeCategory === "all"
    ? current.transactions
    : current.transactions.filter((transaction) => transaction.category === activeCategory);

  const empty = document.querySelector("#empty-table");
  empty.textContent = current.transactions.length === 0
    ? "No transactions were found in that file."
    : "Nothing in that category.";
  empty.hidden = rows.length > 0;
  document.querySelector(".table-wrap").hidden = rows.length === 0;

  body.replaceChildren();
  for (const transaction of rows) {
    const row = document.createElement("tr");
    if (oneOffs.has(txnKey(transaction))) row.classList.add("is-one-off");
    row.append(
      cell(formatDate(transaction.date)),
      descriptionCell(transaction),
      cell(transaction.category),
      amountCell(transaction.amount),
    );
    body.append(row);
  }

}

function renderList(listId, sectionId, items, build) {
  const list = document.querySelector(listId);
  const section = document.querySelector(sectionId);
  list.replaceChildren();
  for (const item of items) list.append(build(item));
  section.hidden = items.length === 0;
}

function descriptionCell(transaction) {
  const td = document.createElement("td");
  const name = document.createElement("span");
  name.className = "desc";
  name.textContent = transaction.description;
  td.append(name);
  const source = document.createElement("span");
  source.className = "source";
  source.textContent = sourceLabel(transaction);
  td.append(source);
  if (canMarkOneOff(transaction)) td.append(oneOffControl(transaction));
  return td;
}

function oneOffControl(transaction) {
  const marked = oneOffs.has(txnKey(transaction));
  const wrap = document.createElement("span");
  const toggle = document.createElement("button");
  toggle.type = "button";
  toggle.className = "text-btn one-off-toggle";
  toggle.setAttribute("aria-pressed", String(marked));
  toggle.textContent = marked ? "Undo one-off" : "One-off";
  toggle.setAttribute(
    "aria-label",
    marked
      ? `Count ${transaction.description} in the monthly pace`
      : `Mark ${transaction.description} as a one-off`,
  );
  toggle.addEventListener("click", () => {
    const key = txnKey(transaction);
    if (oneOffs.has(key)) oneOffs.delete(key);
    else oneOffs.add(key);
    saveOneOffs();
    render();
  });
  wrap.append(toggle);
  if (marked) {
    const note = document.createElement("span");
    note.className = "one-off-note";
    note.textContent = "Left out of the 30-day pace.";
    wrap.append(note);
  }
  return wrap;
}

function amountCell(amount) {
  const td = document.createElement("td");
  td.className = amount < 0 ? "amount out" : amount > 0 ? "amount in" : "amount";
  td.textContent = formatSigned(amount);
  return td;
}

function cell(text) {
  const td = document.createElement("td");
  td.textContent = text;
  return td;
}

function sourceLabel(transaction) {
  if (transaction.source === "column") return "Category from the file";
  if (transaction.source === "rule") return `Matched “${transaction.matchedKeyword}”`;
  return "No rule matched";
}

function asideNote(result) {
  const parts = [];
  if (result.duplicates.length === 1) parts.push("1 duplicate");
  else if (result.duplicates.length > 1) parts.push(`${result.duplicates.length} duplicates`);
  if (result.issues.length === 1) parts.push("1 bad row");
  else if (result.issues.length > 1) parts.push(`${result.issues.length} bad rows`);
  return parts.length > 0 ? parts.join(", ") : "Duplicates and bad rows";
}

function dateSpan(transactions) {
  if (transactions.length === 0) return "No transactions in that file";
  const first = formatDate(transactions[0].date);
  const last = formatDate(transactions[transactions.length - 1].date);
  return first === last ? first : `${first} – ${last}`;
}

function formatDate(iso) {
  const [year, month, day] = iso.split("-").map(Number);
  return new Date(year, month - 1, day).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
  });
}

function formatSigned(value) {
  const formatted = money.format(Math.abs(value));
  if (value < 0) return `−${formatted}`;
  if (value > 0) return `+${formatted}`;
  return formatted;
}

function showError(message) {
  error.hidden = false;
  error.textContent = message;
}

function setBusy(busy) {
  browse.disabled = busy;
  sampleButton.disabled = busy;
  compareBrowse.disabled = busy;
  compareSample.disabled = busy;
  drop.classList.toggle("hot", false);
  document.querySelector(".drop-title").textContent = busy ? "Reading the statement…" : "Drop a statement here";
}

function roundMoney(value) {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

function monthlyPace(amount, days) {
  if (!(days > 0)) return 0;
  return roundMoney(amount / days * 30);
}

function measureCap(spent, days, cap) {
  const monthly = monthlyPace(spent, days);
  return { monthly, overBy: roundMoney(monthly - cap) };
}

function adjustPaceInsight(insight) {
  const aside = oneOffTotal(current);
  if (!(aside > 0)) return insight;
  if (insight.title !== "Left to save" && insight.title !== "Spending ran ahead" && insight.title !== "Even for this period") {
    return insight;
  }
  const days = current.habits.periodDays;
  const dayLabel = days === 1 ? "1 day" : `${days} days`;
  const left = roundMoney(Number(current.habits.leftOver) + aside);
  const pace = monthlyPace(left, days);
  const asideLabel = `${money.format(aside)} in one-off charges stays in this statement and is left out of the pace`;
  if (left > 0) {
    return {
      title: "Left to save",
      detail: `${money.format(Number(current.habits.leftOver))} was left after spending across ${dayLabel}. ${asideLabel}. About ${money.format(pace)} every 30 days can go toward a goal.`,
    };
  }
  if (left < 0) {
    return {
      title: "Spending ran ahead",
      detail: `Spending was ${money.format(-left)} more than income over ${dayLabel} after the one-off charges were set aside. Nothing is left for a goal until that gap closes.`,
    };
  }
  return {
    title: "Even for this period",
    detail: `After setting ${money.format(aside)} aside as a one-off, spending matches income over ${dayLabel}, so this pace doesn't fund a goal yet.`,
  };
}

function savingPace(result = current) {
  if (!result || !result.habits) return 0;
  const left = roundMoney(Number(result.habits.leftOver) + oneOffTotal(result));
  return monthlyPace(left, result.habits.periodDays);
}

function recurringSpend(result) {
  return roundMoney(Math.max(0, Number(result.totalSpent) - oneOffTotal(result)));
}

function oneOffTotal(result) {
  if (!result) return 0;
  let sum = 0;
  for (const transaction of result.transactions) {
    if (!canMarkOneOff(transaction) || !oneOffs.has(txnKey(transaction))) continue;
    sum += -Number(transaction.amount);
  }
  return roundMoney(sum);
}

function recurringCategorySpent(result, category) {
  if (!result) return 0;
  const total = result.totals.find((item) => item.category.toLowerCase() === category.toLowerCase());
  const base = total ? Number(total.spent) : 0;
  let aside = 0;
  for (const transaction of result.transactions) {
    if (transaction.category.toLowerCase() !== category.toLowerCase()) continue;
    if (!canMarkOneOff(transaction) || !oneOffs.has(txnKey(transaction))) continue;
    aside += -Number(transaction.amount);
  }
  return roundMoney(Math.max(0, base - aside));
}

function canMarkOneOff(transaction) {
  return Number(transaction.amount) < 0 && !isTransferOrIncome(transaction.category);
}

function txnKey(transaction) {
  return `${transaction.date}|${transaction.lineNumber}|${Number(transaction.amount).toFixed(2)}|${transaction.description}`;
}

function repeatCharges() {
  if (!current || !previous) return [];
  const now = merchantSpend(current);
  const then = merchantSpend(previous);
  const matches = [];
  for (const [key, later] of now) {
    const earlier = then.get(key);
    if (!earlier) continue;
    const delta = roundMoney(Math.abs(later.amount - earlier.amount));
    if (delta >= 1) continue;
    matches.push({ merchant: later.display, amount: later.amount, exact: delta === 0 });
  }
  matches.sort((a, b) => b.amount - a.amount || a.merchant.localeCompare(b.merchant));
  return matches;
}

function merchantSpend(result) {
  const groups = new Map();
  for (const transaction of result.transactions) {
    if (!canMarkOneOff(transaction)) continue;
    const key = merchantKey(transaction.description);
    if (!key) continue;
    const amount = roundMoney(-Number(transaction.amount));
    const existing = groups.get(key);
    if (existing) existing.amount = roundMoney(existing.amount + amount);
    else groups.set(key, { amount, display: merchantDisplay(transaction.description) });
  }
  return groups;
}

function merchantKey(description) {
  let text = String(description).toLowerCase();
  const cut = text.search(/[*#]/);
  if (cut > 0) text = text.slice(0, cut);
  return text.replace(/[^a-z0-9]+/g, " ").replace(/\s+/g, " ").trim();
}

function merchantDisplay(description) {
  let text = String(description);
  const cut = text.search(/[*#]/);
  if (cut > 0) text = text.slice(0, cut);
  return text.replace(/[\s,.;]+$/g, "").trim();
}

function loadOneOffs() {
  try {
    const saved = JSON.parse(localStorage.getItem(oneOffKey) || "[]");
    return new Set(Array.isArray(saved) ? saved.filter((key) => typeof key === "string") : []);
  } catch {
    return new Set();
  }
}

function saveOneOffs() {
  if (accountBridge.applying) return;
  if (accountBridge.signedIn()) {
    accountBridge.scheduleSave();
    return;
  }
  localStorage.setItem(oneOffKey, JSON.stringify([...oneOffs]));
}

function freedByCaps() {
  if (!current) return 0;
  let freed = 0;
  for (const [category, cap] of Object.entries(caps)) {
    const spent = recurringCategorySpent(current, category);
    const over = measureCap(spent, current.habits.periodDays, cap).overBy;
    if (over > 0) freed += over;
  }
  return roundMoney(freed);
}

function capInsights() {
  if (!current) return [];
  const cards = [];
  for (const [category, cap] of Object.entries(caps)) {
    const spent = recurringCategorySpent(current, category);
    const measured = measureCap(spent, current.habits.periodDays, cap);
    if (measured.overBy > 0) {
      cards.push({
        title: `${category} cap`,
        detail: `${category} is ${money.format(measured.overBy)} over the ${money.format(cap)} cap every 30 days. It's running at ${money.format(measured.monthly)}. Closing that gap adds ${money.format(measured.overBy)} a month toward a goal.`,
      });
    } else if (measured.overBy < 0) {
      cards.push({
        title: `${category} cap`,
        detail: `${category} is ${money.format(Math.abs(measured.overBy))} under the ${money.format(cap)} cap. It's running at ${money.format(measured.monthly)} every 30 days.`,
      });
    } else {
      cards.push({
        title: `${category} cap`,
        detail: `${category} is right on the ${money.format(cap)} cap.`,
      });
    }
  }
  return cards;
}

function categoryChanges() {
  const names = new Map();
  for (const total of current.totals) names.set(total.category.toLowerCase(), total.category);
  for (const total of previous.totals) {
    if (!names.has(total.category.toLowerCase())) names.set(total.category.toLowerCase(), total.category);
  }
  const changes = [];
  for (const [key, category] of names) {
    const now = current.totals.find((total) => total.category.toLowerCase() === key);
    const then = previous.totals.find((total) => total.category.toLowerCase() === key);
    const counts = (total) => total && total.spent > 0 && !isTransferOrIncome(total.category);
    if (!counts(now) && !counts(then)) continue;
    const delta = roundMoney(
      monthlyPace(now ? recurringCategorySpent(current, now.category) : 0, current.habits.periodDays) -
      monthlyPace(then ? recurringCategorySpent(previous, then.category) : 0, previous.habits.periodDays),
    );
    if (Math.abs(delta) < 1) continue;
    changes.push({ category, delta });
  }
  changes.sort((a, b) => Math.abs(b.delta) - Math.abs(a.delta) || a.category.localeCompare(b.category));
  return changes;
}

function isTransferOrIncome(category) {
  const name = category.toLowerCase();
  return name === "income" || name === "transfer" || name === "transfers" || name === "payment" || name === "payments";
}

function loadCaps() {
  try {
    const saved = JSON.parse(localStorage.getItem(capsKey) || "{}");
    if (!saved || Array.isArray(saved)) return {};
    const cleaned = {};
    for (const [category, cap] of Object.entries(saved)) {
      if (Number(cap) >= 0) cleaned[category] = Number(cap);
    }
    return cleaned;
  } catch {
    return {};
  }
}

function saveCaps() {
  if (accountBridge.applying) return;
  if (accountBridge.signedIn()) {
    accountBridge.scheduleSave();
    return;
  }
  localStorage.setItem(capsKey, JSON.stringify(caps));
}

savingsInput.addEventListener("change", () => {
  const raw = savingsInput.value.trim();
  const value = raw ? parseAmount(raw) : 0;
  if (value === null || value < 0) {
    savingsError.hidden = false;
    savingsError.textContent = "Already saved needs to be zero or more.";
    savingsInput.focus();
    return;
  }
  savingsError.hidden = true;
  savingsTotal = roundMoney(value);
  savingsInput.value = savingsTotal > 0 ? savingsTotal.toFixed(2) : "";
  saveSavings();
  renderGoals();
});

goalForm.addEventListener("submit", (event) => {
  event.preventDefault();
  const name = goalName.value.trim();
  const note = goalNote.value.trim();
  const target = parseAmount(goalAmount.value);
  if (!name) {
    showGoalError("Name what you're saving for.");
    goalName.focus();
    return;
  }
  if (target === null || target <= 0) {
    showGoalError("Enter an amount greater than zero.");
    goalAmount.focus();
    return;
  }

  if (editingId) {
    goals = goals.map((goal) => goal.id === editingId ? { ...goal, name, target, note } : goal);
  } else {
    goals = [...goals, { id: crypto.randomUUID(), name, target, note }];
  }
  saveGoals();
  resetGoalForm();
  renderGoals();
});

goalCancel.addEventListener("click", () => {
  resetGoalForm();
});

goalsElement.addEventListener("click", (event) => {
  const button = event.target.closest("button[data-action]");
  if (!button) return;
  const goal = goals.find((item) => item.id === button.dataset.id);
  if (!goal) return;
  if (button.dataset.action === "up" || button.dataset.action === "down") {
    const index = goals.findIndex((item) => item.id === goal.id);
    const swap = button.dataset.action === "up" ? index - 1 : index + 1;
    if (swap < 0 || swap >= goals.length) return;
    const next = goals.slice();
    [next[index], next[swap]] = [next[swap], next[index]];
    goals = next;
    saveGoals();
    renderGoals({ animate: true });
    return;
  }
  if (button.dataset.action === "remove") {
    goals = goals.filter((item) => item.id !== goal.id);
    if (editingId === goal.id) resetGoalForm();
    saveGoals();
    renderGoals();
    return;
  }
  editingId = goal.id;
  goalName.value = goal.name;
  goalAmount.value = goal.target.toFixed(2);
  goalNote.value = goal.note;
  goalSave.textContent = "Save changes";
  goalCancel.hidden = false;
  goalError.hidden = true;
  goalForm.scrollIntoView({ block: "nearest" });
  goalName.focus();
});

function renderGoals(options = {}) {
  const previous = options.animate ? measureGoalPositions() : null;
  const plans = planGoals();
  goalsElement.replaceChildren();
  plans.forEach((plan, index) => {
    const goal = plan.goal;
    const card = document.createElement("article");
    card.className = "goal";
    card.dataset.id = goal.id;

    const top = document.createElement("div");
    top.className = "goal-top";
    const titleWrap = document.createElement("div");
    titleWrap.className = "goal-title";
    const rank = document.createElement("p");
    rank.className = "goal-rank";
    rank.textContent = `Priority ${index + 1}`;
    const title = document.createElement("h3");
    title.textContent = goal.name;
    titleWrap.append(rank, title);
    const amount = document.createElement("p");
    amount.className = "goal-amount";
    amount.textContent = plan.applied > 0
      ? `${money.format(plan.applied)} of ${money.format(goal.target)}`
      : money.format(goal.target);
    top.append(titleWrap, amount);

    const wait = document.createElement("p");
    wait.className = "goal-wait";
    wait.textContent = formatWait(plan, plans);

    card.append(top, wait);
    if (goal.note) {
      const note = document.createElement("p");
      note.className = "goal-note";
      note.textContent = goal.note;
      card.append(note);
    }

    const actions = document.createElement("div");
    actions.className = "goal-actions";
    if (goals.length > 1) {
      actions.append(
        goalButton("Higher", "up", goal.id, { aria: `Higher priority for ${goal.name}`, disabled: index === 0 }),
        goalButton("Lower", "down", goal.id, { aria: `Lower priority for ${goal.name}`, disabled: index === goals.length - 1 }),
      );
    }
    actions.append(
      goalButton("Edit", "edit", goal.id),
      goalButton("Remove", "remove", goal.id, { danger: true }),
    );
    card.append(actions);
    goalsElement.append(card);
  });
  if (previous) slideGoals(previous);
  goalsEmpty.hidden = goals.length > 0;

  const unused = roundMoney(Math.max(0, savingsTotal) - plans.reduce((sum, plan) => sum + plan.applied, 0));
  if (savingsTotal > 0 && goals.length > 0 && unused > 0) {
    savingsLeft.hidden = false;
    savingsLeft.textContent = `${money.format(unused)} is still unassigned because every goal is covered.`;
  } else {
    savingsLeft.hidden = true;
    savingsLeft.textContent = "";
  }
}

function measureGoalPositions() {
  const positions = new Map();
  for (const card of goalsElement.querySelectorAll(".goal")) {
    if (card.dataset.id) positions.set(card.dataset.id, card.getBoundingClientRect().top);
  }
  return positions;
}

function slideGoals(previous) {
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  for (const card of goalsElement.querySelectorAll(".goal")) {
    const before = previous.get(card.dataset.id);
    if (before === undefined) continue;
    const dy = before - card.getBoundingClientRect().top;
    if (Math.abs(dy) < 1) continue;
    card.style.transform = `translateY(${dy}px)`;
    card.getBoundingClientRect();
    card.classList.add("is-moving");
    card.style.transform = "";
    card.addEventListener("transitionend", () => card.classList.remove("is-moving"), { once: true });
  }
}

function planGoals() {
  let remaining = roundMoney(Math.max(0, Number(savingsTotal) || 0));
  return goals.map((goal, index) => {
    const target = roundMoney(Number(goal.target));
    const applied = roundMoney(Math.min(target, remaining));
    remaining = roundMoney(remaining - applied);
    return { goal, index, applied, needed: roundMoney(Math.max(0, target - applied)) };
  });
}

function goalButton(label, action, id, options = {}) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = options.danger ? "text-btn danger" : "text-btn";
  button.dataset.action = action;
  button.dataset.id = id;
  button.textContent = label;
  if (options.aria) button.setAttribute("aria-label", options.aria);
  if (options.disabled) button.disabled = true;
  return button;
}

function formatWait(plan, plans) {
  const { applied, needed, goal, index } = plan;
  const ahead = roundMoney(plans.slice(0, index).reduce((sum, item) => sum + item.needed, 0));
  const throughHere = roundMoney(ahead + needed);
  const target = money.format(goal.target);

  if (needed === 0) {
    return `Covered from the shared savings. ${money.format(applied)} of ${target} is set aside.`;
  }

  const share = applied > 0
    ? `${money.format(applied)} of the shared savings is set aside, ${money.format(needed)} to go. `
    : "";
  if (!current || !current.habits || current.transactions.length === 0) {
    return share
      ? `${share}Load a statement to estimate the rest.`
      : "Load a statement to estimate how long this will take.";
  }

  const pace = savingPace();
  const freed = freedByCaps();
  const start = endDate(current);
  const base = describePace(throughHere, pace, start);
  const queue = ahead > 0 ? " It waits on the goals above it." : "";
  if (!(freed > 0)) return `${share}${base}${queue}`;

  const cappedPace = roundMoney(pace + freed);
  const baseMonths = pace > 0 ? Math.ceil(throughHere / pace) : null;
  const cappedMonths = cappedPace > 0 ? Math.ceil(throughHere / cappedPace) : null;
  const firstOpen = plans.find((item) => item.needed > 0);
  const capLine = firstOpen && firstOpen.index === index
    ? ` Staying inside the caps frees ${money.format(freed)} every 30 days.`
    : "";
  const changed = cappedMonths !== null && cappedMonths !== baseMonths ? ` ${describePace(throughHere, cappedPace, start)}` : "";
  return `${share}${base}${queue}${capLine}${changed}`;
}

function describePace(needed, pace, startIso) {
  if (!(pace > 0)) return "This statement doesn't leave money to save, so there isn't a timeline yet.";
  const paceLabel = `${money.format(pace)} every 30 days`;
  const months = needed / pace;
  let sentence;
  let shift;
  if (months < 1) {
    const days = Math.max(1, Math.ceil(months * 30));
    if (days < 14) {
      sentence = `About ${days} ${days === 1 ? "day" : "days"} at ${paceLabel}.`;
      shift = { days };
    } else {
      const weeks = Math.ceil(days / 7);
      sentence = `About ${weeks} weeks at ${paceLabel}.`;
      shift = { days: weeks * 7 };
    }
  } else {
    const whole = Math.ceil(months);
    if (whole >= 24) {
      const years = Math.ceil(whole / 12);
      sentence = `About ${years} ${years === 1 ? "year" : "years"} at ${paceLabel}.`;
      shift = { years };
    } else {
      sentence = `About ${whole} ${whole === 1 ? "month" : "months"} at ${paceLabel}.`;
      shift = { months: whole };
    }
  }
  const when = around(startIso, shift);
  return when ? `${sentence} ${when}` : sentence;
}

function around(startIso, shift) {
  if (!startIso) return "";
  const date = shiftDate(startIso, shift);
  const label = date.toLocaleDateString("en-US", shift.months || shift.years
    ? { month: "long", year: "numeric" }
    : { month: "short", day: "numeric", year: "numeric" });
  return `Around ${label}.`;
}

function shiftDate(iso, { days = 0, months = 0, years = 0 }) {
  const [year, month, day] = iso.split("-").map(Number);
  const date = new Date(year + years, month - 1 + months, 1);
  const last = new Date(date.getFullYear(), date.getMonth() + 1, 0).getDate();
  date.setDate(Math.min(day, last) + days);
  return date;
}

function parseAmount(raw) {
  const cleaned = String(raw).replace(/[$,\s]/g, "");
  if (!cleaned) return null;
  const value = Number(cleaned);
  return Number.isFinite(value) ? value : null;
}

function resetGoalForm() {
  editingId = null;
  goalForm.reset();
  goalSave.textContent = "Add goal";
  goalCancel.hidden = true;
  goalError.hidden = true;
}

function showGoalError(message) {
  goalError.hidden = false;
  goalError.textContent = message;
}

function loadGoals() {
  try {
    const saved = JSON.parse(localStorage.getItem(storageKey) || "[]");
    if (!Array.isArray(saved)) return [];
    return saved
      .filter((goal) => goal && typeof goal.name === "string" && Number(goal.target) > 0)
      .map((goal) => ({
        id: goal.id,
        name: goal.name,
        target: Number(goal.target),
        note: goal.note || "",
        legacySaved: Number(goal.saved) > 0 ? Number(goal.saved) : 0,
      }));
  } catch {
    return [];
  }
}

function loadSavings(loadedGoals) {
  const raw = localStorage.getItem(savingsKey);
  if (raw !== null) {
    const value = Number(raw);
    return Number.isFinite(value) && value > 0 ? roundMoney(value) : 0;
  }
  const migrated = roundMoney(loadedGoals.reduce((sum, goal) => sum + (goal.legacySaved || 0), 0));
  localStorage.setItem(savingsKey, String(migrated));
  return migrated;
}

function saveGoals() {
  if (accountBridge.applying) return;
  if (accountBridge.signedIn()) {
    accountBridge.scheduleSave();
    return;
  }
  localStorage.setItem(storageKey, JSON.stringify(goals));
}

function saveSavings() {
  if (accountBridge.applying) return;
  if (accountBridge.signedIn()) {
    accountBridge.scheduleSave();
    return;
  }
  localStorage.setItem(savingsKey, String(savingsTotal));
}

function readPersonal() {
  return {
    goals: goals.map((goal) => ({
      id: goal.id,
      name: goal.name,
      target: goal.target,
      note: goal.note || "",
    })),
    savings: roundMoney(Math.max(0, Number(savingsTotal) || 0)),
    caps: { ...caps },
    oneOffs: [...oneOffs],
  };
}

function applyPersonal(data) {
  accountBridge.applying = true;
  goals = normalizeGoals(data && data.goals);
  const saved = Number(data && data.savings);
  savingsTotal = Number.isFinite(saved) && saved > 0 ? roundMoney(saved) : 0;
  caps = normalizeCaps(data && data.caps);
  oneOffs = new Set(Array.isArray(data && data.oneOffs) ? data.oneOffs.filter((key) => typeof key === "string") : []);
  savingsInput.value = savingsTotal > 0 ? savingsTotal.toFixed(2) : "";
  savingsError.hidden = true;
  accountBridge.applying = false;
  renderGoals();
  if (current) render();
}

function reloadLocalPersonal() {
  accountBridge.applying = true;
  const loaded = loadGoals();
  goals = loaded.map(({ legacySaved, ...goal }) => goal);
  savingsTotal = loadSavings(loaded);
  caps = loadCaps();
  oneOffs = loadOneOffs();
  savingsInput.value = savingsTotal > 0 ? savingsTotal.toFixed(2) : "";
  savingsError.hidden = true;
  accountBridge.applying = false;
  renderGoals();
  if (current) render();
}

function normalizeGoals(saved) {
  if (!Array.isArray(saved)) return [];
  return saved
    .filter((goal) => goal && typeof goal.name === "string" && Number(goal.target) > 0)
    .map((goal) => ({
      id: typeof goal.id === "string" && goal.id ? goal.id : crypto.randomUUID(),
      name: goal.name,
      target: Number(goal.target),
      note: goal.note || "",
    }));
}

function normalizeCaps(saved) {
  if (!saved || Array.isArray(saved) || typeof saved !== "object") return {};
  const cleaned = {};
  for (const [category, cap] of Object.entries(saved)) {
    if (Number(cap) >= 0) cleaned[category] = Number(cap);
  }
  return cleaned;
}

window.statementSorterPersonal = {
  accountBridge,
  readPersonal,
  applyPersonal,
  reloadLocalPersonal,
};

if (savingsTotal > 0) savingsInput.value = savingsTotal.toFixed(2);
renderGoals();
