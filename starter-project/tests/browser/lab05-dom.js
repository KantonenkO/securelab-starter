// ЛР 5, відмінний рівень: відтворюваний DOM-тест у Console справжнього браузера (без додаткових framework).
// Запуск: http://localhost:5080/browser-lab.html -> увійти як alice (/auth-lab.html) -> оновити сторінку -> вставити файл цілком.
// Перевіряє ФАКТИЧНИЙ DOM (текстові вузли, відсутність елементів, відсутність дії marker), а не лише HTTP 200.
// На vulnerable commit тест падає (FAIL), на fixed - PASS. load() і #-елементи надає browser-lab.js/html.
await (async () => {
  const MARKER = `<img src=x onerror="document.querySelector('#lab05-marker-target').setAttribute('data-lab05-marker','executed')">`;
  const target = () => document.querySelector("#lab05-marker-target").getAttribute("data-lab05-marker");
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
  const suffix = Math.random().toString(36).slice(2, 8);
  let counter = 0; // назва кожного запису унікальна (інакше предметний конфлікт 409 з ЛР 2)
  const lines = [];
  check(target() === null, "Перезавантажте сторінку: атрибут marker уже встановлено");

  const create = async (title, description) => {
    // ЛР 6: змінювальний запит іде через єдиний helper antiforgery (csrf.js підключено на browser-lab.html).
    const response = await SecureLabCsrf.fetch("/api/incidents", { method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ title: `${title} ${suffix}-${++counter}`, description, severity: "Low", occurredAtUtc: new Date().toISOString() }) });
    check(response.status === 201, `POST ${title}: HTTP ${response.status}`);
    return (await response.json()).id;
  };
  const show = async id => {
    document.querySelector("#incident-id").value = id;
    await load();
    const response = await fetch(`/api/incidents/${id}`);
    check(response.status === 200, `GET HTTP ${response.status}`);
    return response.json();
  };

  // 1. Description: marker зберігається як є і відображається як текст
  const markerId = await create("DOM-тест marker", MARKER);
  const stored = await show(markerId);
  check(stored.description === MARKER, "API змінив збережене значення");
  const box = document.querySelector("#description");
  check(box.textContent === stored.description, "Description: текст у DOM відрізняється від збереженого");
  check(box.childElementCount === 0, "Description: дані стали HTML-елементами");
  await wait(500);
  check(target() === null, "Description: marker виконано");
  lines.push("PASS Description(marker): один текстовий вузол, елементів 0, marker не виконано");

  // 2. Позитивні рядки (апостроф/лапки, < >, Unicode, HTML-like, довгий текст)
  const normal = ["Користувач O'Brien написав \"терміново\"", "5 < 7 і 9 > 3", "Київ — Ω", "<b>жирний</b> текст", "А".repeat(3900)];
  for (const text of normal) {
    const stored2 = await show(await create("DOM-тест текст", text));
    check(stored2.description === text, "API змінив значення");
    check(box.textContent === text && box.childElementCount === 0, `Позитивний рядок відображено неправильно: ${text.slice(0, 20)}`);
  }
  lines.push(`PASS Позитивні рядки (${normal.length}): DOM.textContent == збережене значення, елементів 0`);

  // 3. IncidentComment.Text: кожен comments[].text як текст; повторний render без дублювання
  const seedId = "20000000-0000-0000-0000-000000000001";
  const seed = await show(seedId);
  const paragraphs = () => [...document.querySelectorAll("#comments > p")];
  check(paragraphs().length === seed.comments.length, "Кількість коментарів у DOM не збігається з JSON");
  seed.comments.forEach((comment, index) => {
    check(paragraphs()[index].textContent === comment.text, `Коментар ${index}: текст відрізняється`);
    check(paragraphs()[index].childElementCount === 0, `Коментар ${index}: дані стали HTML-елементами`);
  });
  const before = paragraphs().length;
  await load(); await load();
  check(paragraphs().length === before, "Повторний render продублював коментарі");
  await wait(500);
  check(target() === null, "Коментар: marker виконано");
  lines.push(`PASS Comments: ${before} елементів p, кожен text == comments[].text, елементів у p 0; повторний render ×3 без дублювання; marker не виконано`);
  lines.forEach(line => console.log(line));
  return lines.join("\n");
})();
