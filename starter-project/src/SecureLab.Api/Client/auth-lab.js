// ЛР 03: браузерний сценарій register -> login -> profile -> create -> logout.
// Не зберігає credentials чи principal у localStorage/sessionStorage/dataset і не пише їх у Console.
const result = document.querySelector("#result");
const stateBadge = document.querySelector("#state");
const profileBox = document.querySelector("#profile");

// Запити лише на той самий origin; cookie додає браузер (HttpOnly, JS її не бачить).
async function apiFetch(path, options = {}) {
  const url = new URL(path, location.origin);
  if (url.origin !== location.origin) throw new Error("Потрібна адреса цього застосунку.");
  return fetch(url, { ...options, credentials: "same-origin" });
}

function show(text) { result.textContent = text; }

function showState(state) {
  const labels = { "signed-in": "Сеанс активний", anonymous: "Не ввійшли", unknown: "Стан невідомий" };
  stateBadge.dataset.state = state;
  stateBadge.textContent = labels[state];
}

function clearProfile() { profileBox.replaceChildren(); }

// Лише дозволені поля DTO, виключно через textContent та створені DOM-вузли.
function renderProfile(me) {
  clearProfile();
  const rows = [["Id", me.id], ["Ім’я", me.userName], ["Відображуване", me.displayName], ["Ролі", (me.roles ?? []).join(", ")]];
  for (const [name, value] of rows) {
    const dt = document.createElement("dt"); dt.textContent = name;
    const dd = document.createElement("dd"); dd.textContent = String(value);
    profileBox.append(dt, dd);
  }
}

async function refreshSessionState() {
  try {
    const response = await apiFetch("/api/auth/session-check");
    if (response.status === 204) showState("signed-in");
    else if (response.status === 401) { showState("anonymous"); clearProfile(); }
    else showState("unknown");
  } catch { showState("unknown"); }
}

function asJson(form, mapper = value => value) {
  return JSON.stringify(mapper(Object.fromEntries(new FormData(form))));
}

async function postJson(path, body) {
  return apiFetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body });
}

document.querySelector("#register").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const response = await postJson("/api/auth/register", asJson(form));
  form.elements.password.value = "";
  if (response.status === 201) {
    const created = await response.json();
    show(`Реєстрація: 201. Створено «${created.userName}». Тепер увійдіть (автоматичного входу немає).`);
  } else {
    const problem = await response.json().catch(() => ({}));
    show(`Реєстрація: ${response.status}. ${problem.title ?? ""} ${Object.keys(problem.errors ?? {}).join(", ")}`);
  }
});

async function loadProfile() {
  const response = await apiFetch("/api/me");
  const isJson = response.headers.get("Content-Type")?.includes("application/json");
  if (response.status === 401) { clearProfile(); showState("anonymous"); show("Профіль: 401 (не ввійшли)"); return; }
  if (!response.ok || !isJson) { clearProfile(); show(`Профіль: ${response.status}`); return; }
  renderProfile(await response.json());
  show("Профіль: 200");
}

document.querySelector("#login").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  const response = await postJson("/api/auth/login", asJson(form));
  form.elements.password.value = "";
  show(`Login: ${response.status}`);
  await refreshSessionState();
  if (response.status === 204) await loadProfile();
});

document.querySelector("#me").addEventListener("click", loadProfile);

document.querySelector("#incident").addEventListener("submit", async event => {
  event.preventDefault();
  const form = event.currentTarget;
  // У JSON немає власника, ролі, статусу чи Id: ними керує сервер.
  const body = asJson(form, value => ({
    title: value.title, description: value.description, severity: value.severity,
    occurredAtUtc: new Date(value.occurredAtUtc).toISOString()
  }));
  const response = await postJson("/api/incidents", body);
  if (response.status === 201) {
    const created = await response.json();
    show(`Створення: 201. Інцидент «${created.title}», статус ${created.status}.`);
    form.reset();
  } else if (response.status === 401) {
    show("Створення: 401 — спершу увійдіть."); showState("anonymous"); clearProfile();
  } else {
    const problem = await response.json().catch(() => ({}));
    show(`Створення: ${response.status}. ${problem.title ?? ""} ${Object.keys(problem.errors ?? {}).join(", ")}`);
  }
});

document.querySelector("#logout").addEventListener("click", async () => {
  const response = await postJson("/api/auth/logout", "{}");
  clearProfile();
  document.querySelector("#incident").reset();
  await refreshSessionState();
  // Очищення UI не замінює sign-out: повторний /api/me має дати 401.
  const again = await apiFetch("/api/me");
  show(`Logout: ${response.status}; повторний /api/me: ${again.status}`);
});

refreshSessionState();
