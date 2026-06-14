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
      var customCategories = [];
      try {
        customCategories = JSON.parse(localStorage.getItem("raider.favorites.customCategories") || "[]");
      } catch (err) {}
      if (customCategories.length === 0) {
        status.textContent = "즐겨찾기한 방송인이 없습니다.";
        return;
      }
    }

    status.textContent = favorites.size + "명";

    var grouped = {};
    var allCategories = new Set(["기본"]);

    var customCategories = [];
    try {
      customCategories = JSON.parse(localStorage.getItem("raider.favorites.customCategories") || "[]");
    } catch (err) {}
    customCategories.forEach(function (cat) {
      allCategories.add(cat);
      if (!grouped[cat]) {
        grouped[cat] = [];
      }
    });

    favorites.forEach(function (fav) {
      var cat = fav.category || "기본";
      allCategories.add(cat);
      if (!grouped[cat]) {
        grouped[cat] = [];
      }
      grouped[cat].push(fav);
    });

    var orderMap = {};
    var categoryOrder = [];
    try {
      categoryOrder = JSON.parse(localStorage.getItem("raider.favorites.categoryOrder") || "[]");
    } catch (e) {}
    categoryOrder.forEach(function (c, i) {
      orderMap[c] = i;
    });

    var categoriesList = Array.from(allCategories).sort(function (a, b) {
      var idxA = orderMap[a] !== undefined ? orderMap[a] : 999999;
      var idxB = orderMap[b] !== undefined ? orderMap[b] : 999999;
      if (idxA !== idxB) {
        return idxA - idxB;
      }
      if (a === "기본") return -1;
      if (b === "기본") return 1;
      return a.localeCompare(b);
    });

    var collapsedCategories = new Set();
    try {
      var storedCollapsed = JSON.parse(localStorage.getItem("raider.favorites.collapsedCategories") || "[]");
      storedCollapsed.forEach(function (c) { collapsedCategories.add(c); });
    } catch (e) {}

    categoriesList.forEach(function (cat) {
      var items = grouped[cat];
      if ((!items || items.length === 0) && !customCategories.includes(cat)) {
        return;
      }

      var isCollapsed = collapsedCategories.has(cat);

      var groupDiv = document.createElement("div");
      groupDiv.className = "favorites-category";
      groupDiv.dataset.category = cat;
      if (isCollapsed) {
        groupDiv.classList.add("is-collapsed");
      }

      // 1. COLLAPSIBLE HEADER
      var headerDiv = document.createElement("div");
      headerDiv.className = "category-header";
      headerDiv.draggable = true;
      headerDiv.dataset.category = cat;

      var toggleSpan = document.createElement("span");
      toggleSpan.className = "category-toggle-icon";
      toggleSpan.textContent = isCollapsed ? "▶" : "▼";
      
      var iconFolderSpan = document.createElement("span");
      iconFolderSpan.textContent = "📁 " + cat;

      headerDiv.append(toggleSpan, iconFolderSpan);
      groupDiv.append(headerDiv);

      headerDiv.addEventListener("click", function (e) {
        if (collapsedCategories.has(cat)) {
          collapsedCategories.delete(cat);
        } else {
          collapsedCategories.add(cat);
        }
        localStorage.setItem("raider.favorites.collapsedCategories", JSON.stringify(Array.from(collapsedCategories)));
        render();
      });

      // DRAG AND DROP FOR CATEGORY REORDERING
      headerDiv.addEventListener("dragstart", function (e) {
        e.dataTransfer.setData("text/plain", JSON.stringify({ type: "category", category: cat }));
        e.dataTransfer.effectAllowed = "move";
      });

      headerDiv.addEventListener("dragover", function (e) {
        e.preventDefault();
        headerDiv.classList.add("category-drag-over");
      });

      headerDiv.addEventListener("dragenter", function (e) {
        e.preventDefault();
        headerDiv.classList.add("category-drag-over");
      });

      headerDiv.addEventListener("dragleave", function () {
        headerDiv.classList.remove("category-drag-over");
      });

      headerDiv.addEventListener("drop", function (e) {
        e.preventDefault();
        headerDiv.classList.remove("category-drag-over");
        try {
          var data = JSON.parse(e.dataTransfer.getData("text/plain") || "{}");
          if (data.type === "category" && data.category !== cat) {
            var currentOrder = Array.from(categoriesList);
            var fromIdx = currentOrder.indexOf(data.category);
            var toIdx = currentOrder.indexOf(cat);
            if (fromIdx > -1 && toIdx > -1) {
              currentOrder.splice(fromIdx, 1);
              currentOrder.splice(toIdx, 0, data.category);
              localStorage.setItem("raider.favorites.categoryOrder", JSON.stringify(currentOrder));
              render();
            }
          }
        } catch (err) {}
      });

      // DRAG AND DROP FOR STREAMERS DROPPED ON CATEGORY
      groupDiv.addEventListener("dragover", function (e) {
        e.preventDefault();
        groupDiv.classList.add("drag-over");
      });

      groupDiv.addEventListener("dragenter", function (e) {
        e.preventDefault();
        groupDiv.classList.add("drag-over");
      });

      groupDiv.addEventListener("dragleave", function () {
        groupDiv.classList.remove("drag-over");
      });

      groupDiv.addEventListener("drop", function (e) {
        e.preventDefault();
        groupDiv.classList.remove("drag-over");
        try {
          var data = JSON.parse(e.dataTransfer.getData("text/plain") || "{}");
          if (data.type === "streamer" && data.sourceCategory !== cat) {
            var customCats = [];
            try {
              customCats = JSON.parse(localStorage.getItem("raider.favorites.customCategories") || "[]");
            } catch (err) {}
            var idx = customCats.indexOf(cat);
            if (idx > -1) {
              customCats.splice(idx, 1);
              localStorage.setItem("raider.favorites.customCategories", JSON.stringify(customCats));
            }
            void updateCategory(data.platform, data.channelId, cat);
          }
        } catch (err) {}
      });

      // 2. ITEMS CONTAINER
      var itemsDiv = document.createElement("div");
      itemsDiv.className = "category-items";

      if (items && items.length > 0) {
        // Sort items by status (live first) and viewerCount descending, then streamerName
        items.sort(function (a, b) {
          var aLive = a.status === "live" ? 1 : 0;
          var bLive = b.status === "live" ? 1 : 0;
          if (aLive !== bLive) {
            return bLive - aLive;
          }
          if (a.status === "live") {
            var aView = a.viewerCount || 0;
            var bView = b.viewerCount || 0;
            if (aView !== bView) {
              return bView - aView;
            }
          }
          return a.streamerName.localeCompare(b.streamerName);
        });

        items.forEach(function (fav) {
          var item = document.createElement(fav.watchUrl ? "a" : "div");
          item.className = "favorite-item is-" + fav.status;
          item.draggable = true;
          if (fav.watchUrl) {
            item.href = fav.watchUrl;
            item.target = "_blank";
            item.rel = "noopener noreferrer";
          }

          item.addEventListener("dragstart", function (e) {
            e.dataTransfer.setData("text/plain", JSON.stringify({
              type: "streamer",
              platform: fav.platform,
              channelId: fav.channelId,
              sourceCategory: cat
            }));
            e.dataTransfer.effectAllowed = "move";
            item.classList.add("is-dragging");
          });

          item.addEventListener("dragend", function () {
            item.classList.remove("is-dragging");
          });

          var dot = document.createElement("span");
          dot.className = "favorite-state-dot";
          dot.setAttribute("aria-hidden", "true");

          var copy = document.createElement("span");
          copy.className = "favorite-item-copy";
          var name = document.createElement("strong");
          name.textContent = fav.streamerName;
          var state = document.createElement("span");
          state.textContent = fav.status === "live" ? "라이브" : fav.status === "delayed" ? "상태 확인 지연" : "오프라인";
          copy.append(name, state);

          item.append(dot, copy);

          if (fav.status === "live" && fav.viewerCount !== undefined && fav.viewerCount !== null) {
            var viewerSpan = document.createElement("span");
            viewerSpan.className = "favorite-viewer";
            viewerSpan.textContent = fav.viewerCount.toLocaleString();
            item.append(viewerSpan);
          }

          itemsDiv.append(item);
        });
      } else {
        var emptyPlaceholder = document.createElement("div");
        emptyPlaceholder.className = "favorite-item-empty-placeholder";
        emptyPlaceholder.textContent = "여기에 스트리머를 드래그하세요.";
        itemsDiv.append(emptyPlaceholder);
      }

      groupDiv.append(itemsDiv);
      list.append(groupDiv);
    });
  }

  async function updateCategory(platform, channelId, newCategory) {
    try {
      var response = await fetch("/api/favorites/" + encodeURIComponent(platform) + "/" + encodeURIComponent(channelId) + "/category", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token
        },
        body: JSON.stringify({ category: newCategory })
      });
      if (!response.ok) {
        throw new Error("category update failed");
      }
      await load();
    } catch {
      status.textContent = "카테고리를 변경하지 못했습니다.";
    }
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

  var addCategoryBtn = document.querySelector(".favorites-add-category");
  if (addCategoryBtn) {
    addCategoryBtn.addEventListener("click", function (e) {
      e.stopPropagation();
      var newCat = prompt("새 카테고리 이름을 입력하세요:");
      if (newCat) {
        newCat = newCat.trim();
        if (newCat && newCat.length > 0 && newCat.length <= 100) {
          var customCategories = [];
          try {
            customCategories = JSON.parse(localStorage.getItem("raider.favorites.customCategories") || "[]");
          } catch (err) {}
          if (!customCategories.includes(newCat)) {
            customCategories.push(newCat);
            localStorage.setItem("raider.favorites.customCategories", JSON.stringify(customCategories));
            render();
          }
        }
      }
    });
  }

  setCollapsed(localStorage.getItem("raider.favorites.sidebarCollapsed") === "true");
  syncViewport();
  void load();
}());
