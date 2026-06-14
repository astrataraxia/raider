// 공용 즐겨찾기 토글과 반응형 사이드바를 제어한다.
(function () {
  "use strict";

  var sidebar = document.getElementById("favorites-sidebar");
  var list = document.getElementById("favorites-list");
  var status = document.querySelector(".favorites-status");
  var collapse = document.querySelector(".favorites-collapse");
  var mobileToggle = document.querySelector(".favorites-mobile-toggle");
  var backdrop = document.querySelector(".favorites-backdrop");
  var pageShell = document.querySelector(".page-shell");
  var siteHeader = document.querySelector(".site-header");
  var token = document.querySelector("[data-antiforgery-token] input")?.value;
  var mobileQuery = window.matchMedia("(max-width: 900px)");
  var favorites = new Map();

  if (!sidebar || !list || !status || !collapse || !mobileToggle || !backdrop || !pageShell || !siteHeader || !token) {
    return;
  }

  function key(platform, channelId) {
    return platform + ":" + channelId;
  }

  function setCollapsed(collapsed) {
    sidebar.classList.toggle("is-collapsed", collapsed);
    collapse.setAttribute("aria-expanded", collapsed ? "false" : "true");
    collapse.setAttribute("aria-label", collapsed ? "즐겨찾기 펼치기" : "즐겨찾기 접기");
    collapse.textContent = collapsed ? "›" : "‹";
    localStorage.setItem("raider.favorites.sidebarCollapsed", collapsed ? "true" : "false");
  }

  function setMobileOpen(open) {
    sidebar.classList.toggle("is-mobile-open", open);
    document.body.classList.toggle("favorites-drawer-open", open);
    mobileToggle.setAttribute("aria-expanded", open ? "true" : "false");
    mobileToggle.setAttribute("aria-label", open ? "즐겨찾기 닫기" : "즐겨찾기 열기");
    sidebar.inert = !open;
    pageShell.inert = open;
    siteHeader.inert = open;
    if (!open) {
      mobileToggle.focus();
    }
  }

  function syncViewport() {
    if (mobileQuery.matches) {
      sidebar.inert = !sidebar.classList.contains("is-mobile-open");
      return;
    }

    sidebar.classList.remove("is-mobile-open");
    document.body.classList.remove("favorites-drawer-open");
    mobileToggle.setAttribute("aria-expanded", "false");
    mobileToggle.setAttribute("aria-label", "즐겨찾기 열기");
    sidebar.inert = false;
    pageShell.inert = false;
    siteHeader.inert = false;
  }

  function render() {
    list.replaceChildren();
    document.querySelectorAll(".favorite-toggle").forEach(function (button) {
      var card = button.closest(".stream-card");
      var favorite = favorites.has(key(card.dataset.platform, card.dataset.channelId));
      button.classList.toggle("is-favorite", favorite);
      button.setAttribute("aria-pressed", favorite ? "true" : "false");
      button.setAttribute("aria-label", card.dataset.streamerName + (favorite ? " 즐겨찾기 제거" : " 즐겨찾기 추가"));
      button.setAttribute("title", favorite ? "즐겨찾기 제거" : "즐겨찾기 추가");
    });

    if (favorites.size === 0) {
      status.textContent = "즐겨찾기한 방송인이 없습니다.";
      return;
    }

    status.textContent = favorites.size + "명";
    favorites.forEach(function (favorite) {
      var item = document.createElement(favorite.watchUrl ? "a" : "div");
      item.className = "favorite-item is-" + favorite.status;
      if (favorite.watchUrl) {
        item.href = favorite.watchUrl;
        item.target = "_blank";
        item.rel = "noopener noreferrer";
      }

      var copy = document.createElement("span");
      copy.className = "favorite-item-copy";
      var name = document.createElement("strong");
      name.textContent = favorite.streamerName;
      var state = document.createElement("span");
      state.textContent = favorite.status === "live" ? "라이브" : favorite.status === "delayed" ? "상태 확인 지연" : "오프라인";
      copy.append(name, state);
      var dot = document.createElement("span");
      dot.className = "favorite-state-dot";
      dot.setAttribute("aria-hidden", "true");
      item.append(dot, copy);
      list.append(item);
    });
  }

  async function load() {
    try {
      var response = await fetch("/api/favorites", { headers: { Accept: "application/json" } });
      if (!response.ok) {
        throw new Error("favorites unavailable");
      }

      favorites.clear();
      (await response.json()).forEach(function (favorite) {
        favorites.set(key(favorite.platform, favorite.channelId), favorite);
      });
      render();
    } catch {
      status.textContent = "즐겨찾기를 불러오지 못했습니다.";
    }
  }

  async function toggle(button) {
    var card = button.closest(".stream-card");
    var favoriteKey = key(card.dataset.platform, card.dataset.channelId);
    var removing = favorites.has(favoriteKey);
    button.disabled = true;
    try {
      var response = await fetch("/api/favorites/" + encodeURIComponent(card.dataset.platform) + "/" + encodeURIComponent(card.dataset.channelId), {
        method: removing ? "DELETE" : "PUT",
        headers: { RequestVerificationToken: token }
      });
      if (!response.ok) {
        throw new Error("favorite update failed");
      }
      await load();
    } catch {
      status.textContent = "즐겨찾기를 변경하지 못했습니다.";
    } finally {
      button.disabled = false;
    }
  }

  document.querySelectorAll(".favorite-toggle").forEach(function (button) {
    button.addEventListener("click", function () { void toggle(button); });
  });
  collapse.addEventListener("click", function () {
    if (mobileQuery.matches) {
      setMobileOpen(false);
    } else {
      setCollapsed(!sidebar.classList.contains("is-collapsed"));
    }
  });
  mobileToggle.addEventListener("click", function () { setMobileOpen(!sidebar.classList.contains("is-mobile-open")); });
  backdrop.addEventListener("click", function () { setMobileOpen(false); });
  mobileQuery.addEventListener("change", syncViewport);
  document.addEventListener("keydown", function (event) {
    if (event.key === "Escape" && sidebar.classList.contains("is-mobile-open")) {
      setMobileOpen(false);
    }
  });

  setCollapsed(localStorage.getItem("raider.favorites.sidebarCollapsed") === "true");
  syncViewport();
  void load();
}());
