// ЛР 6 (F-01): єдиний клієнтський helper antiforgery для всіх сторінок із змінювальними запитами.
// Контракт із сервером (docs/security-review.md): GET /api/antiforgery/token -> { token }, header X-CSRF-TOKEN.
// Токен живе лише в пам'яті поточної сторінки (не в localStorage/sessionStorage/cookie/DOM) і не друкується в Console.
// F5 або перехід на іншу сторінку створює нову пам'ять -> токен запитується знову (lazy, перед першою mutation).
(() => {
  const HEADER_NAME = "X-CSRF-TOKEN";
  const TOKEN_ROUTE = "/api/antiforgery/token";
  const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);
  let requestToken = null;

  function assertSameOrigin(input) {
    const url = new URL(input instanceof Request ? input.url : input, location.origin);
    if (url.origin !== location.origin) throw new Error("Токен надсилається лише на адресу цього застосунку.");
  }

  async function ensureToken() {
    if (requestToken) return requestToken;
    const response = await fetch(TOKEN_ROUTE, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error(`Не вдалося отримати antiforgery token: HTTP ${response.status}`);
    requestToken = (await response.json()).token;
    return requestToken;
  }

  // Обгортка fetch: для безпечних методів нічого не змінює; для POST/PUT/PATCH/DELETE додає header токена.
  async function csrfFetch(input, init = {}) {
    const method = (init.method ?? (input instanceof Request ? input.method : "GET")).toUpperCase();
    if (SAFE_METHODS.has(method)) return fetch(input, init);
    assertSameOrigin(input);
    const headers = new Headers(init.headers ?? (input instanceof Request ? input.headers : undefined));
    headers.set(HEADER_NAME, await ensureToken());
    const response = await fetch(input, { ...init, headers, credentials: "same-origin" });
    if (response.status === 400) {
      const problem = await response.clone().json().catch(() => null);
      // Токен втратив чинність (інший principal, логін/логаут в іншій вкладці): наступна дія запитає новий.
      // POST автоматично НЕ повторюється: рішення про повтор приймає користувач.
      if (problem?.title?.startsWith("Antiforgery")) requestToken = null;
    }
    return response;
  }

  // Після login/logout principal змінюється: токен попереднього principal більше не діє.
  function clearToken() { requestToken = null; }

  window.SecureLabCsrf = Object.freeze({ fetch: csrfFetch, clear: clearToken, headerName: HEADER_NAME });
})();
