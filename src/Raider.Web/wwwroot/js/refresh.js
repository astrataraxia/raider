// 라이브 목록 새로고침을 전체 문서 reload 없이 부분 갱신한다.
(function () {
  "use strict";

  var pollTimer = 0;
  var isPolling = false;

  function parseHtml(html) {
    return new DOMParser().parseFromString(html, "text/html");
  }

  function replaceSelector(nextDocument, selector) {
    var current = document.querySelector(selector);
    var next = nextDocument.querySelector(selector);
    if (!current || !next) {
      return;
    }

    current.replaceWith(next);
  }

  async function refreshCurrentHtml() {
    var response = await fetch(window.location.href, {
      headers: { Accept: "text/html", "X-Requested-With": "fetch" }
    });
    if (!response.ok) {
      throw new Error("page refresh failed");
    }

    var nextDocument = parseHtml(await response.text());
    replaceSelector(nextDocument, ".stream-count");
    replaceSelector(nextDocument, ".sync-state");
    replaceSelector(nextDocument, ".hero-stats");
    replaceSelector(nextDocument, "[data-live-content]");

    await refreshFavorites();
    startPollingIfNeeded();
  }

  async function refreshFavorites() {
    if (window.RaiderFavorites && typeof window.RaiderFavorites.refresh === "function") {
      await window.RaiderFavorites.refresh();
    }
  }

  async function submitRefresh(form) {
    var submitButton = form.querySelector("button[type='submit']");
    if (submitButton) {
      submitButton.disabled = true;
    }

    try {
      var response = await fetch(form.action, {
        method: "POST",
        body: new FormData(form),
        headers: { Accept: "text/html", "X-Requested-With": "fetch" }
      });
      if (!response.ok) {
        throw new Error("refresh request failed");
      }

      var nextDocument = parseHtml(await response.text());
      replaceSelector(nextDocument, ".stream-count");
      replaceSelector(nextDocument, ".sync-state");
      replaceSelector(nextDocument, ".hero-stats");
      replaceSelector(nextDocument, "[data-live-content]");
      await refreshFavorites();
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

  async function checkStatus() {
    var liveContent = document.querySelector("[data-live-content]");
    if (!liveContent) {
      isPolling = false;
      return;
    }

    var initialVersion = liveContent.dataset.snapshotVersion;
    try {
      var response = await fetch("/api/refresh/status", {
        headers: { Accept: "application/json", "X-Requested-With": "fetch" }
      });
      if (!response.ok) {
        throw new Error("refresh status failed");
      }

      var data = await response.json();
      if (data.snapshotVersion !== initialVersion || !data.isRefreshing) {
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
    var liveContent = document.querySelector("[data-live-content]");
    if (!liveContent || liveContent.dataset.refreshing !== "true" || isPolling) {
      return;
    }

    isPolling = true;
    schedulePoll(250);
  }

  document.addEventListener("submit", function (event) {
    var form = event.target.closest("[data-refresh-form]");
    if (!form) {
      return;
    }

    event.preventDefault();
    void submitRefresh(form);
  });

  startPollingIfNeeded();
}());
