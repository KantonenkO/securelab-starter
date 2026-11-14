let current;
const id = () => encodeURIComponent(document.querySelector("#incident-id").value.trim());
const status = document.querySelector("#status");
// ЛР 5: єдиний безпечний helper відображення. Description і IncidentComment.Text - звичайний текст (formatted HTML не
// дозволено), тому значення потрапляє лише в text node через textContent; структуру елементів задає лише код.
function renderText(element, value) {
  element.textContent = value ?? "";
}
function renderTextParagraphs(container, values) {
  const fragment = document.createDocumentFragment();
  for (const value of values) {
    const paragraph = document.createElement("p");
    renderText(paragraph, value);
    fragment.append(paragraph);
  }
  container.replaceChildren(fragment); // повторний render очищує попередній вміст, дублювання не виникає
}
async function load() {
  const response = await fetch(`/api/incidents/${id()}`);
  status.textContent = `GET: ${response.status}`;
  if (!response.ok) return;
  current = await response.json();
  renderText(document.querySelector("#title"), current.title);
  renderText(document.querySelector("#description"), current.description);
  renderTextParagraphs(document.querySelector("#comments"), (current.comments ?? []).map(comment => comment.text));
  document.querySelector("#edit").elements.description.value = current.description;
}
document.querySelector("#load").addEventListener("click", load);
document.querySelector("#edit").addEventListener("submit", async event => {
  event.preventDefault();
  const response = await fetch("/api/incidents", { method: "POST", headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title: event.currentTarget.elements.title.value, description: event.currentTarget.elements.description.value,
      severity: "Low", occurredAtUtc: new Date().toISOString() }) });
  status.textContent = `POST: ${response.status}`;
  if (response.ok) {
    const created = await response.json();
    document.querySelector("#incident-id").value = created.id;
    await load();
  }
});
