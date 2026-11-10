const latestButton = document.querySelector("#latest-button");
const latestResult = document.querySelector("#latest-result");

async function loadLatestIncident() {
  latestResult.textContent = "Завантаження…";

  try {
    const latest = await apiFetch("/api/incidents/latest");
    latestResult.textContent =
      `${latest.title} ` + `(створено ${latest.createdAtUtc})`;
  } catch (error) {
    latestResult.textContent = `Помилка: ${error.message}`;
  }
}

latestButton.addEventListener("click", loadLatestIncident);
