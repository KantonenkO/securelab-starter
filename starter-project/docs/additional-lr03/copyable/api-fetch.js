// options = {} застосовується, якщо аргумент не передано.
async function apiFetch(path, options = {}) {
  const response = await fetch(path, {
    // ... копіює поля іншого об’єкта.
    headers: {
      Accept: "application/json",
      ...options.headers,
    },
    ...options,
  });

  // ok дорівнює true для кодів 200–299.
  if (!response.ok) {
    // JSON помилки; для іншого формату — null.
    const problem = await response.json().catch(() => null);
    // Виняток обробить catch того, хто викликав apiFetch.
    throw new Error(problem?.title ?? `HTTP ${response.status}`);
  }

  // Читаємо JSON успішної відповіді.
  return response.json();
}
