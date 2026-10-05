// 라이브 목록 새로고침을 전체 문서 reload 없이 부분 갱신한다.
export function startRefresh(onReplaced) {
  let pollTimer = 0;
  let isPolling = false;
  const reportedCollectionResults = new Set();

  function parseHtml(html) {
    return new DOMParser().parseFromString(html, "text/html");
  }

  function replaceSelector(nextDocument, selector) {
    const current = document.querySelector(selector);
    const next = nextDocument.querySelector(selector);
    if (!current || !next) {
      return;
    }

    const adopted = document.adoptNode(next);
    current.replaceWith(adopted);
    adopted.querySelectorAll("img[src]").forEach((img) => {
      const src = img.getAttribute("src");
      img.removeAttribute("src");
      img.setAttribute("src", src);
    });
  }

  function applyDocument(nextDocument) {
    replaceSelector(nextDocument, ".stream-count");
    replaceSelector(nextDocument, ".sync-state");
    replaceSelector(nextDocument, ".hero-stats");
    replaceSelector(nextDocument, "[data-live-content]");
    replaceSelector(nextDocument, "[data-featured-slot]");
  }

  async function refreshCurrentHtml() {
    const response = await fetch(window.location.href, {
      headers: { Accept: "text/html", "X-Requested-With": "fetch" },
    });
    if (!response.ok) {
      throw new Error("page refresh failed");
    }

    applyDocument(parseHtml(await response.text()));
    await onReplaced();
    startPollingIfNeeded();
  }

  async function submitRefresh(form) {
    const submitButton = form.querySelector("button[type='submit']");
    if (submitButton) {
      submitButton.disabled = true;
    }

    try {
      const response = await fetch(form.action, {
        method: "POST",
        body: new FormData(form),
        headers: { Accept: "text/html", "X-Requested-With": "fetch" },
      });
      if (!response.ok) {
        throw new Error("refresh request failed");
      }

      applyDocument(parseHtml(await response.text()));
      await onReplaced();
      startPollingIfNeeded();
    } catch {
      if (submitButton) {
        submitButton.disabled = false;
      }
    }
  }

  function schedulePoll(delay) {
    window.clearTimeout(pollTimer);
    pollTimer = window.setTimeout(checkStatus, delay);
  }

  function formatDuration(durationMs) {
    return `${(durationMs / 1000).toFixed(durationMs >= 10000 ? 1 : 2)}초`;
  }

  function reportCollectionResults(platforms) {
    (platforms || []).forEach((platform) => {
      if (platform.result === "Pending" || typeof platform.durationMs !== "number") {
        return;
      }

      const resultKey = [platform.platform, platform.result, platform.durationMs, platform.errorKind || ""].join(":");
      if (reportedCollectionResults.has(resultKey)) {
        return;
      }

      reportedCollectionResults.add(resultKey);
      const message = `[Raider 수집] ${platform.platform}: ${platform.result === "Success" ? "성공" : "실패"} (${formatDuration(platform.durationMs)}).`;
      if (platform.result === "Success") {
        console.info(message);
        return;
      }

      console.warn(`${message} API 오류: ${platform.errorKind || "Unknown"}.`);
    });
  }

  async function checkStatus() {
    const liveContent = document.querySelector("[data-live-content]");
    if (!liveContent) {
      isPolling = false;
      return;
    }

    const initialVersion = liveContent.dataset.snapshotVersion;
    try {
      const response = await fetch("/api/refresh/status", {
        headers: { Accept: "application/json", "X-Requested-With": "fetch" },
      });
      if (!response.ok) {
        throw new Error("refresh status failed");
      }

      const data = await response.json();
      if (data.snapshotVersion !== initialVersion || !data.isRefreshing) {
        if (!data.isRefreshing) {
          reportCollectionResults(data.platforms);
        }
        isPolling = false;
        await refreshCurrentHtml();
        return;
      }

      schedulePoll(700);
    } catch {
      schedulePoll(1200);
    }
  }

  function startPollingIfNeeded() {
    const liveContent = document.querySelector("[data-live-content]");
    if (!liveContent || liveContent.dataset.refreshing !== "true" || isPolling) {
      return;
    }

    isPolling = true;
    schedulePoll(250);
  }

  document.addEventListener("submit", (event) => {
    const form = event.target.closest("[data-refresh-form]");
    if (!form) {
      return;
    }

    event.preventDefault();
    void submitRefresh(form);
  });

  startPollingIfNeeded();
  return refreshCurrentHtml;
}
