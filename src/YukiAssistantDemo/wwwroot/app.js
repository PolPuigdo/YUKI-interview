(() => {
  "use strict";
  const form = document.querySelector("#chat-form");
  const input = document.querySelector("#message-input");
  const sendButton = document.querySelector("#send-button");
  const conversation = document.querySelector("#conversation");
  const welcome = document.querySelector("#welcome");

  function appendMessage(role, text, response) {
    const message = document.createElement("article");
    message.className = `message message-${role}`;
    const label = document.createElement("div");
    label.className = "message-label";
    label.textContent = role === "user" ? "You" : "Assistant";
    message.append(label);
    const bubble = document.createElement("div");
    bubble.className = "message-bubble";
    bubble.textContent = text;
    message.append(bubble);
    if (role === "assistant" && response && response.evidence) message.append(createEvidence(response));
    conversation.append(message);
    message.scrollIntoView({ behavior: "smooth", block: "nearest" });
  }

  function createEvidence(response) {
    const evidence = response.evidence;
    const card = document.createElement("section");
    card.className = "evidence-card";
    card.setAttribute("aria-label", "Answer evidence");
    const heading = document.createElement("div");
    heading.className = "evidence-heading";
    heading.textContent = "Evidence";
    card.append(heading);
    const summary = document.createElement("p");
    summary.className = "evidence-summary";
    summary.textContent = evidence.summary || "Source records used for this answer.";
    card.append(summary);
    const metadata = document.createElement("dl");
    metadata.className = "evidence-metadata";
    addMetadata(metadata, "Sources", formatSources(evidence.sourceIds));
    addMetadata(metadata, "Fresh as of", formatFreshness(evidence.freshness));
    if (response.intent) addMetadata(metadata, "Intent", response.intent);
    card.append(metadata);
    return card;
  }

  function addMetadata(container, label, value) {
    const term = document.createElement("dt");
    term.textContent = label;
    const description = document.createElement("dd");
    description.textContent = value;
    container.append(term, description);
  }

  function formatSources(sourceIds) {
    return Array.isArray(sourceIds) && sourceIds.length ? sourceIds.join(", ") : "Not available";
  }

  function formatFreshness(value) {
    if (!value) return "Not available";
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
  }

  function setLoading(loading) {
    input.disabled = loading;
    sendButton.disabled = loading;
    sendButton.textContent = loading ? "Checking…" : "Send ↗";
    if (loading) conversation.setAttribute("aria-busy", "true");
    else conversation.removeAttribute("aria-busy");
  }

  async function submitQuestion(question) {
    const message = question.trim();
    if (!message || sendButton.disabled) return;
    welcome.hidden = true;
    appendMessage("user", message);
    input.value = "";
    setLoading(true);
    try {
      const response = await fetch("/api/chat", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message })
      });
      let body;
      try { body = await response.json(); } catch { throw new Error("invalid-response"); }
      if (!response.ok || !body || typeof body.answer !== "string") throw new Error("request-failed");
      appendMessage("assistant", body.answer, body);
    } catch {
      appendMessage("assistant", "I couldn't reach the demo right now. Please try again in a moment.");
    } finally {
      setLoading(false);
      input.focus();
    }
  }

  form.addEventListener("submit", event => {
    event.preventDefault();
    submitQuestion(input.value);
  });
  document.querySelectorAll("[data-question]").forEach(button => {
    button.addEventListener("click", () => submitQuestion(button.dataset.question || ""));
  });
})();
